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

            public static Settings Default => new Settings
            {
                frequency = 4,
                octaves = 1,
                lacunarity = 2,
                persistence = 0.5f,
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
