// Phase T2.0 — Layer 0: stepped height sampling.
// Spec: PCG_Roadmap.md §"Phase T2 — 3D Relief Adapters", slice T2.0.
// Written concretely, with no emitter seam (rule of two: seam extraction is T2.1).
//
// Temporary housing: lives in Islands.PCG.Inspection until the T1/T2 asmdef-ownership
// decision is resolved (roadmap open decision). Nothing here references inspection
// types; moving this file to the eventual relief-adapter assembly is mechanical.
//
// Determinism: pure function of (MapDataExport, policy). Same export + same policy
// ⇒ bit-identical output. Row-major (index = x + y * Width), matching MapDataExport.

using System;
using Islands.PCG.Layout.Maps;

namespace Islands.PCG.Inspection
{
    /// <summary>
    /// Quantization policy for stepped relief sampling (T2 Layer 0).
    ///
    /// <see cref="NormMin"/>/<see cref="NormMax"/> define the Height band mapped onto
    /// the level span. Values below NormMin clamp to level 0; values at or above
    /// NormMax clamp to <c>LevelCount - 1</c>. Post-W-aux.f, land Height occupies
    /// roughly [waterThreshold01, ~0.96], so quantizing over nominal [0, 1] compresses
    /// land into the upper half of the span — set NormMin to the active
    /// waterThreshold01 to spend the full span on land.
    /// </summary>
    [Serializable]
    public struct SteppedHeightPolicy
    {
        /// <summary>Number of discrete levels. Must be &gt;= 1.</summary>
        public int LevelCount;

        /// <summary>Height value mapped to the bottom of the level span.</summary>
        public float NormMin;

        /// <summary>Height value mapped to the top of the level span. Must be &gt; NormMin.</summary>
        public float NormMax;

        /// <summary>
        /// Default policy: 8 levels over [0.42553192, 1]. The floor mirrors the
        /// recalibrated <c>waterThreshold01</c> component default from W-aux.f.
        /// </summary>
        public static SteppedHeightPolicy Default => new SteppedHeightPolicy
        {
            LevelCount = 8,
            NormMin = 0.42553192f,
            NormMax = 1f
        };
    }

    /// <summary>
    /// T2 Layer 0. Converts a <see cref="MapDataExport"/> Height field into per-cell
    /// integer levels under a <see cref="SteppedHeightPolicy"/>. Pure; no Mesh, no
    /// scene, no UnityEngine dependencies.
    /// </summary>
    public static class SteppedHeightSampler2D
    {
        /// <summary>
        /// Samples quantized levels from the export's Height field.
        ///
        /// Returns false (and null <paramref name="levels"/>) when the Height field
        /// was not exported — the data-shaped failure. Throws on programmer errors:
        /// null export, LevelCount &lt; 1, or NormMax &lt;= NormMin.
        ///
        /// On success, <paramref name="levels"/> is a freshly allocated row-major
        /// array (index = x + y * Width) with values in [0, LevelCount - 1].
        /// </summary>
        public static bool TrySampleLevels(
            MapDataExport export, in SteppedHeightPolicy policy, out int[] levels)
        {
            if (export == null)
                throw new ArgumentNullException(nameof(export));
            if (policy.LevelCount < 1)
                throw new ArgumentOutOfRangeException(
                    nameof(policy), "LevelCount must be >= 1.");
            if (!(policy.NormMax > policy.NormMin))
                throw new ArgumentOutOfRangeException(
                    nameof(policy), "NormMax must be > NormMin.");

            levels = null;
            if (!export.HasField(MapFieldId.Height))
                return false;

            float[] height = export.GetField(MapFieldId.Height);
            float invRange = 1f / (policy.NormMax - policy.NormMin);
            int maxLevel = policy.LevelCount - 1;
            int n = export.Length;

            var result = new int[n];
            for (int i = 0; i < n; i++) // flat row-major walk; ordering inherited from export
            {
                float t = (height[i] - policy.NormMin) * invRange;
                if (t < 0f) t = 0f;
                else if (t > 1f) t = 1f;

                int level = (int)(t * policy.LevelCount);
                result[i] = level > maxLevel ? maxLevel : level; // t == 1 lands here
            }

            levels = result;
            return true;
        }

        /// <summary>
        /// World-space Y of a level's base surface: <c>yOffset + level * stepWorldHeight</c>.
        /// Kept in Layer 0 so every future emitter and host shares one level→world rule.
        /// </summary>
        public static float LevelToWorldY(int level, float stepWorldHeight, float yOffset)
            => yOffset + level * stepWorldHeight;
    }
}