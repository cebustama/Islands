// Phase W-aux.a — Read-only map statistics exporter.
//
// Pure function over MapDataExport: no pipeline access, no Unity dependencies
// beyond none at all (System + core types only). Deterministic output: iteration
// is enum-ordinal order over row-major data; floats formatted InvariantCulture.
//
// Region count is derived from the BiomeRegionId field (0 = water sentinel per
// Stage_Regions2D invariant R-2; land ids are 1-based). This keeps the function
// literal to its contract ("read-only over MapDataExport") instead of coupling
// to Stage_Regions2D.LastBuiltRegistry. Distinct-positive count is used rather
// than max(id) so the exporter does not depend on id contiguity, which is a
// stage implementation detail, not a contract.

using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Islands.PCG.Layout.Maps;

namespace Islands.PCG.Inspection
{
    /// <summary>
    /// Produces a JSON statistics snapshot of a generated map: per-field
    /// min/max/mean, per-layer cell counts, full biome histogram (zeros
    /// explicit), region count, and vegetation broken down by biome.
    /// Console/diagnostic surface only — not a golden, not authority.
    /// </summary>
    public static class MapStatsExporter2D
    {
        /// <summary>Compat overload: histogram/band sections report absent.</summary>
        public static string ToJson(MapDataExport export) => ToJson(export, -1f);

        /// <param name="waterThreshold01">
        /// Effective land threshold of the run that produced the export. Not part
        /// of MapDataExport (the export contract carries data, not tunables), so
        /// the caller supplies it. Negative = unknown → dependent sections absent.
        /// </param>
        public static string ToJson(MapDataExport export, float waterThreshold01)
        {
            CultureInfo ci = CultureInfo.InvariantCulture;
            var sb = new StringBuilder(4096);
            int cells = export.Length;
            float pctDen = cells > 0 ? 100f / cells : 0f;

            sb.Append("{\n");
            sb.AppendFormat(ci,
                "  \"meta\": {{ \"width\": {0}, \"height\": {1}, \"cells\": {2}, \"seed\": {3} }},\n",
                export.Width, export.Height, cells, export.Seed);

            // ---- Fields: min / max / mean, absent ones reported explicitly ----
            sb.Append("  \"fields\": {\n");
            for (int f = 0; f < (int)MapFieldId.COUNT; f++)
            {
                var id = (MapFieldId)f;
                sb.AppendFormat(ci, "    \"{0}\": ", id);
                if (!export.HasField(id))
                {
                    sb.Append("{ \"present\": false }");
                }
                else
                {
                    float[] v = export.GetField(id);
                    float min = float.MaxValue, max = float.MinValue;
                    double sum = 0;
                    for (int i = 0; i < v.Length; i++)
                    {
                        float x = v[i];
                        if (x < min) min = x;
                        if (x > max) max = x;
                        sum += x;
                    }
                    float mean = v.Length > 0 ? (float)(sum / v.Length) : 0f;
                    sb.AppendFormat(ci,
                        "{{ \"present\": true, \"min\": {0:F6}, \"max\": {1:F6}, \"mean\": {2:F6} }}",
                        min, max, mean);
                }
                sb.Append(f < (int)MapFieldId.COUNT - 1 ? ",\n" : "\n");
            }
            sb.Append("  },\n");

            // ---- Layers: count + pct over all cells, all 15 listed ----
            sb.Append("  \"layers\": {\n");
            for (int l = 0; l < (int)MapLayerId.COUNT; l++)
            {
                var id = (MapLayerId)l;
                sb.AppendFormat(ci, "    \"{0}\": ", id);
                if (!export.HasLayer(id))
                {
                    sb.Append("{ \"present\": false }");
                }
                else
                {
                    bool[] m = export.GetLayer(id);
                    int c = 0;
                    for (int i = 0; i < m.Length; i++) if (m[i]) c++;
                    sb.AppendFormat(ci,
                        "{{ \"present\": true, \"count\": {0}, \"pct\": {1:F2} }}",
                        c, c * pctDen);
                }
                sb.Append(l < (int)MapLayerId.COUNT - 1 ? ",\n" : "\n");
            }
            sb.Append("  },\n");

            // ---- Biomes: full histogram, all 13 including explicit 0.00% ----
            bool hasBiome = export.HasField(MapFieldId.Biome);
            float[] biomeField = hasBiome ? export.GetField(MapFieldId.Biome) : null;
            int[] biomeCount = new int[(int)BiomeType.COUNT];
            if (hasBiome)
            {
                for (int i = 0; i < biomeField.Length; i++)
                {
                    int b = (int)biomeField[i];
                    if (b >= 0 && b < biomeCount.Length) biomeCount[b]++;
                }
            }
            int biomesNonZero = 0;
            sb.Append("  \"biomes\": {\n");
            sb.AppendFormat(ci, "    \"present\": {0},\n", hasBiome ? "true" : "false");
            for (int b = 0; b < (int)BiomeType.COUNT; b++)
            {
                if (biomeCount[b] > 0) biomesNonZero++;
                sb.AppendFormat(ci,
                    "    \"{0}\": {{ \"count\": {1}, \"pct\": {2:F2} }}",
                    (BiomeType)b, biomeCount[b], biomeCount[b] * pctDen);
                sb.Append(b < (int)BiomeType.COUNT - 1 ? ",\n" : "\n");
            }
            sb.Append("  },\n");
            sb.AppendFormat(ci, "  \"biomesNonZero\": {0},\n", biomesNonZero);

            // ---- Regions: distinct positive BiomeRegionId values ----
            sb.Append("  \"regions\": ");
            if (!export.HasField(MapFieldId.BiomeRegionId))
            {
                sb.Append("{ \"present\": false },\n");
            }
            else
            {
                float[] r = export.GetField(MapFieldId.BiomeRegionId);
                var ids = new HashSet<int>();
                for (int i = 0; i < r.Length; i++)
                {
                    int v = (int)r[i];
                    if (v > 0) ids.Add(v);   // 0 = water sentinel (R-2)
                }
                sb.AppendFormat(ci, "{{ \"present\": true, \"count\": {0} }},\n", ids.Count);
            }

            // ================================================================
            // W-aux.c block 1 — calibration instrumentation. Diagnostics only,
            // O(cells), button path. Not a golden, not authority.
            // ================================================================

            bool hasHeight = export.HasField(MapFieldId.Height);
            bool hasLand = export.HasLayer(MapLayerId.Land);
            bool[] land = hasLand ? export.GetLayer(MapLayerId.Land) : null;

            // ---- 1. Land-height histogram: 10 buckets over [wt, 1], plateau counter ----
            bool histOk = hasHeight && hasLand
                       && waterThreshold01 >= 0f && waterThreshold01 < 1f;
            sb.Append("  \"landHeightHistogram\": {\n");
            sb.AppendFormat(ci, "    \"present\": {0}", histOk ? "true" : "false");
            if (histOk)
            {
                float[] hgt = export.GetField(MapFieldId.Height);
                float span = 1f - waterThreshold01;
                int landCells = 0, exactlyOne = 0, underThreshold = 0;
                int[] bucket = new int[10];
                for (int i = 0; i < hgt.Length; i++)
                {
                    if (!land[i]) continue;
                    landCells++;
                    float h = hgt[i];
                    if (h == 1f) exactlyOne++;               // exact plateau, by design
                    if (h < waterThreshold01) { underThreshold++; continue; }
                    int bi = (int)((h - waterThreshold01) / span * 10f);
                    if (bi > 9) bi = 9;                       // h == 1.0 lands in bucket 9 too
                    bucket[bi]++;
                }
                float landDen = landCells > 0 ? 100f / landCells : 0f;
                sb.Append(",\n");
                sb.AppendFormat(ci, "    \"waterThreshold01\": {0:F6},\n", waterThreshold01);
                sb.AppendFormat(ci, "    \"landCells\": {0},\n", landCells);
                sb.AppendFormat(ci, "    \"underThreshold\": {0},\n", underThreshold);
                sb.Append("    \"buckets\": [\n");
                for (int b = 0; b < 10; b++)
                {
                    float lo = waterThreshold01 + span * b / 10f;
                    float hi = waterThreshold01 + span * (b + 1) / 10f;
                    sb.AppendFormat(ci,
                        "      {{ \"from\": {0:F4}, \"to\": {1:F4}, \"count\": {2}, \"pctOfLand\": {3:F2} }}{4}\n",
                        lo, hi, bucket[b], bucket[b] * landDen, b < 9 ? "," : "");
                }
                sb.Append("    ],\n");
                sb.AppendFormat(ci,
                    "    \"exactlyOne\": {{ \"count\": {0}, \"pctOfLand\": {1:F2} }}",
                    exactlyOne, exactlyOne * landDen);
            }
            sb.Append("\n  },\n");

            // ---- 2. Biome × elevation band cross table (bands are disjoint) ----
            bool bandOk = hasBiome && hasLand
                       && export.HasLayer(MapLayerId.HillsL1)
                       && export.HasLayer(MapLayerId.HillsL2);
            sb.Append("  \"biomeByBand\": {\n");
            sb.AppendFormat(ci, "    \"present\": {0}", bandOk ? "true" : "false");
            if (bandOk)
            {
                bool[] l1 = export.GetLayer(MapLayerId.HillsL1);
                bool[] l2 = export.GetLayer(MapLayerId.HillsL2);
                int[,] cross = new int[(int)BiomeType.COUNT, 3];
                int[] bandTotal = new int[3];
                for (int i = 0; i < land.Length; i++)
                {
                    if (!land[i]) continue;
                    int band = l2[i] ? 2 : (l1[i] ? 1 : 0);
                    bandTotal[band]++;
                    int b = hasBiome ? (int)biomeField[i] : 0;
                    if (b >= 0 && b < (int)BiomeType.COUNT) cross[b, band]++;
                }
                for (int b = 0; b < (int)BiomeType.COUNT; b++)
                {
                    sb.Append(",\n");
                    sb.AppendFormat(ci,
                        "    \"{0}\": {{ \"plain\": {1}, \"hillsL1\": {2}, \"hillsL2\": {3} }}",
                        (BiomeType)b, cross[b, 0], cross[b, 1], cross[b, 2]);
                }
                sb.Append(",\n");
                sb.AppendFormat(ci,
                    "    \"bandTotals\": {{ \"plain\": {0}, \"hillsL1\": {1}, \"hillsL2\": {2} }}",
                    bandTotal[0], bandTotal[1], bandTotal[2]);
            }
            sb.Append("\n  },\n");

            // ---- 3. Vegetation eligibility funnel. Mirrors stage policy as of
            //         W-aux.c block 3: LandInterior → valid biome with density > 0
            //         → minus HillsL2 cells of biomes without vegetatesOnPeaks.
            //         peaksUnlockedByBiomePolicy = L2 cells that were excluded
            //         before block 3 and are eligible now (direct effect metric). ----
            bool eligOk = hasBiome
                       && export.HasLayer(MapLayerId.LandInterior)
                       && export.HasLayer(MapLayerId.HillsL2);
            sb.Append("  \"vegetationEligibility\": {\n");
            sb.AppendFormat(ci, "    \"present\": {0}", eligOk ? "true" : "false");
            if (eligOk)
            {
                bool[] li = export.GetLayer(MapLayerId.LandInterior);
                bool[] l2 = export.GetLayer(MapLayerId.HillsL2);
                bool[] veg = export.HasLayer(MapLayerId.Vegetation)
                    ? export.GetLayer(MapLayerId.Vegetation) : null;
                int interior = 0, biomeEligible = 0, peaksUnlocked = 0,
                    peaksBlocked = 0, eligible = 0, vegetated = 0;
                for (int i = 0; i < li.Length; i++)
                {
                    if (!li[i]) continue;
                    interior++;
                    int b = (int)biomeField[i];
                    if (b <= 0 || b >= (int)BiomeType.COUNT) continue;
                    BiomeDef def = BiomeTable.Definitions[b];
                    if (def.vegetationDensity <= 0f) continue;
                    biomeEligible++;
                    if (l2[i])
                    {
                        if (!def.vegetatesOnPeaks) { peaksBlocked++; continue; }
                        peaksUnlocked++;
                    }
                    eligible++;
                    if (veg != null && veg[i]) vegetated++;
                }
                sb.Append(",\n");
                sb.AppendFormat(ci, "    \"landInterior\": {0},\n", interior);
                sb.AppendFormat(ci, "    \"biomeEligible\": {0},\n", biomeEligible);
                sb.AppendFormat(ci, "    \"peaksUnlockedByBiomePolicy\": {0},\n", peaksUnlocked);
                sb.AppendFormat(ci, "    \"peaksBlockedByBiomePolicy\": {0},\n", peaksBlocked);
                sb.AppendFormat(ci, "    \"eligible\": {0},\n", eligible);
                sb.AppendFormat(ci, "    \"vegetated\": {0},\n", vegetated);
                sb.AppendFormat(ci, "    \"pctOfEligible\": {0:F2}",
                    eligible > 0 ? 100f * vegetated / eligible : 0f);
            }
            sb.Append("\n  },\n");

            // ---- Vegetation by biome: count + pct of that biome's cells ----
            bool hasVeg = export.HasLayer(MapLayerId.Vegetation);
            bool vegOk = hasVeg && hasBiome;
            sb.Append("  \"vegetationByBiome\": {\n");
            sb.AppendFormat(ci, "    \"present\": {0}", vegOk ? "true" : "false");
            if (vegOk)
            {
                bool[] veg = export.GetLayer(MapLayerId.Vegetation);
                int[] vegCount = new int[(int)BiomeType.COUNT];
                for (int i = 0; i < veg.Length; i++)
                {
                    if (!veg[i]) continue;
                    int b = (int)biomeField[i];
                    if (b >= 0 && b < vegCount.Length) vegCount[b]++;
                }
                for (int b = 0; b < (int)BiomeType.COUNT; b++)
                {
                    float pctOfBiome = biomeCount[b] > 0
                        ? 100f * vegCount[b] / biomeCount[b] : 0f;
                    sb.Append(",\n");
                    sb.AppendFormat(ci,
                        "    \"{0}\": {{ \"count\": {1}, \"pctOfBiome\": {2:F2} }}",
                        (BiomeType)b, vegCount[b], pctOfBiome);
                }
            }
            sb.Append("\n  }\n}");
            return sb.ToString();
        }
    }
}