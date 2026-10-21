@../Documentation~/project_instructions.md

## Claude Code only

- This folder is the `io.github.cebustama.islands` package and its Git repo. The Unity host project is `D:\Projects\Islands - Dungeon Generation Pipeline` (no Git); it references this package via `file:`.
- Governed docs live in `Documentation~/` (`SSoT_INDEX.md`, `CURRENT_STATE.md`, `changelog-ssot.md`, `planning/active/`). Read them from disk; there is no PK and no stale copy problem.
- Code truth is the working tree plus `git log` / `git status`. When `CURRENT_STATE.md` and Git disagree, report the divergence; do not resolve it silently.
- Claude Code cannot run Unity or the EditMode suite. Anything that needs compilation, tests or golden hashes stays labeled unverified until the user runs it and reports the result.
- Propose-by-default still holds. Edit files directly only when the user asks; with edits applied, a runbook is not needed.
- Unity creates a `.meta` file for every new file outside `~` and dot folders. Never hand-write or delete `.meta` files; if a file is moved or deleted, its `.meta` goes with it.
- The user writes in Spanish; answer in Spanish unless asked otherwise.
