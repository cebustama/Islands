# Patch — status header for `W-aux_h_Pending_Doc_Updates.md`

The W-aux.h queue file is not among this session's package inputs, so it was not rewritten.
Apply this one literal pair to its top, then move the file to `planning/archive/`.

```
SEARCH:
Status: **queued, not applied.** Code for W-aux.h is applied and verified (see Evidence
below); the governed documents still describe the pre-W-aux.h state.

Applying is user-controlled. Each item below is a literal search/replace pair or an
insert-after anchor, in the same idiom as `W_b_Pending_Doc_Updates.md`.

REPLACE:
Status: **APPLIED — 2026-08-21. Closed; ready to archive.** All 11 edits across
`PCG_Roadmap.md`, `CURRENT_STATE.md` and `changelog-ssot.md` were applied and verified
in-session: each anchor located exactly once before substitution, each REPLACE confirmed
present exactly once afterwards. **Zero deviations from the queue text** — no correction and
no re-anchor was needed.

Ordering dependency honoured: `W_b_Pending_Doc_Updates.md` was applied first, in the same
session (2026-08-21). The four `[post-W.b anchor]` items matched on the first attempt, which
is the evidence that the order was right.

Applicability precondition, confirmed 2026-08-21: the W-aux.h **code** is applied and the
EditMode suite is green including the three R7 gates — user confirmation. The code surface
was additionally verified by direct file read this session:
`PCGMapTilemapVisualization.LogHydrologyReport()` (L1139), the
`Log Hydrology Report (TEMP)` Inspector button (`PCGMapTilemapVisualizationEditor.cs` L444),
`RiverThresholdFractionDefault` / `CheckRiverFlowNormDecoupled` / `R7.RiverFlowNormDecoupled`
(`MapGenerationPresetDiagnostics.cs` L68 / L80 / L244 / L255), and the three R7 gates
(`MapGenerationPresetDiagnosticsTests.cs` L126 / L140 / L152). The *green* status is user
confirmation only, not re-run here, and every measured number in this queue — basin counts,
ceilings, sweep results, fill depths, lake components — is user-confirmed console output from
the originating session, not re-derived at application time.

**Two things this queue deliberately left undecided are still undecided.** They were not
silently fixed. See `Application_Ledger_2026-08-21.md` §9.4:
1. `CURRENT_STATE.md` §Open observations still opens with "**All are UNVERIFIED as stated**"
   while now containing four entries labelled MEASURED / VERIFIED / REFUTED.
2. The paragraph beginning "Both mirror private constants" still says "Both" about three
   probes, and is false of the third.

Each item below is a literal search/replace pair or an insert-after anchor, in the same
idiom as `W_b_Pending_Doc_Updates.md`. Retained for the record; **do not re-apply.**
```

Verified: the SEARCH text above matches the queue file as delivered on 2026-08-21, exactly
once.
