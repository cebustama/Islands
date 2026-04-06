using System;
using Unity.Mathematics;

namespace Islands.PCG.Fields
{
    /// <summary>
    /// Piecewise-linear spline for deterministic scalar field remapping.
    /// Immutable after construction. No RNG consumption. Pure function of input and control points.
    ///
    /// Typical usage: reshape a [0,1] height field through a designer-defined curve.
    /// Consumed by map stages as an inline post-processor (same category as pow() redistribution).
    ///
    /// Identity spline (two points: 0→0, 1→1) produces mathematically identical output
    /// to no remapping, preserving all existing golden hashes when defaulted.
    ///
    /// N2 — Noise Composition Improvements Roadmap.
    /// </summary>
    public readonly struct ScalarSpline
    {
        // Control points sorted ascending by input value. Length >= 2.
        // These are private readonly; no external mutation possible.
        private readonly float[] inputs;
        private readonly float[] outputs;

        // ---------------------------------------------------------------
        // Construction
        // ---------------------------------------------------------------

        /// <summary>
        /// Creates a spline from pre-sorted control points.
        /// </summary>
        /// <param name="inputs">Sorted ascending input values. Length >= 2. Not null.</param>
        /// <param name="outputs">Corresponding output values. Same length as inputs. Not null.</param>
        /// <exception cref="ArgumentNullException">If either array is null.</exception>
        /// <exception cref="ArgumentException">
        /// If arrays differ in length, have fewer than 2 points, or inputs are not sorted ascending.
        /// </exception>
        public ScalarSpline(float[] inputs, float[] outputs)
        {
            if (inputs == null) throw new ArgumentNullException(nameof(inputs));
            if (outputs == null) throw new ArgumentNullException(nameof(outputs));
            if (inputs.Length < 2)
                throw new ArgumentException("ScalarSpline requires at least 2 control points.", nameof(inputs));
            if (inputs.Length != outputs.Length)
                throw new ArgumentException("Input and output arrays must have the same length.");

            for (int i = 1; i < inputs.Length; i++)
            {
                if (inputs[i] < inputs[i - 1])
                    throw new ArgumentException(
                        $"Inputs must be sorted ascending. inputs[{i - 1}]={inputs[i - 1]} > inputs[{i}]={inputs[i]}.",
                        nameof(inputs));
            }

            // Defensive copy — caller cannot mutate our data after construction.
            this.inputs = new float[inputs.Length];
            this.outputs = new float[outputs.Length];
            Array.Copy(inputs, this.inputs, inputs.Length);
            Array.Copy(outputs, this.outputs, outputs.Length);
        }

        // ---------------------------------------------------------------
        // Evaluation
        // ---------------------------------------------------------------

        /// <summary>
        /// Evaluates the spline at a given input value using piecewise-linear interpolation.
        /// Values below the first control point clamp to the first output.
        /// Values above the last control point clamp to the last output.
        /// Deterministic: identical results for identical inputs on any platform.
        /// </summary>
        public float Evaluate(float t)
        {
            // Defensive: if this is a default(ScalarSpline) with null arrays, return t unchanged.
            if (inputs == null) return t;

            int n = inputs.Length;

            // Clamp to boundary outputs.
            if (t <= inputs[0]) return outputs[0];
            if (t >= inputs[n - 1]) return outputs[n - 1];

            // Binary search for the segment containing t.
            // For typical spline sizes (2–32 points) a linear scan would be fine,
            // but binary search is deterministic and O(log n) regardless.
            int lo = 0;
            int hi = n - 1;
            while (lo < hi - 1)
            {
                int mid = (lo + hi) >> 1;
                if (t < inputs[mid])
                    hi = mid;
                else
                    lo = mid;
            }

            // Piecewise linear interpolation within segment [lo, hi].
            float segIn0 = inputs[lo];
            float segIn1 = inputs[hi];
            float span = segIn1 - segIn0;

            // Guard against degenerate zero-length segment (duplicate input values).
            if (span <= 0f) return outputs[lo];

            float frac = (t - segIn0) / span;
            return math.lerp(outputs[lo], outputs[hi], frac);
        }

        // ---------------------------------------------------------------
        // Identity detection (fast-path guard)
        // ---------------------------------------------------------------

        /// <summary>
        /// True if this spline is the identity mapping (input == output for all values in [0,1]).
        /// Used as a zero-cost skip guard in stage code, analogous to the != 1.0f guard on pow().
        /// A default(ScalarSpline) with null arrays is also treated as identity.
        /// </summary>
        public bool IsIdentity
        {
            get
            {
                if (inputs == null) return true;
                if (inputs.Length != 2) return false;
                return inputs[0] == 0f && inputs[1] == 1f
                    && outputs[0] == 0f && outputs[1] == 1f;
            }
        }

        /// <summary>
        /// Number of control points, or 0 if this is a default (null-array) instance.
        /// </summary>
        public int PointCount => inputs?.Length ?? 0;

        // ---------------------------------------------------------------
        // Static factories
        // ---------------------------------------------------------------

        /// <summary>
        /// The identity spline: two points, (0,0) and (1,1). Evaluate(t) == t for all t in [0,1].
        /// </summary>
        public static ScalarSpline Identity => new ScalarSpline(
            new float[] { 0f, 1f },
            new float[] { 0f, 1f });

        /// <summary>
        /// Creates a spline that approximates pow(t, exponent) over [0,1],
        /// sampled at <paramref name="sampleCount"/> evenly spaced points.
        /// Useful for expressing J2-style power redistribution as a spline.
        /// </summary>
        /// <param name="exponent">Power curve exponent. 1.0 produces the identity spline.</param>
        /// <param name="sampleCount">Number of control points. Minimum 2.</param>
        public static ScalarSpline PowerCurve(float exponent, int sampleCount = 16)
        {
            if (sampleCount < 2) sampleCount = 2;

            var ins = new float[sampleCount];
            var outs = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / (sampleCount - 1);
                ins[i] = t;
                outs[i] = math.pow(t, exponent);
            }

            return new ScalarSpline(ins, outs);
        }

#if UNITY_EDITOR || UNITY_STANDALONE || UNITY_ANDROID || UNITY_IOS
        /// <summary>
        /// Samples a Unity AnimationCurve at evenly spaced points to produce a ScalarSpline.
        /// Returns <see cref="Identity"/> if the curve is null or has fewer than 2 keys.
        ///
        /// This is the authoring→runtime bridge: AnimationCurve lives on the preset (Inspector),
        /// ScalarSpline lives on MapTunables2D (runtime). The conversion happens in ToTunables().
        ///
        /// The AnimationCurve's cubic Bezier interpolation is baked into the piecewise-linear
        /// spline at the chosen sample resolution. 16 samples is sufficient for smooth curves
        /// on typical map resolutions (64–256 cells).
        /// </summary>
        /// <param name="curve">Source AnimationCurve. Null-safe (returns Identity).</param>
        /// <param name="sampleCount">Number of evenly spaced sample points. Minimum 2.</param>
        public static ScalarSpline FromAnimationCurve(UnityEngine.AnimationCurve curve, int sampleCount = 16)
        {
            if (curve == null || curve.length < 2)
                return Identity;
            if (sampleCount < 2) sampleCount = 2;

            var ins = new float[sampleCount];
            var outs = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / (sampleCount - 1);
                ins[i] = t;
                outs[i] = curve.Evaluate(t);
            }

            return new ScalarSpline(ins, outs);
        }
#endif
    }
}