# Arranque: NoiseLab v6, paso «continentes» (antes de la 2b)

Fecha: 2026-11-11. Rama: `main`. Sigue a `NoiseLab_v6_Startup.md` (sección «Progreso»).

## Por qué este paso
Las corrientes de la 2a funcionan, pero el ruido hace **archipiélagos**: un giro necesita una
cuenca cerrada por tierra, y Humboldt un continente largo al este de un océano. La 2b
(temperatura del mar, afloramiento) y la 2c (el clima la lee) no se pueden juzgar sin eso.
Decisión de Claudio (2026-11-11): continentes en la próxima sesión.

## Verificado (con evidencia)
- 2a cerrada: Stommel en regiones de 16×16 vistas fijas al mundo; ω 1,7 con reintento (ω 1,4,
  doble de pasadas) si el residual pasa de 0,01. Editor (código de abajo): residual 0,0000,
  fastest 5,4×, Burst check del océano 4e-5, Burst dos veces 0. *Zoom out ×4* probado en el
  editor (114 ms) y el S22 (68–77 ms). Suite 415/415 por el puente (2026-11-11 22:19).
- ω 1,85 se disparaba con «West current» 0,1 (residual 36,9); reproducido fuera de Unity
  (scripts `sor_div.py`, `sweep.py`, `sweep2.py` del scratchpad de la sesión; no en el repo).
- La frecuencia de los campos es un entero ≥ 1 por ancho de vista: `Settings.frequency` de
  `Runtime/Noise/Noise/Noise.cs` es `int` con `[Min(1)]`, y el deslizador de NoiseLab va de 1
  a 32. La forma más grande posible mide ~1 vista; un continente de ~10 vistas pide ~0,1.

## Sin verificar (hipótesis)
- Que bajar la frecuencia del campo C (continentalidad) a ~0,05–0,2 por vista, con la curva C
  que ya existe, dé continentes y océanos sin más. Probable, pero las costas pueden quedar
  lisas (C domina) o el detalle (E·PV) puede abrir mares interiores; medir.
- Que los giros y la corriente rápida del oeste aparezcan claros con cuencas reales.

## Alcance propuesto (a confirmar con Claudio al arrancar)
1. **Frecuencias fraccionarias en NoiseLab, sin tocar la librería** (cambiar `Settings` de
   Islands.Noise es cambio gobernado y mueve goldens): escala propia del campo en NoiseLab
   (p. ej. multiplicar las coordenadas de muestreo por `frequency / settings.frequency`), con
   un deslizador logarítmico o en pasos (1/16 … 32) y el código `NL1` compatible hacia atrás.
2. **Continentes con la curva C:** una receta guardada (código) con océanos de varias vistas y
   costas con detalle; medirla con *Zoom out ×4* y el histograma.
3. Opcional, si 1–2 no bastan: máscara de continentes de muy baja frecuencia (domain warp o
   Voronoi grueso) sumada a C. El lab de placas (escena aparte) queda fuera.
4. Comprobar: cuencas cerradas en 20°–40° → giro con corriente rápida en la costa oeste;
   Burst check; S22.

## Insumo nuevo (2026-11-13)
Dos modelos de continentes para comparar con el campo C de baja frecuencia, de la propuesta 1 de `Documentation~/research/2026-11-13-pcg-games-proposals.md` (PL-12 del roadmap):
- **Acreción (Civilization 1):** paseos aleatorios de 1–64 pasos pintan «trozos»; donde cae tierra sobre tierra, sube; se para con una cuota de tierra. Algoritmo y constantes en `banco-ideas-juego/juegos-procedurales/investigacion/pcg-11-15.md` (ficha 12); demo en la página https://claude.ai/artifact/HLxMj8NRPTE7omTNjjswuA.
- **Placas (The Seven Cities of Gold, Dwarf Fortress):** cordilleras donde las placas chocan; enlaza con la fase K.
Opcional en esta sesión: decidir con Claudio si se mide alguno junto al campo C, o se deja para después de los continentes por ruido.

## Decisiones abiertas
- Tamaño típico de un continente y de un océano, en vistas (la latitud mide ±8 vistas; una
  región del océano, 16).
- Si el umbral del agua sigue por porcentaje de la vista (q=v) o por mundo (q=w; con
  continentes, por vista una vista toda de mar sube el umbral y crea tierra): recomendado w.
- Si el zoom visual (PL-7 del roadmap) entra aquí o después (recomendado: después).

## Fuera de alcance
2b y 2c (siguientes); la regla de la isla (Godfrey) salvo que se vea el problema; lab de placas
y escena índice (después de la v6); zoom continuo y los tres niveles mundo → país → celda
(Phase W / mundo-islas); cambios en `Runtime/`; catálogo de vistas del mapa (`../exploration/NoiseLab_Views_Catalog.md`, PL-13).

## No rehacer
v1–v5, el paso 1 (1a–1b.2), los pasos de interfaz, la 2a y su arreglo, *Zoom out ×4*.

## Ritual de arranque
1. `git log --oneline -5` y `git status` en `D:\Projects\Islands` (debe estar limpio, con el
   commit de la 2a arriba).
2. Con Unity abierto y fuera de Play, `run.trigger` vacío en
   `D:\Projects\Islands - Dungeon Generation Pipeline\ClaudeTests\`: 415/415.
3. Leer `NoiseLab_v6_Startup.md` (Progreso) y, en `NoiseLab.cs`: `SampleInto`, `FieldKey`,
   el deslizador de frecuencia (`IntSlider("Frequency"…)`), `CombineHeight`, `ApplyCode` y
   `BuildCode` (compatibilidad de códigos).
4. Confirmar con Claudio el alcance (1–4) y las decisiones abiertas; luego el paso 1.
5. Cada paso: Claudio lo prueba en el editor y el S22 antes del siguiente.

## Código de prueba (archipiélago de la 2a)
`NL1;t=simplex/simplex;s=0;f=8;o=1;l=2;g=0.5;w=%76;x=-11.033;y=-0.784;pal=mapa;b=50,30,20/4,30,30,16,12,8;q=w;h=s;n1=simplex/perlin,2,4,2,0.5;n2=simplex/perlin,3,3,2,0.5;n3=simplex/perlin,8,4,2,0.5;c=-1:-0.8,-0.35:-0.35,-0.1:0.05,0.35:0.2,1:0.6/-1:1,-0.2:0.6,0.3:0.15,1:0.05/-1:-0.3,-0.3:-0.05,0.2:0.1,0.6:0.45,1:0.6;k=z;n4=simplex/perlin,2,4,2,0.5;n5=simplex/perlin,3,4,2,0.5;cl=0.8,0.5,0.5,0.1,0.05,0.7,0.17,0.3;wd=300,0.6,0.6,0.01,0.05;wf=0.5,200;oc=0.1;sh=0.8;ct=0.05;ov=contours:0.45;v=2:map,currents`

## Archivos
- `Samples/0.1.0-preview/NoiseLab/NoiseLab.cs` (código; autoridad).
- `Documentation~/planning/active/NoiseLab_v6_Startup.md` (progreso de la v6).
- `Documentation~/planning/active/Fluids_Module_Design.md` (diseño de la 2a–2c; propuesta).
- `Documentation~/research/2026-11-05-mundo-finito-o-infinito.md` (cascada de escalas).
- `Documentation~/planning/active/PCG_Roadmap.md`, PL-7 (zoom no neutro al detalle).
