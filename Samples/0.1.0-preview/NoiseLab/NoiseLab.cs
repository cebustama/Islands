using System.Globalization;
using Unity.Mathematics;
using UnityEngine;

using Islands;
using static Islands.Noise;

// Didactic noise scene.
// v1: samples raw noise values on the CPU, measures them exactly (histogram, water
// threshold or percentage) and draws with OnGUI. No GPU path, no materials.
// v2: Simplex grouped under one button, offset moved by dragging the image, map colours
// by percentage bands with selectable palettes, and a parameter code to copy and paste.
public class NoiseLab : MonoBehaviour
{
    public enum NoiseType
    {
        Value,
        PerlinSquare,
        SimplexOldGradient,      // Simplex2D<Perlin>: the old gradient, peaks ~±0.43
        SimplexPerlinOnSimplex,  // Simplex2D<PerlinOnSimplex>: the fixed gradient, ~±0.98
        SimplexSimplex,          // Simplex2D<Simplex>: ~±1.00
        VoronoiF1
    }

    [SerializeField] NoiseType type = NoiseType.SimplexPerlinOnSimplex;
    [SerializeField] int seed = 0;
    [SerializeField, Range(1, 32)] int frequency = 8;
    [SerializeField, Range(1, 6)] int octaves = 1;
    [SerializeField, Range(2, 4)] int lacunarity = 2;
    [SerializeField, Range(0f, 1f)] float gain = 0.5f;
    [SerializeField, Range(64, 512)] int resolution = 256;

    // v2: where the view looks, in domain units (one view width = 1).
    [SerializeField] Vector2 offset = Vector2.zero;

    // Step 3: water. A raw threshold depends on the noise range; a percentage does not.
    public enum WaterMode { RawThreshold, Percentage }

    [SerializeField] WaterMode waterMode = WaterMode.RawThreshold;
    [SerializeField, Range(-1f, 1f)] float rawThreshold = 0f;
    [SerializeField, Range(0f, 100f)] float waterPercent = 40f;

    // v2: palette by id ("mapa" = the built-in map palette).
    [SerializeField] string paletteId = "mapa";

    // v2: where the percentages are measured. View: the samples on screen, so the sea
    // level follows the window and an island changes as it is dragged. World: a fixed
    // reference region that does not depend on the offset, so the cuts stay put.
    public enum CutsFrom { View, World }

    [SerializeField] CutsFrom cutsFrom = CutsFrom.World;

    // v2: resolution while dragging (full resolution again on release).
    [SerializeField] int dragResolution = 64;

    const int WorldSamples = 128;   // reference grid, WorldSamples² points
    const float WorldSpan = 16f;    // ... spread over 16 × 16 view widths around the origin

    const int Bins = 64; // histogram bins over [-1, 1]

    float[] values, sorted;
    float threshold, waterFraction;
    Texture2D texture;
    Color32[] pixels;
    bool dirty = true;

    // Step 2: measured statistics of the raw values.
    readonly int[] histogram = new int[Bins];
    int histogramPeak;
    float min, max, p1, p99, mean, sd;
    float sampleMs, calibrateMs;
    int sampleN; // side of the current values grid (lower while dragging)

    float[] worldValues, worldSorted;
    string worldKey;

    // Timing history, one entry per frame: frame time and sampling time (0 = no resample).
    const int PerfFrames = 120;
    readonly float[] perfFrame = new float[PerfFrames], perfSample = new float[PerfFrames];
    int perfHead;

    // Longer log for the export: every frame since the last Clear, up to LogFrames.
    const int LogFrames = 3600;
    readonly float[] logTime = new float[LogFrames], logFrame = new float[LogFrames], logSample = new float[LogFrames];
    readonly int[] logN = new int[LogFrames];
    readonly bool[] logDrag = new bool[LogFrames];
    int logHead, logCount;
    float logStart;
    string perfStatus = "";

    void OnValidate() => dirty = true;

    void Update()
    {
        float sampledMs = 0f;
        if (dirty)
        {
            dirty = false;
            moved = false;
            Sample(resolution);
            sampledMs = sampleMs;
            Measure();
            Calibrate();
            Flood();
            Paint();
        }
        else if (moved)
        {
            // While dragging: new samples (at the drag resolution), but the cuts stay where
            // they were. Statistics, quantiles and bands are measured again on release.
            moved = false;
            Sample(dragging ? Mathf.Min(dragResolution, resolution) : resolution);
            sampledMs = sampleMs;
            Paint();
        }

        perfFrame[perfHead] = Time.unscaledDeltaTime * 1000f;
        perfSample[perfHead] = sampledMs;
        perfHead = (perfHead + 1) % PerfFrames;

        if (logCount == 0) logStart = Time.unscaledTime;
        logTime[logHead] = Time.unscaledTime - logStart;
        logFrame[logHead] = Time.unscaledDeltaTime * 1000f;
        logSample[logHead] = sampledMs;
        logN[logHead] = sampledMs > 0f ? sampleN : 0;
        logDrag[logHead] = dragging;
        logHead = (logHead + 1) % LogFrames;
        logCount = Mathf.Min(logCount + 1, LogFrames);
    }

    // ---------------------------------------------------------------------------------
    // Performance export: a plain-text report (device, settings, summary, then one CSV row
    // per frame), copied to the clipboard and saved to a file. In the editor the file goes
    // to <project>/NoiseLabPerf/; on a phone, to Application.persistentDataPath.
    // ---------------------------------------------------------------------------------

    string BuildPerfReport()
    {
        var frames = new System.Collections.Generic.List<float>();
        var dragFrames = new System.Collections.Generic.List<float>();
        var dragSamples = new System.Collections.Generic.List<float>();
        var fullSamples = new System.Collections.Generic.List<float>();
        int first = (logHead - logCount + LogFrames) % LogFrames;
        for (int k = 0; k < logCount; k++)
        {
            int i = (first + k) % LogFrames;
            frames.Add(logFrame[i]);
            if (logDrag[i]) dragFrames.Add(logFrame[i]);
            if (logSample[i] > 0f) (logDrag[i] ? dragSamples : fullSamples).Add(logSample[i]);
        }

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("# NoiseLab perf export v1");
        sb.AppendLine($"date: {System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", Inv)}");
        sb.AppendLine($"device: {SystemInfo.deviceModel} | {SystemInfo.operatingSystem}");
        sb.AppendLine($"cpu: {SystemInfo.processorType} x{SystemInfo.processorCount} @ {SystemInfo.processorFrequency} MHz | ram {SystemInfo.systemMemorySize} MB");
#if ENABLE_IL2CPP
        const string backend = "IL2CPP";
#else
        const string backend = "Mono";
#endif
        sb.AppendLine($"unity: {Application.unityVersion} | {Application.platform} | {backend} | " +
                      $"{(Application.isEditor ? "editor" : Debug.isDebugBuild ? "development build" : "release build")}");
        sb.AppendLine($"screen: {Screen.width}x{Screen.height} | target fps {Application.targetFrameRate}");
        sb.AppendLine($"resolution: {resolution} | drag resolution: {dragResolution} | world reference: {WorldSamples}x{WorldSamples}, last {F(R1(calibrateMs))} ms");
        sb.AppendLine($"code: {BuildCode()}");
        sb.AppendLine();
        sb.AppendLine($"## summary ({logCount} frames, {F(R1(logCount > 0 ? logTime[(logHead - 1 + LogFrames) % LogFrames] : 0f))} s)");
        sb.AppendLine("set | count | avg ms | p50 | p95 | max");
        sb.AppendLine(StatsRow("frame (all)", frames));
        sb.AppendLine(StatsRow("frame (dragging)", dragFrames));
        sb.AppendLine(StatsRow("sample (dragging)", dragSamples));
        sb.AppendLine(StatsRow("sample (full, on release or change)", fullSamples));
        sb.AppendLine($"dragging frames under 33.3 ms (30 fps): {Share(dragFrames, 33.3f)} | under 16.7 ms (60 fps): {Share(dragFrames, 16.7f)}");
        sb.AppendLine();
        sb.AppendLine("## frames");
        sb.AppendLine("t_s,frame_ms,sample_ms,n,drag");
        for (int k = 0; k < logCount; k++)
        {
            int i = (first + k) % LogFrames;
            sb.Append(logTime[i].ToString("0.000", Inv)).Append(',')
              .Append(logFrame[i].ToString("0.0", Inv)).Append(',')
              .Append(logSample[i].ToString("0.0", Inv)).Append(',')
              .Append(logN[i]).Append(',')
              .Append(logDrag[i] ? 1 : 0).AppendLine();
        }
        return sb.ToString();
    }

    static float R1(float v) => Mathf.Round(v * 10f) / 10f;

    static string StatsRow(string name, System.Collections.Generic.List<float> data)
    {
        if (data.Count == 0) return $"{name} | 0 | - | - | - | -";
        var s = data.ToArray();
        System.Array.Sort(s);
        float sum = 0f;
        foreach (float v in s) sum += v;
        return $"{name} | {s.Length} | {F(R1(sum / s.Length))} | {F(R1(s[s.Length / 2]))} | " +
               $"{F(R1(s[Mathf.Min(s.Length - 1, (int)(0.95f * s.Length))]))} | {F(R1(s[s.Length - 1]))}";
    }

    static string Share(System.Collections.Generic.List<float> data, float limit)
    {
        if (data.Count == 0) return "-";
        int under = 0;
        foreach (float v in data) if (v < limit) under++;
        return $"{100f * under / data.Count:0}%";
    }

    void ExportPerf()
    {
        string report = BuildPerfReport();
        GUIUtility.systemCopyBuffer = report;
        try
        {
            string dir = Application.isEditor
                ? System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "..", "NoiseLabPerf"))
                : Application.persistentDataPath;
            System.IO.Directory.CreateDirectory(dir);
            string path = System.IO.Path.Combine(dir, $"noiselab-perf-{System.DateTime.Now.ToString("yyyyMMdd-HHmmss", Inv)}.txt");
            System.IO.File.WriteAllText(path, report);
            Debug.Log($"[NoiseLab] Perf export: {path}");
            perfStatus = $"Copied ({logCount} frames) and saved:\n{path}";
        }
        catch (System.Exception e)
        {
            perfStatus = $"Copied ({logCount} frames); file not saved: {e.Message}";
        }
    }

    static float[] Ensure(float[] a, int length) => a != null && a.Length == length ? a : new float[length];

    // Exact, not estimated: sorts a copy of every sample.
    void Measure()
    {
        sorted = Ensure(sorted, values.Length);
        System.Array.Copy(values, sorted, values.Length);
        System.Array.Sort(sorted);
        int count = sorted.Length;
        min = sorted[0];
        max = sorted[count - 1];
        p1 = sorted[(int)(0.01f * (count - 1))];
        p99 = sorted[(int)(0.99f * (count - 1))];

        double sum = 0, sumSq = 0;
        System.Array.Clear(histogram, 0, Bins);
        foreach (float v in values)
        {
            sum += v;
            sumSq += (double)v * v;
            histogram[BinOf(v)]++;
        }
        mean = (float)(sum / count);
        sd = (float)math.sqrt(math.max(0.0, sumSq / count - (sum / count) * (sum / count)));

        histogramPeak = 1;
        foreach (int h in histogram) histogramPeak = Mathf.Max(histogramPeak, h);
    }

    // ---------------------------------------------------------------------------------
    // Water and bands
    // ---------------------------------------------------------------------------------

    // Nine bands: three of water, six of land. Each group splits by percentage, like the
    // water itself: "the highest 8 % of the land is snow". Shares are per group and are
    // normalised by their sum, so they do not need to add up to exactly 100.
    const int WaterBands = 3, LandBands = 6, BandCount = WaterBands + LandBands;

    static readonly string[] BandNames =
        { "Deep", "Water", "Shallow", "Beach", "Grass", "Forest", "Hillside", "Rock", "Snow" };

    static readonly float[] DefaultWaterShares = { 50f, 30f, 20f };          // deep → shallow
    static readonly float[] DefaultLandShares = { 4f, 30f, 30f, 16f, 12f, 8f }; // beach → snow

    readonly float[] waterShares = (float[])DefaultWaterShares.Clone();
    readonly float[] landShares = (float[])DefaultLandShares.Clone();

    // edges[i] is the lowest raw value of band i + 1. edges[WaterBands - 1] is the water line.
    readonly float[] edges = new float[BandCount - 1];

    // Raw mode: the threshold is given, the area is measured.
    // Percentage mode: the area is given, the threshold is read from the sorted values
    // (the quantile, or inverse CDF): the value that leaves that fraction below it.
    // The band cuts are quantiles too, inside the water and inside the land.
    // The quantiles come from `source`: the view's own sorted samples, or the fixed world
    // reference. The water area shown in the numbers is always measured in the view.
    void Flood()
    {
        float[] source = cutsFrom == CutsFrom.World ? worldSorted : sorted;
        int count = source.Length;
        if (waterMode == WaterMode.Percentage)
        {
            int k = Mathf.RoundToInt(waterPercent / 100f * count); // cells under water
            threshold = k <= 0 ? source[0] : k >= count ? source[count - 1] + 1e-6f : source[k];
        }
        else
        {
            threshold = rawThreshold;
        }

        waterFraction = (float)LowerBound(sorted, threshold) / sorted.Length;

        // Water bands over source[0, below), land bands over source[below, count).
        int below = LowerBound(source, threshold);
        CutGroup(source, waterShares, 0, below, threshold, 0);
        edges[WaterBands - 1] = threshold;
        CutGroup(source, landShares, below, count, source[count - 1] + 1e-6f, WaterBands);
    }

    // The world reference: same noise, same settings, sampled on a fixed grid that ignores
    // the offset. Recomputed only when a noise setting changes, never while moving.
    void Calibrate()
    {
        if (cutsFrom != CutsFrom.World) return;
        string key = $"{type}|{seed}|{frequency}|{octaves}|{lacunarity}|{gain}";
        if (key == worldKey && worldSorted != null) return;
        worldKey = key;

        var watch = System.Diagnostics.Stopwatch.StartNew();
        const float step = WorldSpan / WorldSamples;
        worldValues = Ensure(worldValues, WorldSamples * WorldSamples);
        SampleInto(worldValues, WorldSamples, -0.5f * WorldSpan + 0.5f * step, -0.5f * WorldSpan + 0.5f * step, step);
        worldSorted = Ensure(worldSorted, worldValues.Length);
        System.Array.Copy(worldValues, worldSorted, worldValues.Length);
        System.Array.Sort(worldSorted);
        calibrateMs = (float)watch.Elapsed.TotalMilliseconds;
    }

    void CutGroup(float[] sorted, float[] shares, int start, int end, float top, int firstEdge)
    {
        float total = 0f;
        foreach (float s in shares) total += math.max(0f, s);
        float cumulative = 0f;
        for (int j = 0; j < shares.Length - 1; j++)
        {
            cumulative += math.max(0f, shares[j]);
            float c = total > 0f ? cumulative / total : (j + 1f) / shares.Length;
            int idx = start + Mathf.RoundToInt(c * (end - start));
            edges[firstEdge + j] = idx >= end ? top : sorted[idx];
        }
    }

    // First index whose value is >= v (the number of values below v).
    static int LowerBound(float[] a, float v)
    {
        int lo = 0, hi = a.Length;
        while (lo < hi)
        {
            int mid = (lo + hi) >> 1;
            if (a[mid] < v) lo = mid + 1; else hi = mid;
        }
        return lo;
    }

    int BandOf(float v)
    {
        int b = 0;
        while (b < edges.Length && v >= edges[b]) b++;
        return b;
    }

    // Keeps the group at 100 when one share moves: the others scale to fill the rest.
    static void SetShare(float[] shares, int i, float value)
    {
        value = Mathf.Clamp(Mathf.Round(value), 0f, 100f);
        float others = 0f;
        for (int j = 0; j < shares.Length; j++) if (j != i) others += shares[j];
        float rest = 100f - value;
        for (int j = 0; j < shares.Length; j++)
        {
            if (j == i) continue;
            shares[j] = others > 0f ? shares[j] * rest / others : rest / (shares.Length - 1);
        }
        shares[i] = value;
    }

    static int BinOf(float v) => math.clamp((int)((v * 0.5f + 0.5f) * Bins), 0, Bins - 1);

    void OnDestroy()
    {
        if (texture != null) Destroy(texture);
    }

    // ---------------------------------------------------------------------------------
    // Sampling
    // ---------------------------------------------------------------------------------

    // The view: n × n cell centres starting at the offset, one view width = 1.
    void Sample(int n)
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        sampleN = n;
        values = Ensure(values, n * n);
        float step = 1f / n;
        SampleInto(values, n, offset.x + 0.5f * step, offset.y + 0.5f * step, step);
        sampleMs = (float)watch.Elapsed.TotalMilliseconds;
    }

    void SampleInto(float[] target, int n, float originX, float originZ, float step)
    {
        var settings = Settings.Default;
        settings.seed = seed;
        settings.frequency = frequency;
        settings.octaves = octaves;
        settings.lacunarity = lacunarity;
        settings.persistence = gain;

        switch (type)
        {
            case NoiseType.Value: Fill<Lattice2D<LatticeNormal, Value>>(settings, target, n, originX, originZ, step); break;
            case NoiseType.PerlinSquare: Fill<Lattice2D<LatticeNormal, Perlin>>(settings, target, n, originX, originZ, step); break;
            case NoiseType.SimplexOldGradient: Fill<Simplex2D<Perlin>>(settings, target, n, originX, originZ, step); break;
            case NoiseType.SimplexPerlinOnSimplex: Fill<Simplex2D<PerlinOnSimplex>>(settings, target, n, originX, originZ, step); break;
            case NoiseType.SimplexSimplex: Fill<Simplex2D<Simplex>>(settings, target, n, originX, originZ, step); break;
            case NoiseType.VoronoiF1: Fill<Voronoi2D<LatticeNormal, Worley, F1>>(settings, target, n, originX, originZ, step); break;
        }
    }

    // Same sampling as MapNoiseBridge2D (cell centres, four at a time), but the value is
    // stored raw (no remap, no clamp). Point (x, y) is at origin + (x, y) · step.
    static void Fill<N>(Settings settings, float[] target, int n, float originX, float originZ, float step)
        where N : struct, INoise
    {
        for (int y = 0; y < n; y++)
        {
            float pz = originZ + y * step;
            for (int x = 0; x < n; x += 4)
            {
                float4 px = new float4(x, x + 1, x + 2, x + 3) * step + originX;
                float4 v = GetFractalNoise<N>(new float4x3(px, 0f, pz), settings).v;
                for (int k = 0; k < 4 && x + k < n; k++)
                    target[y * n + x + k] = v[k];
            }
        }
    }

    // ---------------------------------------------------------------------------------
    // Palettes
    // ---------------------------------------------------------------------------------

    struct Palette
    {
        public string id, name;
        public Color32[] bands; // BandCount colours, deep water → snow
    }

    // The map palette. Land colours come from BiomeColorPalette's defaults (Beach,
    // Grassland, TemperateForest, Shrubland, Snow); the three blues and the rock are new.
    static Palette MapPalette() => new Palette
    {
        id = "mapa",
        name = "Map",
        bands = new[]
        {
            Hex("0A2A66"), Hex("1F5FA8"), Hex("4FA3D9"),
            Hex("F0E5B0"), Hex("B8C66A"), Hex("4A7C44"), Hex("A8A065"), Hex("8C8378"), Hex("F5F5FF")
        }
    };

    // Web palettes: copied from claudiobustamante-site/src/data/palette-library.json
    // (commit 9914f4c) into Resources/palette-library.json. They have 4 to 8 colours with
    // no height order, so they are sorted dark → light and interpolated to the nine bands.
    [System.Serializable] class LibraryJson { public PaletteJson[] palettes; }
    [System.Serializable] class PaletteJson { public string id, name; public string[] colors; }

    Palette[] palettes;
    int paletteIndex;

    void LoadPalettes()
    {
        var list = new System.Collections.Generic.List<Palette> { MapPalette() };
        var asset = Resources.Load<TextAsset>("palette-library");
        if (asset != null)
        {
            var library = JsonUtility.FromJson<LibraryJson>(asset.text);
            if (library?.palettes != null)
                foreach (var p in library.palettes)
                    if (p.colors != null && p.colors.Length >= 2)
                        list.Add(new Palette { id = p.id, name = p.name, bands = Spread(p.colors) });
        }
        palettes = list.ToArray();
        paletteIndex = FindPalette(paletteId);
    }

    int FindPalette(string id)
    {
        for (int i = 0; i < palettes.Length; i++) if (palettes[i].id == id) return i;
        return 0;
    }

    static Color32[] Spread(string[] hexes)
    {
        var colours = new Color32[hexes.Length];
        for (int i = 0; i < hexes.Length; i++) colours[i] = Hex(hexes[i]);
        System.Array.Sort(colours, (a, b) => Luma(a).CompareTo(Luma(b)));

        var bands = new Color32[BandCount];
        for (int i = 0; i < BandCount; i++)
        {
            float t = (float)i / (BandCount - 1) * (colours.Length - 1);
            int lo = Mathf.Min((int)t, colours.Length - 2);
            bands[i] = Color32.Lerp(colours[lo], colours[lo + 1], t - lo);
        }
        return bands;
    }

    static float Luma(Color32 c) => 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;

    static Color32 Hex(string hex)
    {
        if (!hex.StartsWith("#")) hex = "#" + hex;
        ColorUtility.TryParseHtmlString(hex, out Color c);
        return c;
    }

    Color32[] Bands => palettes[paletteIndex].bands;

    // Flat colour per band: the map reads by its cuts, not by gradients.
    void Paint()
    {
        int n = sampleN;
        if (texture == null || texture.width != n)
        {
            if (texture != null) Destroy(texture);
            texture = new Texture2D(n, n, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
        }
        if (palettes == null) LoadPalettes();
        if (palettes[paletteIndex].id != paletteId) paletteIndex = FindPalette(paletteId); // code or Inspector

        if (pixels == null || pixels.Length != n * n) pixels = new Color32[n * n];
        var bands = Bands;
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = bands[BandOf(values[i])];
        texture.SetPixels32(pixels);
        texture.Apply();
    }

    // ---------------------------------------------------------------------------------
    // Parameter code: NL1;t=simplex/perlin;s=84253;f=3;o=6;l=2;g=0.53;w=%92;x=0;y=0;
    // pal=mapa;b=50,30,20/4,30,30,16,12,8. Version first, short keys, invariant culture
    // (a phone set to Spanish would otherwise write 0,53). Unknown keys are ignored.
    // ---------------------------------------------------------------------------------

    static readonly string[] TypeCodes =
        { "value", "perlin", "simplex/old", "simplex/perlin", "simplex/simplex", "voronoi" };

    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    // Shortest text that parses back to the same float, so editor and phone get the same map.
    static string F(float v)
    {
        string s = v.ToString(Inv);
        return float.Parse(s, Inv) == v ? s : v.ToString("G9", Inv);
    }

    static string Join(float[] a)
    {
        var parts = new string[a.Length];
        for (int i = 0; i < a.Length; i++) parts[i] = F(Mathf.Round(a[i] * 10f) / 10f);
        return string.Join(",", parts);
    }

    string BuildCode() =>
        $"NL1;t={TypeCodes[(int)type]};s={seed};f={frequency};o={octaves};l={lacunarity};" +
        $"g={F(gain)};w={(waterMode == WaterMode.Percentage ? "%" + F(waterPercent) : "r" + F(rawThreshold))};" +
        $"x={F(offset.x)};y={F(offset.y)};pal={paletteId};b={Join(waterShares)}/{Join(landShares)};" +
        $"q={(cutsFrom == CutsFrom.World ? "w" : "v")}";
    // Reserved for v3: c= (height curve, points x:y). Unknown keys are ignored on paste,
    // so v2 codes keep working in v3 and v3 codes still open here, minus what v2 lacks.

    // Returns null on success, or a short error.
    string ApplyCode(string code)
    {
        if (string.IsNullOrWhiteSpace(code)) return "empty";
        code = code.Trim();
        if (!code.StartsWith("NL1;") && code != "NL1") return "not an NL1 code";

        foreach (string part in code.Split(';'))
        {
            int eq = part.IndexOf('=');
            if (eq <= 0) continue;
            string key = part.Substring(0, eq).Trim(), val = part.Substring(eq + 1).Trim();
            switch (key)
            {
                case "t":
                    int t = System.Array.IndexOf(TypeCodes, val);
                    if (t >= 0) type = (NoiseType)t;
                    break;
                case "s": if (int.TryParse(val, NumberStyles.Integer, Inv, out int s)) seed = s; break;
                case "f": if (int.TryParse(val, NumberStyles.Integer, Inv, out int f)) frequency = Mathf.Clamp(f, 1, 32); break;
                case "o": if (int.TryParse(val, NumberStyles.Integer, Inv, out int o)) octaves = Mathf.Clamp(o, 1, 6); break;
                case "l": if (int.TryParse(val, NumberStyles.Integer, Inv, out int l)) lacunarity = Mathf.Clamp(l, 2, 4); break;
                case "g": if (TryF(val, out float g)) gain = Mathf.Clamp01(g); break;
                case "w":
                    if (val.StartsWith("%") && TryF(val.Substring(1), out float wp))
                    { waterMode = WaterMode.Percentage; waterPercent = Mathf.Clamp(wp, 0f, 100f); }
                    else if (val.StartsWith("r") && TryF(val.Substring(1), out float wr))
                    { waterMode = WaterMode.RawThreshold; rawThreshold = Mathf.Clamp(wr, -1f, 1f); }
                    break;
                case "x": if (TryF(val, out float x)) offset.x = x; break;
                case "y": if (TryF(val, out float y)) offset.y = y; break;
                case "pal":
                    if (palettes == null) LoadPalettes();
                    paletteIndex = FindPalette(val);
                    paletteId = palettes[paletteIndex].id; // unknown ids fall back to the map palette
                    break;
                case "q":
                    if (val == "w") cutsFrom = CutsFrom.World;
                    else if (val == "v") cutsFrom = CutsFrom.View;
                    break;
                case "b":
                    string[] groups = val.Split('/');
                    if (groups.Length == 2)
                    {
                        ReadShares(groups[0], waterShares);
                        ReadShares(groups[1], landShares);
                    }
                    break;
            }
        }
        if (type == NoiseType.SimplexOldGradient || type == NoiseType.SimplexPerlinOnSimplex ||
            type == NoiseType.SimplexSimplex)
            lastSimplex = type;
        dirty = true;
        return null;
    }

    static bool TryF(string s, out float v) =>
        float.TryParse(s, NumberStyles.Float, Inv, out v);

    static void ReadShares(string text, float[] shares)
    {
        string[] parts = text.Split(',');
        if (parts.Length != shares.Length) return;
        var read = new float[shares.Length];
        for (int i = 0; i < parts.Length; i++)
            if (!TryF(parts[i], out read[i])) return;
        read.CopyTo(shares, 0);
    }

    // ---------------------------------------------------------------------------------
    // On-screen controls. Everything is laid out in a virtual screen 800 units tall and
    // scaled to the real one, so text and sliders stay finger-sized on a phone.
    // ---------------------------------------------------------------------------------

    const float VirtualHeight = 800f;

    enum Page { Noise, Map, Code, Perf }
    Page page = Page.Noise;

    static readonly string[] PageLabels = { "Noise", "Map", "Code", "Perf" };
    static readonly string[] CutsLabels = { "% of the view", "% of the world" };
    static readonly int[] DragResolutions = { 32, 64, 128, 512 };
    static readonly string[] DragLabels = { "32", "64", "128", "Full" };
    string seedText = "";
    static readonly string[] FamilyLabels = { "Value", "Perlin", "Simplex", "Voronoi F1" };
    static readonly string[] SimplexLabels = { "Old", "Perlin", "Simplex" };
    static readonly string[] WaterLabels = { "Raw threshold", "% water" };

    NoiseType lastSimplex = NoiseType.SimplexPerlinOnSimplex;
    int selectedBand = BandCount - 1;
    string codeText = "", codeStatus = "";
    string offsetXText = "", offsetYText = "";

    // Drag state.
    Rect imageRect;
    bool dragging, moved;
    Vector2 lastPointer;

    void OnGUI()
    {
        if (texture == null) return;

        float scale = Screen.height / VirtualHeight;
        GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
        float width = Screen.width / scale, height = VirtualHeight;
        const float margin = 16f;

        Rect controls;
        if (width < height * 0.9f)
        {
            // Portrait: view on top, controls underneath. The controls get their height
            // first; the image takes what is left after histogram (70) and numbers (60).
            const float controlsHeight = 330f, viewExtras = 8f + 70f + 4f + 60f + 8f;
            float side = Mathf.Min(width - 2f * margin,
                                   height - 2f * margin - viewExtras - controlsHeight);
            float bottom = DrawView((width - side) * 0.5f, margin, side);
            controls = new Rect(margin, bottom + 8f, width - 2f * margin, height - bottom - 8f - margin);
        }
        else
        {
            // Landscape: view on the left, controls in a column on the right.
            float side = Mathf.Min(height * 0.6f, width * 0.5f);
            DrawView(margin, margin, side);
            float x = 2f * margin + side;
            controls = new Rect(x, margin, Mathf.Min(420f, width - x - margin), height - 2f * margin);
        }

        HandleDrag();
        DrawControls(controls);
    }

    // One finger (or the mouse) on the image moves the view; the point under the finger
    // stays under it. IMGUI turns touches into mouse events, already in GUI space.
    void HandleDrag()
    {
        Event e = Event.current;
        switch (e.type)
        {
            case EventType.MouseDown:
                if (!imageRect.Contains(e.mousePosition)) return;
                dragging = true;
                lastPointer = e.mousePosition;
                GUIUtility.keyboardControl = 0; // leave any text field
                e.Use();
                break;
            case EventType.MouseDrag:
                if (!dragging) return;
                Vector2 d = e.mousePosition - lastPointer;
                lastPointer = e.mousePosition;
                offset.x -= d.x / imageRect.width;
                offset.y += d.y / imageRect.height; // GUI y grows down, texture y grows up
                moved = true;
                e.Use();
                break;
            case EventType.MouseUp:
                if (!dragging) return;
                dragging = false;
                offset.x = Mathf.Round(offset.x * 1000f) / 1000f; // readable codes
                offset.y = Mathf.Round(offset.y * 1000f) / 1000f;
                dirty = true;
                e.Use();
                break;
        }
    }

    void DrawControls(Rect area)
    {
        GUILayout.BeginArea(area);
        page = (Page)GUILayout.Toolbar((int)page, PageLabels, GUILayout.Height(30f));
        GUILayout.Space(4f);

        GUI.changed = false;
        switch (page)
        {
            case Page.Noise: NoisePage(); break;
            case Page.Map: MapPage(); break;
            case Page.Code: CodePage(); break;
            case Page.Perf: PerfPage(); break;
        }
        if (GUI.changed && page != Page.Perf) dirty = true; // Perf buttons must not resample (they would pollute the log)
        GUILayout.EndArea();
    }

    void NoisePage()
    {
        int family = type switch
        {
            NoiseType.Value => 0,
            NoiseType.PerlinSquare => 1,
            NoiseType.VoronoiF1 => 3,
            _ => 2
        };
        int newFamily = GUILayout.Toolbar(family, FamilyLabels, GUILayout.Height(30f));
        if (newFamily != family)
            type = newFamily switch
            {
                0 => NoiseType.Value,
                1 => NoiseType.PerlinSquare,
                3 => NoiseType.VoronoiF1,
                _ => lastSimplex
            };

        if (newFamily == 2)
        {
            // Old stays here on purpose: same picture as Perlin, a third of the range.
            int variant = (int)type - (int)NoiseType.SimplexOldGradient;
            variant = GUILayout.Toolbar(variant, SimplexLabels, GUILayout.Height(30f));
            type = lastSimplex = (NoiseType)((int)NoiseType.SimplexOldGradient + variant);
        }

        GUILayout.BeginHorizontal();
        GUILayout.Label("Seed", GUILayout.Width(110f));
        seed = IntField("seed", ref seedText, seed);
        if (GUILayout.Button("−", GUILayout.Height(30f))) seed--;
        if (GUILayout.Button("+", GUILayout.Height(30f))) seed++;
        if (GUILayout.Button("Random", GUILayout.Height(30f))) seed = UnityEngine.Random.Range(0, 100000); // UI only; the noise itself stays seeded
        GUILayout.EndHorizontal();

        frequency = IntSlider("Frequency", frequency, 1, 32);
        octaves = IntSlider("Octaves", octaves, 1, 6);
        lacunarity = IntSlider("Lacunarity", lacunarity, 2, 4);
        gain = Mathf.Round(Slider("Gain", gain, 0f, 1f, "0.00") * 100f) / 100f;

        // Offset: drag the image, or type it. The fields follow the drag unless focused.
        GUILayout.BeginHorizontal(GUILayout.Height(30f));
        GUILayout.Label("Offset x, y", GUILayout.Width(110f));
        offset.x = OffsetField("offset-x", ref offsetXText, offset.x);
        offset.y = OffsetField("offset-y", ref offsetYText, offset.y);
        if (GUILayout.Button("0", GUILayout.Width(40f), GUILayout.Height(30f))) offset = Vector2.zero;
        GUILayout.EndHorizontal();
    }

    static int IntField(string name, ref string text, int value)
    {
        bool focused = GUI.GetNameOfFocusedControl() == name;
        if (!focused) text = value.ToString(Inv);
        GUI.SetNextControlName(name);
        bool before = GUI.changed;
        GUI.changed = false;
        string edited = GUILayout.TextField(text, GUILayout.Height(30f), GUILayout.MinWidth(90f));
        bool changed = GUI.changed;
        GUI.changed = before;
        if (!changed) return value;
        text = edited;
        if (!int.TryParse(edited, NumberStyles.Integer, Inv, out int parsed) || parsed == value) return value;
        GUI.changed = true;
        return parsed;
    }

    static float OffsetField(string name, ref string text, float value)
    {
        bool focused = GUI.GetNameOfFocusedControl() == name;
        if (!focused) text = F(value);
        GUI.SetNextControlName(name);
        bool before = GUI.changed;
        GUI.changed = false;
        string edited = GUILayout.TextField(text, GUILayout.Height(30f), GUILayout.ExpandWidth(true));
        bool changed = GUI.changed;
        GUI.changed = before;
        if (!changed) return value;
        text = edited;
        if (!TryF(edited, out float parsed) || parsed == value) return value;
        GUI.changed = true;
        return parsed;
    }

    void MapPage()
    {
        waterMode = (WaterMode)GUILayout.Toolbar((int)waterMode, WaterLabels, GUILayout.Height(30f));
        if (waterMode == WaterMode.RawThreshold)
            rawThreshold = Mathf.Round(Slider("Threshold", rawThreshold, -1f, 1f, "+0.00;-0.00") * 100f) / 100f;
        else
            waterPercent = Mathf.Round(Slider("Water %", waterPercent, 0f, 100f, "0"));
        cutsFrom = (CutsFrom)GUILayout.Toolbar((int)cutsFrom, CutsLabels, GUILayout.Height(30f));

        if (palettes == null) LoadPalettes();
        GUILayout.BeginHorizontal(GUILayout.Height(30f));
        GUILayout.Label("Palette", GUILayout.Width(110f));
        if (GUILayout.Button("<", GUILayout.Width(40f), GUILayout.Height(30f)))
            paletteIndex = (paletteIndex + palettes.Length - 1) % palettes.Length;
        GUILayout.Label(palettes[paletteIndex].name, GUILayout.ExpandWidth(true));
        if (GUILayout.Button(">", GUILayout.Width(40f), GUILayout.Height(30f)))
            paletteIndex = (paletteIndex + 1) % palettes.Length;
        GUILayout.EndHorizontal();
        paletteId = palettes[paletteIndex].id;

        // Legend: one swatch per band, tap to pick the band the slider edits.
        Rect strip = GUILayoutUtility.GetRect(10f, 30f, GUILayout.ExpandWidth(true));
        float w = strip.width / BandCount;
        var bands = Bands;
        for (int b = 0; b < BandCount; b++)
        {
            var r = new Rect(strip.x + b * w, strip.y, w - 2f, strip.height);
            if (b == selectedBand) DrawRect(new Rect(r.x - 1f, r.y - 2f, r.width + 2f, r.height + 4f), Color.white);
            DrawRect(r, bands[b]);
            if (GUI.Button(r, GUIContent.none, GUIStyle.none)) selectedBand = b;
        }

        bool water = selectedBand < WaterBands;
        float[] shares = water ? waterShares : landShares;
        int i = water ? selectedBand : selectedBand - WaterBands;
        GUILayout.Label($"{BandNames[selectedBand]}: share of the {(water ? "water" : "land")}");
        float share = Slider("Share %", shares[i], 0f, 100f, "0");
        if (Mathf.Abs(share - shares[i]) >= 0.5f) SetShare(shares, i, share);

        if (GUILayout.Button("Default bands", GUILayout.Height(30f)))
        {
            DefaultWaterShares.CopyTo(waterShares, 0);
            DefaultLandShares.CopyTo(landShares, 0);
        }
    }

    void CodePage()
    {
        bool before = GUI.changed;
        GUI.SetNextControlName("code");
        if (GUI.GetNameOfFocusedControl() != "code") codeText = BuildCode();
        codeText = GUILayout.TextArea(codeText, GUILayout.Height(90f));
        GUI.changed = before; // typing the code does not resample; Apply does

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Copy", GUILayout.Height(40f)))
        {
            GUIUtility.systemCopyBuffer = BuildCode();
            codeStatus = "Copied.";
        }
        if (GUILayout.Button("Paste", GUILayout.Height(40f)))
        {
            string error = ApplyCode(GUIUtility.systemCopyBuffer);
            codeStatus = error == null ? "Pasted." : "Paste: " + error;
            GUIUtility.keyboardControl = 0;
        }
        if (GUILayout.Button("Apply text", GUILayout.Height(40f)))
        {
            string error = ApplyCode(codeText);
            codeStatus = error == null ? "Applied." : "Apply: " + error;
            GUIUtility.keyboardControl = 0;
        }
        GUILayout.EndHorizontal();
        GUILayout.Label(codeStatus);
    }

    // Timing over the last PerfFrames frames, oldest on the left. Grey: frame time.
    // Orange: time spent sampling in that frame (none = no resample). Guides at 16.7 ms
    // (60 fps, green) and 33.3 ms (30 fps, yellow).
    void PerfPage()
    {
        float maxFrame = 0f, maxSample = 0f, sumSample = 0f;
        int samples = 0;
        for (int i = 0; i < PerfFrames; i++)
        {
            maxFrame = Mathf.Max(maxFrame, perfFrame[i]);
            if (perfSample[i] <= 0f) continue;
            maxSample = Mathf.Max(maxSample, perfSample[i]);
            sumSample += perfSample[i];
            samples++;
        }
        float scaleMs = Mathf.Max(40f, maxFrame, maxSample);

        Rect plot = GUILayoutUtility.GetRect(10f, 130f, GUILayout.ExpandWidth(true));
        DrawRect(plot, new Color(0f, 0f, 0f, 0.6f));
        float w = plot.width / PerfFrames;
        for (int k = 0; k < PerfFrames; k++)
        {
            int i = (perfHead + k) % PerfFrames;
            float hf = plot.height * Mathf.Min(1f, perfFrame[i] / scaleMs);
            DrawRect(new Rect(plot.x + k * w, plot.yMax - hf, Mathf.Max(1f, w - 1f), hf), new Color(1f, 1f, 1f, 0.3f));
            float hs = plot.height * Mathf.Min(1f, perfSample[i] / scaleMs);
            DrawRect(new Rect(plot.x + k * w, plot.yMax - hs, Mathf.Max(1f, w - 1f), hs), new Color(1f, 0.6f, 0.1f));
        }
        DrawRect(new Rect(plot.x, plot.yMax - plot.height * 16.7f / scaleMs, plot.width, 1f), new Color(0.3f, 1f, 0.3f));
        DrawRect(new Rect(plot.x, plot.yMax - plot.height * 33.3f / scaleMs, plot.width, 1f), new Color(1f, 0.9f, 0.2f));
        GUI.Label(new Rect(plot.x + 4f, plot.y + 2f, 200f, 20f), $"top = {scaleMs:0} ms");

        GUILayout.Label(
            $"last sample {sampleMs:0.0} ms at {sampleN}×{sampleN}   " +
            $"(last {PerfFrames} frames: {samples} samples, avg {(samples > 0 ? sumSample / samples : 0f):0.0}, max {maxSample:0.0} ms)\n" +
            $"world reference {calibrateMs:0} ms ({WorldSamples}×{WorldSamples}, only when a noise setting changes)");

        GUILayout.BeginHorizontal(GUILayout.Height(30f));
        GUILayout.Label("Drag res", GUILayout.Width(110f));
        int current = System.Array.IndexOf(DragResolutions, dragResolution);
        int chosen = GUILayout.Toolbar(current < 0 ? 1 : current, DragLabels, GUILayout.Height(30f));
        dragResolution = DragResolutions[chosen];
        GUILayout.EndHorizontal();

        // Export: Clear, do the movement to measure, then Export (clipboard + file).
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Clear log", GUILayout.Height(40f)))
        {
            logCount = 0;
            logHead = 0;
            perfStatus = "Log cleared. Move the map, then Export.";
        }
        if (GUILayout.Button($"Export ({logCount} frames)", GUILayout.Height(40f))) ExportPerf();
        GUILayout.EndHorizontal();
        GUILayout.Label(perfStatus);
    }

    static int IntSlider(string label, int value, int min, int max) =>
        Mathf.RoundToInt(Slider(label, value, min, max, "0"));

    static float Slider(string label, float value, float min, float max, string format)
    {
        GUILayout.BeginHorizontal(GUILayout.Height(30f));
        GUILayout.Label(label, GUILayout.Width(110f));
        value = GUILayout.HorizontalSlider(value, min, max, GUILayout.ExpandWidth(true));
        GUILayout.Label(value.ToString(format), GUILayout.Width(50f));
        GUILayout.EndHorizontal();
        return value;
    }

    // Image, histogram and numbers. Returns the y where they end.
    float DrawView(float left, float top, float side)
    {
        imageRect = new Rect(left, top, side, side);
        GUI.DrawTexture(imageRect, texture);

        // Histogram strip under the image. x axis: raw value from -1 (left) to +1 (right).
        var plot = new Rect(left, top + side + 8f, side, 70f);
        DrawRect(plot, new Color(0f, 0f, 0f, 0.6f));
        float barWidth = plot.width / Bins;
        for (int b = 0; b < Bins; b++)
        {
            float h = plot.height * histogram[b] / histogramPeak;
            DrawRect(new Rect(plot.x + b * barWidth, plot.yMax - h, barWidth - 1f, h), Color.white);
        }
        DrawMarker(plot, 0f, new Color(1f, 1f, 1f, 0.35f));        // zero
        DrawMarker(plot, p1, new Color(1f, 0.6f, 0.1f));           // p1 and p99 in orange
        DrawMarker(plot, p99, new Color(1f, 0.6f, 0.1f));
        DrawMarker(plot, min, new Color(1f, 0.2f, 0.2f));          // min and max in red
        DrawMarker(plot, max, new Color(1f, 0.2f, 0.2f));
        var bands = Bands;
        for (int e = 0; e < edges.Length; e++)                     // band cuts in the colour above them
            if (e != WaterBands - 1) DrawTick(plot, edges[e], bands[e + 1]);
        DrawMarker(plot, threshold, new Color(0.3f, 0.7f, 1f));    // water line in blue

        var numbers = new Rect(plot.x, plot.yMax + 4f, plot.width, 60f);
        GUI.Label(
            numbers,
            $"min {min:+0.00;-0.00}   max {max:+0.00;-0.00}   " +
            $"p1 {p1:+0.00;-0.00}   p99 {p99:+0.00;-0.00}\n" +
            $"mean {mean:+0.000;-0.000}   sd {sd:0.000}   ({values.Length} samples, {sampleMs:0} ms)\n" +
            $"water {waterFraction * 100f:0.0}%   threshold {threshold:+0.000;-0.000}   " +
            (waterMode == WaterMode.Percentage ? "(from %)" : "(raw)"));
        return numbers.yMax;
    }

    void DrawMarker(Rect plot, float value, Color color) =>
        DrawRect(new Rect(plot.x + (value * 0.5f + 0.5f) * plot.width - 1f, plot.y, 2f, plot.height), color);

    // Short tick at the bottom of the histogram, for the band cuts.
    void DrawTick(Rect plot, float value, Color color) =>
        DrawRect(new Rect(plot.x + (value * 0.5f + 0.5f) * plot.width - 1f, plot.yMax - 14f, 3f, 14f), color);

    static void DrawRect(Rect r, Color color)
    {
        var previous = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(r, Texture2D.whiteTexture);
        GUI.color = previous;
    }
}
