# Phase T1 — PCG Map Mesh Visualization: Design Specification

Status: Planning (not implementation authority)  
Authority: Design reference for Phase T1 implementation. Supplements the Phase T1 entry
in `PCG_Roadmap.md` with detailed architectural design.  
Depends on: Phase H2 (MapDataExport), Phase H3 (MapGenerationPreset). No unresolved
upstream dependencies — all requirements are already implemented and test-gated.

---

## 1. Phase Intent

Phase T1 adds a 3D mesh visualization adapter that reads the same `MapDataExport` as the
tilemap adapter and produces a Unity `Mesh` with height-displaced vertices and per-layer
vertex colors. This provides a terrain-preview companion to the existing 2D tilemap view,
enabling rapid side-by-side comparison of the procedural overworld and its 3D terrain
interpretation.

**What this enables:** Tweak a seed or tunable in the Inspector, see the tilemap update
flat and the mesh update as a 3D surface in the same frame. The mesh is not a gameplay
surface — it is a design-iteration tool, the same role as `PCGMapTilemapVisualization`
but for 3D terrain preview.

---

## 2. Resolved Design Decisions

### 2.1 Color Entry Type (Resolved)

**Decision: New `MeshColorEntry` struct in the mesh adapter asmdef.**

`ProceduralTileEntry` already maps `MapLayerId` → `Color`, but it lives in
`Islands.PCG.Adapters.Tilemap`. Referencing the tilemap asmdef from the mesh asmdef
creates an inter-adapter dependency that violates the adapters-last / adapter-independence
principle.

A shared extraction (`Islands.PCG.Adapters.Shared`) is premature for a 2-field struct.
If a third adapter appears, extraction becomes a mechanical refactor.

`MeshColorEntry` is a `[Serializable]` struct: `MapLayerId layerId` + `Color color`.
Identical shape to `ProceduralTileEntry`; independent ownership.

### 2.2 Material Strategy (Resolved)

**Decision: Inspector slot with programmatic fallback.**

- `PCGMapMeshVisualization` exposes a `Material material` Inspector field.
- When assigned, the adapter uses it directly on the `MeshRenderer`.
- When null, the adapter attempts `Shader.Find("Unlit/VertexColor")` and creates a
  runtime material from it.
  - If `Shader.Find` returns null (URP-only project without built-in shaders), log a
    clear warning: `"[PCGMapMeshVisualization] No material assigned and fallback shader
    'Unlit/VertexColor' not found. Assign a vertex-color material in the Inspector."`
  - The mesh is still generated and assigned to `MeshFilter`; only rendering is affected.
- The sample scene ships with a pre-assigned `VertexColorUnlit.mat` asset.

This mirrors the pattern of `ProceduralTileFactory`: create runtime assets when convenient,
fail gracefully with a clear message, always allow explicit override.

### 2.3 Coordinate Orientation (Resolved)

**Decision: Mesh in XZ plane, height in Y (3D standard).**

Map grid coordinate `(i, j)` maps to vertex position `(x = i * cellSize, y = Height[i,j] * heightScale, z = j * cellSize)`.

Rationale:
- Universal 3D terrain convention in Unity and elsewhere.
- Orbit cameras, scene navigation, and gizmos behave intuitively.
- The tilemap lives in the XY plane; the mesh lives in the XZ plane. They occupy
  different spatial orientations by design — the mesh is a companion view, not an
  overlay. Placement is handled by the GameObject's `Transform.position` in the scene.

`cellSize` defaults to 1.0 (1 Unity unit per grid cell). `heightScale` defaults to 10.0
(sensible for a 64×64 map with Height in [0, 1]).

---

## 3. Component Design

### 3.1 `MeshColorEntry`

```
[Serializable]
public struct MeshColorEntry
{
    public MapLayerId layerId;
    public Color color;
}
```

File: `Runtime/PCG/Adapters/Mesh/MeshColorEntry.cs`

### 3.2 `MeshAdapter2D` (Static Adapter)

**Responsibility:** Reads a `MapDataExport`, produces a `Mesh`.

```
public static class MeshAdapter2D
{
    public static Mesh Build(
        MapDataExport export,
        MeshColorEntry[] colorTable,
        Color fallbackColor,
        float heightScale = 10f,
        float cellSize = 1f);
}
```

**Vertex generation (row-major):**
```
for j in [0, height):
    for i in [0, width):
        float h = export.HasField(Height) ? export.GetValue(Height, i, j) : 0f
        vertices[i + j * width] = new Vector3(i * cellSize, h * heightScale, j * cellSize)
```

**Triangle generation (2 tris per quad, row-major):**
```
for j in [0, height - 1):
    for i in [0, width - 1):
        int v00 = i + j * width
        int v10 = (i+1) + j * width
        int v01 = i + (j+1) * width
        int v11 = (i+1) + (j+1) * width
        triangles += [v00, v01, v10, v10, v01, v11]   // CCW winding, Y-up
```

**Vertex color resolution:**
Same priority logic as `TilemapAdapter2D`: iterate `colorTable` low→high, last match
per cell wins. For each vertex `(i, j)`, scan layers in table order; if
`export.GetCell(entry.layerId, i, j)` is true, set color. If no match, use
`fallbackColor`.

**Normals:** `Mesh.RecalculateNormals()` after vertex/triangle assignment. No custom
normal calculation needed for a visualization tool.

**Mesh configuration:**
- `mesh.indexFormat = IndexFormat.UInt32` when vertex count > 65535 (resolution > 255).
- `mesh.RecalculateBounds()` after all assignments.

**Null guards:**
- `export == null` → `ArgumentNullException`.
- `colorTable == null` → treated as empty (fallback color everywhere).

**Determinism:** Same export + same colorTable + same parameters → identical mesh data.
Row-major scan with no collection-order dependencies.

### 3.3 `PCGMapMeshVisualization` (`[ExecuteAlways]` MonoBehaviour)

**Responsibility:** Runs the full pipeline on every Inspector change, calls
`MeshAdapter2D.Build`, assigns the result to a `MeshFilter`.

**Inspector fields (always visible):**

| Field | Type | Default | Notes |
|-------|------|---------|-------|
| `meshFilter` | `MeshFilter` | null | Target mesh filter |
| `preset` | `MapGenerationPreset` | null | Shared with tilemap viz |
| `material` | `Material` | null | Fallback to Unlit/VertexColor |
| `heightScale` | `float [0.1, 100]` | 10.0 | Vertex Y multiplier |
| `cellSize` | `float [0.1, 10]` | 1.0 | XZ spacing per grid cell |
| `colorTable` | `MeshColorEntry[]` | — | Layer→Color mapping |
| `fallbackColor` | `Color` | gray | Unmatched cells |

**Inspector fields (hidden when preset assigned):**
Same set as `PCGMapTilemapVisualization`: seed, resolution, stage toggles, island shape,
water & shore, terrain noise, height redistribution, height remap curve, clear before run.
Custom Editor hides these when `preset != null`.

**Lifecycle:**
- `OnEnable`: allocate stages, cache params.
- `OnValidate`: mark dirty.
- `Update` (editor only): if dirty or `ParamsChanged()`, run `Rebuild()`.
- `OnDisable` / `OnDestroy`: dispose `MapContext2D`.

**`Rebuild()` flow:**
1. Resolve effective params (preset ?? inline).
2. `EnsureContextAllocated(resolution)`.
3. Configure stages, build `MapInputs`.
4. `MapPipelineRunner2D.Run(ref ctx, in inputs, stages, clearLayers)`.
5. `MapDataExport export = MapExporter2D.Export(ctx)`.
6. `Mesh mesh = MeshAdapter2D.Build(export, colorTable, fallbackColor, heightScale, cellSize)`.
7. Assign to `meshFilter.sharedMesh` (editor) or `meshFilter.mesh` (play mode).
8. Resolve material: if `material != null`, assign; else fallback.
9. Console log: seed, resolution, stage flags, vertex count, triangle count.
10. `CacheParams(); dirty = false;`

**Dirty tracking:**
FNV-1a hash comparison, same pattern as `PCGMapTilemapVisualization`. Tracks all
pipeline params + `heightScale` + `cellSize` + `material` instance ID + color table hash.

**Context menu presets:**
- `[ContextMenu("Set Classic Colors")]` — same palette as tilemap Classic.
- `[ContextMenu("Set Prototyping Colors")]` — same palette as tilemap Prototyping.

### 3.4 `PCGMapMeshVisualizationEditor` (Custom Inspector)

Mirrors `PCGMapTilemapVisualizationEditor`: hides preset-controlled fields when preset
is assigned, shows HelpBox. Significantly simpler (no multi-layer, no overlay, no
heatmap sections).

File: `Editor/Inspectors/PCGMapMeshVisualizationEditor.cs`  
Namespace: `Islands.PCG.Editor`  
Asmdef: `Islands.PCG.Editor` (existing).

### 3.5 Assembly Definition

**New:** `Islands.PCG.Adapters.Mesh.asmdef`

References:
- `Islands.PCG.Runtime`
- `Islands.PCG.Samples.Shared` (for `MapGenerationPreset`)
- `Unity.Mathematics`

Does **not** reference `Islands.PCG.Adapters.Tilemap`. Independent adapter.

---

## 4. File Placement

| File | Destination | Action |
|------|-------------|--------|
| `MeshColorEntry.cs` | `Runtime/PCG/Adapters/Mesh/` | New |
| `MeshAdapter2D.cs` | `Runtime/PCG/Adapters/Mesh/` | New |
| `PCGMapMeshVisualization.cs` | `Runtime/PCG/Adapters/Mesh/` | New |
| `Islands.PCG.Adapters.Mesh.asmdef` | `Runtime/PCG/Adapters/Mesh/` | New |
| `PCGMapMeshVisualizationEditor.cs` | `Editor/Inspectors/` | New |
| `VertexColorUnlit.mat` | `Runtime/PCG/Samples/PCG Map Mesh/` | New |
| `PCG Map Mesh.unity` | `Runtime/PCG/Samples/PCG Map Mesh/` | New |

---

## 5. Scene Setup (Sample)

```
PCG Map Mesh (scene root)
├── Grid
│   ├── Tilemap (base)          ← existing tilemap viz pattern
│   ├── Tilemap (overlay)
│   └── Tilemap (collider)
├── PCGMapTilemapVisualization  ← existing, uses preset
├── MeshTerrain                 ← new GameObject
│   ├── MeshFilter
│   ├── MeshRenderer            ← VertexColorUnlit.mat
│   └── PCGMapMeshVisualization ← new, uses same preset
└── Main Camera (Perspective)   ← orbits the mesh; separate from 2D camera
```

Both visualization components reference the **same** `MapGenerationPreset` asset.
Changing seed or tunables in the preset updates both views simultaneously.

---

## 6. Invariants

| ID | Invariant |
|----|-----------|
| T1-1 | `MeshAdapter2D.Build` is deterministic: same export + same params → identical mesh |
| T1-2 | `MeshAdapter2D` is read-only on `MapDataExport` (adapters-last) |
| T1-3 | No new `MapLayerId` or `MapFieldId` |
| T1-4 | No runtime contract changes |
| T1-5 | Vertex count = `width * height` |
| T1-6 | Index count = `(width-1) * (height-1) * 6` |
| T1-7 | All vertex Y positions in `[0, heightScale]` when Height in `[0, 1]` |
| T1-8 | Vertex colors resolve by layer priority (last match wins), same as tilemap |
| T1-9 | Null export → `ArgumentNullException` |
| T1-10 | Missing Height field → flat mesh (all Y = 0) |

---

## 7. Test Plan

Tests live under the existing PCG test asmdef.

| Test | Asserts |
|------|---------|
| `Build_NullExport_Throws` | `ArgumentNullException` |
| `Build_EmptyExport_ProducesFlatMesh` | All vertex Y = 0, correct vertex/index counts |
| `Build_WithHeight_VertexYMatchesField` | Spot-check vertex positions against export values |
| `Build_NullColorTable_UsesFallback` | All vertex colors = fallbackColor |
| `Build_ColorPriority_LastMatchWins` | With overlapping layers, highest-priority color wins |
| `Build_Determinism` | Two calls with same inputs → byte-identical mesh data |
| `Build_LargeResolution_UsesUInt32` | Resolution 300 → `IndexFormat.UInt32` |
| `Build_VertexCount` | `mesh.vertexCount == width * height` |
| `Build_IndexCount` | `mesh.triangles.Length == (w-1)*(h-1)*6` |
| `Build_HeightScale` | Vertex Y = Height * heightScale for known values |
| `Build_CellSize` | Vertex X/Z spacing = cellSize |

---

## 8. Visual Smoke Test

### 8.1 Baseline: flat mesh with fallback color

- **Preset:** Default (seed=1, resolution=64, all stages off except base terrain).
- **heightScale:** 0 (force flat).
- **Color table:** Empty.
- **Expected:** A flat gray square in the Scene view. 64×64 = 4096 vertices.
- **Red flag:** No mesh visible, or mesh has non-zero Y displacement.

### 8.2 Height displacement

- **Preset:** seed=1, resolution=64, enableHillsStage=true.
- **heightScale:** 10.
- **Expected:** Visible terrain relief — island center higher, edges lower, hills as
  peaks. The mesh silhouette should roughly match the Height heatmap in the tilemap viz.
- **Red flag:** Flat mesh despite heightScale>0. Inverted terrain (ocean peaks, land
  valleys). Mesh extends in wrong axis.

### 8.3 Layer coloring

- **Color table:** Classic palette (DeepWater=dark blue, Land=green, Hills=brown, etc.).
- **Expected:** Vertex colors match the tilemap procedural colors for the same seed.
  Water areas blue, land green, hills brown/gray.
- **Red flag:** All one color. Colors don't match tilemap. Visible seams at layer
  boundaries (vertex colors should interpolate naturally across the mesh).

### 8.4 Seed variation

- **Seeds:** 1, 42, 999.
- **Expected:** Three visibly different terrain shapes with consistent coloring logic.
- **Red flag:** All three seeds produce identical meshes (determinism failure in the
  wrong direction — same mesh regardless of seed).

### 8.5 Preset sync

- **Both tilemap and mesh viz use the same preset.**
- **Change seed in preset.**
- **Expected:** Both views update simultaneously. Terrain shapes correspond.
- **Red flag:** One updates, the other doesn't. Or they show different terrain for
  the same seed.

---

## 9. Performance Notes

| Resolution | Vertices | Triangles | Expected Rebuild |
|------------|----------|-----------|-----------------|
| 64×64 | 4,096 | 7,938 | < 5 ms |
| 128×128 | 16,384 | 32,258 | < 15 ms |
| 256×256 | 65,536 | 130,050 | < 50 ms |
| 512×512 | 262,144 | 522,242 | ~200 ms (editor only) |

Rebuild is editor-only (`[ExecuteAlways]` + dirty tracking). No runtime hot path.
For resolutions above 256, mesh generation is not the bottleneck — the pipeline itself
dominates. Mesh caching (only regenerate mesh when export changes) is a future
optimization if needed.

---

## 10. Future Extensions (Not In Scope)

These are natural follow-ups but explicitly **not** part of T1:

- **T1b — UV mapping + texture:** Replace vertex colors with UV coordinates mapped to a
  texture atlas or splat map. Requires shader work.
- **T1c — LOD / chunking:** For large maps, generate mesh in chunks with LOD levels.
- **T1d — Collider mesh:** Generate a simplified collision mesh for 3D navigation.
- **T1e — Scalar field height source:** Toggle between Height and other scalar fields
  (CoastDist, FlowAccumulation) as the vertex displacement source.
- **T1f — Normal-based shading:** Custom normals for cliff-face detection, slope-based
  texturing.

---

## 11. Dependencies

| Dependency | Status | Required? |
|------------|--------|-----------|
| Phase H2 (MapDataExport) | Done | Yes |
| Phase H3 (MapGenerationPreset) | Done | Yes |
| Phase H2d (ProceduralTileEntry — pattern reference) | Done | No (pattern only) |
| Phase N4 (Noise Settings) | Next on mainline | No (independent track) |

Phase T1 is sequenced on the **adapter track**, parallel to the mainline pipeline track.
It does not block or depend on N4, Phase L, Phase M, or any future pipeline phase.
Future pipeline phases that add new layers/fields are automatically visible to T1 via
`MapDataExport` — the mesh adapter reads whatever the export contains.
