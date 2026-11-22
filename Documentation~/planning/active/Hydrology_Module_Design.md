# Diseño: módulo de hidrología (ríos, erosión, meandros, deltas)

Documento de diseño, **propuesta**: no es autoridad hasta que cada paso tenga su arranque y Claudio lo cierre. Manda el código; el estado del paquete está en `CURRENT_STATE.md` y en `Phase_L_Design.md`.

- **Origen:** la captura de los mapas de Harold Fisk (1944) y el lab web «El río que se mueve», publicado el 2026-11-17 (https://claudiobustamante.com/labs/islands/meanders/). Claudio, al cerrarlo: «más que cerrar la tangente, me gustaría incluirla en la planificación para el diseño de NoiseLab e Islands, en el componente de hidrología del motor».
- **Orden aceptado por Claudio (2026-11-17):** H1 ríos → H2 erosión con edad → H3 meandros → H4 deltas. Va **después de NoiseLab v6** (2b y 2c del módulo de fluidos), y el foco general sigue en ALWTTT.
- **Entrada en el roadmap:** PL-14 (`PCG_Roadmap.md`).
- **Investigación:** [`../../research/2026-11-17-meanders-deltas-hydrology.md`](../../research/2026-11-17-meanders-deltas-hydrology.md) (fuentes y técnicas) y `../../research/fisk-1944/` (texto del informe).

## 1. Qué hay hoy (comprobado el 2026-11-17)

| Dónde | Qué | Estado |
| --- | --- | --- |
| Paquete, Phase L (`Runtime/PCG/Layout/Maps/Stages/Stage_Hydrology2D.cs`, `Operators/HeightFieldHydrologyOps2D.cs`) | Priority-Flood+ε desde la costa (Barnes 2014, `SortedSet` como montículo), D8 con orden fijo de vecinos (0=N, horario), acumulación de flujo de alto a bajo (`MapFieldId.FlowAccumulation`, cuenta de celdas), ríos por umbral (`totalLandCells × riverThresholdFraction` → `MapLayerId.Rivers`), lagos = agua que no es mar profundo ni anillo costero (`MapLayerId.Lakes`, filtro por área opcional). Sin RNG. Contratos L-1..L-4. | Hecho, con goldens. Detrás de `enableHydrologyStage`. |
| Paquete, Phase M | `riverMoistureBonus` y `riverFlowNorm`: los ríos suben la humedad del bioma. Bit a bit igual con L apagada. | Hecho. |
| Paquete, tilemap | Ríos y lagos en la capa base; los lagos bloquean el paso, los ríos no (L-fix.a). | Hecho. |
| Paquete, Q2 (roadmap) | Rasgos compuestos: cascada (`Rivers ∧ HillsL2`), marmitas, vado, puente, humedal (`Vegetation ∧ adyacente(Rivers ∨ Lakes)`), desembocadura. | Idea. |
| NoiseLab (`Samples/0.1.0-preview/NoiseLab/NoiseLab.cs`) | Altura C + E·PV con splines; clima: temperatura por latitud, humedad con viento y sombra de lluvia, viento que rodea el relieve (proyección de presión), nubes; corrientes marinas (Stommel) y giros; vista World ×16. | Hecho (v3–v6). Siguiente: 2b, temperatura del mar y Humboldt. |
| NoiseLab, tierra | **Nada de agua en tierra:** ni ríos, ni lagos, ni erosión. El campo «Erosion» es un spline de rugosidad (E en C + E·PV), no erosión física. | **Hueco que cubre este módulo.** |
| Web | Modelo de meandros en JS puro y portable (`web/claudiobustamante-site/src/scripts/islands/meander.js`), banco (`tools/islands/meander-bench.mjs`) y lab publicado. | Hecho el 2026-11-17. |

## 2. La idea en una línea

El agua de la lluvia que NoiseLab ya calcula baja por el relieve (H1), lo talla con el tiempo (H2), serpentea donde la pendiente se acaba (H3) y deposita lo que lleva al llegar al mar (H4). Cada paso lee el anterior y deja capas que el juego puede usar: ríos con caudal, valles, lagos, humedales, deltas y un **campo de edad del terreno**.

## 3. Escala: por qué cada paso vive en un nivel distinto

NoiseLab pone los polos a ±8 anchos de vista del ecuador (`LatitudeSpan = 8`), así que de polo a polo hay 16 anchos. **Si** el mundo fuera del tamaño de la Tierra (~20.000 km de polo a polo; supuesto, no decidido), un ancho de vista mide ~1.250 km, y a 256² una celda mide ~5 km (a 64², al arrastrar, ~20 km).

| Fenómeno | Tamaño típico | Dónde se ve |
| --- | --- | --- |
| Cuencas y red de ríos (H1) | 100–1.000 km | Vista normal y World ×16 |
| Valles tallados (H2) | 1–50 km | Vista normal |
| Cinturón de meandros (H3) | ~8 km de ancho (banco: 26 × 7,8 km con W = 200 m) | **Solo con zoom** ×16 o más: nivel país → celda |
| Delta (H4) | 10–200 km | Vista normal, con más detalle al acercarse |

Consecuencia: H1 y H2 son mapas de mundo; H3 necesita el zoom de tres niveles (mundo → país → celda; PL-7, la idea de después de la v6) o una vista propia de «acercarse a un tramo». Es la decisión D-H3-1.

## 4. H1 — Ríos en NoiseLab (lluvia → caudal → ríos y lagos)

**Objetivo:** ver en NoiseLab por dónde va el agua: ríos con caudal, cuencas y lagos, alimentados por la lluvia del clima de la v6 (no solo por el área que drena).

**Algoritmo (port de Phase L, mismo orden de operaciones):**

1. **Relleno de depresiones**, Priority-Flood+ε desde la costa, con los mismos vecinos y desempates que `HeightFieldHydrologyOps2D.FillDepressions`. En Burst el `SortedSet` no sirve: montículo binario sobre `NativeArray` con clave (altura, índice), que da el mismo orden. Variante a medir: Priority-Flood con cola simple para las celdas planas (Barnes 2014, mejora 2).
2. **D8** igual que `ComputeFlowDirectionsD8` (vecino de mayor pendiente; diagonales a √2; no-tierra = nivel del mar).
3. **Acumulación ponderada:** en vez de contar celdas, cada celda aporta su **escorrentía** `r = max(0, lluvia − evaporación(temperatura))`, con la lluvia del campo de humedad del clima (`moist`, que ya incluye viento y sombra de lluvia). Con `r = 1` en todas partes da exactamente la acumulación del paquete (prueba de paridad).
4. **Ríos** por umbral de caudal (fracción del total, como el paquete, o absoluto); ancho visual ∝ √caudal. En World ×4/×16 un río es más fino que una celda: al reducir, conservar la cobertura (que la celda gruesa sea río si lo es alguna fina con caudal suficiente) en vez de promediar, o desaparece como el tallo de una flor lejana (captura del 2026-11-22 en `../../research/2026-11-capturas.md`).
5. **Lagos:** dos fuentes. (a) Las del paquete (agua interior por la máscara). (b) **Nuevas:** depresiones rellenadas con profundidad `relleno − altura > umbral`. Si la cuenca de un lago recibe menos escorrentía que lo que evapora su superficie, el lago **no desagua**: cuenca endorreica, lago salado o salar (enlaza con PL-3, salares, y con la 2b: el desierto costero de Humboldt).

**Capas nuevas en NoiseLab:** *Rivers* (líneas con grosor por caudal), *Flow* (log del caudal, como mapa de calor), *Basins* (cada cuenca de un color, con la paleta del sitio), *Lakes* (con los endorreicos de otro tono). Encajan en el sistema de capas superpuestas de la v4.

**El problema del borde (D-H1-1):** el caudal de un punto depende de todo lo que hay aguas arriba, que puede quedar fuera de la vista. Opciones:
- **A.** Rejilla gruesa fija al mundo, como las corrientes de la 2a: cuencas y caudal se calculan una vez por región, y la vista fina solo dibuja. El mapa no cambia al arrastrar (la regla de la v5).
- **B.** Margen fuera de la vista, como el viento de la v5. Más simple, pero un río largo puede venir de más lejos que cualquier margen.
- Recomendación: **A**, con la vista fina rellenando el D8 local y heredando el caudal de entrada de la rejilla gruesa.

**Medir:** ms por paso en el PC y en el S22 a 64² y 256²; paridad con el paquete (`r = 1`, misma rejilla → misma `FlowAccumulation` celda a celda, comparando con `HeightFieldHydrologyOps2D` desde un test o una exportación); % de tierra que drena al mar frente a endorreica; ríos del lado húmedo frente al lado de sombra de lluvia (debería haber diferencia clara).

**Contratos:** determinista (sin RNG; la misma semilla y los mismos ajustes dan el mismo mapa), ríos ⊆ tierra, cada celda de tierra drena a la costa o a un lago endorreico, Burst = C# gestionado bit a bit (como en la v5).

**Web (opcional):** capítulo de Islands Lab «Adónde va la lluvia» (D8, relleno, caudal), antes o después del port.

## 5. H2 — Erosión con edad (FastScape)

**Objetivo:** que el relieve tenga **edad**: valles tallados por los ríos de H1, montañas gastadas, llanuras donde se acumula sedimento. Un solo parámetro visible: la edad (número de pasos de tiempo).

**Algoritmo:** ley de potencia de la corriente, resuelta de forma implícita en O(n) por paso (Braun y Willett 2013):

- `∂h/∂t = U − K · A^m · S^n` (+ difusión de ladera `κ ∇²h`, opcional).
- `A` = caudal de H1 (ponderado por la lluvia: así la sombra de lluvia también erosiona menos). Los receptores son los del D8 y el orden es la pila de aguas abajo a aguas arriba que ya da la acumulación.
- Con `n = 1` cada celda se resuelve directamente a partir de su receptor ya actualizado; sin iteración ni inestabilidad para cualquier `dt`.
- Cada paso: relleno → D8 → acumulación → actualización implícita. Repetir N pasos (la edad).

**En NoiseLab:** una capa más sobre C + E·PV: `altura_erosionada = FastScape(C + E·PV, edad)`. Todo lo que lee la altura (biomas, clima, viento, costa) lee la versión erosionada. Antes / después con un deslizador de edad (como el del lab de meandros).

**Coste a medir:** 256² = 65.536 celdas por paso, con varias decenas o cientos de pasos. El relleno es lo caro; medir si basta con rellenar cada K pasos. Presupuesto: S22 al soltar, como el muestreo de la v5 (13–35 ms); si no cabe, precálculo al cambiar la semilla o la receta, y caché.

**Hacia el paquete:** es la propuesta 4 de PL-12 («erosión con ríos temporales y una edad») y lo que recomienda la matriz de técnicas: erosión hidráulica **antes** de Phase L. Sería una etapa nueva (`Stage_Erosion2D`, nombre provisional) antes de `Stage_Hydrology2D`, detrás de un interruptor y bit a bit igual apagada. Mueve goldens cuando se encienda: pasa por gobernanza (diseño de fase propio).

**Decisiones:** D-H2-1, ¿difusión de ladera desde el principio o después? D-H2-2, ¿`n = 1` fijo (rápido y estable) o `n ≠ 1` con Newton (más fiel)? Recomendación: `n = 1` y sin difusión en la primera versión.

## 6. H3 — Meandros (el lab web, portado)

**Objetivo:** donde un río de H1 / H2 llega a una llanura, que serpentee, corte cuellos y deje lagos en herradura y cursos fósiles, como en las láminas de Fisk. Deja el **campo de edad del terreno**.

**Lo que ya existe:** `meander.js` (web), que sigue a meanderpy paso a paso: curvatura con diferencias centrales, tasa nominal `R0 = kl·W·curv`, convolución aguas arriba `R1 = ω·R0 + γ·ΣR0·G/ΣG` con `G = exp(−α·s)`, `α = 2Cf/D`, ω = −1, γ = 2,5, corte a 1e-4 y precálculo de `decay`; `R1 · sinuosidad^(−2/3)`; desplazamiento por la normal con tope 0,5Δs; corte del cuello con `crdist` y banda diagonal; remuestreo Catmull-Rom a Δs. Búsqueda de cortes por **hash espacial** (misma respuesta que la fuerza bruta). Semilla con `SmallXXHash`. Huella FNV-1a con redondeo al mm. Medido: 12 curvas y 3.000 pasos → 521 → 1.086 puntos, **0,31 ms por paso** en Node, 29 cortes, sinuosidad 1,00 → 2,09.

**Port a C#:**
- Funciones estáticas puras sobre arrays planos, como `HeightFieldHydrologyOps2D`. Gestionado primero; Burst solo si hace falta.
- **`double`, no `float`:** el JS usa `double`, y el modelo es **caótico**: el orden de las operaciones en coma flotante cambia el resultado (pasó con la semilla 2 al optimizar la convolución). Para la paridad, el mismo orden de operaciones y `double`.
- **Paridad JS = C#:** goldens desde el banco (`tools/islands/meander-bench.mjs`): huella de la semilla 1 (`2ae3a22e`) y de otras semillas y ajustes, a 100, 1.000 y 3.000 pasos. Si una huella diverge, el primer paso en que lo hace dice qué operación cambió de orden.

**Acoplamiento con el mapa:**
1. **Dónde:** tramos de río con pendiente < `s_min` y caudal > `Q_min` (de H1 / H2). Fuera de ellos el río sigue el D8.
2. **Curso inicial:** el camino D8 del tramo, suavizado y remuestreado a Δs, con los extremos anclados (entrada y salida del tramo no se mueven).
3. **Ancho:** `W` desde el caudal con la geometría hidráulica (`W ∝ Q^0,5`, Leopold y Maddock 1953; ⚠ exponente por comprobar en la fuente).
4. **Confinamiento:** meanderpy no tiene valle; aquí el cinturón debe quedarse en la llanura. Propuesta: `kl` local escalado por la pendiente lateral (casi cero en la ladera), como hace Paris et al. 2023 con la erodabilidad (⚠ leer el artículo antes; pendiente en la nota de investigación).
5. **Semilla por tramo:** `SmallXXHash(semilla, idTramo)` (propuesta 2 de PL-12), para que un tramo no cambie al cambiar otro.

**Salidas (rasterizadas a la rejilla del nivel de zoom):**
- *Rivers*: el curso actual.
- *Lakes*: los lagos en herradura, con su edad (se rellenan de arcilla en ~120 años en el lab, por Fisk p. 20).
- *Wetland*: herraduras recientes, cursos fósiles y tierra baja junto al cauce. Alimenta el `Wetland` de Q2.
- **`TerrainAge`** (campo nuevo): años desde que el cauce pasó por cada celda. Para el juego: suelos fértiles, humedales, lugares con historia («aquí estuvo el río»).

**Tiempo:** 4.000 pasos ≈ 1,2 s en JS para un tramo; precalcular por tramo al generar y guardar en caché. No se simula en vivo salvo en la vista de detalle.

**Decisión D-H3-1 (escala):** ¿vista de detalle propia («acercarse a un tramo») en NoiseLab, o esperar al zoom de tres niveles (PL-7)? Recomendación: vista de detalle propia primero (no bloquea), migrar al zoom cuando exista.

**Web:** el lab tiene pendiente el modo «valle» (un río desde un camino D8 sobre un mapa de NoiseLab; meandros solo donde la pendiente es baja), que es H3 en pequeño y sirve de prototipo.

## 7. H4 — Deltas (DeltaRCM-lite) y lo que viene después

**Objetivo:** en las desembocaduras con poca energía de olas y corrientes, el río deposita su sedimento y forma un delta con canales que se bifurcan y lóbulos que cambian.

**Algoritmo:** DeltaRCM (Liang, Voller y Paola 2015): paquetes de agua y sedimento que caminan al azar por la rejilla, con pesos por profundidad y dirección, y depositan donde el flujo se frena. Canales, bifurcaciones y cambios de lóbulo **emergen**. Hay port en Python (pyDeltaRCM, MIT). Es de rejilla, como Islands. **Antes de diseñar: leer las reglas de pyDeltaRCM** (pendiente en la nota de investigación).

**Acoplamiento:** caudal y sedimento de H1 / H2 en la desembocadura; las corrientes de la v6 deciden si hay delta (corriente débil) o estuario (corriente fuerte que se lleva el sedimento). Aleatorio, pero con semilla por desembocadura: determinista.

**Después de H4 (sin orden):** inundaciones y lagos que se llenan con tuberías virtuales (Mei, Decaudin y Hu 2007; Šťava et al. 2008), en Burst como el viento; ríos trenzados (Murray y Paola 1994); redes de ríos como grafo que crece desde la costa (Génevaux et al. 2013) como alternativa a la rejilla.

## 8. Cómo se conecta con lo demás

- **Módulo de fluidos (`Fluids_Module_Design.md`):** H1 lee la humedad y la temperatura que deja la 2c (evaporación según la temperatura). Por eso va después de la v6.
- **2b, Humboldt:** costa fría y seca → ríos que no llegan al mar y cuencas endorreicas (el Loa, que sí llega, es el caso que hay que poder reproducir).
- **Caso de prueba a escala de continente, el Sistema Pebas** (captura del 2026-11-17, `../../research/2026-11-capturas.md`): cordillera que sube + cuenca hundida a su lado + un arco que corta el desagüe = megahumedal en vez de río. H1 debería poder producirlo, y una divisoria baja debería decidir si desagua al norte o al este.
- **PL-3 (salares):** las cuencas endorreicas de H1 son su sitio natural.
- **PL-7 (zoom continuo) y los tres niveles:** H3 los necesita para verse en el mapa.
- **PL-12, propuesta 4:** es H2. **Propuesta 2** (semilla por capa y coordenada): semillas de tramo y desembocadura.
- **PL-13 (vistas del mismo mapa):** el relieve apilado con valles tallados es la demo natural de H2.
- **Q2:** *Wetland*, desembocadura, vado y cascada salen de las capas de H1 y H3.
- **Phase L del paquete:** H1 es su port a NoiseLab con caudal ponderado; si se adopta en el paquete, la acumulación ponderada es un cambio de contrato (cuenta de celdas → caudal) y pasa por gobernanza.
- **Web (Islands Lab):** un capítulo por paso: «Adónde va la lluvia» (H1), «Montañas que envejecen» (H2), «El río que se mueve» (H3, hecho), deltas (H4).
- **mundo-islas / videojuego:** ríos navegables y vados, lagos, humedales, y la edad del terreno como historia del lugar.

## 9. Gobernanza

- H1–H4 viven primero en **NoiseLab** (`Samples/`): no tocan `Runtime/` ni mueven goldens.
- Lo que pase al paquete (H2 como etapa antes de L, el caudal ponderado en L, H3 como etapa) necesita su diseño de fase, interruptor bit a bit igual apagado y goldens nuevos.
- El LLM no entra en el paquete (regla de LLM co-diseñador).
- Cada paso con arranque propio (`NoiseLab_H1_Startup.md`…) cuando le toque, con prueba en el editor y en el S22, como las versiones de NoiseLab.

## 10. Decisiones abiertas

| Id | Pregunta | Recomendación |
| --- | --- | --- |
| D-H-1 | ¿Escala del mundo (km por ancho de vista)? Afecta a W, caudales y si los meandros caben. | Fijarla al empezar H1; Tierra como supuesto de partida. |
| D-H1-1 | Borde de la vista: rejilla gruesa fija al mundo o margen. | Rejilla gruesa (A). |
| D-H1-2 | ¿Lagos endorreicos con balance lluvia − evaporación desde H1 o después? | Desde H1 (es poco código y da salares). |
| D-H2-1 | ¿Difusión de ladera? | Después. |
| D-H2-2 | ¿`n = 1` o `n ≠ 1`? | `n = 1`. |
| D-H2-3 | ¿H2 se calcula en vivo o se precalcula por semilla y receta? | Medir en el S22 y decidir. |
| D-H3-1 | ¿Vista de detalle propia o esperar a PL-7? | Vista propia primero. |
| D-H3-2 | ¿Confinamiento del cinturón por pendiente lateral? | Sí, tras leer Paris 2023. |
| D-H4-1 | ¿DeltaRCM completo o una versión mínima? | Mínima, tras leer pyDeltaRCM. |

## 11. Fuentes (estado de comprobación en la nota de investigación)

- Barnes, R., Lehman, C. y Mulla, D. (2014). Priority-flood. *Computers & Geosciences* 62, 117–127. (Ya citada en Phase L.)
- Braun, J. y Willett, S. D. (2013). A very efficient O(n), implicit and parallel method to solve the stream power equation. *Geomorphology* 180–181, 170–179.
- Howard, A. D. y Knutson, T. R. (1984). Sufficient conditions for river meandering. *Water Resources Research* 20(11), 1659–1667.
- Sylvester, Z. meanderpy (Apache-2.0), https://github.com/zsylvester/meanderpy.
- Paris, A. et al. (2023). Authoring and Simulating Meandering Rivers. *ACM TOG* 42(6). Código MIT.
- Liang, M., Voller, V. R. y Paola, C. (2015). A reduced-complexity model for river delta formation. *Earth Surface Dynamics* 3, 67–86. pyDeltaRCM (MIT).
- Leopold, L. B. y Maddock, T. (1953). The hydraulic geometry of stream channels. USGS Prof. Paper 252. ⚠ Sin leer.
- Fisk, H. N. (1944). *Geological Investigation of the Alluvial Valley of the Lower Mississippi River.* Texto en `../../research/fisk-1944/`.
