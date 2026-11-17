# Arranque: NoiseLab v6, paso 2c (el clima lee el mar)

Fecha: 2026-11-17. Rama: `main`. Sigue a `NoiseLab_v6_Startup.md` («Progreso», «2b cerrada»),
al diseño en `Fluids_Module_Design.md` («Subpasos» y «Añadido el 2026-11-17») y a la fila O4 de
`NoiseLab_Migration_Matrix.md`.

## Objetivo
Que el clima de la tierra lea la temperatura del mar de la 2b: una costa con mar frío
(afloramiento, corriente fría) sale **más fresca y más seca**, y una con mar cálido, más
templada y húmeda. La prueba: al lado de cada «Humboldt» del mundo aparece un «Atacama»
(desierto costero en los subtrópicos, costa oeste), y al lado de cada corriente cálida del
oeste, una costa húmeda. Es la última pieza de la v6.

## Verificado (con evidencia)
- 2b cerrada: `b3688c8`, `a507225`, `c4a54fa`; suite 415/415 por el puente (2026-11-17
  16:32:52); Burst check «sea temp» 0 diferencias al repetir; probado por Claudio en el editor
  y el S22 con varias semillas. Con la semilla 88775 y Upwelling 0,5, la costa que aflora queda
  a −5,7 °C de media respecto de su latitud.
- El clima hoy no sabe del mar más allá de la distancia a la costa: temperatura
  `coastModeration / (1 + distancia)` y humedad `coastalMoistureBonus / (1 + distancia·decay)`
  más el aire del viento (`ClimateManaged` / `ClimateBurst`; con el flujo, la ganancia sobre
  el mar es `1 − exp(−evaporation·dist·mar)` en `FlowAirSetupJob`).
- Orden actual en `Climate()`: primero el clima (`ClimateBurst` o `ClimateManaged`), **después**
  `Ocean()`. Para la 2c el océano tiene que ir antes.
- La temperatura del mar vive en la rejilla de las regiones (128², 8 celdas por vista, unos
  156 km por celda); `Ocean()` hoy la muestrea solo en las celdas de la vista (`seaTemp`), no en
  la rejilla del clima con margen (`extW × extH`, `mL…mT`).

## Sin verificar (hipótesis)
- Que bastan dos enganches: (a) la moderación de la costa toma el signo y el tamaño de la
  anomalía del mar cercano (T − T0), y (b) la evaporación del aire sobre el mar crece con la
  temperatura del mar (mar frío → aire seco → costa seca).
- Que con eso sale un desierto costero reconocible con la receta, sin tocar las perillas del
  clima.
- Que muestrear el mar en la rejilla del clima con margen no rompe el Edge test (el mar está
  fijo al mundo, así que no debería).
- Coste en el S22 (el clima completo hoy ronda 50 ms con el flujo).

## Alcance propuesto (a confirmar con Claudio)
1. Mover `Ocean()` antes del clima y muestrear la temperatura del mar en la rejilla del clima
   (con margen). Capa o lectura de comprobación: la vista *Sea temp* debe salir igual.
2. Temperatura de la costa: la moderación por distancia se multiplica o desplaza según la
   anomalía del mar cercano (frío → costa más fresca). Perilla nueva (peso del mar).
3. Humedad: la evaporación sobre el mar escala con la temperatura del mar (en el barrido de la
   v3 y en el aire del flujo de la 1b.2). Perilla nueva o reutilizar `windEvaporation`.
4. Burst check y Edge test (biomas cambiados con margen ≈ 0 %), S22; medir con la receta: los
   biomas de la costa oeste subtropical antes y después.

## Decisiones abiertas
- Cómo traducir °C del mar a la escala 0–1 del clima (la anomalía en °C dividida por una
  escala fija, p. ej. 10 °C, o el mar absoluto mapeado al mismo rango que `temp`).
- Si la temperatura de la tierra lee el mar solo por la costa (decae con la distancia) o
  también a través del aire (el viento lleva la temperatura además de la humedad).
- Si el barrido de la v3 (viento sin flujo) también lee el mar o solo el modelo de flujo.

## Fuera de alcance
Regla de la isla (Godfrey); mezcla entre giros; viento con componente meridiana; lab de placas;
zoom continuo; hidrología (PL-14, aunque la 2c le importa: costa seca → ríos que no llegan al
mar); `Runtime/`.

## No rehacer
v1–v5, paso 1 (1a–1b.2), pasos de interfaz, 2a, continentes, 2b (temperatura, transporte,
afloramiento, perillas en la vista alejada, semilla, *Temp anomaly*).

## Ritual de arranque
1. `git log --oneline -5` y `git status` en `D:\Projects\Islands` (limpio).
2. Unity abierto y fuera de Play: `run.trigger` vacío en
   `D:\Projects\Islands - Dungeon Generation Pipeline\ClaudeTests\` → 415/415.
3. Leer en `NoiseLab.cs`: `Climate()`, `ClimateManaged`, `ClimateBurst` (y su job), el barrido
   del viento (`WindField`), `FlowAirSetupJob`, `Ocean()`, `SeaHeat` y `SampleRegionField`.
4. Pegar la receta (abajo), World ×16 con *Temp anomaly*: ver dónde está el frío de la costa;
   luego, en la vista, ir a esa costa con la capa *Map* y *Moisture*.
5. Confirmar con Claudio el alcance y las decisiones; cada paso, editor y S22.

## Código de prueba (receta de continentes, semilla 88775, 2b por defecto)
`NL1;t=simplex/simplex;s=88775;f=8;o=1;l=2;g=0.5;w=%71;x=4.426;y=0.162;pal=mapa;b=50,30,20/4,30,30,16,12,8;q=w;h=s;n1=simplex/perlin,0.15,4,2,0.5;n2=simplex/perlin,3,3,2,0.5;n3=simplex/perlin,8,4,2,0.5;c=-1:-0.8,0:-0.4,0.1:-0.1,0.2:0.25,0.5:0.4,1:0.7/-1:1,-0.2:0.6,0.3:0.15,1:0.05/-1:-0.3,-0.3:-0.05,0.2:0.1,0.6:0.45,1:0.6;k=z;n4=simplex/perlin,2,4,2,0.5;n5=simplex/perlin,3,4,2,0.5;cl=0.8,0.5,0.5,0.1,0.05,0.7,0.17,0.3;wd=300,0.6,0.6,0.01,0.05;wf=0.5,200;oc=0.25,1.56,0.5;sh=0.8;ct=0.05;ov=contours:0.45;v=2:map,seatemp`

## Archivos
- `Samples/0.1.0-preview/NoiseLab/NoiseLab.cs` (código; autoridad).
- `Documentation~/planning/active/NoiseLab_v6_Startup.md` (progreso).
- `Documentation~/planning/active/Fluids_Module_Design.md` (diseño; propuesta).
- `Documentation~/planning/active/NoiseLab_Migration_Matrix.md` (fila O4; al cerrar, marcarla).

## Después de la v6
- **Módulo de hidrología** (PL-14), en `Hydrology_Module_Design.md`: la costa fría y seca debe
  dar ríos que no llegan al mar y cuencas endorreicas.
- **Matriz de migración** (PL-15): al cerrar la 2c, O4 pasa a validada y la v6 queda completa.
