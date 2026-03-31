# Noise — Governed Reference

Status: Active governed reference / staged support  
Primary implemented truth: `Runtime/Noise/**`  
Promotion status: not promoted to subsystem SSoT after Batch 6.

## Purpose
This document is the governed reference-facing home for the Islands noise surface.
It summarizes the current runtime boundary, the key contracts, and the correct authority role without overstating subsystem promotion.

## What this document covers
- the current runtime boundary
- the main contracts exposed by the noise surface
- the practical role of the noise surface inside Islands
- what is reference/support versus what is active implementation truth

## What this document does not cover
- it does not promote Noise into a new subsystem SSoT
- it does not replace runtime code as the canonical implementation truth
- it does not define future PCG sequencing

## Implemented truth now

### Core contract surface
The main contract surface lives in:
- `Runtime/Noise/Noise/Noise.cs`

That file defines:
- `Noise.Settings`
- `Noise.INoise`
- `Noise.GetFractalNoise<N>(...)`
- `Noise.Job<N>`
- `Noise.ScheduleDelegate`

### Supporting implementation families
The current implementation families live under:
- `Runtime/Noise/Noise/Noise.Gradient.cs`
- `Runtime/Noise/Noise/Noise.Lattice.cs`
- `Runtime/Noise/Noise/Noise.Sample.cs`
- `Runtime/Noise/Noise/Noise.Simplex.cs`
- `Runtime/Noise/Noise/Noise.Voronoi.cs`
- `Runtime/Noise/Noise/Noise.Voronoi.Distance.cs`
- `Runtime/Noise/Noise/Noise.Voronoi.Function.cs`

### Support utilities / visualization
Additional supporting files include:
- `Runtime/Noise/SmallXXHash.cs`
- `Runtime/Noise/Shapes.cs`
- `Runtime/Noise/Visualization.cs`
- `Runtime/Noise/NoiseBurstExplicitCompilation.cs`

## Current role
Noise is currently best understood as:
- reusable Burst/SIMD runtime support infrastructure
- staging/support for higher-level PCG work
- governed reference rather than promoted subsystem authority

This means the code is real and important, but the documentary role remains deliberately conservative.

## Practical contract summary

### Settings
`Noise.Settings` currently carries the main runtime tuning payload:
- `seed`
- `frequency`
- `octaves`
- `lacunarity`
- `persistence`

### Sampling
`Noise.GetFractalNoise<N>(...)` is the key generic entry point for fractal sampling over an `INoise` implementation.

### Job path
`Noise.Job<N>` provides the main scheduled sampling path using:
- `NativeArray<float3x4>` inputs
- `NativeArray<float4>` outputs
- a `SpaceTRS` domain transform
- scheduled job execution through `ScheduleParallel`

## Reference/support notes
The old snapshot-era `noise.md` contained useful explanatory material such as:
- quick-start framing
- architecture overview
- visualization notes
- performance notes
- terminology and examples

That material was worth salvaging, but it should now live under governed reference rather than act as implicit package authority.

## Not promoted at present
Batch 6 did **not** justify `systems/noise-ssot.md`.

The main reasons were:
- the current promoted documentary spine is still centered on PCG core + Map Pipeline by Layers F0–F2
- Noise is real, but it currently behaves more like support infrastructure than a primary promoted subsystem authority
- planning references to Noise do not by themselves earn a new subsystem SSoT

## Cross-links
- promoted subsystem authority: `systems/pcg-core-ssot.md`
- promoted map slice authority: `systems/map-pipeline-by-layers-ssot.md`
- planning only: `planning/active/PCG_Roadmap.md`
- Batch 6 overview: `reference/overview.md`
