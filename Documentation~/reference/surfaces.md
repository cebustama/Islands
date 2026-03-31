# Surfaces — Governed Reference

Status: Active governed reference / staged support  
Primary implemented truth: `Runtime/Surfaces/*.cs`  
Important sample support: `Samples~/0.1.0-preview/ProceduralSurface.cs`  
Promotion status: not promoted to subsystem SSoT after Batch 6.

## Purpose
This document is the governed reference-facing home for the current surface layer used by Islands.
It exists to clarify what is actually implemented in runtime, what is sample orchestration, and why the surface layer was **not** promoted to subsystem authority in Batch 6.

## What this document covers
- the current runtime jobs in the surface layer
- the sample orchestration boundary
- the shader handshake at a high level
- the correct documentary role of this surface

## What this document does not cover
- it does not treat the sample orchestration layer as runtime authority
- it does not promote Surfaces into a standalone subsystem SSoT
- it does not replace runtime code as canonical implementation truth

## Implemented truth now

### Runtime jobs
The current runtime job surface is narrow but real:
- `Runtime/Surfaces/SurfaceJob.cs`
- `Runtime/Surfaces/FlowJob.cs`

### Sample orchestration
The broader orchestration layer currently sits in sample code:
- `Samples~/0.1.0-preview/ProceduralSurface.cs`

That file is useful for understanding how the jobs, mesh generation, and shader properties were wired together in sample usage, but it should **not** be treated as active subsystem authority.

## Practical runtime summary

### `SurfaceJob<N>`
`SurfaceJob<N>` is the current deformation job.
At a high level it:
- samples fractal noise
- displaces vertices
- rebuilds normals / tangents
- works with a constrained mesh stream path rather than a broad generic surface abstraction

### `FlowJob<N>`
`FlowJob<N>` is the current particle flow support job.
At a high level it:
- samples the same style of noise field
- updates particle motion across that field
- remains a support-layer job, not a promoted subsystem boundary

## Why this surface was not promoted
Batch 6 found that the surface story is still too mixed to justify `systems/surfaces-ssot.md`.

Main reasons:
- the truly implemented runtime surface is narrow: mainly `SurfaceJob` and `FlowJob`
- the broader surface workflow described in older docs relies heavily on `ProceduralSurface.cs`, which in this corpus is sample orchestration
- promoting the whole surface layer would overstate the clarity and stability of the current subsystem boundary

## Shader handshake
The old surface doc was still useful in one way: it identified the bridge between:
- mesh generation
- CPU deformation jobs
- shader property wiring
- optional particle-flow support

That bridge remains a valid reference concern, but it belongs in governed reference, not promoted subsystem authority.

## Relationship to the older snapshot doc
The snapshot-era `surfaces.md` was the most over-claimed Batch 6 source.
It mixed:
- runtime jobs
- sample orchestration
- usage guidance
- shader handshake
- and extension guidance

The right salvage result was **not** to throw it away, but to normalize it into a reference doc with clearer authority boundaries.

## Cross-links
- Batch 6 overview: `reference/overview.md`
- mesh support surface: `reference/mesh.md`
- shader support surface: `reference/shaders.md`
