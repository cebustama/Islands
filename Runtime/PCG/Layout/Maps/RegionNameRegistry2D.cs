using System.Collections.Generic;

namespace Islands.PCG.Layout.Maps
{
    /// <summary>
    /// Sibling registry that records metadata and (optionally) names for each
    /// biome region produced by <see cref="Stages.Stage_Regions2D"/>.
    ///
    /// This class is intentionally separate from the field grid (D6):
    ///   • <see cref="MapFieldId.BiomeRegionId"/> stores integer IDs per cell.
    ///   • <see cref="RegionNameRegistry2D"/> stores per-ID metadata + names.
    ///   • The <see cref="MapLayerId"/> namespace remains grid-only.
    ///
    /// Lifecycle:
    ///   Built during <see cref="Stages.Stage_Regions2D.Execute"/>.
    ///   Retrieved via <see cref="Stages.Stage_Regions2D.LastBuiltRegistry"/>.
    ///   Valid only for the map run that produced it — do not cache across seeds (R-7).
    ///
    /// Cross-seed stability is an explicit NON-GOAL (R-7).  Region IDs in this
    /// registry correspond to a specific generation run and must not be serialised
    /// or compared across runs with different seeds.
    /// </summary>
    public sealed class RegionNameRegistry2D
    {
        // =====================================================================
        // RegionEntry — per-region metadata
        // =====================================================================

        public struct RegionEntry
        {
            /// <summary>BiomeType int value for this region.</summary>
            public int BiomeId;

            /// <summary>
            /// Row-major index of the first cell encountered during CCA scan.
            /// Stable within a run; used as the hash input for name selection.
            /// </summary>
            public int AnchorIndex;

            /// <summary>Final cell count after speck merge.</summary>
            public int CellCount;

            /// <summary>
            /// Authored name from <see cref="RegionNameTableAsset"/>, or
            /// <see cref="string.Empty"/> when no table was assigned.
            /// </summary>
            public string Name;
        }

        // =====================================================================
        // Storage
        // =====================================================================

        private readonly Dictionary<int, RegionEntry> _entries
            = new Dictionary<int, RegionEntry>(64);

        // =====================================================================
        // Internal build API (called by Stage_Regions2D only)
        // =====================================================================

        internal void Add(int regionId, RegionEntry entry)
        {
            _entries[regionId] = entry;
        }

        // =====================================================================
        // Public read API
        // =====================================================================

        /// <summary>Number of regions in this registry.</summary>
        public int Count => _entries.Count;

        /// <summary>
        /// Returns the <see cref="RegionEntry"/> for <paramref name="regionId"/>,
        /// or a default (empty) entry when the ID is not present.
        /// </summary>
        public RegionEntry Get(int regionId)
        {
            return _entries.TryGetValue(regionId, out RegionEntry e) ? e : default;
        }

        /// <summary>
        /// Returns true and populates <paramref name="entry"/> when the ID exists.
        /// </summary>
        public bool TryGet(int regionId, out RegionEntry entry)
        {
            return _entries.TryGetValue(regionId, out entry);
        }

        /// <summary>
        /// Returns all region IDs in ascending order.  Allocates a new array;
        /// intended for editor tooling and tests, not hot paths.
        /// </summary>
        public int[] GetAllIds()
        {
            var ids = new int[_entries.Count];
            _entries.Keys.CopyTo(ids, 0);
            System.Array.Sort(ids);
            return ids;
        }

        /// <summary>
        /// Returns the name for <paramref name="regionId"/>, or
        /// <see cref="string.Empty"/> when absent or unnamed.
        /// </summary>
        public string GetName(int regionId)
        {
            return _entries.TryGetValue(regionId, out RegionEntry e) ? e.Name : string.Empty;
        }
    }
}