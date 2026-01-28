using Islands.PCG.Core;
using Islands.PCG.Grids;
using Islands.PCG.Layout;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;

using Random = Unity.Mathematics.Random;

public sealed class IteratedRandomWalk2DTests
{
    [Test]
    public void Carve_SameSeed_SameConfig_ProducesIdenticalMaskAndEnd()
    {
        const int res = 48;
        var domain = new GridDomain2D(res, res);

        var maskA = new MaskGrid2D(domain, Allocator.Temp, clearToZero: true);
        var maskB = new MaskGrid2D(domain, Allocator.Temp, clearToZero: true);

        try
        {
            var rngA = new Random(123456u);
            var rngB = new Random(123456u);

            int2 start = new int2(res / 2, res / 2);

            int2 endA = IteratedRandomWalk2D.Carve(
                ref maskA, ref rngA,
                start: start,
                iterations: 25,
                walkLengthMin: 20,
                walkLengthMax: 60,
                brushRadius: 1,
                randomStartChance: 0.35f,
                skewX: 0.15f,
                skewY: -0.10f,
                maxRetries: 16);

            int2 endB = IteratedRandomWalk2D.Carve(
                ref maskB, ref rngB,
                start: start,
                iterations: 25,
                walkLengthMin: 20,
                walkLengthMax: 60,
                brushRadius: 1,
                randomStartChance: 0.35f,
                skewX: 0.15f,
                skewY: -0.10f,
                maxRetries: 16);

            Assert.IsTrue(endA.Equals(endB), $"End mismatch: {endA} vs {endB}");

            // Strong determinism: full cell-by-cell equality
            for (int y = 0; y < res; y++)
                for (int x = 0; x < res; x++)
                {
                    bool a = maskA.GetUnchecked(x, y);
                    bool b = maskB.GetUnchecked(x, y);
                    Assert.AreEqual(a, b, $"Cell mismatch at ({x},{y})");
                }

            Assert.AreEqual(maskA.CountOnes(), maskB.CountOnes(), "CountOnes mismatch (should be identical).");

            // Optional: fingerprint for easier debugging if something fails later.
            ulong hashA = maskA.SnapshotHash64();
            ulong hashB = maskB.SnapshotHash64();
            Assert.AreEqual(hashA, hashB, "SnapshotHash mismatch (should be identical).");
        }
        finally
        {
            maskA.Dispose();
            maskB.Dispose();
        }
    }

    [Test]
    public void Carve_IterationsOne_MatchesSimpleRandomWalk_WhenChanceZeroAndFixedLength()
    {
        const int res = 64;
        var domain = new GridDomain2D(res, res);

        var maskSimple = new MaskGrid2D(domain, Allocator.Temp, clearToZero: true);
        var maskIter = new MaskGrid2D(domain, Allocator.Temp, clearToZero: true);

        try
        {
            // Same seed on both RNGs is crucial for parity.
            var rngSimple = new Random(777u);
            var rngIter = new Random(777u);

            int2 start = new int2(res / 2, res / 2);

            const int walkLen = 200;
            const int brush = 0;
            const float skewX = 0.25f;
            const float skewY = -0.10f;
            const int maxRetries = 16;

            int2 endSimple = SimpleRandomWalk2D.Walk(
                ref maskSimple, ref rngSimple,
                start: start,
                walkLength: walkLen,
                brushRadius: brush,
                skewX: skewX,
                skewY: skewY,
                maxRetries: maxRetries);

            int2 endIter = IteratedRandomWalk2D.Carve(
                ref maskIter, ref rngIter,
                start: start,
                iterations: 1,
                walkLengthMin: walkLen,
                walkLengthMax: walkLen,
                brushRadius: brush,
                randomStartChance: 0f,
                skewX: skewX,
                skewY: skewY,
                maxRetries: maxRetries);

            Assert.IsTrue(endSimple.Equals(endIter), $"End mismatch: simple={endSimple} iter={endIter}");

            for (int y = 0; y < res; y++)
                for (int x = 0; x < res; x++)
                {
                    bool a = maskSimple.GetUnchecked(x, y);
                    bool b = maskIter.GetUnchecked(x, y);
                    Assert.AreEqual(a, b, $"Cell mismatch at ({x},{y})");
                }

            Assert.AreEqual(maskSimple.CountOnes(), maskIter.CountOnes(), "CountOnes mismatch (should be identical).");
        }
        finally
        {
            maskSimple.Dispose();
            maskIter.Dispose();
        }
    }

    [Test]
    public void Carve_MoreIterations_DoesNotDecreaseCountOnes_FromSameSeedAndConfig()
    {
        const int res = 64;
        var domain = new GridDomain2D(res, res);

        var maskI1 = new MaskGrid2D(domain, Allocator.Temp, clearToZero: true);
        var maskI10 = new MaskGrid2D(domain, Allocator.Temp, clearToZero: true);

        try
        {
            var rng1 = new Random(999u);
            var rng10 = new Random(999u);

            int2 start = new int2(res / 2, res / 2);

            IteratedRandomWalk2D.Carve(
                ref maskI1, ref rng1,
                start: start,
                iterations: 1,
                walkLengthMin: 25,
                walkLengthMax: 60,
                brushRadius: 1,
                randomStartChance: 0.5f,
                skewX: 0.0f,
                skewY: 0.0f,
                maxRetries: 16);

            IteratedRandomWalk2D.Carve(
                ref maskI10, ref rng10,
                start: start,
                iterations: 10,
                walkLengthMin: 25,
                walkLengthMax: 60,
                brushRadius: 1,
                randomStartChance: 0.5f,
                skewX: 0.0f,
                skewY: 0.0f,
                maxRetries: 16);

            int a = maskI1.CountOnes();
            int b = maskI10.CountOnes();

            Assert.GreaterOrEqual(b, a, $"Expected iterations=10 to have >= ones than iterations=1, got i1={a}, i10={b}");
        }
        finally
        {
            maskI1.Dispose();
            maskI10.Dispose();
        }
    }

    [Test]
    public void Carve_IterationsZero_IsNoOp_ReturnsStart_AndDoesNotWrite()
    {
        const int res = 32;
        var domain = new GridDomain2D(res, res);

        var mask = new MaskGrid2D(domain, Allocator.Temp, clearToZero: true);

        try
        {
            var rng = new Random(42u);
            int2 start = new int2(7, 11);

            int2 end = IteratedRandomWalk2D.Carve(
                ref mask, ref rng,
                start: start,
                iterations: 0,
                walkLengthMin: 10,
                walkLengthMax: 10,
                brushRadius: 0,
                randomStartChance: 1f,
                skewX: 0f,
                skewY: 0f,
                maxRetries: 8);

            Assert.IsTrue(end.Equals(start), $"Expected end==start for iterations=0, got {end}");
            Assert.AreEqual(0, mask.CountOnes(), "Expected no carving when iterations=0.");
        }
        finally
        {
            mask.Dispose();
        }
    }

    [Test]
    public void Carve_DifferentSeed_UsuallyDifferentOutput_Sanity()
    {
        const int res = 64;
        var domain = new GridDomain2D(res, res);

        var maskA = new MaskGrid2D(domain, Allocator.Temp, clearToZero: true);
        var maskB = new MaskGrid2D(domain, Allocator.Temp, clearToZero: true);

        try
        {
            var rngA = new Random(1u);
            var rngB = new Random(2u);

            int2 start = new int2(res / 2, res / 2);

            int2 endA = IteratedRandomWalk2D.Carve(
                ref maskA, ref rngA,
                start: start,
                iterations: 20,
                walkLengthMin: 25,
                walkLengthMax: 60,
                brushRadius: 1,
                randomStartChance: 0.35f,
                skewX: 0.10f,
                skewY: -0.05f,
                maxRetries: 16);

            int2 endB = IteratedRandomWalk2D.Carve(
                ref maskB, ref rngB,
                start: start,
                iterations: 20,
                walkLengthMin: 25,
                walkLengthMax: 60,
                brushRadius: 1,
                randomStartChance: 0.35f,
                skewX: 0.10f,
                skewY: -0.05f,
                maxRetries: 16);

            int onesA = maskA.CountOnes();
            int onesB = maskB.CountOnes();

            ulong hashA = maskA.SnapshotHash64();
            ulong hashB = maskB.SnapshotHash64();

            Assert.IsTrue(
                !endA.Equals(endB) || onesA != onesB || hashA != hashB,
                "Different seeds produced identical end + CountOnes + SnapshotHash (very unlikely; investigate if it happens).");
        }
        finally
        {
            maskA.Dispose();
            maskB.Dispose();
        }
    }
}
