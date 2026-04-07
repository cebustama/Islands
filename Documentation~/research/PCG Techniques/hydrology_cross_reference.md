# Technique Cross-Reference: Hydrology Systems

**Workstream:** Islands.PCG Research / Analysis
**Scope:** Extends the cross-check pattern (same as `noise_cross_reference.md` and
`terrain_biome_cross_reference.md`) to the hydrology technique family feeding Phase L
(Water Systems) and interacting with Phase M (moisture field consumes FlowAccumulation).
**Date:** 2026-04-06
**Source authority:** PCG technique reports (2a, 2b, 6b), six game worldgen reports,
DF erosion gap research, technique_integration_matrix.md (§5, update target),
Phase_L_Design.md, Phase_M_Design.md

---

## 1. Hydrology Techniques — Game × Technique Status Grid

Techniques from Pass 2a/2b: water system generation including depression filling, flow
routing, river extraction, lake detection, and pipeline ordering decisions.

Legend: ✓ = confirmed use | ~ = inferred / partial | ✗ = confirmed absent |
? = insufficient evidence | — = not applicable (architecture incompatible)

### 1.1 Grid: Hydrology Techniques

| Technique | Minecraft | Dwarf Fortress | No Man's Sky | RimWorld | Cataclysm:DDA | Tangledeep |
|---|---|---|---|---|---|---|
| **Depression filling** | ✗ | ~ | — | ✗ | — | — |
| **D8 / D∞ flow direction** | ✗ | ~ | — | ✗ | — | — |
| **Flow accumulation** | ✗ | ✗ | — | ✗ | — | — |
| **River extraction (threshold)** | ✗ | ✗ | — | ✗ | — | — |
| **Lake detection / classification** | ~ | ✓ | ✗ | ✗ | ✗ | — |
| **River-first vs terrain-first ordering** | Terrain-first | Erosion-first | — | Graph-first | Graph-first | — |
| **Agent-based river carving** | ✗ | ✓ | ✗ | ✗ | ✗ | — |
| **Pathfinding-based river placement** | ✗ | ✗ | ✗ | ✓ | ~ | — |
| **Strahler ordering** | ✗ | ✗ | ✗ | ✗ | ✗ | — |

### 1.2 Notes per cell

**Depression filling (Priority-Flood or equivalent):**
- **Minecraft (✗):** No heightmap drainage processing. Rivers are a biome type assigned
  by the multi-noise parameter system (low Peaks-and-Valleys values produce "river" biome
  classification). There is no water-flow simulation at any stage of terrain generation.
- **Dwarf Fortress (~):** Not a formal Priority-Flood algorithm. DF's erosion agents
  trace steepest-descent paths from mountain edges and lower terrain when no downhill
  neighbor exists — this implicitly resolves local depressions by carving through them
  rather than filling them. The effect (all drainage reaches the coast) is equivalent,
  but the mechanism is subtractive terrain modification rather than additive filling.
  Marked ~ because the functional goal (depression-free drainage) is achieved through
  a different algorithm.
- **No Man's Sky (—):** Volumetric density field with no heightmap processing. Water is
  the density isosurface below sea level. No drainage concept.
- **RimWorld (✗):** Rivers are placed by pathfinding on the world-tile graph, not derived
  from elevation drainage. No depression filling at any scale.
- **Cataclysm:DDA (—):** No elevation system. Rivers are categorical overmap terrain.
- **Tangledeep (—):** No overworld or water system.

**D8 / D∞ flow direction:**
- **Minecraft (✗):** No flow direction computation. Water blocks have a spreading mechanic
  at runtime (BFS-like fill from source blocks) but this is gameplay physics, not worldgen.
- **Dwarf Fortress (~):** Erosion agents follow steepest-descent neighbors, which is
  functionally D8 (pick the lowest 8-neighbor). However, this is embedded within the
  erosion agent loop, not computed as a standalone flow-direction grid. The agent traces
  one path per spawn rather than computing flow for all cells simultaneously. Marked ~
  because the per-cell neighbor lookup is equivalent to D8 but the usage pattern differs
  (agent-local rather than grid-global).
- **No Man's Sky (—):** No applicable terrain representation.
- **RimWorld (✗):** No flow direction computation.
- **Cataclysm:DDA (—):** No elevation system.
- **Tangledeep (—):** No overworld.

**Flow accumulation:**
- **Minecraft (✗):** No flow accumulation. River placement is purely noise-driven.
- **Dwarf Fortress (✗):** Not computed as an explicit grid-wide field. DF's erosion agents
  carve independently — there is no step where upstream cell counts are tallied across
  the map. River importance (main river vs brook) is determined by other means after
  river paths are finalized. The gap research confirms: "No flow accumulation during
  erosion; agents operate independently." Marked ✗ rather than ~ because the technique
  is genuinely absent, not merely differently implemented.
- **No Man's Sky (—):** No applicable terrain.
- **RimWorld (✗):** No flow simulation.
- **Cataclysm:DDA (—):** No elevation system.
- **Tangledeep (—):** No overworld.

**River extraction (thresholding flow accumulation):**
- **Minecraft (✗):** Rivers are a biome type. No accumulation field to threshold.
- **Dwarf Fortress (✗):** Rivers are placed along erosion-carved channels after the
  erosion pass completes. The placement criterion is channel depth and drainage path
  tracing, not flow-accumulation thresholding. River classification (major river vs
  brook) uses criteria other than upstream cell count.
- **No Man's Sky (—):** No river system.
- **RimWorld (✗):** Rivers placed by graph pathfinding, not by thresholding.
- **Cataclysm:DDA (—):** No elevation-derived features.
- **Tangledeep (—):** No overworld.

**Lake detection / classification:**
- **Minecraft (~):** Underground aquifer system uses noise-based aquifer barrier and flood
  level fields to create underground water bodies. Surface lakes are implicitly areas
  below sea level that are not ocean — but there is no explicit lake detection algorithm
  (no flood fill to distinguish ocean from inland water). The aquifer system is the
  closest analog, and it is noise-defined rather than drainage-defined. Marked ~ because
  water bodies exist but the detection mechanism is fundamentally different (noise
  threshold vs. topological classification).
- **Dwarf Fortress (✓):** Lakes are grown at points where erosion agents accumulate and
  drainage converges. After river paths are finalized, the worldgen system identifies
  depression points and grows lakes. The DF Wiki documents lake formation as part of the
  river finalization process. Additionally, DF classifies water bodies by type (brook,
  stream, river, lake) based on post-erosion terrain analysis. Confirmed.
- **No Man's Sky (✗):** Single continuous ocean per planet. No inland water bodies.
- **RimWorld (✗):** No lake generation system documented at either world or local scale.
  World-tile metadata includes river data but not lake data. Local maps may have
  marshy/swamp terrain but this is biome-driven, not topological.
- **Cataclysm:DDA (✗):** No lake generation. "Lake" overmap terrain may exist as a
  categorical type but is not derived from elevation or drainage.
- **Tangledeep (—):** No overworld.

**River-first vs terrain-first pipeline ordering:**
- **Minecraft (terrain-first):** Terrain is fully defined by the density function and
  spline system before any water features exist. Rivers are a biome category selected
  by noise parameters — they do not influence terrain shape. Water is an afterthought
  of biome classification, not a driver of terrain form.
- **Dwarf Fortress (erosion-first):** Terrain is generated, then erosion agents carve
  drainage channels, modifying the elevation field. Rivers are placed along the
  erosion-carved channels. This is a hybrid: terrain → erosion → rivers, where erosion
  acts as a terrain-modifying intermediate step. The key insight is that DF's rivers
  follow terrain that was *specifically modified* to accommodate them — neither pure
  terrain-first nor pure rivers-first.
- **No Man's Sky (—):** No river concept.
- **RimWorld (graph-first):** Rivers are placed on the world-tile graph by pathfinding
  algorithms before local maps are generated. Local terrain realization then incorporates
  river data from the world tile. At the world scale, rivers are independent of detailed
  elevation — they are graph connections derived from world-graph structure. At the local
  scale, terrain accommodates the pre-placed river.
- **Cataclysm:DDA (graph-first):** Rivers are overmap terrain connections placed during
  overmap generation. They are categorical features, not elevation-derived.
- **Tangledeep (—):** Not applicable.

**Agent-based river carving:**
- **Minecraft (✗):** No terrain modification for rivers.
- **Dwarf Fortress (✓):** Confirmed and well-characterized by gap research. "Fake river"
  agents spawn at mountain edges (elevation ≥ ~300/400), trace steepest-descent paths,
  and lower terrain when blocked. `EROSION_CYCLE_COUNT` (default 50, community-
  recommended ~250) controls iteration count. Purely subtractive — no sediment tracking.
  After erosion, "real" rivers are placed along carved channels, loop-erased to remove
  cycles, and forced to reach the ocean. Lakes grow at accumulation points. Worlds
  failing minimum river counts are rejected.
  *[Source: DwarfFortress_Worldgen_gap_erosion.md]*
- **No Man's Sky (✗):** No simulation.
- **RimWorld (✗):** No terrain modification.
- **Cataclysm:DDA (✗):** No terrain modification.
- **Tangledeep (—):** Not applicable.

**Pathfinding-based river placement:**
- **Minecraft (✗):** Rivers are noise-assigned biome regions, not pathfound routes.
- **Dwarf Fortress (✗):** Rivers follow erosion-carved channels via steepest-descent
  agent tracing, not A*/Dijkstra pathfinding.
- **No Man's Sky (✗):** No rivers.
- **RimWorld (✓):** Rivers are placed on the world-tile icosphere by pathfinding from
  mountain tiles toward ocean tiles. The pathfinding uses a cost function influenced by
  elevation gradient (preferring downhill paths). River segments are world-tile graph
  edges. At local-map scale, the river crosses the tile based on entry/exit edges
  inherited from the world graph. Confirmed from source analysis.
- **Cataclysm:DDA (~):** Rivers appear as overmap terrain connections. The placement
  mechanism is not fully documented but rivers connect map regions as linear features,
  suggesting a pathfinding or connection algorithm. Marked ~ due to limited documentation.
- **Tangledeep (—):** Not applicable.

**Strahler ordering:**
- **All six games (✗):** No game in the corpus computes Strahler stream order. DF
  classifies rivers by type (brook vs river) but uses post-erosion heuristics rather
  than formal stream ordering. Minecraft has no river hierarchy. RimWorld's world-graph
  rivers have no branching hierarchy. The technique appears only in the PCG technique
  reports (mapgen4 uses Strahler ordering on the Voronoi dual mesh) and academic
  literature. It remains a technique-report-only recommendation with no game validation
  in this corpus.

---

## 2. Islands Status: Hydrology

| Technique | Islands Status | Consuming Phase | Design Doc | Notes |
|---|---|---|---|---|
| Priority-Flood depression filling | **PLAN** | L | Phase_L_Design.md | Barnes 2014 +ε, adapted for island coastal seeding. |
| D8 flow directions | **PLAN** | L | Phase_L_Design.md | Steepest-downslope 8-neighbor. Stage-local intermediate, not persisted. |
| Flow accumulation | **PLAN** | L | Phase_L_Design.md | `MapFieldId.FlowAccumulation` scalar field. Primary simulation output. |
| River mask extraction | **PLAN** | L | Phase_L_Design.md | `MapLayerId.Rivers` by thresholding FlowAccumulation. |
| Lake detection | **PLAN** | L | Phase_L_Design.md | `MapLayerId.Lakes` via boolean mask (NOT-Land ∩ NOT-DeepWater ∩ NOT-ShallowWater). |
| Agent-based river carving | **NONE** | — | — | DF-only technique. Islands does not modify Height in Phase L. |
| Pathfinding-based river placement | **NONE** | — | — | RimWorld's approach. Not needed — drainage simulation is more principled for Islands' single-island geometry. |
| Strahler ordering | **NONE** | L2 (potential) | Phase_L_Design.md §16 | Deferred. FlowAccumulation serves as continuous proxy. |
| Erosion simulation | **NONE** | Future | — | Would run before Phase L if ever added. Not on active roadmap. |

---

## 3. Observations

### 3.1 No game uses the standard hydrology pipeline

The most striking finding of this cross-check is that **no game in the corpus implements
the standard computational hydrology pipeline** (depression filling → D8 flow direction →
flow accumulation → threshold extraction) that the technique reports recommend and that
Phase L designs around. This pipeline comes from GIS/hydrology literature (Barnes 2014,
O'Callaghan & Mark 1984) and is well-documented in PCG technique resources (Red Blob
Games, Pass 2b, Pass 6b), but it has no direct game precedent in this six-game corpus.

This does not invalidate the approach — it simply means Islands is drawing from the
academic/GIS tradition rather than copying game implementations. The standard pipeline is
well-understood, deterministic, and produces physically grounded drainage networks. It is
the technique-report-recommended approach for a reason: it produces results that games
achieve through more ad-hoc means (DF's erosion agents, RW's graph pathfinding, MC's
noise biomes).

### 3.2 Games diverge sharply on water system architecture

The six games represent four fundamentally different approaches to water:

1. **Simulation-driven (DF only):** Terrain is modified by erosion agents, then rivers are
   extracted from the modified terrain. This is the most computationally expensive approach
   and produces the most geologically plausible results. DF is the only game that modifies
   terrain to accommodate water.

2. **Noise-driven biome (MC only):** Rivers are a biome category selected by noise
   parameters (PV valleys). No physical simulation, no terrain modification. Rivers exist
   as classified regions, not as drainage features. Underground aquifers use a separate
   noise-based system.

3. **Graph pathfinding (RW, CDDA):** Rivers are connections in a world graph, placed by
   pathfinding. In RimWorld, rivers traverse the icosphere graph from mountains to coast.
   In Cataclysm, rivers are overmap terrain connections. Both treat rivers as connectivity
   features rather than drainage features.

4. **No water system (NMS, Tangledeep):** NMS has ocean as a density threshold but no
   rivers or lakes. Tangledeep has no water system.

### 3.3 Flow accumulation has zero game precedent but strong technique support

Flow accumulation — the core simulation output of Phase L — is not computed by any game
in the corpus. DF achieves drainage convergence implicitly through erosion agent behavior
but never tallies upstream cell counts. The technique exists purely in hydrology/GIS
literature and PCG technique resources.

For Islands, this is actually an advantage: flow accumulation is a clean, deterministic
scalar field that serves dual purpose — it identifies rivers (by thresholding) and
enriches Phase M's moisture field (as a continuous input). No game achieves both of these
from a single computation. DF achieves river placement through erosion but does not use
drainage for moisture. RW computes rainfall independently of river location. MC uses
independent noise for humidity.

### 3.4 Lake systems are rare and varied

Only DF has an explicit lake generation system (lakes grown at drainage accumulation
points after erosion). Minecraft has underground aquifers (noise-based, not topology-based).
No other game generates lakes as distinct water features.

Islands' planned approach (boolean mask: NOT-Land ∩ NOT-DeepWater ∩ NOT-ShallowWater) is
simpler than DF's growth model but produces a different kind of lake — noise-artifact
depressions rather than drainage-derived basins. This is appropriate for the current
pipeline since Phase L does not modify terrain: lakes are pre-existing low spots in the
noise-generated heightmap, not accumulation points from a simulation.

### 3.5 The terrain-first vs rivers-first question is moot for Islands

The technique reports (Pass 2b, 6b) discuss whether rivers should drive terrain or vice
versa. Andrino's "rivers-first paradigm" suggests generating river networks and then
shaping terrain to accommodate them. DF takes an intermediate approach (erosion modifies
terrain to create channels, then rivers follow).

For Islands, the question is settled by Phase L's explicit design decision: **Phase L does
not modify the Height field.** Rivers are detected on existing terrain, not carved into it.
This is terrain-first by construction. Erosion-driven river carving is a potential future
phase but is not on the active roadmap. The standard hydrology pipeline (fill → flow → 
accumulate → threshold) is inherently a terrain-first approach — it reads terrain and
produces drainage analysis without modifying the input.

### 3.6 Pipeline ordering is unambiguous

Phase L's position (after Morphology, before Biome) is validated by the cross-check:
- DF runs rivers after terrain is finalized (post-erosion, pre-biome).
- RimWorld places rivers at world scale before local terrain generation.
- Phase M's moisture field optionally consumes FlowAccumulation, so L must precede M.

No game contradicts the L→M ordering. The only alternative would be to derive rivers
from biome/moisture data (circular) or to place them independently of terrain entirely
(RimWorld's graph approach, which Islands explicitly chose not to follow per Phase L
design decisions DD-2 and DD-3).

---

## 4. Summary Matrix

Compact technique × game × Islands status:

| Technique | MC | DF | NMS | RW | CDDA | Tang | Islands |
|---|---|---|---|---|---|---|---|
| Depression filling | ✗ | ~ | — | ✗ | — | — | PLAN (L) |
| D8 flow direction | ✗ | ~ | — | ✗ | — | — | PLAN (L) |
| Flow accumulation | ✗ | ✗ | — | ✗ | — | — | PLAN (L) |
| River extraction (threshold) | ✗ | ✗ | — | ✗ | — | — | PLAN (L) |
| Lake detection | ~ | ✓ | ✗ | ✗ | ✗ | — | PLAN (L) |
| Agent-based carving | ✗ | ✓ | ✗ | ✗ | ✗ | — | NONE |
| River pathfinding | ✗ | ✗ | ✗ | ✓ | ~ | — | NONE |
| Strahler ordering | ✗ | ✗ | ✗ | ✗ | ✗ | — | NONE (L2) |
| Erosion simulation | ✗ | ✓ | ✗ | ✗ | ✗ | — | NONE |

**Key counts:**
- DF implements 3/9 techniques (erosion carving, lake detection, approximate depression
  resolution via erosion agents).
- RW implements 1/9 (river pathfinding — its own approach, not from technique reports).
- MC implements 0/9 standard hydrology techniques (it uses noise-based biome assignment
  for rivers, which is a completely different paradigm).
- NMS, CDDA, and Tangledeep implement 0/9 (no applicable water systems).
- Islands plans 5/9 standard hydrology techniques, all for Phase L.

---

## 5. Priority Analysis

### Tier 1 — High impact, directly required for Phase L

All five planned techniques are Tier 1. They form a single pipeline where each step
depends on the previous:

1. **Priority-Flood depression filling** — Prerequisite for meaningful D8 flow. Without
   it, interior depressions trap flow and produce truncated rivers.
2. **D8 flow direction** — Prerequisite for flow accumulation. Determines which neighbor
   each cell drains toward.
3. **Flow accumulation** — Primary simulation output. Dual-purpose: identifies rivers
   (by thresholding) and feeds Phase M moisture (as continuous scalar field).
4. **River mask extraction** — Gameplay/rendering output. Derived from flow accumulation
   via a single tunable threshold.
5. **Lake detection** — Gameplay-distinct freshwater bodies. Simple boolean mask operation
   on existing layer data.

These five techniques are an indivisible pipeline: implementing any subset without the
others produces no useful output. Phase L's design correctly treats them as a single
stage with sequential sub-steps.

### Tier 2 — Medium impact, extends richness

- **Strahler ordering.** Not used by any game in the corpus. Would provide discrete river
  hierarchy (stream order 1, 2, 3...) for gameplay or rendering differentiation.
  FlowAccumulation serves as a continuous proxy. Deferred to Phase L2 if discrete
  hierarchy is needed. Low urgency.

### Excluded — Not applicable to Islands' current pipeline

- **Agent-based river carving (DF pattern).** Requires modifying the Height field, which
  Phase L explicitly does not do. DF's approach is tightly coupled to its erosion system
  and cannot be extracted independently. If erosion is ever added to Islands, it would
  be a separate pre-L phase. The standard hydrology pipeline achieves the same goal
  (drainage-following rivers) without terrain modification.

- **Pathfinding-based river placement (RW pattern).** Designed for world-scale graph
  structures where rivers are connections between discrete tiles. Islands' single-island
  2D grid with continuous elevation data is better served by the standard hydrology
  pipeline, which produces physically grounded drainage rather than pathfound routes.
  RimWorld's approach solves a different problem (world-graph connectivity) than
  Islands needs (within-island drainage).

- **Erosion simulation.** Only DF implements it. High complexity, not on active roadmap.
  Phase L produces meaningful rivers without it — erosion would improve geological
  realism but is not a prerequisite for functional water features. Already characterized
  in gap research and tracked in the technique integration matrix as a NONE/future item.

---

## 6. Recommended Updates to technique_integration_matrix.md §5

### Rows to update (existing):

1. **Priority-Flood depression filling** — Add cross-check annotation: "No game uses
   formal Priority-Flood. DF achieves depression-free drainage via erosion agent terrain
   modification (different mechanism, same functional goal). 0/6 games use the GIS/academic
   standard. Technique is well-validated in hydrology literature (Barnes 2014)."

2. **D8 flow directions** — Add annotation: "DF's erosion agents use equivalent per-cell
   steepest-descent lookup, but as an agent-local operation, not a grid-global computation.
   0/6 games compute a standalone flow-direction grid."

3. **Flow accumulation** — Add annotation: "0/6 games compute flow accumulation as an
   explicit scalar field. This is Islands' primary novel contribution in the hydrology
   family — no game precedent, but strong technique-report and GIS support. Dual-purpose:
   river identification + moisture enrichment for Phase M."

4. **River mask extraction** — Add annotation: "0/6 games derive rivers by thresholding
   flow accumulation. MC uses noise-biome classification, DF uses erosion-channel tracing,
   RW uses graph pathfinding. Islands' threshold approach auto-scales with resolution via
   fractional threshold (Phase L design §5.1.4)."

5. **Lake detection** — Add annotation: "Only DF generates lakes from drainage simulation
   (growth at accumulation points). MC has noise-based underground aquifers. Islands uses
   a simpler boolean mask approach (noise-derived depressions, not drainage-derived basins)."

### Rows to add:

6. **Agent-based river carving** — Status: NONE. Priority: Excluded (for Phase L).
   Consuming phase: Future (pre-L erosion phase if ever added). Note: "DF-only technique.
   Tightly coupled to erosion simulation. Achieves drainage by terrain modification rather
   than terrain analysis. Not compatible with Phase L's no-terrain-modification invariant."

7. **Pathfinding-based river placement** — Status: NONE. Priority: Excluded (for Islands
   single-island pipeline). Consuming phase: Phase W (potential, for world-tile river
   connections). Note: "RimWorld's approach: rivers as world-graph edges placed by
   pathfinding. Designed for world-tile-scale connectivity. Not applicable to within-island
   drainage but potentially relevant for Phase W world-to-local river inheritance."

8. **Strahler stream ordering** — Status: NONE. Priority: Tier 2 (deferred). Consuming
   phase: L2 (potential extension). Note: "Not used by any game in corpus. Technique
   reports recommend it (mapgen4). FlowAccumulation serves as continuous proxy.
   Discrete ordering useful if gameplay needs river hierarchy classes."

### Expected §5 after update: 8 rows (was 5)

---

## 7. Identified Gaps

### 7.1 Gaps where game reports are thin

| Gap | Severity | Affected Technique | Status |
|---|---|---|---|
| MC aquifer algorithm detail | Low | Lake detection | The aquifer barrier and flood level noise fields are documented, but how they interact to form coherent underground water bodies (vs scattered wet blocks) is unclear. **Not worth deeper research** — MC's 3D aquifer system is architecturally incompatible with Islands' 2D grid. |
| DF river classification criteria | Medium | River extraction | How DF classifies rivers as brooks vs streams vs major rivers after erosion is not documented. The criteria (path length? accumulated erosion depth? connectivity?) are unknown. **Potentially worth a gap prompt** if Islands ever needs discrete river hierarchy beyond FlowAccumulation threshold. |
| DF lake growth algorithm | Medium | Lake detection | The exact algorithm for growing lakes at drainage accumulation points is undocumented. Is it flood fill up to a water level? Is the water level derived from the depression depth? **Potentially worth a gap prompt** if Islands adds drainage-derived lakes (vs the current noise-depression approach). |
| RW local-map river realization | Low | River placement | How RimWorld draws the actual river tiles on a local map (given world-tile entry/exit metadata) is partially documented but the exact algorithm for river width, bank placement, and terrain accommodation is not detailed. **Not worth deeper research** — Islands uses drainage simulation, not inherited world-graph rivers. |
| CDDA river placement algorithm | Low | River placement | Whether CDDA rivers are placed by pathfinding, random walk, or another mechanism is undocumented. **Not worth deeper research** — CDDA's categorical overmap system is architecturally irrelevant to Islands. |

### 7.2 Gaps in technique reports

No significant gaps identified. The technique reports (2a, 2b, 6b) cover the standard
hydrology pipeline thoroughly: Priority-Flood with ε (Barnes 2014), D8 with diagonal
correction, flow accumulation by descending-height traversal, threshold extraction with
resolution-relative scaling, and Strahler ordering. Phase L's design spec incorporates
all of these with island-specific adaptations (coastal seeding for Priority-Flood, no-RNG
invariant, fractional threshold). The technique coverage is sufficient for implementation.

---

## 8. Phase L Design Implications

Concrete findings from the cross-check that should inform Phase L implementation decisions.

### 8.1 Islands' approach is principled but unprecedented in games

Phase L's standard hydrology pipeline (Priority-Flood → D8 → flow accumulation →
threshold) has no direct game precedent in the six-game corpus. Every game either uses
a different paradigm entirely (MC noise, RW pathfinding) or achieves drainage through
terrain modification (DF erosion).

**Implication:** There are no game implementations to reference for edge cases, failure
modes, or parameter tuning. The technique reports and GIS literature are the primary
references. Phase L's design spec already addresses the key concerns (ε value, threshold
scaling, tie-breaking for determinism), but visual smoke testing will be especially
important since there are no game-validated defaults to calibrate against.

### 8.2 Lake detection is the simplest approach in the corpus

Islands' planned lake detection (boolean mask: NOT-Land ∩ NOT-DeepWater ∩
NOT-ShallowWater) is simpler than both DF's drainage-derived lake growth and MC's
noise-based aquifers. It detects pre-existing noise-generated depressions rather than
creating new water features.

**Implication:** Lake occurrence is entirely dependent on the noise-generated Height field
and the waterThreshold. With default settings (noiseAmplitude 0.18), inland depressions
below water threshold may be rare. Higher noiseAmplitude (0.30–0.40) or a reshaped height
distribution (via spline remapping) will produce more lakes. This is emergent and
appropriate — but it means lake presence is a tunables-dependent property, not a
guaranteed feature. The Phase L design spec already notes this (§5.2: "Lake occurrence
depends on terrain").

### 8.3 DF's river finalization process suggests potential L2 features

DF's post-erosion river finalization includes three features that Islands' Phase L omits:

1. **Loop erasure:** DF removes river cycles to ensure tree-structured drainage. Phase L's
   D8 flow produces acyclic drainage by construction (each cell has exactly one downstream
   neighbor), so loop erasure is unnecessary.

2. **Forced ocean connectivity:** DF forces all rivers to reach the ocean. Phase L's
   Priority-Flood guarantees monotonic drainage to the coast, so forced connectivity is
   unnecessary — rivers reach the coast by construction.

3. **Brook generation:** DF classifies minor waterways as brooks. Phase L could achieve an
   analogous effect by applying a secondary (lower) threshold to FlowAccumulation, producing
   "streams" alongside "rivers." This is a natural L2 extension — a second threshold producing
   a `Streams` mask — but is not required for Phase L MVP.

**Implication:** Phase L's design already handles the first two concerns by construction.
Brook/stream classification is a natural extension if gameplay needs it.

### 8.4 Resolution-relative thresholding is unique to Islands

No game in the corpus uses resolution-relative river thresholding because no game derives
rivers from flow accumulation. Phase L's `riverThresholdFraction` (fraction of total Land
cells) is a novel approach that auto-scales with grid resolution. The technique reports
note the scaling problem (Pass 6b: "the threshold must scale with resolution") but do not
prescribe the fractional-of-Land-cells solution that Phase L uses.

**Implication:** The 2% default is an educated estimate, not a game-validated value. Visual
smoke testing across resolution variants (64×64, 128×128, 256×256) should verify that the
fraction produces consistent river density at different scales.

---

## 9. Interaction with Phase M

### 9.1 FlowAccumulation → Moisture enrichment

Phase M's moisture formula (§5.2) includes an optional river factor:

```
riverFactor = riverMoistureBonus * clamp(flowAccum[x,y] / riverFlowNorm, 0, 1)
```

This is the only L→M data dependency. The contract is clean:
- Phase L writes `FlowAccumulation` as raw upstream cell counts (≥1f on Land, 0f elsewhere).
- Phase M detects the field via `HasField` and normalizes using its own `riverFlowNorm` tunable.
- If Phase L is not implemented, Phase M falls back to CoastDist + noise moisture only.

**Cross-check finding:** No game in the corpus uses drainage/flow data to derive moisture.
DF computes rainfall via rainshadow simulation (wind-direction moisture sweep) independently
of river location. RimWorld generates rainfall from noise independently of rivers. MC uses
independent humidity noise. Islands' use of FlowAccumulation for moisture enrichment is a
novel integration with no game precedent — but it is ecologically grounded (rivers correlate
with moisture in the real world) and provides a meaningful signal that noise alone cannot.

### 9.2 No circular dependencies

Phase L reads only from F2 (Height, Land, DeepWater) and F4 (ShallowWater). Phase M reads
from Phase L (FlowAccumulation, optional), Phase G (CoastDist), and F2 (Height). No
circular path exists. The pipeline ordering (L before M) is unambiguous.

### 9.3 Rainshadow is Phase M's concern, not Phase L's

The rainshadow approximation (wind-sorted moisture sweep) modulates the moisture field
directly. It reads Height (for orographic lifting) and a wind direction parameter, and
produces modified moisture values. It does not interact with FlowAccumulation or river/lake
data. Phase L and rainshadow are parallel inputs to Phase M's moisture formula, not
sequential dependencies.

**Cross-check confirmation:** DF computes rainshadow after terrain is finalized but
independently of river placement. The rainfall field and river network are separate outputs
that both feed into biome determination but do not depend on each other.

### 9.4 River masks as Phase M constraints

Phase M does not currently consume River or Lake masks directly — only FlowAccumulation.
However, a potential future refinement could use river/lake proximity to modulate moisture
(cells near rivers are wetter than cells far from rivers, beyond what FlowAccumulation
captures). This is not in scope for Phase M's current design but is a natural extension.

---

## 10. Appendix: Source Authority Notes

**PCG technique reports:**
- `pcg_synthesis.md` §PASS 2b — River/drainage systems: Priority-Flood, D8, flow
  accumulation, threshold extraction, mapgen4 alternative, end-to-end pipeline
- `pcg_technique_library.md` §F.3 — Generating Rivers and Drainage: technique comparison,
  drainage-basin-first insight, typical pipeline
- `pcg_technique_library.md` §G.2 — Heightmap + Erosion + Rivers: composition pattern
- `pcg_technique_library.md` §D.11 — Hydraulic Erosion: particle-based algorithm detail

**Game worldgen reports (hydrology-relevant content):**
- Minecraft: River biome via PV noise valleys; aquifer system (noise-based underground water)
- Dwarf Fortress: Agent-based erosion → river extraction → loop erasure → lake growth →
  world rejection. Most complete water system in corpus.
- No Man's Sky: Ocean as density threshold. No rivers, no lakes.
- RimWorld: World-graph rivers via pathfinding; local-map river realization from tile metadata.
- Cataclysm:DDA: Rivers as categorical overmap terrain connections.
- Tangledeep: No water system.

**Gap research:**
- `DwarfFortress_Worldgen_gap_erosion.md` — DF erosion algorithm detail. Confirms agent-based
  carving, no flow accumulation, post-erosion river placement with loop erasure and lake growth.

**Phase design docs:**
- `Phase_L_Design.md` — Full hydrology stage specification.
- `Phase_M_Design.md` §5.2 — Moisture formula with FlowAccumulation integration.

**Confidence assessment:**
- DF hydrology evidence is strong (gap research + Tarn Adams interviews + community testing).
  DF river classification criteria and lake growth algorithm details remain undocumented.
- MC evidence is strong for surface water (Wiki + Kniberg presentations). Aquifer system
  detail is moderate.
- RW evidence is moderate for world-scale rivers. Local-map river realization is partially
  documented.
- NMS, CDDA, and Tangledeep evidence is sufficient to confirm absence/non-applicability of
  standard hydrology techniques.
