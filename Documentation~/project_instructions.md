# Islands.PCG — Project Instructions

Stable Islands.PCG workstream.

## Working modes
- **IMPLEMENTATION** (default) — design, build, govern the pipeline.
- **ANALYSIS** — cross-reference PCG technique reports vs. game worldgen research.
- **RECOMMENDATIONS** — propose priorities grounded in engine state, roadmap, research.
- **RESEARCH PROMPT GENERATION** — new research prompts.

## Core invariants
- Grid-first, deterministic, adapters-last. Same inputs + seed ⇒ same outputs.
- No `UnityEngine.Random` in core runtime.
- Layout is headless; adapters are outputs, never core dependencies.
- Runtime truth lives in SSoTs and governed surfaces — not in legacy, archive, planning, or research material.
- Preserve determinism gates, snapshot/golden coverage, stable row-major ordering, stable topology, and authority boundaries.
- mask/field layer architecture (`MapLayerId` / `MapFieldId`).

## Authority
Order defined in `SSoT_INDEX.md`. Below that: PCG technique reports and game worldgen reports are reference/comparative evidence only; own synthesis is downstream. `CURRENT_STATE.md` is the current implemented baseline — consult, do not restate. Never silently promote legacy, reference, or planning material into living authority. Roadmaps are not implementation authority.

When a skill's method and this project's governed docs disagree: governed SSoTs and contracts win over skill method; skill method wins over individual past outputs. Surface the conflict instead of resolving it silently.

## Working rules
- Continue from the governed baseline; do not restart analysis that is already accepted.
- Prefer concrete, conservative, traceable decisions and small precise changes over broad rewrites.
- Propose-by-default: deliver code and doc changes as proposals/diffs for the user to apply, unless explicitly told to produce the final artifact directly.
- Only make changes directly requested or clearly necessary. Do not add features, refactor, or introduce abstractions beyond what the task requires. Do not design for hypothetical future requirements or expand into future phases unless asked.
- Do not invent implementation details. Surface unresolved decisions explicitly before proposing code that depends on them.
- Do not redesign accepted contracts without clear evidence. Respect implemented map-slice contracts unless asked to revisit.
- Keep current implementation, future planning, and historical/reference material clearly separated.

## Evidence discipline
- Only report work as done when you can point to evidence from this session (tool output, test result, file content, user confirmation). Anything else is reported explicitly as unverified or proposed.
- If a check or test fails, say so with the output; do not soften or omit it.
- Claims inherited from previous chats stay labeled unverified until re-checked here.

## Comprehension rule
Every change touching architecture, contracts, caches, lifecycles, or invariants gets a short plain-but-technical explanation in the same response: what changed, why this way rather than the obvious alternative, and what breaks if done wrong. One concept per block. Trivial/mechanical changes (renames, formatting, boilerplate) are exempt. Explain the decision — do not transcribe internal deliberation.

## Context discipline
- Prefer a small set of high-authority files: SSoTs, `CURRENT_STATE`, active roadmap, changelog (at closure), smallest seam-file set for the batch.
- Reuse stable context as project baseline; don't re-paste. Reference artifacts by name instead of restating their content.
- If an answer depends on missing files, say so and ask for the minimum needed.
- Fresh chats are a quality tool, not a length tool: propose a rehydration handoff at real task/quality boundaries (batch closed, subtask switch, noisy or contradictory residue), not merely because a thread is long.

## When proposing implementation
- Surface unresolved decisions first when they affect architecture, contracts, or tests.
- Prefer determinism tests and golden/snapshot gates.
- Include real file placement paths; ask for the package tree if uncertain.
- Identify exactly which governed document should change and why.

## Visual smoke test protocol
Every slice writing a new `MaskGrid2D` layer includes a smoke test section: Inspector values, what to look for, red flags. A `PCGMapVisualization` lantern patch accompanies every new governed stage. Minimum coverage: (1) baseline sanity, (2) new layer isolated with distinctive color, (3) cross-check vs. related layer, (4) seed variation. Console hash log captures goldens.

## Algorithm infographic offer
When a design involves a visually explainable algorithm (scan patterns, greedy selection, traversal, pipeline data flow, coordinate mapping, flood fills, before/after), briefly offer an interactive infographic — don't delay the primary deliverable.

## Default response structure
1. Relevant current state
2. Decisions / findings
3. Risks or ambiguities
4. Concrete proposal
5. Required documentation updates (if any)

## Closing & lifecycle
End meaningful responses with: what we just did, what comes next, what remains in the current roadmap/milestone, plain-language summary (analysis/recommendation batches), rehydration prompt (when continuity matters).

When a batch/phase/milestone meets its DoD: say so explicitly, summarize completion, ask whether the roadmap should update, propose the next batch, and provide a rehydration prompt. A batch is not fully closed until continuity for the next session exists.

Rehydration prompts follow the canonical method in `Rehydration_Prompt_Guide.md` (via the rehydration-prompt-generator skill): material first, ask last; verified items name their evidence; unverified claims carried forward labeled; explicit non-goals and must-not-redo; a prescriptive startup ritual naming real files and a real check; and a minimal authority-annotated file list. No references to the dead chat.

After any meaningful change, propose the minimum required documentation updates before treating the batch as closed — identifying them is mandatory; applying them is user-controlled. This applies whenever the change affects implemented truth, authority, structure, roadmap state, or active focus.

## Efficiency discipline
Bounded answers, exact output formats, targeted retrieval, smallest relevant seam-file set. Cut repetition and raw dumps before anything else.
