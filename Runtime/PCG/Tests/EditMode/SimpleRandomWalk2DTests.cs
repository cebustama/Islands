using Islands.PCG.Core;
using Islands.PCG.Grids;
using Islands.PCG.Layout;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;

using Random = Unity.Mathematics.Random;

public sealed class SimpleRandomWalk2DTests
{
    [Test]
    public void Walk_SameSeed_SameConfig_ProducesIdenticalMaskAndEnd()
    {
        const int res = 32;
        var domain = new GridDomain2D(res, res);

        var maskA = new MaskGrid2D(domain, Allocator.Temp, clearToZero: true);
        var maskB = new MaskGrid2D(domain, Allocator.Temp, clearToZero: true);

        try
        {
            var rngA = new Random(123456u);
            var rngB = new Random(123456u);

            int2 start = new int2(res / 2, res / 2);

            int2 endA = SimpleRandomWalk2D.Walk(
                ref maskA, ref rngA,
                start: start,
                walkLength: 200,
                brushRadius: 1,
                skewX: 0.25f,
                skewY: -0.10f,
                maxRetries: 16);

            int2 endB = SimpleRandomWalk2D.Walk(
                ref maskB, ref rngB,
                start: start,
                walkLength: 200,
                brushRadius: 1,
                skewX: 0.25f,
                skewY: -0.10f,
                maxRetries: 16);

            Assert.IsTrue(endA.Equals(endB), $"End mismatch: {endA} vs {endB}");

            // Full grid equality (strong determinism check)
            for (int y = 0; y < res; y++)
                for (int x = 0; x < res; x++)
                {
                    bool a = maskA.GetUnchecked(x, y);
                    bool b = maskB.GetUnchecked(x, y);
                    Assert.AreEqual(a, b, $"Cell mismatch at ({x},{y})");
                }

            Assert.AreEqual(maskA.CountOnes(), maskB.CountOnes(), "CountOnes mismatch (should be identical).");
        }
        finally
        {
            maskA.Dispose();
            maskB.Dispose();
        }
    }

    [Test]
    public void Walk_LongerLength_DoesNotDecreaseCountOnes()
    {
        const int res = 64;
        var domain = new GridDomain2D(res, res);

        var maskShort = new MaskGrid2D(domain, Allocator.Temp, clearToZero: true);
        var maskLong = new MaskGrid2D(domain, Allocator.Temp, clearToZero: true);

        try
        {
            // Re-init RNG with same seed so the first N steps match.
            var rngShort = new Random(777u);
            var rngLong = new Random(777u);

            int2 start = new int2(res / 2, res / 2);

            SimpleRandomWalk2D.Walk(ref maskShort, ref rngShort, start, walkLength: 64, brushRadius: 0, skewX: 0f, skewY: 0f, maxRetries: 32);
            SimpleRandomWalk2D.Walk(ref maskLong, ref rngLong, start, walkLength: 256, brushRadius: 0, skewX: 0f, skewY: 0f, maxRetries: 32);

            int a = maskShort.CountOnes();
            int b = maskLong.CountOnes();

            Assert.GreaterOrEqual(b, a, $"Expected longer walk to have >= ones, got short={a}, long={b}");
        }
        finally
        {
            maskShort.Dispose();
            maskLong.Dispose();
        }
    }

    [Test]
    public void Walk_BrushRadius_IncreasesOrEqualsCoverage()
    {
        const int res = 64;
        var domain = new GridDomain2D(res, res);

        var maskR0 = new MaskGrid2D(domain, Allocator.Temp, clearToZero: true);
        var maskR2 = new MaskGrid2D(domain, Allocator.Temp, clearToZero: true);

        try
        {
            var rng0 = new Random(999u);
            var rng2 = new Random(999u);

            int2 start = new int2(res / 2, res / 2);

            SimpleRandomWalk2D.Walk(ref maskR0, ref rng0, start, walkLength: 128, brushRadius: 0, skewX: 0f, skewY: 0f, maxRetries: 32);
            SimpleRandomWalk2D.Walk(ref maskR2, ref rng2, start, walkLength: 128, brushRadius: 2, skewX: 0f, skewY: 0f, maxRetries: 32);

            int a = maskR0.CountOnes();
            int b = maskR2.CountOnes();

            Assert.GreaterOrEqual(b, a, $"Expected radius=2 to have >= ones than radius=0, got r0={a}, r2={b}");
        }
        finally
        {
            maskR0.Dispose();
            maskR2.Dispose();
        }
    }

    [Test]
    public void Walk_LengthZero_StillCarvesStartCell()
    {
        const int res = 16;
        var domain = new GridDomain2D(res, res);

        var mask = new MaskGrid2D(domain, Allocator.Temp, clearToZero: true);

        try
        {
            var rng = new Random(42u);
            int2 start = new int2(3, 5);

            int2 end = SimpleRandomWalk2D.Walk(ref mask, ref rng, start, walkLength: 0, brushRadius: 0);

            Assert.IsTrue(end.Equals(start), $"Expected end==start for length 0, got {end}");
            Assert.Greater(mask.CountOnes(), 0, "Expected at least the start cell to be carved.");
            Assert.IsTrue(mask.GetUnchecked(start.x, start.y), "Expected start cell to be set.");
        }
        finally
        {
            mask.Dispose();
        }
    }
}
