# Phase T2.1-prep — Pending Doc Updates

Live doc-update queue. Unapplied governed content produced by the T2.1-prep batch
(2026-08-21), which wrote `Phase_T2_Design.md`.

**Status:** unapplied. Every anchor below was verified against the governed document text in
the authoring session (2026-08-21). Re-verify before applying; if a document has been edited
since, re-anchor rather than force the replacement.

**Authority note.** This file is a queue, not authority. Nothing here is implemented truth
until it lands in the destination document. `Phase_T2_Design.md` itself is planning
(`SSoT_INDEX.md` tier 5), never implementation authority.

**Dependency — read before applying anything.** Three T2 queues are live simultaneously and
their items collide on `SSoT_INDEX.md` and `PCG_Roadmap.md`. Fixed order:

1. `Phase_T2_0_Pending_Doc_Updates.md` — apply in full, in its own internal order.
2. This queue, §1 → §2.1 → §3.
3. This queue §2.2 **last of all**, because it declares the live-queue list empty.

Items §1.1 and §2.1 below **supersede** `Phase_T2_Pending_Doc_Updates.md` §1.4 and §3
respectively. Do not apply the originals: §1.4's replacement records the status `Not
started`, which is false the moment the design document exists, and §3's SEARCH no longer
matches the index (that queue's own NOT-APPLIED note predicted this and forbids
approximating).

**Physical prerequisite, not a text edit.** `Phase_T2_Design.md` must be committed to
`Documentation~/planning/active/Phase_T2_Design.md` before §1.1 and §2.1 are applied — both
point a governed surface at that path. User action; this queue does not perform it.

---

## Evidence backing these updates (session 2026-08-21)

Verified this session by direct file read:

- Neither `Phase_T2_Pending_Doc_Updates.md` nor `Phase_T2_0_Pending_Doc_Updates.md` had been
  applied: `CURRENT_STATE.md` still read status date 2026-08-20 with `Deferred / optional:
  H8b, T1, T2, J, K, P, Q2, X1.b.`; `PCG_Roadmap.md` still carried both open decisions
  unresolved and the T2.0 bullet without `DONE`; `changelog-ssot.md` still opened on
  `## W-aux.h`; `SSoT_INDEX.md` still read `**The only live queue.**`.
- `PCG_Roadmap.md` L174 `| Phase T1 | [`Phase_T1_Design.md`](Phase_T1_Design.md) | Complete |`
  — present exactly once.
- `SSoT_INDEX.md` §"Current active planning docs" lists the eight `planning/active/` design
  docs plus `crosscheck_roadmap.md`, with `Phase_T1_Design.md` immediately followed by
  `Phase_V_Design.md`.
- Package asmdef graph: `Islands.PCG.Runtime` (references `Islands.Runtime`,
  `Unity.Mathematics`, `Unity.Collections`, `Unity.Burst`), `Islands.PCG.Inspection`,
  `Islands.PCG.Adapters.Tilemap`, `Islands.PCG.Samples`, `Islands.PCG.Samples.Shared`,
  `Islands.PCG.Editor`, `Islands.PCG.Tests.EditMode`. No relief/mesh assembly exists.
- Package tree: `SteppedHeightSampler2D.cs` and `PCGSteppedReliefGizmoPreview.cs` in
  `Runtime/PCG/Inspection/`; `SteppedHeightSampler2DTests.cs` in
  `Runtime/PCG/Tests/EditMode/Inspection/`; all queue files physically in
  `Documentation~/archive/pending doc updates/`.
- `Phase_T1_Design.md` §2.1 names `Islands.PCG.Adapters.Shared` as the anticipated
  extraction home and states the third adapter makes extraction mechanical.
- Layer-priority resolution (entries low→high, last ON layer wins) lives in
  `TilemapAdapter2D.cs`.

Not verified, and no item below asserts otherwise:

- That the EditMode suite is green **after** any of these queues are applied. No item here
  touches code, so the batch-start green (user confirmation, 2026-08-21: clean compile, full
  suite green including the 11 `SteppedHeightSampler2DTests`, goldens unchanged) should
  carry — but it is a documentation batch, so re-run rather than assume.
- Whether `supersession-map.md` should carry a T1→T2.2 row. Flagged in §4, not applied.

---

## §0 — Register this queue in `SSoT_INDEX.md`

**Apply after `Phase_T2_0_Pending_Doc_Updates.md` §0.** Project rule: a live queue is
registered in the index *when written*, not when consumed.

**Document:** `SSoT_INDEX.md`
**Section:** "Current active planning docs" → "Live doc-update queues (unapplied governed content)"

**Anchor** — the text produced by `Phase_T2_0_Pending_Doc_Updates.md` §0:

```
- `Phase_T2_0_Pending_Doc_Updates.md` (unapplied 2026-08-21 — T2.0 implementation record,
  roadmap status, changelog entry, and the T1/T2 relationship decision). Not yet committed
  under `planning/active/`.
```

**Replacement:**

```
- `Phase_T2_0_Pending_Doc_Updates.md` (unapplied 2026-08-21 — T2.0 implementation record,
  roadmap status, changelog entry, and the T1/T2 relationship decision). Not yet committed
  under `planning/active/`.
- `Phase_T2_1_prep_Pending_Doc_Updates.md` (unapplied 2026-08-21 — design-doc registration
  and roadmap freshness for `Phase_T2_Design.md`; supersedes `Phase_T2_Pending_Doc_Updates.md`
  §1.4 and §3). Not yet committed under `planning/active/`.
```

If the apply-all session applies all three queues in one pass, §0 may be skipped and §2.2
applied instead — but only if §2.2 is genuinely reached in the same pass. Skipping §0 and
then stopping early is how invisible queues are created; when in doubt, apply §0.

---

## §1 — `PCG_Roadmap.md`

### §1.1 Design-doc table row — supersedes `Phase_T2_Pending_Doc_Updates.md` §1.4

**Anchor (verified 2026-08-21, unique at L174):**

```
| Phase T1 | [`Phase_T1_Design.md`](Phase_T1_Design.md) | Complete |
```

**Replacement:**

```
| Phase T1 | [`Phase_T1_Design.md`](Phase_T1_Design.md) | Complete |
| Phase T2 | [`Phase_T2_Design.md`](Phase_T2_Design.md) | Complete |
```

Status is `Complete`, not the superseded queue's `Not started`: the document exists and
covers every slice of the branch.

### §1.2 Phase T2 section header

**Anchor (verified 2026-08-21):**

```
### Phase T2 — 3D Relief Adapters (stepped, corner-height, voxel, wireframe)
**Planning. Not designed. Sequenced on adapter track, parallel to mainline.**
```

**Replacement:**

```
### Phase T2 — 3D Relief Adapters (stepped, corner-height, voxel, wireframe)
**Planning. Design complete — see [`Phase_T2_Design.md`](Phase_T2_Design.md). Sequenced on
adapter track, parallel to mainline.**
```

### §1.3 Status snapshot line — depends on `Phase_T2_0_Pending_Doc_Updates.md` §2.1

**Anchor** — the text produced by that item:

```
- Phase T2: in progress (T2.0 closed 2026-08-21 — adapter track, 3D relief family;
  design doc pending)
```

**Replacement:**

```
- Phase T2: in progress (T2.0 closed 2026-08-21 — adapter track, 3D relief family;
  design complete — `Phase_T2_Design.md`)
```

If that queue's replacement wrapped differently when applied, re-anchor on the actual
wrapping rather than forcing this pair.

### §1.4 Extraction trigger — record where it is now scheduled

**Anchor (verified 2026-08-21), last sentence of the "Extraction trigger already recorded"
paragraph:**

```
priority-resolution logic is already duplicated between `TilemapAdapter2D` and the T1
design. T2.1 is the point at which that extraction is due.
```

**Replacement:**

```
priority-resolution logic is already duplicated between `TilemapAdapter2D` and the T1
design. T2.1 is the point at which that extraction is due. **Scheduled 2026-08-21:**
`Phase_T2_Design.md` §1.5 fixes the destination — a new `Islands.PCG.Adapters.Shared`
assembly owning the low→high / last-ON-wins resolver, with entry types remaining
per-adapter; `TilemapAdapter2D` delegates to it in T2.1 under bit-identical behaviour.
```

---

## §2 — `SSoT_INDEX.md`

### §2.1 Planning-docs list — supersedes `Phase_T2_Pending_Doc_Updates.md` §3

Re-anchored against the index as it actually stands (the superseded item's two-line SEARCH
matches zero times).

**Anchor (verified 2026-08-21):**

```
- `planning/active/Phase_T1_Design.md`
- `planning/active/Phase_V_Design.md`
```

**Replacement:**

```
- `planning/active/Phase_T1_Design.md`
- `planning/active/Phase_T2_Design.md`
- `planning/active/Phase_V_Design.md`
```

### §2.2 Close the live-queues block — APPLY LAST

**Apply only once all three T2 queues have been applied in full and archived.** Applying it
earlier states something false and hides a live queue.

**Anchor** — the block as it stands after `Phase_T2_0_Pending_Doc_Updates.md` §0 and this
queue's §0:

```
Live doc-update queues (unapplied governed content):
- `Phase_T2_Pending_Doc_Updates.md` (partially applied 2026-08-21 — §1.4 and §3 blocked on
  `Phase_T2_Design.md` not existing). Not yet committed under `planning/active/`.
- `Phase_T2_0_Pending_Doc_Updates.md` (unapplied 2026-08-21 — T2.0 implementation record,
  roadmap status, changelog entry, and the T1/T2 relationship decision). Not yet committed
  under `planning/active/`.
- `Phase_T2_1_prep_Pending_Doc_Updates.md` (unapplied 2026-08-21 — design-doc registration
  and roadmap freshness for `Phase_T2_Design.md`; supersedes `Phase_T2_Pending_Doc_Updates.md`
  §1.4 and §3). Not yet committed under `planning/active/`.
```

**Replacement:**

```
Live doc-update queues (unapplied governed content):
- None. All three Phase T2 queues (`Phase_T2_Pending_Doc_Updates.md`,
  `Phase_T2_0_Pending_Doc_Updates.md`, `Phase_T2_1_prep_Pending_Doc_Updates.md`) applied in
  full and archived under `archive/pending doc updates/`.
```

Date the "None" line with the day it is actually applied, not 2026-08-21.

---

## §3 — Queue archival headers

Applied last, one per queue, after that queue's items are all in place.

### §3.1 `Phase_T2_Pending_Doc_Updates.md`

**Anchor (verified 2026-08-21):**

```
Status: **PARTIALLY APPLIED — 2026-08-21. LIVE. 5 of 7 items applied; 2 blocked.**
Blocked: **§1.4** (design-doc table row) and **§3** (`SSoT_INDEX.md` active-planning entry).
Both are conditional on `Phase_T2_Design.md` existing. It does not exist, in the package or
in project knowledge, so both stay unapplied — a table row and an index entry pointing at a
non-existent file are worse than their absence. **Do not archive this queue until
`Phase_T2_Design.md` is written and those two items are applied.** They exist nowhere else.
```

**Replacement:**

```
Status: **DISPOSED AND ARCHIVED.** §1.1–§1.3, §2.1, §2.2 applied 2026-08-21. §4 resolved by
user decision, recorded in place. §1.4 and §3 were **superseded, not applied**: their
blocker (`Phase_T2_Design.md` not existing) was cleared by the T2.1-prep batch, which
re-anchored both items into `Phase_T2_1_prep_Pending_Doc_Updates.md` §1.1 and §2.1 — §1.4's
replacement recorded a status of `Not started` that was false once the document existed, and
§3's SEARCH no longer matched the index. Do not apply §1.4 or §3 from this file.
```

### §3.2 `Phase_T2_0_Pending_Doc_Updates.md`

**Anchor:**

```
**Status:** unapplied. Each item is anchored against text verified in the governed document
on 2026-08-21. Re-verify the anchor before applying; if a document has been edited since,
re-anchor rather than force the replacement.
```

**Replacement:**

```
**Status:** APPLIED IN FULL AND ARCHIVED. Applied as the first of the three Phase T2 queues.
Anchor state at application time is recorded in the applying session's report.
```

### §3.3 This queue

**Anchor:**

```
**Status:** unapplied. Every anchor below was verified against the governed document text in
the authoring session (2026-08-21). Re-verify before applying; if a document has been edited
since, re-anchor rather than force the replacement.
```

**Replacement:**

```
**Status:** APPLIED IN FULL AND ARCHIVED. Applied as the last of the three Phase T2 queues.
Anchor state at application time is recorded in the applying session's report.
```

---

## §4 — Not included in this queue, deliberately

- **`changelog-ssot.md`** — no entry for T2.1-prep. Writing a planning document changes
  neither semantics nor authority; the same reasoning `Phase_T2_Pending_Doc_Updates.md` §5
  used. The next entry is due when T2.1 closes. (The T2.0 entry is a separate matter and
  lives in `Phase_T2_0_Pending_Doc_Updates.md` §3.)
- **`CURRENT_STATE.md`** — no edit. Nothing was implemented in this batch; the T2.0 record
  belongs to the T2.0 queue. Adding a design document to the implemented-baseline document
  would blur exactly the planning/implementation line the authority order exists to keep.
- **`SSoT_CONTRACTS.md`** — no contract changed. `MeshBuffers` and `LayerPriorityResolver`
  are designed, not implemented; if either ever hardens into a governed contract it is
  promoted explicitly, when it exists.
- **`coverage-matrix.md`** — no entry. Nothing owns a concept yet.
- **`supersession-map.md`** — **flagged, deliberately not applied.** `Phase_T1_Design.md` is
  *retained* as the specification of the T2.2 emitter, not superseded as a document, so the
  default reading is that no row is due; the roadmap annotation (T2.0 queue §2.5) carries the
  fact. Whether the map should nonetheless record "Phase T1 (as an independent adapter) →
  Phase T2 §T2.2" is a user call. If the answer is yes, it is a one-row addition, not part
  of this queue.
- **File moves and asmdef creation** — not documentation. `Islands.PCG.Adapters.Relief` and
  `Islands.PCG.Adapters.Shared`, and the relocation of the three T2.0 files, are T2.1 step 0,
  gated by their own green suite.

---

## Application order

1. `Phase_T2_0_Pending_Doc_Updates.md`, in full, in its own internal order.
2. This queue: §0, then §1.1, §1.2, §1.3, §1.4, then §2.1.
3. §3.1, §3.2, §3.3 (archival headers).
4. §2.2 last, once 1–3 are done.

`Phase_T2_Design.md` must be at `Documentation~/planning/active/` before step 2.

## Rollback

Every item is a literal pair; reverting means swapping SEARCH and REPLACE. §1.1 and §2.1
roll back by deleting the added line only. No item deletes existing content, so no rollback
loses text.
