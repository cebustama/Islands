using System.Globalization;
using Unity.Mathematics;
using UnityEngine;

using Islands;
using Islands.PCG.Fields;
using Islands.PCG.Layout.Maps;
using static Islands.Noise;

// Didactic noise scene.
// v1: samples raw noise values on the CPU, measures them exactly (histogram, water
// threshold or percentage) and draws with OnGUI. No GPU path, no materials.
// v2: Simplex grouped under one button, offset moved by dragging the image, map colours
// by percentage bands with selectable palettes, and a parameter code to copy and paste.
// v3: more than one field. The height can come from three noises chained through
// editable splines (continentalness, erosion, peaks and valleys), and the land can be
// coloured by biome from temperature and moisture (a prototype of Stage_Biome2D's
// rules, not the stage itself). Optional relief shading.
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

    // Field 0: the one-noise height (v1/v2). These stay as separate members so v2 scenes
    // and codes keep their values.
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

    // ---------------------------------------------------------------------------------
    // v3: more fields. Field k uses seed + k · 7919, so field 0 keeps the v2 seed and the
    // others are decorrelated from it and from each other.
    // ---------------------------------------------------------------------------------

    [System.Serializable]
    public struct NoiseField
    {
        public NoiseType type;
        [Range(1, 32)] public int frequency;
        [Range(1, 6)] public int octaves;
        [Range(2, 4)] public int lacunarity;
        [Range(0f, 1f)] public float gain;

        public NoiseField(NoiseType type, int frequency, int octaves, int lacunarity, float gain)
        {
            this.type = type;
            this.frequency = frequency;
            this.octaves = octaves;
            this.lacunarity = lacunarity;
            this.gain = gain;
        }
    }

    const int FieldCount = 6;
    const int FHeight = 0, FCont = 1, FEros = 2, FPeaks = 3, FTemp = 4, FMoist = 5;
    static readonly string[] FieldNames = { "Height", "Continent", "Erosion", "Peaks", "Temp", "Moist" };

    static NoiseField[] DefaultExtraFields() => new[]
    {
        new NoiseField(NoiseType.SimplexPerlinOnSimplex, 2, 4, 2, 0.5f), // continentalness: big shapes
        new NoiseField(NoiseType.SimplexPerlinOnSimplex, 3, 3, 2, 0.5f), // erosion: where relief is allowed
        new NoiseField(NoiseType.SimplexPerlinOnSimplex, 8, 4, 2, 0.5f), // peaks and valleys: detail
        new NoiseField(NoiseType.SimplexPerlinOnSimplex, 2, 2, 2, 0.5f), // temperature noise
        new NoiseField(NoiseType.SimplexPerlinOnSimplex, 3, 2, 2, 0.5f), // moisture noise
    };

    [SerializeField] NoiseField[] extraFields = DefaultExtraFields(); // fields 1..5

    // Height: one noise (v2), or three noises chained through splines.
    public enum HeightSource { OneNoise, Splines }
    [SerializeField] HeightSource heightSource = HeightSource.OneNoise;

    // Land colour: percentage bands (v2), or biomes from temperature and moisture.
    public enum Colouring { Bands, Biomes }
    [SerializeField] Colouring colouring = Colouring.Bands;

    [SerializeField] bool shade = false;

    // Contour lines every contourStep of raw height (absolute, unlike the bands, which are
    // percentages). Every fifth line is darker.
    [SerializeField] bool contours = false;
    [SerializeField, Range(0.01f, 0.2f)] float contourStep = 0.05f;

    // Camera background behind the view and the controls (Unity's default blue, replaced).
    [SerializeField] Color background = new Color32(0x22, 0x26, 0x2E, 0xFF);
    [SerializeField, Range(0f, 3f)] float relief = 1f;

    // Climate tunables. Same formulas and defaults as Stage_Biome2D, except that the lapse
    // uses the land rank (0 at the coast, 1 at the highest land) and the latitude is the
    // world y (equator at y = 0, poles at ±LatitudeSpan view widths). Coast distances are
    // measured in cells of a 256 map, as the stage's defaults assume.
    [SerializeField, Range(0f, 1f)] float baseTemperature = 0.7f;
    [SerializeField, Range(0f, 1f)] float lapseRate = 0.5f;
    [SerializeField, Range(0f, 1f)] float latitudeEffect = 0.5f;
    [SerializeField, Range(0f, 0.5f)] float coastModeration = 0.1f;
    [SerializeField, Range(0f, 0.3f)] float tempNoiseAmplitude = 0.05f;
    [SerializeField, Range(0f, 1f)] float coastalMoistureBonus = 0.5f;
    [SerializeField, Range(0f, 1f)] float coastDecayRate = 0.3f;
    [SerializeField, Range(0f, 1f)] float moistureNoiseAmplitude = 0.3f;

    // Wind (v3): one direction; the angle is where it blows toward (0 = east, 90 = north).
    [SerializeField] bool wind = false;
    [SerializeField, Range(0f, 360f)] float windAngle = 0f;              // 0 = from the west (westerlies)
    [SerializeField, Range(0f, 1f)] float windStrength = 0.6f;            // weight of the air's humidity in the moisture
    [SerializeField, Range(0f, 1f)] float windLift = 0.6f;                // rain when the air climbs
    [SerializeField, Range(0f, 0.05f)] float windDry = 0.01f;             // loss per cell over flat land
    [SerializeField, Range(0f, 0.2f)] float windEvaporation = 0.05f;      // gain per cell over the sea

    const float LatitudeSpan = 8f;   // view widths from the equator to a pole
    const float CoastUnits = 256f;   // coast distance unit: one cell of a 256 map

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

    // v3: per-field samples, cached by settings (and, for the view, by offset and size),
    // so editing a spline or a climate slider never resamples a noise.
    readonly float[][] viewField = new float[FieldCount][];
    readonly string[] viewFieldKey = new string[FieldCount];
    readonly float[][] worldField = new float[FieldCount][];
    readonly string[] worldFieldKey = new string[FieldCount];

    // v3: climate of the view.
    float[] coast, temp, moist;
    readonly int[] biomeCount = new int[(int)BiomeType.COUNT];

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

    // v3: 60 fps instead of Android's default 30 (the S22 screen runs at 120 Hz).
    void Start()
    {
        Application.targetFrameRate = 60;
        var cam = Camera.main;
        if (cam != null)
        {
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = background;
        }
    }

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
            Climate();
            Paint();
        }
        else if (moved)
        {
            // While dragging: new samples (at the drag resolution), but the cuts stay where
            // they were. Statistics, quantiles and bands are measured again on release.
            moved = false;
            Sample(dragging ? Mathf.Min(dragResolution, resolution) : resolution);
            sampledMs = sampleMs;
            Climate();
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
        sb.AppendLine($"fields sampled per view: {ActiveFieldCount()} (height: {heightSource}, colour: {colouring})");
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
        float[] source = CutSource;
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

    float[] CutSource => cutsFrom == CutsFrom.World ? worldSorted : sorted;

    // The world reference: same noise(s), same settings, sampled on a fixed grid that
    // ignores the offset. Noises are resampled only when their settings change; editing a
    // spline only recombines and sorts (16 384 values).
    void Calibrate()
    {
        if (cutsFrom != CutsFrom.World) return;
        string key = heightSource == HeightSource.OneNoise
            ? FieldKey(FHeight)
            : $"S|{FieldKey(FCont)}|{FieldKey(FEros)}|{FieldKey(FPeaks)}|{CurvesCode()}";
        if (key == worldKey && worldSorted != null) return;
        worldKey = key;

        var watch = System.Diagnostics.Stopwatch.StartNew();
        const float step = WorldSpan / WorldSamples;
        const float origin = -0.5f * WorldSpan + 0.5f * step;
        foreach (int k in HeightFields())
        {
            string fk = FieldKey(k);
            if (worldFieldKey[k] == fk && worldField[k] != null) continue;
            worldField[k] = Ensure(worldField[k], WorldSamples * WorldSamples);
            SampleInto(worldField[k], GetField(k), FieldSeed(k), WorldSamples, origin, origin, step);
            worldFieldKey[k] = fk;
        }
        worldValues = Ensure(worldValues, WorldSamples * WorldSamples);
        CombineHeight(worldValues, worldField);
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
    // Fields
    // ---------------------------------------------------------------------------------

    NoiseField GetField(int k)
    {
        if (k == FHeight) return new NoiseField(type, frequency, octaves, lacunarity, gain);
        EnsureExtraFields();
        return extraFields[k - 1];
    }

    void SetField(int k, NoiseField f)
    {
        if (k == FHeight)
        {
            type = f.type;
            frequency = f.frequency;
            octaves = f.octaves;
            lacunarity = f.lacunarity;
            gain = f.gain;
            return;
        }
        EnsureExtraFields();
        extraFields[k - 1] = f;
    }

    void EnsureExtraFields()
    {
        if (extraFields != null && extraFields.Length == FieldCount - 1) return;
        var defaults = DefaultExtraFields();
        if (extraFields != null)
            for (int i = 0; i < Mathf.Min(extraFields.Length, defaults.Length); i++) defaults[i] = extraFields[i];
        extraFields = defaults;
    }

    int FieldSeed(int k) => seed + k * 7919;

    string FieldKey(int k)
    {
        var f = GetField(k);
        return $"{f.type}|{FieldSeed(k)}|{f.frequency}|{f.octaves}|{f.lacunarity}|{F(f.gain)}";
    }

    static readonly int[] OneNoiseFields = { FHeight };
    static readonly int[] SplineFields = { FCont, FEros, FPeaks };
    static readonly int[] ClimateFields = { FTemp, FMoist };

    int[] HeightFields() => heightSource == HeightSource.OneNoise ? OneNoiseFields : SplineFields;

    // The fields shown in the Noise page, in order.
    int[] ActiveFields()
    {
        var list = new System.Collections.Generic.List<int>(HeightFields());
        if (colouring == Colouring.Biomes) list.AddRange(ClimateFields);
        return list.ToArray();
    }

    int ActiveFieldCount() => ActiveFields().Length;

    // ---------------------------------------------------------------------------------
    // Sampling
    // ---------------------------------------------------------------------------------

    // The view: n × n cell centres starting at the offset, one view width = 1. Every
    // active field is sampled (or taken from its cache), then the height is combined.
    void Sample(int n)
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        sampleN = n;
        float step = 1f / n;
        foreach (int k in ActiveFields())
        {
            string key = $"{FieldKey(k)}|{n}|{F(offset.x)}|{F(offset.y)}";
            if (viewFieldKey[k] == key && viewField[k] != null) continue;
            viewField[k] = Ensure(viewField[k], n * n);
            SampleInto(viewField[k], GetField(k), FieldSeed(k), n, offset.x + 0.5f * step, offset.y + 0.5f * step, step);
            viewFieldKey[k] = key;
        }
        values = Ensure(values, n * n);
        CombineHeight(values, viewField);
        sampleMs = (float)watch.Elapsed.TotalMilliseconds;
    }

    // One noise: the height is field 0 as it is (v2). Splines, after Minecraft's terrain
    // as Henrik Kniberg explains it, simplified to one formula:
    //   height = C(continentalness) + E(erosion) · PV(peaks and valleys)
    // C sets the base level (ocean, coast, inland), E how much relief is allowed there,
    // and PV adds the peaks and the valleys.
    void CombineHeight(float[] target, float[][] src)
    {
        if (heightSource == HeightSource.OneNoise)
        {
            System.Array.Copy(src[FHeight], target, target.Length);
            return;
        }
        var sc = ToSpline(curves[0]);
        var se = ToSpline(curves[1]);
        var sp = ToSpline(curves[2]);
        float[] c = src[FCont], e = src[FEros], p = src[FPeaks];
        for (int i = 0; i < target.Length; i++)
            target[i] = sc.Evaluate(c[i]) + se.Evaluate(e[i]) * sp.Evaluate(p[i]);
    }

    void SampleInto(float[] target, NoiseField field, int fieldSeed, int n, float originX, float originZ, float step)
    {
        var settings = Settings.Default;
        settings.seed = fieldSeed;
        settings.frequency = field.frequency;
        settings.octaves = field.octaves;
        settings.lacunarity = field.lacunarity;
        settings.persistence = field.gain;

        switch (field.type)
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
    // Splines: three editable curves over the raw noise range [-1, 1].
    // ---------------------------------------------------------------------------------

    static readonly string[] CurveNames = { "Continent", "Erosion", "Peaks" };

    static System.Collections.Generic.List<Vector2>[] DefaultCurves() => new[]
    {
        // C: deep ocean, shelf, coast, a gentle rise inland.
        new System.Collections.Generic.List<Vector2>
            { new(-1f, -0.8f), new(-0.35f, -0.35f), new(-0.1f, 0.05f), new(0.35f, 0.2f), new(1f, 0.6f) },
        // E: low erosion = rough land, high erosion = flat.
        new System.Collections.Generic.List<Vector2>
            { new(-1f, 1f), new(-0.2f, 0.6f), new(0.3f, 0.15f), new(1f, 0.05f) },
        // PV: valleys below, peaks above.
        new System.Collections.Generic.List<Vector2>
            { new(-1f, -0.3f), new(-0.3f, -0.05f), new(0.2f, 0.1f), new(0.6f, 0.45f), new(1f, 0.6f) },
    };

    System.Collections.Generic.List<Vector2>[] curves = DefaultCurves();

    static System.Collections.Generic.List<Vector2> DefaultCurve(int i) => DefaultCurves()[i];

    static ScalarSpline ToSpline(System.Collections.Generic.List<Vector2> points)
    {
        var xs = new float[points.Count];
        var ys = new float[points.Count];
        for (int i = 0; i < points.Count; i++) { xs[i] = points[i].x; ys[i] = points[i].y; }
        return new ScalarSpline(xs, ys);
    }

    static float Evaluate(System.Collections.Generic.List<Vector2> points, float x)
    {
        if (x <= points[0].x) return points[0].y;
        for (int i = 1; i < points.Count; i++)
        {
            if (x > points[i].x) continue;
            float span = points[i].x - points[i - 1].x;
            return span <= 0f ? points[i].y : Mathf.Lerp(points[i - 1].y, points[i].y, (x - points[i - 1].x) / span);
        }
        return points[points.Count - 1].y;
    }

    // ---------------------------------------------------------------------------------
    // Climate: temperature, moisture and biome, after Stage_Biome2D (M.1–M.3).
    // ---------------------------------------------------------------------------------

    void Climate()
    {
        if (colouring != Colouring.Biomes) return;
        int n = sampleN, count = n * n;
        coast = Ensure(coast, count);
        temp = Ensure(temp, count);
        moist = Ensure(moist, count);

        CoastDistance(n);
        float toUnits = CoastUnits / n;

        float[] source = CutSource;
        int below = LowerBound(source, threshold);
        int landCount = source.Length - below;
        float[] tn = viewField[FTemp], mn = viewField[FMoist];

        landRank = Ensure(landRank, count);
        for (int y = 0; y < n; y++)
        {
            float yWorld = offset.y + (y + 0.5f) / n;
            float lat = math.saturate(math.abs(yWorld) / LatitudeSpan);
            for (int x = 0; x < n; x++)
            {
                int i = y * n + x;
                // Land rank: 0 at the coast, 1 at the highest land of the cut source.
                float rank = landCount > 0
                    ? math.saturate((float)(LowerBound(source, values[i]) - below) / landCount)
                    : 0f;
                landRank[i] = rank;
                float cd = coast[i] * toUnits;

                float t = baseTemperature
                        - lapseRate * rank
                        - latitudeEffect * lat
                        + coastModeration / (1f + cd)
                        + tempNoiseAmplitude * 0.5f * tn[i];        // amplitude · (noise01 − 0.5)
                temp[i] = math.saturate(t);
            }
        }

        if (wind) WindField(n);

        for (int i = 0; i < count; i++)
        {
            float cd = coast[i] * toUnits;
            float m = moistureNoiseAmplitude * math.saturate(mn[i] * 0.5f + 0.5f)
                    + coastalMoistureBonus / (1f + cd * coastDecayRate)
                    + (wind ? windStrength * windAir[i] : 0f);
            moist[i] = math.saturate(m);
        }
    }

    // ---------------------------------------------------------------------------------
    // Wind: one direction for the whole map. Air enters at the upwind edge of the view,
    // saturated, and moves cell by cell along the wind. Over water it picks up moisture;
    // over land it slowly dries, and much faster when it climbs (orographic rain): the
    // windward slope gets the rain, the lee gets dry air (rain shadow). windAir is the
    // humidity of the air passing each cell, 0–1.
    // The sweep goes line by line along the axis the wind moves most on; the air of a cell
    // comes from the previous line, interpolated at the point one step upwind. O(n²), any
    // angle. Air entering the view is assumed to come from the sea, so land near the
    // upwind edge looks wetter than it would with the real terrain beyond the edge.
    // ---------------------------------------------------------------------------------

    float[] landRank, windAir;

    const float LiftScale = 4f; // lift 1 = the air keeps e^−4 ≈ 2 % after climbing the whole land range

    void WindField(int n)
    {
        windAir = Ensure(windAir, n * n);
        float a = windAngle * Mathf.Deg2Rad;
        float wx = Mathf.Cos(a), wy = Mathf.Sin(a);      // direction the wind blows toward (y up)
        bool xMajor = Mathf.Abs(wx) >= Mathf.Abs(wy);
        float major = xMajor ? wx : wy, minor = xMajor ? wy : wx;
        int dir = major >= 0f ? 1 : -1;
        float slope = minor / Mathf.Abs(major);          // minor-axis shift per major step
        float dist = Mathf.Sqrt(1f + slope * slope) * CoastUnits / n; // step length, in cells of a 256 map
        float keepFlat = Mathf.Exp(-windDry * dist);
        float pickUp = 1f - Mathf.Exp(-windEvaporation * dist);

        int Cell(int u, int j) => xMajor ? j * n + u : u * n + j;

        int first = dir > 0 ? 0 : n - 1;
        for (int k = 0; k < n; k++)
        {
            int u = first + dir * k;
            for (int j = 0; j < n; j++)
            {
                int i = Cell(u, j);
                float air, rankBefore;
                float pj = j - slope;                     // where this air was one step ago
                if (k == 0 || pj < 0f || pj > n - 1)
                {
                    air = 1f;                             // enters the view: sea air
                    rankBefore = landRank[i];
                }
                else
                {
                    int j0 = Mathf.Min((int)pj, n - 2);
                    float f = pj - j0;
                    int prev = u - dir;
                    air = Mathf.Lerp(windAir[Cell(prev, j0)], windAir[Cell(prev, j0 + 1)], f);
                    rankBefore = Mathf.Lerp(landRank[Cell(prev, j0)], landRank[Cell(prev, j0 + 1)], f);
                }

                if (values[i] < threshold)
                    air += (1f - air) * pickUp;           // over the sea
                else
                {
                    float rise = Mathf.Max(0f, landRank[i] - rankBefore);
                    air *= keepFlat * Mathf.Exp(-windLift * LiftScale * rise);
                }
                windAir[i] = air;
            }
        }
    }

    static readonly string[] Compass = { "E", "NE", "N", "NW", "W", "SW", "S", "SE" };

    // "from W": meteorology names a wind by where it comes from.
    string WindName() => $"from {Compass[Mathf.RoundToInt(((windAngle + 180f) % 360f) / 45f) % 8]}";

    // Distance from each land cell to the nearest water cell of the view, in cells (0 on
    // water). Exact Euclidean distance transform (Felzenszwalb & Huttenlocher, "Distance
    // Transforms of Sampled Functions", Theory of Computing 8, 2012): squared distances
    // along each column, then along each row, each pass a lower envelope of parabolas.
    // A chamfer (1, √2) was used first; its octagonal contours showed up as straight
    // diagonal biome borders when the moisture decays slowly. The pipeline's CoastDist is
    // a 4-neighbour BFS (diamond contours); changing that is a governed change, not this.
    // Water outside the view is not seen: near the edges the coast can look farther.
    double[] edtF, edtD, edtZ;
    int[] edtV;

    void CoastDistance(int n)
    {
        const double Inf = 1e20;
        if (edtF == null || edtF.Length != n)
        {
            edtF = new double[n];
            edtD = new double[n];
            edtZ = new double[n + 1];
            edtV = new int[n];
        }

        for (int x = 0; x < n; x++)
        {
            for (int y = 0; y < n; y++) edtF[y] = values[y * n + x] >= threshold ? Inf : 0.0;
            Edt1D(n);
            for (int y = 0; y < n; y++) coast[y * n + x] = (float)edtD[y];
        }
        for (int y = 0; y < n; y++)
        {
            int row = y * n;
            for (int x = 0; x < n; x++) edtF[x] = coast[row + x];
            Edt1D(n);
            for (int x = 0; x < n; x++) coast[row + x] = (float)System.Math.Sqrt(edtD[x]);
        }
    }

    // edtD[q] = min over p of (q − p)² + edtF[p], in O(n).
    void Edt1D(int n)
    {
        int k = 0;
        edtV[0] = 0;
        edtZ[0] = double.NegativeInfinity;
        edtZ[1] = double.PositiveInfinity;
        for (int q = 1; q < n; q++)
        {
            // z[0] = −∞, so the loop always stops at k = 0 at the latest.
            double s = Intersection(q, edtV[k]);
            while (s <= edtZ[k]) { k--; s = Intersection(q, edtV[k]); }
            k++;
            edtV[k] = q;
            edtZ[k] = s;
            edtZ[k + 1] = double.PositiveInfinity;
        }
        k = 0;
        for (int q = 0; q < n; q++)
        {
            while (edtZ[k + 1] < q) k++;
            double d = q - edtV[k];
            edtD[q] = d * d + edtF[edtV[k]];
        }
    }

    // Where the parabolas rooted at q and p cross.
    double Intersection(int q, int p) =>
        ((edtF[q] + (double)q * q) - (edtF[p] + (double)p * p)) / (2.0 * q - 2.0 * p);

    // Biome colours: BiomeColorPalette's defaults (Runtime/PCG/Inspection), by BiomeType.
    static readonly Color32[] BiomeColours =
    {
        Hex("5588AA"), Hex("F5F5FF"), Hex("B8C4CC"), Hex("2F4F35"), Hex("C9B88A"), Hex("A8A065"), Hex("4A7C44"),
        Hex("2F6842"), Hex("E6C989"), Hex("B8C66A"), Hex("5C9840"), Hex("1F7535"), Hex("F0E5B0")
    };

    BiomeType BiomeAt(int i)
    {
        var bt = BiomeTable.Lookup(temp[i], moist[i]);
        // Beach override, as in the stage: warm land next to water.
        if (coast[i] < 1.5f && temp[i] >= BiomeTable.BeachMinTemperature) bt = BiomeType.Beach;
        return bt;
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

    // Plain C# (no Unity call), because static initialisers use it and Unity may run them
    // on a loading thread. Accepts RRGGBB, with or without '#'; anything else is magenta.
    static Color32 Hex(string hex)
    {
        hex = hex.TrimStart('#');
        if (hex.Length != 6 || !uint.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint rgb))
            return new Color32(255, 0, 255, 255);
        return new Color32((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb, 255);
    }

    Color32[] Bands => palettes[paletteIndex].bands;

    // ---------------------------------------------------------------------------------
    // Painting
    // ---------------------------------------------------------------------------------

    // What the image shows. Map: bands or biomes. The rest: one field on its own.
    enum Show { Map, Height, Temperature, Moisture, Continent, Erosion, Peaks, WindAir }
    static readonly string[] ShowNames = { "Map", "Height", "Temperature", "Moisture", "Continent (raw)", "Erosion (raw)", "Peaks (raw)", "Wind air" };
    Show show = Show.Map;

    bool Available(Show s) => s switch
    {
        Show.Temperature or Show.Moisture => colouring == Colouring.Biomes,
        Show.Continent or Show.Erosion or Show.Peaks => heightSource == HeightSource.Splines,
        Show.WindAir => colouring == Colouring.Biomes && wind,
        _ => true
    };

    static readonly Color32 Cold = Hex("2C4A9A"), Mild = Hex("F2E6A0"), Hot = Hex("C0392B");
    static readonly Color32 Dry = Hex("B08850"), Wet = Hex("1E6E6E");

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
        if (!Available(show)) show = Show.Map;

        if (pixels == null || pixels.Length != n * n) pixels = new Color32[n * n];
        var bands = Bands;
        bool biomes = colouring == Colouring.Biomes;
        System.Array.Clear(biomeCount, 0, biomeCount.Length);

        for (int i = 0; i < pixels.Length; i++)
        {
            Color32 c;
            switch (show)
            {
                case Show.Map:
                    int band = BandOf(values[i]);
                    if (band < WaterBands) { c = bands[band]; break; }
                    if (biomes)
                    {
                        var bt = BiomeAt(i);
                        biomeCount[(int)bt]++;
                        c = BiomeColours[(int)bt];
                    }
                    else c = bands[band];
                    if (shade) c = Shade(c, i, n);
                    break;
                case Show.Height: c = Gray(values[i]); break;
                case Show.Temperature: c = Ramp3(Cold, Mild, Hot, temp[i]); break;
                case Show.Moisture: c = Color32.Lerp(Dry, Wet, moist[i]); break;
                case Show.WindAir: c = Color32.Lerp(Dry, Wet, windAir[i]); break;
                default: c = Gray(viewField[FCont + (show - Show.Continent)][i]); break;
            }
            if (contours && (show == Show.Map || show == Show.Height)) c = Contour(c, i, n);
            pixels[i] = c;
        }
        texture.SetPixels32(pixels);
        texture.Apply();
    }

    // A cell is on a line when its right or upper neighbour is in another step of height.
    Color32 Contour(Color32 c, int i, int n)
    {
        int x = i % n, y = i / n;
        int level = (int)math.floor(values[i] / contourStep);
        int right = x < n - 1 ? (int)math.floor(values[i + 1] / contourStep) : level;
        int up = y < n - 1 ? (int)math.floor(values[i + n] / contourStep) : level;
        if (right == level && up == level) return c;
        int line = math.max(level, math.max(right, up));            // the line between the two steps
        float f = line % 5 == 0 ? 0.45f : 0.7f;
        if (values[i] < threshold) f = math.lerp(f, 1f, 0.5f);       // fainter under water
        return new Color32((byte)(c.r * f), (byte)(c.g * f), (byte)(c.b * f), 255);
    }

    static Color32 Gray(float v)
    {
        byte g = (byte)math.clamp((int)((v * 0.5f + 0.5f) * 255f), 0, 255);
        return new Color32(g, g, g, 255);
    }

    static Color32 Ramp3(Color32 a, Color32 b, Color32 c, float t) =>
        t < 0.5f ? Color32.Lerp(a, b, t * 2f) : Color32.Lerp(b, c, t * 2f - 1f);

    // Relief shading: light from the upper left. The slope is measured in domain units, so
    // the drag resolution does not change it; a flat cell keeps its colour.
    Color32 Shade(Color32 c, int i, int n)
    {
        int x = i % n, y = i / n;
        float hl = values[y * n + Mathf.Max(x - 1, 0)], hr = values[y * n + Mathf.Min(x + 1, n - 1)];
        float hd = values[Mathf.Max(y - 1, 0) * n + x], hu = values[Mathf.Min(y + 1, n - 1) * n + x];
        float k = relief * 0.05f * n * 0.5f;
        float3 normal = math.normalize(new float3(-(hr - hl) * k, -(hu - hd) * k, 1f));
        float3 light = math.normalize(new float3(-1f, 1f, 1.2f));
        float f = math.max(0f, math.dot(normal, light)) / light.z;
        return new Color32(
            (byte)math.min(255f, c.r * f), (byte)math.min(255f, c.g * f), (byte)math.min(255f, c.b * f), 255);
    }

    // ---------------------------------------------------------------------------------
    // Parameter code: NL1;t=simplex/perlin;s=84253;f=3;o=6;l=2;g=0.53;w=%92;x=0;y=0;
    // pal=mapa;b=50,30,20/4,30,30,16,12,8. Version first, short keys, invariant culture
    // (a phone set to Spanish would otherwise write 0,53). Unknown keys are ignored.
    // v3 adds, only when in use: h=s (splines), n1..n5 (fields: type,f,o,l,g),
    // c= (the three curves, x:y points, curves separated by /), k=z (biomes),
    // cl= (climate tunables) and sh= (relief shading).
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

    string BuildCode()
    {
        string code =
            $"NL1;t={TypeCodes[(int)type]};s={seed};f={frequency};o={octaves};l={lacunarity};" +
            $"g={F(gain)};w={(waterMode == WaterMode.Percentage ? "%" + F(waterPercent) : "r" + F(rawThreshold))};" +
            $"x={F(offset.x)};y={F(offset.y)};pal={paletteId};b={Join(waterShares)}/{Join(landShares)};" +
            $"q={(cutsFrom == CutsFrom.World ? "w" : "v")}";
        if (heightSource == HeightSource.Splines)
            code += $";h=s;n1={FieldCode(FCont)};n2={FieldCode(FEros)};n3={FieldCode(FPeaks)};c={CurvesCode()}";
        if (colouring == Colouring.Biomes)
            code += $";k=z;n4={FieldCode(FTemp)};n5={FieldCode(FMoist)};cl=" +
                    string.Join(",", F(baseTemperature), F(lapseRate), F(latitudeEffect), F(coastModeration),
                                F(tempNoiseAmplitude), F(coastalMoistureBonus), F(coastDecayRate), F(moistureNoiseAmplitude));
        if (colouring == Colouring.Biomes && wind)
            code += ";wd=" + string.Join(",", F(windAngle), F(windStrength), F(windLift), F(windDry), F(windEvaporation));
        if (shade) code += $";sh={F(relief)}";
        if (contours) code += $";ct={F(contourStep)}";
        return code;
    }

    string FieldCode(int k)
    {
        var f = GetField(k);
        return $"{TypeCodes[(int)f.type]},{f.frequency},{f.octaves},{f.lacunarity},{F(f.gain)}";
    }

    string CurvesCode()
    {
        var parts = new string[curves.Length];
        for (int c = 0; c < curves.Length; c++)
        {
            var points = new string[curves[c].Count];
            for (int i = 0; i < points.Length; i++) points[i] = $"{F(curves[c][i].x)}:{F(curves[c][i].y)}";
            parts[c] = string.Join(",", points);
        }
        return string.Join("/", parts);
    }

    // v3 state goes back to its defaults before a code is applied, so a v2 code (which has
    // no v3 keys) opens exactly the v2 map.
    void ResetV3()
    {
        heightSource = HeightSource.OneNoise;
        colouring = Colouring.Bands;
        shade = false;
        relief = 1f;
        contours = false;
        contourStep = 0.05f;
        extraFields = DefaultExtraFields();
        curves = DefaultCurves();
        baseTemperature = 0.7f;
        lapseRate = 0.5f;
        latitudeEffect = 0.5f;
        coastModeration = 0.1f;
        tempNoiseAmplitude = 0.05f;
        coastalMoistureBonus = 0.5f;
        coastDecayRate = 0.3f;
        moistureNoiseAmplitude = 0.3f;
        wind = false;
        windAngle = 0f;
        windStrength = 0.6f;
        windLift = 0.6f;
        windDry = 0.01f;
        windEvaporation = 0.05f;
    }

    // Returns null on success, or a short error.
    string ApplyCode(string code)
    {
        if (string.IsNullOrWhiteSpace(code)) return "empty";
        code = code.Trim();
        if (!code.StartsWith("NL1;") && code != "NL1") return "not an NL1 code";

        ResetV3();
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
                case "h": heightSource = val == "s" ? HeightSource.Splines : HeightSource.OneNoise; break;
                case "k": colouring = val == "z" ? Colouring.Biomes : Colouring.Bands; break;
                case "n1": ReadField(FCont, val); break;
                case "n2": ReadField(FEros, val); break;
                case "n3": ReadField(FPeaks, val); break;
                case "n4": ReadField(FTemp, val); break;
                case "n5": ReadField(FMoist, val); break;
                case "c": ReadCurves(val); break;
                case "cl": ReadClimate(val); break;
                case "wd": ReadWind(val); break;
                case "ct":
                    if (TryF(val, out float ct)) { contours = true; contourStep = Mathf.Clamp(ct, 0.01f, 0.2f); }
                    break;
                case "sh":
                    if (TryF(val, out float r)) { shade = true; relief = Mathf.Clamp(r, 0f, 3f); }
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

    void ReadField(int k, string text)
    {
        string[] p = text.Split(',');
        if (p.Length != 5) return;
        int t = System.Array.IndexOf(TypeCodes, p[0]);
        if (t < 0 ||
            !int.TryParse(p[1], NumberStyles.Integer, Inv, out int f) ||
            !int.TryParse(p[2], NumberStyles.Integer, Inv, out int o) ||
            !int.TryParse(p[3], NumberStyles.Integer, Inv, out int l) ||
            !TryF(p[4], out float g)) return;
        SetField(k, new NoiseField((NoiseType)t, Mathf.Clamp(f, 1, 32), Mathf.Clamp(o, 1, 6), Mathf.Clamp(l, 2, 4), Mathf.Clamp01(g)));
    }

    // All three curves or none: each needs at least two points with x ascending.
    void ReadCurves(string text)
    {
        string[] parts = text.Split('/');
        if (parts.Length != 3) return;
        var read = new System.Collections.Generic.List<Vector2>[3];
        for (int c = 0; c < 3; c++)
        {
            read[c] = new System.Collections.Generic.List<Vector2>();
            foreach (string point in parts[c].Split(','))
            {
                string[] xy = point.Split(':');
                if (xy.Length != 2 || !TryF(xy[0], out float x) || !TryF(xy[1], out float y)) return;
                if (read[c].Count > 0 && x < read[c][read[c].Count - 1].x) return;
                read[c].Add(new Vector2(Mathf.Clamp(x, -1f, 1f), Mathf.Clamp(y, -1f, 1f)));
            }
            if (read[c].Count < 2) return;
        }
        curves = read;
    }

    void ReadClimate(string text)
    {
        string[] p = text.Split(',');
        if (p.Length != 8) return;
        var v = new float[8];
        for (int i = 0; i < 8; i++) if (!TryF(p[i], out v[i])) return;
        baseTemperature = Mathf.Clamp01(v[0]);
        lapseRate = Mathf.Clamp01(v[1]);
        latitudeEffect = Mathf.Clamp01(v[2]);
        coastModeration = Mathf.Clamp(v[3], 0f, 0.5f);
        tempNoiseAmplitude = Mathf.Clamp(v[4], 0f, 0.3f);
        coastalMoistureBonus = Mathf.Clamp01(v[5]);
        coastDecayRate = Mathf.Clamp01(v[6]);
        moistureNoiseAmplitude = Mathf.Clamp01(v[7]);
    }

    void ReadWind(string text)
    {
        string[] p = text.Split(',');
        if (p.Length != 5) return;
        var v = new float[5];
        for (int i = 0; i < 5; i++) if (!TryF(p[i], out v[i])) return;
        wind = true;
        windAngle = Mathf.Repeat(v[0], 360f);
        windStrength = Mathf.Clamp01(v[1]);
        windLift = Mathf.Clamp01(v[2]);
        windDry = Mathf.Clamp(v[3], 0f, 0.05f);
        windEvaporation = Mathf.Clamp(v[4], 0f, 0.2f);
    }

    // ---------------------------------------------------------------------------------
    // On-screen controls. Everything is laid out in a virtual screen 800 units tall and
    // scaled to the real one, so text and sliders stay finger-sized on a phone.
    // ---------------------------------------------------------------------------------

    const float VirtualHeight = 800f;

    enum Page { Noise, Shape, Map, Climate, Code, Perf }
    Page page = Page.Noise;

    static readonly string[] PageLabels = { "Noise", "Shape", "Map", "Climate", "Code", "Perf" };
    static readonly string[] CutsLabels = { "% of the view", "% of the world" };
    static readonly int[] DragResolutions = { 32, 64, 128, 512 };
    static readonly string[] DragLabels = { "32", "64", "128", "Full" };
    string seedText = "";
    static readonly string[] FamilyLabels = { "Value", "Perlin", "Simplex", "Voronoi F1" };
    static readonly string[] SimplexLabels = { "Old", "Perlin", "Simplex" };
    static readonly string[] WaterLabels = { "Raw threshold", "% water" };
    static readonly string[] HeightLabels = { "One noise", "Splines" };
    static readonly string[] ColouringLabels = { "Bands", "Biomes" };
    static readonly string[] ClimateLabels = { "Temperature", "Moisture", "Wind" };

    NoiseType lastSimplex = NoiseType.SimplexPerlinOnSimplex;
    int selectedBand = BandCount - 1;
    int selectedField = FHeight;
    int selectedCurve, selectedPoint = -1;
    bool curveDragging;
    int climateTab;
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
            const float controlsHeight = 350f, viewExtras = 8f + 70f + 4f + 60f + 8f;
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
        Row(2);
        switch (page)
        {
            case Page.Noise: NoisePage(); break;
            case Page.Shape: ShapePage(); break;
            case Page.Map: MapPage(); break;
            case Page.Climate: ClimatePage(); break;
            case Page.Code: CodePage(); break;
            case Page.Perf: PerfPage(); break;
        }
        if (GUI.changed && page != Page.Perf) dirty = true; // Perf buttons must not resample (they would pollute the log)
        GUI.backgroundColor = Color.white;
        GUILayout.EndArea();
    }

    // Each section tints its controls; a deeper row of tabs gets a lighter tint, so the rows
    // of buttons read as levels (top tabs → section → sub-section).
    static readonly Color[] SectionTints =
    {
        new(0.55f, 0.85f, 1f),   // Noise
        new(1f, 0.78f, 0.45f),   // Shape
        new(0.6f, 1f, 0.6f),     // Map
        new(1f, 0.62f, 0.55f),   // Climate
        new(0.85f, 0.85f, 0.85f),// Code
        new(0.8f, 0.7f, 1f),     // Perf
    };

    void Row(int level) =>
        GUI.backgroundColor = level <= 2 ? SectionTints[(int)page] : Color.Lerp(SectionTints[(int)page], Color.white, 0.55f);

    void NoisePage()
    {
        // v3: one row of field tabs when more than one noise is in use.
        int[] active = ActiveFields();
        int current = Mathf.Max(0, System.Array.IndexOf(active, selectedField));
        if (active.Length > 1)
        {
            var labels = new string[active.Length];
            for (int i = 0; i < active.Length; i++) labels[i] = FieldNames[active[i]];
            current = GUILayout.Toolbar(current, labels, GUILayout.Height(30f));
        }
        selectedField = active[current];
        var field = GetField(selectedField);
        if (active.Length > 1) Row(3);

        int family = field.type switch
        {
            NoiseType.Value => 0,
            NoiseType.PerlinSquare => 1,
            NoiseType.VoronoiF1 => 3,
            _ => 2
        };
        int newFamily = GUILayout.Toolbar(family, FamilyLabels, GUILayout.Height(30f));
        if (newFamily != family)
            field.type = newFamily switch
            {
                0 => NoiseType.Value,
                1 => NoiseType.PerlinSquare,
                3 => NoiseType.VoronoiF1,
                _ => lastSimplex
            };

        if (newFamily == 2)
        {
            // Old stays here on purpose: same picture as Perlin, a third of the range.
            int variant = (int)field.type - (int)NoiseType.SimplexOldGradient;
            variant = GUILayout.Toolbar(variant, SimplexLabels, GUILayout.Height(30f));
            field.type = lastSimplex = (NoiseType)((int)NoiseType.SimplexOldGradient + variant);
        }

        Row(2);
        GUILayout.BeginHorizontal();
        GUILayout.Label(selectedField == FHeight ? "Seed" : $"Seed (+{selectedField})", GUILayout.Width(110f));
        seed = IntField("seed", ref seedText, seed);
        if (GUILayout.Button("−", GUILayout.Height(30f))) seed--;
        if (GUILayout.Button("+", GUILayout.Height(30f))) seed++;
        if (GUILayout.Button("Random", GUILayout.Height(30f))) seed = UnityEngine.Random.Range(0, 100000); // UI only; the noise itself stays seeded
        GUILayout.EndHorizontal();

        field.frequency = IntSlider("Frequency", field.frequency, 1, 32);
        field.octaves = IntSlider("Octaves", field.octaves, 1, 6);
        field.lacunarity = IntSlider("Lacunarity", field.lacunarity, 2, 4);
        field.gain = Mathf.Round(Slider("Gain", field.gain, 0f, 1f, "0.00") * 100f) / 100f;
        SetField(selectedField, field);

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

    // v3: where the height comes from, the curves, and relief shading.
    void ShapePage()
    {
        heightSource = (HeightSource)GUILayout.Toolbar((int)heightSource, HeightLabels, GUILayout.Height(30f));

        GUILayout.BeginHorizontal(GUILayout.Height(30f));
        shade = GUILayout.Toggle(shade, " Shade", GUILayout.Width(110f));
        GUI.enabled = shade;
        relief = Mathf.Round(GUILayout.HorizontalSlider(relief, 0f, 3f, GUILayout.ExpandWidth(true)) * 10f) / 10f;
        GUILayout.Label(relief.ToString("0.0", Inv), GUILayout.Width(50f));
        GUI.enabled = true;
        GUILayout.EndHorizontal();

        if (heightSource == HeightSource.OneNoise)
        {
            GUILayout.Label("Height = the noise as it is (v2). Splines: three noises, one curve each.");
            return;
        }

        GUILayout.Label("height = C(continent) + E(erosion) · PV(peaks)");
        Row(3);
        int curve = GUILayout.Toolbar(selectedCurve, CurveNames, GUILayout.Height(30f));
        if (curve != selectedCurve) { selectedCurve = curve; selectedPoint = -1; }

        CurveEditor(curves[selectedCurve], viewField[FCont + selectedCurve]);

        var points = curves[selectedCurve];
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Add point", GUILayout.Height(30f)))
        {
            // In the middle of the widest gap, on the curve, so the shape does not change.
            int gap = 1;
            for (int i = 2; i < points.Count; i++)
                if (points[i].x - points[i - 1].x > points[gap].x - points[gap - 1].x) gap = i;
            float x = Mathf.Round((points[gap].x + points[gap - 1].x) * 50f) / 100f;
            points.Insert(gap, new Vector2(x, Mathf.Round(Evaluate(points, x) * 100f) / 100f));
            selectedPoint = gap;
        }
        GUI.enabled = selectedPoint > 0 && selectedPoint < points.Count - 1;
        if (GUILayout.Button("Remove", GUILayout.Height(30f)))
        {
            points.RemoveAt(selectedPoint);
            selectedPoint = -1;
        }
        GUI.enabled = true;
        if (GUILayout.Button("Reset curve", GUILayout.Height(30f)))
        {
            curves[selectedCurve] = DefaultCurve(selectedCurve);
            selectedPoint = -1;
        }
        GUILayout.EndHorizontal();
    }

    // x: raw noise value, −1 … +1 (the input's histogram in grey behind). y: the output,
    // −1 … +1. Drag a point; the end points move only up and down.
    void CurveEditor(System.Collections.Generic.List<Vector2> points, float[] input)
    {
        Rect plot = GUILayoutUtility.GetRect(10f, 130f, GUILayout.ExpandWidth(true));
        Vector2 ToGui(Vector2 p) => new(plot.x + (p.x * 0.5f + 0.5f) * plot.width, plot.yMax - (p.y * 0.5f + 0.5f) * plot.height);

        Event e = Event.current;
        switch (e.type)
        {
            case EventType.MouseDown:
                if (!plot.Contains(e.mousePosition)) break;
                int nearest = -1;
                float best = 24f * 24f;
                for (int i = 0; i < points.Count; i++)
                {
                    float d2 = (ToGui(points[i]) - e.mousePosition).sqrMagnitude;
                    if (d2 < best) { best = d2; nearest = i; }
                }
                selectedPoint = nearest;
                curveDragging = nearest >= 0;
                GUIUtility.keyboardControl = 0;
                e.Use();
                break;
            case EventType.MouseDrag:
                if (!curveDragging || selectedPoint < 0 || selectedPoint >= points.Count) break;
                float px = ((e.mousePosition.x - plot.x) / plot.width) * 2f - 1f;
                float py = ((plot.yMax - e.mousePosition.y) / plot.height) * 2f - 1f;
                int last = points.Count - 1;
                px = selectedPoint == 0 ? -1f
                   : selectedPoint == last ? 1f
                   : Mathf.Clamp(px, points[selectedPoint - 1].x + 0.02f, points[selectedPoint + 1].x - 0.02f);
                points[selectedPoint] = new Vector2(Mathf.Round(px * 100f) / 100f, Mathf.Round(Mathf.Clamp(py, -1f, 1f) * 100f) / 100f);
                GUI.changed = true;
                e.Use();
                break;
            case EventType.MouseUp:
                if (!curveDragging) break;
                curveDragging = false;
                e.Use();
                break;
            case EventType.Repaint:
                DrawRect(plot, new Color(0f, 0f, 0f, 0.6f));
                if (input != null)
                {
                    const int bins = 32;
                    var h = new int[bins];
                    int peak = 1;
                    foreach (float v in input) h[math.clamp((int)((v * 0.5f + 0.5f) * bins), 0, bins - 1)]++;
                    foreach (int c in h) peak = Mathf.Max(peak, c);
                    float bw = plot.width / bins;
                    for (int b = 0; b < bins; b++)
                    {
                        float bh = plot.height * h[b] / peak;
                        DrawRect(new Rect(plot.x + b * bw, plot.yMax - bh, bw - 1f, bh), new Color(1f, 1f, 1f, 0.15f));
                    }
                }
                DrawRect(new Rect(plot.x, plot.y + plot.height * 0.5f, plot.width, 1f), new Color(1f, 1f, 1f, 0.3f));
                DrawRect(new Rect(plot.x + plot.width * 0.5f, plot.y, 1f, plot.height), new Color(1f, 1f, 1f, 0.3f));

                // The curve, one column every 2 units, joined by vertical strokes.
                var curveColour = new Color(1f, 0.6f, 0.1f);
                float prevY = ToGui(new Vector2(-1f, Evaluate(points, -1f))).y;
                for (float gx = 0f; gx <= plot.width; gx += 2f)
                {
                    float x = gx / plot.width * 2f - 1f;
                    float gy = ToGui(new Vector2(x, Evaluate(points, x))).y;
                    float top = Mathf.Min(gy, prevY), bottom = Mathf.Max(gy, prevY);
                    DrawRect(new Rect(plot.x + gx - 1f, top - 1f, 2f, bottom - top + 2f), curveColour);
                    prevY = gy;
                }
                for (int i = 0; i < points.Count; i++)
                {
                    Vector2 g = ToGui(points[i]);
                    float s = i == selectedPoint ? 14f : 10f;
                    DrawRect(new Rect(g.x - s * 0.5f, g.y - s * 0.5f, s, s), i == selectedPoint ? Color.white : curveColour);
                }
                if (selectedPoint >= 0 && selectedPoint < points.Count)
                    GUI.Label(new Rect(plot.x + 4f, plot.y + 2f, 200f, 20f),
                              $"{points[selectedPoint].x:+0.00;-0.00} → {points[selectedPoint].y:+0.00;-0.00}");
                break;
        }
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
        GUILayout.BeginHorizontal(GUILayout.Height(30f));
        GUILayout.Label($"{BandNames[selectedBand]}: share of the {(water ? "water" : "land")}");
        if (GUILayout.Button("Default bands", GUILayout.Width(130f), GUILayout.Height(30f)))
        {
            DefaultWaterShares.CopyTo(waterShares, 0);
            DefaultLandShares.CopyTo(landShares, 0);
        }
        GUILayout.EndHorizontal();
        float share = Slider("Share %", shares[i], 0f, 100f, "0");
        if (Mathf.Abs(share - shares[i]) >= 0.5f) SetShare(shares, i, share);

        // v3: what the image shows.
        GUILayout.BeginHorizontal(GUILayout.Height(30f));
        GUILayout.Label("Layer", GUILayout.Width(110f));
        if (GUILayout.Button("<", GUILayout.Width(40f), GUILayout.Height(30f))) show = NextShow(-1);
        GUILayout.Label(ShowNames[(int)show], GUILayout.ExpandWidth(true));
        if (GUILayout.Button(">", GUILayout.Width(40f), GUILayout.Height(30f))) show = NextShow(+1);
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal(GUILayout.Height(30f));
        contours = GUILayout.Toggle(contours, " Contours", GUILayout.Width(110f));
        GUI.enabled = contours;
        contourStep = Mathf.Round(GUILayout.HorizontalSlider(contourStep, 0.01f, 0.2f, GUILayout.ExpandWidth(true)) * 100f) / 100f;
        GUILayout.Label(contourStep.ToString("0.00", Inv), GUILayout.Width(50f));
        GUI.enabled = true;
        GUILayout.EndHorizontal();
    }

    Show NextShow(int direction)
    {
        int count = ShowNames.Length, s = (int)show;
        for (int k = 0; k < count; k++)
        {
            s = (s + direction + count) % count;
            if (Available((Show)s)) return (Show)s;
        }
        return Show.Map;
    }

    // v3: land colour, and the climate tunables (Stage_Biome2D's, see the fields above).
    void ClimatePage()
    {
        colouring = (Colouring)GUILayout.Toolbar((int)colouring, ColouringLabels, GUILayout.Height(30f));
        if (colouring == Colouring.Bands)
        {
            GUILayout.Label("Biomes: temperature and moisture over the land, Whittaker table (BiomeTable).");
            return;
        }

        Row(3);
        climateTab = GUILayout.Toolbar(climateTab, ClimateLabels, GUILayout.Height(30f));
        Row(2);
        if (climateTab == 0)
        {
            baseTemperature = R2(Slider("Base", baseTemperature, 0f, 1f, "0.00"));
            lapseRate = R2(Slider("Altitude", lapseRate, 0f, 1f, "0.00"));
            latitudeEffect = R2(Slider("Latitude", latitudeEffect, 0f, 1f, "0.00"));
            coastModeration = R2(Slider("Coast", coastModeration, 0f, 0.5f, "0.00"));
            tempNoiseAmplitude = R2(Slider("Noise", tempNoiseAmplitude, 0f, 0.3f, "0.00"));
        }
        else if (climateTab == 1)
        {
            coastalMoistureBonus = R2(Slider("Coast", coastalMoistureBonus, 0f, 1f, "0.00"));
            coastDecayRate = R2(Slider("Decay", coastDecayRate, 0f, 1f, "0.00"));
            moistureNoiseAmplitude = R2(Slider("Noise", moistureNoiseAmplitude, 0f, 1f, "0.00"));
        }
        else
        {
            // One row for on/off and the direction; the arrow on the map shows it too.
            GUILayout.BeginHorizontal(GUILayout.Height(30f));
            wind = GUILayout.Toggle(wind, " Wind", GUILayout.Width(110f));
            GUI.enabled = wind;
            windAngle = Mathf.Round(GUILayout.HorizontalSlider(windAngle, 0f, 360f, GUILayout.ExpandWidth(true)) / 15f) * 15f % 360f;
            GUILayout.Label(WindName(), GUILayout.Width(70f));
            GUILayout.EndHorizontal();
            windStrength = R2(Slider("Weight", windStrength, 0f, 1f, "0.00"));
            windLift = R2(Slider("Lift", windLift, 0f, 1f, "0.00"));
            windDry = Mathf.Round(Slider("Dry", windDry, 0f, 0.05f, "0.000") * 1000f) / 1000f;
            windEvaporation = Mathf.Round(Slider("Sea", windEvaporation, 0f, 0.2f, "0.000") * 1000f) / 1000f;
            GUI.enabled = true;
            return; // no room for the legend under five sliders on a phone
        }

        // Whittaker legend: rows hot (top) → cold, columns dry → wet. Next to it, the
        // three biomes that cover most of the land on screen.
        GUILayout.BeginHorizontal();
        Rect grid = GUILayoutUtility.GetRect(96f, 56f, GUILayout.Width(96f), GUILayout.Height(56f));
        float cw = grid.width / BiomeTable.MoistureBands, ch = grid.height / BiomeTable.TemperatureBands;
        for (int t = 0; t < BiomeTable.TemperatureBands; t++)
            for (int m = 0; m < BiomeTable.MoistureBands; m++)
            {
                var bt = BiomeTable.WhittakerTable[t * BiomeTable.MoistureBands + m];
                DrawRect(new Rect(grid.x + m * cw, grid.yMax - (t + 1) * ch, cw - 1f, ch - 1f), BiomeColours[(int)bt]);
            }
        GUILayout.Label(TopBiomes());
        GUILayout.EndHorizontal();
    }

    static float R2(float v) => Mathf.Round(v * 100f) / 100f;

    string TopBiomes()
    {
        int total = 0;
        foreach (int c in biomeCount) total += c;
        if (total == 0) return "(no land on screen, or Layer is not Map)";
        var order = new int[biomeCount.Length];
        for (int i = 0; i < order.Length; i++) order[i] = i;
        System.Array.Sort(order, (a, b) => biomeCount[b].CompareTo(biomeCount[a]));
        var sb = new System.Text.StringBuilder();
        for (int k = 0; k < 3 && biomeCount[order[k]] > 0; k++)
            sb.AppendLine($"{BiomeTable.Definitions[order[k]].displayName} {100f * biomeCount[order[k]] / total:0}%");
        return sb.ToString();
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
            $"last sample {sampleMs:0.0} ms at {sampleN}×{sampleN}, {ActiveFieldCount()} field(s)   " +
            $"(last {PerfFrames} frames: {samples} samples, avg {(samples > 0 ? sumSample / samples : 0f):0.0}, max {maxSample:0.0} ms)\n" +
            $"world reference {calibrateMs:0} ms ({WorldSamples}×{WorldSamples}, only when a height setting changes)");

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
        if (colouring == Colouring.Biomes && wind) DrawWindArrow(new Vector2(left + side - 34f, top + 34f));

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

    // Arrow in the corner of the map: where the wind blows toward. The rotation is built
    // on top of the virtual-screen scale (GUIUtility.RotateAroundPivot ignores that scale).
    void DrawWindArrow(Vector2 centre)
    {
        DrawRect(new Rect(centre.x - 26f, centre.y - 26f, 52f, 52f), new Color(0f, 0f, 0f, 0.45f));
        Matrix4x4 baseMatrix = GUI.matrix;
        void Rotate(float degrees, Vector2 pivot) =>
            GUI.matrix = baseMatrix * Matrix4x4.TRS(pivot, Quaternion.Euler(0f, 0f, degrees), Vector3.one) *
                         Matrix4x4.TRS(-pivot, Quaternion.identity, Vector3.one);

        float guiAngle = -windAngle; // GUI y grows down
        Rotate(guiAngle, centre);
        DrawRect(new Rect(centre.x - 18f, centre.y - 2f, 36f, 4f), Color.white);
        var tip = new Vector2(centre.x + 18f, centre.y);
        foreach (float side in new[] { 150f, -150f })
        {
            Rotate(guiAngle, centre);
            GUI.matrix = GUI.matrix * Matrix4x4.TRS(tip, Quaternion.Euler(0f, 0f, side), Vector3.one) *
                         Matrix4x4.TRS(-tip, Quaternion.identity, Vector3.one);
            DrawRect(new Rect(tip.x, tip.y - 2f, 14f, 4f), Color.white);
        }
        GUI.matrix = baseMatrix;
        GUI.Label(new Rect(centre.x - 26f, centre.y + 26f, 80f, 20f), WindName());
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
