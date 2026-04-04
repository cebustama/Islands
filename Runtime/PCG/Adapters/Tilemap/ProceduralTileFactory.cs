using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Islands.PCG.Adapters.Tilemap
{
    /// <summary>
    /// Generates and caches runtime <see cref="Tile"/> assets from solid <see cref="Color"/> values,
    /// removing the requirement for pre-authored tile art during prototyping and design iteration.
    ///
    /// Each unique <see cref="Color32"/> value produces exactly one cached <see cref="Tile"/>
    /// instance. All tiles share a single white 1×1 backing sprite; <see cref="Tile.color"/>
    /// carries the tint so the rendered cell matches the requested color exactly.
    ///
    /// Usage:
    /// <code>
    ///   // Single tile
    ///   Tile t = ProceduralTileFactory.GetOrCreate(Color.blue);
    ///
    ///   // Full priority table from a ProceduralTileEntry[]
    ///   TilemapLayerEntry[] table = ProceduralTileFactory.BuildPriorityTable(colorEntries);
    ///   TilemapAdapter2D.Apply(export, tilemap, table, fallbackTile);
    /// </code>
    ///
    /// Cache lifecycle:
    ///   Tiles are in-memory <see cref="ScriptableObject"/> instances not tracked by the
    ///   Unity asset database. The static cache is wiped automatically on domain reload.
    ///   Call <see cref="ClearCache"/> to force immediate release (e.g. on scene teardown).
    ///
    /// Thread safety: not thread-safe. Must be called from the main thread (Unity restriction
    /// on <see cref="ScriptableObject"/> and <see cref="Texture2D"/> creation).
    ///
    /// No pipeline dependency. Sample/adapter layer only. No new MapLayerId or MapFieldId.
    /// Phase H2d.
    /// </summary>
    public static class ProceduralTileFactory
    {
        // Shared white 1×1 sprite used as the base for all generated tiles.
        // Tile.color is a multiplicative tint; white base → rendered color == requested color.
        private static Sprite s_WhiteSprite;

        // Cache keyed by Color32 to avoid float-equality edge cases.
        private static readonly Dictionary<Color32, Tile> s_Cache = new Dictionary<Color32, Tile>();

        // =====================================================================
        // Public API
        // =====================================================================

        /// <summary>
        /// Returns a cached <see cref="Tile"/> for <paramref name="color"/>,
        /// creating a new one if this color has not been seen before.
        /// </summary>
        /// <param name="color">The desired solid fill color.</param>
        /// <returns>A <see cref="Tile"/> whose <c>color</c> matches <paramref name="color"/>.</returns>
        public static Tile GetOrCreate(Color color)
        {
            Color32 key = color;

            if (s_Cache.TryGetValue(key, out Tile existing) && existing != null)
                return existing;

            EnsureWhiteSprite();

            Tile tile = ScriptableObject.CreateInstance<Tile>();
            tile.sprite = s_WhiteSprite;
            tile.color = color;

            s_Cache[key] = tile;
            return tile;
        }

        /// <summary>
        /// Converts a <see cref="ProceduralTileEntry"/> array into a
        /// <see cref="TilemapLayerEntry"/> array ready for <see cref="TilemapAdapter2D.Apply"/>.
        ///
        /// Each entry's <see cref="ProceduralTileEntry.Color"/> is resolved through
        /// <see cref="GetOrCreate"/>. The positional (low→high) priority order is preserved.
        ///
        /// Null or empty input returns an empty array — never null.
        /// </summary>
        public static TilemapLayerEntry[] BuildPriorityTable(ProceduralTileEntry[] entries)
        {
            if (entries == null || entries.Length == 0)
                return System.Array.Empty<TilemapLayerEntry>();

            var table = new TilemapLayerEntry[entries.Length];
            for (int i = 0; i < entries.Length; i++)
                table[i] = new TilemapLayerEntry
                {
                    LayerId = entries[i].LayerId,
                    Tile = GetOrCreate(entries[i].Color)
                };
            return table;
        }

        /// <summary>
        /// Releases all cached tiles and the shared white sprite.
        /// Subsequent calls to <see cref="GetOrCreate"/> will produce fresh instances.
        ///
        /// Safe to call multiple times. Does nothing if the cache is already empty.
        /// </summary>
        public static void ClearCache()
        {
            foreach (var tile in s_Cache.Values)
            {
                if (tile != null)
                    Object.DestroyImmediate(tile);
            }
            s_Cache.Clear();

            if (s_WhiteSprite != null)
            {
                // Destroy the backing texture before the sprite to avoid leaking the Texture2D.
                if (s_WhiteSprite.texture != null)
                    Object.DestroyImmediate(s_WhiteSprite.texture);
                Object.DestroyImmediate(s_WhiteSprite);
                s_WhiteSprite = null;
            }
        }

        // =====================================================================
        // Private helpers
        // =====================================================================

        private static void EnsureWhiteSprite()
        {
            if (s_WhiteSprite != null) return;

            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, mipChain: false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = "ProceduralTileFactory_WhitePixel"
            };
            tex.SetPixel(0, 0, Color.white);
            tex.Apply(updateMipmaps: false, makeNoLongerReadable: false);

            s_WhiteSprite = Sprite.Create(
                tex,
                new Rect(0f, 0f, 1f, 1f),
                pivot: new Vector2(0.5f, 0.5f),
                pixelsPerUnit: 1f);
            s_WhiteSprite.name = "ProceduralTileFactory_WhiteSprite";
        }
    }
}