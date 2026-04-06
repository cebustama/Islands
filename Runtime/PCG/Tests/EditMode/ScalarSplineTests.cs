using NUnit.Framework;
using Unity.Mathematics;
using Islands.PCG.Fields;

/// <summary>
/// EditMode tests for <see cref="ScalarSpline"/>.
/// N2 — Noise Composition Improvements.
/// </summary>
[TestFixture]
public class ScalarSplineTests
{
    // ------------------------------------------------------------------
    // Identity
    // ------------------------------------------------------------------

    [Test]
    public void Identity_IsIdentity_ReturnsTrue()
    {
        var s = ScalarSpline.Identity;
        Assert.IsTrue(s.IsIdentity);
    }

    [Test]
    public void Default_IsIdentity_ReturnsTrue()
    {
        ScalarSpline s = default;
        Assert.IsTrue(s.IsIdentity);
    }

    [TestCase(0f)]
    [TestCase(0.25f)]
    [TestCase(0.5f)]
    [TestCase(0.75f)]
    [TestCase(1f)]
    public void Identity_Evaluate_ReturnsInput(float t)
    {
        var s = ScalarSpline.Identity;
        Assert.AreEqual(t, s.Evaluate(t), 0f, $"Identity spline must return exact input for t={t}");
    }

    [TestCase(0f)]
    [TestCase(0.5f)]
    [TestCase(1f)]
    public void Default_Evaluate_ReturnsInput(float t)
    {
        ScalarSpline s = default;
        Assert.AreEqual(t, s.Evaluate(t), 0f, $"Default spline must return input unchanged for t={t}");
    }

    // ------------------------------------------------------------------
    // Control-point exact values
    // ------------------------------------------------------------------

    [Test]
    public void KnownPoints_Evaluate_ReturnsExactOutput()
    {
        // Flatten lowlands, steepen peaks.
        var s = new ScalarSpline(
            new float[] { 0f, 0.5f, 1f },
            new float[] { 0f, 0.1f, 1f });

        Assert.AreEqual(0f, s.Evaluate(0f), 0f);
        Assert.AreEqual(0.1f, s.Evaluate(0.5f), 1e-7f);
        Assert.AreEqual(1f, s.Evaluate(1f), 0f);
    }

    // ------------------------------------------------------------------
    // Interpolation
    // ------------------------------------------------------------------

    [Test]
    public void Interpolation_Midpoint_IsExactLerp()
    {
        var s = new ScalarSpline(
            new float[] { 0f, 1f },
            new float[] { 0f, 0.6f });

        // Midpoint of single segment: lerp(0, 0.6, 0.5) = 0.3
        Assert.AreEqual(0.3f, s.Evaluate(0.5f), 1e-7f);
    }

    [Test]
    public void Interpolation_ThreePoints_SegmentBoundaryCorrect()
    {
        var s = new ScalarSpline(
            new float[] { 0f, 0.4f, 1f },
            new float[] { 0f, 0.8f, 1f });

        // At t=0.2, we're in segment [0, 0.4]. frac = 0.2/0.4 = 0.5.
        // lerp(0, 0.8, 0.5) = 0.4
        Assert.AreEqual(0.4f, s.Evaluate(0.2f), 1e-6f);

        // At t=0.7, we're in segment [0.4, 1.0]. frac = (0.7-0.4)/(1.0-0.4) = 0.5.
        // lerp(0.8, 1.0, 0.5) = 0.9
        Assert.AreEqual(0.9f, s.Evaluate(0.7f), 1e-6f);
    }

    // ------------------------------------------------------------------
    // Boundary clamping
    // ------------------------------------------------------------------

    [Test]
    public void Evaluate_BelowFirstInput_ClampsToFirstOutput()
    {
        var s = new ScalarSpline(
            new float[] { 0.2f, 0.8f },
            new float[] { 0.5f, 0.9f });

        Assert.AreEqual(0.5f, s.Evaluate(0f), 0f);
        Assert.AreEqual(0.5f, s.Evaluate(-1f), 0f);
    }

    [Test]
    public void Evaluate_AboveLastInput_ClampsToLastOutput()
    {
        var s = new ScalarSpline(
            new float[] { 0.2f, 0.8f },
            new float[] { 0.5f, 0.9f });

        Assert.AreEqual(0.9f, s.Evaluate(1f), 0f);
        Assert.AreEqual(0.9f, s.Evaluate(99f), 0f);
    }

    // ------------------------------------------------------------------
    // Construction validation
    // ------------------------------------------------------------------

    [Test]
    public void Constructor_NullInputs_Throws()
    {
        Assert.Throws<System.ArgumentNullException>(() =>
            new ScalarSpline(null, new float[] { 0f, 1f }));
    }

    [Test]
    public void Constructor_TooFewPoints_Throws()
    {
        Assert.Throws<System.ArgumentException>(() =>
            new ScalarSpline(new float[] { 0f }, new float[] { 0f }));
    }

    [Test]
    public void Constructor_UnsortedInputs_Throws()
    {
        Assert.Throws<System.ArgumentException>(() =>
            new ScalarSpline(new float[] { 0.5f, 0.2f, 1f }, new float[] { 0f, 0.5f, 1f }));
    }

    [Test]
    public void Constructor_LengthMismatch_Throws()
    {
        Assert.Throws<System.ArgumentException>(() =>
            new ScalarSpline(new float[] { 0f, 1f }, new float[] { 0f }));
    }

    [Test]
    public void Constructor_DefensiveCopy_CallerCannotMutate()
    {
        float[] ins = { 0f, 1f };
        float[] outs = { 0f, 1f };
        var s = new ScalarSpline(ins, outs);

        // Mutate caller's arrays.
        ins[1] = 999f;
        outs[1] = 999f;

        // Spline should be unaffected.
        Assert.AreEqual(1f, s.Evaluate(1f), 0f);
    }

    // ------------------------------------------------------------------
    // PowerCurve factory
    // ------------------------------------------------------------------

    [Test]
    public void PowerCurve_Exponent1_IsLinear()
    {
        var s = ScalarSpline.PowerCurve(1f, 5);

        for (int i = 0; i <= 4; i++)
        {
            float t = i / 4f;
            Assert.AreEqual(t, s.Evaluate(t), 1e-6f, $"PowerCurve(1.0) should be identity at t={t}");
        }
    }

    [Test]
    public void PowerCurve_Exponent2_MidpointIsQuarter()
    {
        // pow(0.5, 2) = 0.25
        var s = ScalarSpline.PowerCurve(2f, 17);
        Assert.AreEqual(0.25f, s.Evaluate(0.5f), 0.01f);
    }

    // ------------------------------------------------------------------
    // AnimationCurve bridge
    // ------------------------------------------------------------------

    [Test]
    public void FromAnimationCurve_NullCurve_ReturnsIdentity()
    {
        var s = ScalarSpline.FromAnimationCurve(null);
        Assert.IsTrue(s.IsIdentity);
    }

    [Test]
    public void FromAnimationCurve_LinearCurve_ProducesNearIdentity()
    {
        var curve = UnityEngine.AnimationCurve.Linear(0f, 0f, 1f, 1f);
        var s = ScalarSpline.FromAnimationCurve(curve, 16);

        // Not IsIdentity (16 points, not 2), but evaluation is identity.
        Assert.AreEqual(0f, s.Evaluate(0f), 1e-6f);
        Assert.AreEqual(0.5f, s.Evaluate(0.5f), 1e-4f);
        Assert.AreEqual(1f, s.Evaluate(1f), 1e-6f);
    }

    [Test]
    public void FromAnimationCurve_NonTrivialCurve_DiffersFromIdentity()
    {
        // S-curve: flat at bottom, steep at top.
        var curve = new UnityEngine.AnimationCurve(
            new UnityEngine.Keyframe(0f, 0f),
            new UnityEngine.Keyframe(0.5f, 0.1f),
            new UnityEngine.Keyframe(1f, 1f));
        var s = ScalarSpline.FromAnimationCurve(curve, 32);

        // At t=0.5, the curve outputs ~0.1, not 0.5.
        float mid = s.Evaluate(0.5f);
        Assert.Less(mid, 0.3f, "S-curve should flatten the midrange significantly");
    }

    // ------------------------------------------------------------------
    // PointCount
    // ------------------------------------------------------------------

    [Test]
    public void PointCount_Default_IsZero()
    {
        ScalarSpline s = default;
        Assert.AreEqual(0, s.PointCount);
    }

    [Test]
    public void PointCount_Identity_IsTwo()
    {
        Assert.AreEqual(2, ScalarSpline.Identity.PointCount);
    }

    [Test]
    public void PointCount_Custom_MatchesInput()
    {
        var s = new ScalarSpline(
            new float[] { 0f, 0.3f, 0.7f, 1f },
            new float[] { 0f, 0.1f, 0.9f, 1f });
        Assert.AreEqual(4, s.PointCount);
    }
}