
# Surface Deformation & Flow System

> **Version** 0.1.0 · generated 2025-06-09

This document explains the **surface layer** that bridges the Mesh generators and the Shader‑Graphs:

```
Generator → MeshJob → SurfaceJob ─┐
                                  ├─→ MeshRenderer → Shader Graph (“Displacement”)
                    FlowJob ──────┘
```

* `SurfaceJob<N>` — CPU displaces vertices with fractal noise and rebuilds normals/tangents.  
* `FlowJob<N>`    — Updates a `ParticleSystem` so its particles “flow” across that same noise field.  
* `ProceduralSurface` — MonoBehaviour that wires everything together and sets shader keywords.

---

## 1 · `SurfaceJob<N>` – vertex deformation

| Behaviour | Details |
|-----------|---------|
| **Noise sampling** | Gets 4‑wide `Sample4` via `GetFractalNoise<N>` fileciteturn10file8 |
| **Plane / Sphere** | Boolean switch chooses height‑field or radial math |
| **Normal / tangent rebuild** | Plane: `normalize(−dx,1,−dz)`; Sphere: derivative projection then renormalise fileciteturn10file8 |
| **Stream constraint** | Works only with `SingleStream.Stream0`; vertices processed in blocks of 4 fileciteturn10file1 |
| **Vectorisation** | Mesh vertex count padded to multiple of 4 by `MeshJob` |

### API

```csharp
JobHandle SurfaceJob<N>.ScheduleParallel(
    Mesh.MeshData meshData,
    int           resolution,
    Settings      noiseSettings,
    SpaceTRS      domain,
    float         displacement,
    bool          isPlane,
    JobHandle     dependency);
```

---

## 2 · `FlowJob<N>` – particle advection

*Batch of 4 particles processed per job iteration.*

| Mode | Velocity |
|------|----------|
| **Curl** | v = ∇ × noise |
| **Downhill** | v = −∇ noise |

Plane logic uses `dx/dz`; Sphere logic projects onto tangent plane fileciteturn10file6.

---

## 3 · `ProceduralSurface` – the orchestrator

1. Schedules **MeshJob** for chosen generator.  
2. Pipes result through **SurfaceJob** (see lookup table) fileciteturn10file2.  
3. Toggles `_IsPlane` on the Displacement material fileciteturn10file16.  
4. Schedules **FlowJob** on each `OnParticleUpdateJobScheduled` fileciteturn10file15.

---

## 4 · Noise template matrix (excerpt)

| Enum entry | 1‑D | 2‑D | 3‑D |
|------------|-----|-----|-----|
| Perlin | `Lattice1D<...,Perlin>` | `Lattice2D<…>` | `Lattice3D<…>` |
| Simplex | `Simplex1D<Simplex>` | `Simplex2D<…>` | `Simplex3D<…>` |
| Voronoi Worley F1 | `Voronoi1D<…,Worley,F1>` | … | … |

*(See `ProceduralSurface.surfaceJobs` for all rows.)*

---

## 5 · Shader handshake

| Property | Set by | Purpose |
|----------|--------|---------|
| `_IsPlane` float | `ProceduralSurface` | Displacement graph switch between planar and radial logic |
| Vertex attrs | `SurfaceJob` | Already displaced; normals & tangents rebuilt |
| `NoiseGPU.hlsl` | Shader Graph | Must use same settings & `SpaceTRS` as CPU |

Full Shader Graph property reference lives in [docs/shaders.md](shaders.md).

---

## 6 · Best practices

* **Plane**: use *SharedSquareGrid* with `_IsPlane=1`.  
* **Sphere**: use *SharedCubeSphere* → smooth topology.  
* **Particles**: capacity multiple of 4, enable Burst in ParticleSystem job settings.  
* **LOD**: Combine CPU displacement (near) with GPU-only shader displacement (far).

---

## 7 · Extending

1. Add new `INoise` composition.  
2. Append schedule delegates to `surfaceJobs` & `flowJobs`.  
3. Keep `_IsPlane` semantic if you write a new material.

---

## 8 · Changelog

* **0.1.0** – First release.
