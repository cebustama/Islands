# GraphLibrary — Governed Reference

Status: Active governed reference / staged support  
Primary implemented truth: `Runtime/Graphs/GraphLibrary/*.cs`  
Promotion status: not promoted to subsystem SSoT after Batch 5.

## Purpose
This is the governed reference-facing home for GraphLibrary after Batch 5 and Batch 7 normalization.
It exists so the spine has a stable governed destination for GraphLibrary without treating runtime-local markdown as live authority.

## Current role
GraphLibrary is a real runtime support surface, but it has not earned promotion to subsystem authority.
The runtime code remains the implemented truth.
Historical technical explanation is retained inside the documentation tree, not in runtime-local markdown.

## Key boundary
- implemented truth: runtime code in `Runtime/Graphs/GraphLibrary/*.cs`
- governed reference-facing home: this file
- historical technical support: `Documentation~/reference/GraphLibrary_Pipeline_Technical_Doc.md`

## Not promoted at present
No `systems/graphs-ssot.md` is justified yet.
