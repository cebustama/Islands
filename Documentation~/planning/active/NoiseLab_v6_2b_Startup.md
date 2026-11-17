# Arranque: NoiseLab v6, paso 2b (temperatura del mar)

Fecha: 2026-11-17. Rama: `main`. Sigue a `NoiseLab_v6_Startup.md` (sección «Progreso», paso
«continentes») y al diseño de la 2b en `Fluids_Module_Design.md` (modelo B, «Subpasos»).

## Objetivo
Un campo de **temperatura del mar** que se vea en una capa propia: depende de la latitud, la
corriente lo lleva (corrientes calientes hacia los polos por la costa oeste de los océanos,
frías hacia el ecuador por la costa este) y el **afloramiento** enfría la costa oeste de los
continentes donde los alisios soplan hacia el ecuador (Humboldt, Benguela, California). El
clima todavía no lo lee: eso es la 2c.

## Verificado (con evidencia)
- Continentes cerrados: commits `fdf6eab`, `ffd954f`, `13c6dd1`; suite 415/415 por el puente
  (2026-11-12 11:17); receta y giros probados por Claudio en el editor y el S22 (export
  2026-11-17 11:46: región de océano 17,7 ms, Burst check 0).
- Las corrientes (2a) dan 6 giros de polo a polo con la corriente rápida en el oeste de cada
  océano; prueba offline con `stommel.py` (no está en el repo).

## Sin verificar (hipótesis)
- Que el transporte de la 1b.2 (volúmenes finitos sobre caras, hasta asentarse) sirva para el
  mar en la rejilla de las regiones (128², 8 celdas por vista) sin ser lento en el S22.
- Que el afloramiento salga bien con una regla simple: viento paralelo a la costa con la
  costa a la izquierda (sur) o a la derecha (norte) → agua fría. Medir con la receta.
- Que la mezcla de las dos regiones solapadas no deje costuras en la temperatura.

## Alcance propuesto (a confirmar con Claudio)
1. Temperatura base por latitud en el mar + capa **Sea temp** (y en ×4 / ×16).
2. Transporte por la corriente (esquema de la 1b.2) con relajación hacia la temperatura base.
3. Afloramiento costero; comprobar con la receta que la costa oeste de un continente
   subtropical sale fría (el «Humboldt» del mundo).
4. Burst check, Edge test si aplica, S22.

## Decisiones abiertas
- Cuánto pesa el transporte frente a la relajación (cuán lejos llega el agua fría o caliente).
- Si la temperatura se calcula por región (como ψ) o en la vista.

## Fuera de alcance
2c (el clima lee el mar); regla de la isla (Godfrey); lab de placas; zoom continuo; `Runtime/`.

## No rehacer
v1–v5, paso 1 (1a–1b.2), pasos de interfaz, 2a, continentes (frecuencias fraccionarias,
World ×16, Gyres).

## Ritual de arranque
1. `git log --oneline -5` y `git status` en `D:\Projects\Islands` (limpio).
2. Unity abierto y fuera de Play: `run.trigger` vacío en
   `D:\Projects\Islands - Dungeon Generation Pipeline\ClaudeTests\` → 415/415.
3. Leer en `NoiseLab.cs`: `ComputeOceanRegion`, `SampleRegion`, `MakeBigPicture`,
   `GyreColours` y el transporte del aire de la 1b.2 (buscar «flow-grid air»).
4. Pegar la receta (abajo), Colour: Gyres → World ×16: los giros entre continentes.
5. Confirmar con Claudio el alcance y las decisiones; cada paso, editor y S22.

## Código de prueba (receta de continentes)
`NL1;t=simplex/simplex;s=0;f=8;o=1;l=2;g=0.5;w=%71;x=-1.975;y=-1.815;pal=mapa;b=50,30,20/4,30,30,16,12,8;q=w;h=s;n1=simplex/perlin,0.15,4,2,0.5;n2=simplex/perlin,3,3,2,0.5;n3=simplex/perlin,8,4,2,0.5;c=-1:-0.8,0:-0.4,0.1:-0.1,0.2:0.25,0.5:0.4,1:0.7/-1:1,-0.2:0.6,0.3:0.15,1:0.05/-1:-0.3,-0.3:-0.05,0.2:0.1,0.6:0.45,1:0.6;k=z;n4=simplex/perlin,2,4,2,0.5;n5=simplex/perlin,3,4,2,0.5;cl=0.8,0.5,0.5,0.1,0.05,0.7,0.17,0.3;wd=300,0.6,0.6,0.01,0.05;wf=0.5,200;oc=0.1;sh=0.8;ct=0.05;ov=contours:0.45;v=2:map,currents`

## Archivos
- `Samples/0.1.0-preview/NoiseLab/NoiseLab.cs` (código; autoridad).
- `Documentation~/planning/active/NoiseLab_v6_Startup.md` (progreso).
- `Documentation~/planning/active/Fluids_Module_Design.md` (diseño de la 2b–2c; propuesta).

## Después de la v6

- **Módulo de hidrología** (PL-14): H1 ríos → H2 erosión con edad → H3 meandros → H4 deltas, en [`Hydrology_Module_Design.md`](Hydrology_Module_Design.md). La 2b importa ahí: la costa fría y seca de Humboldt debe dar ríos que no llegan al mar y cuencas endorreicas.
