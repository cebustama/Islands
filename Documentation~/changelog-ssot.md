# Changelog — SSoT

## 2026-04-02 (Phase F2c)
- Phase F2c — Arbitrary Shape Input implemented and test-gated.
- New `MapShapeInput` companion struct (`Runtime/PCG/Layout/Maps/MapShapeInput.cs`):
  `HasShape` flag + `MaskGrid2D Mask`. Default (`None`) preserves F2b ellipse+warp path.
  Caller owns and disposes the mask; `MapInputs` holds by value.
- `MapInputs` extended with optional 4th constructor parameter `MapShapeInput shapeInput = default`.
  All existing call sites unchanged (backward compatible).
- `Stage_BaseTerrain2D` extended with F2c shape-input branch:
  - When `HasShape = true`: `mask01 = shape.GetUnchecked(x, y) ? 1f : 0f` replaces ellipse+warp.
  - All three RNG arrays (island noise, warpX, warpY) are always filled in the same order
    regardless of path, so downstream stages see identical RNG state with or without shape input.
  - Dimension guard: throws `ArgumentException` if shape mask dimensions differ from domain.
- No new `MapLayerId` or `MapFieldId` entries.
- No `PCGMapVisualization` patch required: no new stage, no new layer; lantern always runs F2b
  path (no shape injection); shape-path visual testing deferred to future editor tooling.
- F2b goldens unchanged (no-shape path is bit-for-bit identical).
- New F2c shape-path goldens locked: Land=`0xD986402B40273547`, DeepWater=`0xD5F1514F5471CC2F`
  (64×64, seed=12345, center-circle radius=20).
- New test coverage: `Stage_BaseTerrain2D_WithShapeInput_IsDeterministic`,
  `Stage_BaseTerrain2D_WithShapeInput_LandSubsetOfShape`,
  `Stage_BaseTerrain2D_WithShapeInput_GoldenHashes_Locked`,
  `MapPipelineRunner2D_GoldenHash_F2cShapePath_IsLocked`.
- `map-pipeline-by-layers-ssot.md` updated: boundary advanced to Phase H; F2c contracts added.
- `PCG_Roadmap.md` updated: F2c marked Done; Phase H marked Next.
- `CURRENT_STATE.md` updated: F2c recorded as resolved; immediate next focus set to Phase H.

## 2026-04-02 (Phase F2b)
- Phase F2b — Island Shape Reform implemented and test-gated.
- `Stage_BaseTerrain2D` reformed: circular radial falloff replaced with ellipse + domain-warp silhouette.
- Shape pipeline: ellipse (x-axis scaled by 1/islandAspectRatio) → domain warp (two WarpCellSize=16 noise
  arrays displacing the sampling point) → smoothstep radial falloff → height perturbation noise.
- New `MapTunables2D` fields: `islandAspectRatio` (clamped [0.25, 4.0], default 1.0),
  `warpAmplitude01` (clamped [0, 1], default 0.0).
- Both warp noise arrays always consumed from ctx.Rng regardless of warpAmplitude01 value,
  keeping total RNG consumption count tunable-independent for all downstream stages.
  RNG consumption order: island noise → warpX → warpY (stable regardless of tunables).
- aspect=1.0 + warp=0.0 => geometrically identical circle to pre-F2b; goldens differ.
- No new `MapLayerId` or `MapFieldId` entries introduced.
- All F2–Phase G golden hashes re-locked in one migration pass. Phase G goldens locked for first time.
- `PCGMapVisualization` patched: new Inspector header "F2 Tunables (Island Shape — Ellipse + Warp)"
  with `islandAspectRatio` and `warpAmplitude01` fields; dirty tracking and MapTunables2D construction
  updated; `BaseTerrainStage_Configurable` updated to mirror Stage_BaseTerrain2D exactly (including
  WarpCellSize=16 constant and BilinearSample helper).
- `map-pipeline-by-layers-ssot.md` updated: boundary advanced to F2c; tunables list extended
  with new F2b fields; F2b shape pipeline contracts added under Stage_BaseTerrain2D.
- `PCG_Roadmap.md` updated: F2b marked Done; F2c marked Next; status snapshot updated.
- `CURRENT_STATE.md` updated: F2b noted as resolved; immediate next focus set to F2c.

## 2026-04-02 (Phase G)
- Expanded the implemented Map Pipeline by Layers slice from F0–F6 to F0–F6 + Phase G.
- Recorded Phase G — Morphology as implemented and test-gated rather than planning-only.
- Added `MaskMorphologyOps2D` as the deterministic morphological operator surface:
  - `Erode4Once`: single-pass 4-neighborhood erosion (cell is ON iff all 4 cardinal neighbors are ON in src)
  - `Erode4(radius)`: multi-pass erosion via ping-pong with one Allocator.Temp buffer
  - `BfsDistanceField`: multi-source BFS distance field; seeds enqueued row-major; unreached cells receive -1f sentinel
- Added `Stage_Morphology2D` as the implemented morphology stage for the active slice.
- Authoritative outputs: `LandCore` mask, `CoastDist` scalar field.
- New append-only IDs: `MapLayerId.LandCore = 11` (COUNT → 12), `MapFieldId.CoastDist = 2` (COUNT → 3).
- Stage tunables are stage-local: `ErodeRadius` (default 3), `CoastDistMax` (default 0 = auto: min(w,h)/2).
- Contracts:
  - `LandCore ⊆ Land`; `LandCore ⊆ LandInterior` (guaranteed when ErodeRadius >= 1).
  - `CoastDist == 0f` at all LandEdge cells.
  - `CoastDist > 0f` at all LandInterior cells reachable within CoastDistMax steps.
  - `CoastDist == -1f` at all non-Land cells and cells beyond CoastDistMax.
- `Stage_Morphology2D` does not consume `ctx.Rng` (no noise, no randomness).
- Added Phase G stage-level golden gate (`StageMorphology2DTests`: LandCore mask hash + CoastDist field hash).
- Added Phase G pipeline golden gate (`MapPipelineRunner2DGoldenGTests`).
- Field hash uses FNV-1a over float bits (math.asuint), matching the spirit of MaskGrid2D.SnapshotHash64.
- Patched `PCGMapVisualization` with `enableMorphologyStage` toggle and `stagesG` array.
  CoastDist (ScalarField2D) is not yet visualizable via the lantern; shows all-OFF if selected.
- Updated `PCG_Roadmap.md`:
  - Phase G marked done; entry expanded with implementation details.
  - Phase F2b added (planning only): island shape reform — domain warping, parameterized silhouettes, golden migration.
  - Phase F2c added (planning only): arbitrary shape input — image/mask/Voronoi cell as base terrain outline.
  - Phase J and K enriched with explicit archipelago support intent (Level 3 island shape vision).
  - Status snapshot updated: F6 done, Phase G done, F2b next (later), F2c later.
- Updated `CURRENT_STATE.md`: implemented slice reads F0–F6 + Phase G; next focus is Phase F2b.
- Updated `map-pipeline-by-layers-ssot.md`:
  - Scope and boundary extended to Phase G.
  - Phase G contracts added under `Stage_Morphology2D` and `MaskMorphologyOps2D`.
  - `LandCore` and `CoastDist` added to active registry contracts.
  - Phase G added to implemented surface (operators + stage + outputs + lantern).
  - Test-gated behavior list extended with morphology stage gates and Phase G pipeline golden.
  - Determinism rules extended with scalar field hash gate note.
  - Known limitations updated: CoastDist lantern limitation noted.
  - "Not governed here" updated from Phase G+ to Phase F2b+.
- No new subsystem SSoTs created. No authority decisions changed.
- Advanced the active roadmap so Phase F2b becomes the next planned implementation slice.

## 2026-04-01 (F6)
- Expanded the implemented Map Pipeline by Layers slice from F0–F5 to F0–F6.
- Recorded F6 — Traversal as implemented and test-gated rather than planning-only.
- Added `Stage_Traversal2D` as the implemented traversal stage for the active slice.
- Authoritative outputs: `Walkable` mask, `Stairs` mask.
- Contracts:
  - `Walkable` = `Land AND NOT HillsL2`; shore-edge land (LandEdge) is included; only hill peaks excluded.
  - `Stairs` = `HillsL1 AND NOT HillsL2` cells 4-adjacent to at least one `HillsL2` cell.
  - `Stairs ⊆ Walkable`; `Walkable ∩ HillsL2 == ∅`; `Stairs ∩ HillsL2 == ∅`.
  - `Stairs` may be empty on flat maps; not a defect.
- `Stage_Traversal2D` does not consume `ctx.Rng` (no noise, no randomness).
- Decided and recorded: `MapLayerId.Paths` write deferred to Phase O; F6 does not produce it.
  Paths depends on Phase N (POI placement) as a design prerequisite — paths connect places, and
  the places are not known until Phase N.
- Added F6 stage-level golden gate (`StageTraversal2DTests`) and F6 pipeline golden gate
  (`MapPipelineRunner2DGoldenF6Tests`).
- Patched `PCGMapVisualization` with `enableTraversalStage` toggle and `stagesF6` array.
- Updated `PCG_Roadmap.md`:
  - F6 entry rewritten: Walkable + Stairs only; Paths removed from scope.
  - Added Phase O — Traversal Network / Paths after Phase N.
  - Phase N enriched with RPG-style POI suitability examples (coastal village, forest dungeon,
    cave entrance at Stairs cells, open-plains camp).
  - Phase O records `MapLayerId.Paths = 5` as pre-registered; write ownership assigned here.
  - Status snapshot updated: Phase O added as "later (planning only)".
- Updated `CURRENT_STATE.md`: implemented slice reads F0–F6; immediate next focus is Phase G.
- Updated `map-pipeline-by-layers-ssot.md`:
  - Scope and boundary extended to F6.
  - F6 contracts added under `Stage_Traversal2D`.
  - F6 added to implemented surface (stage + outputs + lantern).
  - Test-gated behavior list extended with traversal stage gates.
  - `Paths` deferral note added (Phase O).
  - "Not governed here" updated.
- No new subsystem SSoTs created. No authority decisions changed.
