# Islands — Support Surface Overview

Status: Active governed reference  
Role: Navigation and authority-boundary summary for the support surfaces normalized in Batch 6.  
Does not govern: subsystem authority, planning, or historical source classification by itself.

## Purpose
This document provides a governed overview of the main support surfaces that exist around the promoted PCG core:
- Noise
- Meshes
- Surfaces
- Shaders

It exists to replace the old snapshot-era public module overview with a role-correct reference surface inside `Documentation~/reference/`.

## Batch 6 resolution in one place
- **Noise** is a real runtime support surface, but it is not promoted subsystem authority.
- **Meshes** are a real runtime support surface, but they are not promoted subsystem authority.
- **Surfaces** contain real runtime jobs, but the broader surface story is still mixed with sample orchestration and is not promoted subsystem authority.
- **Shaders** are active support artifacts and governed reference material, not a promoted subsystem authority surface.

## Current promoted authority that these surfaces support
- `systems/pcg-core-ssot.md`
- `systems/map-pipeline-by-layers-ssot.md`

Those two files remain the promoted subsystem authorities for the current migration stage.

## Surface summary

### Noise
**Implemented truth**
- `Runtime/Noise/Noise/Noise.cs`
- supporting implementations in `Runtime/Noise/Noise/*.cs`
- support types such as `Runtime/Noise/Shapes.cs` and `Runtime/Noise/Visualization.cs`

**Role**
- Burst/SIMD noise generation support infrastructure
- reusable runtime support surface
- governed home: `reference/noise.md`

### Meshes
**Implemented truth**
- `Runtime/Meshes/IMeshGenerator.cs`
- `Runtime/Meshes/IMeshStreams.cs`
- `Runtime/Meshes/MeshJob.cs`
- generator and stream families under `Runtime/Meshes/Generators/` and `Runtime/Meshes/Streams/`

**Role**
- procedural mesh generation support infrastructure
- reusable runtime support surface
- governed home: `reference/mesh.md`

### Surfaces
**Implemented truth**
- `Runtime/Surfaces/SurfaceJob.cs`
- `Runtime/Surfaces/FlowJob.cs`

**Important non-authoritative support**
- `Samples~/0.1.0-preview/ProceduralSurface.cs`

**Role**
- runtime deformation / flow jobs plus sample orchestration support
- governed home: `reference/surfaces.md`

### Shaders
**Implemented truth / support artifacts**
- `Runtime/Shaders/HLSL/*`
- `Runtime/Shaders/ShaderGraph/*`

**Role**
- visual support assets and helper code
- governed home: `reference/shaders.md`

## Why there are no new subsystem SSoTs here
Batch 6 explicitly rejected promotion-by-tone.
These surfaces are useful and real, but the current governed architecture is still centered on:
- grid-first PCG core truth,
- Map Pipeline by Layers F0–F2 as the current promoted map slice,
- and support layers remaining support layers until a stronger subsystem boundary is earned.

## Historical source relationship
This document supersedes the snapshot-era `overview.md` as the governed reference-facing overview.
The snapshot file remains useful for traceability and salvage, but it should now be treated as historical-support rather than live authority.
