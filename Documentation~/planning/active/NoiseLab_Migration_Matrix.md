# Matriz de migración: NoiseLab → motor

Fecha: 2026-11-17. **Planificación, no es autoridad de implementación.** La verdad del motor
está en `systems/*-ssot.md` y en `CURRENT_STATE.md`; la de NoiseLab, en
`Samples/0.1.0-preview/NoiseLab/NoiseLab.cs`.

## Para qué sirve

NoiseLab es la **reestructuración validada** de lo que ofrece el motor (decisión de Claudio,
2026-11-17): cada idea se prueba primero allí, en el editor y en el S22, y después pasa al
motor. Este registro se lleva al día **mientras NoiseLab avanza** y se aplica **cuando toque
migrar**. Así no hay que volver a comparar los dos códigos desde cero.

Cómo mantenerlo:
- Cada paso de NoiseLab que se cierre añade o actualiza su fila (columna «Validado»).
- Cuando una fila se migre, se marca como **Migrada** con el commit, y su verdad pasa a los
  SSoT. La fila se queda aquí como historial.
- Nada de esta tabla autoriza código del motor. Cada migración es un batch propio con su
  diseño, sus tests y sus goldens.

## Tipos

- **S, sustituye:** reemplaza algo que el motor ya tiene. Cambia goldens: el re-anclado es
  trabajo mecánico y no es una objeción por sí solo (postura del roadmap, 2026-08-19).
- **A, añade:** una etapa o un campo que el motor no tiene. Se añade al final de
  `MapFieldId` o `MapLayerId` y, apagado, el resultado es idéntico byte a byte.
- **Ad, adaptador o herramienta:** va en un adaptador, en Inspection o en el Editor, nunca en
  el núcleo.
- **FV, falta validar:** está en el motor y NoiseLab todavía no lo cubre. Antes de
  reestructurar alrededor de esa pieza hay que probarla en NoiseLab o decidir conservarla tal
  cual.

## Decisiones estructurales previas (bloquean la migración)

Ninguna está decidida. Se resuelven antes de la primera fila S o A.

| ID | Decisión | Por qué bloquea | Opciones iniciales |
|---|---|---|---|
| MD-1 | **Mundo abierto frente a rejilla finita.** NoiseLab muestrea un dominio continuo sin bordes; el motor trabaja con un `MapContext2D` finito y una máscara de isla. | Afecta al relieve, al mar por cuantil, al clima y al océano. | (a) El mundo de la fase W es una ventana grande, a baja resolución, de las coordenadas de NoiseLab, y la isla es una ventana local; (b) el motor gana coordenadas de mundo (PL-6) y cada mapa es una ventana; (c) las dos cosas, en ese orden. |
| MD-2 | **Independencia de la ventana.** NoiseLab usa un margen fuera de la vista (v5) y una rejilla del océano fija al mundo para que el resultado no cambie al moverse. | El determinismo del motor exige mismo seed y mismos tunables ⇒ mismo mapa. Si el clima depende de la ventana, un mapa local no coincide con su casilla del mundo. | Margen fijo como contrato; o campos de mundo calculados una vez (en W) y leídos por los mapas locales. |
| MD-3 | **Referencia del cuantil.** El mar por porcentaje se calcula sobre la vista o sobre una referencia de mundo (`CutsFrom.World`, 128² en 16×16 vistas). | Con la vista, el mismo lugar cambia de nivel del mar según el encuadre. | Por mundo (lo que valida NoiseLab para mover y hacer zoom) frente a por mapa (el motor actual, con colinas y vegetación). |
| MD-4 | **Burst en el núcleo.** NoiseLab corre el muestreo, el clima, el viento y el océano en jobs de Burst con `FloatMode.Strict` y comprueba que dan lo mismo que C#. | Hay que decidir si el núcleo exige paridad Burst = C# como gate de determinismo. | Gate «Burst check» como test de EditMode, o solo C# en el núcleo y Burst en los adaptadores. |
| MD-5 | **Forma de la isla.** NoiseLab ya no usa máscara (los continentes salen de C). El motor depende de `IslandShapeMode` y F2c. | Decide si F2b, F2c y W.c conviven con los continentes o pasan a ser un caso particular («una isla = C alto en el centro»). | Convivir (la máscara multiplica a C) o sustituir. |

## Matriz

Las columnas «Dónde en NoiseLab» nombran funciones de `NoiseLab.cs`, porque sus líneas
cambian.

### Relieve

| ID | Pieza | Dónde en NoiseLab | Motor hoy | Tipo | Destino propuesto | Validado | Bloqueada por |
|---|---|---|---|---|---|---|---|
| R1 | Altura con tres ruidos y splines encadenados (`C + E·PV`: continentalidad, erosión, picos y valles), curvas editables | `CombineHeight`, `Evaluate`, `CurveEditor`; `HeightSource.Splines` | Un ruido × máscara de isla en `Stage_BaseTerrain2D`, más la curva de remapeo del preset | S | `Stage_BaseTerrain2D` (modo nuevo) o stage nueva antes de él; tres campos nuevos (`Continentalness`, `Erosion`, `PeaksValleys`) si otras etapas los leen | v3 (2026-11-02), editor + S22 | MD-1, MD-5 |
| R2 | Frecuencias fraccionarias (0,05–32); las enteras dan los mismos bits | `SampleInto`, `SnapFrequency` | Frecuencias enteras (cell size) en `MapNoiseBridge2D` | A | `MapNoiseBridge2D` / `TerrainNoiseSettings` | Continentes v6 (2026-11-17) | MD-1 |
| R3 | El mar por porcentaje (cuantil) | `Calibrate`, `CutGroup`, `WaterMode.Percentage` | `waterThreshold01` fijo (coordenada; W-aux.e lo declaró *feature*) | S | `Stage_BaseTerrain2D`, con el operador de cuantil de `HillsThresholdOps2D` | v1–v2 (2026-10-31/11-01) | MD-3 |
| R4 | Bandas de altura por porcentaje (9 bandas, 3 de agua) | `BandOf`, `Calibrate` | Capas `ShallowWater`/`MidWater`/`DeepWater` por profundidad; colinas por cuantil de área (W-aux.g) | S (agua) / Ad (colores) | Agua: `Stage_Shore2D`; colores: adaptador | v2 | MD-3 |
| R5 | Coordenadas de mundo continuas (offset, arrastrar) | `offset`, `HandleDrag` | No existe (PL-6) | A | `MapInputs` / `MapContext2D` | v2 | MD-1 |
| R6 | Planeta cerrado: el mundo da la vuelta este–oeste (cilindro de 32 vistas, 2:1); apagado, los mismos bits | `planet`, `SampleCylinder`, `CylinderNoiseJob<N>`, `Calibrate` (192×96) | Mapa finito y plano | A | Fase W (mundo); muestreo de ruido periódico en `MapNoiseBridge2D` | v7 1a (2026-11-23, `f64eee0`), editor + S22 | MD-1 |
| R7 | *Polar sea*: hunde la tierra más allá de 55–65°S (océano austral abierto) y avisos del planeta (tierra de polo a polo, latitudes toda tierra, mar circumpolar) | `PolarSea`, `PlanetCheck` | No existe | A | Fase W | v7 1c (2026-11-23, `a888ddf`), editor | R6 |

### Clima

| ID | Pieza | Dónde en NoiseLab | Motor hoy | Tipo | Destino propuesto | Validado | Bloqueada por |
|---|---|---|---|---|---|---|---|
| C1 | Distancia a la costa euclídea exacta (EDT, Felzenszwalb) | `CoastDistance`, `Edt1D`, `CoastColumnsJob`/`CoastRowsJob` | BFS con tope `CoastDistMax` en `Stage_Morphology2D` | S | `CoastDist` en `Stage_Morphology2D`; el valor −1 fuera de tierra se mantiene | v3, comprobada contra fuerza bruta (lab web «Clima y biomas») | — |
| C2 | Enfriamiento por altura sobre el **rango de la tierra** (0 en la costa, 1 en la cima) | `ClimateManaged` / `ViewClimateJob` | Sobre la altura cruda (`Stage_Biome2D` M.1) | S | `Stage_Biome2D` M.1 | v3 | R3 (el rango depende de dónde cae el mar) |
| C3 | Latitud = y del mundo (ecuador en y = 0, polos a ±8 vistas) | `ClimateManaged`, `LatitudeSpan` | `latitudeEffect` 0 por defecto, sobre la y del mapa | S | `Stage_Biome2D` M.1 | v6, continentes y World ×16 | MD-1 |
| C4 | Ruidos de temperatura y humedad con 4 octavas (con 2, los biomas salen en franjas rectas) | `DefaultExtraFields` | Ruidos gruesos de pocas octavas | S (tunables) | Defaults de `Stage_Biome2D` | v3 | — |
| C5 | Viento en barrido con sombra de lluvia (evaporación sobre el mar, lluvia al subir, secado sobre tierra) | `WindField`, `WindJob`, `WindModel.Sweep` | No existe | A | Stage nueva entre Morphology y Biome; campos `WindAir` (humedad del aire) y, si hace falta, `Rain` | v3 (2026-11-02) | MD-2 |
| C6 | Viento *Flow*: el viento de fondo rodea el relieve alto (proyección de presión, SOR rojo-negro) | `Flow`, `FlowAirJob`, `FlowMaskJob`, `FlowInitJob` | No existe | A | La misma stage que C5 (modo), campos `WindU`, `WindV` | v6 paso 1 (2026-11-09/11), editor + S22 | MD-2, MD-4 |
| C7 | La humedad la trae el aire (pesa `windStrength` frente a costa y ruido) | `ClimateManaged` | Humedad = cercanía a la costa + ruido + ríos (`Stage_Biome2D` M.2) | S | `Stage_Biome2D` M.2, leyendo el campo de C5/C6 | v3 | C5 |
| C8 | Margen fuera de la vista: el clima no cambia al arrastrar (Edge test 0,00 % de biomas) | `Margins`, `SampleMargins`, `EdgeTest` | No aplica (mapa cerrado) | A (contrato) | Ver MD-2 | v5 (2026-11-06) | MD-2 |
| C9 | Biomas | `ClimateManaged` | `Stage_Biome2D` M.3 + `BiomeTable` | — (comparte fórmulas) | Sin cambio; solo cambian sus entradas | v3 | — |

### Océano

| ID | Pieza | Dónde en NoiseLab | Motor hoy | Tipo | Destino propuesto | Validado | Bloqueada por |
|---|---|---|---|---|---|---|---|
| O1 | Corrientes marinas (Stommel: función de corriente ψ con β y viento por latitud; rejilla gruesa fija al mundo) | `Ocean`, `ComputeOceanRegion`, `OceanSorJob`, `OceanResidual`, `SampleRegion` | No existe | A | Stage nueva de escala de mundo (fase W); campos `CurrentPsi`, `CurrentU`, `CurrentV` | v6 paso 2a (2026-11-11), editor + S22 | MD-1, MD-2 |
| O2 | Temperatura del mar: base por latitud, transportada por las corrientes (volúmenes finitos upwind + relajación, memoria del calor) | `SeaBaseTemp`, `SeaHeat`, `SeaHeatJob` | No existe | A | La misma stage que O1; campo `SeaTemperature` | 2b pasos 1–2 (2026-11-17, `b3688c8`, `a507225`), editor + S22 | O1 |
| O3 | Afloramiento costero (costas oeste frías: Humboldt, Benguela, California, Canarias) | `upwelling`, `SeaDeepTemp` en `SeaHeat` (2b paso 3; lee la corriente hacia el ecuador, no el viento) | No existe | A | La misma stage que O1 | 2b paso 3 (2026-11-17, `c4a54fa`), editor + S22 | O2 |
| O4 | El clima lee el mar (2c): la temperatura del mar modera la costa y una costa fría seca el aire | `extSeaAnom` en la rejilla del clima; *Sea → coast temp*, *Sea reach*; techo de humedad `min(1, e^(k·ΔT))` en `WindField`/`WindJob`, `FlowMaskJob` → `FlowAirJob`, `ViewClimateJob`, `BigBiomes` | `coastModerationStrength` de M.1 (solo distancia) | S | `Stage_Biome2D` M.1/M.2 | 2c (2026-11-18/22, `9fd00fb`, `eb98ede`, `1487c1f`), editor + S22 | O2, C5 |
| O5 | Océano periódico: una región de todo el planeta (256×128) con el este unido al oeste | `ComputeOceanRegion` (`wrap`), `OceanSorJob`, `SeaHeatJob`, `SampleWrapped` | No existe | A | La misma stage que O1, sobre la rejilla del planeta | v7 1b (2026-11-23, `99d8859`), editor + S22 | O1, R6 |

### Adaptadores y herramientas

| ID | Pieza | Dónde en NoiseLab | Motor hoy | Tipo | Destino propuesto | Validado |
|---|---|---|---|---|---|---|
| T1 | Código `NL1`: el mapa entero como texto (mismo código ⇒ mismo mapa en el editor y el S22) | `CodePage`, `Read*`, `WriteSaved` | `MapGenerationPreset` (asset) + JSON de `MapStatsExporter2D` | Ad | Import/export de preset en el Editor (track X1) | v2 |
| T2 | Burst check (Burst = C# en cada valor) y Edge test | `BurstCheck`, `BurstProbe`, `EdgeTest` | Goldens por hash de capa | Ad / gate | Tests de EditMode, si MD-4 lo pide | v5 |
| T3 | Pestaña Perf y exportación de tiempos | `PerfPage`, `ExportPerf` | PL-9 (rendimiento, parking lot) | Ad | Inspection | v2–v5 |
| T4 | Vista dividida 2×2, capas superpuestas con opacidad, curvas de nivel | `PaintPanel`, `Overlays`, `Contour` | `ScalarOverlayRenderer`, `PCGRuntimeOverlay` (Phase V) | Ad | Phase V / PL-8, PL-10 | v4 |
| T5 | Vistas de escala: World ×4 y ×16, colores Gyres, Sea temp y Anomaly, flechas de corriente | `MakeBigPicture`, `GyreColours`, `SeaTempColours`, `AnomalyColours`, `CurrentArrows` | No existe | Ad | Adaptador del mapa de mundo (fase W) | v6 |
| T6 | Nubes animadas (nunca tocan el terreno) | `Clouds`, `CloudNoiseJob` | No existe | Ad | Adaptador | v4–v5 |
| T7 | Histograma exacto y estadísticas del campo | `Measure`, `Flood` | `LogHeightHistogram`, `MapStatsExporter2D` | Ad | Inspection (ya cubierto en parte) | v1 |
| T8 | *Run all tests* (borde, Burst check, placas dos veces, snapshot; informe guardado) y *Snapshot* (todas las capas en un PNG con rótulos); ir al punto tocado en ×4/×16/×32 | `RunAllTests`, `SaveSnapshot`, `DrawLabel`, `DrawBigPicture` | Goldens por hash | Ad / gate | Tests de EditMode (MD-4) | v7 1c (2026-11-23) |
| T9 | Capa de placas, solo vista (Voronoi con jitter o Growth; movimiento, tipo, bordes por tipo), en la vista y en ×16/×32 | `EnsurePlates`, `GrowPlates`, `BorderKind`, `PlatePixels` | No existe (Phase K sin código) | Ad (hoy) / A (Phase K) | Phase K, `Stage_Tectonics2D` | v7 paso 2 (2026-11-23, `15847dc`), editor + S22 |

### Lo que el motor tiene y NoiseLab todavía no valida (FV)

NoiseLab solo trabaja con **campos**. Estas piezas son **máscaras y topología** y siguen en
el motor tal cual hasta que NoiseLab las pruebe o se decida conservarlas.

| ID | Pieza del motor | Dónde | Cómo se cubre | Nota |
|---|---|---|---|---|
| FV1 | Ríos y lagos (D8, relleno de depresiones, acumulación de flujo) | `Stage_Hydrology2D`, `HeightFieldHydrologyOps2D` | **H1** del módulo de hidrología (`Hydrology_Module_Design.md`, PL-14): ríos en NoiseLab con caudal por la lluvia de C5/C7 | Es el puente natural: lo primero que se valida encima del clima nuevo |
| FV2 | Colinas como capas (`HillsL1`, `HillsL2`, cuantil de área) | `Stage_Hills2D` | Comprobar que el cuantil de área sigue teniendo sentido con R1 (las montañas ya vienen de PV) | Puede quedar redundante con R1 |
| FV3 | Morfología: `LandEdge`, `LandCore`, `LandInterior` | `Stage_Morphology2D` | Sin cambio previsto; solo cambia `CoastDist` (C1) | — |
| FV4 | Bandas de agua `ShallowWater`, `MidWater`, `DeepWater` | `Stage_Shore2D` | R4 | — |
| FV5 | Vegetación (cuantil global por bioma) | `Stage_Vegetation2D` | Revisar la calibración cuando cambien los biomas (C2–C7) | M2a-9 |
| FV6 | Regiones con nombre | `Stage_Regions2D` | Sin cambio previsto | — |
| FV7 | Zonas transitables y escaleras (`Walkable`, `Stairs`) | `Stage_Traversal2D` | Sin cambio previsto | — |
| FV8 | Del mundo a la isla (W.a–W.c) | `ShapeMaskBuilder2D`, Phase W | Depende de MD-1 y MD-5 | Es donde aterrizan las filas de escala de mundo (O1–O3, T5) |
| FV9 | Tilemaps, megatiles, tiles por bioma (H2b–H8, Q) | `Adapters/Tilemap/` | Sin cambio: leen campos y capas | — |
| FV10 | Diagnóstico de presets (X1.a, reglas R1–R7) | Editor | Reglas nuevas para los tunables de R1, C5 y O1 | Hilo de «legibilidad de parámetros» |

## Orden sugerido cuando toque migrar (propuesta, sin decidir)

1. Resolver MD-1…MD-5.
2. Lo que no depende de MD-1: **C1** (EDT) y **C4** (octavas). Son pequeñas, solo cambian goldens.
3. **R3 + R4 + C2** juntas (el mar por cuantil cambia el rango de la tierra).
4. **R1 + R2 + R5** (el relieve nuevo), con MD-5 resuelta.
5. **C5–C8** (viento y humedad).
6. **O1–O4**, en la escala de mundo de la fase W.
7. FV1 (ríos) cuando H1 esté validado en NoiseLab.

Un problema por batch, como siempre.

## Historial

| Fecha | Cambio |
|---|---|
| 2026-11-17 | Creada a partir de la comparación entre `NoiseLab.cs` (v6, 2b paso 2, `a507225`) y `Runtime/PCG`. |
| 2026-11-22 | O4 validada (2c cerrada en el editor y el S22). La v6 queda completa. |
| 2026-11-23 | Filas R6, R7, O5, T8 y T9 (NoiseLab v7: planeta cerrado, océano periódico, pruebas, placas). |
