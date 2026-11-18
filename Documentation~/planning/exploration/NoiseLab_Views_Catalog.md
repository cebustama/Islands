# NoiseLab — catálogo de vistas del mismo mapa

**Fecha:** 2026-11-15 · **Estado:** exploración, sin diseño ni código. Después de la v6.
**Origen:** captura del caza de UI8 (sprite stacking) y el wrapped «Sprite stacking»
(https://claude.ai/artifact/9gQ8nFyHVwjpktdgMHb6wU; fuente en `../../research/sprite-stacking/wrapped.html`).
Entrada del roadmap: PL-13 en `../active/PCG_Roadmap.md`.

## La idea (Claudio, 2026-11-15)

> «He estado pensando en que me gustaría probar distintas visualizaciones del mismo mapa 2D,
> estilo wireframe, estilo Roller Coaster Tycoon, estilo minecraft, estilo Valheim, etc. Me
> gustaría catalogar y demostrar cada técnica.»

Un mismo mapa de NoiseLab (altura, agua por cuantil, biomas, nubes) dibujado de muchas maneras,
para **catalogar** cada técnica (qué guarda, cómo dibuja, cuánto cuesta, qué estilo da) y
**demostrarla** en vivo. La vista dividida de la v4 (1×2 / 2×2) ya permite comparar dos o cuatro
vistas del mismo mapa a la vez.

Primera candidata: **relieve apilado (sprite stacking) en Burst**, porque es CPU puro sobre una
textura, como el resto de NoiseLab.

## Catálogo (borrador)

Familias por cómo llegan a la pantalla. «Encaje» dice cuánto se parece a lo que NoiseLab ya hace
(muestreo en jobs de Burst → textura).

### A. Mapas 2D (cartografía)

| Vista | Qué es | Ejemplo | Encaje | Estado |
| --- | --- | --- | --- | --- |
| Bandas de color | Color por banda de cuantil | NoiseLab v2 | — | **Hecho** |
| Curvas de nivel | Isolíneas de altura | NoiseLab v3 (capa) | — | **Hecho** |
| Sombreado de relieve (hillshade) | Luz por la normal del mapa de altura; la cartografía suiza de Imhof es el referente | Mapas topográficos | Muy alto: un job más | Nuevo |
| Glifos / ASCII | Un carácter o tile de un color por celda | *Dwarf Fortress*; tiles de salt_pilgrim (mundo-islas, 2026-11-13) | Alto | Nuevo |
| Hexágonos | Celdas hexagonales con color por bioma | *Civilization* (desde la V) | Medio | Nuevo |
| Costa suave (marching squares) | Contorno interpolado en lugar de escalones | Mapas vectoriales | Medio | Nuevo |

### B. 2.5D por software (píxeles, sin malla)

| Vista | Qué es | Ejemplo | Encaje | Estado |
| --- | --- | --- | --- | --- |
| **Relieve apilado (sprite stacking)** | Cada píxel de suelo es una columna; cada píxel de pantalla busca, de arriba abajo, la primera columna que llega a su altura. Giro libre en vertical, inclinación fija | El caza de UI8 (2026), *Wee Boats* | **Muy alto: un job por píxel de pantalla, sin estado** | **Primera candidata** |
| Voxel Space | Columnas verticales por distancia, de cerca a lejos, con buffer de oclusión por columna | *Comanche* (NovaLogic, 1992); s-macke/VoxelSpace | Alto | Nuevo |
| Isométrico de baldosas con pendientes | Proyección dimétrica 2:1; cada celda es una baldosa plana o inclinada según las alturas de sus esquinas (las alturas van en escalones enteros) | *SimCity 2000*, *Transport Tycoon*, *RollerCoaster Tycoon* | Medio: necesita juego de baldosas por pendiente (unas 19 combinaciones de esquinas) | Nuevo |
| JRPG de 16 bits en terrazas (¾ con muros al sur) | La altura en pocas terrazas (bandas por cuantil); donde una meseta está más alta que su vecina del sur se pinta un muro de (diferencia × k) tiles, de norte a sur; bordes por autotiling con la rejilla dual (~16 piezas por pareja de terrenos); tileset por bioma; cascadas donde un río cruza un muro y cuevas en muros; nivel 0 de agua o de nubes | *Final Fantasy VI* (SNES; capturas de fantasyanime.site, 2026-11-18, en `research/2026-11-capturas.md`) | Medio: el código es simple, lo caro es el tileset (bordes, muros arriba/medio/pie, esquinas). Tiles de FF6 solo en pruebas privadas: para publicar, tileset propio, abierto o generado por código desde la paleta | Nuevo |
| Líneas de cresta (ridgelines) | Perfiles horizontales apilados que se tapan unos a otros | Portada de *Unknown Pleasures* (Joy Division), a partir de datos de un púlsar | Alto | Nuevo |

### C. 3D con malla (adaptador, GPU)

| Vista | Qué es | Ejemplo | Encaje | Estado |
| --- | --- | --- | --- | --- |
| Alambre (wireframe) | La rejilla del mapa de altura como líneas, en perspectiva | Gráficos vectoriales de los 80 (*Battlezone*) | Medio: primera malla | Nuevo |
| Low poly con sombreado plano | Triángulos grandes, un color por cara | Mucho indie actual | Medio | Nuevo |
| Bloques (vóxeles) | Columnas de cubos; solo se mallan las caras visibles (opcional: greedy meshing) | *Minecraft* | Medio | Nuevo |
| Malla suave con texturas mezcladas | Malla del mapa de altura, texturas mezcladas por bioma (splatmap), de baja resolución y sin filtrar, con iluminación moderna | *Valheim* (descripción de memoria, **por comprobar**) | Bajo: shader y texturas | Nuevo |
| Raymarching de mapa de altura | Un rayo por píxel en un shader contra el mapa de altura | Inigo Quilez, «Terrain raymarching» | Medio (shader) | Nuevo |

## Por decidir (antes de diseñar)

1. **Dónde vive:** una pestaña «Vistas» en NoiseLab, o una escena hermana (`ViewsLab`) que lea
   el mismo mapa por código NL1. NoiseLab es un sample, así que ninguna de las dos toca `Runtime/`
   (adaptadores al final, se respeta la invariante).
2. **Qué se demuestra y dónde:** NoiseLab en el editor y el S22, partes de Islands Lab en la web,
   o las dos cosas, como con los cuantiles.
3. **Orden:** recomendado empezar por la familia B (apilado, luego Voxel Space; el isométrico de
   baldosas después) porque reutiliza el camino de NoiseLab, y añadir el sombreado de relieve de
   regalo. La familia C pide una primera malla y va después.
4. **Meta de rendimiento en el S22:** como referencia, el muestreo de la v5 a 256² tarda 13–35 ms.

## Relieve apilado en Burst (primera candidata)

- **Entrada:** la altura y el color por celda que NoiseLab ya calcula, cuantizados en N
  terrazas × k píxeles; se añade el ángulo de giro y la inclinación.
- **Job:** `IJobParallelFor` por píxel de pantalla. Para z de la altura máxima a 0:
  `gy = (sy − cy + z) / inclinación`, rotar (gx, gy) al mapa y, si `altura[celda] ≥ z`,
  pintar cima (si es igual) o ladera (si es más alta, más oscura) y parar. Sin estado: se puede
  paralelizar sin más.
- **Coste:** píxeles × capas en el peor caso; la salida temprana lo reduce mucho. Medir a 256² en
  el S22.
- **Extras del caza:** nubes como pilas de bolas con rango (el deslizador de cobertura quita o
  pone nubes enteras), niebla por distancia y dithering de Bayer.
- **Prototipo JS:** la demo de terreno del wrapped hace exactamente esto, en 180² y a 60 fps
  en el navegador.
