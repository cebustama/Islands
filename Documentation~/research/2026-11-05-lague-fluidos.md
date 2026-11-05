# Sebastian Lague, «Fluid Simulations» (6 videos): tutorial y notas

Fecha: 2026-11-05. Estado: **investigación, no autoridad** (manda `SSoT_INDEX.md`). Captura de
Claudio con `/capturar`.

- **Tipo y origen:** video (playlist), https://youtu.be/rSKMYc1CQHE y el resto de la lista
  *Fluid Simulations* de Sebastian Lague (2023–2025).
- **Lo que dijo Claudio:** «Antes de continuar con la v5 de NoiseLab, me gustaría revisar estos
  recursos sobre simulación de fluidos, que tengo guardados hace años. Probablemente podemos
  obtener ejemplos y código útil, técnicas, algoritmos y conceptos importantes. Analiza todos los
  videos y entrégame un resumen a modo de tutorial CatLikeCoding en un artefacto».
- **Qué es:** SPH (líquido de partículas), renderizado de fluidos, fluido en un planeta (mareas,
  SDF), humo en rejilla MAC (proyección de presión, rojo-negro + SOR, advección
  semi-lagrangiana, flotación, calle de Kármán) y dos *Bad Apple!!* (una máscara como frontera).
  Cuatro transcripciones completas y el código de `SebLague/Fluid-Sim` (Episode-01) y
  `SebLague/Smoke-Simulation`; los *Bad Apple* no tienen narración.
- **Artefacto (privado):** https://claude.ai/artifact/VWDgEtgeFPvqteWDTfzfRq, con demo SPH y
  túnel de viento con isla de ruido (umbral de altura → sólido).
- **Archivos en `fluidos-lague/`:** `tutorial.html` (fuente del artefacto), `notas-tecnicas.md`
  (notas largas por video, con minutos y lo que viene del código marcado [code]),
  `test-demos.js` (prueba en Node: `node test-demos.js tutorial.html`; 600 fotogramas sin NaN,
  divergencia media ~2·10⁻⁴).

## 💡 Uso posible aquí
1. **Viento que rodea el relieve:** máscara por umbral de altura + viento de fondo (+ curl) y
   **una sola proyección de presión**, sin tiempo. Variante nueva para `Fluids_Module_Design.md`.
2. **Solver:** rojo-negro + SOR, ω ≈ 1,7, iteraciones fijas; celdas sólidas con máscara de 4 bits
   (Neumann: quitar el término y dividir por los bordes abiertos).
3. **ψ frente a presión:** con varias islas, ψ necesita una constante desconocida por isla; la
   presión no. Análisis propio, por medir en el prototipo.
4. **Nubes:** advección semi-lagrangiana de un escalar por viento fijo, difusión con
   D·Δt/Δx² ≤ 0,25 y decaimiento; sombreado Beer–Lambert.
5. **Deriva:** trazadores con la bilineal escalonada y arrastre `v += (u − v)·k·Δt`.
6. **Determinismo:** su código no lo es (presión en caliente, bilineal por hardware, bitonic sort
   inestable, atómicos). Receta en la sección 15.2 del tutorial.

## Conecta con
- `planning/active/Fluids_Module_Design.md` (candidatos C, D, E).
- `planning/active/NoiseLab_v5_Startup.md` (candidatos 1 y 3).
- Web, Islands Lab: posible página «El viento rodea la isla» (sin hablar).
- `_comun/conexiones.md`, corriente de Humboldt.
- Juego de surf de lado (idea de Claudio, mismo día): `banco-ideas-juego/2026-11-05-surf-de-olas.md`, con prototipo «Mar de olas».

**Estado:** semilla.
