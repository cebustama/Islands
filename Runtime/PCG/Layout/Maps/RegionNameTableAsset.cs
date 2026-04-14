using UnityEngine;

namespace Islands.PCG.Layout.Maps
{
    /// <summary>
    /// Authored lookup table: one name pool per <see cref="BiomeType"/>.
    ///
    /// Name selection is deterministic and seed-driven — no UnityEngine.Random.
    /// Given (seed, biome, anchorRowMajor), <see cref="SelectName"/> always
    /// returns the same string within a generation run.
    ///
    /// Contract — R-1 (determinism):
    ///   SelectName(seed, biome, anchor) is a pure function; identical inputs
    ///   always produce identical output.  Cross-seed stability is an explicit
    ///   non-goal (R-7 on Stage_Regions2D).
    ///
    /// Usage:
    ///   1. Create via Assets ▶ Islands PCG ▶ Region Name Table.
    ///   2. Populate <see cref="Pools"/> in the Inspector — one entry per biome.
    ///   3. Assign to <see cref="Stage_Regions2D.NameTable"/>.
    ///
    /// Empty/missing pools: <see cref="SelectName"/> returns <see cref="string.Empty"/>
    /// when the pool for the requested biome is absent or has no names.  The
    /// region is still recorded in <see cref="RegionNameRegistry2D"/> with an
    /// empty name rather than throwing.
    /// </summary>
    [CreateAssetMenu(
        menuName = "Islands PCG/Region Name Table",
        fileName = "RegionNameTable",
        order = 210)]
    public sealed class RegionNameTableAsset : ScriptableObject
    {
        // =====================================================================
        // Serialised data
        // =====================================================================

        [System.Serializable]
        public struct BiomeNamePool
        {
            [Tooltip("Biome this pool applies to.")]
            public BiomeType Biome;

            [Tooltip("Candidate names.  Must be non-empty for names to be assigned.")]
            public string[] Names;
        }

        [Tooltip("One pool per BiomeType.  Order does not matter; pools are looked up by Biome value.")]
        public BiomeNamePool[] Pools = System.Array.Empty<BiomeNamePool>();

        // =====================================================================
        // Runtime lookup (lazy build)
        // =====================================================================

        // Populated on first call to SelectName, cleared on OnValidate.
        private System.Collections.Generic.Dictionary<int, string[]> _poolMap;

        private void OnValidate()
        {
            _poolMap = null; // invalidate cache on Inspector edit
        }

        private void EnsurePoolMap()
        {
            if (_poolMap != null) return;

            _poolMap = new System.Collections.Generic.Dictionary<int, string[]>(Pools.Length);
            foreach (var p in Pools)
            {
                if (p.Names != null && p.Names.Length > 0)
                    _poolMap[(int)p.Biome] = p.Names;
            }
        }

        // =====================================================================
        // Public API
        // =====================================================================

        /// <summary>
        /// Returns a deterministic name from the pool for <paramref name="biome"/>.
        ///
        /// Hash: 32-bit Murmur-finaliser mix of (seed ⊕ biome·2246822519 ⊕ anchor·2654435761).
        /// No UnityEngine.Random — pure integer arithmetic.
        ///
        /// Returns <see cref="string.Empty"/> when the pool is absent or empty.
        /// </summary>
        public string SelectName(uint seed, BiomeType biome, int anchorRowMajor)
        {
            EnsurePoolMap();

            if (!_poolMap.TryGetValue((int)biome, out string[] pool)
                || pool.Length == 0)
            {
                return string.Empty;
            }

            uint h = seed
                   ^ unchecked((uint)((int)biome * (int)2246822519u))
                   ^ unchecked((uint)(anchorRowMajor * (int)2654435761u));

            // Murmur3-style finaliser mix for good avalanche on small inputs.
            h ^= h >> 16;
            h = unchecked(h * 0x85ebca6bu);
            h ^= h >> 13;
            h = unchecked(h * 0xc2b2ae35u);
            h ^= h >> 16;

            return pool[(int)(h % (uint)pool.Length)];
        }
    }
}