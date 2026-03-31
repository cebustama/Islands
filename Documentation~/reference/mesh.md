# Meshes — Governed Reference

Status: Active governed reference / staged support  
Primary implemented truth: `Runtime/Meshes/**`  
Promotion status: not promoted to subsystem SSoT after Batch 6.

## Purpose
This document is the governed reference-facing home for the procedural mesh surface used by Islands.
It summarizes the runtime boundary, the main contracts, and the correct authority role after Batch 6 normalization.

## What this document covers
- the current mesh runtime boundary
- the key contracts for generators, streams, and job scheduling
- the correct reference/support role of this surface

## What this document does not cover
- it does not promote Meshes into a standalone subsystem SSoT
- it does not replace runtime code as implementation truth
- it does not redefine the PCG core layering already governed elsewhere

## Implemented truth now

### Core contract surface
The current contract surface includes:
- `Runtime/Meshes/IMeshGenerator.cs`
- `Runtime/Meshes/IMeshStreams.cs`
- `Runtime/Meshes/MeshJob.cs`
- `Runtime/Meshes/Vertex.cs`
- `Runtime/Meshes/TriangleUInt16.cs`

### Generator families
Current generators live under:
- `Runtime/Meshes/Generators/`

Examples present in the current corpus include:
- grid variants
- polyhedra
- sphere variants
- shared-vertex sphere/grid variants

### Stream families
Current stream implementations live under:
- `Runtime/Meshes/Streams/`

## Practical contract summary

### `IMeshGenerator`
`IMeshGenerator` defines the runtime contract for generators:
- `VertexCount`
- `IndexCount`
- `JobLength`
- `Bounds`
- `Resolution`
- `Execute<S>(...)`

### `IMeshStreams`
`IMeshStreams` defines how generated data is written into mesh buffers:
- `Setup(...)`
- `SetVertex(...)`
- `SetTriangle(...)`

### `MeshJob<G,S>`
`MeshJob<G,S>` is the main scheduled generation path that ties a generator and a stream implementation together.

## Current role
Meshes are currently best understood as:
- reusable runtime support infrastructure
- adapter-facing output infrastructure
- governed reference / staged support rather than promoted subsystem authority

This is consistent with the current promoted PCG architecture, which remains:
- grid-first
- deterministic
- adapters-last

Meshes are important, but they are not currently the primary promoted center of documentary authority.

## Relationship to the older snapshot doc
The old snapshot-era `mesh.md` contained useful:
- stream descriptions
- usage framing
- generator catalogues
- practical authoring notes

That material was good reference material, but not a reason to create a new subsystem SSoT.

## Not promoted at present
Batch 6 did **not** justify `systems/meshes-ssot.md`.

The main reasons were:
- the current promoted spine already defines core architectural truth elsewhere
- Meshes currently behave more like reusable support infrastructure and adapters than a separately promoted subsystem
- reference clarity was needed more than promotion

## Cross-links
- promoted subsystem authority: `systems/pcg-core-ssot.md`
- promoted map slice authority: `systems/map-pipeline-by-layers-ssot.md`
- Batch 6 overview: `reference/overview.md`
- shader support surface: `reference/shaders.md`
- surface deformation / flow reference: `reference/surfaces.md`
