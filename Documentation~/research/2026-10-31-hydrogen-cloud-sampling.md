# Research note — sampling a density like a hydrogen orbital (2026-10-31)

**Type:** research (record, not authority). Nothing here changes Islands' plan, SSoTs or code.

**Source:** kavan, «Simulating Atoms in C++», YouTube, 2026-02-14, 13:52 (https://youtu.be/OSAOh4L41Wg). Code: https://github.com/kavan010/Atoms (`src/atom_realtime.cpp`, `src/schrodinger.py`, read on 2026-10-31). Web version: https://www.kavang.com/atom.

**Explainer (private wrapped):** https://claude.ai/artifact/B65FsnZW8tZ5nFjXjnvCPN — source kept next to this note in [`2026-10-31-hydrogen-cloud/wrapped.html`](2026-10-31-hydrogen-cloud/wrapped.html). Its physics was checked: ∫r²R² dr = 1.0000 and peaks at 1, 4 and 9 a₀ for 1s, 2p, 3d.

**Claudio (2026-10-31):** «Está absolutamente fantástico, me encantó! Guarda todo.»

## What the video does (min.)

1. Bohr model, built on purpose to be wrong (1:00–3:30): a circling charge radiates and falls (≈ 1.6 × 10⁻¹¹ s classically).
2. Photons and levels, Eₙ = −13.6 eV / n² (3:30–5:30); `excitedTimer` in the particle struct.
3. De Broglie: only waves that close around the proton survive → integer n (5:30–7:30).
4. 3D, and the cloud as |ψ|² instead of an orbit (7:30–9:00).
5. ψ = R_nl(r) · Y_lm(θ, φ): Laguerre for r, Legendre for θ, φ uniform (9:00–10:40).
6. **Inverse-CDF sampling** (10:40–11:45): tabulate r²R² (4096 bins) and sinθ·P_l^m(cosθ)² (2048 bins), normalise, `lower_bound(u)`.
7. **Probability current** (11:45–12:45): each point rotates about the axis with v = ħm / (mₑ r sinθ).

## Mechanisms worth taking to Islands

The physics is not the point. Three mechanisms are:

- **Sampling by a density** (inverse CDF; marginal + conditional in 2D).
- **Shape = radial profile × angular harmonic.**
- **Motion that does not change the density** (divergence-free flow).

## Ideas

| | Idea | Status | Checked against |
| --- | --- | --- | --- |
| A | **Sea level by percentage.** Inverse CDF (quantile) of the height map: ask for "40 % water", get the threshold. Independent of the noise range (the 2026-10-31 range fix moved coasts under a fixed threshold; a percentage would not). | fits the didactic noise scene | decision 2 of `planning/active/Noise_Didactic_Scene_Startup.md` («umbral de agua crudo frente a % de área») |
| B | **Placement by density.** 2D CDF (row marginal + per-row conditional, two binary searches per sample) for trees, villages, resources by humidity / height / biome. CDF decides *where*; Bridson or hashed priority decides *spacing*. Seeded hash, never `random_device`. | proposal | web `Documentation~/research/points-2d-results.md` leaves «variable r (density maps by biome)» open |
| C | **Harmonic island shapes.** R(r) × (1 + a·cos(mφ + phase)): m lobes = peninsulas and bays; a radial node (as in 2s) = atoll with lagoon. Today `IslandShapeMode.Ellipse` = radial smoothstep falloff + domain warp. A "Harmonic" mode would add m and amplitude. | proposal | `Runtime/PCG/Layout/Maps/IslandShapeMode.cs` |
| D | **Loops that close.** Same principle as the de Broglie wave: walking the angle with 1D noise leaves a seam; sampling 2D noise on a circle always closes. Round coasts, looping animations for the noise visualiser app, periodic tiles (integer frequency per period). | proposal | — |
| E | **Move without piling up.** The probability current is divergence-free; curl noise does the same for any field. Fog, sea currents, leaves, shoals. | proposal | curl already exists: `isCurl` in `Runtime/Surfaces/FlowJob.cs` |
| F | **Islands Lab page "Sampling a density"** (web): rejection vs inverse CDF vs alias method, measured like parts 5–6. The video's structure (wrong model first, then corrected) is the didactic scene's "modo clase". | proposal | web `src/data/islands-techniques.js` |

## Caveats found in the repo

- `schrodinger.py` is **biased**: it proposes r ~ Exp(n²) and θ uniform, accepts with |ψ|²/max, never divides by the proposal density nor includes r² sinθ, and `max_prob` grows while sampling (early samples over-accepted). The C++ sampler is correct. Good example for idea F.
- Hydrogen only (one electron). Colour scaling is cosmetic (`intensity * 1.5 * pow(5, n)`). Not deterministic (`random_device`).

## Q&A (2026-10-31)

### 1. Porting to the mobile app: what gives the most particles?

**Claudio:** «si quisiera portear la simulación a islands en la app de móvil, cual sería la forma de implementarlo que permitiera la mayor cantidad de partículas posibles? (Sampleos de probabilidad, según entendí). CPU? GPU? burst? Shadergraphs? Etc»

**Answer (agent):** GPU, and **stateless**: no particle buffer, no per-frame CPU work.

- **The key fact:** the flow for an eigenstate is a pure rotation about the axis with a constant angular velocity per particle, ω = ħm / (mₑ ρ²) with ρ = r sinθ. r and θ never change, so the position has a closed form: φ(t) = φ₀ + ω·t. Nothing has to be integrated or stored.
- **CPU, once per (n, l, m):** build the two tables and **invert** them into quantile tables (u → r, u → θ), e.g. 1024–4096 floats each, so the GPU does one texture fetch instead of a binary search. Plain C# or a Burst job; it is microseconds.
- **GPU, every frame, in the vertex shader:** particle index (`SV_VertexID` / instance ID) → hash(index, seed) (SmallXXHash already lives in Islands.Noise) → u₁, u₂, u₃ → r = Q_r(u₁), θ = Q_θ(u₂), φ₀ = 2πu₃ → φ = φ₀ + ω t → position. Colour from |ψ|² evaluated in the shader, or stored per bin.
- **Drawing:** `Graphics.RenderPrimitives` / `DrawProcedural` with no mesh; tiny quads (point size is unreliable on Metal/Vulkan in Unity) or 1-px points. **Additive blending, no depth write**: additive is order-independent, so no sorting.
- **Limit on a phone:** fill rate and overdraw, not the maths. Expected order: hundreds of thousands to a few million small additive points at 60 fps on a mid-range phone; **to be measured**, not promised. Smaller points and fewer overlapping pixels buy more particles than any CPU optimisation.
- **Beyond particles:** with enough samples the image converges to the density itself. A fragment shader that raymarches the analytic |ψ|² (kavan's `atom_raytracer.cpp`) costs per pixel, independent of particle count. A hybrid (raymarched density + a few thousand moving particles to show the flow) looks best for the cost.
- **Ranking:**
  1. Stateless vertex shader (above). Most particles, zero CPU; also works on WebGL2 (no compute needed), so the same idea serves the web Labs.
  2. Raymarched volume for the "infinite samples" look.
  3. Burst jobs + `ComputeBuffer` + `DrawMeshInstancedProcedural` — the pattern Islands.Noise already uses (`Runtime/Noise/Visualization.cs`). Only if particles must exist on the CPU (gameplay, collisions); then per-frame upload bandwidth becomes the limit.
  - Not recommended: Shuriken (CPU, tens of thousands); VFX Graph (needs compute, heavy, uneven mobile support). Shader Graph is fine for styling, but procedural placement from vertex ID is cleaner in hand-written HLSL (URP unlit).

### 2. What is the current, and where does the velocity come from?

**Claudio:** «Lo que no me quedó claro es la corriente, qué representa? Cómo se obtiene ese vector de movimiento y según qué parámetros?»

**Answer (agent):**

- ψ is complex: ψ = |ψ|·e^{iS}. |ψ|² says *where*; the phase S says *how it flows*. The probability current is j = (ħ/mₑ)·Im(ψ*∇ψ) = |ψ|²·(ħ/mₑ)·∇S, so the velocity field is **v = j / |ψ|² = (ħ/mₑ)·∇S: the gradient of the phase.** Analogy: density is how much water there is; current is how much crosses a window per second.
- For hydrogen, ψ_nlm = R_nl(r)·P_l^m(cosθ)·e^{imφ}. The only phase is S = mφ. In spherical coordinates ∇S = (1/(r sinθ))·∂S/∂φ·φ̂ = m/(r sinθ)·φ̂. Hence **v = ħm / (mₑ r sinθ)**, tangent to circles about the z axis.
- **Parameters:** only **m** (sign = direction) and the distance to the axis ρ = r sinθ. n and l do not enter the speed; they only decide where the points are. m = 0 → real ψ → no phase gradient → no motion.
- Scale: for m = 1 at ρ = a₀, v = ħ/(mₑa₀) = αc ≈ 2.19 × 10⁶ m/s. The same circulating current is what gives the orbital magnetic moment m·μ_B (Zeeman effect).
- Density does not change because ∂|ψ|²/∂t = −∇·j = 0 for stationary states: the flow runs along circles on which the density is constant.
- The lobed "chemistry" orbitals (pₓ, p_y, d_xy…) are real combinations of +m and −m: their currents cancel and they do not rotate. The rotating donuts are the complex ±m states.
- **Interpretation caveat:** in standard quantum mechanics the electron does not travel along these lines; j is a flux field. Moving points along it is exactly the de Broglie–Bohm (pilot-wave) picture: a valid, interpretation-dependent visualisation.
- kavan integrates it with an Euler step and re-projects onto the same r and θ; the closed form φ₀ + ωt is exact and cheaper.

## Further reading

- Griffiths & Schroeter, *Introduction to Quantum Mechanics*, 3rd ed. (2018), ch. 4 (hydrogen; probability current in ch. 1 problems).
- Luc Devroye, *Non-Uniform Random Variate Generation* (Springer, 1986), ch. 2 (inversion). Free on the author's site.
- Michael Vose, «A linear algorithm for generating random numbers with a given distribution», *IEEE Trans. Software Eng.* (1991) (alias method).
- Bridson, Hourihan & Nordenstam, «Curl-Noise for Procedural Fluid Flow», SIGGRAPH 2007.

## Continuing from Islands (2026-10-31)

Two possible threads; Claudio chooses at the start of the session.

1. **Inside the current focus (recommended):** the didactic noise scene (`planning/active/Noise_Didactic_Scene_Startup.md`), adding idea A (water by percentage via the height-map quantile) next to the histogram. This does not open a new front.
2. **A hydrogen cloud prototype** (new front, sample only, not runtime): stateless vertex shader as in Q&A 1. Open decisions: where it lives (sample in the package vs. host `Assets`), URP unlit HLSL vs. Shader Graph, point budget to measure on Claudio's phone. Before code: confirm it does not displace the didactic scene.

Startup prompt (paste in a session opened in `D:\Projects\Islands`):

> Lee `Documentation~/research/2026-10-31-hydrogen-cloud-sampling.md` y `Documentation~/planning/active/Noise_Didactic_Scene_Startup.md`. Quiero seguir con [1: la escena didáctica, con la idea A del agua por porcentaje | 2: un prototipo de la nube de hidrógeno en GPU]. Empieza por las decisiones abiertas; no escribas código hasta que las cierre.
