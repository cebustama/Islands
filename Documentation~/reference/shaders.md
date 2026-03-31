# Shaders — Governed Reference

Status: Active governed reference / support  
Primary implemented support artifacts: `Runtime/Shaders/**`  
Promotion status: not promoted to subsystem SSoT after Batch 6.

## Purpose
This document is the governed reference-facing home for the shader and HLSL support surface used by Islands.

## What this document covers
- current shader support artifacts
- the HLSL helper layer
- the correct documentary role of shaders after Batch 6

## What this document does not cover
- it does not promote shaders into a standalone subsystem authority surface
- it does not replace the assets as implementation truth
- it does not define the primary PCG architecture

## Implemented support artifacts now
The current shader support surface includes:
- `Runtime/Shaders/HLSL/NoiseGPU.hlsl`
- `Runtime/Shaders/HLSL/ProceduralMesh.hlsl`
- shader graph assets under `Runtime/Shaders/ShaderGraph/`

## Practical support summary

### HLSL helpers
`NoiseGPU.hlsl` provides helper-side shader support for GPU-side noise / buffer usage.
`ProceduralMesh.hlsl` provides the ripple custom-function support used by procedural mesh examples.

### Shader graph assets
The current corpus includes graph assets such as:
- `Cube Map.shadergraph`
- `Displacement.shadergraph`
- `Procedural Mesh.shadergraph`

These are meaningful support assets, but they do not by themselves justify a new subsystem SSoT.

## Why this surface was not promoted
Batch 6 concluded that Shaders are:
- real support artifacts
- useful reference material
- but not a promoted subsystem authority surface

Main reasons:
- there is no comparably strong promoted documentary boundary here like the one already earned by PCG core or the implemented map slice
- much of the value is catalog/reference-oriented rather than subsystem-law oriented
- the older doc behaved more like a public-facing module catalogue than an authority document

## Relationship to the older snapshot doc
The snapshot-era `shaders.md` contained useful:
- property tables
- graph catalogue framing
- HLSL helper descriptions
- performance / usage notes

That material was worth preserving as governed reference, not promoting as standalone subsystem authority.

## Cross-links
- Batch 6 overview: `reference/overview.md`
- mesh support surface: `reference/mesh.md`
- surface support surface: `reference/surfaces.md`
