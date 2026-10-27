# Research note — a text generator for Islands (2026-10-27)

**Type:** research (record, not authority). Nothing here changes Islands' plan, SSoTs or code. Placement and naming, if this ever goes ahead, are decided through Islands' own governance.

**Claudio's plan (2026-10-27):** recover the procedural text generator he built over several years for Band Manager (the original ALWTTT, `D:\Projects\Band Manager (SC) - LTS`). The plan has three steps:
1. demo and refine it on the website;
2. make it **part of Islands PCG as a text generator**;
3. use it in ALWTTT for the names of other artists and songs in the game world.

Full plan: `D:\Projects\banco-ideas-juego\proyectos\generador-de-texto.md`. Analysis of the original: `Docs/analisis-generador-de-nombres.md` in that repo.

**What it is (checked in the original code):** a generative grammar. It has two parts:
- a tagged vocabulary: words with inflections, type tags (`t#`), theme tags (`w#`) and a tag hierarchy;
- pattern strings with conditions (syllables, starts/ends with, length, avoid), captures (`%1`), letters, number ranges and unique/reference markers.

**Gaps to close before it fits Islands (inferred from Islands' conventions, to be confirmed against `Documentation~/`):**
- Determinism: without a seed the original calls `Random.InitState(DateTime.Now.Ticks)`, and it reads global state (`StaticGameData`). Islands needs an explicit seed and no global state.
- Data: the vocabulary is compiled to a 20 MB XML (each word is stored with its characters as an int array). A flat, lighter format fits better.
- Not started before ALWTTT's demo (11 Dec 2026).
