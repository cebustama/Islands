using Islands.PCG.Core;
using Islands.PCG.Grids;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;

using Random = Unity.Mathematics.Random;

public sealed class MaskGrid2DRandomSetBitTests
{
    [Test]
    public void TryGetRandomSetBit_EmptyMask_ReturnsFalse()
    {
        var domain = new GridDomain2D(8, 8);
        var mask = new MaskGrid2D(domain, Allocator.Temp, clearToZero: true);

        try
        {
            var rng = new Random(1u);

            bool ok = mask.TryGetRandomSetBit(ref rng, out int2 cell);
            Assert.IsFalse(ok);
            Assert.IsTrue(cell.Equals(default(int2)));
        }
        finally
        {
            mask.Dispose();
        }
    }

    [Test]
    public void TryGetRandomSetBit_SingleBit_AlwaysReturnsThatCell()
    {
        var domain = new GridDomain2D(16, 16);
        var mask = new MaskGrid2D(domain, Allocator.Temp, clearToZero: true);

        try
        {
            int2 only = new int2(3, 5);
            mask.Set(only.x, only.y, true);

            var rng = new Random(123u);

            for (int i = 0; i < 32; i++)
            {
                bool ok = mask.TryGetRandomSetBit(ref rng, out int2 cell);
                Assert.IsTrue(ok);
                Assert.IsTrue(cell.Equals(only), $"Expected {only} but got {cell}");
            }
        }
        finally
        {
            mask.Dispose();
        }
    }

    [Test]
    public void TryGetRandomSetBit_SameSeed_SameSequence()
    {
        var domain = new GridDomain2D(32, 32);
        var maskA = new MaskGrid2D(domain, Allocator.Temp, clearToZero: true);
        var maskB = new MaskGrid2D(domain, Allocator.Temp, clearToZero: true);

        try
        {
            // Set a small pattern of ON cells
            for (int y = 10; y <= 12; y++)
                for (int x = 10; x <= 12; x++)
                {
                    maskA.Set(x, y, true);
                    maskB.Set(x, y, true);
                }

            var rngA = new Random(999u);
            var rngB = new Random(999u);

            for (int i = 0; i < 64; i++)
            {
                bool okA = maskA.TryGetRandomSetBit(ref rngA, out int2 a);
                bool okB = maskB.TryGetRandomSetBit(ref rngB, out int2 b);

                Assert.AreEqual(okA, okB, $"OK mismatch at i={i}");
                Assert.IsTrue(okA);
                Assert.IsTrue(a.Equals(b), $"Sequence mismatch at i={i}: {a} vs {b}");
            }
        }
        finally
        {
            maskA.Dispose();
            maskB.Dispose();
        }
    }

    [Test]
    public void TryGetRandomSetBit_FillTrue_NeverReturnsOutOfBounds_OnNonMultipleOf64Domain()
    {
        // 10*10 = 100 bits => last word has tail bits; this test ensures tail masking is correct.
        var domain = new GridDomain2D(10, 10);
        var mask = new MaskGrid2D(domain, Allocator.Temp, clearToZero: true);

        try
        {
            mask.Fill(true);

            var rng = new Random(42u);

            for (int i = 0; i < 256; i++)
            {
                bool ok = mask.TryGetRandomSetBit(ref rng, out int2 cell);
                Assert.IsTrue(ok);

                Assert.IsTrue(domain.InBounds(cell.x, cell.y), $"Out of bounds cell returned: {cell}");
                Assert.IsTrue(mask.Get(cell.x, cell.y), $"Returned cell is not ON: {cell}");
            }
        }
        finally
        {
            mask.Dispose();
        }
    }
}
