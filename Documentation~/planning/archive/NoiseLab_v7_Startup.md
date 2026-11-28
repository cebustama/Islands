# Arranque: NoiseLab v7 (planeta cerrado y capa de placas)

**Cerrada el 2026-11-23.** Ver «Avance» al final y `changelog-ssot.md`, «NoiseLab v7». Sigue en
`NoiseLab_v8_Startup.md`.

Fecha: 2026-11-22. Rama: `main`. Sigue a `../archive/NoiseLab_v6_Startup.md` (v6 completa) y a
`../archive/NoiseLab_v6_2c_Startup.md` («Cerrada el 2026-11-22»). Roadmap: PL-17 (planeta) y PL-18
(placas) en `PCG_Roadmap.md`.

## Objetivo
1. **Planeta cerrado (PL-17):** con una opción *Planet*, el mundo da la vuelta de este a oeste
   (cilindro) y se ve entero en una vista *World ×32* (2:1, polo a polo, toda la longitud).
2. **Capa de placas (PL-18):** sobre ese planeta, una vista alternativa del mismo mapa con las
   placas, su movimiento y el tipo de cada borde. **Sin efecto en la generación** todavía: el
   mismo mapa se cambia entre la vista normal y la de placas.

## Verificado (con evidencia)
- v6 completa: S22 (export 2026-11-22 23:14) Burst check 0 de 327.680 valores distintos; clima
  0,000 % de biomas distintos; prueba de borde con margen 0,09 %; clima 33 ms; World ×16 38 ms
  (`changelog-ssot.md`, «NoiseLab v6»).
- **El mundo de hoy no es un toro.** Norte–sur termina en los polos: `LatitudeSpan = 8` vistas del
  ecuador al polo y la latitud se satura más allá (`NoiseLab.cs`, `math.saturate(|y| / LatitudeSpan)`).
  Este–oeste es una franja infinita: el océano se resuelve por regiones de 16×16 vistas
  (`OceanN = 128`, `OceanPerView = 8`, `OceanStride = 8`, caché de 4); `World ×16` es una ventana
  (`WorldScale = 16`, `BigN = 512`) centrada en la longitud de la vista.
- Escala: `KmPerView = 90 / LatitudeSpan · 111,2 ≈ 1251 km`. Si 180° son 16 vistas, 360° son
  **32 vistas** (≈ 40.000 km en el ecuador).
- Islands.Noise tiene `Simplex3D<G>`, `Lattice3D` y `Voronoi3D` (`Runtime/Noise/Noise/`), y
  `INoise` muestrea posiciones `float4x3`: se puede muestrear sobre un cilindro sin tocar `Runtime/`.
- Material de placas guardado (sin código todavía; `grep tecton|plate` en `Runtime/` sin
  resultados según `research/playlist-pcg/cards-tier3.md`):
  - Phase K en `PCG_Roadmap.md`: Voronoi grueso a escala de mundo; convergente → cordillera,
    divergente → rift, transformante → costas desplazadas.
  - `Phase_W_Design.md` §7.2: la elevación de placas como `ElevationEnvelope`.
  - `NoiseLab_v3_Inputs.md`, punto 8: placas de Voronoi con flechas de movimiento.
  - `research/playlist-pcg/cards-tier3.md`: el tipo de borde sale de la velocidad relativa.
  - `research/2026-11-05-mundo-finito-o-infinito.md`: placas como celdas de una rejilla con
    jitter, tipo por hash, borde mirando las 3×3 vecinas; la cascada de escalas.
  - «Supermountains» en `research/2026-11-capturas.md`: edad, tope por isostasia, erosión.

## Sin verificar (hipótesis)
- Muestrear en 3D cuesta ~1,5–2× el 2D; hay que medirlo en el S22.
- `Simplex3D` tiene otro rango que `Simplex2D` (el arreglo `PerlinOnSimplex` fue para 2D): con
  *Planet* encendido el mundo cambia y la receta puede pedir recalibrar la curva C.
- Con el ruido periódico, lo local (viento, flujo, distancia a la costa, margen) da la vuelta
  solo, porque todo lee el ruido; solo el océano por regiones necesita cambio.
- Referencias externas de placas, **por comprobar al arrancar**: Andy Gainey, Experilous,
  «Procedural Planet Generation» (2014); Amit Patel, Red Blob Games, generación de planetas sobre
  una esfera (2018); Lauri Viitanen, tesis PlaTec (2012); Cortial et al., «Procedural Tectonic
  Planets» (Eurographics 2019).

## Alcance propuesto (a confirmar con Claudio)
1. **Opción *Planet*** (código `pl=1`; sin ella, todos los códigos dan los mismos bits). Cada
   campo de ruido se muestrea sobre un cilindro: ángulo θ = 2π·x / 32, posición
   (R·cos θ, y, R·sin θ) con R = 32·f / 2π, para que la frecuencia por vista no cambie. Medir el
   coste en el editor y el S22.
2. **Océano periódico:** con *Planet*, una sola región de 256×128 celdas (32×16 vistas) con el
   borde este–oeste unido, en vez de regiones con caché. **Vista *World ×32*** (512×256, unos
   80 km por píxel) con los colores de ×16.
3. **Avisos y perilla:** sobre la rejilla del planeta, avisar si la tierra une los dos polos o
   si alguna latitud es toda tierra (en el resumen, no rechaza semillas). Perilla *Polar sea*
   opcional: baja la tierra por encima de ~60° en un hemisferio (el océano Austral abierto).
4. **Capa de placas, solo vista:**
   - Semillas en el planeta (número de placas, una perilla, unas 8–20) y crecimiento en la
     rejilla gruesa del planeta. Determinista por semilla, sin `UnityEngine.Random`.
   - A cada placa, un movimiento por hash (traslación más un giro pequeño; en un cilindro no hay
     polos de Euler) y un tipo para comparar con el mapa (continental si la mayor parte de su
     área es tierra).
   - Borde: convergente, divergente o transformante, según la velocidad relativa proyectada en
     la normal del borde.
   - Interfaz: color *Plates* en la vista y en ×16/×32 (cada placa un color, bordes por tipo,
     flechas) y un botón que cambia el mismo mapa entre la vista normal y la de placas.
5. **Verificación:** prueba de borde y Burst check sin cambios con *Planet* apagado; con él, la
   costura este–oeste sin salto (comparar la columna 0 con la 32); S22.

## Decisiones (Claudio, 2026-11-23)
- **D-V7-1:** las dos formas, Voronoi con jitter y crecimiento desde semillas, con un selector; se
  decide mirando en el S22.
- **D-V7-2:** color *Plates* en la vista y en ×16/×32, y un botón que alterna el mismo mapa entre
  la vista normal y la de placas.
- **D-V7-3:** periodo de 32 vistas (2:1).
- **Alcance:** el propuesto, tal cual. Paso 1: *Planet*, océano periódico, *World ×32*, avisos y
  *Polar sea*. Paso 2: placas.
- Arranque comprobado el 2026-11-23: suite 415/415 (batchmode). De las referencias se leyó solo
  Red Blob (2018): semillas 10–50, relleno desde las semillas en orden aleatorio (no Voronoi),
  dirección al azar por placa, borde según se acerquen o se alejen (sin transformante explícito).
  Gainey, Cortial y la tesis PlaTec no se pudieron abrir (Cortial existe: CGF 38(2); PlaTec en
  SourceForge: «divide al azar un mapa fractal en placas»).

## Decisiones abiertas (cerradas arriba)
- **D-V7-1, forma de las placas:** (A) Voronoi con jitter: simple, pero celdas convexas de bordes
  rectos (el «ingenuo» que intuye Claudio); (B) crecimiento desde semillas con relleno aleatorio
  ponderado (Gainey, Patel): placas irregulares, unas grandes y muchas pequeñas. Recomendación:
  hacer A primero como medida y pasar a B en el mismo paso; decidir mirando las dos.
- **D-V7-2:** ¿la vista de placas va en todos los paneles o como capa superpuesta (`ov=`)?
- **D-V7-3:** periodo de 32 vistas (proporciones reales) frente a 16 (cuadrado, más barato).
  Recomendación: 32.

## Fuera de alcance
Que las placas cambien el terreno (cordilleras, rifts: es la fase siguiente, Phase K); esfera o
cubo-esfera; zoom continuo (PL-7); hidrología (PL-14); los huecos del clima de PL-16 (van después
de la v7); cambios en `Runtime/`.

## No rehacer
La v6 (viento, océano, temperatura del mar, la 2c), la receta oficial, la curva C de océano
profundo, el pegado robusto.

## Ritual de arranque
1. `git log --oneline -5` en `D:\Projects\Islands`: el último commit de código es `1487c1f`
   (más los de documentación del cierre).
2. Leer este archivo, PL-17 y PL-18 en `PCG_Roadmap.md`, y en `NoiseLab.cs` `ComputeOceanRegion`,
   `GetOceanRegion`, `DrawBigPicture` y `GridNoiseJob`.
3. Correr la suite por el puente (`ClaudeTests/run.trigger` en el anfitrión): 415/415.
4. Comprobar las cuatro referencias externas de placas (o decir cuáles no se pudieron abrir).
5. Confirmar con Claudio el alcance y D-V7-1…3 antes de escribir código.

## Receta oficial (v6; el estado al pegar debe decir solo «Pasted.»)
`NL1;t=simplex/simplex;s=88775;f=8;o=1;l=2;g=0.5;w=%71;x=0.343;y=2.185;pal=mapa;b=50,30,20/4,30,30,16,12,8;q=w;h=s;n1=simplex/perlin,0.15,4,2,0.5;n2=simplex/perlin,3,3,2,0.5;n3=simplex/perlin,8,4,2,0.5;c=-1:-1,0:-1,0.1:-0.3,0.2:0.25,0.5:0.4,1:0.7/-1:1,-0.2:0.6,0.3:0.15,1:0.05/-1:-0.3,-0.3:-0.05,0.2:0.1,0.6:0.45,1:0.6;k=z;n4=simplex/perlin,2,4,2,0.5;n5=simplex/perlin,3,4,2,0.5;cl=0.8,0.5,0.5,0.1,0.05,0.7,0.17,0.3,0.5,10,3;wd=300,0.6,0.6,0.01,0.05;wf=0.5,200;oc=0.25,1.56,0.5;sh=0.8;ct=0.05;ov=contours:0.45;v=2:map,moisture`

## Archivos
- `Samples/0.1.0-preview/NoiseLab/NoiseLab.cs` (código; autoridad).
- `Documentation~/planning/active/PCG_Roadmap.md` (PL-17, PL-18, Phase K, PL-7, PL-16).
- `Documentation~/research/2026-11-05-mundo-finito-o-infinito.md` (planeta, escalas, placas por celdas).
- `Documentation~/research/playlist-pcg/cards-tier3.md` (placas en la playlist).
- `Documentation~/planning/active/NoiseLab_Migration_Matrix.md` (filas nuevas al cerrar).

## Después de la v7 (orden de Claudio, 2026-11-22)
1. PL-16, puntos 1 y 4: cinturones de lluvia por latitud y aridez (bajar *Sea → coast moisture*).
2. PL-14 H1: ríos, sobre la rejilla del planeta (opción A de D-H1-1).
3. Luego PL-15 (migración) y PL-13 (vistas).

## Avance (2026-11-23)
- **1a** `f64eee0`: *Planet* (`pl=1`), ruido sobre el cilindro (`CylinderNoiseJob<N>`). Apagado: los
  mismos bits (prueba de borde 0,09 %, Burst check 120.480 / 3,35e-6 en el editor). Claudio: editor y
  S22 «Todo OK».
- **1b** `99d8859`: océano del planeta (una región 256×128 con el este unido al oeste), imágenes
  grandes w × h, *World ×32*. Botones de ×16/×32 en su propia fila (se cortaban en el S22).
- **1c** `a888ddf`: avisos del planeta, *Polar sea* (`pl=1,<v>`), *Run all tests*, *Snapshot*, cada
  estado con su x e y, tocar la imagen grande lleva la vista. El «0 land cells» de la Burst check era
  una comprobación hecha en otro sitio, no un fallo. Sitios fijos de prueba: apagado en la receta v6
  (x 0.343, y 2.185); encendido en x −0.515, y 0.593 (con *Planet* el terreno es otro).
- **2** `15847dc`: capa de placas (Voronoi con jitter y Growth; D-V7-1: **Growth**, por defecto),
  rótulos en el snapshot. S22 22:40: Burst check 0, placas dos veces 0 de 18.432, Run all tests 1,2 s.
- Límite visto con *Polar sea* 1: sin corriente circumpolar (ψ = 0 en toda costa) → PL-19, v8.
- Por revisar: en vistas casi todo mar, el viento de la prueba de borde cambia más con margen que sin él.
- Tangentes guardadas: lab web «un océano en una caja» (PL-19); placas reales y Chile (Nazca, punto
  triple de Taitao, Scotia y el paso Drake) para explicaciones futuras.

