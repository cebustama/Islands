# Phase V — Runtime Inspection UI: Design Specification

Status: Planning only (not implementation authority)
Authority: Design reference for Phase V implementation. Supplements the Phase V entry
in `PCG_Roadmap.md` with detailed component contracts, data flow, and test plan.
Depends on: nothing hard. Softly benefits from Phase M (climate fields), Phase L
(`FlowAccumulation`), and Phase M2.b (`BiomeRegionId`) for richer payloads.

---

## 1. Phase Intent

Phase V adds an in-application inspection layer over the PCG pipeline. It answers two
questions developers and designers ask repeatedly during pipeline tuning and bug
triage:

1. **"What is in this cell?"** — answered by V.a (hover tooltip).
2. **"Show me this field/category across the whole map."** — answered by V.b
   (per-cell text overlay and discrete color overlay).

Phase V is a **read-only sample/tooling phase**. It produces no authored data,
participates in no golden test, contributes no determinism gate, and does not
participate in the `MapPipelineRunner2D` stage chain. Existing edit-mode
visualizations (`PCGMapVisualization`, `PCGMapCompositeVisualization`,
`PCGMapTilemapVisualization`) are not replaced — Phase V components run alongside
them and consume their `MapContext2D` via a thin `IMapContextSource` interface.

Phase V is designed to be **implementable independently of every other planned
phase**. Each existing visualization adds a one-line interface implementation;
the Phase V components consume only what is present and degrade gracefully when
fields or layers are absent.

---

## 2. Resolved Design Decisions (this design pass)

These decisions are recorded here and treated as settled for Phase V implementation.
They were surfaced as open questions during the design pass; rationale captured in §15.
None of them was previously recorded at roadmap level — if any deserves promotion to
roadmap-level decision, that promotion happens before implementation begins.

| # | Question | Resolution | Key rationale |
|---|----------|------------|---------------|
| V-DD-1 | `IMapContextSource` shape | Minimal pull surface: `MapContext2D Context`, `Tilemap Tilemap`, `bool FlipY`, `int RegenerationVersion`, `bool TryWorldToCell(Vector3, out int x, out int y)` | Smallest seam; matches read-only tooling intent |
| V-DD-2 | World-to-cell ownership | Source owns `TryWorldToCell` | Localizes flipY logic; prevents per-consumer drift |
| V-DD-3 | V.a field menu | V.a iterates `MapFieldId` directly | Tooltip reflects "what is actually in the cell"; pipeline fields only |
| V-DD-4 | V.b field menu | V.b reuses `ScalarOverlaySource` | Matches existing scalar-overlay UX; gives access to noise previews + `ShapeMask` |
| V-DD-5 | Text overlay rendering | Single `TextMeshPro` component with one rich-text mesh per redraw | Runtime-capable; one draw call; viable up to soft cap |
| V-DD-6 | Editor vs runtime scope | Both V.a and V.b runtime-capable | Roadmap target; both qualify with chosen techniques |
| V-DD-7 | Discrete palette assignment | `BiomeColorPalette` SO for `Biome` field; deterministic hash-color for `BiomeRegionId` | Biome ordinals are named; region IDs are anonymous |
| V-DD-8 | Color overlay placement | New `PCGRuntimeOverlay` MonoBehaviour, owns its own `ScalarOverlayRenderer` instance | Matches roadmap text; isolates from existing scalar overlay slots |
| V-DD-9 | Refresh cadence | Pull-based: consumer polls `RegenerationVersion` in `Update()` and rebuilds on change | No event-subscription bookkeeping; matches Unity Update model |
| V-DD-10 | Hover layer membership | Iterate **all** `MapLayerId`s; ignore `s_baseLayers`/`s_overlayLayers` partition | Tooltip exists to debug routing; cannot depend on routing classification |
| V-DD-11 | Uncreated field handling | Skip the line; do not render `—` | Cleaner; uncreated is the common case (e.g., `Biome` when M off) |
| V-DD-12 | Tooltip placement | Cursor-following screen-space canvas, viewport-clamped, hidden when outside map bounds | Standard tooltip behavior |

**V-DD-5 implementation note (2026-04-26):** TextMeshPro 3D uses `MeshRenderer`,
which URP 2D Renderer does not render (URP 2D uses the sprite sorting system; the
`MeshRenderer` text is visible in Scene view but invisible in Game view).
Implemented as world-space `Canvas` + `TextMeshProUGUI` instead, which participates
in URP 2D sorting via the Canvas batching system. Functionally identical to the
original design intent: single rich-text mesh, one draw call, same vertex budget
math, same `<mspace>` / `<line-height>` tag-driven cell alignment. Change isolated
to `EnsureText` method in `PCGRuntimeOverlay.cs`.

---

## 3. Architectural Placement

### 3.1 Read-only tooling, not a pipeline stage

Phase V introduces no `MapLayerId`, no `MapFieldId`, no `IMapStage2D` implementation,
and no `MapPipelineRunner2D` modification. `MapLayerId.COUNT` remains 15 and
`MapFieldId.COUNT` remains 7 after Phase V. No `SnapshotHash64` change. No golden
file. No determinism test.

### 3.2 Relationship to existing visualization surfaces

`PCGMapTilemapVisualization` is the primary inspection surface and the primary host
for V.a/V.b in development. The two frozen viz components (`PCGMapVisualization`,
`PCGMapCompositeVisualization`) also implement `IMapContextSource` so V.a/V.b work
when those surfaces are active. The one-line interface implementation is the only
modification Phase V makes to existing files.

V.a (hover tooltip) and V.b (per-cell overlay) are independent components — either
can be added or removed without affecting the other.

### 3.3 Authority status

Phase V is **not** promoted to subsystem authority and does **not** appear in
`SSoT_INDEX.md` as a governed surface. This design document carries planning
authority for the phase, equivalent to other phase design docs under
`planning/active/design/`. The roadmap's design-doc table is updated to point at
this file once authored (see §13 documentation updates).

---

## 4. The `IMapContextSource` Contract

### 4.1 Interface surface

```csharp
namespace Islands.PCG.Inspection
{
    /// <summary>
    /// Read-only handle that exposes a live <see cref="MapContext2D"/> together with
    /// the alignment information required to translate world-space cursor positions
    /// into grid cells. Implemented by visualization components so Phase V tools
    /// (hover tooltip, runtime overlay) can attach without coupling to any specific
    /// visualization class.
    ///
    /// Implementations MUST NOT allocate or mutate pipeline state from any member
    /// of this interface. All members are pure observers.
    /// </summary>
    public interface IMapContextSource
    {
        /// <summary>
        /// The live context. Returns null when the source has not yet generated
        /// (e.g., before first <c>Update()</c>) or has been disposed.
        /// </summary>
        MapContext2D Context { get; }

        /// <summary>
        /// The tilemap whose world-space rect corresponds to the context grid.
        /// May be null if the source does not render to a tilemap; consumers
        /// requiring a tilemap (V.a, V.b) MUST handle null by going inactive.
        /// </summary>
        UnityEngine.Tilemaps.Tilemap Tilemap { get; }

        /// <summary>
        /// True when the source flips rows on render. Drives the row inversion
        /// inside <see cref="TryWorldToCell"/>. Phase V consumers do not need
        /// to know flipY directly; they call <see cref="TryWorldToCell"/>.
        /// </summary>
        bool FlipY { get; }

        /// <summary>
        /// Monotonic counter incremented by the source whenever the context has
        /// been regenerated (new seed, new tunables, dirty flag fired). Phase V
        /// consumers poll this in <c>Update()</c> and rebuild their derived state
        /// (overlay textures, label meshes) when it changes. The contract is:
        /// any visible change to <see cref="Context"/> contents is preceded by
        /// an increment of this counter.
        ///
        /// Sources MUST increment by at least 1 per regeneration. A wraparound
        /// of <see cref="System.Int32.MaxValue"/> is acceptable (consumers compare
        /// for inequality, not magnitude).
        /// </summary>
        int RegenerationVersion { get; }

        /// <summary>
        /// Convert a world-space position to integer cell coordinates in the
        /// source's grid. Returns false when <paramref name="world"/> falls
        /// outside the grid bounds, or when the source is not currently capable
        /// of conversion (no tilemap, no context, etc.).
        ///
        /// Cell coordinates are returned in <see cref="MapContext2D.Domain"/>'s
        /// native coordinate space — i.e., the pre-flipY space the pipeline uses.
        /// flipY handling is the source's responsibility.
        /// </summary>
        bool TryWorldToCell(UnityEngine.Vector3 world, out int x, out int y);
    }
}
```

### 4.2 Implementation requirements for existing visualization classes

Each of the three existing viz classes adds one interface declaration and minimal
property/method implementations. No data is duplicated — the interface returns
references to existing private fields.

`PCGMapTilemapVisualization` implementation sketch (illustrative; final field
names confirmed at implementation time):

```csharp
public sealed class PCGMapTilemapVisualization : MonoBehaviour, IMapContextSource
{
    // Existing private fields:
    //   private MapContext2D ctx;
    //   [SerializeField] private Tilemap baseTilemap;
    //   [SerializeField] private bool flipY = true;
    private int _regenVersion;

    public MapContext2D Context => ctx;
    public Tilemap Tilemap => baseTilemap;
    public bool FlipY => flipY;
    public int RegenerationVersion => _regenVersion;

    public bool TryWorldToCell(Vector3 world, out int x, out int y)
    {
        x = y = 0;
        if (ctx == null || baseTilemap == null) return false;
        var local = baseTilemap.WorldToCell(world);
        int gx = local.x, gy = local.y;
        if (gx < 0 || gy < 0 || gx >= ctx.Domain.Width || gy >= ctx.Domain.Height)
            return false;
        x = gx;
        y = flipY ? (ctx.Domain.Height - 1 - gy) : gy;
        return true;
    }

    // Increment _regenVersion at the end of each successful pipeline run
    // (inside the existing dirty-driven Update path, after ctx is repopulated).
}
```

`PCGMapCompositeVisualization` and `PCGMapVisualization` follow the same shape.
`PCGMapVisualization` (GPU lantern) returns null for `Tilemap` if it does not
host one — V.a and V.b will then no-op against that source. (See §15 open
question on lantern hosting.)

### 4.3 Source binding model

Phase V components hold a single `IMapContextSource` reference, assigned in the
Inspector via `[SerializeField] private MonoBehaviour source` plus a runtime cast
(Unity does not serialize interface references directly). When the field references
a component that does not implement `IMapContextSource`, the component logs once
and goes inactive.

### 4.4 Lifecycle

`IMapContextSource` does not extend `IDisposable`. The lifetime of `Context`
belongs to the source. Phase V consumers MUST treat `Context` as transient: they
re-fetch the reference each frame they use it, and they accept that
`source.Context == null` is a normal state.

---

## 5. V.a — Hover Tooltip (`PCGHoverTooltip`)

### 5.1 Component model

`PCGHoverTooltip` is a `MonoBehaviour` placed on a screen-space `Canvas`
GameObject (or auto-creating one in `OnEnable` if none is wired). It owns a
single `Text` (or `TextMeshProUGUI`) child plus a panel background.

```
PCGHoverTooltip (MonoBehaviour)
  ├── source: IMapContextSource (Inspector ref)
  ├── canvas: Canvas (auto-created if null)
  ├── panel: RectTransform background
  ├── label: TextMeshProUGUI
  └── camera: Camera (default Camera.main)
```

### 5.2 Update loop

```
1. If source == null OR source.Context == null OR source.Tilemap == null:
       hide panel; return.
2. Convert mouse position to world via camera.ScreenToWorldPoint.
3. If !source.TryWorldToCell(world, out x, out y):
       hide panel; return.
4. Build tooltip text from (x, y) against source.Context.
5. Position panel near cursor with offset (default +12, -12 px),
   clamp inside canvas RectTransform bounds.
6. Show panel.
```

### 5.3 Tooltip text schema

The tooltip is composed top-to-bottom in the following order. Each section is
omitted entirely when it would be empty.

```
(x, y)                                  ← always present

Layers:                                 ← header omitted if no layers set at cell
  Land
  LandEdge
  Walkable

Fields:                                 ← header omitted if no fields created
  Height        : 0.612
  CoastDist     : 7
  Moisture      : 0.448
  Temperature   : 0.701
  Biome         : Forest (3)
  BiomeRegionId : 4
```

Per-field formatting rules:

| Field | Format | Source |
|-------|--------|--------|
| `Height` | `F3` (3 decimals) | direct |
| `Moisture` | `F3` | direct |
| `Temperature` | `F3` | direct |
| `CoastDist` | integer (`F0`) | rounded; field is BFS distance |
| `Biome` | `Name (id)` | `(BiomeType)((int)value)` formatted via `ToString()` |
| `BiomeRegionId` | integer (`F0`) | direct |
| `FlowAccumulation` | integer (`F0`) | direct |

Layer membership is computed by iterating all `MapLayerId` values 0..COUNT-1 and
calling `ctx.IsLayerCreated(id) && ctx.GetLayer(id).Get(x, y)`. Fields use the
same `IsFieldCreated` gating.

### 5.4 Performance budget

The tooltip rebuilds the string at most once per frame (only when the cell index
changes from the previous frame, OR `RegenerationVersion` changes). Per-frame
cost is dominated by the `MapLayerId.COUNT` (15) + `MapFieldId.COUNT` (7) bit
reads, all O(1). String allocation is unavoidable but bounded by row count
(≤ ~25 lines worst-case). No GC concerns for a development tool.

### 5.5 Hide-when-no-context behavior

When `source.Context == null` (regen in progress, source disabled), V.a hides
the panel without logging. When `TryWorldToCell` returns false (cursor outside
map), V.a hides the panel. There is no "stale data" warning; the user is
expected to know whether the pipeline is currently running.

---

## 6. V.b — Per-Cell Overlay System (`PCGRuntimeOverlay`)

### 6.1 Component model

`PCGRuntimeOverlay` is a `MonoBehaviour` placed on the same GameObject as the
source (or any sibling). It owns one renderer per active mode (text and/or
color) and rebuilds its derived state when `RegenerationVersion` changes,
when the selected source/field/palette changes, or when the user toggles a
mode flag.

```
PCGRuntimeOverlay (MonoBehaviour)
  ├── source: IMapContextSource (Inspector ref)
  ├── enableTextOverlay: bool
  ├── enableColorOverlay: bool
  ├── textField: ScalarOverlaySource          ← V-DD-4
  ├── textFormat: enum { Auto, F0, F1, F2, F3 }
  ├── textColor: Color (default near-black with alpha)
  ├── textFontSize: float (default 0.35 × cellSize)
  ├── textSoftCap: int (default 128, edits clamped to 1..256)
  ├── colorField: ScalarOverlaySource         ← discrete fields only; see §6.4
  ├── colorPalette: BiomeColorPalette         ← used when colorField == Biome
  ├── colorAlpha: float (0..1, default 0.65)
  ├── unmappedColor: Color (default magenta)  ← rendered for IDs not in palette
  ├── _textRenderer: TextMeshPro instance (child GO, HideFlags.DontSave)
  └── _colorRenderer: ScalarOverlayRenderer  (existing class, reused)
```

### 6.2 Mode A — Text overlay

#### 6.2.1 Soft cap behavior (V-DD-5)

`textSoftCap` defaults to 128. The total cell count is `Width × Height`. When
either dimension exceeds `textSoftCap`, the overlay is **not** built; instead a
single warning line is logged once per regeneration:

```
[PCGRuntimeOverlay] Text overlay suppressed: 256x256 > soft cap 128.
                    Increase textSoftCap or reduce map resolution.
```

The renderer is hidden but not destroyed (toggling resolution back below the
cap re-enables it without reallocation). Hard upper bound (compile-time guard):
256 to keep mesh-vertex counts inside `TextMeshPro`'s 16-bit index limit
(256² × 4 verts/glyph ≈ 262 144 — exceeds 65 535 even at one glyph per cell;
see §15 open question on glyph count).

Practical suggestion captured in §6.2.4: at 128² the overlay shows ~16 384
labels, each 1–4 glyphs, well inside TMP's mesh limits.

#### 6.2.2 Rendering technique (V-DD-5)

Single `TextMeshPro` component (3D, not UGUI) parented to the source's transform
and scaled to match the tilemap world rect. The text content is built once per
rebuild as a single string with explicit per-glyph positioning via
`<pos=...>` rich-text tags, OR built as a manual `TMP_Text.SetText` with
character-anchor placement. Implementation chooses whichever Unity TMP version
in use supports cleanly; both produce a single mesh and a single draw call.

Alignment matches `ScalarOverlayRenderer.AlignToTilemap`: bottom-left of the
overlay coincides with the tilemap origin; per-cell positions are computed in
the same row-major order; flipY is applied identically (V-DD-2 makes this trivial
since the source already exposes the convention).

#### 6.2.3 Field selection and sampling

`textField` is a `ScalarOverlaySource` (V-DD-4). Pipeline-field sources are read
from `ctx` via `GetField(MapFieldId)`. Noise and derived previews follow the
exact recomputation pattern already used by `ScalarOverlayRenderer` consumers
(via `MapNoiseBridge2D.FillNoise01`, etc.) — no new noise plumbing.

When the selected field is uncreated (`!IsFieldCreated`), the overlay renders
nothing (zero labels) and logs once per regeneration:

```
[PCGRuntimeOverlay] Text overlay: field Temperature not created (Phase M off?).
```

#### 6.2.4 Format and visual defaults

`textFormat = Auto` chooses per-field defaults:

| Field type | Format |
|-----------|--------|
| `Height`, `Moisture`, `Temperature` | `F2` (2 decimals; 0.00..1.00 fits in 4 chars) |
| `CoastDist`, `BiomeRegionId`, `FlowAccumulation` | `F0` |
| `Biome` | `F0` (the ordinal; full name belongs to V.a, not the dense overlay) |
| Noise/derived previews | `F2` |

`textFontSize` default 0.35 × cellSize keeps a 4-character label inside one
cell with margin at most resolutions.

### 6.3 Mode B — Discrete color overlay

#### 6.3.1 Renderer reuse

Reuses `ScalarOverlayRenderer` (existing, internal to `Adapters.Tilemap`)
unchanged. `PCGRuntimeOverlay` owns its own instance — it does not share or
contend with the two scalar-overlay slots already in `PCGMapTilemapVisualization`.
Sorting order is one above the existing slots so V.b paints on top by default.

This requires `ScalarOverlayRenderer` to be **promoted from `internal` to
`public`** (see §13 documentation updates and §15 open question on namespace
placement).

#### 6.3.2 Discrete vs continuous

`ScalarOverlayRenderer.SetData` interpolates a two-color ramp. For discrete
overlay use, V.b builds the `Color32[]` buffer directly (per-cell palette
lookup) and calls a sibling method:

```csharp
// To be added to ScalarOverlayRenderer:
public void SetDataDirect(Color32[] colors, int width, int height, bool flipY);
```

This change is small (~30 lines) and additive — no existing behavior changes.
The new method follows the same row-iteration + single-`Apply()` pattern.

#### 6.3.3 Field-to-palette binding

| `colorField` | Palette source |
|--------------|----------------|
| `Biome` | `colorPalette` (`BiomeColorPalette` SO; see §7) |
| `BiomeRegionId` | Deterministic hash-color: `HSV(hash(id)/uint.Max, 0.55, 0.85)` (V-DD-7) |

All other `ScalarOverlaySource` values are rejected for color mode (Inspector
validation: revert + log warning). Continuous fields belong in the existing
scalar-overlay slots.

#### 6.3.4 Unmapped IDs

When `colorField == Biome` and the cell's biome ordinal is not present in the
palette's mapping, the cell renders as `unmappedColor` (default magenta). This
is intentionally loud — it signals palette/biome desync.

When `colorField == BiomeRegionId`, every nonzero ID has a color (hash-derived);
zero (water) renders as `unmappedColor` with alpha 0 (transparent).

### 6.4 Refresh handling

```
private int _lastRegenVersion = -1;
private bool _structDirty = true;  // set by Inspector changes via OnValidate

void Update()
{
    if (source == null || source.Context == null) return;
    if (source.RegenerationVersion != _lastRegenVersion || _structDirty)
    {
        Rebuild();
        _lastRegenVersion = source.RegenerationVersion;
        _structDirty = false;
    }
}
```

`Rebuild()` updates whichever modes are enabled. Disabled modes hide their
renderer without disposing it.

### 6.5 Interaction with existing scalar overlay slots

V.b does not touch the two scalar-overlay slots on `PCGMapTilemapVisualization`.
The user is expected to disable them when V.b is active to avoid stacking,
but no enforcement is required — sorting order makes the layering
deterministic (V.b above scalar slots above tilemap).

---

## 7. `BiomeColorPalette` Asset

### 7.1 Asset shape

```csharp
namespace Islands.PCG.Inspection
{
    [CreateAssetMenu(fileName = "BiomeColorPalette",
                     menuName = "Islands/PCG/Inspection/Biome Color Palette")]
    public sealed class BiomeColorPalette : ScriptableObject
    {
        [System.Serializable]
        public struct Entry
        {
            public BiomeType biome;
            public Color color;
        }

        [SerializeField] private Entry[] entries;
        [SerializeField] private Color fallbackColor = Color.magenta;

        // Built lazily on first lookup; rebuilt when entries change.
        // Stable index lookup keyed on (int)BiomeType.
        public Color Lookup(int biomeOrdinal);
    }
}
```

### 7.2 Default content

A starter asset `BiomeColorPalette-Default.asset` ships with one entry per
`BiomeType` value (count drawn from the current `BiomeType` enum). Initial
colors follow the conventional climate-color mapping (cold → blue/white,
temperate forest → green, desert → tan, beach → pale yellow, tropical → lush
green). Exact palette deferred to implementation; recorded as an open
question (§15) only insofar as "these colors should be reviewed by an artist
eventually."

### 7.3 Lookup behavior

`Lookup(ordinal)` returns the matching `Entry.color` if present, else
`fallbackColor`. Multiple entries with the same `biome` are not validated —
last-wins by iteration order. (Validation is editor-time; no test gate.)

---

## 8. Coordinate, flipY, and Multi-Layer Interaction

### 8.1 flipY containment

Phase V consumers never apply flipY themselves. All flipY logic lives inside
`IMapContextSource.TryWorldToCell` and inside the rendering convention used by
`ScalarOverlayRenderer` and the text-overlay placement. This is the same
discipline `ScalarOverlayRenderer.SetData` already enforces and is the reason
the existing scalar overlays render correctly under both flip orientations.

### 8.2 Multi-layer routing independence (V-DD-10)

The hover tooltip iterates all `MapLayerId` values directly against
`ctx.IsLayerCreated` and `ctx.GetLayer(id).Get(x, y)`. It does **not** consult
the `s_baseLayers` / `s_overlayLayers` / `s_colliderLayers` partition arrays in
`PCGMapTilemapVisualization`. The tooltip's primary debugging value is exactly
the case where a layer is created in `ctx` but missing from those partitions —
the tooltip will report the layer's presence, the tilemap will not draw it, and
the desync is visible immediately.

This deliberately decouples Phase V from the multi-layer routing maintenance
rule recorded in `CURRENT_STATE.md` lines 153–162. New `MapLayerId` additions
do not require Phase V updates.

---

## 9. Editor vs Runtime Scope (V-DD-6)

| Component | Editor (Edit Mode) | Editor (Play Mode) | Standalone Runtime |
|-----------|--------------------|--------------------|--------------------|
| `IMapContextSource` | ✓ | ✓ | ✓ |
| V.a `PCGHoverTooltip` | ✓ (uses `EditorApplication.update`-driven Update via `[ExecuteAlways]`) | ✓ | ✓ |
| V.b `PCGRuntimeOverlay` | ✓ (`[ExecuteAlways]`) | ✓ | ✓ |
| `BiomeColorPalette` | ✓ (asset) | ✓ | ✓ |

Both V.a and V.b carry `[ExecuteAlways]` so they function during edit-mode
visualization runs (which are the dominant testing surface today). Runtime
behavior is identical — there are no `#if UNITY_EDITOR` divergences in the
update path.

---

## 10. Test Plan

Phase V is read-only tooling. There are **no goldens, no determinism gates,
no `MapPipelineRunner2D` invariants**. The test surface is intentionally light.

### 10.1 Optional unit tests (editor-only)

| Test | Target | Purpose |
|------|--------|---------|
| `IMapContextSourceTryWorldToCell_RoundTrips` | Each viz class | Cell `(x, y)` → cell-center world → `TryWorldToCell` returns `(x, y)` for all valid cells, both flipY=true and flipY=false |
| `IMapContextSourceRegenerationVersion_IncrementsOnRegen` | `PCGMapTilemapVisualization` | Forcing `dirty = true` and running `Update()` increments `RegenerationVersion` by ≥ 1 |
| `BiomeColorPaletteLookup_ReturnsFallbackForUnknownOrdinal` | `BiomeColorPalette` | Lookup of `(int)BiomeType.COUNT + 99` returns `fallbackColor` |
| `BiomeColorPaletteLookup_ReturnsEntryForKnownOrdinal` | `BiomeColorPalette` | Lookup of each declared entry returns the asset's color |

Total ≤ ~6 tests. They guard the contracts that other code depends on
(`TryWorldToCell` round-trip is the largest landmine). They do not exercise
rendering or UI.

### 10.2 No pipeline-level tests

`MapPipelineRunner2DGoldenLTests` and `MapPipelineRunner2DGoldenM2Tests` are
**not** modified by Phase V. No new golden file. No new snapshot hash.

---

## 11. Visual Smoke Test Protocol

Per project rules, every slice writing a new visual surface includes a smoke
checklist. Phase V is unusual in that its "visual surface" is the tooling
itself, so the checklist is structured around inspection rather than golden
capture.

### 11.1 V.a smoke checklist

| # | Step | Expected | Red flag |
|---|------|----------|----------|
| 1 | Add `PCGHoverTooltip` to scene; assign source = `PCGMapTilemapVisualization`. Hover over land cells. | Tooltip shows `(x,y)`, layers including `Land` / `LandInterior` / `Walkable`, `Height` 0.0..1.0. | Tooltip never shows; tooltip shows but layer list empty when cell is clearly land |
| 2 | Hover over deep-water cells. | Layer list shows `DeepWater`; no `Land`. `Height` near 0. | `Land` reported on water |
| 3 | Disable Phase M (biomeStage off). Hover anywhere. | `Temperature`, `Moisture`, `Biome` lines absent (V-DD-11). | Lines show as `0.000` instead of being absent |
| 4 | Move cursor off the map edge. | Tooltip hides. | Tooltip persists with last-known cell |
| 5 | Change seed; while regenerating, hover. | Tooltip briefly hides during the frame `ctx == null`, returns once regen completes. | Tooltip shows stale data after regen |

### 11.2 V.b smoke checklist (text overlay)

| # | Step | Expected | Red flag |
|---|------|----------|----------|
| 1 | 64×64 map; `enableTextOverlay = true`, `textField = Height`. | Each cell shows a 2-decimal value 0.00..1.00 aligned to its cell. | Labels off-grid; labels overflow cells noticeably |
| 2 | Switch `textField = Biome` with M enabled. | Each land cell shows the biome ordinal (small integer); water cells show 0. | Continuous gradient shown (would indicate scalar mode active by mistake) |
| 3 | Increase resolution to 256; soft cap remains 128. | Overlay disappears; warning logged once. | Editor freezes / crash / silent suppression with no warning |
| 4 | Reduce back to 128; overlay returns without restart. | Labels render again. | Reload required |
| 5 | Toggle flipY on the source. | Labels follow the visual flip. | Labels mirror against tiles |

### 11.3 V.b smoke checklist (color overlay)

| # | Step | Expected | Red flag |
|---|------|----------|----------|
| 1 | `colorField = Biome`, palette = default. | Discrete biome colors visible; matches the underlying biome layout at all cells. | Continuous gradient instead of discrete patches |
| 2 | Remove one biome entry from the palette. | Cells with that biome render as `unmappedColor` (magenta default). | Cells render transparent or with previous color |
| 3 | `colorField = BiomeRegionId` after running M2.b. | Each region renders as a distinct hash-derived color; same region = same color across regen with identical seed. | Region colors flicker frame-to-frame; same seed yields different colors |
| 4 | Run with seed A then seed B; same regions. | Region color is deterministic per-ID, so a region with the same ID gets the same color. (Note: the *set* of regions changes with seed — colors are stable per-ID, not per-region-shape.) | Color depends on something other than the ID |
| 5 | Cross-check with V.a: hover a colored region, read the `BiomeRegionId` value. | The ID is consistent with the cell's color across the whole region. | Color and reported ID disagree |

### 11.4 Cross-component smoke test

Run V.a and V.b together. Toggling V.b modes should not affect V.a behavior.
Both should observe the same `RegenerationVersion` and refresh in lockstep.

---

## 12. Expected Files

Located under the inspection package (final namespace TBD — see §15).

| File | Type | Purpose |
|------|------|---------|
| `IMapContextSource.cs` | interface | The §4 contract |
| `PCGHoverTooltip.cs` | MonoBehaviour | V.a |
| `PCGRuntimeOverlay.cs` | MonoBehaviour | V.b |
| `BiomeColorPalette.cs` | ScriptableObject | §7 palette asset |
| `BiomeColorPalette-Default.asset` | data asset | Starter palette |

Modifications to existing files (one-line interface declarations + minimal property
implementations as in §4.2):

| File | Change |
|------|--------|
| `PCGMapTilemapVisualization.cs` | Add `: IMapContextSource`; implement 4 properties + `TryWorldToCell`; increment `_regenVersion` after pipeline run |
| `PCGMapCompositeVisualization.cs` | Same shape |
| `PCGMapVisualization.cs` | Same shape; `Tilemap` returns null if not hosted |
| `ScalarOverlayRenderer.cs` | Add `SetDataDirect(Color32[], int, int, bool)`; promote class from `internal` to `public` |

Optional test files (per §10.1):

| File | Purpose |
|------|---------|
| `IMapContextSourceTests.cs` | Round-trip + version-increment tests |
| `BiomeColorPaletteTests.cs` | Lookup behavior |

---

## 13. Dependencies and Sequencing

### 13.1 Hard dependencies

None. Phase V can be implemented at any point after Phase F0 (since it requires
`MapContext2D` to exist).

### 13.2 Soft dependencies (richer payload)

| Dependency | Effect on Phase V |
|-----------|-------------------|
| Phase G (`CoastDist`) | V.a `CoastDist` line populated |
| Phase L (`FlowAccumulation`, Rivers, Lakes) | V.a layer + field lines populated |
| Phase M (`Temperature`, `Moisture`, `Biome`) | V.a climate lines + V.b `Biome` color overlay populated |
| Phase M2.a (Vegetation) | `Vegetation` layer line populated |
| Phase M2.b (`BiomeRegionId`) | V.a region ID line + V.b region color overlay populated |

All current implemented phases (F0–G–L–M–M2a–M2b) are present, so Phase V's
soft dependencies are satisfied.

### 13.3 Sub-phase sequencing

V.a and V.b are independent. Recommended order:

1. **V.a first.** Implement `IMapContextSource` + `PCGHoverTooltip`, wire to all
   three viz classes. Validate via §11.1 checklist.
2. **V.b second.** Add `PCGRuntimeOverlay` + `BiomeColorPalette` + the
   `ScalarOverlayRenderer.SetDataDirect` extension. Validate via §11.2 / §11.3.

V.a alone delivers ~70 % of the inspection value (point-query is the daily
debug tool); V.b is the broader visualization helper.

---

## 14. What Phase V Does NOT Do

- **No authoring.** No layer is written, no field is filled, no asset is
  modified by Phase V components at runtime.
- **No persistence.** No serialized data crosses sessions except the
  `BiomeColorPalette` asset itself (which is authored data, not generated).
- **No new `MapLayerId` or `MapFieldId`.** `MapLayerId.COUNT` stays 15;
  `MapFieldId.COUNT` stays 7.
- **No pipeline modification.** `MapPipelineRunner2D` is untouched. No new
  `IMapStage2D`. No new operator.
- **No determinism gate.** Phase V does not consume or produce hashes.
- **No golden coverage.** Existing pipeline goldens are unchanged.
- **No SSoT promotion.** Phase V is not added to `SSoT_INDEX.md`.
- **No multi-layer routing audit.** The deferred `PCGMapVisualization` lantern
  routing audit remains a separate work item; Phase V's inspection surface will
  make that audit easier but does not perform it.
- **No biome palette governance.** Default colors ship as data; no contract
  requires them to match any external standard.
- **No replacement of existing overlays.** The two scalar-overlay slots on
  `PCGMapTilemapVisualization` remain. V.b's color overlay is additive.

---

## 15. Open Questions (Implementation Time)

These are recorded for the implementer to resolve in the implementation PR.
None blocks the design; all are isolated to small surfaces.

1. **Inspection package namespace.** Proposed `Islands.PCG.Inspection`. The
   four new files plus the `BiomeColorPalette` asset live there. Existing viz
   classes stay in `Islands.PCG.Adapters.Tilemap` and only add the interface
   reference (the interface itself lives in `Inspection`). Confirm
   namespace at implementation time; adjust assembly-definition references
   if the inspection code lives in its own asmdef.

2. **`ScalarOverlayRenderer` namespace move.** Promoting from `internal` to
   `public` may motivate moving it from `Adapters.Tilemap` to `Inspection`, or
   leaving it in place and exporting it. Implementer's call.

3. **TextMeshPro version.** The text overlay assumes TMP is present in the
   project. If TMP is not a project dependency, fall back to `MeshRenderer` +
   manual font atlas, or block V.b text mode behind a TMP install warning.
   Confirm at implementation time.

4. **TMP mesh-vertex limit at high resolution.** At 128² × 4 chars × 4
   verts/glyph ≈ 262 144 verts. TMP supports `m_isVolumetricText`-style
   batching but the 16-bit index buffer caps at 65 535. Implementer must
   either (a) split the overlay across multiple TMP submeshes, (b) reduce
   the soft cap below 128², or (c) require Unity's 32-bit index buffer
   path. (b) is the safest default; (a) is the cleanest extension.

5. **Default biome palette colors.** The seven-or-so biome colors are
   utilitarian defaults and not authored by an artist. They are easy to
   replace post-shipping. No design commitment beyond "loud enough to
   distinguish on a 64² map at typical zoom".

6. **`PCGMapVisualization` (lantern) hosting.** V.a/V.b assume a `Tilemap`
   exists. The lantern viz may not host one; in that case its
   `IMapContextSource.Tilemap` returns null and Phase V components no-op
   against it gracefully. If the lantern's primary use case is precisely the
   one needing V.a, the implementer may add a minimal hidden `Tilemap` to
   the lantern surface — design-out-of-scope for V.

7. **Auto-canvas vs explicit canvas for V.a.** V.a creates its own canvas if
   none is wired. Implementer chooses whether to expose canvas-creation
   parameters (sort order, scale mode) in the Inspector or hardcode sensible
   defaults.

8. **Refresh on Inspector edit.** The `_structDirty` flag is set in
   `OnValidate` for `[SerializeField]` changes. Whether `OnValidate` is
   called fast enough during scrubbing is a Unity-version detail; if not,
   add an explicit "Rebuild Now" Inspector button.

---

## 16. Glossary and Cross-References

- **`MapContext2D`** — `Layout/Maps/MapContext2D.cs`. The pipeline's run-scoped
  data container.
- **`ScalarOverlayRenderer`** — `Adapters/Tilemap/ScalarOverlayRenderer.cs`.
  Existing texture-based overlay primitive; V.b reuses it with a small
  additive method (§6.3.2).
- **`ScalarOverlaySource`** — `Adapters/Tilemap/ScalarOverlaySource.cs`.
  Existing field-source enum reused by V.b's selectors (V-DD-4).
- **Multi-layer routing rule** — `CURRENT_STATE.md` lines 153–162. Phase V
  intentionally bypasses this partition for hover (V-DD-10).
- **Phase L design** — `Phase_L_Design.md`. Source of `Rivers`, `Lakes`,
  `FlowAccumulation` payloads visible in V.a.
- **Phase M2.b design** — `Phase_M2_Design.md` §3. Source of `BiomeRegionId`
  payload visible in V.a and V.b.

---

## 17. Required Documentation Updates (after design lands)

These updates execute once this design doc is committed; they should NOT be
auto-applied — confirm before edits.

| Doc | Change |
|-----|--------|
| `PCG_Roadmap.md` line 73 | Status snapshot: `Phase V: planning (design complete)` |
| `PCG_Roadmap.md` §912 | Add design-doc pointer line at top of Phase V section |
| `PCG_Roadmap.md` §98–105 design-doc table | Add row: `Phase V \| Phase_V_Design.md \| Complete` |
| `CURRENT_STATE.md` lines 119–121 | Update Phase V notes to point at `Phase_V_Design.md`; remove "to be written when phase activates" |
| `CURRENT_STATE.md` lines 169–172 | "Next batch" shifts from "author Phase_V_Design.md" to "implement Phase V.a" |
| `SSoT_INDEX.md` | No change. Phase V is planning authority only, not a promoted subsystem. |

No subsystem SSoT touch. No `coverage-matrix.md` entry (Phase V owns no
`MapLayerId` or `MapFieldId`).
