# Phase Q — Biome-Conditional Tile Selection: Design Specification

Status: Planning (not implementation authority)  
Authority: Design reference for Phase Q implementation. Supplements the Phase Q entry
in `PCG_Roadmap.md` with detailed architectural design.  
Depends on: Phase M (done — produces `MapFieldId.Biome` per cell), Phase H3
(`TilesetConfig` / `TilemapAdapter2D`), Phase H5 (multi-layer stamp), Phase H6 (rule tiles).
No unresolved upstream dependencies.

---

## 1. Phase Intent

Phase Q closes the documented but unimplemented gap between Phase M (produces
`MapFieldId.Biome` per cell) and the tilemap adapter (currently ignores it).
`Phase_M_Design.md` §9 and `Phase_M2_Design.md` §7 both list `TilesetConfig` as a
`Biome` consumer; `Resolved design decisions` Decision 1 references `BiomeDef[]`
carrying a "tile palette" property. Today `TilesetConfig.LayerEntry` and
`TilemapAdapter2D` contain zero biome references — the field is produced and ignored.

**What this enables:** Per-cell tile selection that varies by biome. Snow/ice ground
tiles in `Snow` and `Tundra` cells. Sand/rock ground in `SubtropicalDesert` and
`TemperateDesert`. Grass/lush ground in `TemperateForest`, `Grassland`,
`TropicalRainforest`. Snowy mountain peaks in cold-biome `HillsL2` cells.
Vegetation sprite variety: pines in boreal, palms in tropical, cacti in desert,
broadleaf in temperate — all driven from the existing single `MapLayerId.Vegetation`
mask, no layer split.

**Scope boundary:** Pure adapter-side. No new `MapLayerId`. No new `MapFieldId`. No
pipeline stage. No `MapPipelineRunner2D` modification. No golden break. No determinism
gate. The adapter reads the existing `Biome` field and routes layer → tile resolution
through a biome-aware override ScriptableObject.

---

## 2. Resolved Design Decisions

### 2.1 Override Mechanism (Resolved — Q-DD-1)

**Decision: Separate `BiomeTileOverride` ScriptableObject, additive over base `TilesetConfig`.**

Three candidates were evaluated:

| Option | Description | Pros | Cons |
|--------|------------|------|------|
| A — Extend `LayerEntry` | Add per-biome tile arrays directly into `TilesetConfig.LayerEntry` | Single asset; no new SO type | Bloats struct (13 biomes × 15 layers); cluttered Inspector; migration burden for existing assets; layers that don't vary by biome carry dead weight |
| B — `BiomeTileOverride` SO | Separate SO listing biome-layer overrides; assigned alongside `TilesetConfig` | Zero modification to `TilesetConfig`; clean Inspector (override only lists biome-layer pairs that differ); backward-compatible (null override = pre-Q behavior); swappable per tileset | Additional SO to manage; two-asset coordination |
| C — Multi-`TilesetConfig` selector | N separate `TilesetConfig` assets per biome, selected by a `BiomeTilesetSelector` | Full per-biome customization | Massive redundancy (water layers identical across biomes); N × 15 entries for N biomes |

**Selected: Option B.** Rationale:

- Existing `TilesetConfig` stays untouched — zero migration for current assets.
- The override is an additive layer; when no override is assigned, the system is
  identical to pre-Q behavior.
- Artists author only the biome-layer pairs that actually differ (typically 20–40
  entries total: ~6 biome-sensitive layers × ~5 biome groups).
- The same override can be shared across different `TilesetConfig` bases if art
  is reusable, or different overrides can be swapped for the same base.
- Mirrors the additive override pattern already validated by Phase H8's
  `MegaTileRule` (separate config object layered onto the base stamp pass).

### 2.2 Override Data Layout (Resolved — Q-DD-2)

**Decision: Group-by-biome with nested layer slots.**

```csharp
[CreateAssetMenu(
    fileName = "BiomeTileOverride",
    menuName = "Islands/PCG/Biome Tile Override",
    order = 102)]
public sealed class BiomeTileOverride : ScriptableObject
{
    [Serializable]
    public struct LayerSlot
    {
        [Tooltip("Which pipeline layer this override applies to.")]
        public MapLayerId layerId;

        [Tooltip("Static tile. Ignored when Animated or Rule Tile assigned.")]
        public TileBase tile;

        [Tooltip("Animated tile. Ignored when Rule Tile assigned.")]
        public TileBase animatedTile;

        [Tooltip("Rule Tile (highest priority).")]
        public TileBase ruleTile;
    }

    [Serializable]
    public struct BiomeGroup
    {
        [Tooltip("Biome this group applies to.")]
        public BiomeType biome;

        [Tooltip("Layer tile overrides for this biome. Only layers listed here are" +
                 " overridden; unlisted layers fall through to the base TilesetConfig.")]
        public LayerSlot[] layers;
    }

    [Tooltip("Per-biome tile override groups. Each group specifies which layers get" +
             " biome-specific tiles. Order within the array doesn't matter —" +
             " duplicate biome entries are resolved last-wins.")]
    public BiomeGroup[] groups;
}
```

Inspector UX: expand a biome group → see which layers have overridden tiles. Compact
when most biomes only override 3–5 layers. Consistent with the `LayerEntry` tile-slot
pattern (ruleTile > animatedTile > tile).

### 2.3 Runtime Lookup Structure (Resolved — Q-DD-3)

**Decision: Flat lookup array `TileBase[BiomeType.COUNT × MapLayerId.COUNT]`, built
lazily on first access per SO instance.**

The override SO exposes a public `Resolve(BiomeType biome, MapLayerId layerId)`
method returning the resolved `TileBase` (or null for "no override, fall through to
base"). Internally this indexes into a flat array of size `13 × 15 = 195`. Construction
walks the `groups` array once, resolving each `LayerSlot`'s tile priority
(ruleTile > animatedTile > tile), and stores the winner at
`[biome * MapLayerId.COUNT + layerId]`.

Null slot means "no override for this (biome, layer) pair — use base tile."

The lookup is rebuilt whenever the SO is dirtied in the editor (via `OnValidate`)
and on first access at runtime. This keeps per-cell resolution to a single array
index operation — no dictionary, no allocation per frame.

```csharp
// Hot path — called once per cell per matching layer
public TileBase Resolve(BiomeType biome, MapLayerId layerId)
{
    if (_lookup == null) RebuildLookup();
    int idx = (int)biome * _layerCount + (int)layerId;
    return _lookup[idx]; // null = no override
}
```

### 2.4 Adapter Integration Point (Resolved — Q-DD-4)

**Decision: New `TilemapAdapter2D.ApplyBiomeAware()` overload. Existing `Apply()`
unchanged.**

The biome override cannot be pre-resolved at the `TilemapLayerEntry[]` level because
the tile varies per cell (different cells have different biomes). Resolution must happen
inside the per-cell stamp loop. A new overload keeps the existing `Apply()` API stable:

```csharp
public static void ApplyBiomeAware(
    MapDataExport export,
    Tilemap tilemap,
    TilemapLayerEntry[] priorityTable,
    BiomeTileOverride biomeOverride,   // nullable — null = identical to Apply()
    TileBase fallbackTile = null,
    bool clearFirst = true,
    bool flipY = false)
```

Internally:

1. Pre-fetch `biomeField = export.HasField(MapFieldId.Biome) ? export.GetField(MapFieldId.Biome) : null`.
2. Per-cell, per-layer-match: `int biomeId = biomeField != null ? (int)biomeField[idx] : 0`.
3. `TileBase overrideTile = biomeOverride?.Resolve((BiomeType)biomeId, entry.LayerId)`.
4. `winner = overrideTile ?? baseTile` (base tile from the entry's `Tile` field).

When `biomeOverride` is null OR `biomeField` is absent, the method produces output
identical to the existing `Apply()`. This guarantees backward compatibility.

A matching `ApplyLayeredBiomeAware()` passes the override through to each group:

```csharp
public static void ApplyLayeredBiomeAware(
    MapDataExport export,
    TilemapLayerGroup[] groups,
    BiomeTileOverride biomeOverride)
```

### 2.5 Per-Layer Scope (Resolved — Q-DD-5)

**Decision: No scope restriction in the data structure. Document recommended layers.**

The `BiomeTileOverride` SO accepts any `MapLayerId`. The system imposes no compile-time
restriction on which layers can have biome overrides. In practice the following layers
are the recommended biome-variant targets:

| Layer | Biome variation | Example |
|-------|----------------|---------|
| `Land` | Ground surface texture | Snow → white, Desert → sand, Forest → grass |
| `LandInterior` | Interior ground variant | Same as Land or omitted |
| `LandCore` | Deep interior | Same or richer variant |
| `Vegetation` | Biome-specific flora | Boreal → pines, Tropical → palms, Desert → cacti |
| `HillsL1` | Hill slope surface | Snow → icy rock, Desert → dry rock, Forest → mossy rock |
| `HillsL2` | Mountain peak | Snow → snowcap, Desert → sandstone peak |
| `LandEdge` | Coastal fringe | Beach biome → sand strip (already handled by biome override of Beach type) |

Water layers (`DeepWater`, `MidWater`, `ShallowWater`, `Lakes`, `Rivers`), `Walkable`,
`Paths`, `Stairs`, and `LandEdge` typically don't vary by biome and should be left
without override entries.

### 2.6 Fallback Behavior (Resolved — Q-DD-6)

**Decision: Three-level fallback — override tile → base tile → fallback tile.**

Per-cell tile resolution for a given matching layer:

1. **Biome override hit:** `biomeOverride.Resolve(biome, layerId)` returns non-null →
   use override tile.
2. **No override (null):** use the base `TilemapLayerEntry.Tile` from `TilesetConfig`.
3. **Base tile also null:** use the `fallbackTile` parameter (from `TilesetConfig.fallbackTile`).
4. **Fallback also null:** cell is left empty (consistent with existing `Apply()` behavior).

No magenta sentinel. Missing biome entries are silent fallthrough, not errors — this
allows incremental biome art authoring (start with 2–3 biomes, fill in later).

### 2.7 Procedural Tile Path (Resolved — Q-DD-7)

**Decision: Phase Q applies to the `TilesetConfig` (art tile) path only. Procedural
biome tiles deferred to a potential Q.a extension.**

When `useProceduralTiles = true`, tiles come from `ProceduralTileFactory` which
creates solid-color tiles per layer. Biome-aware procedural tiles would need per-biome
color mappings — functionally equivalent to Phase V.b's `BiomeColorPalette`. This is a
natural extension but not required for Phase Q's primary value (art tile variation).

Phase Q's override is skipped when procedural tiles are active. The
`PCGMapTilemapVisualization` only passes the `BiomeTileOverride` to the adapter when
`useProceduralTiles = false && tilesetConfig != null && biomeTileOverride != null`.

### 2.8 Biome Transition Softening (Resolved — Q-DD-8)

**Decision: Accepted as a known visual artifact. Out of scope for Phase Q. Blending
is a separate later refinement.**

The `Biome` field uses hard integer boundaries (§5.3.2 of `Phase_M_Design.md`). Phase Q
faithfully reproduces those hard boundaries in tile art — a Snow cell adjacent to a
TemperateForest cell will show an abrupt snow-to-grass transition. This is the correct
behavior given the current biome data.

Biome transition blending is listed as "Tier 1 — NONE" in `technique_integration_matrix.md`
and is a separate, independent future refinement. Phase Q ships acceptably without it.

---

## 3. File Placement

All new files live in the existing `Adapters.Tilemap` assembly
(`Runtime/PCG/Adapters/Tilemap/`) unless otherwise noted. No new assembly definition.

| File | Location | Purpose |
|------|----------|---------|
| `BiomeTileOverride.cs` | `Runtime/PCG/Adapters/Tilemap/` | SO + `Resolve()` lookup |
| `TilemapAdapter2D.cs` | (existing, modified) | New `ApplyBiomeAware()` + `ApplyLayeredBiomeAware()` overloads |
| `PCGMapTilemapVisualization.cs` | (existing, modified) | New `biomeTileOverride` Inspector slot; `StampMultiLayer` passes override to adapter |
| `PCGMapTilemapVisualizationEditor.cs` | (existing, modified) | Conditional display of `biomeTileOverride` field (hidden when procedural tiles active) |
| `BiomeTileOverrideTests.cs` | `Tests/EditMode/PCG/Adapters/Tilemap/` | Unit tests for lookup correctness |

No new assembly definition. `BiomeTileOverride.cs` needs `Islands.PCG.Layout.Maps`
(for `BiomeType`, `MapLayerId`) which is already referenced by `Adapters.Tilemap.asmdef`.

### 3.1 Starter Asset

> **Corrected 2026-08-21** (open decision `Phase_Q_Pending_Doc_Updates.md` §9.1 resolved by
> the user). Two errors were fixed together, as the blocking note said they would be: the
> path `Samples~/0.1.0-preview/PCG Map Tilemap/` does not exist — verified against the
> package directory listing, which shows `Samples~/0.1.0-preview/` containing Fractal, Hash,
> Noise, Procedural Meshes and ProceduralSurface only — and the "5 temperature clusters"
> description did not match the implemented `Populate Default Biome Groups`.

`Overworld16bit_BiomeOverride.asset` under
`Runtime/PCG/Samples/PCG Map Tilemap/Tilesets/`, following the naming convention
`<SetName>_BiomeOverride` specified in `reference/tileset-import-guide.md` §Phase 7. One
override asset per tileset; the set name in the filename is what keeps them apart once more
than one exists.

Pre-populated by the context menu below with one `BiomeGroup` per `BiomeType` except
`Unclassified` — **12 groups**, each carrying 4 recommended layer slots (`Land`,
`Vegetation`, `HillsL1`, `HillsL2`). Tile references are left null; artists fill in
per-biome tile art, or generate placeholders with the Q-aux.a context menu.

Context menu on `BiomeTileOverride`: **"Populate Default Biome Groups"** — creates
one `BiomeGroup` per `BiomeType` (excluding `Unclassified`) with recommended layer
slots (`Land`, `Vegetation`, `HillsL1`, `HillsL2`) pre-listed but tile refs null.

---

## 4. Contracts and Invariants

### Q-1: Backward compatibility
When `biomeTileOverride` is null or `useProceduralTiles` is true, tilemap output is
bit-identical to pre-Q behavior. No existing tests, goldens, or visual outputs change.

### Q-2: Adapters-last invariant
`BiomeTileOverride` is a read-only consumer of `MapDataExport`. It never writes to
pipeline state. The `Biome` field is read via `export.GetField(MapFieldId.Biome)` —
the same API used by `PCGHoverTooltip` and `PCGRuntimeOverlay`.

### Q-3: Tile resolution priority
Per-cell, per-matching-layer: biome-override tile (ruleTile > animatedTile > tile) →
base `TilesetConfig` tile (existing H6 priority) → `fallbackTile` → null.
Override tiles follow the same ruleTile > animatedTile > tile priority as base entries.

### Q-4: Absent biome field graceful degradation
When `MapFieldId.Biome` is not in the export (biome stage disabled), all cells read as
`BiomeType.Unclassified` (0). If the override has no entry for `Unclassified`, every
cell falls through to the base tile. No error, no warning — identical to pre-Q output.

### Q-5: Override lookup is O(1) per cell
The flat-array lookup ensures no per-cell allocation or dictionary probing. The array
is rebuilt only on SO validation or first access, not per-frame.

### Q-6: Multi-layer mode compatibility
**Amended by Q-fix.a (2026-08-09).** Collider groups are excluded from the biome-aware
path *by construction*, not by convention. `StampMultiLayerBiomeAware` stamps the base and
overlay groups via `ApplyLayeredBiomeAware` and the collider group via plain
`ApplyLayered`. Caller responsibility is documented on the `ApplyLayeredBiomeAware` XML
doc comment.

The prior wording — that the system permits biome overrides on collider layers and merely
relies on null entries falling through — was the direct cause of Q-BUG-2: an override on
`HillsL2` or `Lakes` (both in `s_colliderLayers`) replaced the collider sentinel tile with
biome art, silently changing collision. Permissiveness here is not a neutral default.

---

## 5. Integration with `PCGMapTilemapVisualization`

### 5.1 New Inspector Field

```csharp
[Header("Biome Tile Override (Phase Q)")]
[Tooltip("Optional: assign a BiomeTileOverride asset to vary tile art by biome.\n" +
         "When assigned and Procedural Tiles is disabled, tiles for biome-layer\n" +
         "pairs found in this override replace the base TilesetConfig tiles.\n" +
         "When null, all tiles come from TilesetConfig only (pre-Q behavior).")]
[SerializeField] private BiomeTileOverride biomeTileOverride;
```

Positioned after the existing `tilesetConfig` field in the Inspector. Hidden by the
custom editor when `useProceduralTiles = true`.

### 5.2 Stamp Path Modification

The existing `Regenerate()` method's stamp section changes from:

```csharp
if (enableMultiLayer)
    StampMultiLayer(export, activeTable, activeFallback);
else
    TilemapAdapter2D.Apply(export, tilemap, activeTable, activeFallback, true, flipY);
```

To:

```csharp
BiomeTileOverride activeOverride =
    (!useProceduralTiles && biomeTileOverride != null) ? biomeTileOverride : null;

if (enableMultiLayer)
    StampMultiLayerBiomeAware(export, activeTable, activeFallback, activeOverride);
else
    TilemapAdapter2D.ApplyBiomeAware(
        export, tilemap, activeTable, activeOverride,
        activeFallback, true, flipY);
```

`StampMultiLayerBiomeAware` mirrors `StampMultiLayer` but calls
`TilemapAdapter2D.ApplyLayeredBiomeAware(export, groups, activeOverride)`.

### 5.3 Dirty Hash Extension

`ComputeTilesetConfigHash()` incorporates the `BiomeTileOverride` asset's
`GetInstanceID()` so that assigning or swapping the override triggers regeneration.
The override's internal content changes are tracked via a secondary hash of the
`groups` array length + each entry's biome ordinal and tile InstanceIDs, comparable
to the existing `MegaTileRule` hash pattern.

---

## 6. Non-Goals

- No new `MapLayerId` or `MapFieldId`.
- No pipeline stage modification or addition.
- No golden break or golden coverage (adapter-side visual concern).
- No determinism gate (tilemap output is display-only).
- No biome transition blending (separate future work).
- No vegetation layer split (single `Vegetation` mask, biome differentiates art only).
- No region-aware tile selection (M2.b's `BiomeRegionId` is not consumed here).
- No procedural biome tiles in this phase (deferred to Q.a if needed).
- No SSoT promotion. This design document carries planning authority only.

**Mega-tile (H8) interaction.** The mega-tile post-pass runs after the stamping pass and
therefore overwrites biome-varied tiles unconditionally. A 2×2 mega-tile crossing a biome
boundary is stamped whole, from the base tileset. This is defined behavior, not a decision
left open — Phase Q does not change mega-tile resolution.

---

## 7. Test Plan

### 7.1 Unit Tests — `BiomeTileOverrideTests.cs`

| # | Test | Description |
|---|------|-------------|
| Q-T-1 | `Resolve_KnownBiomeLayer_ReturnsOverrideTile` | Override with Snow+Land entry → `Resolve(Snow, Land)` returns the assigned tile. |
| Q-T-2 | `Resolve_UnknownBiomeLayer_ReturnsNull` | Override with no Desert+Land entry → `Resolve(SubtropicalDesert, Land)` returns null. |
| Q-T-3 | `Resolve_EmptyOverride_ReturnsNull` | Override with zero groups → `Resolve(any, any)` returns null for all inputs. |
| Q-T-4 | `Resolve_NullOverride_ApplyProducesBaseOutput` | `ApplyBiomeAware(export, tilemap, table, null)` produces output identical to `Apply(export, tilemap, table)`. |
| Q-T-5 | `Resolve_RuleTilePriority_WinsOverStaticAndAnimated` | `LayerSlot` with all three assigned → `Resolve()` returns `ruleTile`. |
| Q-T-6 | `Resolve_AnimatedTilePriority_WinsOverStatic` | `LayerSlot` with `animatedTile` + `tile` → `Resolve()` returns `animatedTile`. |
| Q-T-7 | `Resolve_DuplicateBiomeGroup_LastWins` | Two `BiomeGroup` entries for `Snow` → last group's tiles used. |
| Q-T-8 | `Resolve_UnclassifiedBiome_FallsThrough` | No override for `Unclassified` → base tile used for water cells. |
| Q-T-9 | `ApplyBiomeAware_NoBiomeField_IdenticalToBase` | Export without `MapFieldId.Biome` → biome override has no effect; output matches `Apply()`. |

### 7.2 Integration Verification

No golden tests (adapter-side). Verification is visual via smoke tests (§8).

---

## 8. Smoke Test Protocol

### §12.1 — Baseline Sanity (Override Null)

**Setup:** `PCGMapTilemapVisualization` with `tilesetConfig` assigned, `biomeTileOverride = null`.
**Expect:** Tilemap output identical to pre-Q. No visual change, no console warnings.
**Pass criterion:** Visual match with V.b biome color overlay confirming biome field is present.

### §12.2 — Single-Biome Override

**Setup:** Create `BiomeTileOverride` with one group: `Snow` → `Land` layer → distinct snow tile
(e.g., white procedural tile or a clearly different sprite).
**Expect:** Only cells where V.b's biome overlay shows Snow render the snow tile for Land.
All other biome cells render the base `TilesetConfig` Land tile.
**Red flags:** Snow tile appearing in non-Snow cells. Non-Snow cells rendering incorrectly.
**Verification:** Toggle V.b biome color overlay on/off; snow tile boundaries should match
Snow biome boundaries exactly.

### §12.3 — Multi-Biome, Multi-Layer

**Setup:** Override with 3+ biome groups, each overriding Land + Vegetation + HillsL2.
E.g., Snow (white ground, no veg tile, snowy peak), SubtropicalDesert (sand ground,
cactus veg, sandstone peak), TemperateForest (green ground, broadleaf veg, mossy peak).
**Expect:** Distinct visual regions matching biome distribution. V.b overlay boundaries
align with tile transitions.
**Red flags:** Biome boundaries misaligned with tile transitions. Missing fallthrough on
layers without override (e.g., water should be unchanged).

### §12.4 — Seed Variation

**Expect:** Different seeds produce different biome distributions; override tiles follow.
No seed produces a uniform biome (unless tunables are extreme).

### §12.5 — Multi-Layer Mode

**Setup:** `enableMultiLayer = true` with overlay and collider tilemaps assigned.
**Expect:** Base layers (Land) show biome variation. Overlay layers (Vegetation, HillsL1,
HillsL2) show biome variation. Collider layers unaffected (sentinel tile, no override).
**Red flags:** Override tiles appearing on wrong tilemap. Collider behavior changed.

### §12.6 — Graceful Degradation (Biome Stage Disabled)

**Setup:** `enableBiomeStage = false`, `biomeTileOverride` assigned.
**Expect:** All cells read as `Unclassified`. Override has no effect. Output identical
to pre-Q with biome stage off.

---

## 9. V.a / V.b Cross-Verification

Phase V's inspection tools are the primary debugging surface for Phase Q:

- **V.b biome color overlay:** cell-by-cell biome identity. Tile transitions must align
  exactly with biome overlay boundaries. Any misalignment indicates a coordinate or
  field-read bug.
- **V.a hover tooltip:** hover over a cell → see Biome name + ID. Confirm the tile
  rendered at that cell matches the expected biome override.
- **V.b text overlay (Biome field):** per-cell numeric biome ID. Cross-check against
  override's `BiomeGroup.biome` ordinal.

---

## 10. Documentation Updates Required at Delivery

| Document | Change |
|----------|--------|
| `CURRENT_STATE.md` | Add Phase Q resolution block. Update implemented baseline. |
| `PCG_Roadmap.md` | Phase Q status → done. Add delivery notes. |
| `map-pipeline-by-layers-ssot.md` | No change (adapter-side only; no pipeline contract modified). |
| `SSoT_CONTRACTS.md` | No change (no new contract). |
| `coverage-matrix.md` | Add `BiomeTileOverride` SO as adapter-side implementation truth. |

---

## 10b. Batch Q-fix.a — Defects Found on Review (2026-08-09)

Phase Q was found already implemented but registered in no authority surface. Reviewing
the found code surfaced four defects, all adapter-side, all fixed in this batch.

| Id | Defect | Fix |
|---|---|---|
| Q-BUG-1 | Override was ignored when the base layer entry had a null tile — layer masks were only cached when a base tile existed | Cache layer masks unconditionally; the guard `if (resolved != null)` in the per-cell scan preserves `Apply()` parity |
| Q-BUG-2 | The collider group was routed through the biome-aware path, so an override on `HillsL2` or `Lakes` replaced the collider sentinel tile with biome art | Collider group stamped separately via `ApplyLayered`; never biome-aware. See the Q-6 amendment in §4 |
| Q-BUG-3 | Biome field read with `(int)` truncation while `PCGHoverTooltip` (V.a) and `PCGRuntimeOverlay` (V.b) use `Mathf.RoundToInt` | Adapter now uses `Mathf.RoundToInt`, so tooltip, overlay and tile all agree on the same cell |
| Q-BUG-4 | The Phase Q hash block sat after the `tilesetConfig == null` early return, so override edits could miss dirty tracking | Hash block moved above the early return |

**Test coverage.** `BiomeTileOverrideTests` grew 13 → 16 tests with three regression gates
(Q-BUG-1, Q-BUG-3, Q-BUG-4). Q-BUG-2 has no unit coverage — see §12.

**Utility Q-aux.a.** `BiomeTileOverridePlaceholderGenerator` (`Editor/Inspectors/`,
editor-only) generates flat-color placeholder `Tile` assets per (biome, layer) slot so a
biome-conditional setup can be smoke-tested before any real art exists. Colors mirror the
`BiomeColorPalette` defaults (V.b), which makes "painted tile color == overlay color" a
direct cross-check. `HillsL2` placeholders carry a 4px white border by design: a
white-bordered tile appearing on the collider tilemap is the visual signature of a
Q-BUG-2 regression. Zero runtime code.

---

## 11. Dependencies and Phase Interactions

| Phase | Relationship |
|-------|-------------|
| **M** (done) | Hard dependency — produces `MapFieldId.Biome`. |
| **M2.a** (done) | Soft — biome-aware vegetation density is already in the pipeline; Q makes it visible in art. |
| **H3** (done) | `TilesetConfig` SO pattern. |
| **H6** (done) | Rule tile priority (ruleTile > animatedTile > tile). Q reuses this. |
| **H8** (done) | `MegaTileRule` SO-pattern architectural precedent. |
| **L** (done) | No interaction. Rivers/Lakes tiles are not biome-sensitive by default. |
| **V** (done) | Inspection tools are Q's primary debugging surface. |
| **Q2** (planning) | Sibling phase — independent; share adapter-track flavor. Q2 reads layer composition; Q reads biome field. Do not depend on each other. |
| **T1** (planning) | Independent adapter. T1 reads `MapDataExport` for mesh; Q reads it for tiles. |
| **Biome blending** (unbuilt) | Future refinement. When blending ships, Q's hard tile transitions become soft. Q does not block or require blending. |

---

## 12. Open Items

All three mechanism choices from the `PCG_Roadmap.md` Phase Q entry are resolved:
1. **Mechanism:** `BiomeTileOverride` SO (Q-DD-1).
2. **Per-layer scope:** Any layer; recommended set documented (Q-DD-5).
3. **Fallback:** Three-level fallthrough, no magenta sentinel (Q-DD-6).

The following remain open after Q-fix.a:

- **Q-BUG-2 has no unit test coverage.** `StampMultiLayerBiomeAware` is private with no
  test seam, so the collider-exclusion guarantee is verified by the smoke protocol only.
  Options: accept smoke-only verification, or introduce a declarative `BiomeAware` flag on
  `TilemapLayerGroup` to make the exclusion testable at adapter level. Deciding requires
  reading `TilemapLayerGroup.cs`, which has not been reviewed. Logged as test debt.
- **Q-aux.a color modulation.** At ×0.60 brightness, `Vegetation` over dark biomes
  (`BorealForest` #2F4F35, `TropicalRainforest` #1F7535) renders near-black and reads as
  mush. Consider blending toward a fixed hue instead of multiplying. Cosmetic, debug-only.
- **Starter asset naming and path** (§3.1). Blocked; see the note in that section.
