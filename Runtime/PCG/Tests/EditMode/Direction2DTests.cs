using Islands.PCG.Layout;
using NUnit.Framework;
using Unity.Mathematics;

public sealed class Direction2DTests
{
    [Test]
    public void PickSkewedCardinal_SameSeed_SameSequence()
    {
        var rngA = new Unity.Mathematics.Random(123456u);
        var rngB = new Unity.Mathematics.Random(123456u);

        for (int i = 0; i < 32; i++)
        {
            int2 a = Direction2D.PickSkewedCardinal(ref rngA, skewX: 0.25f, skewY: -0.10f);
            int2 b = Direction2D.PickSkewedCardinal(ref rngB, skewX: 0.25f, skewY: -0.10f);
            Assert.IsTrue(a.Equals(b), $"Mismatch at i={i}: {a} vs {b}");
        }
    }

    [Test]
    public void PickSkewedCardinal_PositiveSkewX_FavorsRight()
    {
        var rng = new Unity.Mathematics.Random(1u);

        int right = 0, left = 0;
        const int N = 20000;

        for (int i = 0; i < N; i++)
        {
            int2 d = Direction2D.PickSkewedCardinal(ref rng, skewX: 0.6f, skewY: 0f);
            if (d.x == 1) right++;
            else if (d.x == -1) left++;
        }

        Assert.Greater(right, left, $"Expected more right than left, got right={right}, left={left}");
    }

    [Test]
    public void PickSkewedCardinal_NegativeSkewY_FavorsDown()
    {
        var rng = new Unity.Mathematics.Random(2u);

        int up = 0, down = 0;
        const int N = 20000;

        for (int i = 0; i < N; i++)
        {
            int2 d = Direction2D.PickSkewedCardinal(ref rng, skewX: 0f, skewY: -0.6f);
            if (d.y == 1) up++;
            else if (d.y == -1) down++;
        }

        Assert.Greater(down, up, $"Expected more down than up, got down={down}, up={up}");
    }
}
