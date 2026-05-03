// Phase V.a — Optional unit tests per Phase_V_Design.md §10.1.
// Guards the two contracts other code depends on:
//   1. TryWorldToCell round-trips through the cell center for both flipY=true and false.
//   2. RegenerationVersion increments monotonically across successful pipeline runs.
//
// Palette tests deferred to V.b (BiomeColorPalette ScriptableObject).

using NUnit.Framework;
using UnityEngine;
using UnityEngine.Tilemaps;
using Islands.PCG.Adapters.Tilemap;
using Islands.PCG.Inspection;

namespace Islands.PCG.Tests.EditMode.Inspection
{
    [TestFixture]
    public sealed class IMapContextSourceTryWorldToCellTests
    {
        private GameObject _go;
        private GameObject _gridGO;
        private PCGMapTilemapVisualization _viz;
        private Tilemap _tilemap;

        [SetUp]
        public void Setup()
        {
            // Grid + Tilemap pair (Tilemap requires a Grid component on the parent).
            _gridGO = new GameObject("Grid_Test");
            _gridGO.AddComponent<Grid>();
            var tilemapGO = new GameObject("Tilemap_Test");
            tilemapGO.transform.SetParent(_gridGO.transform);
            _tilemap = tilemapGO.AddComponent<Tilemap>();

            _go = new GameObject("Viz_Test");
            _viz = _go.AddComponent<PCGMapTilemapVisualization>();

            // Wire the tilemap into the viz via reflection — the field is private and
            // we don't want to expose it just for tests.
            typeof(PCGMapTilemapVisualization)
                .GetField("tilemap", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .SetValue(_viz, _tilemap);
        }

        [TearDown]
        public void Teardown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
            if (_gridGO != null) Object.DestroyImmediate(_gridGO);
        }

        [Test]
        public void TryWorldToCell_ReturnsFalse_BeforeFirstUpdate()
        {
            // Context has not been allocated yet — Update() never ran.
            IMapContextSource src = _viz;
            Assert.IsFalse(src.TryWorldToCell(Vector3.zero, out int x, out int y));
            Assert.AreEqual(0, x);
            Assert.AreEqual(0, y);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TryWorldToCell_RoundTripsCellCenter_ForAllValidCells(bool flipY)
        {
            ForceFirstUpdate(flipY);

            IMapContextSource src = _viz;
            var ctx = src.Context;
            Assume.That(ctx, Is.Not.Null, "Update() did not allocate a context.");

            int w = ctx.Domain.Width, h = ctx.Domain.Height;
            for (int cy = 0; cy < h; cy++)
                for (int cx = 0; cx < w; cx++)
                {
                    // Convert ctx cell to tilemap cell. flipY=true means render-row is mirrored.
                    int tilemapY = flipY ? (h - 1 - cy) : cy;
                    Vector3 center = _tilemap.GetCellCenterWorld(new Vector3Int(cx, tilemapY, 0));

                    Assert.IsTrue(src.TryWorldToCell(center, out int rx, out int ry),
                        $"Round-trip failed for cell ({cx},{cy}) flipY={flipY}.");
                    Assert.AreEqual(cx, rx, $"x mismatch at ({cx},{cy}) flipY={flipY}.");
                    Assert.AreEqual(cy, ry, $"y mismatch at ({cx},{cy}) flipY={flipY}.");
                }
        }

        [Test]
        public void TryWorldToCell_ReturnsFalse_OutsideGridBounds()
        {
            ForceFirstUpdate(flipY: false);

            IMapContextSource src = _viz;
            var ctx = src.Context;
            Assume.That(ctx, Is.Not.Null);

            // One cell outside bottom-left.
            Vector3 outside = _tilemap.GetCellCenterWorld(new Vector3Int(-1, -1, 0));
            Assert.IsFalse(src.TryWorldToCell(outside, out _, out _));

            // One cell outside top-right.
            Vector3 outside2 = _tilemap.GetCellCenterWorld(new Vector3Int(ctx.Domain.Width, ctx.Domain.Height, 0));
            Assert.IsFalse(src.TryWorldToCell(outside2, out _, out _));
        }

        [Test]
        public void RegenerationVersion_IncrementsAcrossUpdates()
        {
            ForceFirstUpdate(flipY: false);
            IMapContextSource src = _viz;
            int v1 = src.RegenerationVersion;
            Assert.GreaterOrEqual(v1, 1, "RegenerationVersion did not increment after first Update.");

            // Force another regen by flipping the dirty flag and calling Update again.
            ForceDirty();
            CallUpdate();
            int v2 = src.RegenerationVersion;
            Assert.Greater(v2, v1, "RegenerationVersion did not increment on second Update.");
        }

        // -----------------------------------------------------------------
        // Helpers
        // -----------------------------------------------------------------

        private void ForceFirstUpdate(bool flipY)
        {
            // Set flipY via reflection.
            typeof(PCGMapTilemapVisualization)
                .GetField("flipY", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .SetValue(_viz, flipY);
            // Use a small resolution to keep the round-trip test fast.
            typeof(PCGMapTilemapVisualization)
                .GetField("resolution", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .SetValue(_viz, 8);
            // Use procedural tiles to avoid requiring a TilesetConfig asset.
            typeof(PCGMapTilemapVisualization)
                .GetField("useProceduralTiles", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(_viz, true);

            CallUpdate();
        }

        private void ForceDirty()
        {
            typeof(PCGMapTilemapVisualization)
                .GetField("dirty", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .SetValue(_viz, true);
        }

        private void CallUpdate()
        {
            // The viz's Update() is private; invoke via reflection.
            var update = typeof(PCGMapTilemapVisualization)
                .GetMethod("Update", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assume.That(update, Is.Not.Null, "Update method not found via reflection.");
            update.Invoke(_viz, null);
        }
    }
}