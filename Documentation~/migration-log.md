# Migration Log

## 2026-03-23 — Initial governed scaffold + Batch 2 rescue landing

### Context
The migration began from a mixed legacy documentation tree with hidden authority concentrated in `Documentation~/wip/`.

### Decisions recorded
- Use `Documentation~/` as the governed docs root.
- Keep the full old docs tree as a frozen snapshot instead of rewriting it in place.
- Promote the new PCG direction now, but only where runtime evidence is already strong.
- Treat the implemented Map Pipeline by Layers slice as F0–F2 only.
- Keep F3+ in governed planning, not subsystem authority.

### Files created
- governance spine docs
- initial subsystem authorities for PCG
- active planning docs for PCG and the overall migration

### Remaining migration pressure
- legacy map-generation triage
- layout strategies verification
- subsystem hardening for noise / meshes / surfaces / graphs
