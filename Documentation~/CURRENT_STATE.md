# Current State

Status date: 2026-04-01

## What is active now
- The Islands documentation migration was handled as Tier L and is now materially closed for the reviewed snapshot corpus.
- The old documentation tree is kept as a fixed snapshot under `Documentation-snapshot/`.
- The new governed documentation root is `Documentation~/`.
- The promoted subsystem authority surfaces remain the PCG core and the implemented Map Pipeline by Layers slice.

## What is implemented now (confirmed for documentation authority purposes)
- New PCG runtime direction: grid-first, deterministic, adapters-last.
- Map Pipeline by Layers implemented slice: F0–F3.
- Layout strategies are an implemented, test-gated support surface under PCG.
- GraphLibrary runtime is a real implemented surface, but it is **not** promoted subsystem authority.
- Noise runtime is real and coherent, but it is currently a governed reference / staged support surface, not a promoted subsystem SSoT.
- Mesh runtime is real and coherent, but it is currently a governed reference / staged support surface, not a promoted subsystem SSoT.
- Surfaces runtime contains real jobs, but the surface layer remains mixed with sample orchestration and is currently governed reference / staged support, not a promoted subsystem SSoT.
- Shader assets and HLSL helpers are active support artifacts, but not a promoted subsystem SSoT.
- F4–F6 are not implementation truth yet.

## What current package development just resolved
- F3 — Hills + topology is now implemented and test-gated as part of the active Map Pipeline by Layers slice.
- The map slice now includes topology layer outputs for `LandEdge` and `LandInterior`.
- The active runtime slice now includes `MaskTopologyOps2D`, `MapNoiseBridge2D`, and `Stage_Hills2D`.
- F3 stage-level and pipeline-level golden gates now exist alongside the earlier F2 gates.
- The map lantern can now inspect hills/topology layers for runtime smoke testing, while remaining sample-side support rather than subsystem authority.

## What Batch 3 resolved
- The old tilemap-based Island Map Generation document does not describe active Islands runtime truth.
- That legacy system is treated as an external conceptual / architectural reference.
- The early `MapPipelineByLayers` roadmap is no longer an active roadmap surface; it is absorbed into the governed PCG roadmap and preserved only for planning traceability.

## What Batch 4 resolved
- Layout strategies do not currently deserve a separate subsystem SSoT.
- Their correct role is governed deep reference / staged subsystem support under PCG.
- The old `Islands_PCG_Layout_Strategies_SSoT_v0_1_2.md` should stop acting as an active SSoT through filename authority alone.

## What Batch 5 resolved
- GraphLibrary has a real runtime boundary, but it does not currently deserve promotion to `systems/graphs-ssot.md`.
- `reference/graphs.md` remains the governed reference-facing surface for GraphLibrary.
- `reference/GraphLibrary_Pipeline_Technical_Doc.md` remains historical technical support / deep-support, not active law.
- `DirectedGraphExample.cs` should not be treated as canonical usage truth.

## What Batch 6 resolved
- Noise should remain a governed reference / staged support surface, not a new subsystem SSoT.
- Meshes should remain a governed reference / staged support surface, not a new subsystem SSoT.
- Surfaces should remain a governed reference / staged support surface, not a new subsystem SSoT.
- Shaders should remain governed reference / support only, not a new subsystem SSoT.
- The legacy public-facing module docs for overview, noise, mesh, surfaces, and shaders now have governed homes under `reference/`.
- `Samples~/0.1.0-preview/ProceduralSurface.cs` should be treated as sample orchestration support, not runtime authority.

## What Batch 7 resolved
- Repo-wide cross-links across the governed spine were normalized to the correct governed homes.
- Missing snapshot/source-file status headers were applied for the main authority-risk legacy files.
- `planning/archive/Islands_PCG_MapPipelineByLayers_Roadmap_v0.1.0_2026-01-29.md` is now landed as a governed archive destination instead of a merely declared future path.
- GraphLibrary historical technical support now lives only in `Documentation~/reference/GraphLibrary_Pipeline_Technical_Doc.md`; the old runtime-local markdown copy has been removed from the package tree.
- The remaining work is now bounded cleanup rather than unresolved promotion triage.

## What this final curation pass resolved
- `planning/archive/` now holds the closed migration roadmap plus the main historical planning sources that still matter for traceability.
- `archive/` now holds the key historical technical support docs that were repeatedly referenced during migration but do not deserve active authority status.
- `research/` remains intentionally empty aside from its README because no reviewed snapshot document qualified as active exploratory research rather than archive or governed reference.
- `archive/snapshot-curation-register.md` records the disposition of every file in the reviewed snapshot corpus.

## What is not settled yet
- No unresolved migration batch remains for the reviewed snapshot corpus.
- Future cleanup, if any, should be treated as ordinary documentation maintenance tied to new development work.

## Immediate next focus
Return to package development through the active technical roadmap in `planning/active/PCG_Roadmap.md`, with `F4` as the next planned implementation slice.

## Why Batch 7 closed the current hardening pass
Batch 2 established active PCG authority.  
Batch 3 removed the main legacy map-generation ambiguity.  
Batch 4 resolved layout strategies as staged support rather than separate subsystem authority.  
Batch 5 resolved GraphLibrary as staged support / governed reference rather than subsystem authority.  
Batch 6 hardened Noise / Meshes / Surfaces / Shaders and normalized their governed reference homes.  
Batch 7 completed the remaining repo-wide normalization and traceability hardening for the reviewed evidence set.
