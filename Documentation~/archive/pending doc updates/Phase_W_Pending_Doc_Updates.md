# Phase W — Pending Documentation Updates

Status: **PARTIALLY APPLIED — 2026-08-18; re-verified 2026-08-20.** 7 of 8 items applied;
**1 blocked**.
Re-verification 2026-08-20: §1.1–1.4 confirmed present in `Phase_W_Design.md`. §3 remains
blocked for the same reason — the W.a hash values exist in no governed file, no test constant
and no captured log. A documentation session cannot fill them without inventing them; the
reserved slot in `changelog-ssot.md` is retained.
Blocked: **§3** (W.a golden registration). The blocker has *changed*: the governed
changelog path is now resolved (`Documentation~/changelog-ssot.md`), but the W.a golden
hash values were never written to a governed file, so the entry cannot be filled without
re-running the capture. A reserved-slot note is recorded in `changelog-ssot.md`.
Applied by: Islands.PCG documentation-application session, 2026-08-18.
Session (origin): Islands.PCG Phase W kickoff, 2026-08-09.
Scope: register the P→W sequencing deviation, close the five §9 open design questions,
fix the §10 dependency drift, and record the W.a world-scale smoke result.

To be applied together with `Phase_Q_Pending_Doc_Updates.md` in a dedicated
documentation session (after Phase W or W2). Items are independent unless noted.

---

## 0. Why this document exists

Phase W kickoff (2026-08-09) resolved the five open design questions in
`Phase_W_Design.md` §9, verified the F2c integration seam against source
(`MapShapeInput` / `MapInputs` / `Stage_BaseTerrain2D` — matches §4, no drift),
confirmed known drift in §10 (Phase M and Phase L listed as "Planning" though both
are implemented), and executed W.a: a 64×64 world-scale pipeline run from a
`MapGenerationPreset`, visually smoke-validated.

Nothing below has been applied. Each item states the target document, the anchor,
and the proposed change.

---

## 1. `Phase_W_Design.md`

### 1.1 — §10 dependency table (drift fix)

**Problem:** lists Phase M and Phase L as "Planning". Both are implemented
(baseline verified by execution 2026-08-09), including the L→M moisture coupling
that §7.3 treats as conditional.

**Replace the two rows:**

```diff
 | Dependency | Status | Required? | Role |
 |------------|--------|-----------|------|
 | Phase F2c (MapShapeInput) | **Done** | Yes | Primary integration hook |
-| Phase M (Biome classification) | Planning | Yes | BiomeType, MoistureLevel, TemperatureLevel |
-| Phase L (Hydrology) | Planning | Optional | Enriched MoistureLevel |
+| Phase M (Biome classification) | **Done** | Yes | BiomeType, MoistureLevel, TemperatureLevel |
+| Phase L (Hydrology) | **Done** (incl. L→M moisture coupling) | Optional | Enriched MoistureLevel |
 | Phase K (Plate Tectonics) | Planning | Optional | ElevationEnvelope at geological scale |
```

### 1.2 — §9 open questions → resolved

**Replace the entire §9 table and heading with:**

> ## 9. Design Questions (Resolved 2026-08-09)
>
> | Question | Resolution | Rationale |
> |----------|-----------|-----------|
> | World map resolution | Configurable (already is, by contract — `GridDomain2D` in `MapInputs`); default 64×64 via world-scale preset | The pipeline is resolution-agnostic; fixing a resolution in code would add a constraint no contract requires. Decision lives in data, not contracts. |
> | `WorldTileContext` delivery | Struct + deterministic translation builder → existing `MapInputs`; no new interface; `MapInputs` signature unchanged | Every property already has an entry point: `LocalSeed` → seed, shoreline → `MapShapeInput`, rest → `MapTunables2D`. `IMapWorldContext` would be speculative design for W2/streaming, both explicit non-goals. |
> | Ocean tile behavior | Zoomable; generates all-ocean local map (all-OFF F2c mask). "Not selectable" is client policy, not generator policy | Uniform invariant (every tile generates) beats a special-case branch; falls out of the F2c design for free; keeps layout headless. |
> | Shape mask derivation | 3×3 world land/water neighborhood + bilinear interpolation + threshold | Single-tile footprint cannot represent *where* the world coastline crosses the tile, which is §4's stated goal. Still purely local and deterministic (reads 9 world cells, no neighbor local maps — does not violate the no-boundary-matching non-goal). Produces plausible coasts, not matching edges; matching remains W2. |
> | Phase W2 scope and timing | Deferred; sequenced after Phase W DoD, with evidence from observed W worlds | W's only obligation toward W2: local generation stays a pure function of `(worldSeed, tileX, tileY, worldContext)` so edge constraints can later be added as input without breaking determinism. |

### 1.3 — §3 `ShorelineMask` adjustment (consequence of Q4 resolution)

**Add note under the §3 property table:**

> Note (2026-08-09, per Q4 resolution): `ShorelineMask` is not stored per tile as a
> pre-baked `MaskGrid2D`. The tile carries the 3×3 world land/water neighborhood
> samples; the local-resolution `MaskGrid2D` is derived at zoom-in time by the
> shape-mask builder (W.c) via bilinear interpolation + threshold.

### 1.4 — §5 world shape mode ✅ CONFIRMED 2026-08-18 — APPLIED

The W.a smoke run used `shapeMode = Ellipse` (world = one large island) and passed.
The kickoff's provisional recommendation was `NoShape` (world = noise-derived
continents). **User decision 2026-08-18: `Ellipse` is canonical** — islands keep an
island silhouette as the base. Archipelago support is expected to arrive as *additional
influence layers* composed on top of that base, each island still reading as an island;
a later, better-grounded model of island formation (volcanic, etc.) may refine this.
That is future work, not a change to the Phase W base identity.

Applied to `Phase_W_Design.md` §5 accordingly.

---

## 2. `PCG_Roadmap.md`

### 2.1 — Register the P→W sequencing deviation

**Anchor:** Phase W entry (and Phase P status line).

**Add to the Phase W entry:**

> **Sequencing deviation (2026-08-09):** Phase W proceeds before Phase P. Rationale:
> no rubric yet exists for what distinguishes a good world from a bad one; building
> `IMapValidator2D` without criteria would produce a retry loop with no semantics.
> The rubric will be distilled from observing W-generated worlds. Phase P is paused,
> not cancelled; it resumes with criteria derived from W output.

**Phase P status line:** change to `paused (resumes with rubric derived from Phase W output)`.

### 2.2 — Phase W status

Change Phase W from planning to `active (W.a complete 2026-08-09; W.b next)` —
apply only once W.a's golden hash is captured and registered (see item 3).

---

## 3. W.a golden registration (fill in at doc session)

W.a DoD requires a captured console hash golden for the world-scale run
(seed=56, res=64, world preset). The hash-log patch to
`PCGMapTilemapVisualization` was proposed in-session; once applied and run:

> World-scale golden (2026-08-09): seed=56, 64×64, preset `[asset name]`:
> Land=0x…, Height=0x…, Temperature=0x…, Moisture=0x…, Biome=0x…, Rivers=0x…, Lakes=0x…

**Status 2026-08-18 — BLOCKED, for a changed reason.** The destination is now settled:
the governed changelog is `Documentation~/changelog-ssot.md`
(`Phase_Q_Pending_Doc_Updates.md` §9.3, resolved). What is missing is the **data**. The
`0x…` placeholders above were never filled: the hash-log patch to
`PCGMapTilemapVisualization` was proposed in-session and the resulting hashes exist in no
governed file, no test constant and no captured console log available to the
documentation session. No value can be written without inventing it.

A reserved-slot note has been recorded in `changelog-ssot.md` so the gap is visible.
**To unblock:** re-run seed 56 @ 64×64 with the world preset, capture the ten hashes, and
fill this item.

---

## 3b. Application checklist (added 2026-08-18)

- [x] 1.1 `Phase_W_Design.md` — §10 dependency table drift fix (M and L → Done)
- [x] 1.2 `Phase_W_Design.md` — §9 open questions → resolved
- [x] 1.3 `Phase_W_Design.md` — §3 `ShorelineMask` derivation note
- [x] 1.4 `Phase_W_Design.md` — §5 world shape mode — **unblocked** by user decision
      (Ellipse), applied
- [x] 2.1 `PCG_Roadmap.md` — P→W sequencing deviation + Phase P status line → paused
- [x] 2.2 `PCG_Roadmap.md` — Phase W status → active. **Applied despite the item's own
      precondition.** The item made this conditional on §3's golden being registered;
      §3 is blocked on missing data. Leaving Phase W as "later (planning / exploratory
      only)" with W.a, W-aux.a and W-aux.b closed would have been worse drift than
      applying it, and W-aux §8 requires the Phase W entry to exist as a host for the
      W-aux track. Confirmed by the user as decision D-3.
- [ ] 3 W.a golden registration — **BLOCKED: hash values unavailable** (see §3)
- [x] 4 coordination note (no action)

---

## 4. Coordination with `Phase_Q_Pending_Doc_Updates.md`

Both documents touch `PCG_Roadmap.md`. Q's item changes line ~74
("Phase Q: planning (next focus)"); W's items 2.1–2.2 change the Phase W and
Phase P entries. No textual collision; apply order does not matter. Q's three
open decisions (starter asset name/path, Q-BUG-2 test seam, changelog path) remain
open and block only Q's own items — except item 3 above, which shares the
changelog-path decision.
