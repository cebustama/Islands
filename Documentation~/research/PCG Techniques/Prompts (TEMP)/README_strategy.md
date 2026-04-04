# PCG Research — Multi-Pass Strategy Guide

## Why The Original Single-Pass Prompt Failed

The original prompt asked for output equivalent to a 40,000–60,000 word technical reference
in a single synthesis pass. The research phase handled it (1,035 sources), but the synthesis
step — where the model must actually write the report — likely hit generation limits or timed out.

Two structural problems compounded the length issue:

**No success criterion.** The prompt specified scope ("cover these techniques") but not quality
("implementable without additional sources"). Without a criterion, Research mode stops when context
feels full rather than when the task is genuinely complete.

**Rigid 12-subsection format per technique.** Forcing every technique into 12 fixed slots creates
padding where sources are sparse and truncation where they are dense. Research mode performs
better when given content requirements (what must be covered) rather than document structure
(how to organize it).

---

## The Fix: 6 Focused Passes

Each prompt is scoped to produce a manageable document (~5,000–10,000 words) that covers
one technique family in depth. The prompts in this folder are already the improved versions
— they apply the principles in `GeneralPlaybooks/Providers/Claude/Claude_Research_Prompt_Construction_Guide.md`.

| Pass | File | Covers |
|------|------|--------|
| 1 | pass1_noise_improved.md | Noise functions, PRNGs, sampling, scalar fields |
| 2 | pass2_terrain_and_erosion.md | Heightmaps, erosion, caves, voxel terrain, marching cubes |
| 3 | pass3_dungeons.md | BSP, room-corridor, graph-driven, CA caves, lock-and-key |
| 4 | pass4_rules_and_constraints.md | WFC, L-systems, grammars, autotiling, Wang tiles |
| 5 | pass5_biomes_placement_regions.md | Voronoi, biomes, vegetation, roads, scattering |
| 6 | pass6_multiscale_synthesis.md | Chunks, LOD, pipelines, composition patterns, final synthesis |

---

## How To Run Them

1. Run each pass as a separate Research mode conversation with Extended Thinking enabled
   (Research enables it by default).
2. Sonnet 4.6 may be preferable to Opus 4.6 for synthesis-heavy passes — it is faster and
   less likely to hit generation limits on long outputs. Research quality should be similar.
3. Run passes 1–5 in any order (they are independent).
4. Run pass 6 last — it is the integrative pass that references the others. You can paste
   key findings from passes 1–5 as context before running pass 6.

---

## Tips To Maximize Success

These reflect the principles in the prompt construction guide. The prompts in this folder
already apply them — this section explains the rationale.

**Each prompt has an explicit success criterion.**
The criterion tells Research mode when it has covered a technique well enough to move on.
Without it, the model stops when context feels full rather than when the task is complete.
Example criterion used here: "a developer should be able to implement it without consulting
additional sources."

**Content requirements replace rigid document structure.**
The prompts specify what must be covered (algorithm, variants, failure modes, combinations)
but not how to organize it. Research mode produces better output when allowed to let the
shape of found material determine structure.

**Failure modes are explicitly requested with named artifacts.**
Research mode will describe correct behavior without prompting. It will not automatically
cover failure modes, named artifacts, or common misimplementations unless asked. Each prompt
requests: name the artifact, give the proximate cause, give the fix.

**Comparison and synthesis sections are placed at the end.**
Placing comparisons at the end of the prompt prevents them from biasing individual technique
sections. The model covers each technique on its own terms first, then consolidates.

**Scope exclusions are explicit.**
Each prompt says "Do NOT cover X, Y, Z" to prevent scope creep. Without this, Research mode
will try to cover everything and the synthesis will balloon.

**Citation behavior is specific.**
Each prompt specifies: cite the source immediately after each concrete claim, and mark
unconfirmed claims explicitly. "Cite sources throughout" is too vague — it produces citations
grouped at paragraph ends rather than attributed per claim.

---

## If A Pass Still Fails

If synthesis produces placeholder output or truncates:
- Try splitting the pass further (WFC alone is a viable single-pass topic)
- Remove the comparison sections and run them as a separate short query with the main
  output as context
- Check whether the issue is consistently with synthesis (placeholder output) vs. content
  quality (synthesized but shallow) — they have different fixes

If Research mode is active but Claude does not appear to be searching:
- Steer explicitly: "Claude, please use the Research tool to [task]"
- This is the documented prompting mechanism, not a workaround

---

## After All Passes Complete

You will have 6 focused documents totalling ~30,000–60,000 words. Options:

1. Use them as-is — each is a self-contained reference for its topic.
2. Ask Claude to create a master index or table of contents linking all six.
3. Use pass 6's "Final Synthesis" section as the executive summary for the whole set.
4. Ask Claude to merge selected passes into a unified document (note: merging all six
   at once may hit the same generation limit that motivated the multi-pass approach).

---

## Alternative: Fewer, Broader Passes

If 6 passes is too many, consolidate to 3:

| Pass | Covers |
|------|--------|
| A | Noise + terrain + erosion (passes 1+2) |
| B | Dungeons + rules/WFC + biomes/placement (passes 3+4+5) |
| C | Multi-scale + composition + synthesis (pass 6) |

This risks the same generation-limit problem at a smaller scale. Monitor output length on
pass A before committing to the broader pass B.
