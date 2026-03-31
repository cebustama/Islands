# Islands.PCG — Active Roadmap

Status: Active planning  
Authority: Planning only (not implementation truth)  
Scope: Sequencing and future work for the new Islands.PCG pipeline.

## Rule
This document is not implementation authority.
Implemented truth lives in subsystem SSoTs and governed reference/support docs where explicitly assigned.

## Current status snapshot
- Phase A: done
- Phase B: done
- Phase C: done
- Phase D: done
- Phase E: implemented / test-gated support surface
  - E1: done
  - E2: implemented
  - E3: fully locked
  - E4: seed-set regression complete
- Phase F: in progress
  - F0: done
  - F1: done
  - F2: done
  - F3: next
- Phase G: later
- Phase H: later
- Phase I: later

## Documentary note on Layout Strategies
Layout strategies are currently treated as a governed deep reference / staged support surface under PCG.
They do not currently function as a separate subsystem SSoT.
See `reference/pcg-layout-strategies-reference.md` for deep per-strategy behavior and gates.

## Documentary note on Noise / Meshes / Surfaces / Shaders
After Batch 6:
- Noise remains governed reference / staged support rather than a subsystem SSoT.
- Meshes remain governed reference / staged support rather than a subsystem SSoT.
- Surfaces remain governed reference / staged support rather than a subsystem SSoT.
- Shaders remain governed reference / support only.
This roadmap may mention those surfaces as planning dependencies or support infrastructure, but that does not promote them into subsystem authority.

## Phase F — Map Pipeline by Layers
### Done
- F0 Context + contracts
- F1 Map lantern
- F2 Base terrain (`Height`, `Land`, `DeepWater`)

### Next
#### F3 — Hills + topology
- append topology layer IDs
- add `MaskTopologyOps2D`
- implement `Stage_Hills2D`
- integrate Islands.Noise Jobs via bridge
- add F3 stage + pipeline goldens
- update lantern for hills/topology

#### F4 — Shore + ShallowWater
- deterministic shallow-water ring around land

#### F5 — Vegetation + fixups
- vegetation masks with exclusion rules
- optional moisture support

#### F6 — Paths / Stairs / Placement
- `Walkable`
- `Paths`
- `Stairs`
- placement outputs without GameObjects

## Later phases
### Phase G
Morphology + neighborhood masks

### Phase H
Extract + adapters

### Phase I
Burst / SIMD upgrades

## Legacy relationship
Legacy map-generation documents are conceptual reference only for the new pipeline.
The active authoritative direction is masks/fields + adapters-last + deterministic headless stages.
