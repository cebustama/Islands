# SSoT Contracts

Status: Active
Purpose: Cross-cutting package contracts and governance-relevant technical rules.

## Documentary contracts
- Implemented truth must not live primarily in `wip/`.
- Planning must not be used as implementation authority.
- Reference docs may explain a system, but they do not overrule subsystem SSoTs.
- Historical docs must state their role explicitly once superseded.

## Package boundary contracts
- The governed docs root for this package is `Documentation~/`.
- Root `README.md` is a package entrypoint, not the governance spine.
- No second governance spine should be created under `Runtime/`, `Editor/`, or subsystem code folders.

## PCG cross-cutting technical contracts
- Determinism is a package-level expectation for the active PCG path.
- Stable execution order and stable hashing/golden verification are first-class governance concerns.
- Core PCG runtime follows a grid-first, adapters-last architecture.
- Legacy map-generation documents do not define the new PCG runtime unless explicitly re-promoted.

## Notes
This file is intentionally small at the start of migration.
It should grow only when a rule clearly spans multiple subsystem authorities.
