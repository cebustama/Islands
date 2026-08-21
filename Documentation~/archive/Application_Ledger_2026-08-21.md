# Islands.PCG — Documentation application ledger, 2026-08-21

Scope: **five** pending doc-update queues disposed — `W_b_…` (applied in full),
`W-aux_h_…` (applied in full), `Phase_Q_…` (applied in full, decision §9.1 resolved),
`Phase_W_…` (closed; §3 declared irrecoverable) and `Phase_T2_…` (5 of 7 items applied,
2 blocked, still live). Plus an index and coverage-matrix correction pass against the
package directory listing.
**Zero code changes. Zero tests run. Zero asset or preset changes.** Governed documents
edited: 8. Queues disposed: 3 APPLIED, 1 CLOSED, 1 PARTIALLY APPLIED.
Sections §1–§8 cover the first three queues; **§10 covers the Q and W closures and the
index correction**, which happened later in the same session once the package tree arrived.

This ledger is **evidence, not authority**. It records what this session did and on what
basis. Nothing in it supersedes a governed surface.

---

## 1. Evidence base — what was verified here, and what was not

### Verified in this session, by direct file read or by mechanical check

| Claim | How it was checked |
|---|---|
| The W.b **code surface** is present in the package | `MapGenerationPreset.cs` L112/117/189/194/207 (five promoted fields), L532-533 / L583-584 / L588 (`ToJson()` emission); `MapGenerationPresetJsonImporter.cs` L170-174 (three value keys); `MapGenerationPresetTests.cs` L149 (`Defaults_WbPromotedFields_MatchPrePromotionEffectiveValues`); `MapGenerationPresetJsonRoundTripTests.cs` L46/85/87 (non-default fixture); `MapGenerationPresetWizard.cs` L40-44 (`HelpBox` narrowed to `hydroEpsilon`); `PCGMapTilemapVisualization.cs` L371-374 / L1827-1831 / L1913-1917 (`last*` declarations, `CacheParams`, `ParamsChanged` by effective value) |
| The 2026-08-20 applications are still present in the governed files | `changelog-ssot.md` carries the five entries written that day (W-aux.g / W-aux.f / W-aux.e / W-aux.d / X1.a); `PCG_Roadmap.md` carries Phase X1 in both the status snapshot and its own section; `SSoT_CONTRACTS.md` carries "Shadow defaults in test fixtures (W-aux.f)" — the retargeted §7 recorded in the 2026-08-20 ledger §3; `CURRENT_STATE.md` status date is 2026-08-20; `coverage-matrix.md` carries the X1 rows and the declared gaps |
| `Phase_T2_Pending_Doc_Updates.md` is in project knowledge | read in full, 266 lines |
| Every anchor of both queues, before substitution | 19 anchors located; occurrence counts recorded in §2 |
| Every substitution, after application | for each item: REPLACE / INSERTED TEXT present exactly once; SEARCH absent, except where the queue's own REPLACE deliberately retains the SEARCH text as its first or last line (§2.2a, §2.2b, §3.1, §3.2 of W.b) — for those the expected surviving count is 1 and was 1 |
| The substantive claim of W.b §4.3 item 3 | `PCGMapVisualization.cs` L442-444 + L452-461 and `PCGMapCompositeVisualization.cs` L397-399 + L407-416 assign `enableBiomeStage` and the biome climate fields from inline component fields, with no `preset != null ?` ternary. Claim holds; its numbers did not — see §3 |
| `Phase_T2_Design.md` does not exist | absent from the package file listing and from project knowledge |

### Carried forward WITHOUT verification in this session

| Claim | Source | Why not verified here |
|---|---|---|
| The W.b EditMode suite is **green** | user confirmation, 2026-08-21 (and previously 2026-08-20) | this session has no Unity and ran no tests. The *code* was verified present; *green* is user confirmation only |
| W.b console goldens at seed 243 res 256 identical before/after | user confirmation, 2026-08-20, restated in the W.b queue's Evidence section | same reason. This claim is now written into `changelog-ssot.md` §W.b as user confirmation, and is labelled as such there |
| Everything the 2026-08-20 ledger says about code state | that ledger, which itself declares its code claims carried from its own baseline | not re-derived. Spot-checks above cover the *document* applications only |

---

## 2. Anchor check — all 19 anchors, before substitution

No substitution was made before this table was complete.

### `W_b_Pending_Doc_Updates.md` — 15 edits

| Item | Target | Anchor kind | Occurrences | Result |
|---|---|---|---|---|
| §1.1 `MapTunables2D` claim correction | `PCG_Roadmap.md` | SEARCH | 1 | applied |
| §1.2 close W.b (heading) | `PCG_Roadmap.md` | SEARCH | 1 | applied |
| §1.2 outcome block | `PCG_Roadmap.md` | INSERT AFTER | 1 | applied |
| §1.3 status line (pair 1) | `PCG_Roadmap.md` | SEARCH | 1 | applied |
| §1.3 status line (pair 2) | `PCG_Roadmap.md` | SEARCH | 1 | applied |
| §2.1 N5.b promoted surface | `map-pipeline-by-layers-ssot.md` | INSERT BEFORE | 1 | applied |
| §2.2 M2a-9 clause (a) | `map-pipeline-by-layers-ssot.md` | SEARCH | 1 | applied |
| §2.2 M2a-9 clause (d-bis) | `map-pipeline-by-layers-ssot.md` | SEARCH | 1 | applied |
| §2.3 hydrology authoring coupling | `map-pipeline-by-layers-ssot.md` | **prose only** | — | anchor resolved, see below |
| §3.1 additive schema extension | `SSoT_CONTRACTS.md` | SEARCH | 1 | applied |
| §3.2 M2.a verdict table | `SSoT_CONTRACTS.md` | SEARCH | 1 | applied |
| §4.1 close `moistureModulation` | `CURRENT_STATE.md` | SEARCH | 1 | applied |
| §4.2 close `waterThreshold01` | `CURRENT_STATE.md` | SEARCH | 1 | applied |
| §4.3 three new observations | `CURRENT_STATE.md` | **prose only** | — | anchor resolved, see below; **applied with a correction, see §3** |
| §5 changelog entry | `changelog-ssot.md` | **prose only** | — | anchor resolved, see below |

**Prose-described insertion points, resolved to unique literal anchors.** Each was checked
for uniqueness before use. Recorded here because the queue does not contain them, so a
future rollback needs this table:

- §2.3 → INSERT AFTER `- \`MapPipelineRunner2DGoldenLMTests.cs\` — F0→G→L→M pipeline golden captured`
  (last line of the Phase L §Golden coverage block, immediately before `### Phase M2.b`). 1 occurrence.
- §4.3 → INSERT AFTER `  \`StageBaseTerrain2DTests.NoShapeTunables()\`. Never confirmed.`
  (last bullet of §Open observations, immediately before `## Measured calibration baselines and climate reachability`). 1 occurrence.
- §5 → INSERT BEFORE `## W-aux.g — Hills window recalibration (area-quantile thresholds, F3b′)`
  (the file is newest-first; W-aux.g was the head entry). 1 occurrence.

### `Phase_T2_Pending_Doc_Updates.md` — 5 applied, 2 blocked

| Item | Target | Anchor kind | Occurrences | Result |
|---|---|---|---|---|
| §1.1 Phase T2 branch | `PCG_Roadmap.md` | INSERT BEFORE | 1 | applied |
| §1.2 status snapshot | `PCG_Roadmap.md` | SEARCH | 1 | applied |
| §1.3 annotate Phase T1 | `PCG_Roadmap.md` | SEARCH | 1 | applied |
| §1.4 design-doc table row | `PCG_Roadmap.md` | SEARCH | 1 | **NOT APPLIED — conditional** |
| §2.1 adapter-track sentence | `CURRENT_STATE.md` | SEARCH | **0** | **applied after re-anchor, see §3** |
| §2.2 deferred list | `CURRENT_STATE.md` | SEARCH | 1 | applied |
| §3 index entry | `SSoT_INDEX.md` | SEARCH | 1 | **NOT APPLIED — conditional** |

No collision between the two queues. W.b §1.3 pair 1 edits `PCG_Roadmap.md` L127-129 and
T2 §1.2 edits L126 — adjacent lines, disjoint strings; neither substitution destroys the
other's anchor, in either order.

---

## 3. Deviations from queue text — two, both by user decision

### 3.1 — W.b §4.3: source references corrected before insertion

The queue's third observation cited `PCGMapVisualization` L445–454,
`PCGMapCompositeVisualization` L405–414, and "the twelve `biome*` climate fields". Read
against the files this session, the climate assignment blocks are at **L452–461** and
**L407–416**, and there are **ten** such assignments, not twelve.

Applied text was corrected on both counts (**user decision, 2026-08-21**). The claim itself
— neither component resolves biome climate from the preset, so the same preset yields
different climate on different components — was verified and is unchanged.

Why this was corrected rather than applied verbatim: `CURRENT_STATE.md` is the operational
present tense, and this observation is a pointer to code someone will open. A pointer that
is off by seven lines and wrong about the count sends the reader to the wrong place and
makes them doubt the claim that *was* right. The correction is of the same class as the
verification that produced it, not a rewrite of the queue's argument.

### 3.2 — T2 §2.1: re-anchored

The queue's SEARCH matched **zero** times. Cause: `CURRENT_STATE.md` wraps the sentence
after `remain`, the queue wraps it after `phases`. Content identical, break point different.

Under the no-approximation rule the item was stopped and reported rather than fuzzy-matched.
**User approved the re-anchor**; the corrected pair (anchored on
`distilled from observing W-generated worlds. Adapter-track phases T1 and Q2 remain`) matched
exactly once and was applied. The semantic change is exactly the one the item asked for:
`T1 and Q2` → `T1, T2 and Q2`. The re-anchor is recorded in place inside the queue.

---

## 4. Per-queue final state

### `W_b_Pending_Doc_Updates.md` — **APPLIED. Closed; ready to archive.**

15 of 15 edits applied across five governed documents. No blocker. Precondition confirmed
by the user before applying: W.b code applied and suite green.

| Item | Target | Disposition |
|---|---|---|
| §1.1 | `PCG_Roadmap.md` | applied — planning claim about `MapTunables2D` superseded by implementation evidence |
| §1.2 | `PCG_Roadmap.md` | applied — heading closed, outcome block inserted |
| §1.3 | `PCG_Roadmap.md` | applied — both status lines now read W.b closed, next W-aux.h |
| §2.1 | `map-pipeline-by-layers-ssot.md` | applied — `MapGenerationPreset` registered in §Configuration Assets (N5.b) |
| §2.2 | `map-pipeline-by-layers-ssot.md` | applied — M2a-9 (a) conditioned, (d-bis) added |
| §2.3 | `map-pipeline-by-layers-ssot.md` | applied at the end of the Phase L block — the `riverThresholdFraction` / `biomeRiverFlowNorm = 0` asymmetry is now recorded, deliberately not fixed |
| §3.1 | `SSoT_CONTRACTS.md` | applied — additive key extension declared not a serialization break |
| §3.2 | `SSoT_CONTRACTS.md` | applied — seven-row M2.a promotion verdict table |
| §4.1 | `CURRENT_STATE.md` | applied — `moistureModulation` observation closed |
| §4.2 | `CURRENT_STATE.md` | applied — `waterThreshold01` observation closed |
| §4.3 | `CURRENT_STATE.md` | applied **with the §3.1 correction** — three new observations |
| §5 | `changelog-ssot.md` | applied — W.b entry inserted as the head entry |

### `Phase_T2_Pending_Doc_Updates.md` — **PARTIALLY APPLIED. LIVE.**

| Item | Target | Disposition |
|---|---|---|
| §1.1 | `PCG_Roadmap.md` | applied — Phase T2 branch inserted before `### Phase I` |
| §1.2 | `PCG_Roadmap.md` | applied — snapshot carries T1 (cross-referenced) and T2 |
| §1.3 | `PCG_Roadmap.md` | applied — Phase T1 annotated with the open decision |
| §1.4 | `PCG_Roadmap.md` | **BLOCKED — `Phase_T2_Design.md` does not exist.** Anchor verified unique and ready |
| §2.1 | `CURRENT_STATE.md` | applied **after re-anchor**, see §3.2 |
| §2.2 | `CURRENT_STATE.md` | applied — T2 added to the deferred/optional list |
| §3 | `SSoT_INDEX.md` | **BLOCKED — same reason. Also needs re-anchoring**: this session edited the very lines its SEARCH targets |
| §4 | — | **RESOLVED by user decision: `research/`.** The file move is a package operation and was not performed here |
| §5 | — | no changelog / coverage-matrix / supersession entry, per the queue's own reasoning. Concurred: adding a planning branch changes neither semantics nor authority |

**Blocker restated, per the queue header:** do not archive this queue. §1.4 and §3 exist
nowhere else, and both become applicable the moment `Phase_T2_Design.md` is written.

---

## 5. Governed documents edited this session

| Document | Edits | Source |
|---|---|---|
| `PCG_Roadmap.md` | 8 | W.b §1.1, §1.2 ×2, §1.3 ×2; T2 §1.1, §1.2, §1.3 |
| `CURRENT_STATE.md` | 5 | W.b §4.1, §4.2, §4.3; T2 §2.1, §2.2 |
| `map-pipeline-by-layers-ssot.md` | 4 | W.b §2.1, §2.2 ×2, §2.3 |
| `SSoT_CONTRACTS.md` | 2 | W.b §3.1, §3.2 |
| `changelog-ssot.md` | 1 | W.b §5 |
| `SSoT_INDEX.md` | 2 | **not queue-derived — authored this session**, see §6 |

Plus the two queue files themselves (status headers and in-place disposition notes).

### Short local update loop — how each concept was walked

- **W.b parameter surface.** Primary home per `coverage-matrix.md` is the pipeline SSoT →
  `map-pipeline-by-layers-ssot.md` §Configuration Assets and §M2a-9 first (step 3);
  `CURRENT_STATE.md` next, since active status and open observations changed (step 4);
  `changelog-ssot.md`, since a contract clause and the serialization posture changed
  (step 5); cross-cutting rules to `SSoT_CONTRACTS.md`. No document was replaced or
  absorbed, so no `supersession-map.md` entry (step 6); this is not a salvage pass, so no
  `migration-log.md` entry (step 7).
- **Phase T2 branch.** Planning only. Primary home is the roadmap; `CURRENT_STATE.md`
  touched solely for adapter-track *status*, not for implementation claims. Steps 5-7
  deliberately not taken, per T2 §5 — a planning branch changes neither semantics nor
  authority.

---

## 6. Index edits are mine, not a queue's

`SSoT_INDEX.md` received two edits authored in this session, not carried from any queue:
registering `Phase_T2_Pending_Doc_Updates.md` as live, archiving
`W_b_Pending_Doc_Updates.md`, listing this ledger, and recording a **registration gap** —
both queues consumed today existed, were live, and appeared in neither index list.

That gap is the finding worth keeping. A pending queue is unapplied governed content: if
it is not registered, the next session cannot know it is owed. The index instruction added
is that queues get registered when *written*, not when consumed. These edits carry no
queue's authority behind them and should be reviewed as new drafting.

---

## 7. Still blocked, unchanged from 2026-08-20

| Item | Blocker | What would unblock it |
|---|---|---|
| `Phase_Q_Design.md` §3.1 — starter asset naming and path | open decision `Phase_Q_Pending_Doc_Updates.md` §9.1 | a user decision. Not raised this session |
| `changelog-ssot.md` — W.a world-scale golden | the hash values exist in no governed file, test constant or captured log | re-running the seed 56 @ 64×64 world-preset capture. Code work, out of scope for a documentation session |
| `Phase_T2_Pending_Doc_Updates.md` §1.4 and §3 | `Phase_T2_Design.md` does not exist | writing that design document |

Neither Q nor W was touched. Both remain live with their blockers intact.

---

## 8. Reported, not fixed

- **`coverage-matrix.md` L57 contradicts `SSoT_INDEX.md`.** The row for the
  W-aux.c…W-aux.f / X1.a application record locates the six queues "under
  `planning/active/`" with status "Active until archived", while the index says they were
  moved to `planning/archive/` on 2026-08-20. One of the two governed spine documents is
  stale. Not corrected here: outside both queues, and the fix depends on where the files
  actually are on disk, which this session cannot see. A row for the W.b and T2 queues is
  also missing.
- **`changelog-ssot.md` carries a duplicate empty heading.** `## Phase F3b —
  Height-Coherent Hills (Clean Break)` / `Date: 2026-04-08` appears twice in immediate
  succession, the first with no body. Pre-existing, unrelated to these queues, harmless to
  the W.b insertion (which went in at the head of the file).
- **Corner-height research artifact.** Filed to `research/` by user decision. The user
  notes it is the basis for the T2.3 implementation. Influence is not authority: the model
  becomes implementation authority only when restated in `Phase_T2_Design.md` §T2.3 as this
  package's own decisions, with the artifact cited as source. Recorded in the queue at §4.
- Everything in the 2026-08-20 ledger §6 (`Stage_BaseTerrain2D` parity, wizard test gap,
  `Vegetation` runtime golden gap, `Default_MapPreset` @256 `Height` goldens not recaptured)
  remains open. None of it is documentation work.

---

## 9. `W-aux_h_Pending_Doc_Updates.md` — APPLIED (third queue, same session)

Consumed after `W_b_Pending_Doc_Updates.md`, as its own ordering warning requires: three of
its items anchor on text the W.b queue introduces, and one supersedes two observations W.b
adds. Applying it first would have produced zero matches on those items or a document
asserting both versions of the same finding.

### 9.1 — Applicability precondition

W-aux.h **code** verified present by direct file read this session: probe
`PCGMapTilemapVisualization.LogHydrologyReport()` at L1139 with the `[hydroprobe]` guards and
the `preset != null ?` resolution of the effective fraction; Inspector button
`Log Hydrology Report (TEMP)` at `PCGMapTilemapVisualizationEditor.cs` L444; diagnostics
constant `RiverThresholdFractionDefault = 0.02f` at `MapGenerationPresetDiagnostics.cs` L68,
`CheckRiverFlowNormDecoupled` registered in `Diagnose()` at L80 and implemented at L244, rule
id `R7.RiverFlowNormDecoupled` at L255; three R7 gates at
`MapGenerationPresetDiagnosticsTests.cs` L126 / L140 / L152.

**Green suite is user confirmation only** — the same standing as W.b. No tests were run here.
The three `hydroprobe` runs and every number quoted in this queue (basin counts, ceilings,
sweep results, fill depths, lake components) are **user-confirmed console output from the
originating session, not re-derived here**. They are now written into `CURRENT_STATE.md` and
`changelog-ssot.md` as measured values; their evidentiary basis is that console capture.

### 9.2 — Anchor check, all 11 edits

| Item | Target | Occurrences | Result |
|---|---|---|---|
| §1.1 header status line **[post-W.b anchor]** | `PCG_Roadmap.md` | 1 | applied |
| §1.2 Phase W header **[post-W.b anchor]** | `PCG_Roadmap.md` | 1 | applied |
| §1.3 close W-aux.h in the W-aux track | `PCG_Roadmap.md` | 1 | applied |
| §2.1 status date | `CURRENT_STATE.md` | 1 | applied |
| §2.2 implemented list | `CURRENT_STATE.md` | 1 | applied |
| §2.3 two probes → three | `CURRENT_STATE.md` | 1 | applied |
| §2.4 register the hydrology probe | `CURRENT_STATE.md` | 1 | applied |
| §2.5 rule count six → seven | `CURRENT_STATE.md` | 1 | applied |
| §2.5 R7 row + explanation | `CURRENT_STATE.md` | 1 | applied |
| §2.6 supersede the two W.b observations **[post-W.b anchor]** | `CURRENT_STATE.md` | 1 | applied |
| §3 changelog entry **[post-W.b anchor]** | `changelog-ssot.md` | 1 | applied |

All 11 verified afterwards: REPLACE text present exactly once, SEARCH text absent except
where the queue's own REPLACE retains it (§1.3, §2.4, §2.5b, §3), where the expected
surviving count is 1 and was 1. Zero deviations from queue text — unlike the W.b queue, this
one needed no correction and no re-anchor.

### 9.3 — The queue's two self-declared omissions in the W.b queue: confirmed and covered

The queue states that W.b updates `CURRENT_STATE.md` §Open observations but not its
`Status date:` line nor its `## What is implemented now` list. **Confirmed against the file:**
after the W.b queue was applied, the status date still stopped at W-aux.g and the implemented
list still omitted W.b. Items §2.1 and §2.2 carry both batches, as the queue intends. They
were applied as a pair; applying one alone would have left the status line naming W-aux.h
while the implemented list omitted W.b.

This is a real defect in the W.b queue, not in this one. Recorded so that a future queue
author checks both surfaces, not only §Open observations.

### 9.4 — Two internal inconsistencies introduced or inherited, NOT fixed

Both are flagged rather than silently corrected, because both are judgement calls the queue
itself declined to make.

1. **§Open observations now contains verified content under an "all UNVERIFIED" preamble.**
   The section opens with *"Findings that surfaced during the W-aux.c…W-aux.f run … **All are
   UNVERIFIED as stated** and none is fixed"*. That was already false after W.b (items 4.1 and
   4.2 struck two entries through as RESOLVED); §2.6 makes it plainly false, inserting four
   entries labelled MEASURED, VERIFIED or REFUTED. A reader trusting the preamble will
   discount measured data; a reader trusting the entries will disbelieve the preamble. The
   preamble needs rewording or the section needs splitting. Not done here.
2. **"Both mirror private constants…" now describes three probes.** The queue flags this in
   its own note at §2.4 and deliberately leaves it: the third probe re-derives through the
   stage's operators rather than mirroring them, so the sentence is true of two probes and
   false of the third. Left unedited, per the queue's instruction to decide rather than
   inherit.

### 9.5 — Disposition

**APPLIED in full. Closed; ready to archive.** No blocker, no unapplied item. The queue file
itself is not a package file in this session's inputs; its status header must be replaced
before archiving — the replacement text was delivered with this ledger.

---

## 10. Closures and index correction (later in the same session, after the package tree arrived)

The user supplied the package directory listing (`tree.txt`), which allowed several claims
that had been carried as unverified to be checked, and two blocked queues to be disposed.

### 10.1 — Verified against the directory listing

| Claim | Result |
|---|---|
| `Runtime/PCG/Samples/PCG Map Tilemap/Tilesets/` is the real location of the override asset | **TRUE.** The folder exists and holds `TestBiomeTileOverride.asset`, four `TilesetConfig*` assets and a `Placeholders/` folder with 48 `PH_*` tiles |
| `Samples~/0.1.0-preview/PCG Map Tilemap/` (the path `Phase_Q_Design.md` §3.1 asserted) | **DOES NOT EXIST.** `Samples~/0.1.0-preview/` holds Fractal, Hash, Noise, Procedural Meshes and ProceduralSurface only |
| The `Showcase` world preset asset is absent | **CONFIRMED, third time.** `Runtime/PCG/Samples/Presets/` holds `Default.asset` and `WarpTest.asset` |
| No mesh-adapter code exists on disk | **CONFIRMED.** `Runtime/PCG/Adapters/` contains only `Tilemap/`. No `Runtime/PCG/Samples/PCG Map Mesh/`, no `Islands.PCG.Adapters.Mesh.asmdef` |
| Existing asmdefs | `Islands.PCG.Runtime`, `Islands.PCG.Editor`, `Islands.PCG.Adapters.Tilemap`, `Islands.PCG.Inspection`, `Islands.PCG.Samples`, `Islands.PCG.Samples.Shared`, `Islands.PCG.Tests.EditMode`, `Islands.Runtime`, `Islands.Samples` |
| An F2c shape-path golden test exists | **YES** — `MapPipelineRunner2DGoldenF2cTests.cs`. The W.c handoff's claim that the shape path lacks coverage is true of *visual* coverage only |

### 10.2 — Index paths were wrong; corrected

`SSoT_INDEX.md` located every consumed queue under `planning/archive/`. On disk they are
under `archive/pending doc updates/`, and `Application_Ledger_2026-08-20.md` is under
`archive/`. Corrected, with the correction stated in place rather than applied silently.

Three files the index listed as archived are **not on disk at all**:
`Phase_W_aux_Pending_Doc_Updates.md`, `Phase_W_aux_b_Pending_Doc_Updates.md`,
`W-aux_c_Blocks_2_3_Pending_Doc_Updates.md`. Their disposition is recorded in the
2026-08-20 ledger; the files are gone. Recorded as a gap in the index rather than deleted
from the list, so the loss stays visible.

The index also listed only `PCG_Roadmap.md` plus queues under active planning, while
`planning/active/` holds eight documents (seven phase designs plus `crosscheck_roadmap.md`)
and `planning/exploration/` holds one. All now listed. Two reference docs present on disk
and previously unlisted (`map-tilemap-scene-setup.md`, `scalar-heatmap-scene-setup.md`) were
added.

### 10.3 — `Phase_W_Pending_Doc_Updates.md` — CLOSED, §3 irrecoverable

**User decision, 2026-08-21: close rather than re-capture.** The W.a hashes existed only in
the 2026-08-09 session console. Re-capture cannot restore them: five batches changed the
pipeline afterwards (W-aux.b, W-aux.d, W-aux.f — which moved `waterThreshold01` and therefore
`Land` and everything derived from it — W-aux.g, W.b), and the `Showcase` world preset is
absent, so the input side of the run is missing too. Writing today's numbers under a
2026-08-09 label would make the changelog misstate when a measurement was taken.

Edits: the "Still pending in this file" block in `changelog-ssot.md` became a closure note
recording the loss and both reasons; the W-aux.b cross-reference was updated to point at it;
the queue's status header, §3 and §3b checklist row were rewritten as disposed.
**8 of 8 items disposed. Ready to archive.**

### 10.4 — `Phase_Q_Pending_Doc_Updates.md` — APPLIED in full

**User decision, 2026-08-21: adopt the guide's convention**, concrete name
`Overworld16bit_BiomeOverride`. §3.1 applied to `Phase_Q_Design.md`, fixing both defects in
the single pass the blocking note reserved: the non-existent `Samples~` path, and the
"5 temperature clusters" text that contradicted the implemented `Populate Default Biome
Groups` (12 groups, 4 layer slots each). **17 of 17 items applied. Ready to archive.**

**Flagged at the time of the decision, not silently resolved:** no tileset named
`Overworld16bit` exists in the package. The four present are `TilesetConfig_BaseSet`,
`TilesetConfig_Winlu`, `TilesetConfig-8bit` and `TilesetConfig-DragonWarrior`.
`Overworld16bit` is the *example* used in `tileset-import-guide.md` §Phase 7, not a real set
here. The name was applied as instructed; if no such tileset is planned, one literal pair
switches it.

### 10.5 — Session totals

Five queues disposed: `W_b_…` APPLIED, `W-aux_h_…` APPLIED, `Phase_Q_…` APPLIED,
`Phase_W_…` CLOSED (§3 irrecoverable), `Phase_T2_…` PARTIALLY APPLIED and still live.
Governed documents edited: 8 — `PCG_Roadmap.md`, `CURRENT_STATE.md`,
`map-pipeline-by-layers-ssot.md`, `SSoT_CONTRACTS.md`, `changelog-ssot.md`,
`SSoT_INDEX.md`, `coverage-matrix.md`, `Phase_Q_Design.md`. **Zero code, zero tests, zero
asset changes.** The asset rename and the file moves into `archive/pending doc updates/`
are user actions and were not performed here.
