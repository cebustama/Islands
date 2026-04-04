using System;
using NUnit.Framework;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Tilemaps;

using Islands.PCG.Core;
using Islands.PCG.Layout.Maps;
using Islands.PCG.Layout.Maps.Stages;
using Islands.PCG.Adapters.Tilemap;

namespace Islands.PCG.Tests.EditMode.Maps
{
    /// <summary>
    /// EditMode tests for <see cref="TilemapAdapter2D"/>.
    ///
    /// Tests run in two groups:
    ///   1. Null-guard and contract tests — no pipeline required.
    ///   2. Behavioral tests — use a minimal real pipeline run for controlled layer data.
    ///
    /// All tests create and destroy GameObjects locally; no scene persistence.
    /// </summary>
    public sealed class TilemapAdapter2DTests
    {
        // ─────────────────────────────────────────────────────────────────────────
        // Helpers
        // ─────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Creates a live Tilemap attached to a new GameObject (EditMode-safe).
        /// Caller must call <see cref="DestroyTilemap"/> after use.
        /// </summary>
        private static UnityEngine.Tilemaps.Tilemap CreateTilemap()
        {
            var go = new GameObject("TestTilemap");
            go.AddComponent<Grid>();
            return go.AddComponent<UnityEngine.Tilemaps.Tilemap>();
        }

        private static void DestroyTilemap(UnityEngine.Tilemaps.Tilemap t)
        {
            if (t != null) UnityEngine.Object.DestroyImmediate(t.gameObject);
        }

        /// <summary>
        /// Creates a concrete <see cref="Tile"/> asset (no sprites; only identity matters for
        /// reference-equality checks). Must be destroyed by caller.
        /// </summary>
        private static Tile MakeTile(string name = "TestTile")
        {
            var tile = ScriptableObject.CreateInstance<Tile>();
            tile.name = name;
            return tile;
        }

        private static void DestroyTile(Tile t)
        {
            if (t != null) UnityEngine.Object.DestroyImmediate(t);
        }

        /// <summary>
        /// Runs the full pipeline on a small grid (16×16) with a fixed seed and
        /// returns the exported snapshot. Caller owns disposal of <paramref name="ctx"/>.
        /// </summary>
        private static (MapDataExport export, MapContext2D ctx) RunSmallPipeline(uint seed = 1u)
        {
            var domain = new GridDomain2D(16, 16);
            var inputs = new MapInputs(seed, domain, MapTunables2D.Default);
            var stages = new IMapStage2D[]
            {
                new Stage_BaseTerrain2D(),
                new Stage_Hills2D(),
                new Stage_Shore2D(),
                new Stage_Vegetation2D(),
                new Stage_Traversal2D(),
                new Stage_Morphology2D(),
            };
            var ctx = new MapContext2D(domain, Allocator.Temp);
            MapPipelineRunner2D.Run(ref ctx, in inputs, stages, clearLayers: true);
            return (MapExporter2D.Export(ctx), ctx);
        }

        // ─────────────────────────────────────────────────────────────────────────
        // 1. Null-guard tests
        // ─────────────────────────────────────────────────────────────────────────

        [Test]
        public void Apply_NullExport_ThrowsArgumentNullException()
        {
            var tilemap = CreateTilemap();
            try
            {
                Assert.Throws<ArgumentNullException>(() =>
                    TilemapAdapter2D.Apply(null, tilemap, Array.Empty<TilemapLayerEntry>()));
            }
            finally { DestroyTilemap(tilemap); }
        }

        [Test]
        public void Apply_NullTilemap_ThrowsArgumentNullException()
        {
            var (export, ctx) = RunSmallPipeline();
            try
            {
                Assert.Throws<ArgumentNullException>(() =>
                    TilemapAdapter2D.Apply(export, null, Array.Empty<TilemapLayerEntry>()));
            }
            finally { ctx.Dispose(); }
        }

        [Test]
        public void Apply_NullPriorityTable_ThrowsArgumentNullException()
        {
            var tilemap = CreateTilemap();
            var (export, ctx) = RunSmallPipeline();
            try
            {
                Assert.Throws<ArgumentNullException>(() =>
                    TilemapAdapter2D.Apply(export, tilemap, null));
            }
            finally
            {
                DestroyTilemap(tilemap);
                ctx.Dispose();
            }
        }

        // ─────────────────────────────────────────────────────────────────────────
        // 2. Empty priority table — only fallback tile placed (where fallback != null)
        // ─────────────────────────────────────────────────────────────────────────

        [Test]
        public void Apply_EmptyPriorityTable_NullFallback_LeavesAllCellsEmpty()
        {
            var tilemap = CreateTilemap();
            var (export, ctx) = RunSmallPipeline();
            try
            {
                TilemapAdapter2D.Apply(export, tilemap, Array.Empty<TilemapLayerEntry>(),
                    fallbackTile: null, clearFirst: true);

                // Sample a few cells — all must be null.
                for (int y = 0; y < export.Height; y++)
                    for (int x = 0; x < export.Width; x++)
                        Assert.IsNull(tilemap.GetTile(new Vector3Int(x, y, 0)),
                            $"Expected null at ({x},{y}) with empty table and null fallback.");
            }
            finally
            {
                DestroyTilemap(tilemap);
                ctx.Dispose();
            }
        }

        [Test]
        public void Apply_EmptyPriorityTable_WithFallback_FillsAllCells()
        {
            var tilemap = CreateTilemap();
            var fallback = MakeTile("Fallback");
            var (export, ctx) = RunSmallPipeline();
            try
            {
                TilemapAdapter2D.Apply(export, tilemap, Array.Empty<TilemapLayerEntry>(),
                    fallbackTile: fallback, clearFirst: true);

                for (int y = 0; y < export.Height; y++)
                    for (int x = 0; x < export.Width; x++)
                        Assert.AreSame(fallback, tilemap.GetTile(new Vector3Int(x, y, 0)),
                            $"Expected fallback tile at ({x},{y}).");
            }
            finally
            {
                DestroyTilemap(tilemap);
                DestroyTile(fallback);
                ctx.Dispose();
            }
        }

        // ─────────────────────────────────────────────────────────────────────────
        // 3. Priority resolution — Land < LandCore (LandCore ⊆ Land in valid exports)
        // ─────────────────────────────────────────────────────────────────────────

        [Test]
        public void Apply_PriorityResolution_LandCoreCellsGetLandCoreTile()
        {
            // LandCore ⊆ Land: a LandCore cell is also a Land cell.
            // With priority [tileLand (0), tileLandCore (1)], LandCore cells must
            // receive tileLandCore (higher priority) rather than tileLand.

            var tilemap = CreateTilemap();
            var tileLand = MakeTile("Land");
            var tileLandCore = MakeTile("LandCore");
            var (export, ctx) = RunSmallPipeline();
            try
            {
                // Guard: this test is only meaningful when both layers were created.
                Assume.That(export.HasLayer(MapLayerId.Land), "Land not exported — test skip.");
                Assume.That(export.HasLayer(MapLayerId.LandCore), "LandCore not exported — test skip.");

                var table = new[]
                {
                    new TilemapLayerEntry { LayerId = MapLayerId.Land,     Tile = tileLand     },
                    new TilemapLayerEntry { LayerId = MapLayerId.LandCore, Tile = tileLandCore },
                };

                TilemapAdapter2D.Apply(export, tilemap, table, fallbackTile: null, clearFirst: true);

                bool[] landLayer = export.GetLayer(MapLayerId.Land);
                bool[] landCoreLayer = export.GetLayer(MapLayerId.LandCore);
                int width = export.Width, height = export.Height;

                int landOnlyCount = 0, landCoreCount = 0, checkedCells = 0;
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        int idx = x + y * width;
                        bool isLand = landLayer[idx];
                        bool isLandCore = landCoreLayer[idx];
                        TileBase placed = tilemap.GetTile(new Vector3Int(x, y, 0));

                        if (isLandCore)
                        {
                            // Must be LandCore tile (higher priority overwrites Land).
                            Assert.AreSame(tileLandCore, placed,
                                $"Cell ({x},{y}) is LandCore — expected LandCore tile.");
                            landCoreCount++;
                        }
                        else if (isLand)
                        {
                            // Land-only cell: must be Land tile.
                            Assert.AreSame(tileLand, placed,
                                $"Cell ({x},{y}) is Land-only — expected Land tile.");
                            landOnlyCount++;
                        }
                        checkedCells++;
                    }
                }

                // Sanity: the 16×16 pipeline with default seed should produce both regions.
                Assert.Greater(landOnlyCount, 0, "Expected at least one Land-only cell.");
                Assert.Greater(landCoreCount, 0, "Expected at least one LandCore cell.");
                Assert.AreEqual(width * height, checkedCells);
            }
            finally
            {
                DestroyTilemap(tilemap);
                DestroyTile(tileLand);
                DestroyTile(tileLandCore);
                ctx.Dispose();
            }
        }

        // ─────────────────────────────────────────────────────────────────────────
        // 4. Missing layer in export — silently skipped, no exception
        // ─────────────────────────────────────────────────────────────────────────

        [Test]
        public void Apply_MissingLayer_InPriorityTable_IsSkippedSilently()
        {
            // MapLayerId.Paths is registered but never written — it is absent from any export.
            // Placing it in the table must not throw and must not affect tile output.

            var tilemap = CreateTilemap();
            var tileLand = MakeTile("Land");
            var tilePaths = MakeTile("Paths");
            var (export, ctx) = RunSmallPipeline();
            try
            {
                Assume.That(!export.HasLayer(MapLayerId.Paths),
                    "Paths layer should not be exported — check pipeline stages.");

                var table = new[]
                {
                    new TilemapLayerEntry { LayerId = MapLayerId.Land,  Tile = tileLand  },
                    new TilemapLayerEntry { LayerId = MapLayerId.Paths, Tile = tilePaths }, // absent
                };

                Assert.DoesNotThrow(() =>
                    TilemapAdapter2D.Apply(export, tilemap, table));

                // Paths tile must never appear (layer absent = no match = never wins).
                bool[] landLayer = export.GetLayer(MapLayerId.Land);
                for (int y = 0; y < export.Height; y++)
                    for (int x = 0; x < export.Width; x++)
                    {
                        TileBase placed = tilemap.GetTile(new Vector3Int(x, y, 0));
                        Assert.AreNotSame(tilePaths, placed,
                            $"Paths tile appeared at ({x},{y}) but Paths layer is absent.");
                    }
            }
            finally
            {
                DestroyTilemap(tilemap);
                DestroyTile(tileLand);
                DestroyTile(tilePaths);
                ctx.Dispose();
            }
        }

        // ─────────────────────────────────────────────────────────────────────────
        // 5. clearFirst = false — does not erase pre-existing tiles outside the export
        // ─────────────────────────────────────────────────────────────────────────

        [Test]
        public void Apply_ClearFirstFalse_PreservesExistingTiles()
        {
            var tilemap = CreateTilemap();
            var sentinel = MakeTile("Sentinel");
            var tileLand = MakeTile("Land");
            var (export, ctx) = RunSmallPipeline();
            try
            {
                // Place a sentinel tile at a position known to be outside the export's domain.
                // Export domain: (0,0)..(Width-1,Height-1). We place outside at (-1,-1).
                var outsidePos = new Vector3Int(-1, -1, 0);
                tilemap.SetTile(outsidePos, sentinel);

                TilemapAdapter2D.Apply(export, tilemap,
                    new[] { new TilemapLayerEntry { LayerId = MapLayerId.Land, Tile = tileLand } },
                    fallbackTile: null, clearFirst: false);

                Assert.AreSame(sentinel, tilemap.GetTile(outsidePos),
                    "Sentinel tile outside export domain must survive when clearFirst=false.");
            }
            finally
            {
                DestroyTilemap(tilemap);
                DestroyTile(sentinel);
                DestroyTile(tileLand);
                ctx.Dispose();
            }
        }

        // ─────────────────────────────────────────────────────────────────────────
        // 6. flipY — tiles placed at mirrored Y positions
        // ─────────────────────────────────────────────────────────────────────────

        [Test]
        public void Apply_FlipY_PlacesTilesAtMirroredYCoordinate()
        {
            var tilemapNormal = CreateTilemap();
            var tilemapFlipped = CreateTilemap();
            var tileLand = MakeTile("Land");
            var (export, ctx) = RunSmallPipeline();
            try
            {
                Assume.That(export.HasLayer(MapLayerId.Land), "Land not exported — test skip.");

                var table = new[]
                {
                    new TilemapLayerEntry { LayerId = MapLayerId.Land, Tile = tileLand }
                };

                TilemapAdapter2D.Apply(export, tilemapNormal, table, clearFirst: true, flipY: false);
                TilemapAdapter2D.Apply(export, tilemapFlipped, table, clearFirst: true, flipY: true);

                int height = export.Height;

                // Primary assertion: for every cell (x, y), the tile placed by the normal
                // run at (x, y) must equal the tile placed by the flipped run at (x, height-1-y).
                // This is sufficient to prove the Y-mirror is applied correctly.
                for (int y = 0; y < height; y++)
                    for (int x = 0; x < export.Width; x++)
                    {
                        TileBase normal = tilemapNormal.GetTile(new Vector3Int(x, y, 0));
                        TileBase flipped = tilemapFlipped.GetTile(new Vector3Int(x, height - 1 - y, 0));
                        Assert.AreSame(normal, flipped,
                            $"flipY mismatch: normal[{x},{y}] should equal flipped[{x},{height - 1 - y}].");
                    }
            }
            finally
            {
                DestroyTilemap(tilemapNormal);
                DestroyTilemap(tilemapFlipped);
                DestroyTile(tileLand);
                ctx.Dispose();
            }
        }

        // ─────────────────────────────────────────────────────────────────────────
        // 7. Determinism gate — same export + same table = identical tilemap output
        // ─────────────────────────────────────────────────────────────────────────

        [Test]
        public void Apply_IsDeterministic_SameExportSameTable()
        {
            var tilemapA = CreateTilemap();
            var tilemapB = CreateTilemap();
            var tileLand = MakeTile("Land");
            var (export, ctx) = RunSmallPipeline(seed: 42u);
            try
            {
                Assume.That(export.HasLayer(MapLayerId.Land), "Land not exported — test skip.");

                var table = new[]
                {
                    new TilemapLayerEntry { LayerId = MapLayerId.Land, Tile = tileLand }
                };

                TilemapAdapter2D.Apply(export, tilemapA, table, clearFirst: true);
                TilemapAdapter2D.Apply(export, tilemapB, table, clearFirst: true);

                for (int y = 0; y < export.Height; y++)
                    for (int x = 0; x < export.Width; x++)
                    {
                        var posA = tilemapA.GetTile(new Vector3Int(x, y, 0));
                        var posB = tilemapB.GetTile(new Vector3Int(x, y, 0));
                        Assert.AreSame(posA, posB,
                            $"Tilemap output must be deterministic at ({x},{y}).");
                    }
            }
            finally
            {
                DestroyTilemap(tilemapA);
                DestroyTilemap(tilemapB);
                DestroyTile(tileLand);
                ctx.Dispose();
            }
        }
    }
}