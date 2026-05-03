// Phase V.b — BiomeColorPalette unit tests.
// Spec: planning/active/Phase_V_Design.md §10.1.
//
// Read-only inspection asset tests. No goldens, no determinism gates,
// no MapPipelineRunner2D dependencies.

using Islands.PCG.Inspection;
using Islands.PCG.Layout.Maps;
using NUnit.Framework;
using UnityEngine;

namespace Islands.PCG.Tests.EditMode.Inspection
{
    [TestFixture]
    public sealed class BiomeColorPaletteTests
    {
        // -----------------------------------------------------------------
        // Test fixture helpers
        // -----------------------------------------------------------------

        private static BiomeColorPalette MakePalette(
            (BiomeType biome, Color color)[] mappings,
            Color fallback)
        {
            var palette = ScriptableObject.CreateInstance<BiomeColorPalette>();

            // Inject entries + fallback via reflection — keeps the production API
            // (Inspector-authored) clean while letting tests build deterministic
            // fixtures without touching .asset files.
            var entriesField = typeof(BiomeColorPalette).GetField(
                "entries",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var fallbackField = typeof(BiomeColorPalette).GetField(
                "fallbackColor",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            Assert.IsNotNull(entriesField, "Test fixture: 'entries' field not found on BiomeColorPalette.");
            Assert.IsNotNull(fallbackField, "Test fixture: 'fallbackColor' field not found on BiomeColorPalette.");

            var entries = new BiomeColorPalette.Entry[mappings.Length];
            for (int i = 0; i < mappings.Length; i++)
            {
                entries[i] = new BiomeColorPalette.Entry
                {
                    biome = mappings[i].biome,
                    color = mappings[i].color
                };
            }

            entriesField.SetValue(palette, entries);
            fallbackField.SetValue(palette, fallback);

            return palette;
        }

        // -----------------------------------------------------------------
        // §10.1 — BiomeColorPaletteLookup_ReturnsFallbackForUnknownOrdinal
        // -----------------------------------------------------------------

        [Test]
        public void Lookup_ReturnsFallback_ForOrdinalAboveCount()
        {
            var fallback = new Color(0.7f, 0.1f, 0.5f, 1f);
            var palette = MakePalette(
                new (BiomeType, Color)[]
                {
                    (BiomeType.Snow,            Color.white),
                    (BiomeType.TemperateForest, Color.green),
                },
                fallback);

            try
            {
                int outOfRange = (int)BiomeType.COUNT + 99;
                Color result = palette.Lookup(outOfRange);
                Assert.AreEqual(fallback, result,
                    "Out-of-range ordinal must return fallbackColor.");
            }
            finally
            {
                Object.DestroyImmediate(palette);
            }
        }

        [Test]
        public void Lookup_ReturnsFallback_ForUnmappedInRangeOrdinal()
        {
            var fallback = new Color(0.7f, 0.1f, 0.5f, 1f);
            var palette = MakePalette(
                new (BiomeType, Color)[]
                {
                    (BiomeType.Snow, Color.white),
                    // Tundra deliberately unmapped to verify the in-range fallback path.
                },
                fallback);

            try
            {
                Color result = palette.Lookup((int)BiomeType.Tundra);
                Assert.AreEqual(fallback, result,
                    "In-range but unmapped ordinal must return fallbackColor.");
            }
            finally
            {
                Object.DestroyImmediate(palette);
            }
        }

        [Test]
        public void Lookup_ReturnsFallback_ForNegativeOrdinal()
        {
            var fallback = Color.magenta;
            var palette = MakePalette(
                new (BiomeType, Color)[] { (BiomeType.Grassland, Color.yellow) },
                fallback);

            try
            {
                Color result = palette.Lookup(-1);
                Assert.AreEqual(fallback, result,
                    "Negative ordinal must return fallbackColor.");
            }
            finally
            {
                Object.DestroyImmediate(palette);
            }
        }

        // -----------------------------------------------------------------
        // §10.1 — BiomeColorPaletteLookup_ReturnsEntryForKnownOrdinal
        // -----------------------------------------------------------------

        [Test]
        public void Lookup_ReturnsEntryColor_ForEachDeclaredEntry()
        {
            var snowColor = new Color(0.95f, 0.95f, 1.00f, 1f);
            var forestColor = new Color(0.20f, 0.55f, 0.25f, 1f);
            var desertColor = new Color(0.85f, 0.75f, 0.45f, 1f);

            var palette = MakePalette(
                new (BiomeType, Color)[]
                {
                    (BiomeType.Snow,             snowColor),
                    (BiomeType.TemperateForest,  forestColor),
                    (BiomeType.SubtropicalDesert, desertColor),
                },
                Color.magenta);

            try
            {
                Assert.AreEqual(snowColor, palette.Lookup((int)BiomeType.Snow));
                Assert.AreEqual(forestColor, palette.Lookup((int)BiomeType.TemperateForest));
                Assert.AreEqual(desertColor, palette.Lookup((int)BiomeType.SubtropicalDesert));
            }
            finally
            {
                Object.DestroyImmediate(palette);
            }
        }

        [Test]
        public void Lookup_LastWins_OnDuplicateBiomeEntries()
        {
            // Per Phase_V_Design.md §7.3: duplicates are not validated; last-wins
            // by iteration order. This documents the contract via test.
            var palette = MakePalette(
                new (BiomeType, Color)[]
                {
                    (BiomeType.Snow, Color.red),
                    (BiomeType.Snow, Color.blue),
                },
                Color.magenta);

            try
            {
                Assert.AreEqual(Color.blue, palette.Lookup((int)BiomeType.Snow));
            }
            finally
            {
                Object.DestroyImmediate(palette);
            }
        }
    }
}