# Islands.PCG — PCG Core SSoT

Status: Active
Authority: Primary subsystem authority for the new Islands.PCG core.
Scope: Contracts and implemented core truth for the new grid-first Islands.PCG pipeline.
Out of scope: Legacy tilemap generators, per-phase planning, historical progress reports.

## Purpose
This document governs the active core contracts of the new Islands.PCG pipeline.
It defines the non-negotiable rules, subsystem layering, and the authoritative data-oriented model used by runtime PCG systems.

## Governs
- Grid-first PCG philosophy for the new pipeline
- Determinism rules
- Core layering boundaries
- Core data contracts for grid/field containers
- The rule that layout remains headless and adapters-last

## Does not govern
- Legacy tilemap map generation documents
- Roadmap sequencing or phase planning
- Sample/demo behavior except where needed to explain boundaries

## Non-negotiables
- Same inputs + same seed => same outputs
- No `UnityEngine.Random` in core runtime code
- Core runs on dense grids/fields
- Adapters are outputs, never core dependencies
- New implementations must support deterministic regression gates

## Layering
`math -> grid -> ops -> layout -> adapters/samples`

### Core
Pure indexing/domain primitives.

### Grids / Fields
Authoritative containers that own native memory.

### Operators
Deterministic transforms on authoritative data containers.

### Layout
Seed-driven orchestration on pure grids/fields only.

### Adapters / Samples
Non-authoritative outputs and visualization surfaces.

## Core data contracts
### GridDomain2D
- Immutable discrete domain
- Row-major indexing

### MaskGrid2D
- Native-memory bitmask container
- Intended for authoritative region/occupancy masks
- Supports snapshot hashing for deterministic gates

### ScalarField2D
- Native-memory scalar container
- Used for height/density/SDF-like fields
- Mutable struct; avoid accidental copies

## Cross-links
- Implemented map subsystem truth lives in: `systems/map-pipeline-by-layers-ssot.md`
- Future sequencing lives in: `planning/active/PCG_Roadmap.md`
