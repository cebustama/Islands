# Arranque: NoiseLab v3 (más de un campo)

Fecha: 2026-11-01. Rama: `main`. Sigue a `planning/archive/NoiseLab_v2_Startup.md`.

## Verificado (con evidencia)
- NoiseLab v2 cerrada (commit `b04522a`; detalle y mediciones en `changelog-ssot.md`,
  «NoiseLab v2»). Claudio la probó en el editor y en el S22 Ultra: el mismo código da el mismo mapa.
- `NoiseLab.cs` (sample, IMGUI, un solo campo de ruido): pestañas Noise, Map, Code y Perf.
  Flujo: `Update` → `Sample(n)` → `Measure()` → `Calibrate()` (referencia «world», 128²) →
  `Flood()` (cuantiles de la vista o del mundo) → `Paint()` (9 bandas planas).
- Coste lineal en píxeles: 256² ≈ 25 ms en el S22 (IL2CPP) y ≈ 480 ms en el editor (Mono).
  El móvil va a 30 fps por el tope por defecto de Android (`targetFrameRate` −1), no por el muestreo.
- Rendimiento: Perf → Clear log → mover → Export. En el editor, el archivo queda en
  `<anfitrión>/NoiseLabPerf/`. En el móvil se saca con
  `D:\Unity\Editor\6000.3.10f1\Editor\Data\PlaybackEngines\AndroidPlayer\SDK\platform-tools\adb.exe`
  desde `/storage/emulated/0/Android/data/com.ClaudioBustamante.SnakeCave/files/`
  (con `MSYS_NO_PATHCONV=1` en Git Bash).
- El sample compila en `Islands.Samples.asmdef`, que solo referencia `Islands.Runtime`.
  `ScalarSpline` (`Runtime/PCG/Fields/ScalarSpline.cs`) y `BfsDistanceField` están en
  `Islands.PCG.Runtime`: para usarlos hay que añadir esa referencia al asmdef.
- Con el agua y las bandas por percentil, una curva creciente sobre **un** ruido no cambia el
  mapa (el orden de las celdas es el mismo). Solo se ve con el umbral crudo y en el histograma.
  Lo que cambia la forma es combinar varios ruidos.
- Puente de tests: ya no corre la suite mientras Unity está en Play (cambio en el anfitrión,
  2026-11-01).

## Sin verificar
- Rendimiento con 2 o 3 campos a 256² en el móvil (calculado: unos 50–75 ms por muestreo).
  Medir con el export de Perf.
- Si `targetFrameRate = 60` (el S22 tiene pantalla de 120 Hz) mantiene el arrastre a 64² o 128²
  dentro de 16,7 ms. Las cifras dicen que sí (3,3 y 12,8 ms), pero no se ha probado.

## Material de entrada (investigación, no autoridad)
- `planning/active/NoiseLab_v3_Inputs.md`: 8 modos candidatos con esfuerzo y fuentes, y un
  núcleo propuesto, 1 + 4 + 3 (biomas con viento, rugosidad de costa, autómata celular).
- `research/playlist-pcg/backlog.md`: de ahí salen las tres propuestas de la sesión de la playlist:
  (a) tres ruidos (continentalidad, erosión, picos-valles) con splines que se editan moviendo
  puntos, con el relieve sombreado como prueba visual (Inputs n.º 2);
  (b) un modo «lejos de la costa» con `BfsDistanceField` que marque el punto más lejano
  (Inputs n.º 7);
  (c) dos paletas mezcladas según un segundo ruido (Tom Betts; no está en la tabla de Inputs).
- Fuera de NoiseLab: el autómata celular como operador del pipeline, las tablas de población,
  las mazmorras por grafo y el WFC son del roadmap de Islands, si Claudio los elige.

## Decisiones abiertas (al principio, con recomendación)
1. **Alcance de la v3.** Recomendación: dos piezas que comparten la base de «varios campos»:
   biomas (temperatura y humedad, decisión 5 de la v2) y los splines encadenados (a). El resto
   va a la v4 o a la web. Alternativa: el núcleo pequeño de Inputs (1 + 4 + 3).
2. **Referencia a `Islands.PCG.Runtime` en el asmdef del sample.** Hace falta para (a) y (b).
   Recomendación: sí, es un cambio de una línea en el sample y no toca el runtime.
3. **La clave `c=` del código.** Está reservada para una curva (puntos `x:y`). Con tres splines
   harían falta `c1=`, `c2=` y `c3=`, o `c=` con los tres separados por `/`. Recomendación:
   `c=` con `/`, para que siga siendo una sola clave.
4. **60 fps en el móvil.** Recomendación: `Application.targetFrameRate = 60` en NoiseLab y medirlo
   con el export.

## Tarea
La decide Claudio con la decisión 1. Reglas que siguen valiendo: CPU, determinista, misma
seed ⇒ misma imagen, sin `UnityEngine.Random` en el ruido, y todos los cambios en el sample salvo
acuerdo explícito.

## No rehacer
La v1 y la v2 (bandas, paletas, código, Perf), el arreglo del rango del ruido, la build Android
(ajustes hechos), las mediciones, el análisis de la playlist.

## Fuera de alcance
Burst (no hace falta según las mediciones), UI Toolkit, la app de portafolio, arreglar `Samples~`,
Active Input Handling = Both (deudas en `CURRENT_STATE.md`, «Open observations»).

## Ritual de arranque
1. `git log --oneline -5` y `git status` en `D:\Projects\Islands`.
2. Con Unity abierto y fuera de Play, `ClaudeTests/run.trigger` vacío en el anfitrión: 415/415.
3. Leer `NoiseLab.cs` (estructura arriba) y `NoiseLab_v3_Inputs.md`.
4. Presentar a Claudio las decisiones 1–4 con su recomendación. No escribir código antes de
   su respuesta.

## Archivos
- `Samples/0.1.0-preview/NoiseLab/NoiseLab.cs` (la escena).
- `Samples/0.1.0-preview/Islands.Samples.asmdef` (si se acepta la decisión 2).
- `Runtime/PCG/Fields/ScalarSpline.cs`, `Runtime/PCG/Layout/Maps/BiomeType.cs`,
  `Stage_Biome2D` (referencia; no se tocan).
- `planning/active/NoiseLab_v3_Inputs.md`, `research/playlist-pcg/backlog.md` (entrada).
- `Documentation~/CURRENT_STATE.md`, `changelog-ssot.md` (al cerrar).
