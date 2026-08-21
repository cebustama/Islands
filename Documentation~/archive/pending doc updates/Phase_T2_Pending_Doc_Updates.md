# Phase T2 — Pending documentation updates

Status: **PARTIALLY APPLIED — 2026-08-21. LIVE. 5 of 7 items applied; 2 blocked.**
Blocked: **§1.4** (design-doc table row) and **§3** (`SSoT_INDEX.md` active-planning entry).
Both are conditional on `Phase_T2_Design.md` existing. It does not exist, in the package or
in project knowledge, so both stay unapplied — a table row and an index entry pointing at a
non-existent file are worse than their absence. **Do not archive this queue until
`Phase_T2_Design.md` is written and those two items are applied.** They exist nowhere else.

Applied 2026-08-21: §1.1, §1.2, §1.3, §2.1, §2.2. Each anchor located exactly once before
substitution and each REPLACE confirmed present afterwards, except §2.1 — see its in-place
note. §4 is resolved by user decision, recorded in place.

No code exists for Phase T2. These items add a planning branch to the roadmap and adjust the
two governance surfaces that track adapter-track status. **Nothing here asserts implemented
truth**, and nothing applied from this queue promotes planning or reference material to
living authority.

Each item is a literal search/replace pair or an insert-before anchor, in the same idiom as
`Phase_W_Pending_Doc_Updates.md` and `W_b_Pending_Doc_Updates.md`. Applied items are
retained for the record; **do not re-apply them.**

## Evidence backing these updates (session 2026-08-20)

This queue is **planning-only**. The evidence gate applies to the anchors and to the
facts quoted inside the new text, not to any implementation claim.

Verified this session by direct file read:

- `MapFieldId.Height = 0`; `MapDataExport` exposes `HasField(MapFieldId)`,
  `GetField(MapFieldId)`, `GetValue(id, x, y)`, `Width`, `Height`, `Length`, `Seed`
  — `MapIds2D.cs`, `MapDataExport.cs`.
- Phase T1 is planning with design complete and **not implemented**; it specifies a
  smooth vertex-per-grid-point mesh at `(i*cellSize, Height[i,j]*heightScale, j*cellSize)`,
  `MeshAdapter2D`, `PCGMapMeshVisualization`, `Islands.PCG.Adapters.Mesh.asmdef`,
  11 EditMode tests — `PCG_Roadmap.md` §"Phase T1", `Phase_T1_Design.md`.
- `Phase_T1_Design.md` §2.1 records the decision to duplicate the layer→color entry
  type rather than couple adapter asmdefs, and states that a **third adapter** makes
  extraction a mechanical refactor. Phase T2 is that third adapter.
- Layer priority resolution (entries evaluated low→high, last ON layer wins) exists in
  `TilemapAdapter2D` and is restated as the vertex-color rule in `Phase_T1_Design.md`
  §3.2 — i.e. the logic is already duplicated once.
- Post-W-aux.f measured `Height` range: max ≈ 0.959 / 0.963 / 0.938, min on land
  ≈ 0.412 (res 256, seeds 56 / 8 / 243) — `CURRENT_STATE.md` §W-aux.f.
- `Corner-Height_Terrain_in_RCT__Transport_Tycoon__OpenTTD_and_OpenRCT2__Data_Model__Legal_Slopes_and_Rendering.md`
  is present in project knowledge — directory listing, this session.

Not verified, and **no item below asserts otherwise**:

- The package folder tree. `Islands.PCG.Adapters.Mesh.asmdef` and
  `Runtime/PCG/Samples/PCG Map Mesh/` are names taken from the Phase T1 roadmap entry;
  the on-disk layout was not inspected. The new roadmap text deliberately names no new
  file paths.
- Whether any partial mesh-adapter code already exists on disk.
- Everything in the corner-height research artifact. It is external comparative
  evidence (authority tier 7 per `SSoT_INDEX.md`), never implementation authority.

---

## 1. `PCG_Roadmap.md`

### 1.1 — Insert the Phase T2 branch

Anchor: the `### Phase I` heading that immediately follows the Phase T1 entry.

```
INSERT BEFORE:
### Phase I
Burst / SIMD upgrades

INSERTED TEXT:
### Phase T2 — 3D Relief Adapters (stepped, corner-height, voxel, wireframe)
**Planning. Not designed. Sequenced on adapter track, parallel to mainline.**

A family of 3D relief views over the same `MapDataExport`, sharing one sampling and
assembly core. The branch exists because the four candidate styles — smooth surface,
stepped tiles, corner-height (RollerCoaster Tycoon / Transport Tycoon model), and voxel —
differ only in how a cell becomes triangles. Everything else is common: reading the
export, deriving and quantizing height, resolving colour by layer priority, assembling
the `Mesh`, determinism guarantees, and the `[ExecuteAlways]` host with dirty tracking.

**Layered structure (proposed, not designed):**

- **Layer 0 — height sampling.** `MapDataExport` + policy → per-cell heights, continuous
  or quantized to integer levels. Owns the missing-`Height` guard, the normalization
  range and the quantization rule. Pure; testable without a `Mesh` or a scene.
  Rationale for centralizing: quantization has measurable consequences (post-W-aux.f,
  land `Height` occupies roughly [0.41, 0.96], not [0, 1]), and if each emitter quantized
  independently, two styles would disagree on elevation for the same seed — which
  defeats side-by-side comparison, the purpose of the whole branch.
- **Layer 1 — geometry emitters.** Pure functions `(heights, colours, params) → MeshBuffers`,
  where `MeshBuffers` is a flat vertex / index / colour struct. One emitter per style,
  sharing a quad-append helper that owns index bookkeeping and winding.
- **Layer 2 — one adapter facade** taking a style selector, so the host, material
  handling, preset wiring, null guards and determinism tests exist once.
- **Layer 3 — hosts.** The mesh host is one component. **Wireframe is not a fifth style**
  — it is an alternative renderer that draws `MeshBuffers` edges as gizmo lines, and
  therefore works for every emitter at no extra cost.

**Slice sequence:**

- **T2.0 — Gizmo relief preview.** Layer 0 plus a gizmo host, written concretely with no
  emitter seam. One flat horizontal surface per cell at a quantized base height, with
  configurable step size and offset. No `Mesh`, no material, no shader, no normals, no
  index format. Deliverable: the relief of an already-generated map, visible in the Scene
  view.
- **T2.1 — `MeshBuffers` + stepped emitter + mesh host.** Extracts Layers 1 and 2 now that
  a second consumer exists. The gizmo host is retargeted to draw `MeshBuffers` edges,
  which generalizes wireframe to every later emitter.
- **T2.2 — Smooth emitter.** Phase T1's design, as an emitter. See the open decision below.
- **T2.3 — Corner-height emitter.** Four corner heights per tile, the "adjacent corners
  differ by at most one step" constraint enforced by an iterative clamp with a
  configurable policy (smooth, or accept the jump and emit a vertical wall quad), and
  per-tile water level from the water mask.
- **T2.4 — Voxel emitter.** Speculative, not scheduled. Reuses Layer 0's integer levels
  extruded to a floor; greedy meshing and chunking are optimizations, not requirements.

**Structural limit, carried from the research:** a heightfield admits one surface per
column. Caves, arches and overhangs do not fit this model — in RCT, tunnels are separate
track/path elements clipped by the surface, not terrain geometry. If volumetric caves
become a requirement, T2.3 is not the path; T2.4 or a multi-layer model would be.

**Open decision — relationship to Phase T1.** Options: (a) T1 is absorbed as the T2.2
emitter and its entry is annotated superseded; (b) T1 ships first as its own adapter and
T2 refactors afterwards; (c) both remain separate adapters and the duplication is
accepted. This determines asmdef ownership, type names and test placement. Unresolved —
do not start T2.1 without resolving it.

**Open decision — height parameterization.** Phase T1 uses a continuous `heightScale`;
the stepped and corner styles need levels plus a world-space step. Whether these coexist
or one derives from the other must be settled before Layer 0 is written.

**Extraction trigger already recorded.** `Phase_T1_Design.md` §2.1 chose to duplicate the
layer→colour entry type rather than couple adapter asmdefs, and stated that a third
adapter makes extraction a mechanical refactor. Phase T2 is that third adapter, and the
priority-resolution logic is already duplicated between `TilemapAdapter2D` and the T1
design. T2.1 is the point at which that extraction is due.

**Reference:** `Corner-Height_Terrain_in_RCT__Transport_Tycoon__OpenTTD_and_OpenRCT2__Data_Model__Legal_Slopes_and_Rendering.md`
— external comparative evidence for the corner-height model (data layout, the closed set
of legal slopes, cliff generation, per-tile water, tunnels as separate elements).
Reference tier only; not implementation authority.

- Depends on: Phase H2 (`MapDataExport`), Phase H3 (`MapGenerationPreset`). Both done.
- Does not block or depend on any mainline phase.
- No new `MapLayerId`, `MapFieldId` or runtime contract. Pure adapter/sample-side.
  Adapters-last invariant preserved; golden-neutral by construction.

```

### 1.2 — Add Phase T2 to the status snapshot

```
SEARCH:
- Phase T1: planning (design complete — adapter track)

REPLACE:
- Phase T1: planning (design complete — adapter track; see Phase T2 open decision)
- Phase T2: planning (not designed — adapter track, 3D relief family)
```

### 1.3 — Annotate the Phase T1 entry with the open decision

```
SEARCH:
### Phase T1 — PCG Map Mesh Visualization
**Planning. Design complete. Sequenced on adapter track, parallel to mainline.**
**See [`Phase_T1_Design.md`](Phase_T1_Design.md) for detailed design.**

REPLACE:
### Phase T1 — PCG Map Mesh Visualization
**Planning. Design complete. Sequenced on adapter track, parallel to mainline.**
**See [`Phase_T1_Design.md`](Phase_T1_Design.md) for detailed design.**

**Relationship to Phase T2 is an open decision (2026-08-20).** Phase T2 proposes a family
of 3D relief emitters sharing one sampling and assembly core, in which this phase's smooth
surface is one emitter (T2.2). Whether T1 is absorbed, ships first, or stays a separate
adapter is unresolved. Do not implement either phase past the point where that choice
binds asmdef ownership and type names.
```

### 1.4 — Design-doc table row (apply only if `Phase_T2_Design.md` is created)

```
SEARCH:
| Phase T1 | [`Phase_T1_Design.md`](Phase_T1_Design.md) | Complete |

REPLACE:
| Phase T1 | [`Phase_T1_Design.md`](Phase_T1_Design.md) | Complete |
| Phase T2 | [`Phase_T2_Design.md`](Phase_T2_Design.md) | Not started |
```

**Conditional.** Leave unapplied while T2 has no design document. A table row pointing at
a non-existent file is worse than an absent row.

> **NOT APPLIED — 2026-08-21.** `Phase_T2_Design.md` does not exist in the package or in
> project knowledge (checked this session). The SEARCH anchor
> `| Phase T1 | [`Phase_T1_Design.md`](Phase_T1_Design.md) | Complete |` was located and is
> unique (`PCG_Roadmap.md` L173 pre-application), so this item is ready to apply the moment
> the design document exists. Apply it together with §3.

---

## 2. `CURRENT_STATE.md`

Both items are planning-status only. Neither claims anything is implemented.

### 2.1 — Adapter-track sentence

```
SEARCH:
Adapter-track phases
T1 and Q2 remain independent and can be picked up at any time.

REPLACE:
Adapter-track phases
T1, T2 and Q2 remain independent and can be picked up at any time.
```

> **APPLIED 2026-08-21 WITH A RE-ANCHOR (user decision).** The pair above matched **zero
> times**: `CURRENT_STATE.md` wraps the sentence at a different point. Content identical,
> line break different. Under the no-approximation rule the item was stopped, reported, and
> re-anchored with user approval. The pair actually applied was:
>
> ```
> SEARCH:
> distilled from observing W-generated worlds. Adapter-track phases T1 and Q2 remain
> independent and can be picked up at any time.
>
> REPLACE:
> distilled from observing W-generated worlds. Adapter-track phases T1, T2 and Q2 remain
> independent and can be picked up at any time.
> ```
>
> Verified: one match before substitution, REPLACE present exactly once afterwards. The
> semantic change is the one this item asked for and nothing else.

### 2.2 — Deferred list

```
SEARCH:
Deferred / optional: H8b, T1, J, K, P, Q2, X1.b.

REPLACE:
Deferred / optional: H8b, T1, T2, J, K, P, Q2, X1.b.
```

---

## 3. `SSoT_INDEX.md` (conditional)

Apply **only** alongside item 1.4, i.e. only once `Phase_T2_Design.md` exists.

```
SEARCH:
## Current active planning docs
- `planning/active/PCG_Roadmap.md`

REPLACE:
## Current active planning docs
- `planning/active/PCG_Roadmap.md`
- `planning/active/Phase_T2_Design.md`
```

> **NOT APPLIED — 2026-08-21.** Same blocker as §1.4: no `Phase_T2_Design.md` exists.
> **Re-anchor before applying.** `SSoT_INDEX.md` §"Current active planning docs" was
> edited on 2026-08-21 (this queue and `W_b_Pending_Doc_Updates.md` were registered and
> archived respectively), so the two-line SEARCH above no longer matches the file. Do not
> approximate the substitution — rewrite the pair against the index as it then stands.

---

## 4. Research artifact placement

`Corner-Height_Terrain_in_RCT__Transport_Tycoon__OpenTTD_and_OpenRCT2__Data_Model__Legal_Slopes_and_Rendering.md`
belongs under `research/` per `SSoT_INDEX.md` authority tier 7 (historical or
investigative support only). No index edit is required — `SSoT_INDEX.md` calls out
individual reference docs but does not enumerate `research/` contents, and adding a
precedent for that would create an index that must be maintained per artifact.

If it is instead kept at the documentation root, it should carry a one-line header
stating its tier, so that a future reader cannot mistake it for a governed surface.

> **RESOLVED — user decision 2026-08-21: `research/`.** No index edit, per the paragraph
> above. The move itself is a package file operation and is **not performed by this
> session** — it remains a user action.
>
> **Recorded alongside the decision:** the user notes that this artifact is *the basis for
> the implementation* of the RCT-style corner-height adapter (T2.3). That is a statement
> about influence, not about authority, and the two must not be conflated. Reference-tier
> material may inform a design; it cannot be the thing an implementation is checked
> against, because it describes another team's product and carries no invariant this
> package can gate on. The place where the corner-height model becomes implementation
> authority is `Phase_T2_Design.md` §T2.3, when written: the legal-slope set, the
> adjacent-corner constraint and the per-tile water rule must be restated there as this
> package's own decisions, with the research artifact cited as their source. Until that
> document exists, T2.3 has a source but no authority — which is exactly why §1.4 and §3
> above are still blocked.

---

## 5. Not included in this queue, deliberately

- **`changelog-ssot.md`** — no entry. Adding a planning branch changes neither semantics
  nor authority. An entry becomes due when T2.0 closes and introduces real surface.
- **`coverage-matrix.md`** — no entry. Nothing owns a concept yet.
- **`supersession-map.md`** — no entry. Phase T1 is annotated, not superseded; the
  supersession only becomes real if option (a) of the open decision is chosen.
- **A parking-lot entry for voxels** — folded into the T2.4 slice instead, marked
  speculative there. A second home for the same idea invites drift.

## Application order

1. Item 1.1 (the branch text) before 1.2 and 1.3 — the snapshot and the T1 annotation
   both reference a phase that must exist first.
2. Items 2.1 and 2.2 after item 1.1.
3. Items 1.4 and 3 only when `Phase_T2_Design.md` exists.
4. Item 4 at any time.

## Rollback

Every item is a literal pair. Reverting means swapping SEARCH and REPLACE, except for
1.1, whose rollback is deletion of the inserted block up to but not including the
`### Phase I` heading.
