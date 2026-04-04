using NUnit.Framework;
using UnityEngine;
using UnityEngine.Tilemaps;
using Islands.PCG.Layout.Maps;

namespace Islands.PCG.Adapters.Tilemap.Tests
{
    /// <summary>
    /// EditMode tests for <see cref="ProceduralTileFactory"/>.
    ///
    /// Verifies cache identity, color fidelity, sprite presence, table conversion,
    /// and ClearCache correctness.
    ///
    /// Phase H2d.
    /// </summary>
    public class ProceduralTileFactoryTests
    {
        // Reset the factory between every test so tests are independent of run order.
        [SetUp]
        public void SetUp() => ProceduralTileFactory.ClearCache();

        [TearDown]
        public void TearDown() => ProceduralTileFactory.ClearCache();

        // =====================================================================
        // GetOrCreate — cache identity
        // =====================================================================

        [Test]
        public void GetOrCreate_SameColor_ReturnsSameInstance()
        {
            Tile a = ProceduralTileFactory.GetOrCreate(Color.blue);
            Tile b = ProceduralTileFactory.GetOrCreate(Color.blue);

            Assert.That(b, Is.SameAs(a),
                "Two calls with the same Color should return the identical cached Tile instance.");
        }

        [Test]
        public void GetOrCreate_DifferentColors_ReturnDifferentInstances()
        {
            Tile blue = ProceduralTileFactory.GetOrCreate(Color.blue);
            Tile red = ProceduralTileFactory.GetOrCreate(Color.red);

            Assert.That(red, Is.Not.SameAs(blue),
                "Different colors must produce distinct Tile instances.");
        }

        [Test]
        public void GetOrCreate_SameColorViaColor32Equality_ReturnsSameInstance()
        {
            // Two Color values that are identical when quantised to Color32 must share
            // the same cache slot — the factory uses Color32 as the key.
            Color c1 = new Color(0.5f, 0.25f, 0.75f, 1f);
            Color c2 = new Color(0.5f, 0.25f, 0.75f, 1f);

            Tile t1 = ProceduralTileFactory.GetOrCreate(c1);
            Tile t2 = ProceduralTileFactory.GetOrCreate(c2);

            Assert.That(t2, Is.SameAs(t1),
                "Identical Color values must resolve to the same cached instance.");
        }

        // =====================================================================
        // GetOrCreate — tile properties
        // =====================================================================

        [Test]
        public void GetOrCreate_TileHasNonNullSprite()
        {
            Tile t = ProceduralTileFactory.GetOrCreate(Color.green);

            Assert.That(t.sprite, Is.Not.Null,
                "Generated tile must have a backing sprite (white pixel).");
        }

        [Test]
        public void GetOrCreate_TileColorMatchesRequest_ViaColor32()
        {
            Color requested = Color.cyan;
            Color32 expected = requested;

            Tile t = ProceduralTileFactory.GetOrCreate(requested);
            Color32 actual = t.color;

            Assert.That(actual.r, Is.EqualTo(expected.r), "R channel mismatch");
            Assert.That(actual.g, Is.EqualTo(expected.g), "G channel mismatch");
            Assert.That(actual.b, Is.EqualTo(expected.b), "B channel mismatch");
            Assert.That(actual.a, Is.EqualTo(expected.a), "A channel mismatch");
        }

        [Test]
        public void GetOrCreate_ReturnsTileInstance()
        {
            Tile t = ProceduralTileFactory.GetOrCreate(Color.white);

            Assert.That(t, Is.Not.Null, "GetOrCreate must never return null.");
            Assert.That(t, Is.InstanceOf<Tile>(),
                "Returned object must be a UnityEngine.Tilemaps.Tile.");
        }

        // =====================================================================
        // BuildPriorityTable
        // =====================================================================

        [Test]
        public void BuildPriorityTable_NullInput_ReturnsEmptyArray()
        {
            TilemapLayerEntry[] result = ProceduralTileFactory.BuildPriorityTable(null);

            Assert.That(result, Is.Not.Null, "Result must not be null for null input.");
            Assert.That(result.Length, Is.Zero, "Result must be empty for null input.");
        }

        [Test]
        public void BuildPriorityTable_EmptyInput_ReturnsEmptyArray()
        {
            TilemapLayerEntry[] result = ProceduralTileFactory.BuildPriorityTable(
                System.Array.Empty<ProceduralTileEntry>());

            Assert.That(result, Is.Not.Null);
            Assert.That(result.Length, Is.Zero);
        }

        [Test]
        public void BuildPriorityTable_MapsLayerIdsInOrder()
        {
            var entries = new[]
            {
                new ProceduralTileEntry { LayerId = MapLayerId.DeepWater,   Color = Color.blue   },
                new ProceduralTileEntry { LayerId = MapLayerId.Land,         Color = Color.green  },
                new ProceduralTileEntry { LayerId = MapLayerId.Vegetation,   Color = Color.green  },
            };

            TilemapLayerEntry[] result = ProceduralTileFactory.BuildPriorityTable(entries);

            Assert.That(result.Length, Is.EqualTo(3), "Entry count must match input length.");
            Assert.That(result[0].LayerId, Is.EqualTo(MapLayerId.DeepWater));
            Assert.That(result[1].LayerId, Is.EqualTo(MapLayerId.Land));
            Assert.That(result[2].LayerId, Is.EqualTo(MapLayerId.Vegetation));
        }

        [Test]
        public void BuildPriorityTable_AllTilesNonNull()
        {
            var entries = new[]
            {
                new ProceduralTileEntry { LayerId = MapLayerId.DeepWater, Color = Color.blue  },
                new ProceduralTileEntry { LayerId = MapLayerId.Land,       Color = Color.green },
            };

            TilemapLayerEntry[] result = ProceduralTileFactory.BuildPriorityTable(entries);

            foreach (var entry in result)
                Assert.That(entry.Tile, Is.Not.Null, $"Tile for layer {entry.LayerId} must not be null.");
        }

        [Test]
        public void BuildPriorityTable_SameColorEntriesShareTileInstance()
        {
            // Two entries with the same Color must reference the same cached Tile.
            Color sharedColor = Color.green;
            var entries = new[]
            {
                new ProceduralTileEntry { LayerId = MapLayerId.Land,       Color = sharedColor },
                new ProceduralTileEntry { LayerId = MapLayerId.Vegetation, Color = sharedColor },
            };

            TilemapLayerEntry[] result = ProceduralTileFactory.BuildPriorityTable(entries);

            Assert.That(result[0].Tile, Is.SameAs(result[1].Tile),
                "Entries with the same color must share a single cached Tile instance.");
        }

        // =====================================================================
        // ClearCache
        // =====================================================================

        [Test]
        public void ClearCache_AfterClear_SameColorReturnsFreshInstance()
        {
            Tile before = ProceduralTileFactory.GetOrCreate(Color.magenta);
            ProceduralTileFactory.ClearCache();
            Tile after = ProceduralTileFactory.GetOrCreate(Color.magenta);

            // Both must be valid, but must be different instances.
            Assert.That(before, Is.Not.Null);
            Assert.That(after, Is.Not.Null);
            Assert.That(after, Is.Not.SameAs(before),
                "After ClearCache, the same color must produce a fresh Tile instance.");
        }

        [Test]
        public void ClearCache_CalledMultipleTimes_DoesNotThrow()
        {
            ProceduralTileFactory.GetOrCreate(Color.red);

            Assert.DoesNotThrow(() =>
            {
                ProceduralTileFactory.ClearCache();
                ProceduralTileFactory.ClearCache(); // second call on empty cache must be a no-op
            });
        }

        [Test]
        public void ClearCache_EmptyCache_DoesNotThrow()
        {
            // Cache is already empty from SetUp; clearing again must be a no-op.
            Assert.DoesNotThrow(() => ProceduralTileFactory.ClearCache());
        }
    }
}