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
        public static string ToJson(MapDataExport export)
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