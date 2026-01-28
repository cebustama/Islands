using System;

namespace Islands.PCG.Grids
{
    public partial struct MaskGrid2D
    {
        /// <summary>
        /// Deterministic 64-bit fingerprint of the entire mask contents.
        /// Intended for snapshot tests, regression gates, and debug logs.
        ///
        /// Word-wise hashing (NativeArray&lt;ulong&gt;) is much faster than per-cell hashing.
        /// Tail bits are masked to keep determinism for non-multiple-of-64 sizes.
        /// </summary>
        public ulong SnapshotHash64(bool includeDimensions = true)
        {
            // FNV-1a 64-bit
            const ulong fnvOffset = 1469598103934665603UL;
            const ulong fnvPrime = 1099511628211UL;

            if (!_words.IsCreated || _words.Length == 0)
            {
                // Still mix dims for “empty but sized” identity if desired.
                if (!includeDimensions) return fnvOffset;

                ulong empty = fnvOffset;
                empty = FnvMixU64(empty, (ulong)Domain.Width, fnvPrime);
                empty = FnvMixU64(empty, (ulong)Domain.Height, fnvPrime);
                return empty;
            }

            ulong h = fnvOffset;

            if (includeDimensions)
            {
                h = FnvMixU64(h, (ulong)Domain.Width, fnvPrime);
                h = FnvMixU64(h, (ulong)Domain.Height, fnvPrime);
                h = FnvMixU64(h, (ulong)Domain.Length, fnvPrime);
            }

            int lastIndex = _words.Length - 1;
            ulong lastMask = LastWordValidMask();

            for (int i = 0; i < _words.Length; i++)
            {
                ulong w = _words[i];
                if (i == lastIndex) w &= lastMask;

                h = FnvMixU64(h, w, fnvPrime);
            }

            return h;
        }

        private static ulong FnvMixU64(ulong h, ulong value, ulong fnvPrime)
        {
            // Mix 8 bytes so order/bit patterns matter well.
            h ^= (byte)(value); h *= fnvPrime;
            h ^= (byte)(value >> 8); h *= fnvPrime;
            h ^= (byte)(value >> 16); h *= fnvPrime;
            h ^= (byte)(value >> 24); h *= fnvPrime;
            h ^= (byte)(value >> 32); h *= fnvPrime;
            h ^= (byte)(value >> 40); h *= fnvPrime;
            h ^= (byte)(value >> 48); h *= fnvPrime;
            h ^= (byte)(value >> 56); h *= fnvPrime;
            return h;
        }
    }
}
