using NUnit.Framework;
using Unity.Collections;
using Islands.PCG.Core;
using Islands.PCG.Grids;
using Islands.PCG.Operators;

namespace Islands.PCG.Tests.EditMode.Maps
{
    public sealed class MaskTopologyOps2DTests
    {
        [Test]
        public void ExtractEdgeAndInterior4_Rect_Partitions_Source()
        {
            var domain = new GridDomain2D(8, 8);
            var src = new MaskGrid2D(domain, Allocator.Persistent, clearToZero: true);
            var edge = new MaskGrid2D(domain, Allocator.Persistent, clearToZero: true);
            var interior = new MaskGrid2D(domain, Allocator.Persistent, clearToZero: true);
            var union = new MaskGrid2D(domain, Allocator.Persistent, clearToZero: true);
            var intersection = new MaskGrid2D(domain, Allocator.Persistent, clearToZero: true);

            try
            {
                for (int y = 2; y < 6; y++)
                    for (int x = 2; x < 6; x++)
                        src.SetUnchecked(x, y, true);

                MaskTopologyOps2D.ExtractEdgeAndInterior4(in src, ref edge, ref interior);

                union.CopyFrom(edge);
                union.Or(interior);

                intersection.CopyFrom(edge);
                intersection.And(interior);

                Assert.AreEqual(src.SnapshotHash64(includeDimensions: true), union.SnapshotHash64(includeDimensions: true));
                Assert.AreEqual(0, intersection.CountOnes());
                Assert.AreEqual(12, edge.CountOnes(), "4x4 rect should have 12 edge cells.");
                Assert.AreEqual(4, interior.CountOnes(), "4x4 rect should have 4 interior cells.");
            }
            finally
            {
                src.Dispose();
                edge.Dispose();
                interior.Dispose();
                union.Dispose();
                intersection.Dispose();
            }
        }

        [Test]
        public void ExtractEdgeAndInterior4_Donut_Treats_Hole_Border_As_Edge()
        {
            var domain = new GridDomain2D(7, 7);
            var src = new MaskGrid2D(domain, Allocator.Persistent, clearToZero: true);
            var edge = new MaskGrid2D(domain, Allocator.Persistent, clearToZero: true);
            var interior = new MaskGrid2D(domain, Allocator.Persistent, clearToZero: true);

            try
            {
                for (int y = 1; y < 6; y++)
                    for (int x = 1; x < 6; x++)
                        src.SetUnchecked(x, y, true);

                for (int y = 2; y < 5; y++)
                    for (int x = 2; x < 5; x++)
                        src.SetUnchecked(x, y, false);

                MaskTopologyOps2D.ExtractEdgeAndInterior4(in src, ref edge, ref interior);

                Assert.AreEqual(16, edge.CountOnes(), "5x5 ring with 3x3 hole should be all edge.");
                Assert.AreEqual(0, interior.CountOnes(), "Thin donut should have no interior under 4-neighborhood rule.");
            }
            finally
            {
                src.Dispose();
                edge.Dispose();
                interior.Dispose();
            }
        }

        [Test]
        public void LabelConnectedComponents4_Uses_Stable_RowMajor_Discovery_Order()
        {
            var domain = new GridDomain2D(8, 8);
            var src = new MaskGrid2D(domain, Allocator.Persistent, clearToZero: true);
            var labels = new NativeArray<int>(domain.Length, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            var components = new NativeList<MaskTopologyOps2D.MaskComponent2D>(Allocator.Persistent);

            try
            {
                // Component 0 (top-left)
                src.Set(1, 1, true);
                src.Set(2, 1, true);
                src.Set(1, 2, true);

                // Component 1 (middle-right)
                src.Set(5, 3, true);
                src.Set(5, 4, true);

                // Component 2 (bottom-left)
                src.Set(0, 7, true);

                int count = MaskTopologyOps2D.LabelConnectedComponents4(in src, labels, components);

                Assert.AreEqual(3, count);
                Assert.AreEqual(3, components.Length);

                Assert.AreEqual(domain.Index(1, 1), components[0].AnchorIndex);
                Assert.AreEqual(domain.Index(5, 3), components[1].AnchorIndex);
                Assert.AreEqual(domain.Index(0, 7), components[2].AnchorIndex);

                Assert.AreEqual(3, components[0].Area);
                Assert.AreEqual(2, components[1].Area);
                Assert.AreEqual(1, components[2].Area);
            }
            finally
            {
                src.Dispose();
                labels.Dispose();
                components.Dispose();
            }
        }
    }
}
