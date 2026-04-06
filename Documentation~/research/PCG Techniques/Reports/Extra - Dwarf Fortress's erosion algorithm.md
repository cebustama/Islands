# Dwarf Fortress's erosion algorithm: what we know and what remains black-boxed

Dwarf Fortress uses a **greedy agent-based channel carver** — not a physically-based hydraulic erosion simulation — during world generation. Temporary "fake rivers" launch from mountain edges, trace steepest-descent paths, and lower terrain when they cannot flow downhill, carving channels that permanent rivers later follow. This system is documented almost entirely from two Tarn Adams interviews (2008 and ~2019) plus community parameter testing. **No pseudocode, formal specification, or code-level reverse engineering of the erosion algorithm has ever been published.** The algorithm is simple by design: Tarn Adams estimated the combined river-and-rain-shadow system at roughly 1,000 lines of C/C++ code. Despite DF's outsized influence on procedural generation, no academic paper reconstructs the erosion algorithm in detail — they cite DF only as an exemplar of simulation-based terrain generation.

---

## The algorithm in Tarn Adams's own words

Two developer quotes form the canonical description of DF's erosion pass. The earlier and more mechanistically specific comes from a 2008 Gamasutra interview (now hosted at gamedeveloper.com):

> "It picks out the bases of the mountains (mountains are all squares above a given elevation), then it runs temporary river paths out from there, **preferring the lowest elevation and digging away at a square if it can't find a lower one**, until it gets to the ocean or gets stuck. This is the phase where you see the mountain being worn away during world creation. I have it intentionally center on a mountain at that point so you can watch. This will generally leave some good channels to the ocean, so it runs the real rivers after this. However, some of them don't make it, so it forces paths to the ocean after that, and bulges out some lakes. Then it loop-erases the rivers, and sends out (invisible on the world map) brooks out from them."

A later interview (~2019, cited on the DF2014 wiki from gamedeveloper.com) gives additional context on smoothing and material independence:

> "Many fake rivers flow downward from these points, carving channels in the elevation field if they can't find a path to the sea. **Extreme elevation differences are often smoothed here so that everything isn't canyons.** Ideally we'd use mineral types for that, but we don't yet. Lakes are grown out at several points along the rivers."

These two passages — confirmed at the highest confidence level as direct developer statements — are the foundation of everything known about the erosion system. The Game AI Pro 2 chapter authored by Tarn Adams (2015) adds only a single summary sentence: "In the next phase of the simulation, temporary rivers wear down the mountains, followed by permanent rivers that flow from high ground to low ground." His 2013 Reddit AMA confirmed the scale: "Adding rivers and other effects like rain shadows can put you up another **1000 or so [lines]** depending on what you do."

---

## Reconstructed pipeline: the erosion pass step by step

Synthesizing all sources, the erosion/river stage operates within the "Running rivers…" phase of world generation, after elevation fractal generation and pre-erosion smoothing, and before vegetation and biome finalization. The full sequence is:

**Pre-erosion setup.** A midpoint displacement fractal generates the base elevation field. A non-linear parabola is applied to shape mountains. Five scalar fields (elevation, rainfall, temperature, drainage, volcanism) are seeded and filled fractally. Volcanoes are placed. Mid-level elevations are smoothed to create more plains. Small/inland oceans are then dried out.

**Erosion agent spawning.** The system identifies mountain edges — tiles at the boundary of the mountain mass, where "mountains are all squares above a given elevation." The wiki documents that river start locations require a minimum elevation of **300** (on a 0–400 scale), and mountain peaks form at elevation ≥ **400** (pre-erosion ≥ 380 for peak placement candidates). Agents are spawned specifically at the "bases" or "edges of mountain sides," not at peaks or uniformly across all high-elevation tiles.

**Agent traversal and carving.** Each fake river agent traces a path using **greedy steepest-descent**: it moves to whichever adjacent cell has the lowest elevation. If no adjacent cell is lower than the agent's current position, the agent **"digs away at a square"** — it lowers the elevation of the tile to create a downhill path. The agent continues until it reaches the ocean or gets stuck. This carving is the core erosion mechanic: purely subtractive elevation reduction with no sediment tracking or deposition.

**Iterative cycling.** The `EROSION_CYCLE_COUNT` parameter (default **50**) controls how many times this process repeats. Each cycle presumably launches another batch of agents from mountain edges, progressively deepening channels and wearing down peaks. Community testing shows values of **~250** produce well-eroded, realistic terrain, while maximum values cause mountains to "dissolve before your eyes into plains."

**Extreme cliff smoothing.** If `PERIODICALLY_ERODE_EXTREMES` is enabled (default: yes), extreme elevation differences are smoothed during the erosion process. This prevents the terrain from becoming nothing but canyons. The wiki describes this as converting "every impassable rock wall into a series of ramps."

**River finalization.** After the erosion cycles complete, "real" permanent rivers are placed along the carved channels. Rivers that failed to reach the ocean have paths **forced to the ocean**. Lakes are "grown out" (suggesting a flood-fill expansion) at several points along the rivers. River paths are **loop-erased** — a standard technique from random walk theory that removes self-intersecting loops from paths. Flow amounts are calculated to classify rivers as tributaries. Finally, invisible brooks are sent out from rivers.

**Post-erosion adjustments.** Elevations receive a final smoothing pass from mountains to sea. Peaks and volcanoes do local adjustments. Rainfall is recalculated with orographic precipitation and rain shadows (if enabled). Temperature is reset based on the new elevations, and vegetation is recalculated.

---

## Answers to the eight specific algorithmic questions

### 1. Hydraulic vs. thermal erosion: a single unified process

DF does **not** distinguish hydraulic from thermal erosion. There is one erosion mechanism: agent-based channel carving by fake rivers. Tarn Adams explicitly confirmed that material types do not influence erosion resistance: "Ideally we'd use mineral types for that, but we don't yet." There is no freeze-thaw cycle, no slope-dependent weathering, and no rainfall-weighted erosion rate. The system is a single, unified, material-independent process.

**Confidence:** Confirmed (Tarn Adams, ~2019 interview).

### 2. Agent spawning: mountain edges, not all cells above a threshold

Agents are spawned at the **edges/bases of mountains**, not at all high-elevation cells or at peaks. Tarn's 2008 description: "It picks out the bases of the mountains (mountains are all squares above a given elevation)." His later description: "it locates edges of mountain sides where it can run test rivers." The 40d wiki notes that mountain peaks are placed at pre-erosion elevation ≥ 380 and "withstand erosion better" — suggesting peaks may receive some protection, though the mechanism is undocumented. River start locations require minimum elevation **300**, which likely approximates the mountain-edge threshold.

**Confidence:** Confirmed (Tarn Adams, both interviews). Exact threshold value is community-inferred.

### 3. No flow accumulation — independent downhill tracing

Each agent independently traces a downhill path. There is **no flow accumulation step** during the erosion pass — no drainage basin calculation, no rainfall-weighted water volume, no convergence of flow into accumulated streams. The agents operate independently and greedily. However, *after* erosion and river placement, during the "Forming Lakes" phase, "flow amounts are calculated to determine which rivers are tributaries" — this is a post-hoc classification step, not an erosion-time simulation.

**Confidence:** High (inferred from Tarn Adams's descriptions, which describe only independent agents with no mention of accumulation during erosion).

### 4. Erosion cycle count: the key parameter

| Property | Value |
|----------|-------|
| **Token** | `[EROSION_CYCLE_COUNT:<number>]` |
| **Default** | 50 |
| **Practical range** | 0–250 (higher values accepted but cause excessive flattening) |
| **Effect** | Controls the number of erosion iterations. Each cycle runs another batch of fake-river agents from mountain edges |

At **0**, no erosion occurs — terrain retains its raw fractal character. At the default **50**, moderate erosion produces reasonably jagged mountains with some carved channels. Community consensus recommends **~250** for well-eroded, natural-looking terrain. At very high values, mountains "dissolve before your eyes into plains," often triggering world rejection because insufficient mountain tiles remain for river start points or dwarven civilization placement. The wiki also notes that "extremely high pre-erosion [river count] values speed erosion greatly," suggesting the pre-erosion river count interacts with erosion by determining how many agents are launched per cycle.

**Confidence:** Parameter values confirmed (game configuration files, wiki). Internal mechanism (one batch per cycle) is community inference.

### 5. When agents can't descend: carving first, lakes second

When an agent cannot find a lower neighbor, it **carves** — it lowers the elevation of a tile. This is the primary erosion mechanic, stated directly by Tarn: "preferring the lowest elevation and **digging away at a square if it can't find a lower one.**" The agent continues carving until it reaches the ocean or gets "stuck." Stuck agents do not directly create lakes; instead, lake formation is a **separate subsequent step** where lakes are "grown out at several points along the rivers" and "bulged out." The exact conditions under which an agent is considered "stuck" versus continuing to carve are undocumented. Rivers that never reach the ocean have paths "forced to the ocean" as a cleanup step.

**Confidence:** Confirmed (Tarn Adams, 2008 interview). The threshold for "stuck" is unknown.

### 6. No sediment tracking — erosion is purely subtractive

Erosion in DF is **purely subtractive**: it only lowers elevation values. There is no sediment variable, no deposition in lowlands, no alluvial fan formation, and no tracked material transport. Tarn's remark that mineral types are not yet used confirms that no material properties participate in the erosion calculation. The `PERIODICALLY_ERODE_EXTREMES` smoothing pass reduces extreme height differences but does this through elevation adjustment, not sediment redistribution. The drainage scalar field (range 0–100) affects biome classification (low drainage → swamps/marshes) but is a separate pre-generated field, not a product of the erosion simulation.

**Confidence:** Confirmed (absence documented across all sources; Tarn Adams's mineral-types remark).

### 7. No erosion rate formula has been disclosed

**No specific formula for elevation reduction per step has been published** — not by Tarn Adams, not in any community reverse engineering, not in any academic paper. From the descriptions, we know only the behavioral rule: if no lower neighbor exists, lower a tile's elevation by some amount and continue. Whether the reduction is a fixed constant, proportional to the elevation difference, or dependent on any other variable is entirely unknown. The DFHack project has mapped the `worldgen_parms` data structure (confirming `erosion_cycle_count`, `periodically_erode_extremes`, and `orographic_precipitation` as `int32_t` fields) but has not reverse-engineered the erosion function's internal logic. Quietust's extensive disassemblies of multiple DF versions exist but no readable pseudocode of the erosion algorithm has been extracted from them.

**Confidence:** Confirmed absence (no source provides this information).

### 8. Agent spawning and path selection mechanics

Synthesizing all sources, the agent lifecycle is:

1. **Spawn** at mountain edge tiles (elevation ≥ ~300, at the boundary of the mountain mass)
2. **Select path** by greedy steepest descent — choose the neighbor with the lowest elevation
3. **If blocked** (no lower neighbor), lower the current or adjacent tile's elevation ("dig away")
4. **Continue** tracing downhill through the newly carved path
5. **Terminate** when reaching ocean tiles (elevation < 100) or when stuck
6. After all agents in a cycle complete, optionally smooth extreme cliffs
7. Repeat for `EROSION_CYCLE_COUNT` iterations

The number of agents spawned per cycle and their exact distribution across mountain edges is undocumented. The `RIVER_MINS` parameter (`[RIVER_MINS:<pre>:<post>]`, default `1:1`) sets minimum riverhead counts before and after erosion; the pre-erosion value appears to influence how many agents are launched, since "extremely high pre-erosion values speed erosion greatly." Setting pre-erosion river count to **800** is documented as producing more lakes, while setting it to **0** eliminates extra canyons.

**Confidence:** Path selection and carving behavior confirmed (Tarn Adams). Agent count and distribution per cycle are undocumented.

---

## What the reverse engineering community has found

The DFHack project and Quietust's disassembly work represent the most advanced attempts at understanding DF internals, but **neither has published the erosion algorithm's logic**. DFHack's `df-structures` repository maps the `worldgen_parms` struct, confirming the parameter field names match the wiki tokens. A DFHack GitHub discussion (#3774) explicitly states: "I've checked the API/df-structures/forums and haven't found evidence of a clear way to hook into specific parts of the worldgen process." Quietust has "nearly completely documented disassemblies" of versions 0.23 through 0.47, but no readable erosion pseudocode has been published from this work. The worldgen erosion internals remain effectively **black-boxed at the code level**.

---

## How erosion reshapes the biome map

Community testing has documented several emergent effects of the erosion pass on biome distribution. **Mountain regions increase in count** after erosion because rivers fragment contiguous mountain masses into separate smaller regions — you gain mountain *regions* even as total mountain area decreases. **Marsh regions decrease in count** because rivers and lakes cause marshes to merge into larger contiguous areas. These effects are accounted for in DF's parameter system through separate pre-erosion ("Minimum Initial") and post-erosion ("Minimum Final") biome count thresholds in `REGION_COUNTS`. The interaction between the erosion and drainage fields is indirect: the drainage scalar field is generated independently before erosion and influences biome classification (low drainage → swamps, high drainage → hills), but does not appear to modify the erosion rate itself.

---

## Conclusion

The Dwarf Fortress erosion system is elegant in its simplicity: a greedy particle-tracer that carves the heightmap by brute force, iterated enough times to produce convincing drainage networks. The entire documented algorithm can be summarized as **"spawn agents at mountain edges, walk downhill, dig when stuck, repeat."** The key insight is the two-phase design — fake rivers carve the terrain, then real rivers are placed along the carved channels — which decouples the erosion simulation from the final hydrological network. Three critical gaps remain entirely undocumented: the per-step erosion magnitude, the agent density per cycle, and the exact smoothing algorithm. These would require binary analysis of the DF executable to resolve, and despite decades of community effort, no one has yet published that level of detail. DF's erosion system stands as a case study in how a remarkably simple agent-based rule — greedy descent with terrain modification on failure — can produce complex, realistic-looking drainage patterns when iterated at scale.