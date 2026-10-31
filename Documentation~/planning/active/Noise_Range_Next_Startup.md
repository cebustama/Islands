# Arranque: rango del ruido — smoke test, fusión y escena didáctica

Fecha: 2026-10-31. Rama de trabajo: `noise-range-fix` (sin push).

## Verificado (con evidencia)
- Arreglo A + D aplicado en `noise-range-fix`: `Noise.PerlinOnSimplex` (constantes exactas
  2D `27(3−√3)/4`, 3D `3072/625`, 1D `32/27`) y fBm normalizado por `1 + p + …`. Commits
  `c39308d`, `3ecadf4` (28 goldens re-anclados), `1845399` (docs).
- Suite EditMode 415/415 en verde tras el re-anclaje (puente `ClaudeTests/`, 2026-10-31).
- Medición C#: `NoiseRangeMeasurementTests` (en `main`, `32a19b6`). Terreno por defecto
  0,20–0,80 → 0,16–0,84; pico de simplex-Perlin 0,43 → 0,98.
- Claudio aceptó el +14 % de contraste sin recalibrar (2026-10-31).
- W.c commiteada en `main` (`d39dea0`), tests en verde.

## Sin verificar
- El aspecto visual de los mapas tras el arreglo: nadie lo ha mirado todavía.

## Decisiones abiertas
- Dónde vive la escena didáctica: ampliar `Samples/0.1.0-preview/Noise`, sample nuevo o
  `Assets` del anfitrión.
- Contenido mínimo propuesto: selector de ruido (valor, Perlin, simplex con los dos
  gradientes, Voronoi), octavas, frecuencia, lacunaridad y ganancia, histograma en vivo
  (mín., máx., p1, p99) y umbral de agua crudo frente a % del área. Pensada para pantalla
  vertical táctil, sin construir la app.

## Tarea
1. Smoke test visual (protocolo de `project_instructions.md`), `Default_MapPreset`, semillas
   56 / 8 / 243, `main` frente a `noise-range-fix`: siluetas, costa, colinas, ríos. Señales
   de alarma: islas partidas, mesetas planas, costas muy distintas.
2. Si Claudio lo aprueba: fusionar `noise-range-fix` en `main` (fast-forward) y quitar
   «pending visual smoke test» de `CURRENT_STATE.md`.
3. Escena didáctica del ruido, en modo clase.

## No rehacer
Mediciones, constantes, re-anclaje, changelog. Ni W-aux.g ni `waterThreshold01`.

## Fuera de alcance
Port de puntos, artefactos y especies; la app; la documentación de W.c (pendiente aparte);
cambios en la web.

## Ritual de arranque
1. `git log --oneline -6` y `git status` en `D:\Projects\Islands`; confirmar la rama.
2. Con Unity abierto, crear `ClaudeTests/run.trigger` (vacío) en el anfitrión y leer
   `summary.txt`: debe dar 415/415.
3. Pedir a Claudio el smoke test, con la lista de qué mirar.

## Archivos
- `Documentation~/CURRENT_STATE.md` (autoridad del estado), `changelog-ssot.md` (entrada
  «Noise range»), `reference/noise.md` (rangos medidos).
- `Runtime/Noise/Noise/Noise.Gradient.cs`, `Noise.cs`; `Runtime/PCG/Layout/Maps/MapNoiseBridge2D.cs`.
- `Samples/0.1.0-preview/Noise/` (visualizador existente).
- `.claude/CLAUDE.md` (cómo se corren los tests).
