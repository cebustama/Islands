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
5. **¿Algo de la playlist pasa al roadmap?** `PCG_Roadmap.md` no cambió. El backlog
   (`research/playlist-pcg/backlog.md`) propone, en orden, siete puntos para el pipeline:
   `moistureModulation` encendido en un preset (XS), validadores con mapas de distancia,
   operador de autómata celular con 8 vecinos, guardar la profundidad del relleno (lagos,
   salares), viento y sombra de lluvia en `Stage_Biome2D`, rugosidad de costa en `Inspection/`
   y splines encadenados. Claudio decide; no editar el roadmap sin su OK. Lo que se pruebe en
   NoiseLab es un prototipo: pasarlo a `Stage_Biome2D` es un cambio gobernado (se mueven goldens).

Orden de las capas según Inputs: núcleo 1 + 4 + 3; siguiente capa, splines (n.º 2) y nivel
del mar con lagos y salares desde la profundidad del relleno (n.º 6); v4 o web, el resto.

## Avance (2026-11-01, misma sesión)
- **Decisiones de Claudio:** 1A (biomas + splines encadenados; el viento si alcanza), 2 sí, 3 sí,
  4 sí, 5 no.
- **Hecho y en commit:** campos 1–5 con seed `seed + k·7919` y caché por campo; pestaña Shape
  (One noise / Splines, `altura = C(c) + E(e)·PV(pv)`, editor de curvas con histograma, Shade);
  pestaña Climate (Bands / Biomes, fórmulas de `Stage_Biome2D` con rango en tierra y latitud por
  la `y` del mundo, `BiomeTable.Lookup`, leyenda de Whittaker); Map → Show (mapa, altura,
  temperatura, humedad, ruidos en crudo); claves `h`, `n1…n5`, `c`, `k`, `cl`, `sh`;
  `targetFrameRate = 60`; referencia a `Islands.PCG.Runtime` en el asmdef. Suite 415/415.
- **Probado por Claudio en el editor:** splines y biomas funcionan («me gusta mucho el resultado»).
  Se vieron bordes de bioma rectos y diagonales: venían de la distancia chamfer (octógonos).
  **Arreglado** con la distancia euclídea exacta (Felzenszwalb y Huttenlocher, 2012; comprobada
  contra fuerza bruta, error 0). **Falta que Claudio lo vea.**
- **Observación para `CURRENT_STATE` al cerrar:** `CoastDist` del pipeline es BFS de 4 vecinos
  (contornos en rombo); con un decaimiento lento de la humedad, los biomas pueden seguir esos rombos.
  Cambiarlo es un cambio gobernado (goldens).
- **Medido de pasada:** 1.628 ms al soltar el arrastre con 5 campos a 256² en el editor (Mono).
  El móvil, sin medir.
- **Corrección:** la distancia euclídea no quitó las bandas rectas de la captura de Claudio (van a
  unos 27°; los octógonos solo daban 0°, 45° y 90°). Hipótesis sin comprobar: ruido de humedad de
  baja frecuencia con 2 octavas, casi un plano inclinado, cortado por las franjas de la tabla.
  Prueba: Layer → Moisture y Moist a 4 octavas. Si se confirma, poner 4 octavas por defecto en
  Temp y Moist.
- **Viento hecho** (`1d834c5`): una dirección y recorrido por líneas en O(n²); Climate → Wind
  (Weight, Lift, Dry, Sea), flecha en el mapa, capa «Wind air», clave `wd=`. Comprobado con una
  copia en Python (la sombra cae a sotavento en las 8 direcciones) y visto por Claudio en el
  editor («me encanta»).
- **Retoques de uso** (pedidos por Claudio): fondo de cámara `#22262E` (campo `background`),
  color por sección en los controles con la fila más profunda más clara, «Show» → «Layer»,
  curvas de nivel (Map → Contours, clave `ct=`), «Strength» → «Weight». Suite 415/415. Falta
  que Claudio los vea.
- **Pendiente para cerrar:** medir en el S22 con Perf (Splines + Biomes + viento, arrastrando);
  cierre (changelog, `CURRENT_STATE`, arranque de la v4, `proyectos.md` y `foco.md`).
- **Para la v4 (aceptado por Claudio):** vista dividida 2×2 con una capa por panel (primero) y
  capas superpuestas con +/− y una casilla cada una (después); nubes animadas con `Simplex3D`
  (z = tiempo) desplazadas con el viento y disueltas sobre las cumbres; los vórtices de von
  Kármán detrás de islas altas necesitan simular fluidos (Stam, 1999; sin verificar las
  imágenes de Alejandro Selkirk y Canarias). Bandas por altura absoluta como alternativa a los
  porcentajes.
- **Explainers para la web:** prompt en
  `web/claudiobustamante-site/Documentation~/planning/active/Splines_Biomes_Explainers_Startup.md`.

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
4. Presentar a Claudio las decisiones 1–5 con su recomendación. No escribir código antes de
   su respuesta.

## Archivos
- `Samples/0.1.0-preview/NoiseLab/NoiseLab.cs` (la escena).
- `Samples/0.1.0-preview/Islands.Samples.asmdef` (si se acepta la decisión 2).
- `Runtime/PCG/Fields/ScalarSpline.cs`, `Runtime/PCG/Layout/Maps/BiomeType.cs`,
  `Stage_Biome2D` (referencia; no se tocan).
- `planning/active/NoiseLab_v3_Inputs.md`, `research/playlist-pcg/backlog.md` (entrada).
- `Documentation~/CURRENT_STATE.md`, `changelog-ssot.md` (al cerrar).
