# Rainshadow on a grid: practical algorithms for procedural worldgen

**The best rainshadow approximation for a finite 2D grid is a single-pass wind-sorted moisture sweep, costing O(N log N) and producing visible wet/dry asymmetry with roughly 30 lines of code.** This approach, pioneered by Amit Patel in mapgen4 and closely mirroring what Dwarf Fortress likely does internally, sorts all cells by their projection onto a wind vector, then processes them upwind-to-downwind while tracking a carried humidity value that mountains deplete. The technique is native to regular grids, runs in under **2 ms on a 512×512 map**, and requires only an elevation field and a prevailing wind direction as inputs. For island-scale maps where mountain ranges span just 20–40 tiles, the effect is subtle but measurable — and whether the payoff justifies the implementation depends on how much the game's biome system leverages the resulting moisture gradient.

Six distinct approaches to grid-based rainshadow simulation were found across game worldgen pipelines, academic models, and open-source PCG projects. They range from trivial ray-cast heuristics to FFT-based physics models, all operating on regular 2D grids at costs between O(N) and O(365N). The sections below document each at implementation depth, assess their tradeoffs, and close with a minimum viable algorithm for the Islands target.

---

## How Dwarf Fortress handles rainshadow (and what remains unknown)

Dwarf Fortress remains the benchmark. Its rainshadow pass runs **after all terrain is finalized** — specifically after erosion, river carving, and final elevation smoothing — and modifies the rainfall field that was initially seeded via midpoint displacement. Tarn Adams confirmed this ordering in a Gamasutra interview preserved on the DF Wiki, noting that "now that the elevations are finalized, it makes adjustments to rainfall based on rain shadows and orographic precipitation." He later wrote in Game AI Pro 2 that "world maps improved greatly when rain shadows were taken into consideration."

The conceptual model is straightforward: **moist air from ocean tiles is transported by a prevailing wind, forced upward by terrain, and depleted of moisture proportionally to elevation gain**. The DF Wiki's description of the toggle states: "As the terrain gets higher, it forces the moist air up, causing it to rain on the seaward side of a mountain. Eventually, all the rain has fallen if the mountain is tall enough." This implies a full-depletion model where sufficiently high mountains can reduce leeward moisture to near zero.

Several details are confirmed. The system uses a **single prevailing wind direction per generated world**, strongly inferred from PerfectWorldDF (a DF world-painting tool) which exposes a "Wind Direction" parameter and regenerates its rain shadow map when this parameter changes. Rain shadows **only apply where drainage ≥ 50** — a confirmed and reproduced behavior documented in DF bug reports. The orographic precipitation toggle can push rainfall **above or below** the configured min/max rainfall parameters in Advanced World Generation.

What remains unknown is the exact formula. DF's ~700,000-line C++ codebase is proprietary, and no reverse-engineering of the orographic precipitation function has been published. The community consensus is that it performs a directional sweep (column-by-column for a westerly wind), initializing moisture from ocean tiles and depleting it at each cell based on elevation gain, but the specific constants, decay rates, and whether it uses a linear or exponential depletion function are all undetermined. DF's world sizes range from **17×17 (pocket) to 257×257 (large)** in region tiles, placing the rainshadow computation squarely in the target range for this analysis.

---

## Mapgen4's wind-sorted sweep adapts directly to grids

Amit Patel's mapgen4 contains the most clearly documented rainshadow algorithm in any open-source PCG project. Despite running on a Voronoi mesh, its core logic is **topology-independent** and adapts to a regular grid with simplifications rather than complications.

The algorithm has four essential steps. First, a single prevailing wind angle θ defines a direction vector `[cos(θ), sin(θ)]`. Each cell receives a scalar sort key equal to its position projected onto this vector: `key = x·cos(θ) + y·sin(θ)`. All cells are sorted by this key, producing an upwind-to-downwind processing order. Second, cells are visited in sorted order. For each cell, **humidity is gathered by averaging the humidity values of upwind neighbors** — those whose sort key is smaller. On a Voronoi mesh this means checking ~6 irregular neighbors; on a regular grid it means checking the 3–4 of 8 neighbors that have a negative dot product with the wind vector, weighted by alignment. Third, water cells add moisture: `humidity += evaporation`. Fourth, elevation caps moisture capacity: `cap = 1.0 - elevation`. If carried humidity exceeds this cap, the excess precipitates as orographic rainfall (`rainfall += rain_shadow × (humidity - cap)`) and humidity is clamped to the cap. A small fraction of carried humidity also falls as background precipitation everywhere.

The key parameters exposed as sliders are **wind_angle_deg** (0–360°), **raininess** (default 0.9, scales all precipitation), **evaporation** (default 0.5, moisture pickup over water), and **rain_shadow** (default 0.5, orographic intensity). The computational cost is **O(N log N)** dominated by the sort, plus O(N) for the sweep — Amit reports the entire pipeline including rendering runs in ~30 ms for 25,000 Voronoi cells.

Adapting to a regular grid eliminates the mesh library entirely. Neighbor lookup becomes O(1) with fixed offsets. The sort step can be replaced by a deterministic traversal order for axis-aligned winds (simply iterate columns left-to-right for westerly wind), though for arbitrary angles the sort over N = W×H cells remains trivially fast. The moisture interpolation step (regions → triangles) disappears since both live on the same grid cells. **The only new concern is grid axis bias**: for diagonal winds, the 8-connected neighbor discretization produces blockier gradients than an irregular mesh. Weighting neighbor contributions by `max(0, dot(windVec, normalize(neighbor_offset)))` mitigates this effectively.

Amit acknowledged one artifact: "moisture going through a mountain pass and creating a path of green in the middle of desert." This **mountain-pass leaking** is actually worse on regular grids where passes align with grid axes. Adding slight noise to elevation before the sweep, or using a wider neighbor kernel for the humidity gather, helps mask this.

Grid pseudocode for the complete algorithm:

```python
def rainshadow(elevation, wind_deg, raininess=0.9, evaporation=0.5, shadow=0.5):
    H, W = elevation.shape
    θ = radians(wind_deg)
    wv = (cos(θ), sin(θ))
    
    # Sort cells by wind projection (upwind first)
    cells = sorted(
        ((r, c) for r in range(H) for c in range(W)),
        key=lambda rc: rc[1]*wv[0] + rc[0]*wv[1]
    )
    
    humidity = zeros((H, W))
    rainfall = zeros((H, W))
    NEIGHBORS = [(-1,-1),(-1,0),(-1,1),(0,-1),(0,1),(1,-1),(1,0),(1,1)]
    
    for r, c in cells:
        # Weighted average of upwind neighbors
        h_sum, w_sum = 0.0, 0.0
        for dr, dc in NEIGHBORS:
            nr, nc = r+dr, c+dc
            if 0 <= nr < H and 0 <= nc < W:
                dot = dc*wv[0] + dr*wv[1]
                if dot < 0:  # neighbor is upwind
                    w = abs(dot) / sqrt(dr*dr + dc*dc)
                    h_sum += humidity[nr, nc] * w
                    w_sum += w
        h = h_sum / w_sum if w_sum > 0 else 0
        
        if elevation[r, c] <= 0:  # water
            h += evaporation
        
        elev = max(0, elevation[r, c])
        cap = 1.0 - elev
        rain = 0.0
        if h > cap:
            rain += shadow * (h - cap) * raininess
            h = cap
        rain += raininess * h * 0.08  # background drizzle
        
        humidity[r, c] = h
        rainfall[r, c] = rain
    return rainfall
```

For a **256×256 grid** (65,536 cells), the sort takes ~1M comparisons (~0.3 ms) and the sweep touches each cell once with 8 neighbor lookups (~0.5M operations, ~0.2 ms). **Total: well under 1 ms.** For 512×512 (262,144 cells): ~2 ms. This is negligible even within an interactive worldgen loop.

---

## The FFT approach produces physics-quality results with zero iteration

An entirely different strategy avoids moisture advection altogether. The Smith & Barstad (2004) Linear Theory of Orographic Precipitation solves the problem in Fourier space: take the 2D FFT of the terrain, multiply by a wavenumber-dependent transfer function that encodes wind speed, cloud conversion time, fallout distance, and moist stability, then inverse-FFT to get the precipitation field. The result is a **single, non-iterative computation** that produces physically grounded windward enhancement and leeward drying.

The transfer function `T(k, l)` encapsulates five length scales: mountain width, a buoyancy wave scale, the moist layer depth, and two condensed-water advection distances. For PCG purposes, the key parameters are **wind speed and direction** (U, V), **conversion time** τc (how quickly vapor becomes cloud droplets, ~1000 s), **fallout time** τf (how quickly droplets precipitate, ~1000 s), and **background precipitation** P∞. The Python implementation is available as `pip install orographic_precipitation` from the FastScape project, and a QGIS plugin exists for GIS applications.

The computational cost is **O(N log N)** — identical asymptotically to the sweep approach, but dominated by two FFT passes rather than a sort. For a 256×256 grid, NumPy's FFT runs in roughly **5–10 ms** in Python, or sub-millisecond in C with FFTW. The approach is natively grid-based and handles 2D terrain naturally.

The tradeoff is parameter interpretability. The sweep method's four sliders (wind angle, raininess, evaporation, rain shadow strength) map directly to intuitive design intent. Smith-Barstad's parameters (τc = 1000 s, Nm = 0.005 s⁻¹, Hw = 5000 m) are atmospheric physics constants that require tuning to produce aesthetically pleasing game output. For a PCG pipeline where the designer wants direct artistic control over "how dramatic is the rain shadow," **the sweep method wins on ergonomics**. For a pipeline that wants a physically plausible result with minimal hand-tuning, the FFT method wins on output quality.

---

## Three cheaper alternatives and when they make sense

**Single-pass row sweep (O(WH), no sort).** For axis-aligned wind, skip the sort entirely and iterate rows in the wind direction. Each row is independent: initialize moisture at the upwind edge (from ocean cells), march downwind, deplete moisture at mountains, deposit remainder as rain. This is the simplest possible implementation — a nested for-loop with no data structures beyond the elevation and rainfall arrays. It cannot handle arbitrary wind angles without rotating the grid, and it produces no lateral moisture diffusion (each row is independent), leading to visible horizontal streaking on diagonal terrain features. **Best for prototyping or game jams where development time matters more than output quality.**

**Ray-cast shadow check (O(N × d), where d is check distance).** For each cell, cast a ray upwind for d tiles and record the maximum "blocking" from mountains encountered. Subtract this blocking from a base moisture value. This is topologically identical to terrain shadow-casting for lighting. Jason Dookeran's terrain generator uses `d = 15` tiles and a blocking factor of `(elevation - 0.7) × 0.5` for mountains above 0.7 threshold. The result is a binary-ish wet/dry pattern — functional but lacking the smooth moisture gradients of advection-based methods. **Best when rainshadow is a secondary visual cue rather than a biome driver.**

**Cellular automata weather simulation (O(365 × WH)).** Nick McDonald's procedural weather system models humidity advection, diffusion, evaporation, and precipitation as a cellular automaton stepped ~365 times (one simulated year). Each timestep computes wind from a global vector, advects humidity to downwind cells, diffuses with neighbors, adds evaporation over water, removes moisture as rain, and tracks temperature via adiabatic cooling. The result is **dramatically better** than single-pass methods — emergent weather fronts, realistic orographic lift, smooth moisture gradients — but costs 365× more computation. For a 256×256 grid this is still only ~100–200 ms in C++, which is acceptable for offline worldgen but not interactive editing. **Best when biome realism is a priority and generation time is not frame-bound.**

---

## Parameter sensitivity and what the designer actually controls

Across all approaches, the core parameters reduce to a small set of design levers:

| Parameter | Effect | Typical range | Sensitivity |
|-----------|--------|---------------|-------------|
| **Wind direction** | Determines which side of mountains is wet/dry | 0–360° | High — rotates entire moisture pattern |
| **Evaporation rate** | How much moisture enters over water | 0.1–1.0 | Medium — scales total available moisture |
| **Orographic factor** | How aggressively mountains extract moisture | 0.1–2.0 | High — controls rain shadow intensity |
| **Background precipitation** | Baseline rainfall everywhere | 0.0–0.2 | Low — prevents total deserts on lee side |
| **Moisture decay** | Continental drying over flat terrain | 0.95–1.0 per cell | Low at small scales, high at continental |

At **128×128**, wind direction and orographic factor dominate. Moisture decay over flat terrain is nearly invisible because the map is too small for continental-scale drying. At **512×512**, decay becomes relevant and a second wind direction or seasonal variation starts to matter. At DF's 257×257, all parameters contribute visibly.

One underappreciated parameter is **elevation normalization**. If the elevation field ranges from 0.0 to 1.0 with mountains peaking at 0.8, a cap of `1.0 - elevation` leaves 0.2 capacity at summits — enough moisture passes through to soften the shadow. If mountains peak at 1.0, the cap drops to 0 and the shadow is absolute. **The relief ratio of the terrain determines rainshadow intensity as much as the algorithm parameters do.**

---

## How scale affects whether rainshadow is worth implementing

The geographic visibility of rainshadow depends on the ratio between mountain range width and map size. Real rainshadows extend **hundreds of kilometers** downwind — the Great Basin behind the Sierra Nevada spans ~800 km. On a 256×256 tile map where each tile represents, say, 100 m, the entire map is 25.6 km across. A mountain range might span 10–30 tiles (1–3 km). The resulting rain shadow extends perhaps 20–50 tiles downwind — visible, but compressed.

At **128×128** with island-scale terrain, mountains are small bumps. The rainshadow effect is a gradient of perhaps 15–25 tiles wide. It will produce a subtle moisture differential that shows up in biome assignment (slightly drier grassland on the lee side of a ridge) but won't create dramatic desert-versus-jungle contrasts. Whether this is "worth it" depends entirely on the biome system's granularity. If the game distinguishes only 4–5 biomes, the rainshadow's contribution may be invisible. If it distinguishes **8–12 biomes** with moisture-sensitive subtypes (wet forest vs. dry forest, marsh vs. meadow), the asymmetry becomes a genuine geographic feature that players can navigate and reason about.

At **256×256**, rainshadow effects are clearly visible on any mountain range spanning 20+ tiles. The wet/dry asymmetry creates natural biome boundaries that feel geographically motivated rather than noise-generated. This is the sweet spot for the technique.

At **512×512**, the technique produces continental-scale climate patterns. Multiple wind passes or seasonal variation become warranted. This is where DF operates and where rainshadow most dramatically improves output.

---

## Feasibility assessment: the minimum viable rainshadow for Islands

**Target**: ~256×256 grid, elevation field with known prevailing wind direction, island-scale map.

**Minimum viable algorithm**: The mapgen4 wind-sorted sweep, adapted to a grid. Sort all 65,536 cells by `cos(θ)·col + sin(θ)·row`. Process upwind-to-downwind. At each cell, average humidity from upwind 8-connected neighbors (weighted by wind alignment), add evaporation over water, cap humidity at `1.0 - elevation`, precipitate excess as orographic rain, store humidity and rainfall. Single pass, no iteration.

**Expected cost**: Under **1 ms** in any compiled language, ~5 ms in Python with NumPy vectorization. Negligible relative to terrain generation (typically 50–500 ms for noise + erosion).

**Expected output**: Visible wet/dry asymmetry on any mountain ridge spanning ≥10 tiles. Windward slopes receive **2–5× more rainfall** than leeward slopes, depending on the `rain_shadow` parameter. The effect is strongest on the tallest, widest ridges. On an island map with a central mountain spine and prevailing westerly wind, the western coast will be lush and the eastern interior noticeably drier — a pattern immediately recognizable to anyone who has seen Hawaii, New Zealand's South Island, or the Pacific Northwest.

**Implementation complexity**: ~30–50 lines of core logic. No external dependencies. No iteration. No convergence testing. The sort can be precomputed once and reused if the wind direction doesn't change between regenerations.

**Is the payoff worth it?** For Islands specifically, the answer is **conditionally yes**, with two caveats:

- **The biome system must be moisture-sensitive enough to express the gradient.** If rainfall feeds into a continuous moisture field that determines vegetation density, river flow, soil color, and biome selection, the rainshadow creates a visible and gameplay-relevant geographic asymmetry for essentially zero cost. If the biome system uses Perlin noise for moisture independently of terrain, adding rainshadow to the pipeline requires also rewiring the biome system to consume the new rainfall field.

- **The island's mountains must be large enough to create a meaningful shadow.** On a small atoll with peaks of 5–10 tiles, the shadow is negligible. On an island with a **20–40 tile mountain spine** (the Kauai or Maui archetype), the shadow creates a dramatic windward/leeward split that is both visually striking and strategically interesting. If the island generator consistently produces such terrain, the rainshadow is unambiguously worth implementing. If islands are flat with scattered hills, the effect vanishes and the 30 lines of code produce no visible payoff.

The recommendation: **implement it.** The cost is trivial, the algorithm is simple and debuggable, and it transforms rainfall from "random noise that happens to be there" into "a physical consequence of terrain that players can see and reason about." Even if the effect is subtle at island scale, it creates the kind of geographic coherence — wet western forests giving way to dry eastern scrubland — that makes procedural worlds feel designed rather than generated.