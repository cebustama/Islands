using Islands.PCG.Core;
using Islands.PCG.Grids;
using Islands.PCG.Layout;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;

using Random = Unity.Mathematics.Random;

/// <summary>
/// D6 “Golden hash” regression gate.
/// Locks a known SnapshotHash64 for a fixed seed + config.
/// If this fails, something in the algorithm / hashing / tail-bit masking changed.
/// Update the expected constant ONLY when you intentionally change the output contract.
/// </summary>
public sealed class GoldenSnapshotHashTests
{
    // IMPORTANT:
    // 1) Run the test once with ExpectedHash = 0.
    // 2) It will fail and print the computed hash in the failure message.
    // 3) Copy/paste that value here (0x...............UL).
    // 4) Re-run: now it becomes a stable regression gate.
    private const ulong ExpectedHash = 0xA371ED87D32DCF81UL;

    [Test]
    public void IteratedRandomWalk_GoldenSnapshotHash_FixedSeedAndConfig()
    {
        const int res = 48;
        var domain = new GridDomain2D(res, res);
        var mask = new MaskGrid2D(domain, Allocator.Temp, clearToZero: true);

        try
        {
            var rng = new Random(123456u);
            int2 start = new int2(res / 2, res / 2);

            IteratedRandomWalk2D.Carve(
                ref mask, ref rng,
                start: start,
                iterations: 25,
                walkLengthMin: 20,
                walkLengthMax: 60,
                brushRadius: 1,
                randomStartChance: 0.35f,
                skewX: 0.15f,
                skewY: -0.10f,
                maxRetries: 16);

            // NOTE: includeDimensions defaults to true; keep it that way for this gate.
            ulong actual = mask.SnapshotHash64();

            if (ExpectedHash == 0UL)
            {
                Assert.Fail(
                    "Golden hash not locked yet.\n" +
                    $"Set ExpectedHash to 0x{actual:X16}UL and re-run.\n" +
                    "(Only update the constant when you intentionally change the output contract.)");
            }

            Assert.AreEqual(
                ExpectedHash, actual,
                $"Golden SnapshotHash64 changed!\nExpected: 0x{ExpectedHash:X16}\nActual:   0x{actual:X16}\n" +
                "If this change is intentional, update ExpectedHash. Otherwise, investigate for nondeterminism/regression.");
        }
        finally
        {
            mask.Dispose();
        }
    }
}
