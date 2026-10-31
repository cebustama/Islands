# Arranque: escena didáctica del ruido (modo clase)

Fecha: 2026-10-31. Rama: `main` (el arreglo del rango del ruido ya está fusionado y subido).

## Verificado (con evidencia)
- Arreglo A + D en `main`: `Noise.PerlinOnSimplex` (2D `27(3−√3)/4`, 3D `3072/625`,
  1D `32/27`) y fBm normalizado por `1 + p + …`. Commits `c39308d`, `3ecadf4`, `1845399`;
  entrada «Noise range» en `changelog-ssot.md`.
- Suite EditMode 415/415 en verde (puente `ClaudeTests/`, 2026-10-31).
- Smoke test visual aprobado por Claudio el 2026-10-31 (`Default_MapPreset`, res 256,
  semillas 8, 243 y 110591): islas coherentes, sin alarmas.
- Rangos medidos (`NoiseRangeMeasurementTests`, 480 000 muestras; `reference/noise.md`):
  `Simplex2D<Perlin>` ±0,43; `Simplex2D<PerlinOnSimplex>` ±0,98; `Simplex2D<Simplex>` ±1,00;
  `Lattice2D<Perlin>` ±0,97. Terreno por defecto 0,16–0,84 (SD 0,117).
- Existe un visualizador de ruido: `Samples/0.1.0-preview/Noise/` (`Noise.unity`,
  `NoiseVisualization.cs`, shader graph de GPU, `Noise.mat`).

## Sin verificar
- Qué muestra y qué controles tiene hoy `NoiseVisualization.cs` (nadie lo ha leído en esta
  línea de trabajo).
- `package.json` declara el sample en `Samples~/0.1.0-preview`, pero la carpeta en disco es
  `Samples/` (sin `~`). Comprobar antes de decidir dónde vive la escena.
- Si el anfitrión importa o referencia ese sample.

## Decisiones abiertas (las toma Claudio, al principio)
1. **Dónde vive la escena:** ampliar `Samples/0.1.0-preview/Noise`, sample nuevo en el
   paquete, o `Assets` del anfitrión (sin git).
2. **Alcance del primer corte.** Propuesta mínima: selector de ruido (valor, Perlin cuadrado,
   simplex con `Perlin`, con `PerlinOnSimplex` y con `Simplex`, Voronoi), octavas, frecuencia,
   lacunaridad y ganancia; histograma en vivo (mín., máx., p1, p99); umbral de agua crudo
   frente a % de área. Pensada para pantalla vertical táctil, sin construir la app.
   Idea relacionada (2026-10-31, investigación, no autoridad): fijar el agua por porcentaje con el cuantil
   del mapa de alturas; ver `../../research/2026-10-31-hydrogen-cloud-sampling.md` (idea A).
3. **Tests:** la escena es de sample y no lleva goldens; si se añade código de runtime para
   el histograma, decidir si lleva test.

## Tarea
Construir la escena didáctica en **modo clase**: un paso cada vez, con su explicación en
lenguaje llano y una pregunta de comprobación, y cerrando cada bloque con «qué hice» y «qué
viene». Es el primer paso del port web → C# (las partes 2–4 de Islands Lab en la web, ruido
de valor, Perlin y simplex, son la referencia didáctica).

DoD: la escena abre y corre sin errores; los controles cambian el ruido en vivo; el
histograma coincide con los rangos medidos (±0,98 en `PerlinOnSimplex`, ±0,43 en el
gradiente viejo); Claudio la prueba en el editor; suite EditMode en verde; commit local, sin
push sin su OK.

## No rehacer
Mediciones, constantes, re-anclaje, changelog del ruido, smoke test. Ni W-aux.g ni
`waterThreshold01`.

## Fuera de alcance
La app de portafolio; port de puntos, artefactos y especies; documentación de W.c (deuda
aparte: CURRENT_STATE, changelog, `Phase_W_Design.md`); cambios en la web.

## Ritual de arranque
1. `git log --oneline -5` y `git status` en `D:\Projects\Islands`: rama `main`, al día con
   `origin/main`. `Artstyle References/` sin seguimiento no es de esta tarea: no tocar.
2. Con Unity abierto, crear `ClaudeTests/run.trigger` (vacío) en el anfitrión y leer
   `summary.txt`: debe dar 415/415.
3. Leer `Samples/0.1.0-preview/Noise/NoiseVisualization.cs` y comprobar lo de `Samples~`.
4. Presentar a Claudio las decisiones abiertas 1 y 2 con una recomendación; no escribir
   código antes de su respuesta.

## Archivos
- `Documentation~/CURRENT_STATE.md` (autoridad del estado); `reference/noise.md` (rangos).
- `Runtime/Noise/Noise/Noise.Gradient.cs`, `Noise.cs` (API de ruido y fBm).
- `Samples/0.1.0-preview/Noise/` (visualizador existente); `package.json` (declaración de
  samples).
- `Runtime/PCG/Tests/EditMode/Noise/NoiseRangeMeasurementTests.cs` (cómo se mide el rango).
- `.claude/CLAUDE.md` (cómo se corren los tests).
- Referencia didáctica, solo lectura: `D:\Projects\web\claudiobustamante-site`, Islands Lab
  partes 2–4.
