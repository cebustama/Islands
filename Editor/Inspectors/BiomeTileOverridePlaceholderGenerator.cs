// Phase Q — Q-aux.a: editor-only placeholder tile generation for BiomeTileOverride.
//
// Purpose: let a biome-conditional tile setup be smoke-tested before any real art
// exists. Generates flat-colour Tile assets, one per (biome, layer) slot, coloured
// to match the BiomeColorPalette defaults used by PCGRuntimeOverlay (V.b) so that
// "painted tile colour == overlay colour" is a direct visual cross-check.
//
// Scope discipline:
//   - EDITOR ONLY. Zero runtime code, zero changes to BiomeTileOverride,
//     TilemapAdapter2D or PCGMapTilemapVisualization.
//   - Generates real Tile assets and assigns them into existing slots. The runtime
//     fallthrough contract (Q-DD-6: override -> base -> fallbackTile, no sentinel)
//     is untouched — from the adapter's point of view these are ordinary tiles.
//   - Never overwrites a slot that already has art, unless the "(Overwrite All)"
//     variant is used.
//
// Output: <folder of the override asset>/Placeholders/PH_<Biome>_<Layer>.asset
// These are disposable debug assets. Regenerate or delete freely.

using System.Collections.Generic;
using System.IO;

using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

using Islands.PCG.Layout.Maps;
using Islands.PCG.Adapters.Tilemap;

namespace Islands.PCG.EditorTools
{
    /// <summary>
    /// Context-menu utilities that fill a <see cref="BiomeTileOverride"/> with
    /// generated flat-colour placeholder tiles.
    /// </summary>
    internal static class BiomeTileOverridePlaceholderGenerator
    {
        // ------------------------------------------------------------------
        // Tunables
        // ------------------------------------------------------------------

        /// <summary>
        /// Placeholder texture size in pixels. Sprite PPU is set to the same value,
        /// so one tile always occupies exactly one Tilemap cell regardless of this
        /// number — it only controls how crisp the layer border reads when zoomed in.
        /// </summary>
        private const int TileSizePx = 32;

        private const string PlaceholderFolderName = "Placeholders";
        private const string AssetPrefix = "PH_";

        // ------------------------------------------------------------------
        // Biome colours — mirrors BiomeColorPalette's built-in defaults (V.b).
        //
        // Deliberately duplicated rather than referenced: BiomeColorPalette lives in
        // the Islands.PCG.Inspection assembly, and adding that reference to the editor
        // assembly for a debug utility is not worth the coupling. If the V.b defaults
        // change, this table must be updated by hand — accepted, and noted here.
        // ------------------------------------------------------------------

        private static readonly Dictionary<BiomeType, string> s_biomeHex =
            new Dictionary<BiomeType, string>
        {
            { BiomeType.Unclassified,           "5588AA" },
            { BiomeType.Snow,                   "F5F5FF" },
            { BiomeType.Tundra,                 "B8C4CC" },
            { BiomeType.BorealForest,           "2F4F35" },
            { BiomeType.TemperateDesert,        "C9B88A" },
            { BiomeType.Shrubland,              "A8A065" },
            { BiomeType.TemperateForest,        "4A7C44" },
            { BiomeType.TemperateRainforest,    "2F6842" },
            { BiomeType.SubtropicalDesert,      "E6C989" },
            { BiomeType.Grassland,              "B8C66A" },
            { BiomeType.TropicalSeasonalForest, "5C9840" },
            { BiomeType.TropicalRainforest,     "1F7535" },
            { BiomeType.Beach,                  "F0E5B0" },
        };

        // ------------------------------------------------------------------
        // Layer styling — brightness + border, so two layers inside the same
        // biome remain distinguishable at a glance.
        // ------------------------------------------------------------------

        private struct LayerStyle
        {
            public float Brightness;   // multiplier applied to the biome colour
            public Color Border;       // border colour
            public int BorderPx;       // border thickness in pixels (0 = none)
        }

        private static LayerStyle StyleFor(MapLayerId id)
        {
            switch (id)
            {
                case MapLayerId.Land:
                    return new LayerStyle { Brightness = 1.00f, Border = Color.clear, BorderPx = 0 };
                case MapLayerId.LandInterior:
                    return new LayerStyle { Brightness = 0.92f, Border = Color.clear, BorderPx = 0 };
                case MapLayerId.LandCore:
                    return new LayerStyle { Brightness = 0.84f, Border = Color.clear, BorderPx = 0 };
                case MapLayerId.LandEdge:
                    return new LayerStyle { Brightness = 1.10f, Border = new Color(0f, 0f, 0f, 0.5f), BorderPx = 2 };
                case MapLayerId.Vegetation:
                    return new LayerStyle { Brightness = 0.60f, Border = new Color(0f, 0f, 0f, 0.85f), BorderPx = 3 };
                case MapLayerId.HillsL1:
                    return new LayerStyle { Brightness = 1.15f, Border = new Color(0f, 0f, 0f, 0.6f), BorderPx = 2 };
                case MapLayerId.HillsL2:
                    return new LayerStyle { Brightness = 1.35f, Border = Color.white, BorderPx = 4 };
                default:
                    return new LayerStyle { Brightness = 1.00f, Border = Color.magenta, BorderPx = 2 };
            }
        }

        // ------------------------------------------------------------------
        // Menu entries
        // ------------------------------------------------------------------

        [MenuItem("CONTEXT/BiomeTileOverride/Generate Placeholder Tiles (empty slots)")]
        private static void GenerateEmpty(MenuCommand command)
        {
            Generate(command.context as BiomeTileOverride, overwrite: false);
        }

        [MenuItem("CONTEXT/BiomeTileOverride/Generate Placeholder Tiles (overwrite all)")]
        private static void GenerateAll(MenuCommand command)
        {
            var target = command.context as BiomeTileOverride;
            if (target == null) return;

            bool ok = EditorUtility.DisplayDialog(
                "Overwrite all tile slots?",
                $"This replaces EVERY tile reference in '{target.name}' with a generated " +
                "placeholder, including any real art already assigned.\n\nContinue?",
                "Overwrite", "Cancel");

            if (ok) Generate(target, overwrite: true);
        }

        [MenuItem("CONTEXT/BiomeTileOverride/Clear Placeholder Tiles")]
        private static void ClearPlaceholders(MenuCommand command)
        {
            var target = command.context as BiomeTileOverride;
            if (target == null || target.groups == null) return;

            string folder = PlaceholderFolder(target, createIfMissing: false);
            int cleared = 0;

            var groups = target.groups;
            for (int g = 0; g < groups.Length; g++)
            {
                var slots = groups[g].layers;
                if (slots == null) continue;

                for (int s = 0; s < slots.Length; s++)
                {
                    if (IsPlaceholder(slots[s].tile, folder)) { slots[s].tile = null; cleared++; }
                    if (IsPlaceholder(slots[s].animatedTile, folder)) slots[s].animatedTile = null;
                    if (IsPlaceholder(slots[s].ruleTile, folder)) slots[s].ruleTile = null;
                }
                groups[g].layers = slots;
            }
            target.groups = groups;

            target.RebuildLookup();
            EditorUtility.SetDirty(target);
            AssetDatabase.SaveAssets();

            Debug.Log(
                $"[BiomeTileOverride] '{target.name}': cleared {cleared} placeholder reference(s). " +
                (string.IsNullOrEmpty(folder)
                    ? ""
                    : $"The generated assets remain in '{folder}' — delete that folder to remove them."),
                target);
        }

        // ------------------------------------------------------------------
        // Core
        // ------------------------------------------------------------------

        private static void Generate(BiomeTileOverride target, bool overwrite)
        {
            if (target == null) return;

            if (target.groups == null || target.groups.Length == 0)
            {
                Debug.LogWarning(
                    $"[BiomeTileOverride] '{target.name}': no biome groups. " +
                    "Run 'Populate Default Biome Groups' first.", target);
                return;
            }

            string folder = PlaceholderFolder(target, createIfMissing: true);
            if (string.IsNullOrEmpty(folder))
            {
                Debug.LogError(
                    $"[BiomeTileOverride] '{target.name}': the asset must live inside the " +
                    "project (Assets/ or a local Packages/ folder) to generate placeholders.",
                    target);
                return;
            }

            int created = 0, assigned = 0, skipped = 0;

            var groups = target.groups;
            for (int g = 0; g < groups.Length; g++)
            {
                BiomeType biome = groups[g].biome;
                var slots = groups[g].layers;
                if (slots == null) continue;

                // Unclassified is water by contract — an override there can never match
                // a land layer and would only add confusion. Skipped by design.
                if (biome == BiomeType.Unclassified)
                {
                    skipped += slots.Length;
                    continue;
                }

                for (int s = 0; s < slots.Length; s++)
                {
                    bool occupied =
                        slots[s].tile != null ||
                        slots[s].animatedTile != null ||
                        slots[s].ruleTile != null;

                    if (occupied && !overwrite) { skipped++; continue; }

                    MapLayerId layerId = slots[s].layerId;
                    string path = Path.Combine(folder, $"{AssetPrefix}{biome}_{layerId}.asset")
                                      .Replace('\\', '/');

                    var tile = AssetDatabase.LoadAssetAtPath<Tile>(path);
                    if (tile == null)
                    {
                        tile = CreatePlaceholderTile(biome, layerId, path);
                        created++;
                    }

                    slots[s].tile = tile;
                    if (overwrite)
                    {
                        // Keep resolution unambiguous: H6 priority is ruleTile > animatedTile > tile,
                        // so leftover higher-priority references would mask the placeholder.
                        slots[s].animatedTile = null;
                        slots[s].ruleTile = null;
                    }
                    assigned++;
                }

                groups[g].layers = slots;
            }
            target.groups = groups;

            target.RebuildLookup();
            EditorUtility.SetDirty(target);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log(
                $"[BiomeTileOverride] '{target.name}': placeholders ready — " +
                $"{created} asset(s) created, {assigned} slot(s) assigned, {skipped} skipped. " +
                $"Folder: {folder}", target);
        }

        /// <summary>
        /// Builds one flat-colour <see cref="Tile"/> asset with its texture and sprite
        /// stored as sub-assets, so the whole placeholder is a single file.
        /// </summary>
        private static Tile CreatePlaceholderTile(BiomeType biome, MapLayerId layerId, string path)
        {
            LayerStyle style = StyleFor(layerId);
            Color fill = Modulate(BiomeColor(biome), style.Brightness);

            var tex = new Texture2D(TileSizePx, TileSizePx, TextureFormat.RGBA32, false)
            {
                name = $"{AssetPrefix}{biome}_{layerId}_Tex",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };

            var pixels = new Color32[TileSizePx * TileSizePx];
            Color32 fill32 = fill;
            Color32 border32 = style.Border;

            for (int y = 0; y < TileSizePx; y++)
            {
                for (int x = 0; x < TileSizePx; x++)
                {
                    bool onBorder =
                        style.BorderPx > 0 &&
                        (x < style.BorderPx || y < style.BorderPx ||
                         x >= TileSizePx - style.BorderPx || y >= TileSizePx - style.BorderPx);

                    pixels[y * TileSizePx + x] =
                        onBorder && style.Border.a > 0f
                            ? Blend(fill32, border32)
                            : fill32;
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply(false, false);

            var sprite = Sprite.Create(
                tex,
                new Rect(0f, 0f, TileSizePx, TileSizePx),
                new Vector2(0.5f, 0.5f),
                TileSizePx);                       // PPU = size => 1 tile == 1 cell
            sprite.name = $"{AssetPrefix}{biome}_{layerId}_Sprite";

            var tile = ScriptableObject.CreateInstance<Tile>();
            tile.name = $"{AssetPrefix}{biome}_{layerId}";
            tile.sprite = sprite;
            tile.colliderType = UnityEngine.Tilemaps.Tile.ColliderType.None;

            AssetDatabase.CreateAsset(tile, path);
            AssetDatabase.AddObjectToAsset(tex, tile);
            AssetDatabase.AddObjectToAsset(sprite, tile);
            AssetDatabase.ImportAsset(path);

            return tile;
        }

        // ------------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------------

        private static Color BiomeColor(BiomeType biome)
        {
            if (s_biomeHex.TryGetValue(biome, out string hex) &&
                ColorUtility.TryParseHtmlString("#" + hex, out Color c))
                return c;

            return Color.magenta;   // unmapped biome — deliberately loud
        }

        private static Color Modulate(Color c, float brightness)
        {
            return new Color(
                Mathf.Clamp01(c.r * brightness),
                Mathf.Clamp01(c.g * brightness),
                Mathf.Clamp01(c.b * brightness),
                1f);
        }

        private static Color32 Blend(Color32 baseCol, Color32 over)
        {
            float a = over.a / 255f;
            return new Color32(
                (byte)Mathf.RoundToInt(baseCol.r * (1f - a) + over.r * a),
                (byte)Mathf.RoundToInt(baseCol.g * (1f - a) + over.g * a),
                (byte)Mathf.RoundToInt(baseCol.b * (1f - a) + over.b * a),
                255);
        }

        /// <summary>
        /// "<folder of the override asset>/Placeholders". Returns empty when the
        /// override is not a project asset.
        /// </summary>
        private static string PlaceholderFolder(BiomeTileOverride target, bool createIfMissing)
        {
            string assetPath = AssetDatabase.GetAssetPath(target);
            if (string.IsNullOrEmpty(assetPath)) return string.Empty;

            string parent = Path.GetDirectoryName(assetPath).Replace('\\', '/');
            string folder = $"{parent}/{PlaceholderFolderName}";

            if (createIfMissing && !AssetDatabase.IsValidFolder(folder))
                AssetDatabase.CreateFolder(parent, PlaceholderFolderName);

            return folder;
        }

        private static bool IsPlaceholder(TileBase tile, string folder)
        {
            if (tile == null) return false;
            if (!tile.name.StartsWith(AssetPrefix)) return false;
            if (string.IsNullOrEmpty(folder)) return true;

            string p = AssetDatabase.GetAssetPath(tile);
            return !string.IsNullOrEmpty(p) && p.Replace('\\', '/').StartsWith(folder);
        }
    }
}