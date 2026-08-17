// Phase Q — BiomeTileOverride unit tests.
// Spec: Phase_Q_Design.md §7.1.
//
// Tests the SO lookup correctness: known/unknown biome-layer pairs,
// empty override, tile priority (H6 convention), duplicate biome groups,
// Unclassified biome fallthrough, and adapter backward-compatibility
// (null override, absent biome field).
//
// Q-T-4 and Q-T-9 require InternalsVisibleTo on the layout assembly
// to construct MapDataExport, plus a live Tilemap for stamping.
//
// Q-fix.a: three regression tests appended at the end of the fixture
// (Q-BUG-1 null base tile, Apply() parity guard, Q-BUG-3 RoundToInt).

using Islands.PCG.Adapters.Tilemap;
using Islands.PCG.Layout.Maps;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Islands.PCG.Tests.EditMode.Adapters.Tilemap
{
    [TestFixture]
    public sealed class BiomeTileOverrideTests
    {
        // -----------------------------------------------------------------
        // Helpers — tiles and overrides
        // -----------------------------------------------------------------

        /// <summary>
        /// Creates a throwaway Tile instance for test assertions.
        /// Caller must DestroyImmediate when done.
        /// </summary>
        private static Tile MakeTile(string name = "test")
        {
            var t = ScriptableObject.CreateInstance<Tile>();
            t.name = name;
            return t;
        }

        private static BiomeTileOverride MakeOverride(
            params BiomeTileOverride.BiomeGroup[] groups)
        {
            var so = ScriptableObject.CreateInstance<BiomeTileOverride>();
            so.groups = groups;
            so.RebuildLookup();
            return so;
        }

        /// <summary>Destroys all supplied UnityEngine.Objects.</summary>
        private static void Cleanup(params Object[] objects)
        {
            foreach (var obj in objects)
                if (obj != null) Object.DestroyImmediate(obj);
        }

        // -----------------------------------------------------------------
        // Helpers — MapDataExport construction (requires InternalsVisibleTo)
        // -----------------------------------------------------------------

        /// <summary>
        /// Builds a minimal <see cref="MapDataExport"/> with only a Land mask
        /// and an optional Biome field. All other layers/fields are null.
        /// </summary>
        private static MapDataExport MakeExport(
            int w, int h, bool[] landMask, float[] biomeField = null)
        {
            var layers = new bool[(int)MapLayerId.COUNT][];
            layers[(int)MapLayerId.Land] = landMask;

            var fields = new float[(int)MapFieldId.COUNT][];
            if (biomeField != null)
                fields[(int)MapFieldId.Biome] = biomeField;

            return new MapDataExport(w, h, /*seed*/ 1u, layers, fields);
        }

        /// <summary>
        /// Builds a <see cref="MapDataExport"/> with an arbitrary set of masks plus an
        /// optional Biome field. Q-fix.a helper.
        /// </summary>
        private static MapDataExport MakeExportWithLayers(
            int w, int h, (MapLayerId id, bool[] mask)[] masks, float[] biomeField = null)
        {
            var layers = new bool[(int)MapLayerId.COUNT][];
            foreach (var m in masks)
                layers[(int)m.id] = m.mask;

            var fields = new float[(int)MapFieldId.COUNT][];
            if (biomeField != null)
                fields[(int)MapFieldId.Biome] = biomeField;

            return new MapDataExport(w, h, /*seed*/ 1u, layers, fields);
        }

        // -----------------------------------------------------------------
        // Helpers — Tilemap fixture
        // -----------------------------------------------------------------

        /// <summary>
        /// Creates a Grid + child Tilemap pair. Returns both so the caller
        /// can destroy the Grid root (which destroys the child too).
        /// </summary>
        private static (GameObject gridGo, UnityEngine.Tilemaps.Tilemap tilemap) MakeTilemap(
            string name = "TestTilemap")
        {
            var gridGo = new GameObject(name + "_Grid");
            gridGo.AddComponent<Grid>();

            var tmGo = new GameObject(name);
            tmGo.transform.SetParent(gridGo.transform);
            var tm = tmGo.AddComponent<UnityEngine.Tilemaps.Tilemap>();
            tmGo.AddComponent<TilemapRenderer>();

            return (gridGo, tm);
        }

        /// <summary>
        /// Asserts that two tilemaps contain the same tile at every cell
        /// in the given (w × h) region.
        /// </summary>
        private static void AssertTilemapsEqual(
            UnityEngine.Tilemaps.Tilemap a,
            UnityEngine.Tilemaps.Tilemap b,
            int w, int h)
        {
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    var pos = new Vector3Int(x, y, 0);
                    TileBase tileA = a.GetTile(pos);
                    TileBase tileB = b.GetTile(pos);
                    Assert.AreSame(tileA, tileB,
                        $"Tile mismatch at ({x},{y}): " +
                        $"expected '{(tileA != null ? tileA.name : "null")}', " +
                        $"got '{(tileB != null ? tileB.name : "null")}'.");
                }
            }
        }

        // -----------------------------------------------------------------
        // Q-T-1: Resolve_KnownBiomeLayer_ReturnsOverrideTile
        // -----------------------------------------------------------------

        [Test]
        public void Resolve_KnownBiomeLayer_ReturnsOverrideTile()
        {
            var snowLandTile = MakeTile("snow_land");
            var so = MakeOverride(new BiomeTileOverride.BiomeGroup
            {
                biome = BiomeType.Snow,
                layers = new[]
                {
                    new BiomeTileOverride.LayerSlot
                    {
                        layerId = MapLayerId.Land,
                        tile = snowLandTile,
                    }
                }
            });

            try
            {
                TileBase result = so.Resolve(BiomeType.Snow, MapLayerId.Land);
                Assert.AreSame(snowLandTile, result,
                    "Resolve(Snow, Land) must return the assigned override tile.");
            }
            finally
            {
                Cleanup(so, snowLandTile);
            }
        }

        // -----------------------------------------------------------------
        // Q-T-2: Resolve_UnknownBiomeLayer_ReturnsNull
        // -----------------------------------------------------------------

        [Test]
        public void Resolve_UnknownBiomeLayer_ReturnsNull()
        {
            var snowLandTile = MakeTile("snow_land");
            var so = MakeOverride(new BiomeTileOverride.BiomeGroup
            {
                biome = BiomeType.Snow,
                layers = new[]
                {
                    new BiomeTileOverride.LayerSlot
                    {
                        layerId = MapLayerId.Land,
                        tile = snowLandTile,
                    }
                }
            });

            try
            {
                TileBase result = so.Resolve(BiomeType.SubtropicalDesert, MapLayerId.Land);
                Assert.IsNull(result,
                    "Resolve for an unconfigured biome-layer pair must return null.");
            }
            finally
            {
                Cleanup(so, snowLandTile);
            }
        }

        // -----------------------------------------------------------------
        // Q-T-3: Resolve_EmptyOverride_ReturnsNull
        // -----------------------------------------------------------------

        [Test]
        public void Resolve_EmptyOverride_ReturnsNull()
        {
            var so = MakeOverride(); // zero groups

            try
            {
                TileBase result = so.Resolve(BiomeType.TemperateForest, MapLayerId.Vegetation);
                Assert.IsNull(result,
                    "Empty override (zero groups) must return null for all inputs.");
            }
            finally
            {
                Cleanup(so);
            }
        }

        [Test]
        public void Resolve_NullGroups_ReturnsNull()
        {
            var so = ScriptableObject.CreateInstance<BiomeTileOverride>();
            so.groups = null;
            so.RebuildLookup();

            try
            {
                TileBase result = so.Resolve(BiomeType.Grassland, MapLayerId.Land);
                Assert.IsNull(result,
                    "Null groups array must return null for all inputs.");
            }
            finally
            {
                Cleanup(so);
            }
        }

        // -----------------------------------------------------------------
        // Q-T-4: ApplyBiomeAware with null override = identical to Apply
        // -----------------------------------------------------------------

        [Test]
        public void ApplyBiomeAware_NullOverride_IdenticalToApply()
        {
            // 2×2 grid, all land, biome field present (Snow everywhere).
            int w = 2, h = 2;
            var landMask = new bool[] { true, true, true, true };
            var biomeField = new float[] { 1f, 1f, 1f, 1f }; // Snow = 1
            var export = MakeExport(w, h, landMask, biomeField);

            var landTile = MakeTile("land");
            var fallback = MakeTile("fallback");
            var table = new TilemapLayerEntry[]
            {
                new TilemapLayerEntry { LayerId = MapLayerId.Land, Tile = landTile }
            };

            var (gridA, tmA) = MakeTilemap("Baseline");
            var (gridB, tmB) = MakeTilemap("BiomeAware");

            try
            {
                TilemapAdapter2D.Apply(export, tmA, table, fallback, true, false);
                TilemapAdapter2D.ApplyBiomeAware(export, tmB, table, null, fallback, true, false);

                AssertTilemapsEqual(tmA, tmB, w, h);
            }
            finally
            {
                Cleanup(landTile, fallback);
                Object.DestroyImmediate(gridA);
                Object.DestroyImmediate(gridB);
            }
        }

        // -----------------------------------------------------------------
        // Q-T-5: Resolve_RuleTilePriority_WinsOverStaticAndAnimated
        // -----------------------------------------------------------------

        [Test]
        public void Resolve_RuleTilePriority_WinsOverStaticAndAnimated()
        {
            var staticTile = MakeTile("static");
            var animTile = MakeTile("animated");
            var ruleTile = MakeTile("rule");

            var so = MakeOverride(new BiomeTileOverride.BiomeGroup
            {
                biome = BiomeType.BorealForest,
                layers = new[]
                {
                    new BiomeTileOverride.LayerSlot
                    {
                        layerId = MapLayerId.Vegetation,
                        tile = staticTile,
                        animatedTile = animTile,
                        ruleTile = ruleTile,
                    }
                }
            });

            try
            {
                TileBase result = so.Resolve(BiomeType.BorealForest, MapLayerId.Vegetation);
                Assert.AreSame(ruleTile, result,
                    "When all three slots assigned, ruleTile must win (H6 priority).");
            }
            finally
            {
                Cleanup(so, staticTile, animTile, ruleTile);
            }
        }

        // -----------------------------------------------------------------
        // Q-T-6: Resolve_AnimatedTilePriority_WinsOverStatic
        // -----------------------------------------------------------------

        [Test]
        public void Resolve_AnimatedTilePriority_WinsOverStatic()
        {
            var staticTile = MakeTile("static");
            var animTile = MakeTile("animated");

            var so = MakeOverride(new BiomeTileOverride.BiomeGroup
            {
                biome = BiomeType.TropicalRainforest,
                layers = new[]
                {
                    new BiomeTileOverride.LayerSlot
                    {
                        layerId = MapLayerId.Land,
                        tile = staticTile,
                        animatedTile = animTile,
                        ruleTile = null,
                    }
                }
            });

            try
            {
                TileBase result = so.Resolve(BiomeType.TropicalRainforest, MapLayerId.Land);
                Assert.AreSame(animTile, result,
                    "With animatedTile + tile (no ruleTile), animatedTile must win.");
            }
            finally
            {
                Cleanup(so, staticTile, animTile);
            }
        }

        // -----------------------------------------------------------------
        // Q-T-7: Resolve_DuplicateBiomeGroup_LastWins
        // -----------------------------------------------------------------

        [Test]
        public void Resolve_DuplicateBiomeGroup_LastWins()
        {
            var firstTile = MakeTile("first");
            var lastTile = MakeTile("last");

            var so = MakeOverride(
                new BiomeTileOverride.BiomeGroup
                {
                    biome = BiomeType.Snow,
                    layers = new[]
                    {
                        new BiomeTileOverride.LayerSlot
                        {
                            layerId = MapLayerId.Land,
                            tile = firstTile,
                        }
                    }
                },
                new BiomeTileOverride.BiomeGroup
                {
                    biome = BiomeType.Snow,
                    layers = new[]
                    {
                        new BiomeTileOverride.LayerSlot
                        {
                            layerId = MapLayerId.Land,
                            tile = lastTile,
                        }
                    }
                });

            try
            {
                TileBase result = so.Resolve(BiomeType.Snow, MapLayerId.Land);
                Assert.AreSame(lastTile, result,
                    "Duplicate biome groups must resolve last-wins by iteration order.");
            }
            finally
            {
                Cleanup(so, firstTile, lastTile);
            }
        }

        // -----------------------------------------------------------------
        // Q-T-8: Resolve_UnclassifiedBiome_FallsThrough
        // -----------------------------------------------------------------

        [Test]
        public void Resolve_UnclassifiedBiome_FallsThrough()
        {
            var snowTile = MakeTile("snow_land");
            var so = MakeOverride(new BiomeTileOverride.BiomeGroup
            {
                biome = BiomeType.Snow,
                layers = new[]
                {
                    new BiomeTileOverride.LayerSlot
                    {
                        layerId = MapLayerId.Land,
                        tile = snowTile,
                    }
                }
            });

            try
            {
                TileBase result = so.Resolve(BiomeType.Unclassified, MapLayerId.Land);
                Assert.IsNull(result,
                    "Unclassified biome with no override entry must return null (fallthrough).");
            }
            finally
            {
                Cleanup(so, snowTile);
            }
        }

        // -----------------------------------------------------------------
        // Q-T-9: ApplyBiomeAware with no biome field = identical to Apply
        // -----------------------------------------------------------------

        [Test]
        public void ApplyBiomeAware_NoBiomeField_IdenticalToApply()
        {
            // 2×2 grid, all land, NO biome field → all cells Unclassified.
            int w = 2, h = 2;
            var landMask = new bool[] { true, true, true, true };
            var export = MakeExport(w, h, landMask, biomeField: null);

            var landTile = MakeTile("land");
            var snowTile = MakeTile("snow_override");
            var fallback = MakeTile("fallback");
            var table = new TilemapLayerEntry[]
            {
                new TilemapLayerEntry { LayerId = MapLayerId.Land, Tile = landTile }
            };

            // Override with Snow entry — but biome field is absent so all cells
            // read as Unclassified (0). Override should have no effect.
            var bto = MakeOverride(new BiomeTileOverride.BiomeGroup
            {
                biome = BiomeType.Snow,
                layers = new[]
                {
                    new BiomeTileOverride.LayerSlot
                    {
                        layerId = MapLayerId.Land,
                        tile = snowTile,
                    }
                }
            });

            var (gridA, tmA) = MakeTilemap("Baseline");
            var (gridB, tmB) = MakeTilemap("BiomeAware");

            try
            {
                TilemapAdapter2D.Apply(export, tmA, table, fallback, true, false);
                TilemapAdapter2D.ApplyBiomeAware(export, tmB, table, bto, fallback, true, false);

                AssertTilemapsEqual(tmA, tmB, w, h);
            }
            finally
            {
                Cleanup(landTile, snowTile, fallback, bto);
                Object.DestroyImmediate(gridA);
                Object.DestroyImmediate(gridB);
            }
        }

        // -----------------------------------------------------------------
        // Supplementary: multiple biomes × multiple layers
        // -----------------------------------------------------------------

        [Test]
        public void Resolve_MultipleBiomesAndLayers_ReturnsCorrectTile()
        {
            var snowLand = MakeTile("snow_land");
            var snowVeg = MakeTile("snow_veg");
            var desertLand = MakeTile("desert_land");

            var so = MakeOverride(
                new BiomeTileOverride.BiomeGroup
                {
                    biome = BiomeType.Snow,
                    layers = new[]
                    {
                        new BiomeTileOverride.LayerSlot { layerId = MapLayerId.Land, tile = snowLand },
                        new BiomeTileOverride.LayerSlot { layerId = MapLayerId.Vegetation, tile = snowVeg },
                    }
                },
                new BiomeTileOverride.BiomeGroup
                {
                    biome = BiomeType.SubtropicalDesert,
                    layers = new[]
                    {
                        new BiomeTileOverride.LayerSlot { layerId = MapLayerId.Land, tile = desertLand },
                    }
                });

            try
            {
                Assert.AreSame(snowLand, so.Resolve(BiomeType.Snow, MapLayerId.Land));
                Assert.AreSame(snowVeg, so.Resolve(BiomeType.Snow, MapLayerId.Vegetation));
                Assert.AreSame(desertLand, so.Resolve(BiomeType.SubtropicalDesert, MapLayerId.Land));

                Assert.IsNull(so.Resolve(BiomeType.SubtropicalDesert, MapLayerId.Vegetation),
                    "Desert+Vegetation not configured — must return null.");
                Assert.IsNull(so.Resolve(BiomeType.Grassland, MapLayerId.Land),
                    "Grassland not configured — must return null.");
            }
            finally
            {
                Cleanup(so, snowLand, snowVeg, desertLand);
            }
        }

        // -----------------------------------------------------------------
        // Supplementary: all-null slot produces no override
        // -----------------------------------------------------------------

        [Test]
        public void Resolve_AllNullSlot_ReturnsNull()
        {
            var so = MakeOverride(new BiomeTileOverride.BiomeGroup
            {
                biome = BiomeType.Tundra,
                layers = new[]
                {
                    new BiomeTileOverride.LayerSlot
                    {
                        layerId = MapLayerId.Land,
                        tile = null,
                        animatedTile = null,
                        ruleTile = null,
                    }
                }
            });

            try
            {
                TileBase result = so.Resolve(BiomeType.Tundra, MapLayerId.Land);
                Assert.IsNull(result,
                    "LayerSlot with all null tiles must produce no override (null).");
            }
            finally
            {
                Cleanup(so);
            }
        }

        // -----------------------------------------------------------------
        // Supplementary: biome override actually changes stamped tile
        // -----------------------------------------------------------------

        [Test]
        public void ApplyBiomeAware_OverrideTile_StampedAtMatchingBiomeCells()
        {
            // 2×2 grid: cells 0,1 = Snow (1), cells 2,3 = Grassland (9).
            int w = 2, h = 2;
            var landMask = new bool[] { true, true, true, true };
            var biomeField = new float[] { 1f, 1f, 9f, 9f };
            var export = MakeExport(w, h, landMask, biomeField);

            var baseLand = MakeTile("base_land");
            var snowLand = MakeTile("snow_land");
            var table = new TilemapLayerEntry[]
            {
                new TilemapLayerEntry { LayerId = MapLayerId.Land, Tile = baseLand }
            };

            var bto = MakeOverride(new BiomeTileOverride.BiomeGroup
            {
                biome = BiomeType.Snow,
                layers = new[]
                {
                    new BiomeTileOverride.LayerSlot
                    {
                        layerId = MapLayerId.Land,
                        tile = snowLand,
                    }
                }
            });

            var (gridGo, tm) = MakeTilemap("Override");

            try
            {
                TilemapAdapter2D.ApplyBiomeAware(export, tm, table, bto, null, true, false);

                // Snow cells (row 0) get override tile.
                Assert.AreSame(snowLand, tm.GetTile(new Vector3Int(0, 0, 0)),
                    "Snow cell (0,0) must get override tile.");
                Assert.AreSame(snowLand, tm.GetTile(new Vector3Int(1, 0, 0)),
                    "Snow cell (1,0) must get override tile.");

                // Grassland cells (row 1) have no override → base tile.
                Assert.AreSame(baseLand, tm.GetTile(new Vector3Int(0, 1, 0)),
                    "Grassland cell (0,1) must fall through to base tile.");
                Assert.AreSame(baseLand, tm.GetTile(new Vector3Int(1, 1, 0)),
                    "Grassland cell (1,1) must fall through to base tile.");
            }
            finally
            {
                Cleanup(baseLand, snowLand, bto);
                Object.DestroyImmediate(gridGo);
            }
        }
        // =================================================================
        // Q-fix.a — regression tests for Q-BUG-1 / Q-BUG-2 / Q-BUG-3
        // =================================================================

        // -----------------------------------------------------------------
        // Q-fix.a-T-1: override applies even when the base layer entry has no tile.
        // Regression: Q-BUG-1. Before the fix, ApplyBiomeAware skipped caching any
        // layer whose base Tile was null, so the override was never consulted.
        // -----------------------------------------------------------------

        [Test]
        public void ApplyBiomeAware_NullBaseTile_OverrideStillApplies()
        {
            // 2x1 grid: both cells Vegetation, both Snow (1).
            int w = 2, h = 1;
            var vegMask = new bool[] { true, true };
            var biomeField = new float[] { 1f, 1f };
            var export = MakeExportWithLayers(
                w, h, new[] { (MapLayerId.Vegetation, vegMask) }, biomeField);

            var snowVeg = MakeTile("snow_vegetation");

            // Base entry deliberately carries NO tile — the override is the only source.
            var table = new TilemapLayerEntry[]
            {
                new TilemapLayerEntry { LayerId = MapLayerId.Vegetation, Tile = null }
            };

            var bto = MakeOverride(new BiomeTileOverride.BiomeGroup
            {
                biome = BiomeType.Snow,
                layers = new[]
                {
                    new BiomeTileOverride.LayerSlot
                    {
                        layerId = MapLayerId.Vegetation,
                        tile = snowVeg,
                    }
                }
            });

            var (gridGo, tm) = MakeTilemap("NullBaseTile");

            try
            {
                TilemapAdapter2D.ApplyBiomeAware(export, tm, table, bto, null, true, false);

                Assert.AreSame(snowVeg, tm.GetTile(new Vector3Int(0, 0, 0)),
                    "Override must apply even when the base layer entry has a null tile.");
                Assert.AreSame(snowVeg, tm.GetTile(new Vector3Int(1, 0, 0)),
                    "Override must apply even when the base layer entry has a null tile.");
            }
            finally
            {
                Cleanup(snowVeg, bto);
                Object.DestroyImmediate(gridGo);
            }
        }

        // -----------------------------------------------------------------
        // Q-fix.a-T-2: a layer that resolves to nothing must not erase a
        // lower-priority winner. This is the parity guard that keeps the
        // Q-BUG-1 fix equivalent to Apply().
        // -----------------------------------------------------------------

        [Test]
        public void ApplyBiomeAware_NullBaseTile_NoOverride_DoesNotEraseLowerLayer()
        {
            // 1x1 grid: Land (has base tile) + Vegetation (no tile, no override) both ON.
            int w = 1, h = 1;
            var landMask = new bool[] { true };
            var vegMask = new bool[] { true };
            var biomeField = new float[] { 9f }; // Grassland — not covered by the override.
            var export = MakeExportWithLayers(
                w, h,
                new[] { (MapLayerId.Land, landMask), (MapLayerId.Vegetation, vegMask) },
                biomeField);

            var baseLand = MakeTile("base_land");
            var snowLand = MakeTile("snow_land");

            // Vegetation sits ABOVE Land in priority but supplies no tile.
            var table = new TilemapLayerEntry[]
            {
                new TilemapLayerEntry { LayerId = MapLayerId.Land, Tile = baseLand },
                new TilemapLayerEntry { LayerId = MapLayerId.Vegetation, Tile = null },
            };

            // Override exists but covers a different biome — nothing resolves here.
            var bto = MakeOverride(new BiomeTileOverride.BiomeGroup
            {
                biome = BiomeType.Snow,
                layers = new[]
                {
                    new BiomeTileOverride.LayerSlot
                    {
                        layerId = MapLayerId.Land,
                        tile = snowLand,
                    }
                }
            });

            var (gridGo, tm) = MakeTilemap("ParityGuard");

            try
            {
                TilemapAdapter2D.ApplyBiomeAware(export, tm, table, bto, null, true, false);

                Assert.AreSame(baseLand, tm.GetTile(new Vector3Int(0, 0, 0)),
                    "A higher-priority layer with neither base tile nor override must not " +
                    "erase the lower-priority winner (Apply() parity).");
            }
            finally
            {
                Cleanup(baseLand, snowLand, bto);
                Object.DestroyImmediate(gridGo);
            }
        }

        // -----------------------------------------------------------------
        // Q-fix.a-T-3: biome field float -> int uses RoundToInt, matching the
        // convention in PCGHoverTooltip (V.a) and PCGRuntimeOverlay (V.b).
        // Regression: Q-BUG-3 (truncation would read 0.9999 as biome 0).
        // -----------------------------------------------------------------

        [Test]
        public void ApplyBiomeAware_BiomeField_UsesRoundToIntConvention()
        {
            // Single cell whose biome value sits just below the exact integer.
            int w = 1, h = 1;
            var landMask = new bool[] { true };
            var biomeField = new float[] { 0.9999f }; // must read as Snow (1), not Unclassified (0).
            var export = MakeExportWithLayers(
                w, h, new[] { (MapLayerId.Land, landMask) }, biomeField);

            var baseLand = MakeTile("base_land");
            var snowLand = MakeTile("snow_land");

            var table = new TilemapLayerEntry[]
            {
                new TilemapLayerEntry { LayerId = MapLayerId.Land, Tile = baseLand }
            };

            var bto = MakeOverride(new BiomeTileOverride.BiomeGroup
            {
                biome = BiomeType.Snow,
                layers = new[]
                {
                    new BiomeTileOverride.LayerSlot
                    {
                        layerId = MapLayerId.Land,
                        tile = snowLand,
                    }
                }
            });

            var (gridGo, tm) = MakeTilemap("RoundToInt");

            try
            {
                TilemapAdapter2D.ApplyBiomeAware(export, tm, table, bto, null, true, false);

                Assert.AreSame(snowLand, tm.GetTile(new Vector3Int(0, 0, 0)),
                    "Biome field must be read with RoundToInt, matching V.a/V.b. " +
                    "Truncation would misread 0.9999 as Unclassified.");
            }
            finally
            {
                Cleanup(baseLand, snowLand, bto);
                Object.DestroyImmediate(gridGo);
            }
        }
    }
}