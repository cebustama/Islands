using System;
using Unity.Collections;
using Unity.Mathematics;
using Islands.PCG.Core;

namespace Islands.PCG.Grids
{
    /// <summary>
    /// A compact 2D 0/1 grid backed by a bitset (1 bit per cell) stored in a NativeArray&lt;ulong&gt;.
    ///
    /// Designed for data-oriented PCG workflows (Burst/Jobs-friendly) and fast bulk operations.
    /// This type owns native memory and must be disposed.
    ///
    /// IMPORTANT: This is a mutable struct. Avoid copying it by value; prefer passing by ref.
    /// </summary>
    public struct MaskGrid2D : IDisposable
    {
        /// <summary>
        /// The discrete grid domain (width/height) that defines valid coordinates and indexing.
        /// </summary>
        public GridDomain2D Domain { get; }

        /// <summary>
        /// Total number of cells (bits) in the domain.
        /// </summary>
        public int LengthBits => Domain.Length;

        /// <summary>
        /// Number of 64-bit words used to store the bitset.
        /// </summary>
        public int WordCount => _words.IsCreated ? _words.Length : 0;

        /// <summary>
        /// Returns true if the underlying NativeArray storage has been allocated.
        /// </summary>
        public bool IsCreated => _words.IsCreated;

        private NativeArray<ulong> _words;

        /// <summary>
        /// Allocates a new mask grid for the given domain.
        /// </summary>
        /// <param name="domain">Grid domain (width/height).</param>
        /// <param name="allocator">Allocator to use (Persistent/TempJob/etc.).</param>
        /// <param name="clearToZero">If true, initializes all bits to 0.</param>
        public MaskGrid2D(GridDomain2D domain, Allocator allocator, bool clearToZero = true)
        {
            Domain = domain;

            int totalBits = domain.Length;
            int wordCount = (totalBits + 63) >> 6; // ceil(bits/64)

            _words = new NativeArray<ulong>(wordCount, allocator,
                clearToZero ? NativeArrayOptions.ClearMemory : NativeArrayOptions.UninitializedMemory);

            // If we didn't clear, we still want out-of-range tail bits to be deterministic (0).
            if (!clearToZero)
            {
                ClearTailBits();
            }
        }

        /// <summary>
        /// Releases the underlying native storage.
        /// Safe to call multiple times (no-op if already disposed).
        /// </summary>
        public void Dispose()
        {
            if (_words.IsCreated)
            {
                _words.Dispose();
            }
        }

        /// <summary>
        /// Gets the bit value at (x,y). Throws if out of bounds.
        /// </summary>
        public bool Get(int x, int y)
        {
            EnsureInBounds(x, y);
            GetWordAndMaskUnchecked(x, y, out int wordIndex, out ulong mask);
            return (_words[wordIndex] & mask) != 0UL;
        }

        /// <summary>
        /// Sets the bit value at (x,y). Throws if out of bounds.
        /// </summary>
        public void Set(int x, int y, bool value)
        {
            EnsureInBounds(x, y);
            GetWordAndMaskUnchecked(x, y, out int wordIndex, out ulong mask);

            ulong w = _words[wordIndex];
            _words[wordIndex] = value ? (w | mask) : (w & ~mask);
        }

        /// <summary>
        /// Clears the entire mask to 0.
        /// </summary>
        public void Clear()
        {
            if (!_words.IsCreated) return;

            for (int i = 0; i < _words.Length; i++)
            {
                _words[i] = 0UL;
            }
        }

        /// <summary>
        /// Fills the entire mask with the given value.
        /// </summary>
        /// <param name="value">If true, all cells become 1. If false, all cells become 0.</param>
        public void Fill(bool value)
        {
            if (!_words.IsCreated) return;

            ulong w = value ? ulong.MaxValue : 0UL;
            for (int i = 0; i < _words.Length; i++)
            {
                _words[i] = w;
            }

            // Keep out-of-range bits deterministic (0).
            if (value)
            {
                ClearTailBits();
            }
        }

        // ----------------------------
        // C5: fast, word-wise boolean ops
        // ----------------------------

        /// <summary>
        /// Copies all bits from <paramref name="other"/> into this mask (word-wise).
        /// </summary>
        public void CopyFrom(in MaskGrid2D other)
        {
            EnsureCompatible(in other, nameof(CopyFrom));

            for (int i = 0; i < _words.Length; i++)
            {
                _words[i] = other._words[i];
            }

            // Defensive: ensure determinism even if source had stray tail bits.
            ClearTailBits();
        }

        /// <summary>
        /// Union: this |= other (word-wise).
        /// </summary>
        public void Or(in MaskGrid2D other)
        {
            EnsureCompatible(in other, nameof(Or));

            for (int i = 0; i < _words.Length; i++)
            {
                _words[i] |= other._words[i];
            }

            ClearTailBits();
        }

        /// <summary>
        /// Intersection: this &= other (word-wise).
        /// </summary>
        public void And(in MaskGrid2D other)
        {
            EnsureCompatible(in other, nameof(And));

            for (int i = 0; i < _words.Length; i++)
            {
                _words[i] &= other._words[i];
            }

            ClearTailBits();
        }

        /// <summary>
        /// Subtract: this &= ~other (word-wise).
        /// </summary>
        public void AndNot(in MaskGrid2D other)
        {
            EnsureCompatible(in other, nameof(AndNot));

            for (int i = 0; i < _words.Length; i++)
            {
                _words[i] &= ~other._words[i];
            }

            ClearTailBits();
        }

        /// <summary>
        /// Counts how many cells are set to 1 in the grid.
        /// This is O(WordCount) and is suitable for snapshot tests and quick metrics.
        /// </summary>
        public int CountOnes()
        {
            if (!_words.IsCreated) return 0;

            int count = 0;

            int lastIndex = _words.Length - 1;
            ulong lastMask = LastWordValidMask();

            for (int i = 0; i < _words.Length; i++)
            {
                ulong w = _words[i];
                if (i == lastIndex)
                {
                    w &= lastMask;
                }
                count += PopCount(w);
            }

            return count;
        }

        /// <summary>
        /// Unchecked get: no bounds check. Use only in hot loops where you already guarantee bounds.
        /// </summary>
        public bool GetUnchecked(int x, int y)
        {
            GetWordAndMaskUnchecked(x, y, out int wordIndex, out ulong mask);
            return (_words[wordIndex] & mask) != 0UL;
        }

        /// <summary>
        /// Unchecked set: no bounds check. Use only in hot loops where you already guarantee bounds.
        /// </summary>
        public void SetUnchecked(int x, int y, bool value)
        {
            GetWordAndMaskUnchecked(x, y, out int wordIndex, out ulong mask);

            ulong w = _words[wordIndex];
            _words[wordIndex] = value ? (w | mask) : (w & ~mask);
        }

        /// <summary>
        /// Convenience indexer for (x,y). Bounds-checked.
        /// </summary>
        public bool this[int x, int y]
        {
            get => Get(x, y);
            set => Set(x, y, value);
        }

        private void EnsureCompatible(in MaskGrid2D other, string opName)
        {
            if (!_words.IsCreated)
                throw new InvalidOperationException($"{opName}: this mask is not created/allocated.");
            if (!other._words.IsCreated)
                throw new InvalidOperationException($"{opName}: other mask is not created/allocated.");

            // Domain is immutable, so width/height match is enough.
            if (Domain.Width != other.Domain.Width || Domain.Height != other.Domain.Height)
            {
                throw new ArgumentException(
                    $"{opName}: domain mismatch (this={Domain.Width}x{Domain.Height}, other={other.Domain.Width}x{other.Domain.Height}).");
            }

            if (_words.Length != other._words.Length)
            {
                // Should not happen if domain matches, but keep it explicit and safe.
                throw new ArgumentException(
                    $"{opName}: word storage mismatch (thisWords={_words.Length}, otherWords={other._words.Length}).");
            }
        }

        private void EnsureInBounds(int x, int y)
        {
            if (!Domain.InBounds(x, y))
            {
                throw new ArgumentOutOfRangeException(
                    $"Coordinate ({x},{y}) is out of bounds for domain {Domain.Width}x{Domain.Height}.");
            }
        }

        private void GetWordAndMaskUnchecked(int x, int y, out int wordIndex, out ulong mask)
        {
            int idx = Domain.Index(x, y);
            wordIndex = idx >> 6;          // / 64
            int bit = idx & 63;            // % 64
            mask = 1UL << bit;
        }

        private ulong LastWordValidMask()
        {
            int totalBits = Domain.Length;
            int wordCount = _words.Length;

            // Bits used by all words except last:
            int usedBeforeLast = (wordCount - 1) << 6; // (wordCount-1) * 64
            int validBits = totalBits - usedBeforeLast; // 1..64

            return validBits >= 64 ? ulong.MaxValue : ((1UL << validBits) - 1UL);
        }

        private void ClearTailBits()
        {
            if (!_words.IsCreated || _words.Length == 0) return;

            int lastIndex = _words.Length - 1;
            ulong lastMask = LastWordValidMask();

            _words[lastIndex] &= lastMask;
        }

        // Burst-friendly popcount for ulong using two uint halves.
        private static int PopCount(ulong x)
        {
            uint lo = (uint)x;
            uint hi = (uint)(x >> 32);

            return (int)math.countbits(lo) + (int)math.countbits(hi);
        }
    }
}
