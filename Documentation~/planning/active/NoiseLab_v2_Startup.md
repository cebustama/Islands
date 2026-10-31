# Arranque: NoiseLab v2 (explorar, colores de mapa, código de parámetros)

Fecha: 2026-11-01. Rama: `main`. Sigue a `planning/archive/NoiseLab_Mobile_Build_Startup.md`.

## Verificado (con evidencia)
- NoiseLab (`Samples/0.1.0-preview/NoiseLab/NoiseLab.cs`, 314 líneas, IMGUI) funciona en el
  editor y en el móvil de Claudio (Samsung S22 Ultra, Android 15): Development Build por USB,
  tacto en vertical y horizontal, histograma como en el editor. Capturas de Claudio con
  seed 84253, frecuencia 3, 6 octavas, lacunaridad 2, gain 0,53 y 92 % de agua. Detalle en
  `changelog-ssot.md` («NoiseLab — Android build»).
- Suite EditMode 415/415 al empezar la sesión del 2026-10-31/11-01 (puente `ClaudeTests/`).
- `Simplex2D<Perlin>` («Simplex old») y `Simplex2D<PerlinOnSimplex>` («Simplex Perlin») usan el
  mismo gradiente (`BaseGradients.Square`); solo cambia la constante de escala
  (`Runtime/Noise/Noise/Noise.Gradient.cs`). Misma imagen, rango ±0,43 frente a ±0,98.
  `Simplex2D<Simplex>` usa `BaseGradients.Circle`: mismo esqueleto (misma rejilla y hash),
  otros detalles.
- Estructura de `NoiseLab.cs`: `Update` → `Sample()` (`Fill<N>`, puntos en el centro de cada
  celda en [0,1], `float4x3(px, 0, pz)`) → `Measure()` (copia ordenada, histograma de 64 bins
  en [-1,1]) → `Flood()` (umbral crudo o cuantil) → `Paint()` (gris para la tierra, azul por
  profundidad para el agua). Recalcula todo cuando hay `dirty`. Pantalla virtual de 800 de alto.
- Biomas en el runtime: `BiomeType` (12 biomas + `Unclassified`, Whittaker / Patel),
  `Stage_Biome2D` (temperatura por altura, latitud, costa y ruido; humedad por costa, ruido y
  `FlowAccumulation`), `BiomeColorPalette` (SO con colores por defecto, en
  `Runtime/PCG/Inspection/`), consumido por `PCGRuntimeOverlay` / `PCGMapTilemapVisualization`.
- Paletas de la web: `D:\Projects\web\claudiobustamante-site\src\data\palette-library.json`,
  9 paletas de **5 colores sin orden de altura** (Tablero C, Moebius, Ghibli, Torres del Paine,
  Valle de la Luna, Altiplano…). Claudio dio permiso (2026-11-01) para leer y usar lo que haga
  falta de otros proyectos.

## Sin verificar
- En qué escena del anfitrión vive `PCGMapTilemapVisualization`: su GUID
  (`834c084c…`) no aparece en ninguna `.unity` ni `.prefab` guardada en `Assets/`,
  `Packages/` ni `Samples/`. Quizá esté en una escena sin guardar. Preguntar a Claudio.
- Rendimiento al arrastrar: mover el offset recalcula 65 536 muestras × octavas en el hilo
  principal (C#, sin Burst) y vuelve a ordenar la copia en cada fotograma. En el S22 no se
  midió con arrastre continuo.
- `GUIUtility.systemCopyBuffer` en Android (debería funcionar; no se ha probado aquí).

## Decisiones tomadas por Claudio
- **Bandas de color por porcentaje** (como el agua): «el x % más alto es nieve». Más adelante,
  biomas, temperatura, humedad y cercanía al agua también decidirán el color.
- Los tres Simplex se agrupan en un botón «Simplex», con un selector de tres opciones debajo
  que solo aparece cuando Simplex está elegido. «Old» se queda en el selector (es la lección del
  rango).

## Decisiones abiertas (al principio, con recomendación)
1. **Bandas por defecto y sus porcentajes.** Propuesta: azul profundo, azul poco profundo
   (las dos dentro del agua, cortadas por profundidad relativa), playa, prado, bosque, ladera,
   roca y nieve, con cortes como porcentajes de la tierra. Recomendación: los porcentajes de
   tierra son editables y se guardan en el código de parámetros.
2. **Paletas de la web en un mapa de 8 bandas.** Tienen 5 colores sin orden. Opciones:
   (a) ordenarlas por luminancia e interpolar a 8; (b) asignar a mano cada paleta a las
   bandas; (c) usarlas solo como «tinte» sobre la paleta de mapa. Recomendación: (a) para
   empezar, con la paleta de mapa como la de defecto. Copiar el JSON a Islands como un
   `TextAsset` del sample, citando el origen.
3. **Moverse por el ruido.** Arrastrar con un dedo desplaza el offset; pellizcar hace zoom
   (cambia la frecuencia o la escala del dominio). Recomendación: arrastre + campo de offset
   ahora; pellizco solo si el arrastre va fluido. Mientras se arrastra, se pinta sin volver a
   ordenar (se recalculan la estadística y el cuantil al soltar), y se baja la resolución si
   el S22 no va fluido.
4. **Formato del código.** Propuesta: `NL1;t=simplex/perlin;s=84253;f=3;o=6;l=2;g=0.53;w=%92;`
   `x=0;y=0;pal=mapa;b=…`, con versión al principio y claves cortas. Pegar ignora las claves
   que no conoce. Botones Copiar y Pegar.
5. **Biomas en NoiseLab:** ¿ahora o en v3? Recomendación: v3. NoiseLab tiene un solo campo de
   ruido, y los biomas necesitan temperatura y humedad (otros campos más la costa). En v2,
   como mucho, dejar la paleta de mapa preparada para que una banda pueda pasar a ser un
   bioma más adelante.

## Tarea
NoiseLab v2, solo en el sample: (1) el botón Simplex con su selector; (2) offset con arrastre
táctil y campo numérico; (3) colores de mapa por bandas de porcentaje y selector de paletas
(mapa por defecto más las de la web); (4) código de parámetros para copiar y pegar.

DoD: las cuatro cosas funcionan en el editor y en el S22 (nueva build por USB); con el mismo
código, editor y móvil muestran el mismo mapa; el arrastre va fluido o la resolución en el
móvil se bajó con medición; suite 415/415; sin cambios de runtime; Claudio lo confirma.

## No rehacer
La escena v1, su lección, el arreglo del rango del ruido, la build Android (ajustes ya hechos),
las mediciones.

## Fuera de alcance
La app de portafolio; UI Toolkit; biomas reales con temperatura y humedad (v3); pasar el
muestreo a Burst (solo si el arrastre no va fluido ni bajando la resolución); arreglar
`Samples~`; Active Input Handling = Both (deuda en `CURRENT_STATE.md`, «Open observations»).

## Ritual de arranque
1. `git log --oneline -5` y `git status` en `D:\Projects\Islands`.
2. Con Unity abierto, `ClaudeTests/run.trigger` vacío en el anfitrión: 415/415.
3. Leer `NoiseLab.cs` entero y `BiomeColorPalette.cs` (colores por defecto).
4. Preguntar a Claudio en qué escena vive `PCGMapTilemapVisualization` y presentar las
   decisiones 1–5 con su recomendación. No escribir código antes de su respuesta.

## Archivos
- `Samples/0.1.0-preview/NoiseLab/NoiseLab.cs` (la escena; aquí van todos los cambios).
- `Runtime/PCG/Inspection/BiomeColorPalette.cs`, `Runtime/PCG/Layout/Maps/BiomeType.cs`
  (referencia de colores y biomas; no se tocan).
- `D:\Projects\web\claudiobustamante-site\src\data\palette-library.json` (origen de las
  paletas; solo lectura).
- `Documentation~/CURRENT_STATE.md`, `changelog-ssot.md` (al cerrar).
