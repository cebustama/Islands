
# Shader Graph & HLSL Reference

> **Version** 0.1.0 · generated 2025-06-09

This document catalogues the **Shader Graph assets** and **HLSL custom functions** used by the *Islands* pipeline.

* Core idea: **CPU SurfaceJob** pre‑warps vertices while **Shader Graphs** add smaller, GPU‑friendly tweaks (normal mapping, ripples, cubemap lookup).  
* All graphs live in **Assets/Shaders/** and share common helper code from ***NoiseGPU.hlsl*** and ***ProceduralMesh.hlsl***.

---

## 1 · Displacement (vertex stage)

| Property | Type | Default | Notes |
|----------|------|---------|-------|
| **`_DisplacementScale`** | Float | `1` | Multiplies incoming height in object‑space Y **(plane)** or radial offset **(sphere)** |
| **`_IsPlane`** | Boolean (float 0/1) | `0` | Set by `ProceduralSurface`; toggles plane vs sphere branch |
| (Vertex inputs) | Position / Normal / Tangent | — | Must already be displaced & rebuilt by **SurfaceJob** |

**Custom Function Blocks**

*None – pure Graph logic:*  
`abs(sin(height)) → saturate → multiply` chain visible in screenshot ①.

---

## 2 · Procedural Mesh (ripple demo)

| Property | Type | Default | |
|----------|------|---------|--|
| **`_BaseMap`** | Texture2D | — | Albedo |
| **`_NormalMap`** | Texture2D | — | Tangent‑space |
| **`_RippleOrigin`** | Vector4 | `(0,0,0,0)` | XYZ = world origin, W unused |
| **`_RipplePeriod`** | Float | `1` |
| **`_RippleSpeed`** | Float | `1` |
| **`_RippleAmplitude`** | Float | `0.1` |

Uses **`Ripple_float`** function from `ProceduralMesh.hlsl`, which outputs **Position**, **Normal**, and **Tangent**:

```hlsl
void Ripple_float (
    float3 PositionIn, float3 Origin,
    float Period, float Speed, float Amplitude,
    out float3 PositionOut, out float3 NormalOut, out float3 TangentOut );
```

> *Shader Graph chains three “Ripple (Custom Function)” nodes to layer wave octaves (see screenshot ②).*

---

## 3 · Cube Map material

| Property | Type | Notes |
|----------|------|-------|
| **`_BaseMap`** | CubeMap | Albedo cube |
| **`_NormalMap`** | CubeMap | Object‑space normal cube |

Custom Function **`CustomCubemap_float`**:

```hlsl
float3 CustomCubemap (Cube cube, float3 dir);
```

Implemented in `NoiseGPU.hlsl` using `UNITY_SAMPLE_TEXCUBE_LOD` and `DecodeHDR`.  
Combination of **Multiply** and **Subtract** nodes blends two cubemap samples (screenshot ③).

---

## 4 · Shared HLSL helpers

### 4.1 `NoiseGPU.hlsl` (45 lines)

* **Instancing hook** – rewrites `unity_ObjectToWorld` for GPU instanced procedural meshes.  
* `_Noise` structured buffer (float) matches CPU `Sample4.v`.  
* `ConfigureProcedural()` called from `ShaderGraph` generated code when `UNITY_PROCEDURAL_INSTANCING_ENABLED` is defined.

### 4.2 `ProceduralMesh.hlsl` (16 lines)

Contains the **Ripple_float** definition shown above.  
Key math:

```hlsl
float f = 2π * Period * (d - Speed * _Time.y);
PositionOut.y += Amplitude * sin(f);
derivatives = (2π * Amplitude * Period * cos(f) / max(d,0.0001)) * p.xz;
```

Tangents and normals are rebuilt analytically so they stay coherent with vertex displacement.

---

## 5 · Performance / SRP batcher notes

* Graphs use **Object‑space** positions so SRP batcher remains enabled.  
* All custom functions are declared `static inline` → no additional variants.  
* When *SurfaceJob* already outputs correct normals, keep Shader Graph **Normal Vector** node in *Object* space to avoid double transformation.

---

## 6 · Link map

| CPU doc page | Where to link to **shaders.md** |
|--------------|---------------------------------|
| `noise.md` (end of *Derivatives in Practice*) | “→ *See* **docs/shaders.md §4** for the GPU counterpart that samples the same noise in HLSL.” |
| `mesh.md` (after *Switching streams in practice*) | “→ Materials are described in **docs/shaders.md**.” |
| `surfaces.md` (end of *Shader handshake* section) | “→ Shader property reference lives in **docs/shaders.md**.” |

Add a short “Next → Shader Graphs” arrow in each spot.

---

## 7 · Changelog

* **0.1.0** – First publication of shader/HLSL reference.
