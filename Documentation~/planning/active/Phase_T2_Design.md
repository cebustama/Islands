# Phase T2 Design — 3D Relief Adapters

**Status: Complete. Written 2026-08-21 (batch T2.1-prep).**
**Authority: planning document (`SSoT_INDEX.md` tier 5). Not implementation authority.**
**Home: `Documentation~/planning/active/Phase_T2_Design.md`. Registered in `SSoT_INDEX.md`
§"Current active planning docs".**

Companion documents: `PCG_Roadmap.md` §"Phase T2 — 3D Relief Adapters" (branch overview,
slice sequence), `Phase_T1_Design.md` (retained as the specification of the T2.2 smooth
emitter — see §1.1), `CURRENT_STATE.md` §T2.0 (implementation record and measurements for
the closed slice).

Baseline at time of writing: T2.0 closed 2026-08-21 and green — three files, zero core
files modified, zero goldens touched. Evidence in `CURRENT_STATE.md` §T2.0.

---

## 1. Decision record

Decisions §1.1–§1.5 are the ones the roadmap flagged as blocking T2.1. §1.1 was resolved
before this document; it is recorded here, not re-debated. §1.2–§1.5 are resolved by this
document.

### 1.1 Relationship to Phase T1 — RESOLVED 2026-08-21: option (a), absorption

T1 is absorbed as the T2.2 emitter. `Phase_T1_Design.md` remains the specification of that
emitter, not of an independent adapter. Options (b) ship T1 first and refactor afterwards
and (c) keep two adapters were rejected.

Reasons, in order of weight:

1. `Phase_T1_Design.md` §2.1 already stated that a third adapter turns the layer→colour
   extraction into a mechanical refactor, and T2 is that third adapter — (a) honours that
   condition at the point it was declared due.
2. T1 was never implemented, so absorbing a design costs nothing, whereas absorbing shipped
   code would have meant refactoring with two live consumers.
3. Independent quantization per style would let two styles disagree on the elevation of the
   same cell at the same seed, which defeats side-by-side comparison — the purpose of the
   branch.

Accepted cost: the smooth surface is not available until T2.2.

Consequences bound by this document: asmdef ownership (§1.2), the permanent home of
Layer 0 (§1.3), test placement (§1.4), the shared-extraction home (§1.5), and shared type
names (§2–§3).

### 1.2 Asmdef ownership — `Islands.PCG.Adapters.Relief`

**Decision: one new assembly, `Islands.PCG.Adapters.Relief`, at
`Runtime/PCG/Adapters/Relief/`, created in T2.1. It owns Layers 0–3 of the branch: the
sampler, all emitters, the facade, and the hosts.**

This follows the `Islands.PCG.Adapters.Tilemap` precedent exactly: an adapter is its own
assembly under `Runtime/PCG/Adapters/<Name>/`, referencing core and inspection, never
referenced by them (adapters-last).

**Why not `Islands.PCG.Adapters.Mesh`** (the name `Phase_T1_Design.md` used):

- Inside a namespace whose final segment is `Mesh`, the bare identifier `Mesh` resolves to
  the namespace, not to `UnityEngine.Mesh`. Every `new Mesh()` in the facade and hosts —
  the assembly's central activity — would need full qualification. A permanent tax for a
  name.
- The package already has an unrelated `Runtime/Meshes/` domain (`Islands.Runtime`
  procedural-mesh generators). Two "mesh" homes invite misfiling.
- "Relief" names what the family renders; "Tilemap" names a medium because Unity's Tilemap
  *is* the medium. Here the medium (`Mesh`) is shared by four styles whose common subject
  is the relief.

This supersedes the asmdef name inside `Phase_T1_Design.md` §"Assembly", which was written
for an independent adapter that no longer exists (§1.1). The T1 design's *content*
(vertex layout, material strategy, orientation) is untouched; only its packaging is
overridden, by this document, per the absorption decision.

**Assembly definition (T2.1 creates this file verbatim):**

`Runtime/PCG/Adapters/Relief/Islands.PCG.Adapters.Relief.asmdef`

```json
{
    "name": "Islands.PCG.Adapters.Relief",
    "rootNamespace": "Islands.PCG.Adapters.Relief",
    "references": [
        "Islands.PCG.Runtime",
        "Islands.PCG.Inspection",
        "Islands.PCG.Adapters.Shared"
    ],
    "includePlatforms": [],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": false,
    "precompiledReferences": [],
    "autoReferenced": true,
    "defineConstraints": [],
    "versionDefines": [],
    "noEngineReferences": false
}
```

Reference rationale: `Islands.PCG.Runtime` for `MapDataExport` / `MapLayerId` /
`MapFieldId`; `Islands.PCG.Inspection` for `IMapContextSource` (the V.a inspection seam
the hosts refresh on); `Islands.PCG.Adapters.Shared` per §1.5. **Not referenced:**
`Islands.PCG.Samples.Shared` — no parameter is preset-backed (T2.0 decision, recorded in
`CURRENT_STATE.md` §T2.0); if preset wiring ever becomes real for this family, adding the
reference is the visible signal and requires adjudication under `SSoT_CONTRACTS.md` §M2.a
at that time. No TextMeshPro, no Unity.Mathematics until a concrete need appears.

### 1.3 Permanent home of Layer 0 — moved in T2.1, step 0

`SteppedHeightSampler2D.cs` and `PCGSteppedReliefGizmoPreview.cs` move from
`Runtime/PCG/Inspection/` to `Runtime/PCG/Adapters/Relief/`, and their namespace changes
`Islands.PCG.Inspection` → `Islands.PCG.Adapters.Relief`. This is the move the T2.0 file
headers announced ("temporary housing … moving this file to the eventual relief-adapter
assembly is mechanical"). Neither file references inspection *types* beyond
`IMapContextSource` (host only), so the move is a folder + namespace + `using` rename with
no logic change. It happens as **step 0 of T2.1**, before any new code, and is gated by
the full EditMode suite staying green with goldens unchanged.

`Islands.PCG.Inspection` keeps its purpose (inspection seam + diagnostic tooling) and
loses its temporary tenant. Its asmdef is untouched.

### 1.4 Test placement

- New folder: `Runtime/PCG/Tests/EditMode/Adapters/Relief/`.
- `SteppedHeightSampler2DTests.cs` moves there in T2.1 step 0 (its header records the
  Inspection placement as temporary alongside the code it tests). Same assembly
  (`Islands.PCG.Tests.EditMode`), so the existing `InternalsVisibleTo` grant that lets it
  build synthetic `MapDataExport` instances is unaffected.
- `Islands.PCG.Tests.EditMode.asmdef` gains two references in T2.1:
  `"Islands.PCG.Adapters.Relief"` and `"Islands.PCG.Adapters.Shared"`.
- All future branch tests (emitter tests, facade determinism tests) live in that folder.

### 1.5 Shared-extraction home — `Islands.PCG.Adapters.Shared`

**Decision: the layer-priority resolution is extracted in T2.1 into a new thin assembly,
`Islands.PCG.Adapters.Shared`, at `Runtime/PCG/Adapters/Shared/`.** This is the home
`Phase_T1_Design.md` §2.1 itself anticipated ("a shared extraction
(`Islands.PCG.Adapters.Shared`) is premature for a 2-field struct. If a third adapter
appears, extraction becomes a mechanical refactor"). The third adapter is here; the
condition is due. Placing it in `Islands.PCG.Inspection` was considered and rejected:
priority resolution is adapter presentation policy, not inspection tooling, and Inspection
must remain referencable without dragging adapter conventions along.

**What is extracted — the loop, not the entry types.** The rule duplicated today between
`TilemapAdapter2D` (entries evaluated low→high over the priority table, last ON layer at a
cell wins) and `Phase_T1_Design.md` §3.2 (the same rule restated for vertex colours)
becomes one pure static resolver:

```csharp
namespace Islands.PCG.Adapters.Shared
{
    /// Evaluates candidate layers low→high; returns the index of the last entry whose
    /// layer is ON at (x, y), or -1 if none matches. Pure; deterministic.
    public static class LayerPriorityResolver
    {
        public static int ResolveLastOn(
            MapDataExport export, ReadOnlySpan<MapLayerId> orderedLayers, int x, int y);
    }
}
```

(Exact signature may be tuned during T2.1 against the real call sites; the contract that
is fixed here is: pure, low→high, last-ON-wins, `-1` sentinel, no allocation per cell.)

Entry types stay per-adapter, exactly as T1 §2.1 decided: `ProceduralTileEntry`
(`MapLayerId` → tile) stays in `Adapters.Tilemap`; the relief family defines its own
`MapLayerId` → `Color32` entry in `Adapters.Relief`. What each adapter shares is the
*order semantics*, which is precisely the part that must never diverge.

**T2.1 refactors `TilemapAdapter2D` to delegate its inner loop to the resolver.** This is
the one deliberate touch to shipped adapter code in the branch. It is guarded by the
existing `TilemapAdapter2DTests` plus the full golden suite; behaviour must be
bit-identical. `Islands.PCG.Adapters.Tilemap.asmdef` gains the
`"Islands.PCG.Adapters.Shared"` reference.

**Assembly definition (T2.1 creates this file verbatim):**

`Runtime/PCG/Adapters/Shared/Islands.PCG.Adapters.Shared.asmdef`

```json
{
    "name": "Islands.PCG.Adapters.Shared",
    "rootNamespace": "Islands.PCG.Adapters.Shared",
    "references": [
        "Islands.PCG.Runtime"
    ],
    "includePlatforms": [],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": false,
    "precompiledReferences": [],
    "autoReferenced": true,
    "defineConstraints": [],
    "versionDefines": [],
    "noEngineReferences": false
}
```

Adapter-independence is preserved: `Tilemap` and `Relief` both reference `Shared`; neither
references the other; `Shared` references only core. Core references none of them
(adapters-last).

---

## 2. `MeshBuffers` contract

The single geometry currency of the branch. Emitters produce it; the facade uploads it to
a `UnityEngine.Mesh`; the wireframe renderer reads its edges. Lives in
`Islands.PCG.Adapters.Relief`.

```csharp
namespace Islands.PCG.Adapters.Relief
{
    /// Flat geometry buffers. The only output type of emitters and the only input type
    /// of the mesh upload and the wireframe renderer. Never wraps or references a
    /// UnityEngine.Mesh.
    public sealed class MeshBuffers
    {
        public readonly List<Vector3> Vertices;
        public readonly List<int>     Indices;   // triangle list
        public readonly List<Color32> Colors;    // parallel to Vertices

        public MeshBuffers(int vertexCapacity, int indexCapacity);

        public void Clear();   // keeps capacity; enables host-side reuse across refreshes

        /// Appends one quad as two triangles. Owns index bookkeeping and winding:
        /// vertices are given in the order (v00, v01, v11, v10) walking the quad
        /// perimeter; triangles are emitted clockwise when viewed against the quad's
        /// outward normal (Unity front face). One colour per quad, replicated to its
        /// four vertices (flat shading).
        public void AppendQuad(
            Vector3 v00, Vector3 v01, Vector3 v11, Vector3 v10, Color32 color);
    }
}
```

Contract clauses:

- **Roadmap deviation, declared:** the roadmap sketch says "flat vertex / index / colour
  *struct*". `MeshBuffers` is a sealed **class**: it holds growable lists and is mutated
  across an append loop; a mutable struct with reference-type fields gives copy semantics
  on the shell and aliasing on the contents — the exact combination that produces silent
  bugs. "Flat" survives as the data shape (three parallel/flat buffers, no nesting), which
  is what the roadmap was actually specifying.
- **Emitters never touch `UnityEngine.Mesh`.** The facade (Layer 2) owns the upload:
  `SetVertices` / `SetColors` / `SetTriangles`, and selects
  `IndexFormat.UInt32` when `Vertices.Count > 65535`, else `UInt16`. Emitters therefore
  stay pure and testable without a scene.
- **Determinism.** Append order is row-major cell order (`index = x + y * Width`),
  matching `MapDataExport` and the T2.0 sampler. Same export + same params ⇒ bit-identical
  buffer contents, asserted by tests comparing two independent emissions element-wise.
- **`AppendQuad` is the only index writer** in T2.1. If a later emitter needs non-quad
  primitives (T2.3 corner triangles), it adds a sibling `AppendTriangle` under the same
  winding rule rather than writing indices inline.
- **Coordinate convention** is inherited from `Phase_T1_Design.md` §2.3: XZ plane, height
  along Y. Already honoured by the T2.0 gizmo host.
- Normals and UVs are **out of the v1 contract**. Flat-shaded vertex colours need neither
  (`Mesh.RecalculateNormals` at upload if a lit material ever requires it). Adding buffer
  fields later is additive.

---

## 3. Emitter seam (Layers 1–2)

- **Layer 1 — emitters.** Pure static functions, one per style:
  `static MeshBuffers Emit(<sampled input>, <colour input>, in <StyleParams> p)` — or an
  equivalent `void Emit(..., MeshBuffers target)` overload for host-side buffer reuse.
  The stepped emitter (T2.1) consumes the T2.0 sampler's integer levels and
  `LevelToWorldY`; the smooth emitter (T2.2) consumes continuous heights per
  `Phase_T1_Design.md`. Colour input is the per-cell `Color32` resolved via
  `LayerPriorityResolver` + the relief colour entry table (§1.5).
- **Layer 2 — facade.** One entry point with a style selector enum
  (`ReliefStyle { Stepped, Smooth, Corner, Voxel }`, members added as slices land). Owns
  the null/missing-field guards once, the upload, and the `IndexFormat` decision. The
  hosts talk only to the facade.
- **Layer 3 — hosts.** The mesh host (`[ExecuteAlways]`, `IMapContextSource` pull-based
  refresh per V-DD-9, same pattern as `PCGRuntimeOverlay` and the T2.0 gizmo host) is one
  component. **Wireframe is a renderer, not a fifth style** (roadmap decision, must not be
  redone): in T2.1 the existing `PCGSteppedReliefGizmoPreview` is retargeted to draw
  `MeshBuffers` edges as gizmo lines, which makes it work for every current and future
  emitter unchanged.

---

## 4. Slice plan

### T2.1 — `MeshBuffers` + stepped emitter + mesh host

Step 0 (mechanical, gated green before step 1): create the two asmdefs (§1.2, §1.5); move
the three T2.0 files (§1.3, §1.4); namespace renames; test-asmdef references. Then:
`MeshBuffers` (§2) with unit tests; `LayerPriorityResolver` extraction + `TilemapAdapter2D`
delegation (§1.5); relief colour entry type; stepped emitter over the T2.0 sampler; facade
(stepped only); mesh host; gizmo host retargeted to `MeshBuffers` edges.

DoD: full EditMode suite green; every existing golden unchanged; emitter determinism test
(two emissions, element-wise identical); `TilemapAdapter2D` behaviour bit-identical under
its existing tests; visual smoke test per protocol (baseline sanity, stepped mesh vs gizmo
preview cross-check, seed variation, console hash log).

### T2.2 — Smooth emitter

`Phase_T1_Design.md` is the specification (absorbed per §1.1); its adapter-packaging
sections are superseded by §1.2 of this document. **Open decision, deliberately not
resolved here:** whether T1's continuous `heightScale` coexists with `stepWorldHeight` or
one derives from the other. It becomes real only when this emitter exists; resolving it
now would be designing against a consumer that hasn't been written. Recorded in
`PCG_Roadmap.md` §Phase T2 (height-parameterization decision, "still open" clause).

### T2.3 — Corner-height emitter: posture on the research model

The corner-height model (four corner heights per tile, a closed legal-slope set, the
"adjacent corners differ by at most one step" constraint, per-tile water) is documented in
`Corner-Height_Terrain_in_RCT__Transport_Tycoon__OpenTTD_and_OpenRCT2__Data_Model__Legal_Slopes_and_Rendering.md`
— **external comparative evidence, authority tier 7, and it stays that way for now.**

**This document deliberately does not reaffirm the model as a package decision.** The
reaffirmation — restating the legal-slope set, the corner constraint, the clamp policy
(smooth vs. wall-quad) and the per-tile water rule as this package's own gated decisions,
with the artifact cited as source — is the opening act of the batch that starts T2.3, in a
§T2.3 amendment to this document. Until then, T2.3 has a source but no authority. Reason:
reaffirming now would freeze constraint details (slope-set contents, clamp order) that
nothing yet consumes, ahead of the emitter seam they must fit — exactly the pattern the
rule against designing for unwritten consumers exists to prevent.

### T2.4 — Voxel emitter

Speculative, not scheduled, unchanged from the roadmap. Reuses Layer 0's integer levels
extruded to a floor; greedy meshing and chunking are optimizations, not requirements. No
further design here.

---

## 5. Invariants and non-goals

- Grid-first, deterministic, adapters-last. The branch is pure output: no stage, no
  `MapLayerId` / `MapFieldId`, no tunable, no preset field, no core file touched, in any
  slice. Golden-neutral by construction; the full golden suite is the gate on every slice.
- No `UnityEngine.Random` anywhere in the branch (nothing here randomizes at all).
- Structural limit, carried from the research: a heightfield admits one surface per
  column. Caves, arches and overhangs are out of scope for T2.1–T2.3; if they become a
  requirement, T2.4 or a multi-layer model is the path, not a T2.3 extension.
- This document is planning. Implementation truth lands in `CURRENT_STATE.md` and
  `changelog-ssot.md` when slices close; contracts (if any type here ever hardens into
  one) land in `SSoT_CONTRACTS.md` by explicit promotion, never by drift.
