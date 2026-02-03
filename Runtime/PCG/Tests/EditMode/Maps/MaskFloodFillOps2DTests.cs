using NUnit.Framework;
using Unity.Collections;

using Islands.PCG.Core;
using Islands.PCG.Grids;
using Islands.PCG.Generators;
using Islands.PCG.Operators;

namespace Islands.PCG.Tests.EditMode.Maps
{
    public sealed class MaskFloodFillOps2DTests
    {
        [Test]
        public void FloodFillBorderConnected_NotSolid_Donut_ExcludesEnclosedLake()
        {
            // Domain
            var domain = new GridDomain2D(16, 16);

            // solid = land (true = blocked)
            // deep  = result (true = border-connected water)
            var solid = new MaskGrid2D(domain, Allocator.Persistent, clearToZero: true);
            var deep = new MaskGrid2D(domain, Allocator.Persistent, clearToZero: true);

            try
            {
                // Build "donut" using helpers:
                // 1) A land block in the middle (does NOT touch borders)
                // 2) A carved lake hole inside the land block
                //
                // Land block: [3,13) x [3,13)  => 10x10 = 100 cells
                RectFillGenerator.FillRect(ref solid, 3, 3, 13, 13, value: true, clampToDomain: true);

                // Lake hole: [6,10) x [6,10) => 4x4 = 16 cells set back to water
                RectFillGenerator.FillRect(ref solid, 6, 6, 10, 10, value: false, clampToDomain: true);

                // Run flood fill: mark all border-connected NOT-solid cells as deep water
                MaskFloodFillOps2D.FloodFillBorderConnected_NotSolid(ref solid, ref deep);

                // --------------------------
                // Assertions (behavior)
                // --------------------------

                // Border water must be deep
                Assert.IsTrue(deep.Get(0, 0), "Border-connected water at (0,0) must be deep.");
                Assert.IsTrue(deep.Get(2, 2), "Water outside land block should be deep.");

                // Land must never be deep
                Assert.IsFalse(deep.Get(4, 4), "Land cell should NOT be marked deep.");

                // Enclosed lake should NOT be deep (not border-connected)
                Assert.IsFalse(deep.Get(7, 7), "Enclosed lake cell must NOT be deep.");

                // Strong invariant: deep ∩ land == ∅
                int w = domain.Width;
                int h = domain.Height;

                int deepCount = 0;
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                    {
                        bool isLand = solid.Get(x, y);
                        bool isDeep = deep.Get(x, y);

                        if (isDeep) deepCount++;

                        Assert.IsFalse(isLand && isDeep, $"Cell ({x},{y}) is both land and deep, which is invalid.");
                    }

                // For this exact donut geometry:
                // Outside-water region size is 156 cells; the lake is 16 cells and must stay OFF in deep.
                Assert.AreEqual(156, deepCount, "Deep water count should match expected border-connected water region.");

                // --------------------------
                // Golden hash gate (micro-gate)
                // --------------------------
                // IMPORTANT: This constant is tied to MaskGrid2D.SnapshotHash64 implementation
                // (byte-wise FNV mix + includeDimensions=true) and to this exact donut geometry.
                const ulong ExpectedDeepHash = 0x41E755C162D8A214UL;

                ulong got = deep.SnapshotHash64(includeDimensions: true);
                Assert.AreEqual(ExpectedDeepHash, got, "Golden hash changed: flood-fill behavior drift or contract break.");
            }
            finally
            {
                solid.Dispose();
                deep.Dispose();
            }
        }
    }
}
