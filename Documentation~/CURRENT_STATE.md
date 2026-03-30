# Current State

Status date: 2026-03-23

## What is active now
- The Islands documentation migration is being handled as Tier L.
- The old documentation tree is intended to be kept as a fixed snapshot.
- The new governed documentation root is `Documentation~/`.
- The first promoted authority surfaces are the PCG core and the implemented Map Pipeline by Layers slice.

## What is implemented now (confirmed for documentation authority purposes)
- New PCG runtime direction: grid-first, deterministic, adapters-last.
- Map Pipeline by Layers implemented slice: F0–F2.
- F3–F6 are not implementation truth yet.

## What is not settled yet
- Final role of legacy tilemap map-generation docs.
- Whether layout strategies deserve their own subsystem SSoT.
- GraphLibrary promotion.
- Noise / Mesh / Surfaces migration hardening.

## Immediate next migration focus
Batch 3 — legacy map-generation triage.

## Why Batch 3 comes next
Batch 2 already created the first active governed PCG surfaces.
The next highest-risk ambiguity is the relationship between:
- the new Map Pipeline by Layers direction,
- legacy map-generation SSoT-like material,
- and the old roadmap/doc names that still imply authority.
