# Islands.PCG — Documentation application ledger, 2026-08-20

Scope: eight pending doc-update queues consumed in one session. **Zero code changes.**
Governed documents rewritten in full: 10. Queues closed: 6 APPLIED, 2 PARTIALLY APPLIED
(both with pre-existing blockers, neither unblocked here).

Evidence base for this ledger: the eight queue files and the ten governed documents were
read directly in-session; every "already applied" claim below was verified against the
governed file, not against a checklist. Claims about the code (green suite, W-aux.f fix
verified) are **carried from the session baseline and were not re-verified here** — this
session ran no tests.

---

## 1. Per-queue disposition

### `Phase_Q_Pending_Doc_Updates.md` — PARTIALLY APPLIED (unchanged)
16/17 applied on 2026-08-18. **Verified in-session** that §3.2–3.5 (`Phase_Q_Design.md`),
§4 (`coverage-matrix.md`), §5 (`tileset-import-guide.md`) and §6 (`SSoT_INDEX.md`) are
present in the governed files. No item re-applied.

| Item | Disposition |
|---|---|
| §3.1 starter asset naming and path | **BLOCKED** by open decision §9.1. Not unblocked here: the decision is the user's and the correction is marked in place inside `Phase_Q_Design.md` §3.1 so it lands in one pass |
| all others | already applied, verified |

### `Phase_W_Pending_Doc_Updates.md` — PARTIALLY APPLIED (unchanged)
7/8 applied on 2026-08-18. **Verified in-session** that §1.1–1.4 are present in
`Phase_W_Design.md` (§9 "Resolved 2026-08-09", `shapeMode = Ellipse` decision).

| Item | Disposition |
|---|---|
| §3 W.a golden registration | **BLOCKED — data unavailable.** The hashes exist in no governed file, test constant or captured log. Re-running the capture is code work. Reserved slot retained in `changelog-ssot.md` |
| all others | already applied, verified |

### `W-aux_c_Pending_Doc_Updates.md` — APPLIED
| Item | Target | Disposition |
|---|---|---|
| §1 changelog entry | `changelog-ssot.md` | applied, with a forward-pointer note recording what W-aux.f later superseded |
| §2.1 status date + slice | `CURRENT_STATE.md` | applied; date superseded by W-aux.f → 2026-08-20 |
| §2.2 exporter sections | `CURRENT_STATE.md` | applied (folded into the W-aux.c resolution block, cross-referenced from the W-aux.a block) |
| §2.3 Hot-ceiling re-measure | `CURRENT_STATE.md` | applied, dated, with a caveat that its arithmetic uses the pre-W-aux.f threshold |
| §2.4 height composition ceiling | `CURRENT_STATE.md` | **SUPERSEDED by W-aux.f §1/§2** — not written as current truth |
| §2.5 vegetation noise granularity | `CURRENT_STATE.md` | **SUPERSEDED by W-aux.d** (cause was value mapping, not grain). Replaced by the M2a-9(d) caveat. *Detected while writing, not in the census* |
| §3 default-preset-calibration | `reference/default-preset-calibration.md` | **DROPPED by user decision 2026-08-20.** The asset is still being tuned; the reference row was removed from `coverage-matrix.md` |
| §4 exporter coverage gap | `coverage-matrix.md` | applied |
| §5 index drift | `SSoT_INDEX.md` | applied, with the pointer wording (index owns authority order, not implementation state) |
| §6 roadmap entries | `PCG_Roadmap.md` | applied; the W-aux.d batch *proposal* superseded by W-aux.d's own closure |
| §8.1 `DeepWater` | — | **RESOLVED by X1 §4**; interpretation note added to `CURRENT_STATE.md` |
| §8.2 `moistureModulation` | `CURRENT_STATE.md` §Open observations | applied, **still UNVERIFIED** |
| §8.3 vegetation noise frequency | `CURRENT_STATE.md` §Open observations | applied with its motivation corrected by W-aux.d; survives as a spatial-character question |
| §8.4 `hillsL2_fraction` 0.65 | — | **RESOLVED**: applied in W-aux.c block 3. The window question reopens via W-aux.f, not via this item |
| §8.5 `Showcase` asset absent | `CURRENT_STATE.md` §Open observations | applied, **still UNVERIFIED** |
| §9 parameter legibility | `PCG_Roadmap.md` | applied as a standing thread, with X1 diagnostics rules named as its delivery mechanism |
| §10 not-changed record | — | superseded: block 3 *did* change the SSoT, as §10 itself predicted |

### `W-aux_c_Blocks_2_3_Pending_Doc_Updates.md` — APPLIED
| Item | Target | Disposition |
|---|---|---|
| §1 SSoT F5 edits | `map-pipeline-by-layers-ssot.md` | already applied at its own session, **verified in-session**. Its open governance question (scope line) resolved here as authority wording |
| §2 gates + declared gaps | `coverage-matrix.md` | applied (importer gate, wizard-UI gap, `Vegetation` runtime-golden gap, no evidence for river/lake advice) |
| §3 implemented baseline | `CURRENT_STATE.md` | applied, including the corrected asmdef-reference claim |
| §4 reference run | — | **SUPERSEDED** as current truth by W-aux.d and W-aux.f; retained as historical measurement inside the queue |
| §5 DoD outcome | this ledger | historical; the vegetation shortfall it records was resolved by W-aux.d |
| §6 carried-forward items | `CURRENT_STATE.md` §Open observations | applied, still labelled |
| §7 open decisions | `PCG_Roadmap.md` / this ledger | §7.1 folded into the Hills recalibration entry; §7.2 resolved; §7.3 closed by W-aux.d |

### `W-aux_d_Vegetation_Quantile_Pending_Doc_Updates.md` — APPLIED
| Item | Target | Disposition |
|---|---|---|
| §2.1 M2a-9 contract | `map-pipeline-by-layers-ssot.md` §F5 | applied verbatim, heading updated |
| §2.2 test-gated behaviour | `map-pipeline-by-layers-ssot.md` | applied, with the golden values advanced to their post-W-aux.f state and the full re-anchor chain recorded |
| §3 changelog entry | `changelog-ssot.md` | applied, with a note that both goldens moved again in W-aux.f |
| §4 CURRENT_STATE | `CURRENT_STATE.md` | applied |
| §5 append to blocks 2/3 queue | that queue's `Status:` header + this ledger | applied as disposition, not as an edit to a consumed file's body |
| §6 coverage-matrix no-change | — | **partially incorrect and corrected**: the file *does* carry a "Test coverage and known gaps" section, so the gates from blocks 2/3 §2 landed there |
| §7.1 prioritisation posture | `PCG_Roadmap.md` | applied as a top-level section; no collision (nothing equivalent existed) |
| §7.2 next batch | `PCG_Roadmap.md` | **SUPERSEDED** by W-aux.e's closure |
| §7.3 parked items | `PCG_Roadmap.md` | applied as PL-10 and PL-11 |
| §8 probe fate | `CURRENT_STATE.md` | resolved: **retained**, per the decision already recorded in W-aux.e §6 |
| §9 project-instructions tension | — | recorded, no edit proposed or made; project instructions are user-owned |

### `W-aux_e_Threshold_Mapping_Audit_Pending_Doc_Updates.md` — APPLIED
| Item | Target | Disposition |
|---|---|---|
| §2 changelog entry | `changelog-ssot.md` | applied, with a forward pointer to the W-aux.f fix |
| §3.1 status date | `CURRENT_STATE.md` | superseded by W-aux.f's date |
| §3.2 known issue (open) | `CURRENT_STATE.md` | **SUPERSEDED by W-aux.f §2** — written once, past tense, RESOLVED |
| §3.3 temporary probes | `CURRENT_STATE.md` | applied as its own section, plus the W-aux.f formula-coupling note |
| §4.1 F3b window correction | `PCG_Roadmap.md` | applied **merged with W-aux.f §5** into a single block: unreachable, why, and what unblocked it |
| §4.2 next batch = W-aux.f | `PCG_Roadmap.md` | **SUPERSEDED** by W-aux.f's closure |
| §5 SSoT edit deferred | — | **SUPERSEDED** by W-aux.f §1, which made the edit |
| §6 decisions | this ledger | recorded |
| §7–§8 files touched / non-claims | `CURRENT_STATE.md` §Open observations | the non-claims survive as labelled UNVERIFIED items |

### `W-aux_f_Height_Ceiling_Desaturation_Pending_Doc_Updates.md` — APPLIED
| Item | Target | Disposition |
|---|---|---|
| §1 F2 contracts | `map-pipeline-by-layers-ssot.md` | applied, plus the probe-coupling line (resolves §8 as option 1) |
| §2 atom RESOLVED | `CURRENT_STATE.md` | applied, replacing W-aux.e §3.2 |
| §3 recalibrated defaults | `CURRENT_STATE.md` | applied (6 entry points + bit-identical land topology) |
| §4 changelog entry | `changelog-ssot.md` | applied, 29-row re-anchor table intact |
| §5 roadmap | `PCG_Roadmap.md` | applied, merged with W-aux.e §4.1 |
| §6 calibration inventory | `SSoT_CONTRACTS.md` | applied **per user decision** (recommended target; 3 of 6 entry points fall outside the F2 slice) |
| §7 shadow defaults | ~~`method_notes.md`~~ → `SSoT_CONTRACTS.md` | **RETARGETED, see §3 below.** Applied in full, different home |
| §8 probe status | `map-pipeline-by-layers-ssot.md` §F2 + `CURRENT_STATE.md` | resolved as **option 1** (register the coupling); promotion/removal still undecided and recorded as such |
| carried-forward UNVERIFIED (4) | `CURRENT_STATE.md` §Open observations | applied, still labelled |

### `X1_Pending_Doc_Updates.md` — APPLIED
| Item | Target | Disposition |
|---|---|---|
| §1.1–1.2 Phase X1 registration | `PCG_Roadmap.md` | applied (phase list entry + full section) |
| §2.1 status date | `CURRENT_STATE.md` | superseded by W-aux.f's date |
| §2.2 X1.a surface | `CURRENT_STATE.md` | applied, **with an added note that R4/R6 measured backing is now historical** |
| §2.3 `DeepWater` correction | `CURRENT_STATE.md` | **verified in-session**: the file already described `DeepWater` correctly as border-connected flood fill, so the item reduced to an interpretation note ("% of sea", not "% of deep ocean") |
| §3 changelog entry | `changelog-ssot.md` | applied |
| §4 closes `W-aux.c` §8.1 | queue + `CURRENT_STATE.md` | applied |
| §5 coverage-matrix row | `coverage-matrix.md` | applied (was optional; taken) |
| §6 legibility relation | `PCG_Roadmap.md` | applied inside the standing thread |
| §7 not-changed record | — | still accurate for X1.a |

---

## 2. Census corrections found by reading the governed files

The session brief listed four documents as pending that were **already updated**. Verified
against file content, not checklists:

| Document | Claimed pending | Actual state |
|---|---|---|
| `Phase_Q_Design.md` | Q §3.2–3.5 | already applied (blocking note for §3.1 present at L262) |
| `Phase_W_Design.md` | W §1.1–1.3 | already applied (§9 resolved at L229; Ellipse decision at L140) |
| `tileset-import-guide.md` | Q §5 | already applied (placeholder workflow at L251+) |
| `coverage-matrix.md` | Q §4 | already applied (rows at L51–52) |

Consequence: **Wave C rewrote no design document or guide.** Also corrected: the brief
counted two changelog entries for W-aux.e; there is one. Five new changelog entries were
written, not six.

---

## 3. One deviation from a queue's stated target

`W-aux.f` §7 targets `method_notes.md`. That document is the **skill-construction method**
notebook (§0 external canon, §1 auditor family with two-case evidence, §4 generative-shape
generalization) with an explicit append-only change discipline; a Unity test-fixture rule has
no section it can occupy without breaking that discipline. The rule was applied **in full**
to `SSoT_CONTRACTS.md` instead, adjacent to the calibration inventory that depends on it.
`method_notes.md` is unchanged this session. Reversible if the original target is preferred.

---

## 4. Still blocked (not unblocked here, by instruction)

| Item | Blocker | What would unblock it |
|---|---|---|
| `Phase_Q_Design.md` §3.1 — starter asset naming and path | open decision `Phase_Q_Pending_Doc_Updates.md` §9.1 | a user decision: adopt `<SetName>_BiomeOverride` under `Runtime/PCG/Samples/PCG Map Tilemap/Tilesets/` (matches guide and tree) and rename `TestBiomeTileOverride.asset` |
| `changelog-ssot.md` — W.a world-scale golden | data does not exist in any governed file | re-run seed 56 @ 64×64 with the world preset and capture the ten hashes. Code/session work, not documentation |

## 5. Parked or undecided, explicitly

- **Probe promotion/removal.** The two probes are *retained* (decision confirmed). Whether
  `LogHeightHistogram` is promoted to a governed diagnostic surface or scheduled for removal
  is still open; the formula coupling is registered so the cost of leaving it is visible.
- **Q-BUG-2 test seam** (`Phase_Q_Design.md` §12) — recorded, not resolved. Blocks nothing.
- **X1 rule re-basing.** `R4` and `R6` cite runs that no longer describe the pipeline. Code
  work, unscheduled.
- **`BiomeTable` vegetation densities** have not been recalibrated since W-aux.d changed
  what they mean.
- **Hills window recalibration** — unblocked by W-aux.f, proposed, unscheduled, no batch
  label assigned.

## 6. Drift reported, not fixed (zero code changes, per constraint)

- `Stage_BaseTerrain2D` ↔ `BaseTerrainStage_Configurable` parity is still unasserted, and
  the height composition formula now exists in **three** places (the third being the
  ungoverned probe). Two consecutive batches had to patch multiple copies by hand.
- `MapGenerationPresetWizard` (the EditorWindow) has no automated test.
- `Vegetation` is not among the ten runtime golden hashes.
- `Default_MapPreset` @256 `Height` goldens were not recaptured after W-aux.f; the W-aux.e
  values are superseded with no replacement.
