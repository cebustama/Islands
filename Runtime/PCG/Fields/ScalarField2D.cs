using System;
using Unity.Collections;
using Unity.Mathematics;
using Islands.PCG.Core;

namespace Islands.PCG.Fields
{
    /// <summary>
    /// A dense 2D scalar field backed by a NativeArray<float> (1 float per cell).
    ///
    /// Designed for data-oriented PCG workflows (Burst/Jobs-friendly) and deterministic operations.
    /// This type owns native memory and must be disposed.
    ///
    /// IMPORTANT: This is a mutable struct. Avoid copying it by value; prefer passing by ref/in.
    /// </summary>
    public struct ScalarField2D : IDisposable
    {
        public GridDomain2D Domain { get; private set; }

        /// <summary>
        /// Flat storage of scalar values in row-major order: index = x + y * Width.
        /// </summary>
        public NativeArray<float> Values;

        public int Length => Domain.Length;

        public bool IsCreated => Values.IsCreated;

        public ScalarField2D(
            in GridDomain2D domain,
            Allocator allocator,
            NativeArrayOptions options = NativeArrayOptions.ClearMemory
        )
        {
            Domain = domain;
            Values = new NativeArray<float>(domain.Length, allocator, options);
        }

        /// <summary>
        /// Releases the underlying native storage.
        /// Safe to call multiple times (no-op if already disposed).
        /// </summary>
        public void Dispose()
        {
            if (Values.IsCreated)
            {
                Values.Dispose();
            }
        }

        /// <summary>
        /// Gets the scalar value at (x,y). Throws if out of bounds.
        /// </summary>
        public float Get(int x, int y)
        {
            EnsureInBounds(x, y);
            return Values[Domain.Index(x, y)];
        }

        /// <summary>
        /// Sets the scalar value at (x,y). Throws if out of bounds.
        /// </summary>
        public void Set(int x, int y, float value)
        {
            EnsureInBounds(x, y);
            Values[Domain.Index(x, y)] = value;
        }

        /// <summary>
        /// Unchecked get: no bounds check. Use only in hot loops where you already guarantee bounds.
        /// </summary>
        public float GetUnchecked(int x, int y)
        {
            return Values[Domain.Index(x, y)];
        }

        /// <summary>
        /// Unchecked set: no bounds check. Use only in hot loops where you already guarantee bounds.
        /// </summary>
        public void SetUnchecked(int x, int y, float value)
        {
            Values[Domain.Index(x, y)] = value;
        }

        /// <summary>
        /// Fills the entire field with a constant value.
        /// Simple loop for now; can be replaced by a Burst job later.
        /// </summary>
        public void Clear(float value = 0f)
        {
            for (int i = 0; i < Values.Length; i++)
            {
                Values[i] = value;
            }
        }

        /// <summary>
        /// Convenience indexer for (x,y). Bounds-checked.
        /// </summary>
        public float this[int x, int y]
        {
            get => Get(x, y);
            set => Set(x, y, value);
        }

        private void EnsureInBounds(int x, int y)
        {
            if (!Domain.InBounds(x, y))
            {
                throw new ArgumentOutOfRangeException(
                    $"({x},{y}) is out of bounds for domain {Domain.Width}x{Domain.Height}."
                );
            }
        }
    }
}
