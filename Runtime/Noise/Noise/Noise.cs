using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

using static Unity.Mathematics.math;

namespace Islands
{
    /// <summary>
    /// Fractal accumulation mode for multi-octave noise evaluation.
    /// Applies to all <see cref="Noise.INoise"/> types via <see cref="Noise.GetFractalNoise{N}"/>.
    ///
    /// Phase N5.b: declared in Islands.PCG.Layout.Maps (carried, not functional).
    /// Phase N5.c: migrated to Islands namespace, functional in noise runtime.
    /// </summary>
    public enum FractalMode : byte
    {
        /// <summary>Standard fBm — octaves summed with decaying amplitude. Default.</summary>
        Standard = 0,
        /// <summary>Ridged multifractal — abs(noise) with offset/gain feedback (Musgrave).</summary>
        Ridged = 1,
    }

    public static partial class Noise
    {
        [Serializable]
        public struct Settings
        {
            public int seed;

            // Scale of the noise, how fast it changes. Uniform (domain scale can be nonuniform).
            // The higher the frequency or scale, the faster it changes, thus smaller features.
            [Min(1)] public int frequency;
            // Number of samples taken at different frequencies.
            [Range(1, 6)] public int octaves;
            // Frequency scaling, how it changes between octaves/samples.
            // The higher the lacunarity the more gaps or space there is between octaves.
            [Range(2, 4)] public int lacunarity;
            // Amplitude scaling, how it changes between octaves/samples.
            [Range(0f, 1f)] public float persistence;

            // --- N5.c additions: ridged multifractal parameters ---

            /// <summary>
            /// Fractal accumulation mode. Standard = existing fBm (default, zero-init safe).
            /// Ridged = Musgrave ridged multifractal with offset/gain feedback.
            /// </summary>
            public FractalMode fractalMode;

            /// <summary>
            /// Ridged multifractal offset. Controls ridge sharpness.
            /// Only used when <see cref="fractalMode"/> == <see cref="FractalMode.Ridged"/>.
            /// Canonical default: 1.0 (Musgrave).
            /// </summary>
            public float ridgedOffset;

            /// <summary>
            /// Ridged multifractal gain. Controls heterogeneity feedback.
            /// Only used when <see cref="fractalMode"/> == <see cref="FractalMode.Ridged"/>.
            /// Canonical default: 2.0 (Musgrave).
            /// </summary>
            public float ridgedGain;

            public static Settings Default => new Settings
            {
                frequency = 4,
                octaves = 1,
                lacunarity = 2,
                persistence = 0.5f,
                fractalMode = FractalMode.Standard,
                ridgedOffset = 1.0f,
                ridgedGain = 2.0f,
            };
        }

        public interface INoise
        {
            Sample4 GetNoise4(float4x3 positions, SmallXXHash4 hash, int frequency);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Sample4 GetFractalNoise<N>(
            float4x3 position, Settings settings
        ) where N : struct, INoise
        {
            if (settings.fractalMode == FractalMode.Ridged)
                return GetRidgedFractalNoise<N>(position, settings);

            // --- Standard fBm — unchanged from pre-N5.c ---
            var hash = SmallXXHash4.Seed(settings.seed);
            int frequency = settings.frequency;
            float amplitude = 1f, amplitudeSum = 0f;
            Sample4 sum = default;

            for (int o = 0; o < settings.octaves; o++)
            {
                sum += amplitude * default(N).GetNoise4(position, hash + o, frequency);
                frequency *= settings.lacunarity;
                amplitude *= settings.persistence;
                amplitudeSum += amplitude;
            }

            return sum / amplitudeSum;
        }

        /// <summary>
        /// Musgrave ridged multifractal accumulation. Called by
        /// <see cref="GetFractalNoise{N}"/> when <c>settings.fractalMode == Ridged</c>.
        ///
        /// Algorithm: each octave computes <c>signal = (offset - |noise|)^2</c>,
        /// with inter-octave feedback <c>weight = clamp(signal * gain, 0, 1)</c>.
        /// Produces sharp ridges with heterogeneous detail.
        ///
        /// Returns <see cref="Sample4"/> with <c>.v</c> populated; derivatives are
        /// zero because <c>abs()</c> breaks derivative continuity.
        ///
        /// Phase N5.c.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Sample4 GetRidgedFractalNoise<N>(
            float4x3 position, Settings settings
        ) where N : struct, INoise
        {
            var hash = SmallXXHash4.Seed(settings.seed);
            int frequency = settings.frequency;
            float amplitude = 1f, amplitudeSum = 1f;
            float offset = settings.ridgedOffset;
            float gain = settings.ridgedGain;

            // First octave — no feedback
            float4 signal = offset - abs(
                default(N).GetNoise4(position, hash, frequency).v);
            signal *= signal;
            float4 result = signal;

            // Subsequent octaves with inter-octave feedback
            for (int o = 1; o < settings.octaves; o++)
            {
                frequency *= settings.lacunarity;
                amplitude *= settings.persistence;

                float4 weight = clamp(signal * gain, 0f, 1f);

                signal = offset - abs(
                    default(N).GetNoise4(position, hash + o, frequency).v);
                signal *= signal;
                signal *= weight;

                result += signal * amplitude;
                amplitudeSum += amplitude;
            }

            Sample4 sum = default;
            sum.v = result / amplitudeSum;
            return sum;
        }

        [BurstCompile(FloatPrecision.Standard, FloatMode.Fast, CompileSynchronously = true)]
        public struct Job<N> : IJobFor where N : struct, INoise
        {
            [Unity.Collections.ReadOnly] public NativeArray<float3x4> positions;
            [WriteOnly] public NativeArray<float4> noise;

            public Settings settings;
            public float3x4 domainTRS;

            public void Execute(int i) => noise[i] = GetFractalNoise<N>(
                domainTRS.TransformVectors(transpose(positions[i])), settings
            ).v;

            public static JobHandle ScheduleParallel(
                NativeArray<float3x4> positions, NativeArray<float4> noise,
                Settings settings, SpaceTRS domainTRS, int resolution, JobHandle dependency
            ) => new Job<N>
            {
                positions = positions,
                noise = noise,
                settings = settings,
                domainTRS = domainTRS.Matrix,
            }.ScheduleParallel(positions.Length, resolution, dependency);
        }

        public delegate JobHandle ScheduleDelegate(
                NativeArray<float3x4> positions, NativeArray<float4> noise,
                Settings settings, SpaceTRS domainTRS, int resolution, JobHandle dependency
        );
    }
}