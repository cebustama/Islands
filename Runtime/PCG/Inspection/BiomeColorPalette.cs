// Phase V.b — Biome → Color lookup asset.
// Spec: planning/active/Phase_V_Design.md §7.
// Decision rationale: V-DD-7 (BiomeColorPalette SO for Biome field; deterministic
// hash-color for BiomeRegionId — region IDs are anonymous so live elsewhere).
//
// Read-only inspection asset. Authored by hand in the Inspector. Replace the
// shipped default at any time without touching code.

using Islands.PCG.Layout.Maps;
using UnityEngine;

namespace Islands.PCG.Inspection
{
    /// <summary>
    /// Editor-authored mapping from <see cref="BiomeType"/> ordinal to display
    /// <see cref="Color"/>. Consumed by <see cref="PCGRuntimeOverlay"/> when its
    /// color field is set to <c>Biome</c>.
    ///
    /// Lookup is O(1) after lazy table build. Table is rebuilt when entries change
    /// (detected via <see cref="OnValidate"/>).
    ///
    /// Multiple entries with the same <see cref="Entry.biome"/> are not validated;
    /// last-wins by iteration order (per Phase_V_Design.md §7.3).
    /// </summary>
    [CreateAssetMenu(fileName = "BiomeColorPalette",
                     menuName = "Islands/PCG/Inspection/Biome Color Palette")]
    public sealed class BiomeColorPalette : ScriptableObject
    {
        [System.Serializable]
        public struct Entry
        {
            public BiomeType biome;
            public Color color;
        }

        [Tooltip("BiomeType → Color entries. One entry per BiomeType ordinal you " +
                 "want mapped. Unmapped ordinals fall through to fallbackColor.")]
        [SerializeField] private Entry[] entries = System.Array.Empty<Entry>();

        [Tooltip("Color used for any BiomeType ordinal not present in entries. " +
                 "Default magenta is intentionally loud — signals palette/biome desync.")]
        [SerializeField] private Color fallbackColor = Color.magenta;

        // Direct-lookup table: index = (int)BiomeType, value = color.
        // Built lazily on first Lookup; invalidated on OnValidate.
        // Size = (int)BiomeType.COUNT (currently 13).
        private Color[] _table;
        private bool _hasTable;

        /// <summary>
        /// Return the color for the given biome ordinal, or <see cref="fallbackColor"/>
        /// if the ordinal is out of range or not present in <see cref="entries"/>.
        /// </summary>
        public Color Lookup(int biomeOrdinal)
        {
            EnsureTable();
            if (biomeOrdinal < 0 || biomeOrdinal >= _table.Length)
                return fallbackColor;
            return _table[biomeOrdinal];
        }

        /// <summary>The fallback color, exposed for consumers that want to render
        /// unmapped IDs explicitly (e.g. as a transparent water sentinel).</summary>
        public Color FallbackColor => fallbackColor;

        /// <summary>Number of <see cref="BiomeType"/> ordinals the palette covers.
        /// Equals <c>(int)BiomeType.COUNT</c>. Useful for tests and diagnostics.</summary>
        public int CoveredOrdinalCount => (int)BiomeType.COUNT;

        // =====================================================================
        // Default palette — utilitarian climate colors (Phase V.b D3)
        // =====================================================================
        // Update this table when BiomeType gains new entries.
        // Colors are chosen for visibility and contrast on a 64² debug overlay,
        // not for artistic quality. Replace them in the Inspector at any time.

        private static Color H(string hex)
        {
            ColorUtility.TryParseHtmlString("#" + hex, out Color c);
            return c;
        }

        private static readonly (BiomeType biome, Color color)[] s_defaults = new[]
        {
            (BiomeType.Unclassified,           H("5588AA")),  // muted blue (water sentinel — rendered transparent by overlay, but visible if inspected raw)
            (BiomeType.Snow,                   H("F5F5FF")),  // near-white, cool tint
            (BiomeType.Tundra,                 H("B8C4CC")),  // pale blue-grey
            (BiomeType.BorealForest,           H("2F4F35")),  // dark cool green
            (BiomeType.TemperateDesert,        H("C9B88A")),  // muted tan
            (BiomeType.Shrubland,              H("A8A065")),  // olive-yellow
            (BiomeType.TemperateForest,        H("4A7C44")),  // mid-green
            (BiomeType.TemperateRainforest,    H("2F6842")),  // deep saturated green
            (BiomeType.SubtropicalDesert,      H("E6C989")),  // sandy yellow
            (BiomeType.Grassland,              H("B8C66A")),  // yellow-green
            (BiomeType.TropicalSeasonalForest, H("5C9840")),  // warm green
            (BiomeType.TropicalRainforest,     H("1F7535")),  // deep lush green
            (BiomeType.Beach,                  H("F0E5B0")),  // pale sand
        };

        /// <summary>
        /// Populate entries with one entry per <see cref="BiomeType"/> value using
        /// utilitarian default colors. Overwrites the current entries array.
        /// Available via right-click context menu on the asset Inspector.
        /// </summary>
        [ContextMenu("Populate Defaults")]
        private void PopulateDefaults()
        {
            entries = new Entry[s_defaults.Length];
            for (int i = 0; i < s_defaults.Length; i++)
            {
                entries[i] = new Entry
                {
                    biome = s_defaults[i].biome,
                    color = s_defaults[i].color
                };
            }
            fallbackColor = Color.magenta;

            // Mark dirty so Unity serializes the change to disk.
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif

            _hasTable = false;
            _table = null;

            Debug.Log($"[BiomeColorPalette] Populated {entries.Length} default entries.", this);
        }

        /// <summary>
        /// Called by Unity when the asset is first created via CreateAssetMenu.
        /// Auto-populates defaults so new palettes are immediately usable.
        /// </summary>
        private void Reset()
        {
            PopulateDefaults();
        }

        // =====================================================================
        // Lookup internals
        // =====================================================================

        private void EnsureTable()
        {
            if (_hasTable && _table != null) return;
            BuildTable();
        }

        private void BuildTable()
        {
            int count = (int)BiomeType.COUNT;
            _table = new Color[count];
            for (int i = 0; i < count; i++)
                _table[i] = fallbackColor;

            if (entries != null)
            {
                for (int i = 0; i < entries.Length; i++)
                {
                    int ord = (int)entries[i].biome;
                    if (ord < 0 || ord >= count) continue; // out-of-range entry: skip
                    _table[ord] = entries[i].color;        // last-wins on duplicates
                }
            }

            _hasTable = true;
        }

        private void OnValidate()
        {
            // Force table rebuild on next Lookup; cheap, runs only on Inspector edit.
            _hasTable = false;
            _table = null;
        }
    }
}