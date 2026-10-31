using Unity.Mathematics;
using UnityEngine;

using Islands;
using static Islands.Noise;

// Didactic noise scene (step 1: CPU sampling + display).
// Samples raw noise values on the CPU, so later steps can measure them exactly
// (histogram, water threshold). No GPU path, no materials: it draws with OnGUI.
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

    // Step 3: water. A raw threshold depends on the noise range; a percentage does not.
    public enum WaterMode { RawThreshold, Percentage }

    [SerializeField] WaterMode waterMode = WaterMode.RawThreshold;
    [SerializeField, Range(-1f, 1f)] float rawThreshold = 0f;
    [SerializeField, Range(0f, 100f)] float waterPercent = 40f;

    const int Bins = 64; // histogram bins over [-1, 1]

    float[] values, sorted;
    float threshold, waterFraction;
    Texture2D texture;
    bool dirty = true;

    // Step 2: measured statistics of the raw values.
    readonly int[] histogram = new int[Bins];
    int histogramPeak;
    float min, max, p1, p99, mean, sd;

    void OnValidate() => dirty = true;

    void Update()
    {
        if (!dirty) return;
        dirty = false;
        Sample();
        Measure();
        Flood();
        Paint();
    }

    // Exact, not estimated: sorts a copy of every sample.
    void Measure()
    {
        sorted = (float[])values.Clone();
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

    // Raw mode: the threshold is given, the area is measured.
    // Percentage mode: the area is given, the threshold is read from the sorted values
    // (the quantile, or inverse CDF): the value that leaves that fraction below it.
    void Flood()
    {
        int count = sorted.Length;
        if (waterMode == WaterMode.Percentage)
        {
            int k = Mathf.RoundToInt(waterPercent / 100f * count); // cells under water
            threshold = k <= 0 ? sorted[0] : k >= count ? sorted[count - 1] + 1e-6f : sorted[k];
        }
        else
        {
            threshold = rawThreshold;
        }

        int below = 0;
        foreach (float v in values) if (v < threshold) below++;
        waterFraction = (float)below / count;
    }

    static int BinOf(float v) => math.clamp((int)((v * 0.5f + 0.5f) * Bins), 0, Bins - 1);

    void OnDestroy()
    {
        if (texture != null) Destroy(texture);
    }

    void Sample()
    {
        var settings = Settings.Default;
        settings.seed = seed;
        settings.frequency = frequency;
        settings.octaves = octaves;
        settings.lacunarity = lacunarity;
        settings.persistence = gain;

        switch (type)
        {
            case NoiseType.Value: Fill<Lattice2D<LatticeNormal, Value>>(settings); break;
            case NoiseType.PerlinSquare: Fill<Lattice2D<LatticeNormal, Perlin>>(settings); break;
            case NoiseType.SimplexOldGradient: Fill<Simplex2D<Perlin>>(settings); break;
            case NoiseType.SimplexPerlinOnSimplex: Fill<Simplex2D<PerlinOnSimplex>>(settings); break;
            case NoiseType.SimplexSimplex: Fill<Simplex2D<Simplex>>(settings); break;
            case NoiseType.VoronoiF1: Fill<Voronoi2D<LatticeNormal, Worley, F1>>(settings); break;
        }
    }

    // Same sampling as MapNoiseBridge2D: cell centres in [0,1], four at a time,
    // but the value is stored raw (no remap, no clamp).
    void Fill<N>(Settings settings) where N : struct, INoise
    {
        int n = resolution;
        if (values == null || values.Length != n * n) values = new float[n * n];
        float inv = 1f / n;

        for (int y = 0; y < n; y++)
        {
            float pz = (y + 0.5f) * inv;
            for (int x = 0; x < n; x += 4)
            {
                float4 px = new float4(x, x + 1, x + 2, x + 3) * inv + 0.5f * inv;
                float4 v = GetFractalNoise<N>(new float4x3(px, 0f, pz), settings).v;
                for (int k = 0; k < 4 && x + k < n; k++)
                    values[y * n + x + k] = v[k];
            }
        }
    }

    // Land: raw value -1 → black, 0 → mid grey, +1 → white.
    // Water (below the threshold): blue, darker with depth.
    void Paint()
    {
        int n = resolution;
        if (texture == null || texture.width != n)
        {
            if (texture != null) Destroy(texture);
            texture = new Texture2D(n, n, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
        }

        var pixels = new Color32[n * n];
        for (int i = 0; i < pixels.Length; i++)
        {
            float v = values[i];
            byte g = (byte)(math.saturate(v * 0.5f + 0.5f) * 255f);
            if (v < threshold)
            {
                float depth = math.saturate((threshold - v) / 0.5f);
                pixels[i] = Color32.Lerp(new Color32(70, 150, 220, 255), new Color32(10, 30, 90, 255), depth);
            }
            else
            {
                pixels[i] = new Color32(g, g, g, 255);
            }
        }
        texture.SetPixels32(pixels);
        texture.Apply();
    }

    // Step 4: on-screen controls. Everything is laid out in a virtual screen 800 units
    // tall and scaled to the real one, so text and sliders stay finger-sized on a phone.
    const float VirtualHeight = 800f;

    static readonly string[] TypeLabels =
        { "Value", "Perlin square", "Simplex old", "Simplex Perlin", "Simplex", "Voronoi F1" };
    static readonly string[] WaterLabels = { "Raw threshold", "% water" };

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

        DrawControls(controls);
    }

    void DrawControls(Rect area)
    {
        GUILayout.BeginArea(area);
        GUI.changed = false;

        type = (NoiseType)GUILayout.SelectionGrid((int)type, TypeLabels, 3, GUILayout.Height(60f));

        GUILayout.BeginHorizontal();
        GUILayout.Label($"Seed {seed}", GUILayout.Width(110f));
        if (GUILayout.Button("−", GUILayout.Height(30f))) seed--;
        if (GUILayout.Button("+", GUILayout.Height(30f))) seed++;
        if (GUILayout.Button("Random", GUILayout.Height(30f))) seed = UnityEngine.Random.Range(0, 100000); // UI only; the noise itself stays seeded
        GUILayout.EndHorizontal();

        frequency = IntSlider("Frequency", frequency, 1, 32);
        octaves = IntSlider("Octaves", octaves, 1, 6);
        lacunarity = IntSlider("Lacunarity", lacunarity, 2, 4);
        gain = Slider("Gain", gain, 0f, 1f, "0.00");

        waterMode = (WaterMode)GUILayout.Toolbar((int)waterMode, WaterLabels, GUILayout.Height(30f));
        if (waterMode == WaterMode.RawThreshold)
            rawThreshold = Slider("Threshold", rawThreshold, -1f, 1f, "+0.00;-0.00");
        else
            waterPercent = Slider("Water %", waterPercent, 0f, 100f, "0");

        if (GUI.changed) dirty = true;
        GUILayout.EndArea();
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
        GUI.DrawTexture(new Rect(left, top, side, side), texture);

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
        DrawMarker(plot, threshold, new Color(0.3f, 0.7f, 1f));    // water line in blue

        var numbers = new Rect(plot.x, plot.yMax + 4f, plot.width, 60f);
        GUI.Label(
            numbers,
            $"min {min:+0.00;-0.00}   max {max:+0.00;-0.00}   " +
            $"p1 {p1:+0.00;-0.00}   p99 {p99:+0.00;-0.00}\n" +
            $"mean {mean:+0.000;-0.000}   sd {sd:0.000}   ({values.Length} samples)\n" +
            $"water {waterFraction * 100f:0.0}%   threshold {threshold:+0.000;-0.000}   " +
            (waterMode == WaterMode.Percentage ? "(from %)" : "(raw)"));
        return numbers.yMax;
    }

    void DrawMarker(Rect plot, float value, Color color) =>
        DrawRect(new Rect(plot.x + (value * 0.5f + 0.5f) * plot.width - 1f, plot.y, 2f, plot.height), color);

    static void DrawRect(Rect r, Color color)
    {
        var previous = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(r, Texture2D.whiteTexture);
        GUI.color = previous;
    }
}
