# Phase Q — Pending Documentation Updates

Status: **PARTIALLY APPLIED — 2026-08-18; re-verified 2026-08-20.** 16 of 17 items applied;
**1 blocked**.
Blocked: **§3.1** (starter asset naming and path) — blocked by open decision §9.1.
Re-verification 2026-08-20: the 16 applied items were checked against the governed files
themselves (`Phase_Q_Design.md` §3.2–3.5, `coverage-matrix.md`, `tileset-import-guide.md`,
`SSoT_INDEX.md`) and are present. Nothing was re-applied. §3.1 remains blocked; the
documentation-application session of 2026-08-20 did **not** unblock it, by instruction —
the decision is the user's to make.
The block is marked in place inside `Phase_Q_Design.md` §3.1 so the correction lands in
one pass once the decision is made.
Applied by: Islands.PCG documentation-application session, 2026-08-18.
Session (origin): Islands.PCG reconnection + Phase Q closure, 2026-08-09.
Scope: register Phase Q (implemented, previously unregistered), batch Q-fix.a, and utility Q-aux.a.

---

## 0. Why this document exists

Phase Q was found already implemented, with a complete design document and a complete
authoring guide, but **registered in no authority surface**. `CURRENT_STATE.md` still
declared Phase W as next focus, and `PCG_Roadmap.md` still declared Phase Q as
"planning (next focus)" with its design document "to be written when the phase activates".

This session verified the implemented baseline (R0–R7 all green), reviewed the found code,
fixed four defects (Q-fix.a), added an editor-only authoring utility (Q-aux.a), and
smoke-validated the result.

Nothing below has been applied. Each item states the target document, the anchor, and the
proposed change. Apply order does not matter; items are independent.

---

## 1. `CURRENT_STATE.md`

### 1.1 — `## Immediate next focus` (line ~288)

**Problem:** declares "Next batch: Resume toward **Phase W**" and lists Q under
"Deferred / optional". Both statements are now false — Q is implemented.

**Replace the `**Next batch:**` paragraph with:**

> **Next batch:** Phase Q closure follow-up. Phase Q (adapter-side biome-conditional
> tile selection) is implemented and smoke-validated as of 2026-08-09, including batch
> Q-fix.a (four defect fixes) and utility Q-aux.a (editor-only placeholder tile
> generation). Remaining Q-track work is authoring real tile art and, optionally,
> Q2 (composite-condition tiles).
>
> Long-term target remains **Phase W** (world-to-local architecture). The previously
> documented Phase P → Phase W sequencing is paused (not cancelled). Adapter-track
> phases T1 and Q2 remain independent and can be picked up at any time.

**Replace the deferred line:**

- Before: `Deferred / optional: H8b, T1, J, K, P, Q, Q2, W.`
- After: `Deferred / optional: H8b, T1, J, K, P, Q2, W.`

**Replace the adapter-side enrichment line:**

- Before: `Adapter-side enrichment (independent of W path): Q (biome-conditional tiles), Q2 (composite-condition tiles).`
- After: `Adapter-side enrichment (independent of W path): Q (biome-conditional tiles — DONE), Q2 (composite-condition tiles).`

### 1.2 — `## What is implemented now (confirmed for documentation authority purposes)` (line ~11)

**Add Phase Q to the implemented slice.** The implemented slice line should read
F0–N6 + M + M-fix.a/c + M2.a + M2.b + L + L→M + L-fix.a + V (V.a + V.b) **+ Q (Q + Q-fix.a)**.

### 1.3 — New resolution block under `## What current package development just resolved` (line ~21)

**Insert, following the existing block format:**

> ### Phase Q — Biome-Conditional Tile Selection (adapter-side)
>
> Closes the documented gap between Phase M (produces `MapFieldId.Biome`) and the tilemap
> adapter (previously ignored it entirely).
>
> - `BiomeTileOverride` ScriptableObject: additive per-biome tile overrides layered over a
>   base `TilesetConfig`. Flat `TileBase[]` lookup of size `BiomeType.COUNT × MapLayerId.COUNT`,
>   O(1) per cell, rebuilt lazily and on `OnValidate`.
> - `TilemapAdapter2D.ApplyBiomeAware` / `ApplyLayeredBiomeAware`: biome-aware stamping
>   overloads. A null override delegates to `Apply` / `ApplyLayered`, so pre-Q behavior is
>   byte-identical.
> - `PCGMapTilemapVisualization`: `biomeTileOverride` Inspector field, `StampMultiLayerBiomeAware`,
>   and override-content hashing for dirty tracking.
> - Zero new `MapLayerId`, zero new `MapFieldId`, zero new stages, zero changes to
>   `MapPipelineRunner2D`, zero golden impact. Purely adapter-side.
> - Hard biome boundaries by design. Transition blending remains Tier 1, not built.
> - `BiomeRegionId` (M2.b) is not consumed by Q — separate axis.
> - Mega-tiles (H8) run as a post-pass after stamping and therefore win unconditionally
>   over biome-varied tiles. Documented as a non-goal with defined behavior.
>
> **Batch Q-fix.a** — four defects found on review of the previously unregistered
> implementation, all adapter-side, all fixed and regression-tested:
>
> | Id | Defect | Fix |
> |---|---|---|
> | Q-BUG-1 | Override was ignored when the base layer entry had a null tile — layer masks were only cached when a base tile existed | Cache layer masks unconditionally; guard `if (resolved != null)` in the per-cell scan preserves `Apply()` parity |
> | Q-BUG-2 | The collider group was routed through the biome-aware path, so an override on `HillsL2` or `Lakes` replaced the collider sentinel tile with biome art | Collider group stamped separately via `ApplyLayered`; never biome-aware |
> | Q-BUG-3 | Biome field read with `(int)` truncation while `PCGHoverTooltip` (V.a) and `PCGRuntimeOverlay` (V.b) use `Mathf.RoundToInt` | Adapter now uses `Mathf.RoundToInt` |
> | Q-BUG-4 | The Phase Q hash block sat after the `tilesetConfig == null` early return, so override edits could miss dirty tracking | Hash block moved above the early return |
>
> Test coverage: `BiomeTileOverrideTests` 13 → 16 tests. Q-BUG-2 has no unit coverage —
> `StampMultiLayerBiomeAware` is private with no test seam — and is verified by the smoke
> protocol only. Logged as test debt.
>
> **Utility Q-aux.a** — `BiomeTileOverridePlaceholderGenerator`, editor-only. Generates
> flat-color placeholder `Tile` assets per (biome, layer) slot so a biome-conditional setup
> can be smoke-tested before any real art exists. Colors mirror the `BiomeColorPalette`
> defaults (V.b), making "painted tile color == overlay color" a direct cross-check.
> Zero runtime code.

### 1.4 — `## Visualization Maintenance Policy` (line ~259)

**Add, alongside the existing L-fix.a rule:**

> **Q-fix.a rule.** Any `MapLayerId` present in `s_colliderLayers` must not receive biome
> art through the biome-aware stamping path. The collider group is stamped separately via
> `TilemapAdapter2D.ApplyLayered` and must stay that way. `HillsL2` and `Lakes` are both in
> `s_colliderLayers` and both are plausible biome-override targets, so this is a live hazard
> rather than a theoretical one.

---

## 2. `PCG_Roadmap.md`

### 2.1 — `## Current status snapshot` (line ~74)

- Before: `- Phase Q: planning (next focus — adapter-side biome-conditional tile selection)`
- After: `- Phase Q: done (adapter-side biome-conditional tile selection; Q-fix.a + Q-aux.a folded in)`

**Also resolve the duplicated next-focus declaration.** With `CURRENT_STATE.md` §1.1 updated,
exactly one "next focus" is declared. Confirm no other phase line in this snapshot still
claims it.

### 2.2 — `## Documentary note on Phase Design Documents` table (line ~96–108)

**Add row:**

| Phase | Design document | Status |
|---|---|---|
| Phase Q | `planning/active/Phase_Q_Design.md` | Complete — implemented |

### 2.3 — Phase Q section body (line ~970+)

**Remove** the sentence stating the design document is "to be written when the phase
activates" — it exists and is complete.

**Remove** the open mechanism choices list; replace with pointers to the resolved decisions
Q-DD-1 (mechanism), Q-DD-5 (layer scope), Q-DD-6 (fallback).

**Add a Done subsection** following the format used by other completed phases, summarizing
the implemented surface and referencing `CURRENT_STATE.md` §1.3 for detail.

---

## 3. `Phase_Q_Design.md`

Path note: the document lives at `Documentation~/planning/active/Phase_Q_Design.md`.
Earlier session material referred to `planning/active/design/` — that subfolder does not
exist in the package tree.

### 3.1 — §3.1 Starter Asset

**Problem:** specifies `BiomeTileOverride-Starter.asset` under
`Samples~/0.1.0-preview/PCG Map Tilemap/`. The real tree has
`Runtime/PCG/Samples/PCG Map Tilemap/Tilesets/`, and `tileset-import-guide.md` §Phase 7
specifies the naming convention `<SetName>_BiomeOverride`.

**Decision needed — recommended:** adopt the guide's convention and path, since both match
the real tree. Update §3.1 accordingly.

**Also:** §3.1 describes a starter with 5 temperature clusters. The implemented
`Populate Default Biome Groups` creates 12 groups (one per biome except `Unclassified`),
each with 4 recommended layer slots. Align the text with the code.

### 3.2 — §6 Non-goals

**Add:**

> **Mega-tile (H8) interaction.** The mega-tile post-pass runs after the stamping pass and
> therefore overwrites biome-varied tiles unconditionally. A 2×2 mega-tile crossing a biome
> boundary is stamped whole, from the base tileset. This is defined behavior, not a
> decision left open — Phase Q does not change mega-tile resolution.

### 3.3 — Q-6 amendment

**Problem:** Q-6 states that the system does not prevent biome overrides on collider layers.
That permissiveness is the direct cause of Q-BUG-2.

**Replace with:** collider groups are excluded from the biome-aware path by construction.
`StampMultiLayerBiomeAware` stamps base and overlay via `ApplyLayeredBiomeAware` and the
collider group via `ApplyLayered`. Caller responsibility is documented on the
`ApplyLayeredBiomeAware` XML doc comment.

### 3.4 — §12 Open Items

**Replace "None" with:**

- Q-BUG-2 has no unit test coverage; `StampMultiLayerBiomeAware` is private with no seam.
  Options: accept smoke-only verification, or introduce a declarative `BiomeAware` flag on
  `TilemapLayerGroup` to make the exclusion testable at adapter level. Requires
  `TilemapLayerGroup.cs`, not reviewed this session.
- Q-aux.a color modulation: at ×0.60 brightness, `Vegetation` over dark biomes
  (`BorealForest` #2F4F35, `TropicalRainforest` #1F7535) renders near-black and reads as
  mush. Consider blending toward a fixed hue instead of multiplying. Cosmetic, debug-only.

### 3.5 — New section: Q-fix.a

**Add** a batch section recording the four defects, their fixes, and the three regression
tests, matching the table in `CURRENT_STATE.md` §1.3.

---

## 4. `coverage-matrix.md`

**Add rows to the main concept table:**

| Concept | Primary home | Role | Status |
|---|---|---|---|
| Phase Q — biome-conditional tile selection (adapter-side) | `planning/active/Phase_Q_Design.md` | design authority for implemented adapter slice | Active |
| Tileset import and biome tile authoring workflow | `reference/tileset-import-guide.md` | governed reference / implementation-time support | Active |

---

## 5. `tileset-import-guide.md`

Path: `Documentation~/reference/`.

### 5.1 — §Phase 7 (Wire biome-specific tile overrides)

**Add, before the "Creating the asset" numbered list:**

> **Placeholder-first workflow (Q-aux.a).** Real tile art is not required to validate a
> biome-conditional setup. After running *Populate Default Biome Groups*, use the Inspector
> header context menu → **Generate Placeholder Tiles (empty slots)**. This creates
> flat-color `Tile` assets under `<override folder>/Placeholders/`, one per (biome, layer)
> slot, colored from the `BiomeColorPalette` defaults with per-layer brightness and border
> modulation. Every biome present in the map becomes immediately visible, which makes the
> smoke test independent of which biomes a given seed happens to produce.
>
> `HillsL2` placeholders carry a 4px white border by design: `HillsL2` is in
> `s_colliderLayers`, so a white-bordered tile appearing on the collider tilemap is the
> visual signature of a Q-BUG-2 regression.
>
> **Generate Placeholder Tiles (overwrite all)** replaces every slot including real art
> (confirmation dialog). **Clear Placeholder Tiles** removes placeholder references while
> leaving real art untouched; delete the `Placeholders/` folder to remove the assets.

### 5.2 — §Minimum tiles for a smoke test

**Add a note:** the three-pair minimum assumes the target biomes exist in the generated map.
With placeholder generation (§Phase 7) this constraint disappears — all 48 default slots are
filled at once.

### 5.3 — §Common gotchas

**Add rows:**

| Symptom | Cause | Fix |
|---|---|---|
| Override assigned but a layer never varies by biome | Base `TilesetConfig` entry for that layer has no tile — pre-Q-fix.a behavior skipped it | Update to Q-fix.a; overrides now apply to layers with no base art |
| Biome art appears on the collider tilemap | Collider group routed through the biome-aware path | Q-fix.a fixes this; the collider group must be stamped via `ApplyLayered` |

---

## 6. `SSoT_INDEX.md`

**Add** `reference/tileset-import-guide.md` to the `reference/` list (line ~37–47).
It is currently an unlisted governed support surface.

---

## 7. `SSoT_CONTRACTS.md` and `map-pipeline-by-layers-ssot.md`

**No change.** Verified against the diff: Phase Q, Q-fix.a and Q-aux.a touch no pipeline
contract, no stage, no registry, no determinism gate. Recorded here so the absence of a
change is deliberate rather than an omission.

---

## 8. Application checklist

- [x] 1.1 `CURRENT_STATE.md` — next focus, deferred list, enrichment line — **applied,
      adapted.** The deferred list and enrichment line are applied verbatim. The
      `**Next batch:**` paragraph is *not* applied verbatim: it was written on 2026-08-09
      and declares Phase Q closure follow-up as the next batch with W as a long-term
      target. W.a, W-aux.a and W-aux.b have since closed, so the proposed text would have
      been false on arrival. Applied text keeps the item's substance (Q is done; P→W
      sequencing paused not cancelled; adapter track independent) and names **W.b** as
      the next batch.
- [x] 1.2 `CURRENT_STATE.md` — implemented slice line (extended past `+ Q` to include
      `+ W.a + W-aux.a + W-aux.b`; see EXT-1 in the session traceability table)
- [x] 1.3 `CURRENT_STATE.md` — Phase Q resolution block
- [x] 1.4 `CURRENT_STATE.md` — Q-fix.a visualization maintenance rule
- [x] 2.1 `PCG_Roadmap.md` — status snapshot
- [x] 2.2 `PCG_Roadmap.md` — design document table row (path `planning/active/` per user
      decision D-4; the seven pre-existing rows pointed at a `design/` subfolder that does
      not exist in the package tree and were corrected in the same pass — see EXT-4)
- [x] 2.3 `PCG_Roadmap.md` — Phase Q section body
- [ ] 3.1 `Phase_Q_Design.md` — §3.1 starter asset naming and path — **BLOCKED by §9.1
      (starter asset naming and path).** Not applied. A blocking note is inserted in place
      at `Phase_Q_Design.md` §3.1 recording both known errors (non-existent
      `Samples~/0.1.0-preview/...` path; "5 temperature clusters" vs. the implemented 12
      groups) so nothing is applied half-way.
- [x] 3.2 `Phase_Q_Design.md` — §6 mega-tile non-goal
- [x] 3.3 `Phase_Q_Design.md` — Q-6 amendment
- [x] 3.4 `Phase_Q_Design.md` — §12 open items. **Applied in full.** This item *records*
      the Q-BUG-2 seam question as open; it does not require §9.2 to be resolved first.
      §9.2 remains open and is now documented inside `Phase_Q_Design.md` §12.
- [x] 3.5 `Phase_Q_Design.md` — Q-fix.a batch section (added as §10b)
- [x] 4 `coverage-matrix.md` — two rows
- [x] 5 `tileset-import-guide.md` — three edits (§5.1, §5.2, §5.3)
- [x] 6 `SSoT_INDEX.md` — one line
- [x] 7 no-change record (no action) — re-confirmed 2026-08-18: `SSoT_CONTRACTS.md` and
      `map-pipeline-by-layers-ssot.md` received no edit in the application session

---

## 9. Open decisions blocking full application

1. **Starter asset naming and path** (§3.1). Recommended: adopt
   `<SetName>_BiomeOverride` under `Runtime/PCG/Samples/PCG Map Tilemap/Tilesets/`,
   matching both the guide and the real tree. Requires renaming
   `TestBiomeTileOverride.asset`.
2. **Q-BUG-2 test seam** (§3.4). Accept smoke-only verification, or add a declarative
   `BiomeAware` flag to `TilemapLayerGroup`.
3. ~~**Whether a changelog entry is required.**~~ **RESOLVED 2026-08-18.** The governed
   changelog is `Documentation~/changelog-ssot.md`, already listed in `SSoT_INDEX.md`
   under "Current governance spine docs" and referenced by its short local update loop
   (step 5). This unblocks `Phase_W_aux_b_Pending_Doc_Updates.md` §6 (applied). It does
   **not** unblock `Phase_W_Pending_Doc_Updates.md` §3, which is blocked for a different
   reason: the W.a golden hash values themselves are not available in any governed file.

**Status of decisions 1 and 2 at 2026-08-18: both still open.** Decision 1 blocks §3.1.
Decision 2 does not block any item — §3.4 records it rather than resolving it.
