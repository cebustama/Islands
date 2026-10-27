# Referencias de PCG sacadas de los marcadores de Chrome

Status: Research, no es contrato  
Date: 2026-10-27  
Authority: Investigación exploratoria. No es verdad de implementación ni de roadmap.  
Home: `Documentation~/research/2026-10-27-referencias-desde-marcadores.md`

Fuente: análisis de los 2.223 marcadores de Chrome de Claudio (`D:\Projects\marcadores-chrome`: `catalogo.csv`, `profundo/fichas_pcg.md`, textos y transcripciones). Los números `i` son las filas de ese catálogo. No se leyó ni se tocó código de Islands para esta nota.

## Qué está comprobado y qué no

- **Comprobado:** que los enlaces existen y qué dicen (título, descripción, texto o transcripción descargados); el estado de las técnicas según `technique_integration_matrix.md` (2026-04-06) y `CURRENT_STATE.md`; qué cubre ya la carpeta `research/`.
- **No comprobado:** el estado real del código. La matriz es de abril: `CURRENT_STATE.md` ya da la fase L (hidrología) por implementada, mientras que la matriz la tiene en PLAN.
- Lo marcado como *inferido* es una propuesta mía, no un dato de la fuente.

## 1. Erosión (matriz: NONE, «pre-L»; única técnica de las 12 prioridades sin roadmap)

| `i` | Fuente | Qué aporta |
| --- | --- | --- |
| 1407 | Job Talle, [Simulating hydraulic erosion](https://jobtalle.com/simulating_hydraulic_erosion.html) | Método por gotas «simple y rápido» que busca terreno creíble, no realista. Descarta la simulación de agua en rejilla por rendimiento. |
| 1484 (dup. 1493) | Sebastian Lague, [Coding Adventure: Hydraulic Erosion](https://www.youtube.com/watch?v=eaXk97ujbPQ) | Implementación por gotas con demo. Segunda vía para comparar con 1407. |
| 1162 | [Estructura de datos para terreno multicapa 3D y erosión](https://old.reddit.com/r/proceduralgeneration/comments/unn7a4/an_efficient_data_structure_for_3d_multilayer/) | Hilo bloqueado: solo el título. Es 3D, que Islands no usa. |
| 1492, 1494 | Insydium (herramienta 3D); erosión en tiempo real con compute shaders (OpenGL) | Sin revisar a fondo. Posible referencia de GPU. |

Contexto del repo (comprobado en la matriz): la erosión que describe es la de Dwarf Fortress, con agentes voraces cuesta abajo, solo sustractiva y sin sedimento, como etapa `Stage_Erosion2D` entre G y L. Las gotas de 1407 y 1484 mueven sedimento (erosionan y depositan): otra familia.

*Inferido:* hay que decidir entre las dos antes de implementar. Las gotas dependen del orden de trabajo, el mismo problema que `points-distribution` señala en Poisson (orden, paralelismo, costuras entre chunks), y la matriz anota que L no muta `Height`, por lo que la erosión debería ser una etapa previa y propia.

**Próxima acción:** un documento de diseño de la erosión que compare DF-greedy, 1407 y 1484 con los criterios del proyecto (determinismo, paralelismo, costuras).

## 2. Rejilla dual, WFC y síntesis de modelos (no figura en la matriz)

| `i` | Fuente | Qué aporta |
| --- | --- | --- |
| 86 | Game Dev Buddies, [How One Guy FIXED Procedural Generation](https://www.youtube.com/watch?v=Y19Mw5YsgjI) (2026) | Stålberg (Townscaper, Bad North). Rejilla dual: una copia de la rejilla desplazada media celda; las piezas se modelan en ella y así las esquinas salen redondas, convexas y cóncavas. Piezas hechas a mano. Cita la síntesis de modelos (Merrell, 2007) y WFC (Gumin, 2016). |
| 1400, 1173 | Sunny Valley Studio, tutoriales de WFC en Unity (01 introducción; 02 teoría) | Tutoriales. Sin leer a fondo. |
| 1419, 1420 | Robert Heaton, [explicación de WFC](https://robertheaton.com/2018/12/17/wavefunction-collapse-algorithm/); repo [mxgmn/WaveFunctionCollapse](https://github.com/mxgmn/WaveFunctionCollapse) | La explicación clara y la implementación original. |
| 1469 | GDC, [WFC en Caves of Qud](https://www.youtube.com/watch?v=AdCgi9E90jw) | WFC en producción. |
| 1463, 1452 | AI and Games, [How Townscaper Works](https://www.youtube.com/watch?v=_1fvJ5sHh6A); Sergey Lerg, terreno en rejilla irregular tipo Townscaper (Godot) | Funcionamiento de Townscaper y una réplica pequeña. |

Comprobado: la matriz y `CURRENT_STATE.md` no tienen fila de WFC ni de rejilla dual. Sí hay investigación previa en esta carpeta (`PCG Techniques/pcg_technique_library.md`, `Reports/4a`, `Reports/4b`), que cubre WFC y autotiling en general; lo nuevo de 86 es la rejilla dual y el enfoque de Stålberg (piezas a mano con control artístico).

*Inferido:* encaja en pueblos y costas del juego de colonización, no en el terreno base, que es continuo.

**Próxima acción:** decidir si WFC / rejilla dual entra en la matriz (propuesta: NONE) y en qué capa (asentamientos). Leer antes `Reports/4b` y 1469.

## 3. Asentamientos a partir de una historia

| `i` | Fuente | Qué aporta |
| --- | --- | --- |
| 1745 | [Multi-agent Story-based Settlement Generation](https://ieeexplore.ieee.org/stamp/stamp.jsp?tp=&arnumber=9643241) (IEEE, 2021–22) | Un sistema multiagente usa un relato corto como inspiración para crear asentamientos 3D. El resumen dice que sin narrativa el mundo procedural «puede sentirse genérico». |
| 103 (dup. 108, 417), 214 | CultGame Devl, historia procedural en un juego indie; Jason Grinblat, *Procedural History in Caves of Qud* | Historia generada que condiciona el mundo. |
| 250, 251 | Hilos de generación de pueblos | 250 bloqueado; 251 redirigido (Town Maker, Unity). |

Límite: **el texto completo de 1745 no se descargó** (solo la página de cookies de IEEE). Se conoce el resumen, no la técnica.

**Próxima acción:** conseguir el paper por otra vía (DOI o autores) antes de nada. Si sirve, conecta con la fase W y con «Cómo nace una aldea» de `cuaderno-civilizaciones`.

## 4. Mapas poligonales y regiones de Voronoi

| `i` | Fuente | Qué aporta |
| --- | --- | --- |
| 98, 174 (duplicados) | Amit Patel, [Polygonal Map Generation for Games](http://www-cs-students.stanford.edu/~amitp/game-programming/polygon-map-generation/) | El enlace falla desde el rastreador, pero el texto está descargado (`profundo/textos/174.txt`). Mapas con ~1.000 polígonos en lugar de 1.000.000 casillas; Voronoi con Lloyd. El grafo modela lo que manda el juego (elevación, caminos, ríos, misiones) y el ruido lo que no (costa, árboles). Montaña = lo más lejos de la costa, así siempre se llega cuesta arriba. |
| 97, 1041 (duplicados) | [Red Blob Games](https://www.redblobgames.com/) | Portada, como índice. |
| 173 | [Making maps with noise functions](https://www.redblobgames.com/maps/terrain-from-noise/) | Ya cubierto (fBm, remapeo, biomas). Referencia cruzada. |
| 1430 | [Mapas sobre una esfera](https://www.redblobgames.com/x/1843-planet-generation/) | Delaunay y Voronoi sobre esfera; no hay mapas esféricos en el catálogo. |

Comprobado: `PCG_Roadmap.md` ya distingue Voronoi dentro de mapas (fase J) de Voronoi a escala geológica (placas, fase K). Lo que suma Patel es el **método de grafo primero** como alternativa al de ruido primero.

**Próxima acción:** al diseñar la fase J, leer 174 entero y decidir si las regiones salen del ruido de Worley que ya hay o de un grafo propio.

## 5. Minecraft y funciones de densidad 3D (descartadas en el catálogo de la web)

| `i` | Fuente | Qué aporta |
| --- | --- | --- |
| 110 | Henrik Kniberg, [Minecraft terrain generation in a nutshell](https://www.youtube.com/watch?v=CSa5O6knuwI) | La mejor explicación breve de cómo se moldea el terreno con curvas sobre varios campos de ruido. |
| 1470 | Kniberg, *Reinventing Minecraft world generation* | Versión larga de 110. |

Islands es 2D. Sirven como referencia si se reabre la idea; no piden acción ahora.

## 6. RNG por ruido (justificación del hash)

| `i` | Fuente | Qué aporta |
| --- | --- | --- |
| 128 | Squirrel Eiserloh, [Noise-Based RNG](https://www.youtube.com/watch?v=LWFzPP8ZbdU) (GDC 2017) | Reemplazar el generador de números aleatorios por un hash de coordenadas: acceso desordenado, reseed, grabar y reproducir, paralelismo sin bloqueos. |

Es la base teórica de SmallXXHash (DONE) y de la regla de no colocar cosas con `ctx.Rng` secuencial.

**Próxima acción:** citarla en la documentación del hash.

## 7. Menores

- **1399** (Holmér, continuidad de splines): fundamento del remapeo con spline y de caminos o ríos curvos.
- **1165, 1209, 1430:** hilo de «Planetas» (sin esferas en el catálogo).
- **1373** (Library of Babel): mundos como función pura de coordenadas, el mismo principio que 128.
- **Sin aporte o con problemas:** 1500 (isla 2D en Unity; la fase W.c ya lo cubre), 1037 (sitio secuestrado), 1119 (texto de otra lista).
- Vida artificial y emergencia (1544, 1741, 1760...): no son de Islands; van a la nota de la web.

## Addendum 2026-10-27: transcripciones leídas

Fichas completas en `D:\Projects\marcadores-chrome\profundo\fichas_videos.md`.

- **1484 (erosión por gotas, comprobado):** 70.000 gotas en un mapa de 255×255 tardan unos 0,75 s. La regla de no erosionar más que la diferencia de altura sirve como test de oro. La erosión sigue en NONE.
- **1412 (Wolverson):** DLA como pseudoerosión y mapas de Dijkstra con un «camino caliente» para comprobar que todo es alcanzable y colocar objetivos.
- **1463, 1469 y 1400 (WFC):**
  - 1463 aporta navegabilidad por tesela y reglas de prioridad.
  - 1469 aporta el patrón segmentar + WFC + A* (Caves of Qud).
  - 1400 es una introducción.
  - Confirman que falta en el catálogo.
- **1500:** máscara circular más ruido en el borde, capas por pasos y A* para verificar. Coincide con W.c (máscara de forma) y sirve para contrastarla.
- **1470:** detalle de las funciones de densidad 3D y de los splines sobre tres ruidos. Solo si se reabre la técnica, que está en OUT.
- **1165:** esfera a partir de un cubo, base para ruido sobre esfera, que el catálogo no tiene.
