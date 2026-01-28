using Islands.PCG.Core;
using Islands.PCG.Grids;
using Islands.PCG.Operators;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;

public sealed class DrawLineTests
{
    [Test]
    public void DrawLine_EndpointInclusion_AxisAligned_Radius0()
    {
        var domain = new GridDomain2D(16, 16);
        var mask = new MaskGrid2D(domain, Allocator.Temp, clearToZero: true);

        try
        {
            int2 a = new int2(2, 2);
            int2 b = new int2(10, 2);

            MaskRasterOps2D.DrawLine(ref mask, a, b, brushRadius: 0, value: true);

            Assert.IsTrue(mask.Get(a.x, a.y), $"Expected endpoint A to be carved at {a}.");
            Assert.IsTrue(mask.Get(b.x, b.y), $"Expected endpoint B to be carved at {b}.");
        }
        finally
        {
            mask.Dispose();
        }
    }

    [Test]
    public void DrawLine_ReversalInvariance_SameHash()
    {
        var domain = new GridDomain2D(32, 32);
        var maskA = new MaskGrid2D(domain, Allocator.Temp, clearToZero: true);
        var maskB = new MaskGrid2D(domain, Allocator.Temp, clearToZero: true);

        try
        {
            int2 a = new int2(3, 27);
            int2 b = new int2(28, 6);

            MaskRasterOps2D.DrawLine(ref maskA, a, b, brushRadius: 0, value: true);
            MaskRasterOps2D.DrawLine(ref maskB, b, a, brushRadius: 0, value: true);

            ulong hashA = maskA.SnapshotHash64();
            ulong hashB = maskB.SnapshotHash64();

            Assert.AreEqual(hashA, hashB, "Expected reversal invariance: A->B and B->A should produce identical SnapshotHash64.");
        }
        finally
        {
            maskA.Dispose();
            maskB.Dispose();
        }
    }

    [Test]
    public void DrawLine_AxisAlignedCountSanity_Radius0()
    {
        var domain = new GridDomain2D(16, 16);
        var mask = new MaskGrid2D(domain, Allocator.Temp, clearToZero: true);

        try
        {
            int2 a = new int2(2, 2);
            int2 b = new int2(10, 2);

            MaskRasterOps2D.DrawLine(ref mask, a, b, brushRadius: 0, value: true);

            int expected = math.abs(b.x - a.x) + 1; // endpoints included
            int actual = mask.CountOnes();

            Assert.AreEqual(expected, actual, $"Expected axis-aligned line to carve exactly {expected} cells, got {actual}.");
        }
        finally
        {
            mask.Dispose();
        }
    }

    [Test]
    public void DrawLine_BrushRadius_GrowsFilledArea()
    {
        var domain = new GridDomain2D(64, 64);
        var maskR0 = new MaskGrid2D(domain, Allocator.Temp, clearToZero: true);
        var maskR2 = new MaskGrid2D(domain, Allocator.Temp, clearToZero: true);

        try
        {
            int2 a = new int2(10, 10);
            int2 b = new int2(50, 10);

            MaskRasterOps2D.DrawLine(ref maskR0, a, b, brushRadius: 0, value: true);
            MaskRasterOps2D.DrawLine(ref maskR2, a, b, brushRadius: 2, value: true);

            int onesR0 = maskR0.CountOnes();
            int onesR2 = maskR2.CountOnes();

            Assert.Greater(onesR2, onesR0, $"Expected brushRadius=2 to carve more cells than brushRadius=0, got r0={onesR0}, r2={onesR2}.");
        }
        finally
        {
            maskR0.Dispose();
            maskR2.Dispose();
        }
    }
}
