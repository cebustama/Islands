using System.Globalization;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
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
    // v6 (continents): float. Integers sample exactly as before; below 1 (down to 0.05) the
    // shapes span several views. See SampleInto.
    [SerializeField, Range(MinFrequency, MaxFrequency)] float frequency = 8;
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
        [Range(MinFrequency, MaxFrequency)] public float frequency;
        [Range(1, 6)] public int octaves;
        [Range(2, 4)] public int lacunarity;
        [Range(0f, 1f)] public float gain;

        public NoiseField(NoiseType type, float frequency, int octaves, int lacunarity, float gain)
        {
            this.type = type;
            this.frequency = frequency;
            this.octaves = octaves;
            this.lacunarity = lacunarity;
            this.gain = gain;
        }
    }

    const float MinFrequency = 0.05f, MaxFrequency = 32f;

    // Below 1: hundredths (0.05 … 1); from 1 up: integers, as before.
    static float SnapFrequency(float f) =>
        f >= 1f ? Mathf.Clamp(Mathf.Round(f), 1f, MaxFrequency)
                : Mathf.Clamp(Mathf.Round(f * 100f) / 100f, MinFrequency, 1f);

    static bool IsWholeFrequency(float f) => f >= 1f && f == Mathf.Round(f);

    const int FieldCount = 6;
    const int FHeight = 0, FCont = 1, FEros = 2, FPeaks = 3, FTemp = 4, FMoist = 5;
    static readonly string[] FieldNames = { "Height", "Continent", "Erosion", "Peaks", "Temp", "Moist" };

    static NoiseField[] DefaultExtraFields() => new[]
    {
        new NoiseField(NoiseType.SimplexPerlinOnSimplex, 2, 4, 2, 0.5f), // continentalness: big shapes
        new NoiseField(NoiseType.SimplexPerlinOnSimplex, 3, 3, 2, 0.5f), // erosion: where relief is allowed
        new NoiseField(NoiseType.SimplexPerlinOnSimplex, 8, 4, 2, 0.5f), // peaks and valleys: detail
        // Temperature and moisture use 4 octaves: with 2 the field is almost a smooth slope at
        // view scale, and the biome table cuts it into long, nearly straight bands.
        new NoiseField(NoiseType.SimplexPerlinOnSimplex, 2, 4, 2, 0.5f), // temperature noise
        new NoiseField(NoiseType.SimplexPerlinOnSimplex, 3, 4, 2, 0.5f), // moisture noise
    };

    [SerializeField] NoiseField[] extraFields = DefaultExtraFields(); // fields 1..5

    // Height: one noise (v2), or three noises chained through splines.
    public enum HeightSource { OneNoise, Splines }
    [SerializeField] HeightSource heightSource = HeightSource.Splines; // v4: default on start (codes without h= still mean one noise)

    // Land colour: percentage bands (v2), or biomes from temperature and moisture.
    public enum Colouring { Bands, Biomes }
    [SerializeField] Colouring colouring = Colouring.Biomes;     // v4: default on start (codes without k= still mean bands)

    [SerializeField] bool shade = false;

    // Contour lines every contourStep of raw height (absolute, unlike the bands, which are
    // percentages). Every fifth line is darker. v4: turned on as an overlay layer.
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

    // v6 (step 1a): wind model. Sweep = the v3 sweep in one direction. Flow = the same
    // background wind, made to go around the high land (a pressure projection, see Flow).
    public enum WindModel { Sweep, Flow }
    [SerializeField] WindModel windModel = WindModel.Sweep;
    [SerializeField, Range(0f, 1f)] float solidAbove = 0.5f;             // land rank above which the air goes around
    [SerializeField, Range(10, 600)] int flowIterations = 200;           // red-black SOR passes (red + black)
    [SerializeField] bool ocean = false;                                 // v6 (2a): ocean currents
    [SerializeField, Range(0.1f, 1f)] float oceanWidth = 0.25f;          // western current width, view widths
    [SerializeField, Range(0.25f, 8f)] float heatMemory = 2f;            // 2b: how far the sea carries its temperature, view widths at the interior speed
    [SerializeField, Range(0f, 1f)] float upwelling = 0.5f;              // 2b step 3: strength of the coastal upwelling

    const float LatitudeSpan = 8f;   // view widths from the equator to a pole
    const float CoastUnits = 256f;   // coast distance unit: one cell of a 256 map

    const int WorldSamples = 128;   // reference grid, WorldSamples² points
    const float WorldSpan = 16f;    // ... spread over 16 × 16 view widths around the origin

    const int Bins = 64; // histogram bins over [-1, 1]

    float[] values, sorted;
    float threshold, waterFraction;
    // v4: split view. 1, 2 or 4 panels share the samples, the cuts and the climate; each
    // panel only paints its own layer into its own texture.
    const int MaxPanels = 4;
    readonly Texture2D[] textures = new Texture2D[MaxPanels];
    readonly Color32[][] panelPixels = new Color32[MaxPanels][];
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
    readonly float[] logPaint = new float[LogFrames], logCloud = new float[LogFrames]; // v4 (export v2)
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
        // v6 UI: this device's default map (Code > Set as default) over the scene's values.
        string start = PlayerPrefs.GetString(DefaultKey, "");
        if (start.Length > 0 && ApplyCode(start) != null) PlayerPrefs.DeleteKey(DefaultKey); // unreadable: forget it
        var cam = Camera.main;
        if (cam != null)
        {
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = background;
        }
    }

    void Update()
    {
        float sampledMs = 0f, paintedMs = 0f, cloudedMs = 0f;
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
            paintedMs = paintMs;
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
            paintedMs = paintMs;
        }

        // 2b: a slider inside the ×4 / ×16 picture moved: redraw it (once per frame at most).
        if (bigPicture != null && bigDirty)
        {
            bigDirty = false;
            MakeBigPicture(bigScale);
        }

        // v4: the clouds move every frame; the terrain is not touched.
        if (clouds)
        {
            Clouds();
            cloudedMs = cloudMs;
        }

        perfFrame[perfHead] = Time.unscaledDeltaTime * 1000f;
        perfSample[perfHead] = sampledMs;
        perfHead = (perfHead + 1) % PerfFrames;

        if (logCount == 0) logStart = Time.unscaledTime;
        logTime[logHead] = Time.unscaledTime - logStart;
        logFrame[logHead] = Time.unscaledDeltaTime * 1000f;
        logSample[logHead] = sampledMs;
        logPaint[logHead] = paintedMs;
        logCloud[logHead] = cloudedMs;
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
        var paints = new System.Collections.Generic.List<float>();
        var cloudTimes = new System.Collections.Generic.List<float>();
        int first = (logHead - logCount + LogFrames) % LogFrames;
        for (int k = 0; k < logCount; k++)
        {
            int i = (first + k) % LogFrames;
            frames.Add(logFrame[i]);
            if (logDrag[i]) dragFrames.Add(logFrame[i]);
            if (logSample[i] > 0f) (logDrag[i] ? dragSamples : fullSamples).Add(logSample[i]);
            if (logPaint[i] > 0f) paints.Add(logPaint[i]);
            if (logCloud[i] > 0f) cloudTimes.Add(logCloud[i]);
        }

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("# NoiseLab perf export v2");
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
        sb.AppendLine($"resolution: {resolution} | drag resolution: {dragResolution} | world reference: {WorldSamples}x{WorldSamples} over {F(R1(worldSpan))} views, last {F(R1(calibrateMs))} ms");
        sb.AppendLine($"fields sampled per view: {ActiveFieldCount()} (height: {heightSource}, colour: {colouring})");
        sb.AppendLine($"panels: {viewCount} ({ViewCode()}) | overlays: {overlays.Count} | clouds: {(clouds ? CloudRes + "x" + CloudRes + ", " + GridNoiseJob<Simplex2D<Simplex>>.StateName(cloudBurstState) : "off")}");
        sb.AppendLine($"climate margin: {(climateMargin ? "on" : "off")} | cells L/R/B/T {mL}/{mR}/{mB}/{mT} at {sampleN} | climate grid {extW}x{extH} | last climate {F(R1(climateMs))} ms, {GridNoiseJob<Simplex2D<Simplex>>.StateName(climateBurstState)}");
        if (FlowOn) sb.AppendLine($"flow: {FlowSummary()}");
        if (colouring == Colouring.Biomes && ocean) sb.AppendLine($"ocean: {OceanSummary()}");
        if (edgeStatus.Length > 0) sb.AppendLine($"edge test: {edgeStatus.Replace("\n", " | ")}");
        sb.AppendLine($"burst: {(useBurst ? "on" : "off")} | last sampling {GridNoiseJob<Simplex2D<Simplex>>.StateName(burstState)} | jobs workers {Unity.Jobs.LowLevel.Unsafe.JobsUtility.JobWorkerCount}");
        if (burstStatus.Length > 0) sb.AppendLine($"burst check: {burstStatus}");
        sb.AppendLine($"code: {BuildCode()}");
        sb.AppendLine();
        sb.AppendLine($"## summary ({logCount} frames, {F(R1(logCount > 0 ? logTime[(logHead - 1 + LogFrames) % LogFrames] : 0f))} s)");
        sb.AppendLine("set | count | avg ms | p50 | p95 | max");
        sb.AppendLine(StatsRow("frame (all)", frames));
        sb.AppendLine(StatsRow("frame (dragging)", dragFrames));
        sb.AppendLine(StatsRow("sample (dragging)", dragSamples));
        sb.AppendLine(StatsRow("sample (full, on release or change)", fullSamples));
        sb.AppendLine(StatsRow("paint (all panels, when repainted)", paints));
        sb.AppendLine(StatsRow("clouds (every frame while on)", cloudTimes));
        sb.AppendLine($"dragging frames under 33.3 ms (30 fps): {Share(dragFrames, 33.3f)} | under 16.7 ms (60 fps): {Share(dragFrames, 16.7f)}");
        sb.AppendLine();
        sb.AppendLine("## frames");
        sb.AppendLine("t_s,frame_ms,sample_ms,n,drag,paint_ms,cloud_ms");
        for (int k = 0; k < logCount; k++)
        {
            int i = (first + k) % LogFrames;
            sb.Append(logTime[i].ToString("0.000", Inv)).Append(',')
              .Append(logFrame[i].ToString("0.0", Inv)).Append(',')
              .Append(logSample[i].ToString("0.0", Inv)).Append(',')
              .Append(logN[i]).Append(',')
              .Append(logDrag[i] ? 1 : 0).Append(',')
              .Append(logPaint[i].ToString("0.0", Inv)).Append(',')
              .Append(logCloud[i].ToString("0.0", Inv)).AppendLine();
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

    // v6 (continents): the world reference spans 16 views, or about four wavelengths of the
    // lowest height frequency when that is larger (C at 0.1 → 40 views), with the same 128²
    // samples, so the water percentage is measured over several continents and not over two.
    // With whole frequencies it stays at 16 (same cuts as before).
    float worldSpan = WorldSpan;

    float WorldSpanFor(int[] fields)
    {
        float min = MaxFrequency;
        foreach (int k in fields) min = Mathf.Min(min, GetField(k).frequency);
        return Mathf.Max(WorldSpan, 4f / min);
    }

    float[] CutSource => cutsFrom == CutsFrom.World ? worldSorted : sorted;

    // The world reference: same noise(s), same settings, sampled on a fixed grid that
    // ignores the offset. Noises are resampled only when their settings change; editing a
    // spline only recombines and sorts (16 384 values).
    void Calibrate()
    {
        if (cutsFrom != CutsFrom.World) return;
        string key = HeightKey();
        if (key == worldKey && worldSorted != null) return;
        worldKey = key;

        var watch = System.Diagnostics.Stopwatch.StartNew();
        float span = worldSpan = WorldSpanFor(HeightFields());
        float step = span / WorldSamples;
        float origin = -0.5f * span + 0.5f * step;
        foreach (int k in HeightFields())
        {
            string fk = $"{FieldKey(k)}|{F(span)}";
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

    // Everything the height depends on (noises and curves), as text: same key, same heights.
    string HeightKey() => heightSource == HeightSource.OneNoise
        ? FieldKey(FHeight)
        : $"S|{FieldKey(FCont)}|{FieldKey(FEros)}|{FieldKey(FPeaks)}|{CurvesCode()}";

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
        foreach (var t in textures) if (t != null) Destroy(t);
        if (cloudTexture != null) Destroy(cloudTexture);
        if (cloudNoise.IsCreated) cloudNoise.Dispose();
        if (cloudState.IsCreated) cloudState.Dispose();
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
        return $"{f.type}|{FieldSeed(k)}|{F(f.frequency)}|{f.octaves}|{f.lacunarity}|{F(f.gain)}";
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
        SampleMargins(n);
        sampleMs = (float)watch.Elapsed.TotalMilliseconds;
    }

    // ---------------------------------------------------------------------------------
    // v5: climate beyond the view. Wind and coast depend on what lies outside the view:
    // without a margin the air enters the upwind edge saturated and the water beyond the
    // edges is not seen, so the rain shadow and the coastal moisture change when dragging.
    // The climate runs on an extended grid, extW × extH: the view plus a margin on each
    // side, at the same cell size. The view's own samples are copied in unchanged (the
    // view is bit-identical with or without margin); only the margins are sampled, as four
    // strips. Only the height fields are needed there.
    //
    // How wide. The air reaching the view from a cell M steps upwind differs from the true
    // air by at most the difference at that cell times what it keeps on the way: per cell,
    // e^−dry over land and e^−sea over water (the deficit 1 − air shrinks by that factor).
    // So with M = 3 / min(dry, sea) cells the error is at most e^−3 ≈ 5 %. The coast: if
    // the nearest water is outside the grid, the true distance is at least the margin C,
    // so the error in the coastal moisture is at most bonus / (1 + C · decay): 5 % with
    // C = 19 / decay (and 1 / (1 + C) for the temperature, 5 % at C = 19). Both have a cap.
    // Distances in cells of a 256 map, as the climate sliders.
    // ---------------------------------------------------------------------------------

    [SerializeField] bool climateMargin = true;

    // v5 (B1): sampling with Burst (GridNoiseJob, below the class). burstState: what the
    // last sampling did (GridNoiseJob.Off / Compiled / Managed).
    [SerializeField] bool useBurst = true;
    int burstState = -1;
    string burstStatus = "";

    // Forget the cached samples, so the next sampling runs again (after Burst on/off).
    void ForgetSamples()
    {
        for (int k = 0; k < FieldCount; k++) viewFieldKey[k] = extFieldKey[k] = worldFieldKey[k] = null;
        worldKey = null;
        dirty = true;
    }

    // Samples every active field of the view at full resolution twice, managed and with
    // Burst, into scratch arrays (the caches are not touched), and compares them.
    void BurstCheck()
    {
        int n = resolution;
        float step = 1f / n;
        var a = new float[n * n];
        var b = new float[n * n];
        double managedMs = 0, burstMs = 0;
        int differ = 0, total = 0;
        float worst = 0f;
        int state = -1;
        foreach (int k in ActiveFields())
        {
            bool keep = useBurst;
            var watch = System.Diagnostics.Stopwatch.StartNew();
            useBurst = false;
            SampleInto(a, GetField(k), FieldSeed(k), n, offset.x + 0.5f * step, offset.y + 0.5f * step, step);
            managedMs += watch.Elapsed.TotalMilliseconds;
            watch.Restart();
            useBurst = true;
            SampleInto(b, GetField(k), FieldSeed(k), n, offset.x + 0.5f * step, offset.y + 0.5f * step, step);
            burstMs += watch.Elapsed.TotalMilliseconds;
            state = burstState;
            useBurst = keep;
            for (int i = 0; i < a.Length; i++)
            {
                float d = Mathf.Abs(a[i] - b[i]);
                if (d > 0f) differ++;
                worst = Mathf.Max(worst, d);
            }
            total += a.Length;
        }
        burstStatus = $"Burst check at {n}², {ActiveFieldCount()} field(s): managed {managedMs:0} ms, Burst {burstMs:0} ms " +
                      $"({(burstMs > 0 ? managedMs / burstMs : 0):0.0}×, {GridNoiseJob<Simplex2D<Simplex>>.StateName(state)}); " +
                      $"values that differ {differ} of {total}, max |Δ| {worst:G3}\n" + ClimateCheck() + FlowCheck() + OceanCheck();
        dirty = true; // repaint with the path in use
        Debug.Log($"[NoiseLab] {burstStatus}");
    }

    const float MarginLoss = 3f;          // e^−3 ≈ 5 % left of the edge's error
    const float WindMarginCap = 384f;     // 1.5 view widths
    const float CoastMarginMin = 19f;     // 1 / (1 + 19) = 5 % of the coast moderation
    const float CoastMarginCap = 128f;    // 0.5 view widths

    int mL, mR, mB, mT, extW, extH;       // margins in cells of the current grid
    float[] extValues, extRank, extCoast, extAir, stripBuffer;
    readonly float[][] extField = new float[FieldCount][];
    readonly string[] extFieldKey = new string[FieldCount];

    void Margins(int n)
    {
        mL = mR = mB = mT = 0;
        if (climateMargin && colouring == Colouring.Biomes)
        {
            float c = Mathf.Min(CoastMarginCap, CoastMarginMin / Mathf.Max(coastDecayRate, 1e-6f));
            float left = c, right = c, bottom = c, top = c;
            if (wind)
            {
                float rate = Mathf.Min(windDry, windEvaporation);
                float m = rate > 0f ? Mathf.Min(WindMarginCap, MarginLoss / rate) : WindMarginCap;
                float a = windAngle * Mathf.Deg2Rad;
                float wx = Mathf.Cos(a), wy = Mathf.Sin(a); // where it blows toward: the air comes from the other side
                if (wx > 0f) left = Mathf.Max(left, m * wx); else right = Mathf.Max(right, -m * wx);
                if (wy > 0f) bottom = Mathf.Max(bottom, m * wy); else top = Mathf.Max(top, -m * wy);
            }
            float toN = n / CoastUnits;
            mL = Mathf.CeilToInt(left * toN);
            mR = Mathf.CeilToInt(right * toN);
            mB = Mathf.CeilToInt(bottom * toN);
            mT = Mathf.CeilToInt(top * toN);
        }
        extW = mL + n + mR;
        extH = mB + n + mT;
    }

    void SampleMargins(int n)
    {
        Margins(n);
        if (colouring != Colouring.Biomes) return;
        if (mL + mR + mB + mT == 0)
        {
            extValues = values; // no margin: the climate grid is the view
            return;
        }

        float step = 1f / n;
        foreach (int k in HeightFields())
        {
            string key = $"{FieldKey(k)}|{n}|{F(offset.x)}|{F(offset.y)}|{mL},{mR},{mB},{mT}";
            if (extFieldKey[k] == key && extField[k] != null) continue;
            float[] ext = extField[k] = Ensure(extField[k], extW * extH);
            float[] view = viewField[k];
            for (int y = 0; y < n; y++)
                System.Array.Copy(view, y * n, ext, (y + mB) * extW + mL, n);
            Strip(k, ext, 0, 0, extW, mB, step);            // bottom, corners included
            Strip(k, ext, 0, mB + n, extW, mT, step);       // top, corners included
            Strip(k, ext, 0, mB, mL, n, step);              // left
            Strip(k, ext, mL + n, mB, mR, n, step);         // right
            extFieldKey[k] = key;
        }
        if (extValues == values) extValues = null;
        extValues = Ensure(extValues, extW * extH);
        CombineHeight(extValues, extField);
    }

    // Samples the w × h block of the extended grid whose lower-left cell is (x0, y0).
    // Extended cell (ex, ey) is view cell (ex − mL, ey − mB).
    void Strip(int k, float[] ext, int x0, int y0, int w, int h, float step)
    {
        if (w <= 0 || h <= 0) return;
        if (stripBuffer == null || stripBuffer.Length < w * h) stripBuffer = new float[w * h];
        SampleInto(stripBuffer, GetField(k), FieldSeed(k), w, h,
                   offset.x + (x0 - mL + 0.5f) * step, offset.y + (y0 - mB + 0.5f) * step, step);
        for (int y = 0; y < h; y++)
            System.Array.Copy(stripBuffer, y * w, ext, (y0 + y) * extW + x0, w);
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

    void SampleInto(float[] target, NoiseField field, int fieldSeed, int n, float originX, float originZ, float step) =>
        SampleInto(target, field, fieldSeed, n, n, originX, originZ, step);

    // v5: a w × h rectangle (the climate margins).
    void SampleInto(float[] target, NoiseField field, int fieldSeed, int w, int h, float originX, float originZ, float step)
    {
        var settings = Settings.Default;
        settings.seed = fieldSeed;
        // v6 (continents): the library's frequency is an integer >= 1 and only multiplies the
        // coordinates (Lattice and Simplex: positions *= frequency, no tiling). A whole frequency
        // goes through unchanged, so existing codes give the same bits. A fractional one samples
        // at frequency 1 with the coordinates scaled by f instead, which is the same noise
        // stretched 1/f times. Runtime/ stays untouched.
        if (IsWholeFrequency(field.frequency))
            settings.frequency = (int)field.frequency;
        else
        {
            settings.frequency = 1;
            originX *= field.frequency;
            originZ *= field.frequency;
            step *= field.frequency;
        }
        settings.octaves = field.octaves;
        settings.lacunarity = field.lacunarity;
        settings.persistence = field.gain;

        switch (field.type)
        {
            case NoiseType.Value: burstState = GridNoiseJob<Lattice2D<LatticeNormal, Value>>.Fill(useBurst, settings, target, w, h, originX, originZ, step); break;
            case NoiseType.PerlinSquare: burstState = GridNoiseJob<Lattice2D<LatticeNormal, Perlin>>.Fill(useBurst, settings, target, w, h, originX, originZ, step); break;
            case NoiseType.SimplexOldGradient: burstState = GridNoiseJob<Simplex2D<Perlin>>.Fill(useBurst, settings, target, w, h, originX, originZ, step); break;
            case NoiseType.SimplexPerlinOnSimplex: burstState = GridNoiseJob<Simplex2D<PerlinOnSimplex>>.Fill(useBurst, settings, target, w, h, originX, originZ, step); break;
            case NoiseType.SimplexSimplex: burstState = GridNoiseJob<Simplex2D<Simplex>>.Fill(useBurst, settings, target, w, h, originX, originZ, step); break;
            case NoiseType.VoronoiF1: burstState = GridNoiseJob<Voronoi2D<LatticeNormal, Worley, F1>>.Fill(useBurst, settings, target, w, h, originX, originZ, step); break;
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
        // v6 (2a): its own world grid, cached. 2c: first, because the climate reads the sea.
        if (ocean) Ocean(); else extSeaAnom = null;
        var watch = System.Diagnostics.Stopwatch.StartNew();
        if (useBurst) ClimateBurst(); else ClimateManaged();
        climateMs = (float)watch.Elapsed.TotalMilliseconds; // v6 1b: includes the flow and its air
        ViewMeans();
        SeaCoastStats();
    }

    // v6 UI: mean temperature and moisture of the land in the view (0–1, the values the
    // biome table reads), shown next to the latitude.
    float viewTemp, viewMoist;
    int viewLand;

    void ViewMeans()
    {
        double t = 0.0, m = 0.0;
        int land = 0;
        for (int i = 0; i < values.Length; i++)
        {
            if (values[i] < threshold) continue;
            t += temp[i];
            m += moist[i];
            land++;
        }
        viewLand = land;
        viewTemp = land > 0 ? (float)(t / land) : 0f;
        viewMoist = land > 0 ? (float)(m / land) : 0f;
    }

    // The climate in plain C#, one thread (v3–v5 step 1). Kept as the Burst-off path and
    // as the reference for the Burst check.
    void ClimateManaged()
    {
        climateBurstState = GridNoiseJob<Simplex2D<Simplex>>.Off;
        int n = sampleN, count = n * n;
        int w = extW, h = extH, extCount = w * h;
        coast = Ensure(coast, count);
        temp = Ensure(temp, count);
        moist = Ensure(moist, count);

        // v5: coast distance and land rank on the extended grid (view + margins), then the
        // view's part is read at (x + mL, y + mB).
        extCoast = Ensure(extCoast, extCount);
        CoastDistance(extValues, extCoast, w, h);
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
                coast[y * n + x] = extCoast[(y + mB) * w + x + mL];
        float toUnits = CoastUnits / n;

        float[] source = CutSource;
        int below = LowerBound(source, threshold);
        int landCount = source.Length - below;
        float[] tn = viewField[FTemp], mn = viewField[FMoist];

        // Land rank: 0 at the coast, 1 at the highest land of the cut source.
        extRank = Ensure(extRank, extCount);
        for (int i = 0; i < extCount; i++)
            extRank[i] = landCount > 0
                ? math.saturate((float)(LowerBound(source, extValues[i]) - below) / landCount)
                : 0f;

        for (int y = 0; y < n; y++)
        {
            float yWorld = offset.y + (y + 0.5f) / n;
            float lat = math.saturate(math.abs(yWorld) / LatitudeSpan);
            for (int x = 0; x < n; x++)
            {
                int i = y * n + x;
                float rank = extRank[(y + mB) * w + x + mL];
                float cd = coast[i] * toUnits;

                float t = baseTemperature
                        - lapseRate * rank
                        - latitudeEffect * lat
                        + coastModeration / (1f + cd)
                        + tempNoiseAmplitude * 0.5f * tn[i];        // amplitude · (noise01 − 0.5)
                temp[i] = math.saturate(t);
            }
        }

        if (wind)
        {
            if (FlowOn) Flow(false); // v6 1b: the flow, and the air that follows it
            else WindField(w, h);
            windAir = Ensure(windAir, count);
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                    windAir[y * n + x] = extAir[(y + mB) * w + x + mL];
        }

        for (int i = 0; i < count; i++)
        {
            float cd = coast[i] * toUnits;
            float m = moistureNoiseAmplitude * math.saturate(mn[i] * 0.5f + 0.5f)
                    + coastalMoistureBonus / (1f + cd * coastDecayRate)
                    + (wind ? windStrength * windAir[i] : 0f);
            moist[i] = math.saturate(m);
        }
    }

    float climateMs;
    int climateBurstState = -1;

    // ---------------------------------------------------------------------------------
    // v5 (B2): the same climate as four Burst jobs, chained by their dependencies:
    //   rank (parallel by row) ─┐
    //   coast columns → coast rows (parallel) ─┴→ wind (one thread) → view climate (parallel)
    // The wind sweep runs on one thread: each line needs the line before it, and a barrier
    // per line would cost more than it saves; Burst alone makes it fast. The managed
    // arrays are copied in and out of NativeArrays (a few hundred KB, well under 1 ms).
    // The wind uses math.exp and math.lerp in float, where the managed code goes through
    // Mathf (Exp in double): the last digits may differ (Perf → Burst check measures it).
    // ---------------------------------------------------------------------------------
    void ClimateBurst()
    {
        int n = sampleN, count = n * n;
        int w = extW, h = extH, extCount = w * h;
        coast = Ensure(coast, count);
        temp = Ensure(temp, count);
        moist = Ensure(moist, count);
        extCoast = Ensure(extCoast, extCount);
        extRank = Ensure(extRank, extCount);

        float[] source = CutSource;
        int below = LowerBound(source, threshold);

        var heights = new NativeArray<float>(extValues, Allocator.TempJob);
        var sorted = new NativeArray<float>(source, Allocator.TempJob);
        var rank = new NativeArray<float>(extCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
        var dist = new NativeArray<float>(extCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
        var air = new NativeArray<float>(wind ? extCount : 1, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
        var tn = new NativeArray<float>(viewField[FTemp], Allocator.TempJob);
        var mn = new NativeArray<float>(viewField[FMoist], Allocator.TempJob);
        var coastV = new NativeArray<float>(count, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
        var tempV = new NativeArray<float>(count, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
        var moistV = new NativeArray<float>(count, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
        var airV = new NativeArray<float>(wind ? count : 1, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
        // 1 if a job ran without Burst. The rank job runs at the same time as the coast jobs,
        // so it gets its own (two parallel jobs may not write the same array).
        var probe = new NativeArray<int>(1, Allocator.TempJob);
        var rankProbe = new NativeArray<int>(1, Allocator.TempJob);
        try
        {
            var hRank = new RankJob
            {
                heights = heights, sorted = sorted, rank = rank, w = w,
                below = below, landCount = sorted.Length - below, probe = rankProbe
            }.ScheduleParallel(h, 8, default);
            var hCols = new CoastColumnsJob
            {
                heights = heights, dist = dist, w = w, h = h, threshold = threshold, probe = probe
            }.ScheduleParallel(w, 8, default);
            var hRows = new CoastRowsJob { dist = dist, w = w, probe = probe }.ScheduleParallel(h, 8, hCols);
            var deps = JobHandle.CombineDependencies(hRank, hRows);

            if (wind && FlowOn)
            {
                // v6 1b: the flow needs the rank, and the air needs the flow. The flow and the
                // air are their own jobs (Flow, FlowAir), so the chain stops here and goes on.
                deps.Complete();
                rank.CopyTo(extRank);
                Flow(true);
                air.CopyFrom(extAir);
            }
            else if (wind)
            {
                float a = windAngle * Mathf.Deg2Rad;
                float wx = Mathf.Cos(a), wy = Mathf.Sin(a);
                bool xMajor = Mathf.Abs(wx) >= Mathf.Abs(wy);
                float major = xMajor ? wx : wy, minor = xMajor ? wy : wx;
                float slope = minor / Mathf.Abs(major);
                float stepLen = Mathf.Sqrt(1f + slope * slope) * CoastUnits / n;
                deps = new WindJob
                {
                    heights = heights, rank = rank, air = air, w = w, h = h, threshold = threshold,
                    xMajor = xMajor, dir = major >= 0f ? 1 : -1, slope = slope,
                    keepFlat = Mathf.Exp(-windDry * stepLen), pickUp = 1f - Mathf.Exp(-windEvaporation * stepLen),
                    lift = windLift * LiftScale, probe = probe
                }.Schedule(deps);
            }

            new ViewClimateJob
            {
                extCoast = dist, extRank = rank, extAir = air, tn = tn, mn = mn,
                coast = coastV, temp = tempV, moist = moistV, air = airV,
                n = n, w = w, mL = mL, mB = mB, wind = wind, toUnits = CoastUnits / n,
                offsetY = offset.y, latitudeSpan = LatitudeSpan, span = 1f,
                baseTemperature = baseTemperature, lapseRate = lapseRate, latitudeEffect = latitudeEffect,
                coastModeration = coastModeration, tempNoiseAmplitude = tempNoiseAmplitude,
                moistureNoiseAmplitude = moistureNoiseAmplitude, coastalMoistureBonus = coastalMoistureBonus,
                coastDecayRate = coastDecayRate, windStrength = windStrength, probe = probe
            }.ScheduleParallel(n, 8, deps).Complete();

            dist.CopyTo(extCoast);
            rank.CopyTo(extRank);
            coastV.CopyTo(coast);
            tempV.CopyTo(temp);
            moistV.CopyTo(moist);
            if (wind)
            {
                extAir = Ensure(extAir, extCount);
                air.CopyTo(extAir);
                windAir = Ensure(windAir, count);
                airV.CopyTo(windAir);
            }
            climateBurstState = probe[0] != 0 || rankProbe[0] != 0 ? GridNoiseJob<Simplex2D<Simplex>>.Managed : GridNoiseJob<Simplex2D<Simplex>>.Compiled;
        }
        finally
        {
            heights.Dispose(); sorted.Dispose(); rank.Dispose(); dist.Dispose(); air.Dispose();
            tn.Dispose(); mn.Dispose(); coastV.Dispose(); tempV.Dispose(); moistV.Dispose(); airV.Dispose();
            probe.Dispose(); rankProbe.Dispose();
        }
    }

    // Burst check, climate part: the current view's climate in plain C# and with Burst,
    // compared array by array. Leaves the result of the path in use.
    string ClimateCheck()
    {
        if (colouring != Colouring.Biomes) return "climate: choose Biomes colouring to check it";
        var watch = System.Diagnostics.Stopwatch.StartNew();
        ClimateManaged();
        double managedMs = watch.Elapsed.TotalMilliseconds;
        var c0 = (float[])coast.Clone();
        var t0 = (float[])temp.Clone();
        var m0 = (float[])moist.Clone();
        var a0 = wind ? (float[])windAir.Clone() : null;
        watch.Restart();
        ClimateBurst();
        double burstMs = watch.Elapsed.TotalMilliseconds;
        string state = GridNoiseJob<Simplex2D<Simplex>>.StateName(climateBurstState);
        float Worst(float[] x, float[] y)
        {
            float m = 0f;
            for (int i = 0; i < x.Length; i++) m = Mathf.Max(m, Mathf.Abs(x[i] - y[i]));
            return m;
        }
        int biomes = 0, land = 0;
        for (int i = 0; i < m0.Length; i++)
        {
            if (values[i] < threshold) continue;
            land++;
            if (BiomeTable.Lookup(t0[i], m0[i]) != BiomeTable.Lookup(temp[i], moist[i])) biomes++;
        }
        string result = $"climate at {sampleN}² (grid {extW}×{extH}): managed {managedMs:0.0} ms, Burst {burstMs:0.0} ms " +
                        $"({(burstMs > 0 ? managedMs / burstMs : 0):0.0}×, {state}); max |Δ| coast {Worst(c0, coast):G3}, " +
                        $"temp {Worst(t0, temp):G3}, moist {Worst(m0, moist):G3}" +
                        (wind ? $", air {Worst(a0, windAir):G3}" : "") +
                        $"; biome changed {(land > 0 ? 100f * biomes / land : 0f):0.000} % of {land} land cells";
        if (!useBurst) ClimateManaged();
        return result;
    }

    // ---------------------------------------------------------------------------------
    // Wind: one direction for the whole map. Air enters at the upwind edge of the view,
    // saturated, and moves cell by cell along the wind. Over water it picks up moisture;
    // over land it slowly dries, and much faster when it climbs (orographic rain): the
    // windward slope gets the rain, the lee gets dry air (rain shadow). windAir is the
    // humidity of the air passing each cell, 0–1.
    // The sweep goes line by line along the axis the wind moves most on; the air of a cell
    // comes from the previous line, interpolated at the point one step upwind. O(n²), any
    // angle. Air entering the grid is assumed to come from the sea. v5: the grid is the
    // view plus the upwind margin (SampleMargins), so that assumption is made far enough
    // upwind to fade before the air reaches the view.
    // ---------------------------------------------------------------------------------

    float[] windAir;

    const float LiftScale = 4f; // lift 1 = the air keeps e^−4 ≈ 2 % after climbing the whole land range

    void WindField(int w, int h)
    {
        extAir = Ensure(extAir, w * h);
        int n = w - mL - mR; // cells per view width
        float a = windAngle * Mathf.Deg2Rad;
        float wx = Mathf.Cos(a), wy = Mathf.Sin(a);      // direction the wind blows toward (y up)
        bool xMajor = Mathf.Abs(wx) >= Mathf.Abs(wy);
        float major = xMajor ? wx : wy, minor = xMajor ? wy : wx;
        int dir = major >= 0f ? 1 : -1;
        float slope = minor / Mathf.Abs(major);          // minor-axis shift per major step
        float dist = Mathf.Sqrt(1f + slope * slope) * CoastUnits / n; // step length, in cells of a 256 map
        float keepFlat = Mathf.Exp(-windDry * dist);
        float pickUp = 1f - Mathf.Exp(-windEvaporation * dist);

        int lu = xMajor ? w : h, lj = xMajor ? h : w;   // lengths along the major and minor axes
        int Cell(int u, int j) => xMajor ? j * w + u : u * w + j;

        int first = dir > 0 ? 0 : lu - 1;
        for (int k = 0; k < lu; k++)
        {
            int u = first + dir * k;
            for (int j = 0; j < lj; j++)
            {
                int i = Cell(u, j);
                float air, rankBefore;
                float pj = j - slope;                     // where this air was one step ago
                if (k == 0 || pj < 0f || pj > lj - 1)
                {
                    air = 1f;                             // enters the grid: sea air
                    rankBefore = extRank[i];
                }
                else
                {
                    int j0 = Mathf.Min((int)pj, lj - 2);
                    float f = pj - j0;
                    int prev = u - dir;
                    air = Mathf.Lerp(extAir[Cell(prev, j0)], extAir[Cell(prev, j0 + 1)], f);
                    rankBefore = Mathf.Lerp(extRank[Cell(prev, j0)], extRank[Cell(prev, j0 + 1)], f);
                }

                if (extValues[i] < threshold)
                    air += (1f - air) * pickUp;           // over the sea
                else
                {
                    float rise = Mathf.Max(0f, extRank[i] - rankBefore);
                    air *= keepFlat * Mathf.Exp(-windLift * LiftScale * rise);
                }
                extAir[i] = air;
            }
        }
    }

    // ---------------------------------------------------------------------------------
    // v6 (step 1a): wind that goes around the high land. Land whose rank is above
    // solidAbove is a wall: the air goes around it, and climbs what lies below (the
    // orographic rain of v3). One pressure projection, no time steps (the "single
    // projection" of the fluids design): the background wind on every face of a MAC grid,
    // zero on the faces that touch a wall, then the pressure that removes the divergence
    // this leaves (Poisson, red-black SOR, fixed passes, starting from 0, so the same map
    // always gives the same wind), and the faces minus the pressure gradient.
    // Grid: a quarter of the climate grid (one flow cell = 4 × 4 climate cells; 1a.3, it
    // was 2 × 2). The passes needed grow with the side and each pass with the cells, so
    // the cost grows with the cube of the side: a quarter costs ~8× less than a half for
    // the same quality (half: 400 passes ≈ 170 ms in the editor at 256²). The wind is
    // smooth anyway; the climate does not need more. Outside the grid the pressure is 0 (open
    // edges: the air leaves and enters freely). Walls are Neumann (Lague's 4-bit rule: sum
    // only over the open neighbours and divide by how many there are).
    // Why pressure and not a stream function ψ: with several islands, ψ needs one unknown
    // constant per island; the pressure does not.
    // 1b: the humidity follows this wind (FlowAirJob), on the flow grid (1b.2).
    // ---------------------------------------------------------------------------------

    // SOR weight: the optimum for a grid this size, 2 / (1 + sin(π / N)). 1a used Lague's 1.7,
    // which only reaches a few cells from each wall in 60 passes; on a 290×236 grid that left
    // more divergence than before the solve. Measured offline (1a.2): with the optimum, 300
    // passes leave ~1/4 of the starting divergence, 400 ~1/10. Too few passes with it is
    // worse than 1.7, hence the higher default.
    static float FlowOmega(int fw, int fh) => 2f / (1f + Mathf.Sin(Mathf.PI / Mathf.Max(2, Mathf.Max(fw, fh))));
    int flowW, flowH;                       // flow grid
    const int FlowBlock = 4;                // climate cells per flow cell, per side
    int flowOx, flowOy;                     // climate cells the first block starts before the grid (world alignment)
    int flowPasses;                         // passes of the last solve (fewer while dragging)
    float[] extFlowU, extFlowV;             // the flow at every climate cell (bilinear), for the view
    byte[] extFlowSolid;
    float[] flowAir;                        // 1b.2: the air's humidity on the flow grid
    float[] flowU, flowV, flowDiv;          // faces: u (flowW + 1) × flowH, v flowW × (flowH + 1); divergence before
    byte[] flowSolid;
    float[] windU, windV;                   // view cells, bilinear from the flow cell centres
    byte[] viewSolid;
    float flowMs, flowDivBefore, flowDivAfter, flowDivMax, flowSpeedMax;
    int flowBurstState = -1, flowFluid;

    bool FlowOn => colouring == Colouring.Biomes && wind && windModel == WindModel.Flow;

    void Flow(bool burst)
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        int w = extW, h = extH;
        // The blocks follow the world, not the grid: each block starts at a world cell that
        // is a multiple of FlowBlock. Otherwise dragging regroups them and a wall can appear
        // or vanish at a mountain's edge (1a edge test: wind Δ max 1.35).
        const int B = FlowBlock;
        int ox = flowOx = (Mathf.RoundToInt(offset.x * sampleN) - mL) & (B - 1);
        int oy = flowOy = (Mathf.RoundToInt(offset.y * sampleN) - mB) & (B - 1);
        int fw = flowW = (w + ox + B - 1) / B, fh = flowH = (h + oy + B - 1) / B;
        // While dragging, a quarter of the passes (the wind is half done until the release,
        // which solves again with all of them). Same map + same passes = same wind.
        int passes = flowPasses = dragging ? Mathf.Max(20, flowIterations / 4) : flowIterations;
        int cells = fw * fh;
        float a = windAngle * Mathf.Deg2Rad;

        var rank = new NativeArray<float>(extRank, Allocator.TempJob);
        var heights = new NativeArray<float>(extValues, Allocator.TempJob);
        var solid = new NativeArray<byte>(cells, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
        var blockRank = new NativeArray<float>(cells, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
        var blockSea = new NativeArray<float>(cells, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
        var div = new NativeArray<float>(cells, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
        var p = new NativeArray<float>(cells, Allocator.TempJob);   // starts at 0: no warm start
        var u = new NativeArray<float>((fw + 1) * fh, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
        var v = new NativeArray<float>(fw * (fh + 1), Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
        var weight = new NativeArray<float4>(cells, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
        var gain = new NativeArray<float>(cells, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
        var keep = new NativeArray<float>(cells, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
        var fAir = new NativeArray<float>(cells, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
        var fStart = new NativeArray<float>(cells, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
        var airStats = new NativeArray<float>(4, Allocator.TempJob);
        var eu = new NativeArray<float>(w * h, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
        var ev = new NativeArray<float>(w * h, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
        var es = new NativeArray<byte>(w * h, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
        var ea = new NativeArray<float>(w * h, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
        var probe = new NativeArray<int>(1, Allocator.TempJob);
        try
        {
            var mask = new FlowMaskJob
            {
                rank = rank, heights = heights, solid = solid, blockRank = blockRank, blockSea = blockSea,
                w = w, h = h, fw = fw, ox = ox, oy = oy, block = B, solidAbove = solidAbove, threshold = threshold, probe = probe
            };
            var init = new FlowInitJob { solid = solid, u = u, v = v, div = div, fw = fw, fh = fh, bu = Mathf.Cos(a), bv = Mathf.Sin(a), probe = probe };
            var sor = new FlowSorJob { solid = solid, div = div, p = p, fw = fw, fh = fh, omega = FlowOmega(fw, fh), probe = probe };
            var apply = new FlowApplyJob { solid = solid, p = p, u = u, v = v, fw = fw, fh = fh, probe = probe };
            var setup = new FlowAirSetupJob
            {
                u = u, v = v, solid = solid, rank = blockRank, sea = blockSea, weight = weight, gain = gain, keep = keep,
                fw = fw, fh = fh, cell = CoastUnits / sampleN * B, dry = windDry, evaporation = windEvaporation,
                lift = windLift * LiftScale, bu = Mathf.Cos(a), bv = Mathf.Sin(a), probe = probe
            };
            var airJob = new FlowAirJob
            {
                weight = weight, gain = gain, keep = keep, air = fAir, start = fStart, stats = airStats,
                fw = fw, fh = fh, maxRounds = FlowAirMaxRounds, tolerance = FlowAirTolerance, probe = probe
            };
            var sample = new FlowSampleJob
            {
                u = u, v = v, solid = solid, air = fAir, eu = eu, ev = ev, es = es, ea = ea,
                w = w, fw = fw, fh = fh, ox = ox, oy = oy, block = B, probe = probe
            };
            System.Diagnostics.Stopwatch airWatch;
            if (burst)
            {
                var handle = mask.ScheduleParallel(fh, 8, default);
                handle = init.ScheduleParallel(fh + 1, 8, handle);
                for (int k = 0; k < passes; k++)
                {
                    sor.parity = 0; handle = sor.ScheduleParallel(fh, 8, handle);
                    sor.parity = 1; handle = sor.ScheduleParallel(fh, 8, handle);
                }
                apply.ScheduleParallel(fh + 1, 8, handle).Complete();
                airWatch = System.Diagnostics.Stopwatch.StartNew();
                handle = setup.ScheduleParallel(fh, 8, default);
                airJob.Schedule(handle).Complete();
                flowAirMs = (float)airWatch.Elapsed.TotalMilliseconds;
                sample.ScheduleParallel(h, 8, default).Complete();
                flowBurstState = probe[0] != 0 ? GridNoiseJob<Simplex2D<Simplex>>.Managed : GridNoiseJob<Simplex2D<Simplex>>.Compiled;
            }
            else
            {
                // Same Execute, plain C# on one thread, in the same order.
                for (int y = 0; y < fh; y++) mask.Execute(y);
                for (int y = 0; y <= fh; y++) init.Execute(y);
                for (int k = 0; k < passes; k++)
                {
                    sor.parity = 0; for (int y = 0; y < fh; y++) sor.Execute(y);
                    sor.parity = 1; for (int y = 0; y < fh; y++) sor.Execute(y);
                }
                for (int y = 0; y <= fh; y++) apply.Execute(y);
                airWatch = System.Diagnostics.Stopwatch.StartNew();
                for (int y = 0; y < fh; y++) setup.Execute(y);
                airJob.Execute();
                flowAirMs = (float)airWatch.Elapsed.TotalMilliseconds;
                for (int y = 0; y < h; y++) sample.Execute(y);
                flowBurstState = GridNoiseJob<Simplex2D<Simplex>>.Off;
            }
            flowSolid = flowSolid != null && flowSolid.Length == cells ? flowSolid : new byte[cells];
            solid.CopyTo(flowSolid);
            flowDiv = Ensure(flowDiv, cells);
            div.CopyTo(flowDiv);
            flowU = Ensure(flowU, u.Length);
            u.CopyTo(flowU);
            flowV = Ensure(flowV, v.Length);
            v.CopyTo(flowV);
            flowAir = Ensure(flowAir, cells);
            fAir.CopyTo(flowAir);
            flowAirMaxChange = airStats[0];
            flowAirMeanChange = airStats[1];
            flowAirUnsettled = (int)airStats[2];
            flowAirRounds = (int)airStats[3];
            extFlowU = Ensure(extFlowU, w * h);
            eu.CopyTo(extFlowU);
            extFlowV = Ensure(extFlowV, w * h);
            ev.CopyTo(extFlowV);
            extFlowSolid = extFlowSolid != null && extFlowSolid.Length == w * h ? extFlowSolid : new byte[w * h];
            es.CopyTo(extFlowSolid);
            extAir = Ensure(extAir, w * h);
            ea.CopyTo(extAir);
        }
        finally
        {
            rank.Dispose(); heights.Dispose(); solid.Dispose(); blockRank.Dispose(); blockSea.Dispose();
            div.Dispose(); p.Dispose(); u.Dispose(); v.Dispose();
            weight.Dispose(); gain.Dispose(); keep.Dispose(); fAir.Dispose(); fStart.Dispose(); airStats.Dispose();
            eu.Dispose(); ev.Dispose(); es.Dispose(); ea.Dispose(); probe.Dispose();
        }
        FlowStats();
        FlowToView();
        flowMs = (float)watch.Elapsed.TotalMilliseconds;
    }

    // Mean |divergence| over the open cells before and after the projection (1 = the
    // background speed across one cell), and the largest one after.
    void FlowStats()
    {
        int fw = flowW, fh = flowH;
        double before = 0.0, after = 0.0;
        float worst = 0f;
        int fluid = 0;
        for (int y = 0; y < fh; y++)
            for (int x = 0; x < fw; x++)
            {
                int c = y * fw + x;
                if (flowSolid[c] != 0) continue;
                fluid++;
                before += Mathf.Abs(flowDiv[c]);
                float d = flowU[y * (fw + 1) + x + 1] - flowU[y * (fw + 1) + x] + flowV[(y + 1) * fw + x] - flowV[y * fw + x];
                after += Mathf.Abs(d);
                worst = Mathf.Max(worst, Mathf.Abs(d));
            }
        flowFluid = fluid;
        flowDivBefore = fluid > 0 ? (float)(before / fluid) : 0f;
        flowDivAfter = fluid > 0 ? (float)(after / fluid) : 0f;
        flowDivMax = worst;
    }

    // The velocity at each view cell: the view's part of the climate-grid samples (bilinear
    // between the flow cell centres, FlowSampleJob). View cell (x, y) is climate cell (x + mL, y + mB).
    void FlowToView()
    {
        int n = sampleN, w = extW;
        windU = Ensure(windU, n * n);
        windV = Ensure(windV, n * n);
        viewSolid = viewSolid != null && viewSolid.Length == n * n ? viewSolid : new byte[n * n];
        float fastest = 0f;
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                int i = y * n + x, e = (y + mB) * w + x + mL;
                float uu = windU[i] = extFlowU[e];
                float vv = windV[i] = extFlowV[e];
                viewSolid[i] = extFlowSolid[e];
                fastest = Mathf.Max(fastest, uu * uu + vv * vv);
            }
        flowSpeedMax = Mathf.Sqrt(fastest);
    }

    // ---------------------------------------------------------------------------------
    // v6 (1b): the humidity of the air, carried by the flow. Same rules as the sweep (v3):
    // the air enters the grid as sea air (1), picks up moisture over the sea, dries slowly
    // over flat land and fast when it climbs (orographic rain). What changes is the path.
    // 1b.2: solved on the flow grid itself (1/4 of the climate grid per side, 16× fewer
    // cells) and stretched to the climate grid like the wind (bilinear, FlowSampleJob).
    // - Finite volumes, first-order upwind, steady state: the air of a cell is the mix of
    //   what enters through its four faces, weighted by the flux through each (the MAC faces
    //   of the projection, not the interpolated centres). A face against a wall carries 0,
    //   so open cells never draw air from walls, and a cell between two inflows (a
    //   convergence line) mixes both.
    // - Each flow cell is a block of climate cells: the sea fraction of the block splits the
    //   distance between evaporation (sea part) and drying (land part); the climb is the
    //   block's mean rank minus the flux-weighted rank upwind.
    // - The distance a parcel covers per cell is speed / (|u| + |v|) cells (1 along an axis,
    //   0.71 on a diagonal). Losses are per distance, not per time: calm air is not wetter.
    // - Walls (blocks above solidAbove) have no flow: there the air comes from the
    //   background wind's upwind neighbours, so it climbs them and gives them rain.
    // Everything that does not depend on the air (weights, gain, keep) is fixed per cell and
    // computed once (FlowAirSetupJob, in parallel); the sweeps only mix and scale.
    // Fast sweeping (Zhao 2005): Gauss–Seidel in the four diagonal orders, one thread, until
    // no cell changes more than FlowAirTolerance in a round (at most FlowAirMaxRounds). The
    // stopping rule is part of the result: same inputs, same rounds, same air.
    // ---------------------------------------------------------------------------------

    const int FlowAirMaxRounds = 64;
    const float FlowAirTolerance = 1e-4f;   // well under one colour step (1/255)
    float flowAirMs, flowAirMaxChange, flowAirMeanChange;
    int flowAirRounds, flowAirUnsettled;

    string FlowSummary() =>
        $"flow {flowW}×{flowH}, {flowPasses} passes, ω {FlowOmega(flowW, flowH):0.000}, {flowMs:0.0} ms ({GridNoiseJob<Simplex2D<Simplex>>.StateName(flowBurstState)}); " +
        $"divergence {flowDivBefore:0.0000} → {flowDivAfter:0.0000} (max {flowDivMax:0.000}); fastest {flowSpeedMax:0.00}× the background; " +
        $"air {flowAirRounds} rounds (max {FlowAirMaxRounds}), {flowAirMs:0.0} ms; last round: largest change {flowAirMaxChange:0.00000}, " +
        $"mean {flowAirMeanChange:0.000000}, cells over 0.01: {flowAirUnsettled}";

    // ---------------------------------------------------------------------------------
    // v6 (2a): ocean currents, the wind-driven circulation of Stommel (1948).
    // The planet's winds (easterly trades near the equator, westerlies at mid latitudes,
    // polar easterlies) drag the sea surface. Where that drag turns (its curl), the water is
    // forced north or south, and the planet's spin, which matters more toward the poles
    // (beta = how fast the Coriolis parameter grows with latitude), balances it: the
    // interior of every ocean drifts slowly toward the equator in the subtropics (Sverdrup).
    // The water must come back, and it does along the WEST side of each basin, in a narrow
    // fast current (Gulf Stream, Kuroshio, Brazil); the east side keeps the slow broad drift
    // toward the equator (California, Canary, Humboldt). Friction (eps) sets the width of
    // the western current; here it is a slider in view widths.
    //   eps · ∇²psi + beta(y) · dpsi/dx = curl tau(y),   u = −dpsi/dy, v = dpsi/dx
    // psi is the streamfunction: water flows along its contour lines, faster where they
    // crowd. psi = 0 on land (no water crosses the coast) and outside the grid.
    // - Scale: a gyre spans half an ocean, far more than the view (about 11° of latitude).
    //   So the ocean has its own coarse grid, fixed to the world: regions of 16 × 16 view
    //   widths (pole to pole in y), OceanPerView cells per view width, one every 8 views
    //   in x, each overlapping its neighbours by half. A point is the blend of the two
    //   regions that hold it, weighted by how far it is from their edges (their walls fall
    //   where their weight is 0). Nothing depends on the view: dragging never changes the
    //   currents. A region is computed once and cached (the last 4).
    // - Land: each cell is sea if at least half of its 2 × 2 height samples are below the
    //   sea level. Islands smaller than a cell disappear for the ocean.
    // - Solver: Gauss–Seidel by lines with over-relaxation. Rows in two colours (even, odd)
    //   in parallel; each row is swept east to west, the direction the beta term carries
    //   information (the Sverdrup interior is integrated from the east coast), so most of
    //   the basin is right after one pass. beta·dpsi/dx is one-sided (upwind, east): the
    //   scheme stays stable however thin the western current. Measured offline (2a,
    //   rectangular and noise coasts, 128², west current 0.1–1): ω 1.85 blew up with a
    //   narrow current; ω 1.7 converged in all; a region that does not is solved again.
    // - Units: lengths in view widths; wind drag tau0 = 1/6 so the interior (Sverdrup)
    //   speed is about 1. Speeds are reported as multiples of it.
    // Known limit (2a): every land mass has psi = 0. Between two land masses no net water
    // passes (the island rule of Godfrey 1989 would let it); to measure before fixing.
    // ---------------------------------------------------------------------------------

    const int OceanPerView = 8;
    const float OceanStride = 8f;                  // view widths between region centres
    const int OceanN = 16 * OceanPerView;          // cells per side of a region
    const int OceanPasses = 200;
    static readonly float[] OceanOmegas = { 1.7f, 1.4f }; // second try: safe, twice the passes
    const float OceanTolerance = 0.01f;
    const float OceanTau = 1f / 6f;
    const int OceanCacheSize = 4;

    class OceanRegion
    {
        public int index;
        public string key;
        public float[] u, v, psi; // psi: the stream function (continents step: the Gyres colouring)
        public float[] temp;      // 2b: sea surface temperature, °C (land cells too, so the bilinear blend is smooth at the coast)
        public byte[] sea;        // 2b: 1 = sea cell
        public float[] anom;      // 2c: T − T0 °C over the sea, carried SeaAnomRings cells into the land (what the climate reads)
        public float ms, residual, omega, fastest, seaShare;
        public int state;
        // 2b: the temperature has its own key (heat memory): moving that slider never solves the currents again.
        public string heatKey;
        public float heatMs, heatChange, heatAnomaly, upAnomaly, upColdest;
        public int heatRounds, heatState, upCells;
    }

    readonly System.Collections.Generic.List<OceanRegion> oceanRegions = new();
    float[] currentU, currentV;  // view cells
    float[] seaTemp;             // 2b: view cells, °C

    // v6 (2b), step 1: sea surface temperature by latitude only. 28 °C at the equator,
    // −2 °C (sea water freezes) at the poles, cos² in between: about 20 °C at 30°, 13 °C
    // at 45°, 5 °C at 60°, close to the zonal means of the real oceans. Steps 2 and 3 move
    // it with the currents and cool the upwelling coasts; this is what they relax toward.
    const float SeaWarmest = 28f, SeaColdest = -2f;

    static float SeaBaseTemp(float y)
    {
        float c = Mathf.Cos(OceanLatitude(y));
        return SeaColdest + (SeaWarmest - SeaColdest) * c * c;
    }
    float oceanMs, oceanLastRegionMs;
    string oceanRegionsUsed = "";

    string OceanKey(int index) => $"{HeightKey()}|{F(threshold)}|{F(oceanWidth)}|{index}";

    OceanRegion GetOceanRegion(int index)
    {
        string key = OceanKey(index);
        foreach (var r in oceanRegions)
            if (r.key == key)
            {
                if (r.heatKey != HeatKey()) SeaHeat(r, useBurst);
                return r;
            }
        var region = ComputeOceanRegion(index, useBurst);
        SeaHeat(region, useBurst);
        oceanLastRegionMs = region.ms;
        oceanRegions.Insert(0, region);
        if (oceanRegions.Count > OceanCacheSize) oceanRegions.RemoveAt(oceanRegions.Count - 1);
        return region;
    }

    // ---------------------------------------------------------------------------------
    // v6 (2b), step 2: the currents carry the heat. Steady state of
    //   u · ∇T = (T0(lat) − T) / τ
    // the water keeps its temperature as it moves and forgets it toward the base of its
    // latitude (T0, step 1) at a fixed rate. τ is a time, not a distance: fast water
    // carries its temperature farther (the Gulf Stream warms Europe because it is fast).
    // Heat memory = how far the water at the interior (Sverdrup) speed, about 1 in these
    // units, travels in τ, in view widths; so τ = heat memory.
    // - Finite volumes on the region's own grid, first-order upwind, the scheme of the air
    //   (1b.2): a cell's temperature is the mix of what flows into it through its four
    //   faces, weighted by the flow through each, plus the pull toward T0:
    //     T = (Σ in_k · T_k + (h / τ) · T0) / (Σ in_k + h / τ)
    //   The face flows come from psi at the cell corners (the mean of the four cells
    //   around each corner): the water through a face is the difference of psi at its two
    //   ends, so what enters a cell is what leaves it (no water made or lost). A face with
    //   land on either side carries nothing; land and still water keep T0.
    // - Fast sweeping (four diagonal orders, one thread) until no cell changes more than
    //   SeaHeatTolerance (at most SeaHeatMaxRounds): same inputs, same rounds, same result.
    //   A gyre is a closed loop, so the heat goes round it; with a long memory it takes
    //   more rounds to settle.
    // ---------------------------------------------------------------------------------

    const int SeaHeatMaxRounds = 100;
    const float SeaHeatTolerance = 0.005f; // °C, well under a colour step

    string HeatKey() => $"{F(heatMemory)}|{F(upwelling)}";

    // ---------------------------------------------------------------------------------
    // 2b, step 3: coastal upwelling (Humboldt, Benguela, California, Canary). On the west
    // coast of a continent in the subtropics the wind blows toward the equator along the
    // coast; the Earth's turn pushes the surface water out to sea (Ekman transport) and
    // cold water rises from below to take its place. The planet's wind here is zonal only
    // (OceanWindX), so the rule cannot read an alongshore wind; it reads its trace instead:
    // the same wind drives the slow current toward the equator along those coasts. A sea
    // cell upwells when the next cell to the east is land (a real coast, not the region's
    // wall) and its water flows toward the equator; the faster, the stronger. It is one more
    // pull in the mix, toward the deep water temperature (or T0 where that is colder):
    //   T = (Σ in_k · T_k + relax · T0 + up · Tdeep) / (Σ in_k + relax + up),
    //   up = 2 · Upwelling · (speed toward the equator)
    // so at Upwelling 0.5 and the interior speed the coast goes about halfway to Tdeep, and
    // the current carries the cold water on, toward the equator and then west.
    // Subpolar gyres run poleward along the same coasts, so they never upwell.
    // ---------------------------------------------------------------------------------
    const float SeaDeepTemp = 10f; // °C, the water that rises (real upwelled water: about 8–14 °C)

    void SeaHeat(OceanRegion r, bool burst)
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        int n = OceanN, cells = n * n;
        float h = 1f / OceanPerView, y0 = -LatitudeSpan;
        float[] psi = r.psi;
        byte[] sea = r.sea;
        // psi at the corner up-right of cell (x, y): the mean of the four cells around it (0 outside).
        float P(int x, int y) => x < 0 || y < 0 || x >= n || y >= n ? 0f : psi[y * n + x];
        float C(int x, int y) => 0.25f * (P(x, y) + P(x + 1, y) + P(x, y + 1) + P(x + 1, y + 1));
        bool Sea(int x, int y) => x >= 0 && y >= 0 && x < n && y < n && sea[y * n + x] != 0;

        var weight = new float4[cells];
        var pull = new float[cells];
        var t0 = new float[cells];
        float relax = h / heatMemory;
        float[] cv = r.v;
        var upAt = new bool[cells];
        for (int y = 0; y < n; y++)
        {
            float rowBase = SeaBaseTemp(y0 + (y + 0.5f) * h);
            for (int x = 0; x < n; x++)
            {
                int i = y * n + x;
                t0[i] = rowBase;
                if (!Sea(x, y)) { pull[i] = rowBase; continue; } // weight 0: land keeps T0
                // Velocity through each face (positive = east or north), from psi at its ends.
                float uW = Sea(x - 1, y) ? -(C(x - 1, y) - C(x - 1, y - 1)) / h : 0f;
                float uE = Sea(x + 1, y) ? -(C(x, y) - C(x, y - 1)) / h : 0f;
                float vS = Sea(x, y - 1) ? (C(x, y - 1) - C(x - 1, y - 1)) / h : 0f;
                float vN = Sea(x, y + 1) ? (C(x, y) - C(x - 1, y)) / h : 0f;
                var inflow = new float4(Mathf.Max(uW, 0f), Mathf.Max(-uE, 0f), Mathf.Max(vS, 0f), Mathf.Max(-vN, 0f));
                // Step 3: land right to the east (inside the grid) and water toward the equator.
                float up = 0f;
                if (x + 1 < n && sea[i + 1] == 0)
                {
                    float toEquator = (y0 + (y + 0.5f) * h) > 0f ? -cv[i] : cv[i];
                    up = 2f * upwelling * Mathf.Max(toEquator, 0f);
                    upAt[i] = toEquator > 0f; // counted at any strength, so Upwelling 0 gives the baseline
                }
                float total = inflow.x + inflow.y + inflow.z + inflow.w + relax + up;
                weight[i] = inflow / total;
                pull[i] = (relax * rowBase + up * Mathf.Min(SeaDeepTemp, rowBase)) / total;
            }
        }

        var nWeight = new NativeArray<float4>(weight, Allocator.TempJob);
        var nPull = new NativeArray<float>(pull, Allocator.TempJob);
        var nTemp = new NativeArray<float>(t0, Allocator.TempJob); // starts at the base
        var nStart = new NativeArray<float>(cells, Allocator.TempJob);
        var stats = new NativeArray<float>(2, Allocator.TempJob);
        var probe = new NativeArray<int>(1, Allocator.TempJob);
        var temp = new float[cells];
        int state = GridNoiseJob<Simplex2D<Simplex>>.Off;
        try
        {
            var job = new SeaHeatJob
            {
                weight = nWeight, pull = nPull, temp = nTemp, start = nStart, stats = stats, probe = probe,
                n = n, maxRounds = SeaHeatMaxRounds, tolerance = SeaHeatTolerance
            };
            if (burst)
            {
                job.Schedule().Complete();
                state = probe[0] != 0 ? GridNoiseJob<Simplex2D<Simplex>>.Managed : GridNoiseJob<Simplex2D<Simplex>>.Compiled;
            }
            else job.Execute();
            nTemp.CopyTo(temp);
            r.heatChange = stats[0];
            r.heatRounds = (int)stats[1];
        }
        finally
        {
            nWeight.Dispose(); nPull.Dispose(); nTemp.Dispose(); nStart.Dispose(); stats.Dispose(); probe.Dispose();
        }

        // How far the currents moved the temperature: mean |T − T0| over the sea.
        double anomaly = 0.0;
        int seaCells = 0;
        for (int i = 0; i < cells; i++)
            if (sea[i] != 0) { anomaly += Mathf.Abs(temp[i] - t0[i]); seaCells++; }
        r.heatAnomaly = seaCells > 0 ? (float)(anomaly / seaCells) : 0f;
        // Step 3: the upwelling coast, T − T0 (negative = colder than its latitude).
        double upSum = 0.0;
        int upCount = 0;
        float coldest = 0f;
        for (int i = 0; i < cells; i++)
            if (upAt[i]) { float d = temp[i] - t0[i]; upSum += d; upCount++; coldest = Mathf.Min(coldest, d); }
        r.upCells = upCount;
        r.upAnomaly = upCount > 0 ? (float)(upSum / upCount) : 0f;
        r.upColdest = coldest;
        r.anom = SeaAnomaly(temp, t0, sea, n);
        r.temp = temp;
        r.heatKey = HeatKey();
        r.heatState = state;
        r.heatMs = (float)watch.Elapsed.TotalMilliseconds;
    }

    // ---------------------------------------------------------------------------------
    // 2c, step 1: what the land climate reads from the sea. The anomaly T − T0 (how much
    // colder or warmer than its latitude), not the temperature itself: the climate already
    // cools with latitude, the absolute value would count it twice, and with the sea
    // weights at 0 the climate stays exactly as it was.
    // The region grid is coarse (1/8 of a view per cell) and its land cells hold T0, so a
    // bilinear read at the real coast would mix the cold coast cell with land at anomaly 0
    // and halve it; the coarse coast also misses the real one by up to a cell. So the
    // anomaly is carried SeaAnomRings cells into the land: each ring takes the mean of its
    // known 8 neighbours (computed into a copy, so the order of the cells does not matter).
    // Farther land keeps 0. Cached with the region's temperature (same heat key).
    // ---------------------------------------------------------------------------------
    const int SeaAnomRings = 2;

    static float[] SeaAnomaly(float[] temp, float[] t0, byte[] sea, int n)
    {
        int cells = n * n;
        var anom = new float[cells];
        var known = new bool[cells];
        for (int i = 0; i < cells; i++)
            if (sea[i] != 0) { anom[i] = temp[i] - t0[i]; known[i] = true; }
        var next = new float[cells];
        var nextKnown = new bool[cells];
        for (int ring = 0; ring < SeaAnomRings; ring++)
        {
            System.Array.Copy(anom, next, cells);
            System.Array.Copy(known, nextKnown, cells);
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    int i = y * n + x;
                    if (known[i]) continue;
                    float sum = 0f;
                    int count = 0;
                    for (int dy = -1; dy <= 1; dy++)
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            int xx = x + dx, yy = y + dy;
                            if (xx < 0 || yy < 0 || xx >= n || yy >= n) continue;
                            int j = yy * n + xx;
                            if (known[j]) { sum += anom[j]; count++; }
                        }
                    if (count > 0) { next[i] = sum / count; nextKnown[i] = true; }
                }
            System.Array.Copy(next, anom, cells);
            System.Array.Copy(nextKnown, known, cells);
        }
        return anom;
    }

    static float OceanLatitude(float y) => 0.5f * Mathf.PI * Mathf.Min(Mathf.Abs(y) / LatitudeSpan, 1f);
    static float OceanWindX(float y) => -OceanTau * Mathf.Sin(6f * OceanLatitude(y)); // trades, westerlies, polar easterlies

    OceanRegion ComputeOceanRegion(int index, bool burst)
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        int n = OceanN, cells = n * n;
        float h = 1f / OceanPerView;
        float x0 = OceanStride * index - 8f, y0 = -LatitudeSpan;

        // Land and sea: 2 × 2 height samples per cell, the same noises and curves as the view.
        int sn = 2 * n;
        float step = 0.5f * h;
        var src = new float[FieldCount][];
        int keepState = burstState;
        foreach (int k in HeightFields())
        {
            src[k] = new float[sn * sn];
            SampleInto(src[k], GetField(k), FieldSeed(k), sn, x0 + 0.5f * step, y0 + 0.5f * step, step);
        }
        burstState = keepState; // the "last sampling" label is about the view
        var heights = new float[sn * sn];
        CombineHeight(heights, src);
        var sea = new byte[cells];
        int seaCount = 0;
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                int below = 0;
                for (int dy = 0; dy < 2; dy++)
                    for (int dx = 0; dx < 2; dx++)
                        if (heights[(2 * y + dy) * sn + 2 * x + dx] < threshold) below++;
                if (below >= 2) { sea[y * n + x] = 1; seaCount++; }
            }

        // Per row: the drag's curl (centred difference of the zonal wind) and beta.
        var force = new float[n];
        var beta = new float[n];
        for (int y = 0; y < n; y++)
        {
            float yc = y0 + (y + 0.5f) * h;
            force[y] = -(OceanWindX(yc + 0.5f * h) - OceanWindX(yc - 0.5f * h)) / h;
            beta[y] = Mathf.Cos(OceanLatitude(yc)) * Mathf.PI / (2f * LatitudeSpan);
        }
        float eps = oceanWidth * Mathf.PI / (2f * LatitudeSpan); // western current width × beta at the equator

        var nSea = new NativeArray<byte>(sea, Allocator.TempJob);
        var nForce = new NativeArray<float>(force, Allocator.TempJob);
        var nBeta = new NativeArray<float>(beta, Allocator.TempJob);
        var nPsi = new NativeArray<float>(cells, Allocator.TempJob);
        var probe = new NativeArray<int>(1, Allocator.TempJob);
        var psi = new float[cells];
        int state = GridNoiseJob<Simplex2D<Simplex>>.Off;
        float residual = 0f, omegaUsed = 0f;
        try
        {
            // 2a fix: over-relaxation can blow up on this non-symmetric equation (ω 1.85 did,
            // with a narrow west current and an irregular coast). ω 1.7 converged in every
            // offline case; if a region still does not settle, it is solved again with a
            // safe ω and twice the passes. Deterministic: same inputs, same attempts.
            foreach (float omega in OceanOmegas)
            {
                int passes = omega == OceanOmegas[0] ? OceanPasses : 2 * OceanPasses;
                for (int i = 0; i < cells; i++) nPsi[i] = 0f;
                var sor = new OceanSorJob { sea = nSea, force = nForce, beta = nBeta, psi = nPsi, probe = probe, n = n, eps = eps, h = h, omega = omega };
                int rows = (n + 1) / 2;
                if (burst)
                {
                    JobHandle handle = default;
                    for (int p = 0; p < passes; p++)
                    {
                        sor.parity = 0; handle = sor.ScheduleParallel(rows, 4, handle);
                        sor.parity = 1; handle = sor.ScheduleParallel(rows, 4, handle);
                    }
                    handle.Complete();
                    state = probe[0] != 0 ? GridNoiseJob<Simplex2D<Simplex>>.Managed : GridNoiseJob<Simplex2D<Simplex>>.Compiled;
                }
                else
                {
                    for (int p = 0; p < passes; p++)
                    {
                        sor.parity = 0; for (int r = 0; r < rows; r++) sor.Execute(r);
                        sor.parity = 1; for (int r = 0; r < rows; r++) sor.Execute(r);
                    }
                }
                nPsi.CopyTo(psi);
                residual = OceanResidual(psi, sea, force, beta, n, eps, h);
                omegaUsed = omega;
                if (residual < OceanTolerance) break;
            }
        }
        finally
        {
            nSea.Dispose(); nForce.Dispose(); nBeta.Dispose(); nPsi.Dispose(); probe.Dispose();
        }

        // Velocities at the cell centres (centred differences; land and outside: psi = 0)
        // and the fastest water.
        float P(int x, int y) => x < 0 || y < 0 || x >= n || y >= n ? 0f : psi[y * n + x];
        var u = new float[cells];
        var v = new float[cells];
        float fastest = 0f;
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                int i = y * n + x;
                if (sea[i] == 0) continue;
                u[i] = -(P(x, y + 1) - P(x, y - 1)) / (2f * h);
                v[i] = (P(x + 1, y) - P(x - 1, y)) / (2f * h);
                fastest = Mathf.Max(fastest, u[i] * u[i] + v[i] * v[i]);
            }

        // 2b: the temperature is solved apart (SeaHeat), with its own key.
        return new OceanRegion
        {
            index = index, key = OceanKey(index), u = u, v = v, psi = psi, sea = sea, state = state,
            ms = (float)watch.Elapsed.TotalMilliseconds,
            residual = residual, omega = omegaUsed,
            fastest = Mathf.Sqrt(fastest),
            seaShare = (float)seaCount / cells
        };
    }

    // Mean |error of the equation| over the sea, relative to the mean |wind curl|.
    static float OceanResidual(float[] psi, byte[] sea, float[] force, float[] beta, int n, float eps, float h)
    {
        float P(int x, int y) => x < 0 || y < 0 || x >= n || y >= n ? 0f : psi[y * n + x];
        double res = 0.0, scale = 0.0;
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                if (sea[y * n + x] == 0) continue;
                float c = P(x, y), e = P(x + 1, y), w = P(x - 1, y), no = P(x, y + 1), so = P(x, y - 1);
                res += Mathf.Abs(eps * (e + w + no + so - 4f * c) / (h * h) + beta[y] * (e - c) / h - force[y]);
                scale += Mathf.Abs(force[y]);
            }
        return scale > 0.0 ? (float)(res / scale) : 0f;
    }

    // Bilinear between the region's cell centres at world point (x, y), in view widths.
    static void SampleRegion(OceanRegion r, float x, float y, out float u, out float v)
    {
        int n = OceanN;
        float gx = Mathf.Clamp((x - (OceanStride * r.index - 8f)) * OceanPerView - 0.5f, 0f, n - 1);
        float gy = Mathf.Clamp((y + LatitudeSpan) * OceanPerView - 0.5f, 0f, n - 1);
        int x0 = Mathf.Min((int)gx, n - 2), y0 = Mathf.Min((int)gy, n - 2);
        float tx = gx - x0, ty = gy - y0;
        int i = y0 * n + x0;
        u = Mathf.Lerp(Mathf.Lerp(r.u[i], r.u[i + 1], tx), Mathf.Lerp(r.u[i + n], r.u[i + n + 1], tx), ty);
        v = Mathf.Lerp(Mathf.Lerp(r.v[i], r.v[i + 1], tx), Mathf.Lerp(r.v[i + n], r.v[i + n + 1], tx), ty);
    }

    static float SampleRegionPsi(OceanRegion r, float x, float y) => SampleRegionField(r, r.psi, x, y);

    // 2b: any per-cell field of a region (psi, temperature), bilinear as SampleRegion.
    static float SampleRegionField(OceanRegion r, float[] f, float x, float y)
    {
        int n = OceanN;
        float gx = Mathf.Clamp((x - (OceanStride * r.index - 8f)) * OceanPerView - 0.5f, 0f, n - 1);
        float gy = Mathf.Clamp((y + LatitudeSpan) * OceanPerView - 0.5f, 0f, n - 1);
        int x0 = Mathf.Min((int)gx, n - 2), y0 = Mathf.Min((int)gy, n - 2);
        float tx = gx - x0, ty = gy - y0;
        int i = y0 * n + x0;
        return Mathf.Lerp(Mathf.Lerp(f[i], f[i + 1], tx), Mathf.Lerp(f[i + n], f[i + n + 1], tx), ty);
    }

    // The currents at every view cell: the two regions around each column, blended.
    // 2c: runs before the climate, and also reads the sea anomaly on the climate grid
    // (view + margins, extSeaAnom), so the regions span the margins too. The blending is
    // RegionSampleJob (Burst; with Burst off the same Execute in plain C#): the climate grid
    // is ~5× the view's cells, and in plain C# it cost ~60 ms in the editor.
    void Ocean()
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        int n = sampleN, w = extW, h = extH;
        currentU = Ensure(currentU, n * n);
        currentV = Ensure(currentV, n * n);
        seaTemp = Ensure(seaTemp, n * n);
        extSeaAnom = Ensure(extSeaAnom, w * h);
        int first = Mathf.FloorToInt((offset.x - (float)mL / n) / OceanStride);
        int last = Mathf.FloorToInt((offset.x + (float)(n + mR) / n) / OceanStride) + 1;
        var regions = new OceanRegion[last - first + 1];
        for (int k = 0; k < regions.Length; k++) regions[k] = GetOceanRegion(first + k);
        oceanRegionsUsed = first == last - 1 ? $"{first}, {last}" : $"{first} to {last}";

        var gridWatch = System.Diagnostics.Stopwatch.StartNew();
        int rn = OceanN * OceanN;
        NativeArray<float> Stack(System.Func<OceanRegion, float[]> pick)
        {
            var a = new NativeArray<float>(regions.Length * rn, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            for (int k = 0; k < regions.Length; k++) NativeArray<float>.Copy(pick(regions[k]), 0, a, k * rn, rn);
            return a;
        }
        var fu = Stack(r => r.u);
        var fv = Stack(r => r.v);
        var ft = Stack(r => r.temp);
        var fa = Stack(r => r.anom);
        var ou = new NativeArray<float>(n * n, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
        var ov = new NativeArray<float>(n * n, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
        var ot = new NativeArray<float>(n * n, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
        var oa = new NativeArray<float>(w * h, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
        var probe = new NativeArray<int>(1, Allocator.TempJob);
        try
        {
            RegionSampleJob Job(NativeArray<float> field, NativeArray<float> output, int cols, int shiftX, int shiftY) => new RegionSampleJob
            {
                field = field, output = output, probe = probe, first = first, cols = cols, n = n,
                originX = offset.x, originY = offset.y, shiftX = shiftX, shiftY = shiftY,
                oceanN = OceanN, perView = OceanPerView, stride = OceanStride, latitudeSpan = LatitudeSpan
            };
            var jobs = new[] { (Job(fu, ou, n, 0, 0), n), (Job(fv, ov, n, 0, 0), n), (Job(ft, ot, n, 0, 0), n), (Job(fa, oa, w, mL, mB), h) };
            if (useBurst)
            {
                JobHandle handle = default; // one after the other: they share the probe
                foreach (var (job, rows) in jobs) handle = job.ScheduleParallel(rows, 8, handle);
                handle.Complete();
                oceanSampleState = probe[0] != 0 ? GridNoiseJob<Simplex2D<Simplex>>.Managed : GridNoiseJob<Simplex2D<Simplex>>.Compiled;
            }
            else
            {
                foreach (var (job, rows) in jobs) for (int y = 0; y < rows; y++) job.Execute(y);
                oceanSampleState = GridNoiseJob<Simplex2D<Simplex>>.Off;
            }
            ou.CopyTo(currentU);
            ov.CopyTo(currentV);
            ot.CopyTo(seaTemp);
            oa.CopyTo(extSeaAnom);
        }
        finally
        {
            fu.Dispose(); fv.Dispose(); ft.Dispose(); fa.Dispose();
            ou.Dispose(); ov.Dispose(); ot.Dispose(); oa.Dispose(); probe.Dispose();
        }
        seaGridMs = (float)gridWatch.Elapsed.TotalMilliseconds;
        oceanMs = (float)watch.Elapsed.TotalMilliseconds;
    }

    int oceanSampleState = -1;

    float[] extSeaAnom; // 2c: T − T0 °C at every climate cell (null with Currents off)
    float seaGridMs;

    // 2c: what the coast of the view reads from the sea. Land within SeaCoastCells (cells of
    // a 256 map) of the sea: how many, the mean anomaly, the coldest and how many under −2 °C.
    const float SeaCoastCells = 4f;
    int seaCoastLand, seaCoastCold;
    float seaCoastMean, seaCoastMin;

    void SeaCoastStats()
    {
        seaCoastLand = seaCoastCold = 0;
        seaCoastMean = seaCoastMin = 0f;
        if (extSeaAnom == null) return;
        int n = sampleN, w = extW;
        float toUnits = CoastUnits / n;
        double sum = 0.0;
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                int i = y * n + x;
                if (values[i] < threshold || coast[i] * toUnits > SeaCoastCells) continue;
                float a = extSeaAnom[(y + mB) * w + x + mL];
                sum += a;
                seaCoastMin = Mathf.Min(seaCoastMin, a);
                if (a < -2f) seaCoastCold++;
                seaCoastLand++;
            }
        seaCoastMean = seaCoastLand > 0 ? (float)(sum / seaCoastLand) : 0f;
    }

    string OceanSummary()
    {
        var r = oceanRegions.Count > 0 ? oceanRegions[0] : null;
        if (r == null) return "ocean: not computed yet";
        return $"ocean regions {oceanRegionsUsed} (16×16 view widths each, {OceanN}×{OceanN} cells, {OceanPasses} passes); " +
               $"last new region {r.ms:0.0} ms ({GridNoiseJob<Simplex2D<Simplex>>.StateName(r.state)}), sea {100f * r.seaShare:0}%, " +
               $"ω {r.omega}{(r.omega != OceanOmegas[0] ? " (retried)" : "")}, residual {r.residual:0.0000}{(r.residual < OceanTolerance ? "" : " NOT CONVERGED")}, fastest {r.fastest:0.0}× the interior speed; " +
               $"sea temp: memory {F(heatMemory)}, {r.heatRounds} rounds{(r.heatChange < SeaHeatTolerance ? "" : " NOT SETTLED")}, {r.heatMs:0.0} ms ({GridNoiseJob<Simplex2D<Simplex>>.StateName(r.heatState)}), " +
               $"mean |T − T0| {r.heatAnomaly:0.00} °C; upwelling {F(upwelling)}: {r.upCells} coast cells, mean T − T0 {r.upAnomaly:0.00} °C, coldest {r.upColdest:0.00} °C; " +
               $"view {oceanMs:0.0} ms (sampling {seaGridMs:0.0} ms, {GridNoiseJob<Simplex2D<Simplex>>.StateName(oceanSampleState)}; climate grid {extW}×{extH}); " +
               $"coast land (≤{SeaCoastCells:0} cells): {seaCoastLand}, sea T − T0 mean {seaCoastMean:0.00} °C, coldest {seaCoastMin:0.00} °C, under −2 °C: {seaCoastCold}";
    }

    void OceanControls()
    {
        GUILayout.BeginHorizontal(GUILayout.Height(30f));
        ocean = GUILayout.Toggle(ocean, new GUIContent(" Currents",
            "Ocean currents from the planet's winds (Stommel 1948), on a coarse grid fixed to the world. Show them with the Currents layer."),
            GUILayout.Width(110f));
        GUILayout.EndHorizontal();
        GUI.enabled = ocean;
        OceanSliders();
        GUI.enabled = true;
        if (ocean)
        {
            GUILayout.Label(OceanSummary());
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(new GUIContent("Copy", "Copy this summary as text."), GUILayout.Width(90f), GUILayout.Height(30f)))
                GUIUtility.systemCopyBuffer = OceanSummary();
            if (GUILayout.Button(new GUIContent("Zoom out ×4", "The currents from 4 times higher up: 4×4 view widths around the view, full screen."), GUILayout.Width(140f), GUILayout.Height(30f)))
                MakeBigPicture(4);
            if (GUILayout.Button(new GUIContent("World ×16", "The whole latitude range, pole to pole: 16×16 view widths, centred on the view's longitude, full screen. The first time it may solve a few more ocean regions."), GUILayout.Width(140f), GUILayout.Height(30f)))
                MakeBigPicture(WorldScale);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal(); // its own row: three buttons do not fit on a narrow panel
            BigColourButton();
            GUILayout.EndHorizontal();
        }
    }

    // 2b: the ocean's sliders, shared by the Climate page and the ×4 / ×16 pictures (there
    // they redraw the picture as they move). Every slider that changes the big picture
    // goes here.
    void OceanSliders()
    {
        oceanWidth = R2(Slider("West current", oceanWidth, 0.1f, 1f, "0.00",
            "Width of the fast current on the west side of each ocean, in view widths (friction). Narrow = faster and closer to the coast. Below 0.25 it is thinner than two ocean cells."));
        heatMemory = R2(Slider("Heat memory", heatMemory, 0.25f, 8f, "0.00",
            "How far the sea carries its temperature before it takes the one of its latitude, in view widths at the slow interior speed (a view is about 11° of latitude). Fast currents carry it farther: the west current is many times faster. Low = straight isotherms; high = warm water far toward the poles, cold toward the equator."));
        upwelling = R2(Slider("Upwelling", upwelling, 0f, 1f, "0.00",
            "Cold water rising on the west coast of the continents, where the current runs toward the equator along the coast (Humboldt, Benguela, California). 0 = none; 0.5 = about halfway to 10 °C at the interior speed."));
    }

    static readonly string[] BigColourNames = { "Speed", "Gyres", "Sea temp", "Temp anomaly", "Biomes" };

    void BigColourButton()
    {
        if (GUILayout.Button(new GUIContent($"Colour: {BigColourNames[(int)bigColour]}",
                "How the ×4 and ×16 pictures paint the sea (tap to change). Speed: dark = still, orange = four times the interior speed and more. " +
                "Gyres: red turns one way, blue the other, lines follow the water (stream function); each gyre is a closed loop. " +
                "Sea temp: sea surface temperature, blue −2 °C to red 28 °C, a line every 2 °C (isotherms). Temp anomaly: how much warmer (red) or colder (blue) than its latitude, white = the same, full colour at ±6 °C, a line every 1 °C. Biomes: the land's biomes from the same climate as the view, coarser (always the sweep wind, no beach), the sea as in Temp anomaly."),
                GUILayout.Width(160f), GUILayout.Height(30f)))
            bigColour = (BigColour)(((int)bigColour + 1) % BigColourNames.Length);
    }

    // v6 (2a): the currents from four times higher up: 4 × 4 view widths centred on the view,
    // land in grey, the view outlined. The ocean grid is fixed to the world and coarse, so
    // this costs only the heights of the bigger square (the regions are cached). Nothing
    // else (climate, biomes) is computed for it.
    // Continents step: a second size, the whole latitude range (pole to pole = 16 views).
    const int BigN = 512, WorldScale = 16;
    int bigScale = 4;
    // continents step: paint the stream function instead of the speed; 2b: or the sea temperature
    enum BigColour { Speed, Gyres, SeaTemp, Anomaly, Biomes } // 2c: Biomes
    BigColour bigColour;
    Texture2D bigPicture;
    Vector2 bigOrigin;
    float bigMs;
    // 2b: the sliders inside the big picture redraw it as they move. The heights of the
    // picture do not depend on them, so they are kept (by height settings, place and size);
    // the view behind is recomputed once, when the picture closes.
    float[] bigHeights;
    string bigHeightsKey;
    bool bigDirty, viewStale;

    void MakeBigPicture(int scale)
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        bigScale = scale;
        int n = BigN, cells = n * n;
        float step = (float)bigScale / n;
        bigOrigin = offset - 0.5f * (bigScale - 1) * Vector2.one;
        if (bigScale == WorldScale) bigOrigin.y = -LatitudeSpan; // pole to pole
        string heightsKey = $"{HeightKey()}|{F(bigOrigin.x)}|{F(bigOrigin.y)}|{bigScale}";
        if (bigHeights == null || bigHeightsKey != heightsKey)
        {
            var src = new float[FieldCount][];
            int keepState = burstState;
            foreach (int k in HeightFields())
            {
                src[k] = new float[cells];
                SampleInto(src[k], GetField(k), FieldSeed(k), n, bigOrigin.x + 0.5f * step, bigOrigin.y + 0.5f * step, step);
            }
            burstState = keepState;
            bigHeights = new float[cells];
            CombineHeight(bigHeights, src);
            bigHeightsKey = heightsKey;
        }
        var heights = bigHeights;

        int first = Mathf.FloorToInt(bigOrigin.x / OceanStride);
        int last = Mathf.FloorToInt((bigOrigin.x + bigScale) / OceanStride) + 1;
        var regions = new OceanRegion[last - first + 1];
        for (int k = 0; k < regions.Length; k++) regions[k] = GetOceanRegion(first + k);
        var u = new float[cells];
        var v = new float[cells];
        var field = bigColour != BigColour.Speed ? new float[cells] : null; // psi or temperature
        var pixels = new Color32[cells];
        for (int x = 0; x < n; x++)
        {
            float wx = bigOrigin.x + (x + 0.5f) * step;
            float t = wx / OceanStride;
            int k0 = Mathf.FloorToInt(t);
            float b = t - k0;
            for (int y = 0; y < n; y++)
            {
                float wy = bigOrigin.y + (y + 0.5f) * step;
                SampleRegion(regions[k0 - first], wx, wy, out float u0, out float v0);
                SampleRegion(regions[k0 + 1 - first], wx, wy, out float u1, out float v1);
                int i = y * n + x;
                u[i] = Mathf.Lerp(u0, u1, b);
                v[i] = Mathf.Lerp(v0, v1, b);
                var r0 = regions[k0 - first];
                var r1 = regions[k0 + 1 - first];
                if (bigColour == BigColour.Gyres)
                    field[i] = Mathf.Lerp(SampleRegionPsi(r0, wx, wy), SampleRegionPsi(r1, wx, wy), b);
                else if (bigColour == BigColour.SeaTemp || bigColour == BigColour.Anomaly || bigColour == BigColour.Biomes)
                    field[i] = Mathf.Lerp(SampleRegionField(r0, r0.temp, wx, wy), SampleRegionField(r1, r1.temp, wx, wy), b);
                else
                    pixels[i] = CurrentColour(heights[i], u[i], v[i]);
            }
        }
        if (bigColour == BigColour.Gyres) GyreColours(pixels, n, heights, field);
        else if (bigColour == BigColour.SeaTemp) SeaTempColours(pixels, n, heights, field, 2f);
        else if (bigColour == BigColour.Anomaly) AnomalyColours(pixels, n, heights, field, bigOrigin.y, step);
        else if (bigColour == BigColour.Biomes)
        {
            AnomalyColours(pixels, n, heights, field, bigOrigin.y, step); // the sea as in Temp anomaly
            BigBiomes(pixels, heights);
        }
        CurrentArrows(pixels, n, n / 32, heights, u, v);
        if (bigPicture == null)
            bigPicture = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        bigPicture.SetPixels32(pixels);
        bigPicture.Apply();
        bigMs = (float)watch.Elapsed.TotalMilliseconds;
    }

    // Continents step: the stream function. Water flows along the lines of equal psi, so
    // each gyre is a closed loop; the sign says which way it turns (red: psi > 0, blue:
    // psi < 0), the strength is how deep the colour is. 12 lines between 0 and the largest
    // |psi| of the picture. Land in grey, as in the speed colouring.
    void GyreColours(Color32[] pixels, int n, float[] heights, float[] psi)
    {
        float max = 1e-6f;
        for (int i = 0; i < psi.Length; i++)
            if (heights[i] < threshold) max = Mathf.Max(max, Mathf.Abs(psi[i]));
        float spacing = max / 12f;
        var pale = new Color32(235, 235, 230, 255);
        var red = new Color32(200, 60, 45, 255);
        var blue = new Color32(40, 90, 190, 255);
        var line = new Color32(20, 20, 25, 255);
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                int i = y * n + x;
                if (heights[i] >= threshold) { pixels[i] = Color32.Lerp(Wall, LandTint, 0.35f); continue; }
                float p = psi[i];
                var c = Color32.Lerp(pale, p >= 0f ? red : blue, Mathf.Sqrt(Mathf.Abs(p) / max));
                int band = Mathf.FloorToInt(p / spacing);
                bool edge = (x + 1 < n && heights[i + 1] < threshold && Mathf.FloorToInt(psi[i + 1] / spacing) != band) ||
                            (y + 1 < n && heights[i + n] < threshold && Mathf.FloorToInt(psi[i + n] / spacing) != band);
                pixels[i] = edge ? Color32.Lerp(c, line, 0.6f) : c;
            }
    }

    // 2b: sea surface temperature, blue (−2 °C) to pale yellow to red (28 °C), the same ramp
    // as the land's Temperature layer, with a dark line every `step` °C (isotherms): where
    // they bend, the currents carry warm or cold water. Land in grey.
    static Color32 SeaTempColour(float t) =>
        Ramp3(Cold, Mild, Hot, Mathf.Clamp01((t - SeaColdest) / (SeaWarmest - SeaColdest)));

    void SeaTempColours(Color32[] pixels, int n, float[] heights, float[] temp, float step)
    {
        var line = new Color32(20, 20, 25, 255);
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                int i = y * n + x;
                if (heights[i] >= threshold) { pixels[i] = Color32.Lerp(Wall, LandTint, 0.35f); continue; }
                var c = SeaTempColour(temp[i]);
                int band = Mathf.FloorToInt(temp[i] / step);
                bool edge = (x + 1 < n && heights[i + 1] < threshold && Mathf.FloorToInt(temp[i + 1] / step) != band) ||
                            (y + 1 < n && heights[i + n] < threshold && Mathf.FloorToInt(temp[i + n] / step) != band);
                pixels[i] = edge ? Color32.Lerp(c, line, 0.5f) : c;
            }
    }

    // 2b step 3: the temperature minus the base of its latitude (T − T0): white = the same,
    // red warmer, blue colder, full colour at ±6 °C, a line every 1 °C. The latitude bands
    // vanish, so what the currents and the upwelling do stands out.
    void AnomalyColours(Color32[] pixels, int n, float[] heights, float[] temp, float originY, float step)
    {
        var white = new Color32(245, 245, 240, 255);
        var line = new Color32(20, 20, 25, 255);
        var anomaly = new float[pixels.Length];
        for (int y = 0; y < n; y++)
        {
            float t0 = SeaBaseTemp(originY + (y + 0.5f) * step);
            for (int x = 0; x < n; x++) anomaly[y * n + x] = temp[y * n + x] - t0;
        }
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                int i = y * n + x;
                if (heights[i] >= threshold) { pixels[i] = Color32.Lerp(Wall, LandTint, 0.35f); continue; }
                float a = anomaly[i];
                var c = Color32.Lerp(white, a >= 0f ? Hot : Cold, Mathf.Clamp01(Mathf.Abs(a) / 6f));
                int band = Mathf.FloorToInt(a);
                bool edge = (x + 1 < n && heights[i + 1] < threshold && Mathf.FloorToInt(anomaly[i + 1]) != band) ||
                            (y + 1 < n && heights[i + n] < threshold && Mathf.FloorToInt(anomaly[i + n]) != band);
                pixels[i] = edge ? Color32.Lerp(c, line, 0.5f) : c;
            }
    }

    // ---------------------------------------------------------------------------------
    // 2c: the land's biomes in the big picture, to see the whole coast of a continent at
    // once (an "Atacama" next to every "Humboldt"). The view's climate jobs on the picture's
    // grid: rank, coast distance, the wind sweep and ViewClimateJob, every distance in cells
    // of a 256 map as in the view, so one pixel (2 cells at ×4, 8 at ×16) is a coarse sample
    // of the same model. Simplified: always the sweep (the flow is too costly at this size),
    // no margins (the air enters the picture as sea air), no beach. Kept by its key.
    // ---------------------------------------------------------------------------------
    float[] bigTemp, bigMoist;
    string bigClimateKey;
    float bigClimateMs;

    string BigClimateKey() =>
        $"{bigHeightsKey}|{F(threshold)}|{FieldKey(FTemp)}|{FieldKey(FMoist)}|" +
        $"{F(baseTemperature)}|{F(lapseRate)}|{F(latitudeEffect)}|{F(coastModeration)}|{F(tempNoiseAmplitude)}|" +
        $"{F(moistureNoiseAmplitude)}|{F(coastalMoistureBonus)}|{F(coastDecayRate)}|" +
        $"{wind}|{F(windAngle)}|{F(windStrength)}|{F(windLift)}|{F(windDry)}|{F(windEvaporation)}";

    void BigBiomes(Color32[] pixels, float[] heights)
    {
        int n = BigN, cells = n * n;
        string key = BigClimateKey();
        if (bigTemp == null || bigClimateKey != key)
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            float step = (float)bigScale / n;
            var src = new float[2][];
            int keepState = burstState;
            for (int k = 0; k < 2; k++)
            {
                src[k] = new float[cells];
                int f = ClimateFields[k];
                SampleInto(src[k], GetField(f), FieldSeed(f), n, bigOrigin.x + 0.5f * step, bigOrigin.y + 0.5f * step, step);
            }
            burstState = keepState;
            float[] source = CutSource;
            int below = LowerBound(source, threshold);
            float toUnits = CoastUnits * bigScale / n;
            var nHeights = new NativeArray<float>(heights, Allocator.TempJob);
            var sorted = new NativeArray<float>(source, Allocator.TempJob);
            var rank = new NativeArray<float>(cells, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            var dist = new NativeArray<float>(cells, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            var air = new NativeArray<float>(wind ? cells : 1, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            var tn = new NativeArray<float>(src[0], Allocator.TempJob);
            var mn = new NativeArray<float>(src[1], Allocator.TempJob);
            var coastV = new NativeArray<float>(cells, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            var tempV = new NativeArray<float>(cells, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            var moistV = new NativeArray<float>(cells, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            var airV = new NativeArray<float>(wind ? cells : 1, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            var probe = new NativeArray<int>(1, Allocator.TempJob);
            var rankProbe = new NativeArray<int>(1, Allocator.TempJob);
            try
            {
                var hRank = new RankJob
                {
                    heights = nHeights, sorted = sorted, rank = rank, w = n,
                    below = below, landCount = sorted.Length - below, probe = rankProbe
                }.ScheduleParallel(n, 8, default);
                var hCols = new CoastColumnsJob
                {
                    heights = nHeights, dist = dist, w = n, h = n, threshold = threshold, probe = probe
                }.ScheduleParallel(n, 8, default);
                var hRows = new CoastRowsJob { dist = dist, w = n, probe = probe }.ScheduleParallel(n, 8, hCols);
                var deps = JobHandle.CombineDependencies(hRank, hRows);
                if (wind)
                {
                    float a = windAngle * Mathf.Deg2Rad;
                    float wx = Mathf.Cos(a), wy = Mathf.Sin(a);
                    bool xMajor = Mathf.Abs(wx) >= Mathf.Abs(wy);
                    float major = xMajor ? wx : wy, minor = xMajor ? wy : wx;
                    float slope = minor / Mathf.Abs(major);
                    float stepLen = Mathf.Sqrt(1f + slope * slope) * toUnits;
                    deps = new WindJob
                    {
                        heights = nHeights, rank = rank, air = air, w = n, h = n, threshold = threshold,
                        xMajor = xMajor, dir = major >= 0f ? 1 : -1, slope = slope,
                        keepFlat = Mathf.Exp(-windDry * stepLen), pickUp = 1f - Mathf.Exp(-windEvaporation * stepLen),
                        lift = windLift * LiftScale, probe = probe
                    }.Schedule(deps);
                }
                new ViewClimateJob
                {
                    extCoast = dist, extRank = rank, extAir = air, tn = tn, mn = mn,
                    coast = coastV, temp = tempV, moist = moistV, air = airV,
                    n = n, w = n, mL = 0, mB = 0, wind = wind, toUnits = toUnits,
                    offsetY = bigOrigin.y, latitudeSpan = LatitudeSpan, span = bigScale,
                    baseTemperature = baseTemperature, lapseRate = lapseRate, latitudeEffect = latitudeEffect,
                    coastModeration = coastModeration, tempNoiseAmplitude = tempNoiseAmplitude,
                    moistureNoiseAmplitude = moistureNoiseAmplitude, coastalMoistureBonus = coastalMoistureBonus,
                    coastDecayRate = coastDecayRate, windStrength = windStrength, probe = probe
                }.ScheduleParallel(n, 8, deps).Complete();
                bigTemp = Ensure(bigTemp, cells);
                bigMoist = Ensure(bigMoist, cells);
                tempV.CopyTo(bigTemp);
                moistV.CopyTo(bigMoist);
            }
            finally
            {
                nHeights.Dispose(); sorted.Dispose(); rank.Dispose(); dist.Dispose(); air.Dispose();
                tn.Dispose(); mn.Dispose(); coastV.Dispose(); tempV.Dispose(); moistV.Dispose(); airV.Dispose();
                probe.Dispose(); rankProbe.Dispose();
            }
            bigClimateKey = key;
            bigClimateMs = (float)watch.Elapsed.TotalMilliseconds;
        }
        for (int i = 0; i < cells; i++)
            if (heights[i] >= threshold) pixels[i] = BiomeColours[(int)BiomeTable.Lookup(bigTemp[i], bigMoist[i])];
    }

    // Full screen until a tap on the picture, Back or Escape. 2b: with the ocean's sliders
    // beside it (landscape) or under it (portrait); moving one redraws the picture.
    void DrawBigPicture(float width, float height, float margin)
    {
        const float panelWidth = 360f, panelHeight = 250f;
        Rect r, panel;
        if (width - height >= panelWidth)
        {
            float s = Mathf.Min(width - panelWidth - 3f * margin, height - 2f * margin);
            r = new Rect(margin, (height - s) * 0.5f, s, s);
            panel = new Rect(r.xMax + margin, margin, width - r.xMax - 2f * margin, height - 2f * margin);
        }
        else
        {
            float s = Mathf.Min(width - 2f * margin, height - panelHeight - 3f * margin);
            r = new Rect((width - s) * 0.5f, margin, s, s);
            panel = new Rect(margin, r.yMax + margin, width - 2f * margin, height - r.yMax - 2f * margin);
        }
        float side = r.width;
        GUI.DrawTexture(r, bigPicture);
        // the view, where it is inside the picture (texture rows go up, screen rows down)
        float w = side / bigScale;
        var view = new Rect(r.x + (offset.x - bigOrigin.x) * w,
                            r.yMax - (offset.y - bigOrigin.y + 1f) * w, w, w);
        var line = new Color(1f, 1f, 1f, 0.9f);
        DrawRect(new Rect(view.x, view.y, view.width, 2f), line);
        DrawRect(new Rect(view.x, view.yMax - 2f, view.width, 2f), line);
        DrawRect(new Rect(view.x, view.y, 2f, view.height), line);
        DrawRect(new Rect(view.xMax - 2f, view.y, 2f, view.height), line);
        string name = $"{BigColourNames[(int)bigColour]} ×{bigScale}: lat {LatitudeName(bigOrigin.y)} – {LatitudeName(bigOrigin.y + bigScale)}, " +
                      $"the view outlined; {bigMs:0} ms" + (bigColour == BigColour.Biomes ? $" (climate {bigClimateMs:0} ms)" : "");
        DrawRect(new Rect(r.x, r.y, Mathf.Min(8.5f * name.Length + 8f, r.width), 22f), new Color(0f, 0f, 0f, 0.5f));
        GUI.Label(new Rect(r.x + 4f, r.y + 1f, r.width - 4f, 22f), name);

        // The sliders first: a slider uses its own mouse-up, so releasing it never closes.
        DrawRect(panel, new Color(0f, 0f, 0f, 0.35f));
        GUILayout.BeginArea(new Rect(panel.x + 8f, panel.y + 8f, panel.width - 16f, panel.height - 16f));
        bool close = false;
        bool before = GUI.changed;
        GUI.changed = false;
        GUILayout.BeginHorizontal();
        BigColourButton();
        if (GUILayout.Button(new GUIContent("Back", "Back to the view (or tap the picture, or Esc)."), GUILayout.Width(90f), GUILayout.Height(30f)))
            close = true;
        GUILayout.EndHorizontal();
        OceanSliders();
        if (GUI.changed && !close) { bigDirty = true; viewStale = true; }
        // 2b: other continents in one tap. A new seed moves the sea level too (the world's
        // cuts), so the whole view is recomputed first (dirty), then the picture.
        GUI.changed = false;
        GUILayout.BeginHorizontal(GUILayout.Height(30f));
        GUILayout.Label(new GUIContent($"Seed {seed}", "The map's seed: other continents, same settings."), GUILayout.Width(110f));
        if (GUILayout.Button("−", GUILayout.Width(50f), GUILayout.Height(30f))) seed--;
        if (GUILayout.Button("+", GUILayout.Width(50f), GUILayout.Height(30f))) seed++;
        if (GUILayout.Button("Random", GUILayout.Width(90f), GUILayout.Height(30f))) seed = UnityEngine.Random.Range(0, 100000); // UI only
        GUILayout.EndHorizontal();
        if (GUI.changed && !close) { dirty = true; bigDirty = true; }
        GUILayout.Label(OceanSummary());
        GUI.changed = before;
        GUILayout.EndArea();

        Event e = Event.current;
        if ((e.type == EventType.MouseUp && r.Contains(e.mousePosition)) || (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape))
        {
            close = true;
            e.Use();
        }
        if (close)
        {
            Destroy(bigPicture);
            bigPicture = null;
            bigDirty = false;
            if (viewStale) { viewStale = false; dirty = true; } // the view catches up once
        }
    }

    // Burst check, ocean part: the view's first region in plain C# and with Burst.
    string OceanCheck()
    {
        if (!(colouring == Colouring.Biomes && ocean)) return "";
        int index = Mathf.FloorToInt(offset.x / OceanStride);
        var a = ComputeOceanRegion(index, false);
        var b = ComputeOceanRegion(index, true);
        var c = ComputeOceanRegion(index, true);
        float worst = 0f;
        int differ = 0, repeat = 0;
        for (int i = 0; i < a.u.Length; i++)
        {
            float d = Mathf.Max(Mathf.Abs(a.u[i] - b.u[i]), Mathf.Abs(a.v[i] - b.v[i]));
            if (d > 0f) differ++;
            worst = Mathf.Max(worst, d);
            if (b.u[i] != c.u[i] || b.v[i] != c.v[i]) repeat++;
        }
        return $"\nocean region {index}: managed {a.ms:0.0} ms, Burst {b.ms:0.0} ms; cells that differ {differ} of {a.u.Length}, " +
               $"max |Δ| {worst:G3} (fastest {a.fastest:0.0}); Burst run twice: {repeat} differ" + HeatCheck(b) + SampleCheck();
    }

    // 2c: the sampling of the regions (view currents and temperature, anomaly on the climate
    // grid) in plain C# and with Burst. Leaves the result of the path in use.
    string SampleCheck()
    {
        bool keep = useBurst;
        useBurst = false; Ocean();
        float managedMs = seaGridMs;
        var t0 = (float[])seaTemp.Clone();
        var a0 = (float[])extSeaAnom.Clone();
        useBurst = true; Ocean();
        float burstMs = seaGridMs;
        string state = GridNoiseJob<Simplex2D<Simplex>>.StateName(oceanSampleState);
        float worst = 0f;
        for (int i = 0; i < t0.Length; i++) worst = Mathf.Max(worst, Mathf.Abs(t0[i] - seaTemp[i]));
        float worstA = 0f;
        for (int i = 0; i < a0.Length; i++) worstA = Mathf.Max(worstA, Mathf.Abs(a0[i] - extSeaAnom[i]));
        useBurst = keep;
        if (!keep) Ocean();
        return $"\nsea sampling (view {sampleN}², climate grid {extW}×{extH}): managed {managedMs:0.0} ms, Burst {burstMs:0.0} ms ({state}); " +
               $"max |Δ| sea temp {worst:G3} °C, anomaly {worstA:G3} °C";
    }

    // 2b: the heat solve alone, on the same currents: plain C#, Burst, and Burst again.
    string HeatCheck(OceanRegion r)
    {
        SeaHeat(r, false);
        var a = r.temp;
        float aMs = r.heatMs;
        int aRounds = r.heatRounds;
        SeaHeat(r, true);
        var b = r.temp;
        float bMs = r.heatMs;
        SeaHeat(r, true);
        var c = r.temp;
        float worst = 0f;
        int differ = 0, repeat = 0;
        for (int i = 0; i < a.Length; i++)
        {
            float d = Mathf.Abs(a[i] - b[i]);
            if (d > 0f) differ++;
            worst = Mathf.Max(worst, d);
            if (b[i] != c[i]) repeat++;
        }
        return $"\nsea temp, region {r.index}: managed {aMs:0.0} ms ({aRounds} rounds), Burst {bMs:0.0} ms ({r.heatRounds} rounds); " +
               $"cells that differ {differ} of {a.Length}, max |Δ| {worst:G3} °C; Burst run twice: {repeat} differ";
    }

    // Speed of the current: dark = still, light = the interior (Sverdrup) speed, orange =
    // four times it and more (the western currents); land in grey.
    Color32 CurrentColour(int i) => CurrentColour(values[i], currentU[i], currentV[i]);

    Color32 CurrentColour(float height, float u, float v)
    {
        if (height >= threshold) return Color32.Lerp(Wall, LandTint, 0.35f);
        float s = Mathf.Sqrt(u * u + v * v);
        return s < 1f ? Color32.Lerp(Calm, Breeze, s) : Color32.Lerp(Breeze, Gust, (s - 1f) / 3f);
    }

    void CurrentArrows(Color32[] pixels, int n) => CurrentArrows(pixels, n, n / 16, values, currentU, currentV);

    // Arrows over the sea, as for the wind; length grows with the square root of the speed.
    void CurrentArrows(Color32[] pixels, int n, int every, float[] heights, float[] currentU, float[] currentV)
    {
        int spacing = Mathf.Max(4, every);
        var body = new Color32(255, 255, 255, 255);
        var tail = new Color32(20, 20, 20, 255);
        for (int cy = spacing / 2; cy < n; cy += spacing)
            for (int cx = spacing / 2; cx < n; cx += spacing)
            {
                int i = cy * n + cx;
                if (heights[i] >= threshold) continue;
                float u = currentU[i], v = currentV[i];
                float s = Mathf.Sqrt(u * u + v * v);
                if (s < 1e-3f) continue;
                float len = 0.5f * spacing * Mathf.Clamp(0.5f * Mathf.Sqrt(s), 0.25f, 1f);
                int steps = Mathf.Max(1, Mathf.CeilToInt(len));
                for (int k = 0; k <= steps; k++)
                {
                    float t = len * k / steps;
                    int x = Mathf.RoundToInt(cx + u / s * t), y = Mathf.RoundToInt(cy + v / s * t);
                    if (x < 0 || y < 0 || x >= n || y >= n) break;
                    pixels[y * n + x] = Color32.Lerp(tail, body, (float)k / steps);
                }
            }
    }

    // Burst check, flow part: plain C# against Burst, and Burst twice (the red-black passes
    // run in parallel; each colour only reads the other, so two runs must be identical).
    string FlowCheck()
    {
        if (!FlowOn) return "";
        Flow(false);
        var u0 = (float[])flowU.Clone();
        var v0 = (float[])flowV.Clone();
        var a0 = (float[])flowAir.Clone();
        float managedMs = flowMs;
        Flow(true);
        var u1 = (float[])flowU.Clone();
        var v1 = (float[])flowV.Clone();
        var a1 = (float[])flowAir.Clone();
        float burstMs = flowMs;
        Flow(true);
        float airWorst = 0f;
        int airRepeat = 0;
        for (int i = 0; i < a0.Length; i++)
        {
            airWorst = Mathf.Max(airWorst, Mathf.Abs(a0[i] - a1[i]));
            if (a1[i] != flowAir[i]) airRepeat++;
        }
        float worst = 0f;
        int differ = 0, repeat = 0;
        for (int i = 0; i < u0.Length; i++)
        {
            float d = Mathf.Abs(u0[i] - u1[i]);
            if (d > 0f) differ++;
            worst = Mathf.Max(worst, d);
            if (u1[i] != flowU[i]) repeat++;
        }
        for (int i = 0; i < v0.Length; i++)
        {
            float d = Mathf.Abs(v0[i] - v1[i]);
            if (d > 0f) differ++;
            worst = Mathf.Max(worst, d);
            if (v1[i] != flowV[i]) repeat++;
        }
        string result = $"\nflow {flowW}×{flowH}, {flowIterations} passes: managed {managedMs:0.0} ms, Burst {burstMs:0.0} ms; " +
                        $"faces that differ {differ} of {u0.Length + v0.Length}, max |Δ| {worst:G3}; Burst run twice: {repeat} differ; " +
                        $"flow-grid air max |Δ| {airWorst:G3}, Burst twice: {airRepeat} differ";
        if (!useBurst) Flow(false);
        return result;
    }

    // ---------------------------------------------------------------------------------
    // v4: clouds, first version. A separate low-resolution texture drawn over the panels
    // that show the map, recomputed every frame while on; the terrain caches are not
    // touched. Shape: fractal Simplex3D with time as the third coordinate, so the clouds
    // change without jumps. They drift with the wind direction and sit in world
    // coordinates, so they move with the map when dragging. Cover follows the humidity of
    // the air (windAir, with wind on; the moisture otherwise): rising air forms clouds on
    // the windward slopes and the peaks, and the dry air coming down the lee dissolves them
    // (föhn), so a clear gap stays in the rain shadow while the clouds pass.
    // ---------------------------------------------------------------------------------

    [SerializeField] bool clouds = false;
    [SerializeField, Range(0f, 1f)] float cloudCover = 0.5f;    // more cover = more sky under cloud
    [SerializeField, Range(1, 8)] int cloudSize = 3;            // noise frequency: low = big fronts
    [SerializeField, Range(0f, 1f)] float cloudSpeed = 0.3f;    // drift; 1 = 0.1 view widths per second
    [SerializeField, Range(0f, 1f)] float cloudChange = 0.3f;   // how fast the shapes evolve
    [SerializeField, Range(0f, 1f)] float cloudHumidity = 0.5f; // weight of the air humidity in the cover
    [SerializeField, Range(0f, 1f)] float cloudOpacity = 0.8f;
    [SerializeField, Range(0.01f, 0.2f)] float cloudEdge = 0.04f; // width of the cloud edge, in noise units (was 0.12, fixed)

    // 96² (was 64²): on a 480-unit panel a cloud texel is 5 units instead of 7.5, so the
    // bilinear stretch blurs less. One more octave (5) adds the small detail at the edges.
    const int CloudRes = 96;
    const int CloudOctaves = 5;
    const float CloudShadow = 0.025f;   // shadow offset, in view widths (light from the upper left)

    Texture2D cloudTexture;
    Color32[] cloudPixels;
    NativeArray<float> cloudNoise;   // v5: CloudRes² raw noise values, filled by CloudNoiseJob
    NativeArray<int> cloudState;
    int cloudBurstState = -1;
    float2 cloudDrift;
    float cloudPhase, cloudMs;

    void Clouds()
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f); // no jump after a pause
        float a = windAngle * Mathf.Deg2Rad;
        cloudDrift += new float2(Mathf.Cos(a), Mathf.Sin(a)) * (cloudSpeed * 0.1f * dt);
        cloudPhase += cloudChange * 0.2f * dt;

        if (cloudTexture == null)
        {
            cloudTexture = new Texture2D(CloudRes, CloudRes, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear, // soft edges from a coarse grid
                wrapMode = TextureWrapMode.Clamp
            };
            cloudPixels = new Color32[CloudRes * CloudRes];
        }

        var settings = Settings.Default;
        settings.seed = seed + 7919; // its own noise, tied to the map seed
        settings.frequency = cloudSize;
        settings.octaves = CloudOctaves;
        settings.lacunarity = 2;
        settings.persistence = 0.5f;

        // Humidity source, read at the nearest cell of the view grid (the clouds are coarser).
        bool biomes = colouring == Colouring.Biomes;
        float[] air = biomes ? (wind && windAir != null ? windAir : moist) : null;
        int n = sampleN;
        if (air != null && air.Length != n * n) air = null;

        // v5 (step 5): the noise, the expensive part, in CloudNoiseJob (Burst, every core);
        // with Burst off the same Execute runs in a plain loop. Cover and colour stay here.
        if (!cloudNoise.IsCreated)
        {
            cloudNoise = new NativeArray<float>(CloudRes * CloudRes, Allocator.Persistent);
            cloudState = new NativeArray<int>(1, Allocator.Persistent);
        }
        var job = new CloudNoiseJob
        {
            settings = settings, res = CloudRes, step = 1f / CloudRes,
            offsetX = offset.x, offsetY = offset.y, driftX = cloudDrift.x, driftY = cloudDrift.y,
            phase = cloudPhase, target = cloudNoise, state = cloudState
        };
        if (useBurst)
        {
            job.ScheduleParallel(CloudRes, 4, default).Complete();
            cloudBurstState = cloudState[0];
        }
        else
        {
            for (int y = 0; y < CloudRes; y++) job.Execute(y);
            cloudBurstState = GridNoiseJob<Simplex2D<Simplex>>.Off;
        }

        for (int y = 0; y < CloudRes; y++)
        {
            int vy = Mathf.Min(n - 1, (int)((y + 0.5f) * n / CloudRes));
            for (int x = 0; x < CloudRes; x += 4)
            {
                for (int k = 0; k < 4; k++)
                {
                    float d = cloudNoise[y * CloudRes + x + k] * 0.5f + 0.5f;
                    float cover = cloudCover;
                    if (air != null)
                    {
                        int vx = Mathf.Min(n - 1, (int)((x + k + 0.5f) * n / CloudRes));
                        cover += cloudHumidity * (air[vy * n + vx] - 0.5f);
                    }
                    float edge = 1f - cover;
                    float t = math.smoothstep(edge - cloudEdge, edge + cloudEdge, d);
                    cloudPixels[y * CloudRes + x + k] = new Color32(255, 255, 255, (byte)(255f * t * cloudOpacity));
                }
            }
        }
        cloudTexture.SetPixels32(cloudPixels);
        cloudTexture.Apply();
        cloudMs = (float)watch.Elapsed.TotalMilliseconds;
    }

    // Shadow first (a dark copy, shifted down and right), then the clouds.
    void DrawClouds(Rect r)
    {
        var previous = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.3f);
        GUI.DrawTextureWithTexCoords(r, cloudTexture, new Rect(-CloudShadow, CloudShadow, 1f, 1f));
        GUI.color = previous;
        GUI.DrawTexture(r, cloudTexture);
    }

    string CloudCode() => string.Join(",", F(cloudCover), cloudSize.ToString(Inv), F(cloudSpeed),
                                      F(cloudChange), F(cloudHumidity), F(cloudOpacity), F(cloudEdge));

    void ReadClouds(string text)
    {
        string[] p = text.Split(',');
        if (p.Length != 6 && p.Length != 7) return; // 6 = first version, without the edge
        var v = new float[p.Length];
        for (int i = 0; i < p.Length; i++) if (!TryF(p[i], out v[i])) return;
        clouds = true;
        cloudCover = Mathf.Clamp01(v[0]);
        cloudSize = Mathf.Clamp(Mathf.RoundToInt(v[1]), 1, 8);
        cloudSpeed = Mathf.Clamp01(v[2]);
        cloudChange = Mathf.Clamp01(v[3]);
        cloudHumidity = Mathf.Clamp01(v[4]);
        cloudOpacity = Mathf.Clamp01(v[5]);
        cloudEdge = p.Length == 7 ? Mathf.Clamp(v[6], 0.01f, 0.2f) : 0.12f; // old codes keep their look
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
    // Water outside the grid is not seen: near its edges the coast can look farther. v5: the
    // grid is the view plus the margins, so only water beyond the margin is missed.
    double[] edtF, edtD, edtZ;
    int[] edtV;

    void CoastDistance(float[] src, float[] dst, int w, int h)
    {
        const double Inf = 1e20;
        int len = Mathf.Max(w, h);
        if (edtF == null || edtF.Length < len)
        {
            edtF = new double[len];
            edtD = new double[len];
            edtZ = new double[len + 1];
            edtV = new int[len];
        }

        for (int x = 0; x < w; x++)
        {
            for (int y = 0; y < h; y++) edtF[y] = src[y * w + x] >= threshold ? Inf : 0.0;
            Edt1D(h);
            for (int y = 0; y < h; y++) dst[y * w + x] = (float)edtD[y];
        }
        for (int y = 0; y < h; y++)
        {
            int row = y * w;
            for (int x = 0; x < w; x++) edtF[x] = dst[row + x];
            Edt1D(w);
            for (int x = 0; x < w; x++) dst[row + x] = (float)System.Math.Sqrt(edtD[x]);
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
    // v6: WindFlow goes last; it is a panel layer only (the overlay list maps its entries to
    // Show by index + 1 up to WindAir, and Contours after that).
    enum Show { Map, Height, Temperature, Moisture, Continent, Erosion, Peaks, WindAir, WindFlow, Currents, SeaTemp }
    static readonly string[] ShowNames = { "Map", "Height", "Temperature", "Moisture", "Continent (raw)", "Erosion (raw)", "Peaks (raw)", "Wind air", "Wind flow", "Currents", "Sea temp" };
    static readonly string[] ShowCodes = { "map", "height", "temperature", "moisture", "continent", "erosion", "peaks", "wind", "flow", "currents", "seatemp" };

    // v4: number of panels (1, 2 or 4) and the layer each one shows. A choice that is not
    // available (Temperature with Bands) is kept, and the panel shows the map meanwhile.
    static readonly int[] ViewCounts = { 1, 2, 4 };
    static readonly string[] ViewLabels = { "1", "1×2", "2×2" };
    static readonly Show[] DefaultPanels = { Show.Map, Show.Height, Show.Temperature, Show.Moisture };
    int viewCount = 1;
    readonly Show[] panelShow = (Show[])DefaultPanels.Clone();
    int selectedPanel;

    Show Effective(int k) => Available(panelShow[k]) ? panelShow[k] : Show.Map;

    bool Available(Show s) => s switch
    {
        Show.Temperature or Show.Moisture => colouring == Colouring.Biomes,
        Show.Continent or Show.Erosion or Show.Peaks => heightSource == HeightSource.Splines,
        Show.WindAir => colouring == Colouring.Biomes && wind,
        Show.WindFlow => FlowOn,
        Show.Currents or Show.SeaTemp => colouring == Colouring.Biomes && ocean,
        _ => true
    };

    static readonly Color32 Cold = Hex("2C4A9A"), Mild = Hex("F2E6A0"), Hot = Hex("C0392B");
    static readonly Color32 Dry = Hex("B08850"), Wet = Hex("1E6E6E");

    // Flat colour per band: the map reads by its cuts, not by gradients.
    float paintMs;

    void Paint()
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        if (palettes == null) LoadPalettes();
        if (palettes[paletteIndex].id != paletteId) paletteIndex = FindPalette(paletteId); // code or Inspector
        System.Array.Clear(biomeCount, 0, biomeCount.Length);
        bool counted = false; // the biome shares come from the first panel that shows the map
        for (int k = 0; k < viewCount; k++)
        {
            Show s = Effective(k);
            PaintPanel(k, s, s == Show.Map && !counted);
            if (s == Show.Map) counted = true;
        }
        paintMs = (float)watch.Elapsed.TotalMilliseconds;
    }

    void PaintPanel(int k, Show show, bool countBiomes)
    {
        int n = sampleN;
        var texture = textures[k];
        if (texture == null || texture.width != n)
        {
            if (texture != null) Destroy(texture);
            texture = textures[k] = new Texture2D(n, n, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
        }

        var pixels = panelPixels[k];
        if (pixels == null || pixels.Length != n * n) pixels = panelPixels[k] = new Color32[n * n];
        var bands = Bands;
        bool biomes = colouring == Colouring.Biomes;

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
                        if (countBiomes) biomeCount[(int)bt]++;
                        c = BiomeColours[(int)bt];
                    }
                    else c = bands[band];
                    if (shade) c = Shade(c, i, n);
                    break;
                default: c = FieldColour(show, i); break;
            }
            // v4: overlays go over the map; a Height panel keeps the contour lines (as in v3).
            if (show == Show.Map) c = Overlays(c, i, n, false);
            else if (show == Show.Height) c = Overlays(c, i, n, true);
            pixels[i] = c;
        }
        if (show == Show.WindFlow) FlowArrows(pixels, n);
        if (show == Show.Currents) CurrentArrows(pixels, n);
        if (show == Show.SeaTemp) SeaTempColours(pixels, n, values, seaTemp, 1f); // 2b: a line every 1 °C (a view spans ~11° of latitude)
        texture.SetPixels32(pixels);
        texture.Apply();
    }

    Color32 FieldColour(Show s, int i) => s switch
    {
        Show.Height => Gray(values[i]),
        Show.Temperature => Ramp3(Cold, Mild, Hot, temp[i]),
        Show.Moisture => Color32.Lerp(Dry, Wet, moist[i]),
        Show.WindAir => Color32.Lerp(Dry, Wet, windAir[i]),
        Show.WindFlow => FlowColour(i),
        Show.Currents => CurrentColour(i),
        Show.SeaTemp => default, // painted whole, with its isotherms, after the loop
        _ => Gray(viewField[FCont + (s - Show.Continent)][i])
    };

    // v6: speed of the wind. Dark blue = calm, light = the background speed, orange = up
    // to twice as fast; walls in dark grey; the rest of the land a little greyer, so the
    // coast shows.
    static readonly Color32 Calm = Hex("1B2A4A"), Breeze = Hex("9CC3D5"), Gust = Hex("F2A541");
    static readonly Color32 Wall = Hex("3C3C3C"), LandTint = Hex("8A8A8A");

    Color32 FlowColour(int i)
    {
        if (viewSolid[i] != 0) return Wall;
        float s = Mathf.Sqrt(windU[i] * windU[i] + windV[i] * windV[i]);
        Color32 c = s < 1f ? Color32.Lerp(Calm, Breeze, s) : Color32.Lerp(Breeze, Gust, s - 1f);
        return values[i] >= threshold ? Color32.Lerp(c, LandTint, 0.3f) : c;
    }

    // Arrows painted into the panel texture, one every n / 16 cells: a line from the cell
    // along the wind, as long as half the spacing at the background speed, with a white tip.
    void FlowArrows(Color32[] pixels, int n)
    {
        int spacing = Mathf.Max(4, n / 16);
        var body = new Color32(255, 255, 255, 255);
        var tail = new Color32(20, 20, 20, 255);
        for (int cy = spacing / 2; cy < n; cy += spacing)
            for (int cx = spacing / 2; cx < n; cx += spacing)
            {
                int i = cy * n + cx;
                if (viewSolid[i] != 0) continue;
                float u = windU[i], v = windV[i];
                float s = Mathf.Sqrt(u * u + v * v);
                if (s < 1e-3f) continue;
                float len = 0.5f * spacing * Mathf.Min(s, 2f);
                int steps = Mathf.Max(1, Mathf.CeilToInt(len));
                for (int k = 0; k <= steps; k++)
                {
                    float t = len * k / steps;
                    int x = Mathf.RoundToInt(cx + u / s * t), y = Mathf.RoundToInt(cy + v / s * t);
                    if (x < 0 || y < 0 || x >= n || y >= n) break;
                    pixels[y * n + x] = Color32.Lerp(tail, body, (float)k / steps);
                }
            }
    }

    // ---------------------------------------------------------------------------------
    // v4: overlay layers. A list (up to MaxOverlays) of a layer, an on/off box and an
    // opacity, blended in list order over every panel that shows the map. Contour lines
    // are one more layer: at opacity 1 they look as in v3.
    // ---------------------------------------------------------------------------------

    class Overlay
    {
        public int layer;          // index into OverlayCodes
        public bool on = true;
        public float opacity = 0.5f;
    }

    const int MaxOverlays = 4;
    const int OvContours = 7;
    static readonly string[] OverlayCodes = { "height", "temperature", "moisture", "continent", "erosion", "peaks", "wind", "contours" };
    static readonly string[] OverlayNames = { "Height", "Temperature", "Moisture", "Continent", "Erosion", "Peaks", "Wind air", "Contours" };
    readonly System.Collections.Generic.List<Overlay> overlays = new();

    bool OverlayAvailable(int layer) => layer == OvContours || Available((Show)(layer + 1));

    Color32 Overlays(Color32 c, int i, int n, bool contoursOnly)
    {
        for (int k = 0; k < overlays.Count; k++)
        {
            var o = overlays[k];
            if (!o.on || o.opacity <= 0f || !OverlayAvailable(o.layer)) continue;
            if (o.layer == OvContours) c = Color32.Lerp(c, Contour(c, i, n), o.opacity);
            else if (!contoursOnly) c = Color32.Lerp(c, FieldColour((Show)(o.layer + 1), i), o.opacity);
        }
        return c;
    }

    bool HasContours()
    {
        foreach (var o in overlays) if (o.layer == OvContours) return true;
        return false;
    }

    // Next available layer in the given direction.
    int NextOverlay(int layer, int direction)
    {
        int count = OverlayCodes.Length;
        for (int k = 0; k < count; k++)
        {
            layer = (layer + direction + count) % count;
            if (OverlayAvailable(layer)) return layer;
        }
        return OvContours;
    }

    // ov=height:0.5,-contours:1 ("-" = off).
    string OverlayCode()
    {
        var parts = new string[overlays.Count];
        for (int k = 0; k < parts.Length; k++)
            parts[k] = $"{(overlays[k].on ? "" : "-")}{OverlayCodes[overlays[k].layer]}:{F(R2(overlays[k].opacity))}";
        return string.Join(",", parts);
    }

    void ReadOverlays(string text)
    {
        overlays.Clear();
        foreach (string part in text.Split(','))
        {
            if (overlays.Count == MaxOverlays) break;
            string[] p = part.Trim().Split(':');
            bool on = !p[0].StartsWith("-");
            int layer = System.Array.IndexOf(OverlayCodes, p[0].TrimStart('-'));
            if (layer < 0) continue;
            float opacity = p.Length > 1 && TryF(p[1], out float op) ? Mathf.Clamp01(op) : 0.5f;
            overlays.Add(new Overlay { layer = layer, on = on, opacity = opacity });
        }
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
    // cl= (climate tunables) and sh= (relief shading). v4 adds v= (panels and their layers)
    // and ov= (overlay layers; ct= keeps the contour step), cu= (clouds).
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
            $"NL1;t={TypeCodes[(int)type]};s={seed};f={F(frequency)};o={octaves};l={lacunarity};" +
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
        if (FlowOn) code += $";wf={F(solidAbove)},{flowIterations}"; // v6: flow model
        if (colouring == Colouring.Biomes && ocean) code += $";oc={F(oceanWidth)},{F(heatMemory)},{F(upwelling)}"; // v6 (2a): currents; 2b: heat memory, upwelling
        if (shade) code += $";sh={F(relief)}";
        if (HasContours()) code += $";ct={F(contourStep)}";
        if (overlays.Count > 0) code += ";ov=" + OverlayCode();
        if (clouds) code += ";cu=" + CloudCode();
        if (viewCount > 1 || Effective(0) != Show.Map) code += ";v=" + ViewCode();
        return code;
    }

    // v4: v=4:map,height,moisture,wind. Writes what each panel shows.
    string ViewCode()
    {
        var names = new string[viewCount];
        for (int k = 0; k < viewCount; k++) names[k] = ShowCodes[(int)Effective(k)];
        return $"{viewCount}:{string.Join(",", names)}";
    }

    void ReadView(string text)
    {
        string[] p = text.Split(':');
        if (!int.TryParse(p[0], NumberStyles.Integer, Inv, out int count) ||
            System.Array.IndexOf(ViewCounts, count) < 0) return;
        viewCount = count;
        if (p.Length < 2) return;
        string[] names = p[1].Split(',');
        for (int k = 0; k < Mathf.Min(names.Length, MaxPanels); k++)
        {
            int s = System.Array.IndexOf(ShowCodes, names[k].Trim());
            if (s >= 0) panelShow[k] = (Show)s;
        }
    }

    string FieldCode(int k)
    {
        var f = GetField(k);
        return $"{TypeCodes[(int)f.type]},{F(f.frequency)},{f.octaves},{f.lacunarity},{F(f.gain)}";
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
        overlays.Clear();
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
        windModel = WindModel.Sweep; // v6: a code without wf= keeps the v3 sweep
        solidAbove = 0.5f;
        flowIterations = 200;
        ocean = false; // v6 (2a): a code without oc= has no currents
        oceanWidth = 0.25f;
        heatMemory = 2f; // 2b: a 2a code (oc= with one value) keeps the default
        upwelling = 0.5f;
        // v4: a code without v= opens one panel with the map.
        viewCount = 1;
        DefaultPanels.CopyTo(panelShow, 0);
        selectedPanel = 0;
        clouds = false;
        cloudCover = 0.5f;
        cloudSize = 3;
        cloudSpeed = 0.3f;
        cloudChange = 0.3f;
        cloudHumidity = 0.5f;
        cloudOpacity = 0.8f;
        cloudEdge = 0.04f;
    }

    // Returns null on success, or a short error.
    string ApplyCode(string code)
    {
        if (string.IsNullOrWhiteSpace(code)) return "empty";
        code = code.Trim();
        if (!code.StartsWith("NL1;") && code != "NL1") return "not an NL1 code";

        ResetV3();
        bool sawContours = false, sawOverlays = false;
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
                case "f": if (TryF(val, out float f)) frequency = SnapFrequency(f); break;
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
                case "oc":
                {
                    string[] oc = val.Split(',');
                    if (TryF(oc[0], out float ow)) { ocean = true; oceanWidth = Mathf.Clamp(ow, 0.1f, 1f); }
                    if (oc.Length > 1 && TryF(oc[1], out float hm)) heatMemory = Mathf.Clamp(hm, 0.25f, 8f);
                    if (oc.Length > 2 && TryF(oc[2], out float up)) upwelling = Mathf.Clamp01(up);
                    break;
                }
                case "wf":
                {
                    string[] wf = val.Split(',');
                    if (wf.Length == 2 && TryF(wf[0], out float sa) && TryF(wf[1], out float it))
                    {
                        windModel = WindModel.Flow;
                        solidAbove = Mathf.Clamp01(sa);
                        flowIterations = Mathf.Clamp(Mathf.RoundToInt(it), 10, 600);
                    }
                    break;
                }
                case "v": ReadView(val); break;
                case "ov": ReadOverlays(val); sawOverlays = true; break;
                case "cu": ReadClouds(val); break;
                case "ct":
                    if (TryF(val, out float ct)) { sawContours = true; contourStep = Mathf.Clamp(ct, 0.01f, 0.2f); }
                    break;
                case "sh":
                    if (TryF(val, out float r)) { shade = true; relief = Mathf.Clamp(r, 0f, 3f); }
                    break;
            }
        }
        // A v3 code with ct= and no ov= had the contour lines on: one contour layer at full opacity.
        if (sawContours && !sawOverlays) overlays.Add(new Overlay { layer = OvContours, opacity = 1f });
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
            !TryF(p[1], out float f) ||
            !int.TryParse(p[2], NumberStyles.Integer, Inv, out int o) ||
            !int.TryParse(p[3], NumberStyles.Integer, Inv, out int l) ||
            !TryF(p[4], out float g)) return;
        SetField(k, new NoiseField((NoiseType)t, SnapFrequency(f), Mathf.Clamp(o, 1, 6), Mathf.Clamp(l, 2, 4), Mathf.Clamp01(g)));
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
    static readonly string[] ClimateLabels = { "Temperature", "Moisture", "Wind", "Clouds", "Ocean" };
    static readonly string[] WindModelLabels = { "Sweep", "Flow" };
    static readonly string[] WindModelTips =
    {
        "v3: the wind blows straight over everything, in one direction.",
        "v6: the same wind goes around the high land (Solid above). Show it with the Wind flow layer."
    };

    // v4: tooltips, one per label above (same order). Hover a label (on the phone, hold the
    // finger on it) to read what it is and what it changes.
    static readonly string[] PageTips =
    {
        "The noise fields: type, seed, frequency and octaves of each one.",
        "How the height is built: one noise as it is, or splines (continent, erosion, peaks).",
        "Colours of the map (water and land bands, palette), the split view and the overlay layers.",
        "Land coloured by biome: temperature, moisture, wind and clouds.",
        "A text code with every setting. Copy it to open the same map in the editor or on the phone.",
        "Timings of sampling and painting, the resolution while dragging, and the log export.",
    };
    static readonly string[] CutsTips =
    {
        "Band cuts measured on what you see: the view keeps its share of water as you move, but the coast shifts.",
        "Band cuts measured once on a large patch of the world: moving the view does not change the island.",
    };
    static readonly string[] DragTips =
    {
        "Sample 32×32 while dragging: smoothest, very blocky.",
        "Sample 64×64 while dragging.",
        "Sample 128×128 while dragging: sharper, slower.",
        "Full resolution while dragging: as sharp as at rest, slowest.",
    };
    static readonly string[] FamilyTips =
    {
        "Value noise: random numbers at the grid points, blended. Soft and blocky.",
        "Perlin noise: random slopes at the points of a square grid.",
        "Simplex noise: random slopes on a triangle grid. Fewer grid artefacts than Perlin.",
        "Voronoi F1: distance to the nearest random point. Makes cells.",
    };
    static readonly string[] SimplexTips =
    {
        "Old gradients: the same picture as Perlin with a third of the range (kept to compare).",
        "Perlin gradients on the simplex grid (the default).",
        "Simplex's own gradients.",
    };
    static readonly string[] WaterTips =
    {
        "Water is every cell below a fixed height. The amount of water changes with the seed and the view.",
        "Water is a fixed share of the cells (the sea level is found by percentile).",
    };
    static readonly string[] HeightTips =
    {
        "Height = the Height noise as it is.",
        "Height = C(continent) + E(erosion) · PV(peaks): three noises, each bent by its own curve.",
    };
    static readonly string[] ColouringTips =
    {
        "Land coloured by height bands.",
        "Land coloured by biome, from temperature and moisture (Whittaker table).",
    };
    static readonly string[] ClimateTips =
    {
        "What makes a place warm or cold.",
        "What makes a place wet or dry.",
        "Air that carries moisture from the sea, rains on the slopes it climbs and leaves a rain shadow behind.",
        "Animated clouds over the map. They never change the terrain.",
        "Ocean currents driven by the planet's winds: gyres, fast western currents, slow cold eastern ones. Show them with the Currents layer.",
    };
    static readonly string[] FieldTips =
    {
        "The noise used as height when Shape is One noise.",
        "Continentalness: large shapes, land against sea. Input of the C curve.",
        "Erosion: flat or rugged. Its curve scales the peaks.",
        "Peaks and valleys: the small relief. Input of the PV curve.",
        "Noise added to the temperature, so climate bands are not straight.",
        "Noise added to the moisture.",
    };
    static readonly string[] CurveTips =
    {
        "Curve C: how the continent noise becomes base height.",
        "Curve E: how the erosion noise scales the peaks (low = flat, high = rugged).",
        "Curve PV: how the peaks noise becomes relief.",
    };
    static readonly string[] MapTabTips =
    {
        "Water level, band shares and palette.",
        "The split view (1, 1×2, 2×2), the layer of each panel and the overlays.",
    };
    static readonly string[] ViewTips =
    {
        "One panel.",
        "Two panels. Tap a panel to choose what it shows.",
        "Four panels. Tap a panel to choose what it shows.",
    };

    // Content for a toolbar: the labels with their tips, built once per array.
    static readonly System.Collections.Generic.Dictionary<string[], GUIContent[]> tipped = new();

    static GUIContent[] Tipped(string[] labels, string[] tips)
    {
        if (tipped.TryGetValue(labels, out var contents)) return contents;
        contents = new GUIContent[labels.Length];
        for (int i = 0; i < labels.Length; i++) contents[i] = new GUIContent(labels[i], tips[i]);
        return tipped[labels] = contents;
    }

    GUIStyle tipStyle;
    const float TouchTipDelay = 1f; // seconds the finger must stay down before a tip shows
    float touchStart = -1f;

    // Drawn last, over everything. With a mouse it follows the hover; on a touch screen it
    // shows only while the finger is down (after a tap the pointer stays where it was, and a
    // tip left open would cover the controls).
    void DrawTooltip(float width, float height)
    {
        if (Event.current.type != EventType.Repaint) return;
        if (Application.isMobilePlatform)
        {
            // Hold about a second: a quick tap on a tab or a slider shows nothing. Timed on
            // every repaint (with or without a tip), so a new touch never inherits an old start.
            if (Input.touchCount == 0) { touchStart = -1f; return; }
            if (touchStart < 0f || Input.GetTouch(0).phase == TouchPhase.Began) touchStart = Time.unscaledTime;
            if (Time.unscaledTime - touchStart < TouchTipDelay) return;
        }
        string tip = GUI.tooltip;
        if (string.IsNullOrEmpty(tip)) return;
        tipStyle ??= new GUIStyle(GUI.skin.label) { wordWrap = true, padding = new RectOffset(8, 8, 6, 6) };
        tipStyle.normal.textColor = Color.white;
        var content = new GUIContent(tip);
        float w = Mathf.Min(320f, width - 16f);
        float h = tipStyle.CalcHeight(content, w);
        Vector2 m = Event.current.mousePosition;
        float x = Mathf.Clamp(m.x + 12f, 8f, width - w - 8f);
        float y = m.y - h - 16f;          // above the pointer: a finger would cover it below
        if (y < 8f) y = Mathf.Min(m.y + 28f, height - h - 8f);
        var r = new Rect(x, y, w, h);
        DrawRect(new Rect(r.x - 1f, r.y - 1f, r.width + 2f, r.height + 2f), new Color(1f, 1f, 1f, 0.5f));
        DrawRect(r, new Color(0.08f, 0.09f, 0.11f, 0.96f));
        GUI.Label(r, content, tipStyle);
    }

    NoiseType lastSimplex = NoiseType.SimplexPerlinOnSimplex;
    int selectedBand = BandCount - 1;
    int selectedField = FHeight;
    int selectedCurve, selectedPoint = -1;
    bool curveDragging;
    int climateTab;
    string codeText = "", codeStatus = "";
    string offsetXText = "", offsetYText = "";

    // Drag state. imageRect covers all the panels; panelSide converts a drag to view widths.
    Rect imageRect;
    readonly Rect[] panelRects = new Rect[MaxPanels];
    float panelSide = 1f;
    bool dragging, moved;
    Vector2 lastPointer;
    float dragDistance; // a press that barely moves is a tap: it picks the panel

    // v6 UI: one panel over the whole screen for a while (right click, or a double tap).
    // -1 = the normal view. The other panels keep painting; only the layout changes.
    int focusPanel = -1;
    int lastTapPanel = -1;
    float lastTapTime = -1f;
    const float DoubleTapSeconds = 0.35f;

    const float PanelGap = 4f;

    void OnGUI()
    {
        if (textures[0] == null) return;

        float scale = Screen.height / VirtualHeight;
        GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
        float width = Screen.width / scale, height = VirtualHeight;
        const float margin = 16f;
        if (bigPicture != null)
        {
            DrawBigPicture(width, height, margin);
            return;
        }
        if (focusPanel >= viewCount) focusPanel = -1;
        if (focusPanel >= 0)
        {
            DrawFocus(width, height, margin);
            HandleDrag();
            return;
        }
        const float viewExtras = 8f + 70f + 4f + 60f + 8f; // histogram (70) and numbers (60)

        Rect controls;
        if (width < height * 0.9f)
        {
            // Portrait: view on top, controls underneath. The controls get their height
            // first; the panels take what is left.
            const float controlsHeight = 350f;
            Grid(width - 2f * margin, height - 2f * margin - viewExtras - controlsHeight, out int cols, out float side);
            float gridWidth = GridWidth(cols, side);
            float bottom = DrawView((width - gridWidth) * 0.5f, margin, cols, side);
            controls = new Rect(margin, bottom + 8f, width - 2f * margin, height - bottom - 8f - margin);
        }
        else
        {
            // Landscape: view on the left, controls in a column on the right. One panel keeps
            // the v3 size; several panels may use the full height.
            int cols = 1;
            float side = Mathf.Min(height * 0.6f, width * 0.5f);
            if (viewCount > 1)
                Grid(Mathf.Min(width * 0.5f, width - 3f * margin - 380f), height - 2f * margin - viewExtras, out cols, out side);
            DrawView(margin, margin, cols, side);
            float x = 2f * margin + GridWidth(cols, side);
            controls = new Rect(x, margin, Mathf.Min(420f, width - x - margin), height - 2f * margin);
        }

        HandleDrag();
        DrawControls(controls);
        DrawTooltip(width, height);
    }

    // v4: the largest square panels that fit in w × h. Two panels go side by side or one
    // above the other, whichever leaves them bigger; four make a 2×2.
    void Grid(float w, float h, out int cols, out float side)
    {
        float Side(int c)
        {
            int r = (viewCount + c - 1) / c;
            return Mathf.Min((w - PanelGap * (c - 1)) / c, (h - PanelGap * (r - 1)) / r);
        }
        cols = viewCount switch { 1 => 1, 2 => Side(2) >= Side(1) ? 2 : 1, _ => 2 };
        side = Mathf.Max(32f, Side(cols));
    }

    static float GridWidth(int cols, float side) => cols * side + PanelGap * (cols - 1);

    // One finger (or the mouse) on the image moves the view; the point under the finger
    // stays under it. IMGUI turns touches into mouse events, already in GUI space.
    // v4: all panels move together; a tap on a panel picks it for the Layer control.
    void HandleDrag()
    {
        Event e = Event.current;
        switch (e.type)
        {
            case EventType.MouseDown:
                if (!imageRect.Contains(e.mousePosition)) return;
                if (e.button == 1) // right click: this panel over the whole screen, or back
                {
                    int k = PanelAt(e.mousePosition);
                    if (k >= 0) ToggleFocus(k);
                    e.Use();
                    return;
                }
                dragging = true;
                lastPointer = e.mousePosition;
                dragDistance = 0f;
                GUIUtility.keyboardControl = 0; // leave any text field
                e.Use();
                break;
            case EventType.MouseDrag:
                if (!dragging) return;
                Vector2 d = e.mousePosition - lastPointer;
                lastPointer = e.mousePosition;
                dragDistance += d.magnitude;
                offset.x -= d.x / panelSide;
                offset.y += d.y / panelSide; // GUI y grows down, texture y grows up
                moved = true;
                e.Use();
                break;
            case EventType.MouseUp:
                if (!dragging) return;
                dragging = false;
                if (dragDistance < 8f)
                {
                    int k = PanelAt(e.mousePosition);
                    if (k >= 0)
                    {
                        selectedPanel = k;
                        // Two taps on the same panel: whole screen, or back (the touch screen's right click).
                        if (k == lastTapPanel && Time.unscaledTime - lastTapTime < DoubleTapSeconds)
                        {
                            ToggleFocus(k);
                            lastTapPanel = -1;
                        }
                        else
                        {
                            lastTapPanel = k;
                            lastTapTime = Time.unscaledTime;
                        }
                    }
                }
                offset.x = Mathf.Round(offset.x * 1000f) / 1000f; // readable codes
                offset.y = Mathf.Round(offset.y * 1000f) / 1000f;
                dirty = true;
                e.Use();
                break;
        }
    }

    int PanelAt(Vector2 point)
    {
        for (int k = 0; k < viewCount; k++)
            if ((focusPanel < 0 || k == focusPanel) && panelRects[k].Contains(point)) return k;
        return -1;
    }

    void ToggleFocus(int k) => focusPanel = focusPanel >= 0 ? -1 : k;

    // The focused panel as the largest square on the screen, with its name, the wind arrow and
    // the latitude; no controls. Dragging still moves the map.
    void DrawFocus(float width, float height, float margin)
    {
        float side = Mathf.Min(width, height) - 2f * margin;
        var r = new Rect((width - side) * 0.5f, (height - side) * 0.5f, side, side);
        imageRect = r;
        panelSide = side;
        for (int k = 0; k < viewCount; k++) panelRects[k] = k == focusPanel ? r : Rect.zero;
        if (textures[focusPanel] != null) GUI.DrawTexture(r, textures[focusPanel]);
        if (clouds && cloudTexture != null && Effective(focusPanel) == Show.Map) DrawClouds(r);
        string name = $"{ShowNames[(int)Effective(focusPanel)]}  (right click or double tap: back)";
        DrawRect(new Rect(r.x, r.y, 8.5f * name.Length + 8f, 22f), new Color(0f, 0f, 0f, 0.5f));
        GUI.Label(new Rect(r.x + 4f, r.y + 1f, r.width - 4f, 22f), name);
        if (colouring == Colouring.Biomes && wind) DrawWindArrow(new Vector2(r.xMax - 34f, r.y + 34f));
        DrawLatitude(r.x, r.yMax);
    }

    // v6 UI: how far north or south the view is. The climate already cools with |world y|
    // (equator at y = 0, poles at ±LatitudeSpan view widths; v3), so this is the latitude
    // that sets the temperature, in degrees: bottom edge to top edge of the view.
    static string LatitudeName(float yWorld)
    {
        float degrees = Mathf.Clamp01(Mathf.Abs(yWorld) / LatitudeSpan) * 90f;
        int d = Mathf.RoundToInt(degrees);
        return d == 0 ? "0°" : $"{d}°{(yWorld > 0f ? "N" : "S")}";
    }

    void DrawLatitude(float x, float bottom)
    {
        if (colouring != Colouring.Biomes) return;
        string text = $"lat {LatitudeName(offset.y)} – {LatitudeName(offset.y + 1f)}" +
                      (viewLand > 0 ? $"   land: temp {viewTemp:0.00}, moist {viewMoist:0.00}" : "   no land");
        var box = new Rect(x, bottom - 22f, 8.5f * text.Length + 8f, 22f);
        DrawRect(box, new Color(0f, 0f, 0f, 0.5f));
        GUI.Label(new Rect(box.x + 4f, box.y + 1f, box.width, 22f),
            new GUIContent(text, "Latitude of the bottom and top edges of the view (90° = 8 view widths from the equator; farther it stays 90°). Temperature drops away from the equator (Climate > Temperature > Latitude). Mean temperature and moisture of the land in the view, 0–1 as the biome table reads them: temperature below 0.25 is cold (tundra, snow), 0.25–0.5 cool (boreal forest, shrubland)."));
    }

    readonly Vector2[] pageScroll = new Vector2[6]; // one per Page
    bool pageDrag;

    void DrawControls(Rect area)
    {
        GUILayout.BeginArea(area);
        page = (Page)GUILayout.Toolbar((int)page, Tipped(PageLabels, PageTips), GUILayout.Height(30f));
        GUILayout.Space(4f);

        // v5: the page scrolls under the fixed tabs. A finger dragged over text or empty space
        // scrolls it (IMGUI's scroll view only follows its bar on a touch screen); a drag that
        // starts on a slider or a button belongs to that control (it holds hotControl).
        Rect body = new Rect(0f, 38f, area.width, area.height - 38f);
        Event e = Event.current;
        if (e.type == EventType.MouseDown && body.Contains(e.mousePosition)) pageDrag = GUIUtility.hotControl == 0;
        if (e.type == EventType.MouseUp) pageDrag = false;
        // The curve editor drags its points by hand (no hotControl): its drags are not scrolls.
        if (e.type == EventType.MouseDrag && pageDrag && GUIUtility.hotControl == 0 && !curveDragging)
        {
            pageScroll[(int)page].y -= e.delta.y;
            e.Use();
        }

        GUI.changed = false;
        cloudControlsOnly = false;
        pageScroll[(int)page] = GUILayout.BeginScrollView(pageScroll[(int)page], GUIStyle.none, GUI.skin.verticalScrollbar);
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
        GUILayout.EndScrollView();
        // Perf buttons must not resample (they would pollute the log); cloud controls neither
        // (the clouds are recomputed every frame anyway).
        if (GUI.changed && page != Page.Perf && !cloudControlsOnly) dirty = true;
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
            var labels = new GUIContent[active.Length];
            for (int i = 0; i < active.Length; i++) labels[i] = new GUIContent(FieldNames[active[i]], FieldTips[active[i]]);
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
        int newFamily = GUILayout.Toolbar(family, Tipped(FamilyLabels, FamilyTips), GUILayout.Height(30f));
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
            variant = GUILayout.Toolbar(variant, Tipped(SimplexLabels, SimplexTips), GUILayout.Height(30f));
            field.type = lastSimplex = (NoiseType)((int)NoiseType.SimplexOldGradient + variant);
        }

        Row(2);
        GUILayout.BeginHorizontal();
        GUILayout.Label(new GUIContent(selectedField == FHeight ? "Seed" : $"Seed (+{selectedField})",
            "The number every noise starts from. Same seed and settings = same map. Each field adds its own number (+1, +2...)."), GUILayout.Width(110f));
        seed = IntField("seed", ref seedText, seed);
        if (GUILayout.Button("−", GUILayout.Height(30f))) seed--;
        if (GUILayout.Button("+", GUILayout.Height(30f))) seed++;
        if (GUILayout.Button("Random", GUILayout.Height(30f))) seed = UnityEngine.Random.Range(0, 100000); // UI only; the noise itself stays seeded
        GUILayout.EndHorizontal();

        field.frequency = FrequencySlider(field.frequency);
        field.octaves = IntSlider("Octaves", field.octaves, 1, 6, "Layers of noise added together, each finer than the last. More = more detail.");
        field.lacunarity = IntSlider("Lacunarity", field.lacunarity, 2, 4, "How much finer each octave is than the previous one (its frequency is multiplied by this).");
        field.gain = Mathf.Round(Slider("Gain", field.gain, 0f, 1f, "0.00", "How strong each octave is compared with the previous one. Low = smooth, high = rough.") * 100f) / 100f;
        SetField(selectedField, field);

        // Offset: drag the image, or type it. The fields follow the drag unless focused.
        GUILayout.BeginHorizontal(GUILayout.Height(30f));
        GUILayout.Label(new GUIContent("Offset x, y", "Where the view is in the world, in view widths. Dragging the map changes it."), GUILayout.Width(110f));
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
        heightSource = (HeightSource)GUILayout.Toolbar((int)heightSource, Tipped(HeightLabels, HeightTips), GUILayout.Height(30f));

        GUILayout.BeginHorizontal(GUILayout.Height(30f));
        shade = GUILayout.Toggle(shade, new GUIContent(" Shade", "Relief shading, lit from the upper left. The slider is its strength."), GUILayout.Width(110f));
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
        int curve = GUILayout.Toolbar(selectedCurve, Tipped(CurveNames, CurveTips), GUILayout.Height(30f));
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

    // v4: two sub-tabs, so the overlay list fits on a phone.
    static readonly string[] MapTabLabels = { "Bands", "Layers" };
    int mapTab;

    void MapPage()
    {
        mapTab = GUILayout.Toolbar(mapTab, Tipped(MapTabLabels, MapTabTips), GUILayout.Height(30f));
        Row(3);
        if (mapTab == 0) BandsTab();
        else LayersTab();
    }

    void BandsTab()
    {
        waterMode =(WaterMode)GUILayout.Toolbar((int)waterMode, Tipped(WaterLabels, WaterTips), GUILayout.Height(30f));
        if (waterMode == WaterMode.RawThreshold)
            rawThreshold = Mathf.Round(Slider("Threshold", rawThreshold, -1f, 1f, "+0.00;-0.00", "Sea level: cells below this height are water.") * 100f) / 100f;
        else
            waterPercent = Mathf.Round(Slider("Water %", waterPercent, 0f, 100f, "0", "Share of the cells that are water. The sea level is found to match it."));
        cutsFrom = (CutsFrom)GUILayout.Toolbar((int)cutsFrom, Tipped(CutsLabels, CutsTips), GUILayout.Height(30f));

        if (palettes == null) LoadPalettes();
        GUILayout.BeginHorizontal(GUILayout.Height(30f));
        GUILayout.Label(new GUIContent("Palette", "Colours of the bands (the website's palettes)."), GUILayout.Width(110f));
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
        if (GUILayout.Button(new GUIContent("Default bands", "Back to the default share of every band."), GUILayout.Width(130f), GUILayout.Height(30f)))
        {
            DefaultWaterShares.CopyTo(waterShares, 0);
            DefaultLandShares.CopyTo(landShares, 0);
        }
        GUILayout.EndHorizontal();
        float share = Slider("Share %", shares[i], 0f, 100f, "0", "Share of the water (or of the land) that the selected band takes. The other bands adjust.");
        if (Mathf.Abs(share - shares[i]) >= 0.5f) SetShare(shares, i, share);
    }

    void LayersTab()
    {
        // v3: what the image shows. v4: the view (1, 1×2, 2×2) and, with the arrows, the
        // layer of the selected panel (tap a panel to select it).
        GUILayout.BeginHorizontal(GUILayout.Height(30f));
        int view = GUILayout.Toolbar(System.Array.IndexOf(ViewCounts, viewCount), Tipped(ViewLabels, ViewTips),
                                     GUILayout.Width(130f), GUILayout.Height(30f));
        viewCount = ViewCounts[view];
        selectedPanel = Mathf.Min(selectedPanel, viewCount - 1);
        if (GUILayout.Button("<", GUILayout.Width(40f), GUILayout.Height(30f))) panelShow[selectedPanel] = NextShow(-1);
        string layerName = ShowNames[(int)Effective(selectedPanel)];
        GUILayout.Label(new GUIContent(viewCount > 1 ? $"{selectedPanel + 1}: {layerName}" : layerName,
            "What the selected panel shows. < > to change it."), GUILayout.ExpandWidth(true));
        if (GUILayout.Button(">", GUILayout.Width(40f), GUILayout.Height(30f))) panelShow[selectedPanel] = NextShow(+1);
        GUILayout.EndHorizontal();

        // Overlays over the map, in order: box, < layer >, opacity, remove.
        GUILayout.Label(overlays.Count == 0 ? "Overlays over the map: none (+ to add)" : "Overlays over the map, in order:");
        int remove = -1;
        for (int k = 0; k < overlays.Count; k++)
        {
            var o = overlays[k];
            GUILayout.BeginHorizontal(GUILayout.Height(30f));
            o.on = GUILayout.Toggle(o.on, new GUIContent("", "Show this layer."), GUILayout.Width(24f), GUILayout.Height(30f));
            if (GUILayout.Button("<", GUILayout.Width(30f), GUILayout.Height(30f))) o.layer = NextOverlay(o.layer, -1);
            string name = OverlayNames[o.layer] + (OverlayAvailable(o.layer) ? "" : " (off)");
            GUILayout.Label(new GUIContent(name, "A layer blended over the map, in list order. The slider is its opacity; − removes it."), GUILayout.Width(105f));
            if (GUILayout.Button(">", GUILayout.Width(30f), GUILayout.Height(30f))) o.layer = NextOverlay(o.layer, +1);
            o.opacity = R2(GUILayout.HorizontalSlider(o.opacity, 0f, 1f, GUILayout.ExpandWidth(true)));
            GUILayout.Label(o.opacity.ToString("0.00", Inv), GUILayout.Width(40f));
            if (GUILayout.Button("−", GUILayout.Width(30f), GUILayout.Height(30f))) remove = k;
            GUILayout.EndHorizontal();
        }
        if (remove >= 0) { overlays.RemoveAt(remove); GUI.changed = true; }

        GUILayout.BeginHorizontal(GUILayout.Height(30f));
        GUI.enabled = overlays.Count < MaxOverlays;
        if (GUILayout.Button(new GUIContent("+", "Add a layer over the map (up to four)."), GUILayout.Width(60f), GUILayout.Height(30f)))
            overlays.Add(new Overlay { layer = FirstUnusedOverlay() });
        GUI.enabled = true;
        if (HasContours())
        {
            GUILayout.Label(new GUIContent("Contour step", "Height between two contour lines."), GUILayout.Width(100f));
            contourStep = Mathf.Round(GUILayout.HorizontalSlider(contourStep, 0.01f, 0.2f, GUILayout.ExpandWidth(true)) * 100f) / 100f;
            GUILayout.Label(contourStep.ToString("0.00", Inv), GUILayout.Width(40f));
        }
        GUILayout.EndHorizontal();
    }

    // A new overlay starts on the first available layer not yet in the list.
    int FirstUnusedOverlay()
    {
        for (int layer = 0; layer < OverlayCodes.Length; layer++)
        {
            if (!OverlayAvailable(layer)) continue;
            bool used = false;
            foreach (var o in overlays) used |= o.layer == layer;
            if (!used) return layer;
        }
        return OvContours;
    }

    Show NextShow(int direction)
    {
        int count = ShowNames.Length, s = (int)Effective(selectedPanel);
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
        colouring = (Colouring)GUILayout.Toolbar((int)colouring, Tipped(ColouringLabels, ColouringTips), GUILayout.Height(30f));
        if (colouring == Colouring.Bands)
        {
            GUILayout.Label("Biomes: temperature and moisture over the land, Whittaker table (BiomeTable).");
            Row(3);
            CloudControls(); // v4: the clouds work with bands too (without the humidity)
            return;
        }

        Row(3);
        // v6 (2a): five tabs no longer fit in one row (each button is as wide as "Temperature"),
        // so they wrap into two rows of three.
        climateTab = GUILayout.SelectionGrid(climateTab, Tipped(ClimateLabels, ClimateTips), 3, GUILayout.Height(62f));
        Row(2);
        if (climateTab == 0)
        {
            baseTemperature = R2(Slider("Base", baseTemperature, 0f, 1f, "0.00", "Temperature at sea level on the equator (the row y = 0 of the world)."));
            lapseRate = R2(Slider("Altitude", lapseRate, 0f, 1f, "0.00", "How much colder the land gets with height. It uses the height rank, so it cools the whole land."));
            latitudeEffect = R2(Slider("Latitude", latitudeEffect, 0f, 1f, "0.00", "How much colder it gets away from the equator (north or south of y = 0)."));
            coastModeration = R2(Slider("Coast", coastModeration, 0f, 0.5f, "0.00", "Warmth from the sea: the coast is milder, fading with distance inland."));
            tempNoiseAmplitude = R2(Slider("Noise", tempNoiseAmplitude, 0f, 0.3f, "0.00", "Random variation of the temperature (the Temp noise), so the bands are not straight."));
        }
        else if (climateTab == 1)
        {
            coastalMoistureBonus = R2(Slider("Coast", coastalMoistureBonus, 0f, 1f, "0.00", "Extra moisture near the sea."));
            coastDecayRate = R2(Slider("Decay", coastDecayRate, 0f, 1f, "0.00", "How fast the coastal moisture fades inland. Low = it reaches far."));
            moistureNoiseAmplitude = R2(Slider("Noise", moistureNoiseAmplitude, 0f, 1f, "0.00", "Moisture from the Moist noise, everywhere: random wet and dry patches."));
        }
        else if (climateTab == 3)
        {
            CloudControls();
            return;
        }
        else if (climateTab == 4)
        {
            OceanControls();
            return;
        }
        else
        {
            // One row for on/off and the direction; the arrow on the map shows it too.
            GUILayout.BeginHorizontal(GUILayout.Height(30f));
            wind = GUILayout.Toggle(wind, new GUIContent(" Wind", "Wind on or off. The slider is where it blows from; the arrow on the map shows where it goes."), GUILayout.Width(110f));
            GUI.enabled = wind;
            windAngle = Mathf.Round(GUILayout.HorizontalSlider(windAngle, 0f, 360f, GUILayout.ExpandWidth(true)) / 15f) * 15f % 360f;
            GUILayout.Label(WindName(), GUILayout.Width(70f));
            GUILayout.EndHorizontal();
            // v6 (1a, 1b): the model. With Flow the humidity follows the flow too.
            GUILayout.BeginHorizontal(GUILayout.Height(30f));
            GUILayout.Label(new GUIContent("Model", "How the wind meets the land. The sliders below act on the humidity the same way in both."), GUILayout.Width(110f));
            windModel = (WindModel)GUILayout.Toolbar((int)windModel, Tipped(WindModelLabels, WindModelTips), GUILayout.Height(30f));
            GUILayout.EndHorizontal();
            if (windModel == WindModel.Flow)
            {
                solidAbove = R2(Slider("Solid above", solidAbove, 0f, 1f, "0.00",
                    "Land higher than this is a wall: the air goes around it. Below it the air climbs, as with Sweep. Height rank: 0 = coast, 1 = highest land. 0 = all land is a wall; 1 = none."));
                flowIterations = Mathf.RoundToInt(Slider("Passes", flowIterations, 10f, 600f, "0",
                    "Solver passes (red + black). More = the air feels the walls from farther away and leaves less divergence, but costs more.") / 10f) * 10;
                GUILayout.Label(FlowSummary());
                if (GUILayout.Button(new GUIContent("Copy", "Copy this summary as text."), GUILayout.Width(90f), GUILayout.Height(30f)))
                    GUIUtility.systemCopyBuffer = FlowSummary();
            }
            windStrength = R2(Slider("Weight", windStrength, 0f, 1f, "0.00", "How much the moisture carried by the wind adds to the land's moisture."));
            windLift = R2(Slider("Lift", windLift, 0f, 1f, "0.00", "Rain when the air climbs a slope. High = wet windward slopes and a dry rain shadow behind."));
            windDry = Mathf.Round(Slider("Dry", windDry, 0f, 0.05f, "0.000", "Moisture the air loses per cell over flat land.") * 1000f) / 1000f;
            windEvaporation = Mathf.Round(Slider("Sea", windEvaporation, 0f, 0.2f, "0.000", "Moisture the air picks up per cell over the sea.") * 1000f) / 1000f;
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

    bool cloudControlsOnly;

    // v4: cloud controls. Changing them does not touch the terrain. The tab switch above
    // them still counts as a change (one repaint), which is harmless.
    void CloudControls()
    {
        bool before = GUI.changed;
        GUI.changed = false;
        GUILayout.BeginHorizontal(GUILayout.Height(30f));
        clouds = GUILayout.Toggle(clouds, new GUIContent(" Clouds", "Clouds on or off. The slider is their opacity."), GUILayout.Width(110f));
        GUI.enabled = clouds;
        cloudOpacity = R2(GUILayout.HorizontalSlider(cloudOpacity, 0f, 1f, GUILayout.ExpandWidth(true)));
        GUILayout.Label(new GUIContent(cloudOpacity.ToString("0.00", Inv), "Cloud opacity."), GUILayout.Width(50f));
        GUILayout.EndHorizontal();
        cloudCover = R2(Slider("Cover", cloudCover, 0f, 1f, "0.00", "How much of the sky is under cloud."));
        cloudSize = IntSlider("Size", cloudSize, 1, 8, "Cloud scale: 1 = large fronts, 8 = small scattered clouds.");
        cloudEdge = R2(Slider("Edge", cloudEdge, 0.01f, 0.2f, "0.00", "Width of the cloud edges: low = crisp, high = soft and hazy."));
        cloudSpeed = R2(Slider("Speed", cloudSpeed, 0f, 1f, "0.00", "How fast the clouds drift, in the direction of the wind (even with the wind off)."));
        cloudChange = R2(Slider("Change", cloudChange, 0f, 1f, "0.00", "How fast the clouds change shape."));
        cloudHumidity = R2(Slider("Humidity", cloudHumidity, 0f, 1f, "0.00", "How much the air's moisture decides where clouds form: more over wet air and windward slopes, fewer in the rain shadow. No effect with Bands."));
        GUI.enabled = true;
        if (GUI.changed && !before) cloudControlsOnly = true;
        GUI.changed |= before;
    }

    string TopBiomes()
    {
        int total = 0;
        foreach (int c in biomeCount) total += c;
        if (total == 0) return "(no land on screen, or no panel shows the Map)";
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

        // v6 UI: the map the app opens with on this device (PlayerPrefs). Without one, the
        // values of the NoiseLab component in the scene (the Inspector) are the default.
        bool hasDefault = PlayerPrefs.HasKey(DefaultKey);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button(new GUIContent("Set as default", "Open the app with the current map on this device (its whole code: noises, curves, climate, wind, panels)."), GUILayout.Height(40f)))
        {
            PlayerPrefs.SetString(DefaultKey, BuildCode());
            PlayerPrefs.Save();
            codeStatus = "This map is now the default on this device.";
        }
        GUI.enabled = hasDefault;
        if (GUILayout.Button(new GUIContent("Clear default", "Open the app with the scene's values again."), GUILayout.Height(40f)))
        {
            PlayerPrefs.DeleteKey(DefaultKey);
            PlayerPrefs.Save();
            codeStatus = "Default cleared: the app opens with the scene's values.";
        }
        GUI.enabled = true;
        GUILayout.EndHorizontal();
        GUILayout.Label(codeStatus);
        SavedCodes();
    }

    // v6 UI: codes saved on this device (PlayerPrefs: the editor and the S22 each keep their
    // own list). One line per entry, "name<TAB>code"; codes never hold tabs or new lines.
    const string SavedKey = "NoiseLab.savedCodes";
    const string DefaultKey = "NoiseLab.defaultCode";
    readonly System.Collections.Generic.List<(string name, string code)> saved = new();
    bool savedRead;
    string saveName = "";
    int deleteArmed = -1; // the first press on Del arms it; the second deletes

    void ReadSaved()
    {
        savedRead = true;
        saved.Clear();
        foreach (string line in PlayerPrefs.GetString(SavedKey, "").Split('\n'))
        {
            int tab = line.IndexOf('\t');
            if (tab > 0) saved.Add((line.Substring(0, tab), line.Substring(tab + 1)));
        }
    }

    void WriteSaved()
    {
        var lines = new string[saved.Count];
        for (int i = 0; i < saved.Count; i++) lines[i] = saved[i].name + "\t" + saved[i].code;
        PlayerPrefs.SetString(SavedKey, string.Join("\n", lines));
        PlayerPrefs.Save();
    }

    void SavedCodes()
    {
        if (!savedRead) ReadSaved();
        bool before = GUI.changed; // saving, naming and deleting do not resample; Load does
        GUILayout.Space(8f);
        GUILayout.Label("Saved maps (on this device)");
        GUILayout.BeginHorizontal();
        saveName = GUILayout.TextField(saveName, 40, GUILayout.Height(40f), GUILayout.ExpandWidth(true));
        if (GUILayout.Button(new GUIContent("Save", "Save the current map under this name (empty: Map N). The same name replaces it."),
                GUILayout.Width(90f), GUILayout.Height(40f)))
        {
            string name = saveName.Replace("\t", " ").Replace("\n", " ").Trim();
            if (name.Length == 0) name = $"Map {saved.Count + 1}";
            int at = saved.FindIndex(e => e.name == name);
            if (at >= 0) saved[at] = (name, BuildCode()); else saved.Add((name, BuildCode()));
            WriteSaved();
            codeStatus = $"Saved \"{name}\".";
            saveName = "";
            deleteArmed = -1;
            GUIUtility.keyboardControl = 0;
        }
        GUILayout.EndHorizontal();
        GUI.changed = before;

        for (int i = 0; i < saved.Count; i++)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label($"({i + 1}) {saved[i].name}", GUILayout.Height(40f), GUILayout.ExpandWidth(true));
            if (GUILayout.Button(new GUIContent("Load", "Open this map (its whole code)."), GUILayout.Width(90f), GUILayout.Height(40f)))
            {
                string error = ApplyCode(saved[i].code);
                codeStatus = error == null ? $"Loaded \"{saved[i].name}\"." : "Load: " + error;
                deleteArmed = -1;
                GUIUtility.keyboardControl = 0;
                before = GUI.changed; // keep the resample
            }
            bool armed = deleteArmed == i;
            if (GUILayout.Button(new GUIContent(armed ? "Sure?" : "Del", "Delete this entry (press twice)."), GUILayout.Width(70f), GUILayout.Height(40f)))
            {
                if (armed)
                {
                    codeStatus = $"Deleted \"{saved[i].name}\".";
                    saved.RemoveAt(i);
                    WriteSaved();
                    deleteArmed = -1;
                    GUILayout.EndHorizontal();
                    GUI.changed = before;
                    break;
                }
                deleteArmed = i;
            }
            GUILayout.EndHorizontal();
            GUI.changed = before;
        }
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
        GUILayout.Label(new GUIContent("Drag res", "Resolution used while dragging. Full resolution comes back when you let go."), GUILayout.Width(110f));
        int current = System.Array.IndexOf(DragResolutions, dragResolution);
        int chosen = GUILayout.Toolbar(current < 0 ? 1 : current, Tipped(DragLabels, DragTips), GUILayout.Height(30f));
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

        // v5: climate margin on/off and the edge test.
        GUILayout.BeginHorizontal(GUILayout.Height(30f));
        bool margin = GUILayout.Toggle(climateMargin, new GUIContent(" Margin",
            "Wind and coast look beyond the view (a margin upwind and around it), so the climate does not change when dragging. Off = as in v4."),
            GUILayout.Width(110f));
        if (margin != climateMargin) { climateMargin = margin; dirty = true; }
        GUILayout.Label($"{mL}/{mR}/{mB}/{mT} cells, grid {extW}×{extH}, climate {climateMs:0.0} ms ({GridNoiseJob<Simplex2D<Simplex>>.StateName(climateBurstState)})");
        if (GUILayout.Button(new GUIContent("Edge test",
            "Moves the view a quarter upwind and back, without and with margin, and measures how much the climate changes where the two views overlap."),
            GUILayout.Width(110f), GUILayout.Height(30f))) EdgeTest();
        GUILayout.EndHorizontal();
        if (edgeStatus.Length > 0) GUILayout.Label(edgeStatus);
        if (FlowOn) GUILayout.Label(FlowSummary()); // v6 (1a)

        // v5 (B1): Burst on/off and the check.
        GUILayout.BeginHorizontal(GUILayout.Height(30f));
        bool burst = GUILayout.Toggle(useBurst, new GUIContent(" Burst",
            "Sample the noises (terrain and clouds) with Burst jobs on every core. Off = plain C# on one thread, as in v4."),
            GUILayout.Width(110f));
        if (burst != useBurst) { useBurst = burst; ForgetSamples(); }
        GUILayout.Label($"last sampling: {GridNoiseJob<Simplex2D<Simplex>>.StateName(burstState)}");
        if (GUILayout.Button(new GUIContent("Burst check",
            "Samples the view's fields with and without Burst, times both and counts the values that differ."),
            GUILayout.Width(110f), GUILayout.Height(30f))) BurstCheck();
        GUILayout.EndHorizontal();
        if (burstStatus.Length > 0) GUILayout.Label(burstStatus);

        // v6 UI: the labels above cannot be selected, so a button copies them.
        if (GUILayout.Button(new GUIContent("Copy results", "Copy the edge test, the flow summary and the Burst check as text."), GUILayout.Height(30f)))
        {
            var sb = new System.Text.StringBuilder();
            if (edgeStatus.Length > 0) sb.AppendLine(edgeStatus);
            if (FlowOn) sb.AppendLine(FlowSummary());
            if (colouring == Colouring.Biomes && ocean) sb.AppendLine(OceanSummary());
            if (burstStatus.Length > 0) sb.AppendLine(burstStatus);
            sb.AppendLine("code: " + BuildCode());
            GUIUtility.systemCopyBuffer = sb.ToString();
            perfStatus = "Results copied.";
        }
    }

    // ---------------------------------------------------------------------------------
    // v5: edge test. Same as a drag at full resolution: the view moves a quarter of its
    // width upwind (whole cells, so the cells of the overlap coincide), cuts unchanged.
    // Compared on the land of the overlap: moisture and biome before and after the move.
    // With a perfect climate nothing changes there. Run without and with margin; the
    // times are sampling + climate at the moved view (caches cold, as after a drag).
    // ---------------------------------------------------------------------------------

    string edgeStatus = "";

    void EdgeTest()
    {
        if (colouring != Colouring.Biomes) { edgeStatus = "Edge test: choose Biomes colouring first."; return; }
        bool keepMargin = climateMargin;
        Vector2 keepOffset = offset;
        string off = EdgeTestOnce(false, keepOffset);
        string on = EdgeTestOnce(true, keepOffset);
        climateMargin = keepMargin;
        offset = keepOffset;
        dirty = true;
        edgeStatus = $"Edge test at {resolution}², wind {(wind ? WindName() + (FlowOn ? " (flow)" : "") : "off")}, land of the overlap:\n" +
                     $"  no margin: {off}\n  margin:    {on}";
        Debug.Log($"[NoiseLab] {edgeStatus}");
    }

    string EdgeTestOnce(bool margin, Vector2 start)
    {
        climateMargin = margin;
        int n = resolution;
        float a = wind ? windAngle * Mathf.Deg2Rad : 0f;
        int sx = -Mathf.RoundToInt(0.25f * n * Mathf.Cos(a)); // upwind
        int sy = -Mathf.RoundToInt(0.25f * n * Mathf.Sin(a));

        offset = start;
        Sample(n);
        Climate();
        var m0 = (float[])moist.Clone();
        var b0 = new BiomeType[n * n];
        for (int i = 0; i < n * n; i++) b0[i] = BiomeAt(i);
        bool flow = FlowOn;
        var u0 = flow ? (float[])windU.Clone() : null;
        var v0 = flow ? (float[])windV.Clone() : null;

        offset = start + new Vector2(sx, sy) / n;
        var watch = System.Diagnostics.Stopwatch.StartNew();
        Sample(n);
        Climate();
        float ms = (float)watch.Elapsed.TotalMilliseconds;

        // New cell (x, y) is old cell (x + sx, y + sy).
        double sum = 0.0, flowSum = 0.0;
        float worst = 0f, flowWorst = 0f;
        int land = 0, changed = 0, cells = 0;
        for (int y = Mathf.Max(0, -sy); y < Mathf.Min(n, n - sy); y++)
            for (int x = Mathf.Max(0, -sx); x < Mathf.Min(n, n - sx); x++)
            {
                int i = y * n + x;
                int j = (y + sy) * n + x + sx;
                if (flow)
                {
                    // v6: how much the wind changes in the overlap (|Δ velocity|, 1 = background speed)
                    float du = windU[i] - u0[j], dv = windV[i] - v0[j];
                    float d2 = Mathf.Sqrt(du * du + dv * dv);
                    flowSum += d2;
                    flowWorst = Mathf.Max(flowWorst, d2);
                    cells++;
                }
                if (values[i] < threshold) continue;
                float d = Mathf.Abs(moist[i] - m0[j]);
                sum += d;
                worst = Mathf.Max(worst, d);
                land++;
                if (BiomeAt(i) != b0[j]) changed++;
            }
        if (land == 0) return "no land in the overlap";
        return $"moisture Δ mean {sum / land:0.0000}, max {worst:0.000}; biome changed {100f * changed / land:0.00} % of {land} cells; " +
               $"{ms:0} ms (grid {extW}×{extH})" +
               (flow && cells > 0 ? $"; wind Δ mean {flowSum / cells:0.0000}, max {flowWorst:0.000}" : "");
    }

    // v6 (continents): logarithmic, so 0.05 … 1 gets as much room as 1 … 32.
    static float FrequencySlider(float value)
    {
        const string tip = "Features per view width. Higher = smaller, more numerous shapes. " +
                           "Below 1 a shape spans several views (0.1 ≈ continents of several views).";
        float lo = Mathf.Log(MinFrequency), hi = Mathf.Log(MaxFrequency);
        GUILayout.BeginHorizontal(GUILayout.Height(30f));
        GUILayout.Label(new GUIContent("Frequency", tip), GUILayout.Width(110f));
        float t = GUILayout.HorizontalSlider(Mathf.Log(Mathf.Max(value, MinFrequency)), lo, hi, GUILayout.ExpandWidth(true));
        float f = SnapFrequency(Mathf.Exp(t));
        if (Mathf.Abs(t - Mathf.Log(Mathf.Max(value, MinFrequency))) < 1e-4f) f = value; // untouched: keep it
        GUILayout.Label(new GUIContent(f.ToString(f < 1f ? "0.00" : "0", Inv), tip), GUILayout.Width(50f));
        GUILayout.EndHorizontal();
        return f;
    }

    static int IntSlider(string label, int value, int min, int max, string tip) =>
        Mathf.RoundToInt(Slider(label, value, min, max, "0", tip));

    // The tip shows over the name and over the number (the slider bar itself has none).
    static float Slider(string label, float value, float min, float max, string format, string tip)
    {
        GUILayout.BeginHorizontal(GUILayout.Height(30f));
        GUILayout.Label(new GUIContent(label, tip), GUILayout.Width(110f));
        value = GUILayout.HorizontalSlider(value, min, max, GUILayout.ExpandWidth(true));
        GUILayout.Label(new GUIContent(value.ToString(format), tip), GUILayout.Width(50f));
        GUILayout.EndHorizontal();
        return value;
    }

    // Panels, histogram and numbers. Returns the y where they end.
    float DrawView(float left, float top, int cols, float side)
    {
        int rows = (viewCount + cols - 1) / cols;
        float gridWidth = GridWidth(cols, side), gridHeight = rows * side + PanelGap * (rows - 1);
        imageRect = new Rect(left, top, gridWidth, gridHeight);
        panelSide = side;
        for (int k = 0; k < viewCount; k++)
        {
            var r = panelRects[k] = new Rect(left + k % cols * (side + PanelGap), top + k / cols * (side + PanelGap), side, side);
            if (viewCount > 1 && k == selectedPanel)
                DrawRect(new Rect(r.x - 2f, r.y - 2f, r.width + 4f, r.height + 4f), Color.white);
            if (textures[k] != null) GUI.DrawTexture(r, textures[k]);
            if (clouds && cloudTexture != null && Effective(k) == Show.Map) DrawClouds(r);
            if (viewCount > 1)
            {
                string name = $"{k + 1} {ShowNames[(int)Effective(k)]}";
                DrawRect(new Rect(r.x, r.y, 9f * name.Length + 8f, 22f), new Color(0f, 0f, 0f, 0.5f));
                GUI.Label(new Rect(r.x + 4f, r.y + 1f, r.width - 4f, 22f), name);
            }
        }
        // The wind arrow goes in the top-right panel.
        if (colouring == Colouring.Biomes && wind) DrawWindArrow(new Vector2(left + gridWidth - 34f, top + 34f));
        DrawLatitude(left, top + gridHeight); // bottom-left panel

        // Histogram strip under the panels. x axis: raw value from -1 (left) to +1 (right).
        var plot = new Rect(left, top + gridHeight + 8f, gridWidth, 70f);
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

// ---------------------------------------------------------------------------------
// v5 (B1): the grid sampling as a Burst job. One row per index; the rows are spread over
// the worker threads (ScheduleParallel) and Burst compiles Execute to native code with
// SIMD. The arithmetic is the managed loop's, line by line. FloatMode.Strict: Burst may
// not reorder or fuse the float operations, so the values stay as close as possible to
// the managed path (Perf → Burst check counts the ones that differ).
// Burst only compiles generic jobs whose type it can see: each GridNoiseJob<N> is named
// with a concrete N in NoiseLab.SampleInto. If one ran without Burst anyway, the job says
// so: MarkManaged is [BurstDiscard], so it only runs in the managed version.
// ---------------------------------------------------------------------------------
[BurstCompile(FloatPrecision.Standard, FloatMode.Strict, CompileSynchronously = true)]
public struct GridNoiseJob<N> : IJobFor where N : struct, INoise
{
    public const int Off = 0, Compiled = 1, Managed = 2;

    public Settings settings;
    public int w;
    public float originX, originZ, step;

    // Each index writes a whole row, not only element y.
    [WriteOnly, NativeDisableParallelForRestriction] public NativeArray<float> target;
    [NativeDisableParallelForRestriction] public NativeArray<int> state;

    public void Execute(int y)
    {
        if (y == 0)
        {
            bool managed = false;
            MarkManaged(ref managed);
            state[0] = managed ? Managed : Compiled;
        }
        float pz = originZ + y * step;
        for (int x = 0; x < w; x += 4)
        {
            float4 px = new float4(x, x + 1, x + 2, x + 3) * step + originX;
            float4 v = GetFractalNoise<N>(new float4x3(px, 0f, pz), settings).v;
            for (int k = 0; k < 4 && x + k < w; k++)
                target[y * w + x + k] = v[k];
        }
    }

    [BurstDiscard]
    static void MarkManaged(ref bool managed) => managed = true;

    public static string StateName(int s) =>
        s == Compiled ? "Burst" : s == Managed ? "Burst NOT compiled (managed job)" : s == Off ? "plain C#" : "-";

    // Fills target[0 .. w·h) and returns Off, Compiled or Managed.
    public static int Fill(bool burst, Settings settings, float[] target, int w, int h,
                           float originX, float originZ, float step)
    {
        if (!burst)
        {
            FillManaged(settings, target, w, h, originX, originZ, step);
            return Off;
        }
        var buffer = new NativeArray<float>(w * h, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
        var flag = new NativeArray<int>(1, Allocator.TempJob);
        try
        {
            new GridNoiseJob<N>
            {
                settings = settings, w = w, originX = originX, originZ = originZ, step = step,
                target = buffer, state = flag
            }.ScheduleParallel(h, 4, default).Complete();
            NativeArray<float>.Copy(buffer, 0, target, 0, w * h);
            return h > 0 ? flag[0] : Compiled;
        }
        finally
        {
            buffer.Dispose();
            flag.Dispose();
        }
    }

    // Same sampling as MapNoiseBridge2D (cell centres, four at a time), but the value is
    // stored raw (no remap, no clamp). Point (x, y) is at origin + (x, y) · step.
    static void FillManaged(Settings settings, float[] target, int w, int h, float originX, float originZ, float step)
    {
        for (int y = 0; y < h; y++)
        {
            float pz = originZ + y * step;
            for (int x = 0; x < w; x += 4)
            {
                float4 px = new float4(x, x + 1, x + 2, x + 3) * step + originX;
                float4 v = GetFractalNoise<N>(new float4x3(px, 0f, pz), settings).v;
                for (int k = 0; k < 4 && x + k < w; k++)
                    target[y * w + x + k] = v[k];
            }
        }
    }
}

// ---------------------------------------------------------------------------------
// v5 (step 5): the cloud noise as a Burst job. Fractal Simplex3D with the time phase as
// the second coordinate, one row of the CloudRes² texture per index. Same arithmetic as
// the v4 loop; NoiseLab calls this Execute directly when Burst is off.
// ---------------------------------------------------------------------------------
[BurstCompile(FloatPrecision.Standard, FloatMode.Strict, CompileSynchronously = true)]
public struct CloudNoiseJob : IJobFor
{
    public Settings settings;
    public int res;
    public float step, offsetX, offsetY, driftX, driftY, phase;

    [WriteOnly, NativeDisableParallelForRestriction] public NativeArray<float> target;
    [NativeDisableParallelForRestriction] public NativeArray<int> state;

    public void Execute(int y)
    {
        if (y == 0)
        {
            bool managed = false;
            MarkManaged(ref managed);
            state[0] = managed ? GridNoiseJob<Simplex2D<Simplex>>.Managed : GridNoiseJob<Simplex2D<Simplex>>.Compiled;
        }
        float pz = offsetY + (y + 0.5f) * step - driftY;
        for (int x = 0; x < res; x += 4)
        {
            float4 px = (new float4(x, x + 1, x + 2, x + 3) + 0.5f) * step + offsetX - driftX;
            float4 v = GetFractalNoise<Simplex3D<Simplex>>(new float4x3(px, phase, pz), settings).v;
            for (int k = 0; k < 4 && x + k < res; k++)
                target[y * res + x + k] = v[k];
        }
    }

    [BurstDiscard]
    static void MarkManaged(ref bool managed) => managed = true;
}

// ---------------------------------------------------------------------------------
// v5 (B2): climate jobs. Each mirrors a part of NoiseLab.ClimateManaged; the comments
// there explain the formulas. probe[0] is set to 1 by any job that runs without Burst.
// ---------------------------------------------------------------------------------
static class BurstProbe
{
    [BurstDiscard]
    static void MarkManaged(ref bool managed) => managed = true;

    public static void Check(NativeArray<int> probe)
    {
        bool managed = false;
        MarkManaged(ref managed);
        if (managed) probe[0] = 1;
    }
}

// Land rank of each cell of the climate grid: its position among the land values of the
// cut source (binary search), 0 at the coast, 1 at the top. One row per index.
[BurstCompile(FloatPrecision.Standard, FloatMode.Strict, CompileSynchronously = true)]
struct RankJob : IJobFor
{
    [ReadOnly] public NativeArray<float> heights, sorted;
    [WriteOnly, NativeDisableParallelForRestriction] public NativeArray<float> rank;
    [NativeDisableParallelForRestriction] public NativeArray<int> probe;
    public int w, below, landCount;

    public void Execute(int y)
    {
        if (y == 0) BurstProbe.Check(probe);
        for (int x = 0; x < w; x++)
        {
            int i = y * w + x;
            float v = heights[i];
            int lo = 0, hi = sorted.Length;
            while (lo < hi)
            {
                int mid = (lo + hi) >> 1;
                if (sorted[mid] < v) lo = mid + 1; else hi = mid;
            }
            rank[i] = landCount > 0 ? math.saturate((float)(lo - below) / landCount) : 0f;
        }
    }
}

// Exact Euclidean distance transform (Felzenszwalb & Huttenlocher), as
// NoiseLab.CoastDistance: squared distances down each column, then along each row.
// Each index is one column (or row) with its own scratch arrays (Allocator.Temp).
static class Edt
{
    public static void Run(NativeArray<double> f, NativeArray<double> d, NativeArray<double> z, NativeArray<int> v, int n)
    {
        int k = 0;
        v[0] = 0;
        z[0] = double.NegativeInfinity;
        z[1] = double.PositiveInfinity;
        for (int q = 1; q < n; q++)
        {
            double s = Intersection(f, q, v[k]);
            while (s <= z[k]) { k--; s = Intersection(f, q, v[k]); }
            k++;
            v[k] = q;
            z[k] = s;
            z[k + 1] = double.PositiveInfinity;
        }
        k = 0;
        for (int q = 0; q < n; q++)
        {
            while (z[k + 1] < q) k++;
            double dq = q - v[k];
            d[q] = dq * dq + f[v[k]];
        }
    }

    static double Intersection(NativeArray<double> f, int q, int p) =>
        ((f[q] + (double)q * q) - (f[p] + (double)p * p)) / (2.0 * q - 2.0 * p);
}

[BurstCompile(FloatPrecision.Standard, FloatMode.Strict, CompileSynchronously = true)]
struct CoastColumnsJob : IJobFor
{
    [ReadOnly] public NativeArray<float> heights;
    [WriteOnly, NativeDisableParallelForRestriction] public NativeArray<float> dist;
    [NativeDisableParallelForRestriction] public NativeArray<int> probe;
    public int w, h;
    public float threshold;

    public void Execute(int x)
    {
        if (x == 0) BurstProbe.Check(probe);
        var f = new NativeArray<double>(h, Allocator.Temp);
        var d = new NativeArray<double>(h, Allocator.Temp);
        var z = new NativeArray<double>(h + 1, Allocator.Temp);
        var v = new NativeArray<int>(h, Allocator.Temp);
        for (int y = 0; y < h; y++) f[y] = heights[y * w + x] >= threshold ? 1e20 : 0.0;
        Edt.Run(f, d, z, v, h);
        for (int y = 0; y < h; y++) dist[y * w + x] = (float)d[y];
        f.Dispose(); d.Dispose(); z.Dispose(); v.Dispose();
    }
}

[BurstCompile(FloatPrecision.Standard, FloatMode.Strict, CompileSynchronously = true)]
struct CoastRowsJob : IJobFor
{
    [NativeDisableParallelForRestriction] public NativeArray<float> dist;
    [NativeDisableParallelForRestriction] public NativeArray<int> probe;
    public int w;

    public void Execute(int y)
    {
        if (y == 0) BurstProbe.Check(probe);
        var f = new NativeArray<double>(w, Allocator.Temp);
        var d = new NativeArray<double>(w, Allocator.Temp);
        var z = new NativeArray<double>(w + 1, Allocator.Temp);
        var v = new NativeArray<int>(w, Allocator.Temp);
        int row = y * w;
        for (int x = 0; x < w; x++) f[x] = dist[row + x];
        Edt.Run(f, d, z, v, w);
        for (int x = 0; x < w; x++) dist[row + x] = (float)math.sqrt(d[x]);
        f.Dispose(); d.Dispose(); z.Dispose(); v.Dispose();
    }
}

// The wind sweep of NoiseLab.WindField, on one thread (each line needs the previous one).
[BurstCompile(FloatPrecision.Standard, FloatMode.Strict, CompileSynchronously = true)]
struct WindJob : IJob
{
    [ReadOnly] public NativeArray<float> heights, rank;
    public NativeArray<float> air;
    public NativeArray<int> probe;
    public int w, h, dir;
    public bool xMajor;
    public float threshold, slope, keepFlat, pickUp, lift;

    int Cell(int u, int j) => xMajor ? j * w + u : u * w + j;

    public void Execute()
    {
        BurstProbe.Check(probe);
        int lu = xMajor ? w : h, lj = xMajor ? h : w;
        int first = dir > 0 ? 0 : lu - 1;
        for (int k = 0; k < lu; k++)
        {
            int u = first + dir * k;
            for (int j = 0; j < lj; j++)
            {
                int i = Cell(u, j);
                float a, rankBefore;
                float pj = j - slope;
                if (k == 0 || pj < 0f || pj > lj - 1)
                {
                    a = 1f;
                    rankBefore = rank[i];
                }
                else
                {
                    int j0 = math.min((int)pj, lj - 2);
                    float f = pj - j0;
                    int prev = u - dir;
                    a = math.lerp(air[Cell(prev, j0)], air[Cell(prev, j0 + 1)], f);
                    rankBefore = math.lerp(rank[Cell(prev, j0)], rank[Cell(prev, j0 + 1)], f);
                }

                if (heights[i] < threshold)
                    a += (1f - a) * pickUp;
                else
                {
                    float rise = math.max(0f, rank[i] - rankBefore);
                    a *= keepFlat * math.exp(-lift * rise);
                }
                air[i] = a;
            }
        }
    }
}

// Temperature and moisture of the view (and its crops of coast distance and air), one
// view row per index.
[BurstCompile(FloatPrecision.Standard, FloatMode.Strict, CompileSynchronously = true)]
struct ViewClimateJob : IJobFor
{
    [ReadOnly] public NativeArray<float> extCoast, extRank, extAir, tn, mn;
    [WriteOnly, NativeDisableParallelForRestriction] public NativeArray<float> coast, temp, moist, air;
    [NativeDisableParallelForRestriction] public NativeArray<int> probe;
    public int n, w, mL, mB;
    public bool wind;
    public float toUnits, offsetY, latitudeSpan;
    public float span; // 2c: view widths the n rows cover (1 for the view; the big picture's scale for its Biomes colour)
    public float baseTemperature, lapseRate, latitudeEffect, coastModeration, tempNoiseAmplitude;
    public float moistureNoiseAmplitude, coastalMoistureBonus, coastDecayRate, windStrength;

    public void Execute(int y)
    {
        if (y == 0) BurstProbe.Check(probe);
        float yWorld = offsetY + (y + 0.5f) * span / n; // span 1: the same as (y + 0.5) / n
        float lat = math.saturate(math.abs(yWorld) / latitudeSpan);
        for (int x = 0; x < n; x++)
        {
            int i = y * n + x;
            int e = (y + mB) * w + x + mL;
            float c = extCoast[e];
            coast[i] = c;
            float cd = c * toUnits;
            float t = baseTemperature
                    - lapseRate * extRank[e]
                    - latitudeEffect * lat
                    + coastModeration / (1f + cd)
                    + tempNoiseAmplitude * 0.5f * tn[i];
            temp[i] = math.saturate(t);

            float a = wind ? extAir[e] : 0f;
            if (wind) air[i] = a;
            float m = moistureNoiseAmplitude * math.saturate(mn[i] * 0.5f + 0.5f)
                    + coastalMoistureBonus / (1f + cd * coastDecayRate)
                    + (wind ? windStrength * a : 0f);
            moist[i] = math.saturate(m);
        }
    }
}

// ---------------------------------------------------------------------------------
// v6 (step 1a): the flow around the high land, as four jobs (see NoiseLab.Flow). Each
// writes only its own row; NoiseLab runs the same Execute in a plain loop with Burst off.
// Flow grid: fw × fh cells; u faces (fw + 1) × fh (u[y·(fw+1) + i] sits between cells
// i − 1 and i), v faces fw × (fh + 1) (v[r·fw + x] sits between rows r − 1 and r).
// Outside the grid: open, pressure 0.
// ---------------------------------------------------------------------------------

// The flow and the air at every climate cell: bilinear between the flow cell centres (the
// velocity of a centre is the mean of its two faces per axis), and whether the cell lies in
// a wall block. Index: climate row.
[BurstCompile(FloatPrecision.Standard, FloatMode.Strict, CompileSynchronously = true)]
struct FlowSampleJob : IJobFor
{
    [ReadOnly] public NativeArray<float> u, v;
    [ReadOnly] public NativeArray<byte> solid;
    [ReadOnly] public NativeArray<float> air; // flow cells
    [WriteOnly, NativeDisableParallelForRestriction] public NativeArray<float> eu, ev, ea;
    [WriteOnly, NativeDisableParallelForRestriction] public NativeArray<byte> es;
    [NativeDisableParallelForRestriction] public NativeArray<int> probe;
    public int w, fw, fh, ox, oy, block;

    float Ca(int cx, int cy) => air[cy * fw + cx];

    float Cu(int cx, int cy) => 0.5f * (u[cy * (fw + 1) + cx] + u[cy * (fw + 1) + cx + 1]);
    float Cv(int cx, int cy) => 0.5f * (v[cy * fw + cx] + v[(cy + 1) * fw + cx]);

    public void Execute(int ey)
    {
        if (ey == 0) BurstProbe.Check(probe);
        int by = ey + oy; // counted from the first block's start
        float fy = math.clamp((by + 0.5f) / block - 0.5f, 0f, fh - 1);
        int y0 = math.min((int)fy, math.max(fh - 2, 0)), y1 = math.min(y0 + 1, fh - 1);
        float ty = fy - y0;
        for (int ex = 0; ex < w; ex++)
        {
            int bx = ex + ox;
            float fx = math.clamp((bx + 0.5f) / block - 0.5f, 0f, fw - 1);
            int x0 = math.min((int)fx, math.max(fw - 2, 0)), x1 = math.min(x0 + 1, fw - 1);
            float tx = fx - x0;
            int i = ey * w + ex;
            eu[i] = math.lerp(math.lerp(Cu(x0, y0), Cu(x1, y0), tx), math.lerp(Cu(x0, y1), Cu(x1, y1), tx), ty);
            ev[i] = math.lerp(math.lerp(Cv(x0, y0), Cv(x1, y0), tx), math.lerp(Cv(x0, y1), Cv(x1, y1), tx), ty);
            ea[i] = math.lerp(math.lerp(Ca(x0, y0), Ca(x1, y0), tx), math.lerp(Ca(x0, y1), Ca(x1, y1), tx), ty);
            es[i] = solid[by / block * fw + bx / block];
        }
    }
}

// What the air of each flow cell depends on, fixed for the whole solve (see NoiseLab, v6 1b.2):
// the share of each face's inflow (left, right, below, above), the gain over the sea part and
// the keep over the land part plus the climb. Index: flow row.
[BurstCompile(FloatPrecision.Standard, FloatMode.Strict, CompileSynchronously = true)]
struct FlowAirSetupJob : IJobFor
{
    [ReadOnly] public NativeArray<float> u, v, rank, sea;
    [ReadOnly] public NativeArray<byte> solid;
    [WriteOnly, NativeDisableParallelForRestriction] public NativeArray<float4> weight;
    [WriteOnly, NativeDisableParallelForRestriction] public NativeArray<float> gain, keep;
    [NativeDisableParallelForRestriction] public NativeArray<int> probe;
    public int fw, fh;
    public float cell, dry, evaporation, lift, bu, bv;

    // Outside the grid the air is sea air at the cell's own rank (no climb).
    float R(int x, int y, int c) => x >= 0 && x < fw && y >= 0 && y < fh ? rank[y * fw + x] : rank[c];

    public void Execute(int y)
    {
        if (y == 0) BurstProbe.Check(probe);
        for (int x = 0; x < fw; x++)
        {
            int c = y * fw + x;
            float uL = u[y * (fw + 1) + x], uR = u[y * (fw + 1) + x + 1];
            float vB = v[y * fw + x], vT = v[(y + 1) * fw + x];
            float uc = 0.5f * (uL + uR), vc = 0.5f * (vB + vT);
            var w4 = new float4(math.max(uL, 0f), math.max(-uR, 0f), math.max(vB, 0f), math.max(-vT, 0f));
            float total = math.csum(w4);
            if (solid[c] != 0 || total < 1e-6f) // walls (and a cell nothing flows into): the background wind
            {
                uc = bu; vc = bv;
                w4 = new float4(math.max(bu, 0f), math.max(-bu, 0f), math.max(bv, 0f), math.max(-bv, 0f));
                total = math.csum(w4);
            }
            w4 /= total;
            float rankBefore = w4.x * R(x - 1, y, c) + w4.y * R(x + 1, y, c) + w4.z * R(x, y - 1, c) + w4.w * R(x, y + 1, c);
            float l1 = math.abs(uc) + math.abs(vc);
            float dist = l1 > 1e-6f ? cell * math.sqrt(uc * uc + vc * vc) / l1 : cell;
            float s = sea[c];
            weight[c] = w4;
            gain[c] = 1f - math.exp(-evaporation * dist * s);
            keep[c] = math.exp(-dry * dist * (1f - s) - lift * math.max(0f, rank[c] - rankBefore));
        }
    }
}

// The air's humidity on the flow grid, by fast sweeping until it settles. One thread: each
// update reads neighbours the same sweep has just written. stats: largest and mean change of
// a cell over the last round, cells that still moved more than 0.01, rounds used.
[BurstCompile(FloatPrecision.Standard, FloatMode.Strict, CompileSynchronously = true)]
struct FlowAirJob : IJob
{
    [ReadOnly] public NativeArray<float4> weight;
    [ReadOnly] public NativeArray<float> gain, keep;
    public NativeArray<float> air, start, stats;
    public NativeArray<int> probe;
    public int fw, fh, maxRounds;
    public float tolerance;

    public void Execute()
    {
        BurstProbe.Check(probe);
        int cells = fw * fh;
        for (int i = 0; i < cells; i++) air[i] = 1f; // not reached yet: sea air
        float worst = 0f, sum = 0f;
        int unsettled = 0, rounds = 0;
        while (rounds < maxRounds)
        {
            rounds++;
            for (int i = 0; i < cells; i++) start[i] = air[i];
            for (int sweep = 0; sweep < 4; sweep++)
            {
                bool right = (sweep & 1) == 0, up = (sweep & 2) == 0;
                for (int k = 0; k < fh; k++)
                {
                    int y = up ? k : fh - 1 - k;
                    for (int m = 0; m < fw; m++) Update(right ? m : fw - 1 - m, y);
                }
            }
            worst = 0f; sum = 0f; unsettled = 0;
            for (int i = 0; i < cells; i++)
            {
                float d = math.abs(air[i] - start[i]);
                worst = math.max(worst, d);
                sum += d;
                if (d > 0.01f) unsettled++;
            }
            if (worst < tolerance) break;
        }
        stats[0] = worst;
        stats[1] = sum / cells;
        stats[2] = unsettled;
        stats[3] = rounds;
    }

    void Update(int x, int y)
    {
        int c = y * fw + x;
        float4 w4 = weight[c];
        float a = w4.x * (x > 0 ? air[c - 1] : 1f) + w4.y * (x < fw - 1 ? air[c + 1] : 1f)
                + w4.z * (y > 0 ? air[c - fw] : 1f) + w4.w * (y < fh - 1 ? air[c + fw] : 1f);
        air[c] = (a + (1f - a) * gain[c]) * keep[c];
    }
}

// v6 (2c): one field of the ocean regions (currents, temperature or anomaly) read at every
// cell of a grid, as NoiseLab.SampleRegionField and the blend of NoiseLab.Ocean: bilinear
// between a region's cell centres, the two regions around each column weighted by how far
// they are. field holds the regions first, first + 1, … one after the other (oceanN² each).
// Grid cell (x, y) is the world point (originX + (x − shiftX + 0.5) / n, originY + (y − shiftY + 0.5) / n).
// Index: grid row.
[BurstCompile(FloatPrecision.Standard, FloatMode.Strict, CompileSynchronously = true)]
struct RegionSampleJob : IJobFor
{
    [ReadOnly] public NativeArray<float> field;
    [WriteOnly, NativeDisableParallelForRestriction] public NativeArray<float> output;
    [NativeDisableParallelForRestriction] public NativeArray<int> probe;
    public int first, cols, n, shiftX, shiftY, oceanN, perView;
    public float originX, originY, stride, latitudeSpan;

    float Sample(int k, float x, float y)
    {
        int rn = oceanN;
        float gx = math.clamp((x - (stride * (first + k) - 8f)) * perView - 0.5f, 0f, rn - 1);
        float gy = math.clamp((y + latitudeSpan) * perView - 0.5f, 0f, rn - 1);
        int x0 = math.min((int)gx, rn - 2), y0 = math.min((int)gy, rn - 2);
        float tx = gx - x0, ty = gy - y0;
        int i = k * rn * rn + y0 * rn + x0;
        return math.lerp(math.lerp(field[i], field[i + 1], tx), math.lerp(field[i + rn], field[i + rn + 1], tx), ty);
    }

    public void Execute(int y)
    {
        if (y == 0) BurstProbe.Check(probe);
        float wy = originY + (y - shiftY + 0.5f) / n;
        for (int x = 0; x < cols; x++)
        {
            float wx = originX + (x - shiftX + 0.5f) / n;
            float t = wx / stride;
            int k0 = (int)math.floor(t);
            float b = t - k0;
            int k = k0 - first;
            output[y * cols + x] = math.lerp(Sample(k, wx, wy), Sample(k + 1, wx, wy), b);
        }
    }
}

// v6 (2b): the sea temperature carried by the currents (see NoiseLab.SeaHeat), by fast
// sweeping until it settles. One thread, as FlowAirJob. weight: share of the inflow through
// the west, east, south and north faces; pull: the relaxation share times the base
// temperature. stats: largest change of a cell in the last round, rounds used.
[BurstCompile(FloatPrecision.Standard, FloatMode.Strict, CompileSynchronously = true)]
struct SeaHeatJob : IJob
{
    [ReadOnly] public NativeArray<float4> weight;
    [ReadOnly] public NativeArray<float> pull;
    public NativeArray<float> temp, start, stats;
    public NativeArray<int> probe;
    public int n, maxRounds;
    public float tolerance;

    public void Execute()
    {
        BurstProbe.Check(probe);
        int cells = n * n;
        float worst = 0f;
        int rounds = 0;
        while (rounds < maxRounds)
        {
            rounds++;
            for (int i = 0; i < cells; i++) start[i] = temp[i];
            for (int sweep = 0; sweep < 4; sweep++)
            {
                bool right = (sweep & 1) == 0, up = (sweep & 2) == 0;
                for (int k = 0; k < n; k++)
                {
                    int y = up ? k : n - 1 - k;
                    for (int m = 0; m < n; m++) Update(right ? m : n - 1 - m, y);
                }
            }
            worst = 0f;
            for (int i = 0; i < cells; i++) worst = math.max(worst, math.abs(temp[i] - start[i]));
            if (worst < tolerance) break;
        }
        stats[0] = worst;
        stats[1] = rounds;
    }

    // A face with no inflow has weight 0, so the neighbour beyond the grid is never read.
    void Update(int x, int y)
    {
        int c = y * n + x;
        float4 w4 = weight[c];
        float t = pull[c];
        if (w4.x > 0f) t += w4.x * temp[c - 1];
        if (w4.y > 0f) t += w4.y * temp[c + 1];
        if (w4.z > 0f) t += w4.z * temp[c - n];
        if (w4.w > 0f) t += w4.w * temp[c + n];
        temp[c] = t;
    }
}

// v6 (2a): one colour of rows of the Stommel solve (see NoiseLab.ComputeOceanRegion). Row
// y = 2r + parity, swept east to west; rows of one colour only read the other colour above
// and below, so they run in parallel and the result does not depend on the threads.
[BurstCompile(FloatPrecision.Standard, FloatMode.Strict, CompileSynchronously = true)]
struct OceanSorJob : IJobFor
{
    [ReadOnly] public NativeArray<byte> sea;
    [ReadOnly] public NativeArray<float> force, beta;
    [NativeDisableParallelForRestriction] public NativeArray<float> psi;
    [NativeDisableParallelForRestriction] public NativeArray<int> probe;
    public int n, parity;
    public float eps, h, omega;

    public void Execute(int r)
    {
        if (r == 0) BurstProbe.Check(probe);
        int y = 2 * r + parity;
        if (y >= n) return;
        float b = beta[y], f = force[y];
        float k = eps / (h * h);
        float diag = 4f * k + b / h;
        for (int x = n - 1; x >= 0; x--)
        {
            int i = y * n + x;
            if (sea[i] == 0) continue; // land stays 0
            float e = x < n - 1 ? psi[i + 1] : 0f, w = x > 0 ? psi[i - 1] : 0f;
            float no = y < n - 1 ? psi[i + n] : 0f, so = y > 0 ? psi[i - n] : 0f;
            float target = (k * (e + w + no + so) + b * e / h - f) / diag;
            psi[i] += omega * (target - psi[i]);
        }
    }
}

// Wall cells: mean land rank of the block × block climate cells above solidAbove.
[BurstCompile(FloatPrecision.Standard, FloatMode.Strict, CompileSynchronously = true)]
struct FlowMaskJob : IJobFor
{
    [ReadOnly] public NativeArray<float> rank, heights;
    [WriteOnly, NativeDisableParallelForRestriction] public NativeArray<byte> solid;
    [WriteOnly, NativeDisableParallelForRestriction] public NativeArray<float> blockRank, blockSea;
    [NativeDisableParallelForRestriction] public NativeArray<int> probe;
    public int w, h, fw, ox, oy, block; // ox, oy: cells the first block starts before the grid
    public float solidAbove, threshold;

    public void Execute(int fy)
    {
        if (fy == 0) BurstProbe.Check(probe);
        for (int fx = 0; fx < fw; fx++)
        {
            float sum = 0f;
            int count = 0, sea = 0;
            for (int dy = 0; dy < block; dy++)
                for (int dx = 0; dx < block; dx++)
                {
                    int x = block * fx + dx - ox, y = block * fy + dy - oy;
                    if (x < 0 || y < 0 || x >= w || y >= h) continue;
                    sum += rank[y * w + x];
                    if (heights[y * w + x] < threshold) sea++;
                    count++;
                }
            int c = fy * fw + fx;
            float mean = sum / count; // count ≥ 1: every block holds at least one grid cell
            blockRank[c] = mean;
            blockSea[c] = (float)sea / count;
            solid[c] = (byte)(mean > solidAbove ? 1 : 0);
        }
    }
}

// Background wind (bu, bv) on every face, 0 on the faces that touch a wall, and the
// divergence this leaves in each open cell (computed from the walls alone, so no row
// reads faces another row is writing). Index r = 0..fh: u row r (if r < fh), v row r.
[BurstCompile(FloatPrecision.Standard, FloatMode.Strict, CompileSynchronously = true)]
struct FlowInitJob : IJobFor
{
    [ReadOnly] public NativeArray<byte> solid;
    [WriteOnly, NativeDisableParallelForRestriction] public NativeArray<float> u, v, div;
    [NativeDisableParallelForRestriction] public NativeArray<int> probe;
    public int fw, fh;
    public float bu, bv;

    bool Wall(int x, int y) => x >= 0 && y >= 0 && x < fw && y < fh && solid[y * fw + x] != 0;
    float U(int i, int y) => Wall(i - 1, y) || Wall(i, y) ? 0f : bu;
    float V(int x, int r) => Wall(x, r - 1) || Wall(x, r) ? 0f : bv;

    public void Execute(int r)
    {
        if (r == 0) BurstProbe.Check(probe);
        if (r < fh)
        {
            for (int i = 0; i <= fw; i++) u[r * (fw + 1) + i] = U(i, r);
            for (int x = 0; x < fw; x++)
                div[r * fw + x] = Wall(x, r) ? 0f : U(x + 1, r) - U(x, r) + V(x, r + 1) - V(x, r);
        }
        for (int x = 0; x < fw; x++) v[r * fw + x] = V(x, r);
    }
}

// One colour of a red-black SOR pass: the cells with (x + y) % 2 == parity. Each reads
// only cells of the other colour, so rows can run in parallel with no race and the
// result does not depend on the order. Open neighbours count; outside the grid is open
// with p = 0; walls are skipped (Neumann). p_new = (Σ p_open − div) / n_open.
[BurstCompile(FloatPrecision.Standard, FloatMode.Strict, CompileSynchronously = true)]
struct FlowSorJob : IJobFor
{
    [ReadOnly] public NativeArray<byte> solid;
    [ReadOnly] public NativeArray<float> div;
    [NativeDisableParallelForRestriction] public NativeArray<float> p;
    [NativeDisableParallelForRestriction] public NativeArray<int> probe;
    public int fw, fh, parity;
    public float omega;

    public void Execute(int y)
    {
        if (y == 0) BurstProbe.Check(probe);
        for (int x = (y + parity) & 1; x < fw; x += 2)
        {
            int c = y * fw + x;
            if (solid[c] != 0) continue;
            float sum = 0f;
            int n = 0;
            if (x == 0) n++; else if (solid[c - 1] == 0) { sum += p[c - 1]; n++; }
            if (x == fw - 1) n++; else if (solid[c + 1] == 0) { sum += p[c + 1]; n++; }
            if (y == 0) n++; else if (solid[c - fw] == 0) { sum += p[c - fw]; n++; }
            if (y == fh - 1) n++; else if (solid[c + fw] == 0) { sum += p[c + fw]; n++; }
            if (n == 0) continue;
            float pNew = (sum - div[c]) / n;
            p[c] += omega * (pNew - p[c]);
        }
    }
}

// Faces minus the pressure gradient (faces next to a wall stay 0). Index r as FlowInitJob.
[BurstCompile(FloatPrecision.Standard, FloatMode.Strict, CompileSynchronously = true)]
struct FlowApplyJob : IJobFor
{
    [ReadOnly] public NativeArray<byte> solid;
    [ReadOnly] public NativeArray<float> p;
    [NativeDisableParallelForRestriction] public NativeArray<float> u, v;
    [NativeDisableParallelForRestriction] public NativeArray<int> probe;
    public int fw, fh;

    bool Wall(int x, int y) => x >= 0 && y >= 0 && x < fw && y < fh && solid[y * fw + x] != 0;
    float P(int x, int y) => x >= 0 && y >= 0 && x < fw && y < fh ? p[y * fw + x] : 0f;

    public void Execute(int r)
    {
        if (r == 0) BurstProbe.Check(probe);
        if (r < fh)
            for (int i = 0; i <= fw; i++)
                if (!Wall(i - 1, r) && !Wall(i, r)) u[r * (fw + 1) + i] -= P(i, r) - P(i - 1, r);
        for (int x = 0; x < fw; x++)
            if (!Wall(x, r - 1) && !Wall(x, r)) v[r * fw + x] -= P(x, r) - P(x, r - 1);
    }
}
