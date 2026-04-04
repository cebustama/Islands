using System;
using NUnit.Framework;
using Unity.Collections;

using Islands.PCG.Core;
using Islands.PCG.Layout.Maps;

namespace Islands.PCG.Tests.EditMode.Maps
{
    /// <summary>
    /// Unit tests for <see cref="MapExporter2D"/> and <see cref="MapDataExport"/>.
    ///
    /// Covers: empty-context export, layer/field round-trip fidelity, determinism,
    /// missing-slot guards, and out-of-bounds guards.
    ///
    /// Does NOT test the full pipeline — that is covered by golden/integration tests.
    /// </summary>
    public sealed class MapExporter2DTests
    {
        private const int W = 8;
        private const int H = 6;
        private const uint TestSeed = 99u;

        private MapContext2D _ctx;

        [SetUp]
        public void SetUp()
        {
            var domain = new GridDomain2D(W, H);
            _ctx = new MapContext2D(domain, Allocator.Persistent);
            var inputs = new MapInputs(TestSeed, domain, MapTunables2D.Default);
            _ctx.BeginRun(in inputs);
        }

        [TearDown]
        public void TearDown()
        {
            _ctx?.Dispose();
        }

        // ─────────────────────────────────────────────────────────────────────
        // Export shape / presence
        // ─────────────────────────────────────────────────────────────────────

        [Test]
        public void Export_EmptyContext_AllLayersAndFieldsAbsent()
        {
            var export = MapExporter2D.Export(_ctx);

            Assert.AreEqual(W, export.Width);
            Assert.AreEqual(H, export.Height);
            Assert.AreEqual(W * H, export.Length);
            Assert.AreEqual(TestSeed, export.Seed);

            for (int i = 0; i < (int)MapLayerId.COUNT; i++)
                Assert.IsFalse(export.HasLayer((MapLayerId)i),
                    $"Expected layer {(MapLayerId)i} to be absent.");

            for (int i = 0; i < (int)MapFieldId.COUNT; i++)
                Assert.IsFalse(export.HasField((MapFieldId)i),
                    $"Expected field {(MapFieldId)i} to be absent.");
        }

        // ─────────────────────────────────────────────────────────────────────
        // Layer round-trip fidelity
        // ─────────────────────────────────────────────────────────────────────

        [Test]
        public void Export_Layer_RoundTrip_MatchesSource()
        {
            ref var land = ref _ctx.EnsureLayer(MapLayerId.Land, clearToZero: true);

            // Set a recognizable pattern: diagonal cells.
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                    land.Set(x, y, x == y);

            var export = MapExporter2D.Export(_ctx);

            Assert.IsTrue(export.HasLayer(MapLayerId.Land));

            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                    Assert.AreEqual(x == y, export.GetCell(MapLayerId.Land, x, y),
                        $"Mismatch at ({x},{y}).");
        }

        [Test]
        public void Export_Layer_AllOnes_RoundTrip()
        {
            ref var deep = ref _ctx.EnsureLayer(MapLayerId.DeepWater, clearToZero: true);
            deep.Fill(true);

            var export = MapExporter2D.Export(_ctx);

            Assert.IsTrue(export.HasLayer(MapLayerId.DeepWater));
            bool[] buf = export.GetLayer(MapLayerId.DeepWater);
            Assert.AreEqual(W * H, buf.Length);
            foreach (bool v in buf)
                Assert.IsTrue(v, "Expected all cells to be true.");
        }

        [Test]
        public void Export_Layer_AllZeros_RoundTrip()
        {
            _ctx.EnsureLayer(MapLayerId.Vegetation, clearToZero: true); // all false

            var export = MapExporter2D.Export(_ctx);

            Assert.IsTrue(export.HasLayer(MapLayerId.Vegetation));
            bool[] buf = export.GetLayer(MapLayerId.Vegetation);
            foreach (bool v in buf)
                Assert.IsFalse(v, "Expected all cells to be false.");
        }

        [Test]
        public void Export_UnrelatedLayer_Absent()
        {
            _ctx.EnsureLayer(MapLayerId.Land);

            var export = MapExporter2D.Export(_ctx);

            Assert.IsTrue(export.HasLayer(MapLayerId.Land));
            Assert.IsFalse(export.HasLayer(MapLayerId.HillsL1), "HillsL1 was not created.");
        }

        // ─────────────────────────────────────────────────────────────────────
        // Field round-trip fidelity
        // ─────────────────────────────────────────────────────────────────────

        [Test]
        public void Export_Field_RoundTrip_MatchesSource()
        {
            ref var height = ref _ctx.EnsureField(MapFieldId.Height, clearToZero: true);

            // Write a gradient pattern.
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                    height.Values[x + y * W] = x * 0.1f + y * 0.01f;

            var export = MapExporter2D.Export(_ctx);

            Assert.IsTrue(export.HasField(MapFieldId.Height));

            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                    Assert.AreEqual(x * 0.1f + y * 0.01f, export.GetValue(MapFieldId.Height, x, y),
                        1e-6f, $"Field mismatch at ({x},{y}).");
        }

        [Test]
        public void Export_Field_Absent_When_NotCreated()
        {
            var export = MapExporter2D.Export(_ctx);
            Assert.IsFalse(export.HasField(MapFieldId.CoastDist));
        }

        // ─────────────────────────────────────────────────────────────────────
        // Determinism
        // ─────────────────────────────────────────────────────────────────────

        [Test]
        public void Export_IsDeterministic_SameContextState()
        {
            ref var land = ref _ctx.EnsureLayer(MapLayerId.Land, clearToZero: true);
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                    land.Set(x, y, (x + y) % 2 == 0);

            ref var height = ref _ctx.EnsureField(MapFieldId.Height, clearToZero: true);
            for (int j = 0; j < W * H; j++)
                height.Values[j] = j * 0.001f;

            var exportA = MapExporter2D.Export(_ctx);
            var exportB = MapExporter2D.Export(_ctx);

            bool[] landA = exportA.GetLayer(MapLayerId.Land);
            bool[] landB = exportB.GetLayer(MapLayerId.Land);
            Assert.AreEqual(landA.Length, landB.Length);
            for (int i = 0; i < landA.Length; i++)
                Assert.AreEqual(landA[i], landB[i], $"Layer mismatch at index {i}.");

            float[] htA = exportA.GetField(MapFieldId.Height);
            float[] htB = exportB.GetField(MapFieldId.Height);
            for (int i = 0; i < htA.Length; i++)
                Assert.AreEqual(htA[i], htB[i], 0f, $"Field mismatch at index {i}.");
        }

        // ─────────────────────────────────────────────────────────────────────
        // Snapshot independence
        // ─────────────────────────────────────────────────────────────────────

        [Test]
        public void Export_SnapshotIsIndependent_ContextMutationAfterExport()
        {
            ref var land = ref _ctx.EnsureLayer(MapLayerId.Land, clearToZero: true);
            land.Set(0, 0, true);

            var export = MapExporter2D.Export(_ctx);

            // Mutate the context after export.
            land.Set(0, 0, false);

            // Snapshot must reflect the state at export time.
            Assert.IsTrue(export.GetCell(MapLayerId.Land, 0, 0),
                "Export snapshot should be independent of post-export context mutations.");
        }

        // ─────────────────────────────────────────────────────────────────────
        // Guard / error path
        // ─────────────────────────────────────────────────────────────────────

        [Test]
        public void Export_Null_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => MapExporter2D.Export(null));
        }

        [Test]
        public void GetLayer_AbsentLayer_Throws()
        {
            var export = MapExporter2D.Export(_ctx); // empty context
            Assert.Throws<InvalidOperationException>(() => export.GetLayer(MapLayerId.Land));
        }

        [Test]
        public void GetField_AbsentField_Throws()
        {
            var export = MapExporter2D.Export(_ctx);
            Assert.Throws<InvalidOperationException>(() => export.GetField(MapFieldId.Height));
        }

        [Test]
        public void GetCell_OutOfBounds_Throws()
        {
            _ctx.EnsureLayer(MapLayerId.Land);
            var export = MapExporter2D.Export(_ctx);

            Assert.Throws<ArgumentOutOfRangeException>(() => export.GetCell(MapLayerId.Land, W, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => export.GetCell(MapLayerId.Land, 0, H));
            Assert.Throws<ArgumentOutOfRangeException>(() => export.GetCell(MapLayerId.Land, -1, 0));
        }

        [Test]
        public void GetValue_OutOfBounds_Throws()
        {
            _ctx.EnsureField(MapFieldId.Height);
            var export = MapExporter2D.Export(_ctx);

            Assert.Throws<ArgumentOutOfRangeException>(() => export.GetValue(MapFieldId.Height, W, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => export.GetValue(MapFieldId.Height, 0, H));
        }
    }
}