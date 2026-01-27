# PCG (Example Project) — Technical Snapshot del Pipeline (Dungeon + Tilemap)

> Fuente: carpeta `PCG/` del zip compartido. Este snapshot describe el sistema **tal como está hoy**, incluyendo dependencias externas referenciadas pero no incluidas en el zip.

## 1) Qué es este sistema (en una frase)

Un **mini–framework de generación procedural en grid** orientado a **Unity Tilemaps**, donde distintos *generators* producen un `HashSet<Vector2Int>` de “floor tiles”, se pintan en `TilemapVisualizer`, y opcionalmente se post-procesan con **Cellular Automata** y/o se rodean con **walls** usando un clasificador de vecinos (bitmasks).

---

## 2) Mapa de componentes (componentes → responsabilidades → interacciones)

### A) Orquestación (MonoBehaviours “entrypoint”)
**`AbstractTilemapGenerator`**
- **Responsabilidad:** define el contrato mínimo del pipeline (`Generate()` + `RunProceduralGeneration()`), y ofrece `ApplyAutomata()` como post-proceso.
- **Interacciones:**
  - depende de `TilemapVisualizer` para pintar/leer el estado de tiles.
  - usa `ProceduralGenerationAlgorithms.ApplyCAStep(...)` para Cellular Automata.
  - **Ojo:** `ApplyAutomata()` lee desde el `Tilemap` (no desde un modelo puro), así que asume que ya existe un piso pintado.

**`AbstractDungeonGenerator : AbstractTilemapGenerator`**
- **Responsabilidad:** especializa el post-proceso (`ApplyAutomata()`) para que tras el CA también se creen walls.
- **Interacciones:** llama `WallGenerator.CreateWalls(...)` si `addWalls`.

### B) Generators concretos (dungeon/rooms)
**`SimpleRandomWalkDungeonGenerator : AbstractDungeonGenerator`**
- **Responsabilidad:** genera cavernas/paths con random walk iterado.
- **Interacciones:**
  - usa `ProceduralGenerationAlgorithms.IteratedSimpleRandomWalk(...)`.
  - pinta con `tilemapVisualizer.PaintFloorTiles(...)`.
  - si `addWalls`, llama `WallGenerator.CreateWalls(...)`.
- **Dependencia externa (no incluida):** `SimpleRandomWalkSO` (parámetros del walk).

**`CorridorFirstDungeonGenerator : SimpleRandomWalkDungeonGenerator`**
- **Responsabilidad:** crea una “espina” de corredores (random walk corridor) y luego agrega rooms en endpoints y dead-ends.
- **Interacciones:**
  - `CreateCorridors(...)` usa `ProceduralGenerationAlgorithms.RandomWalkCorridor(...)`.
  - `CreateRooms(...)` y `CreateRoomsAtDeadEnd(...)` usan `RunRandomWalk(...)` heredado (rooms por random walk).
  - pinta floors y crea walls.

**`RoomFirstDungeonGenerator : SimpleRandomWalkDungeonGenerator`**
- **Responsabilidad:** genera rooms primero (BSP), luego conecta centros de rooms con corredores ortogonales.
- **Interacciones:**
  - usa `ProceduralGenerationAlgorithms.BinarySpacePartitioning(...)` → `List<BoundsInt>` de rooms.
  - rooms se crean como:
    - **random-walk rooms** (si `randomWalkRooms == true`), o
    - **simple rectangular fill** (si `false`).
  - luego `ConnectRooms(...)` crea corredores entre centros por camino Manhattan.
  - pinta y crea walls.

**`RoomGridDungeonGenerator : SimpleRectangularRoomGenerator`**
- **Responsabilidad:** intenta construir un dungeon basado en **grid de rooms**, manteniendo un “room manager” (para tiles/doors).
- **Interacciones:**
  - hereda helpers `CreateRectangularRoom(...)` y `CreateDoor(...)`.
  - usa `ProceduralGenerationAlgorithms.IteratedSimpleRandomWalkList(...)` para elegir una secuencia de coordenadas de rooms (ruta).
  - para cada “room coords” calcula su `startPosition` real y crea la geometría.
  - guarda tiles en un manager.
- **Dependencias externas (no incluidas):** `GridDungeonRoomManager`, `GridDungeonRoomManager` API (`CreateGridRoom`, `AddRoomTiles`, `Clear`, etc).

**Generators de “shapes” (rooms/corridors unitarios)**
- `SimpleRectangularRoomGenerator : AbstractDungeonGenerator`
  - genera **una** sala rectangular (y helpers para “door tiles”).
  - depende de `RectangularRoomDataSO` (no incluido).
  - usa `ShapeAlgorithms.Rectangle(...)`.
- `CircularRoomGenerator : AbstractDungeonGenerator`
  - genera **una** sala circular/elíptica.
  - depende de `EllipticalRoomDataSO` (no incluido).
  - usa `ShapeAlgorithms.Circle(...)`.
  - usa `WallGenerator.CreateWalls(..., BoundsInt bounds)` (overload).
- `CorridorGenerator : AbstractDungeonGenerator`
  - genera un corredor entre `pointA` y `pointB` con algoritmo de línea.
  - usa `ShapeAlgorithms.Line(...)`.

### C) Rendering/Visualización (Unity Tilemaps)
**`TilemapVisualizer : MonoBehaviour`**
- **Responsabilidad:** convertir `IEnumerable<Vector2Int>` → tiles pintados en `Tilemap`s (`floorTilemap`, `wallTilemap`).
- Mantiene límites `minX/maxX/minY/maxY` **en función de lo pintado** (se usan para CA).
- Selecciona tiles de walls según tipo (top/side/corner/diagonal/etc) usando:
  - `WallTypesHelper` (set de bitmasks válidos),
  - `DungeonTilesetSO tileset` (no incluido) con arrays de variantes por tipo.
- **Interacciones:** lo usan todos los generators y `WallGenerator`.

### D) Post-proceso Walls
**`WallGenerator`**
- **Responsabilidad:** dado `floorPositions`, detectar posiciones vecinas que deberían ser walls y pintar el tile correcto según vecinos.
- Pipeline interno:
  1) `FindWallsInDirections(floorPositions, cardinalDirections)` → “basic walls”.
  2) `FindWallsInDirections(floorPositions, diagonalDirections)` → “corner walls”.
  3) Para cada wall, construir *neighbor bitmask* como string binario:
     - 4-direcciones para basic,
     - 8-direcciones para corner.
  4) `tilemapVisualizer.PaintSingleBasicWall(...)` o `PaintSingleCornerWall(...)`.
- Tiene overload `CreateWalls(floorPositions, visualizer, BoundsInt bounds)` para recortar walls a un área.

**`WallTypesHelper`**
- **Responsabilidad:** catálogos (`HashSet<int>`) que mapean “bitmasks de vecinos” → “tipo de wall” (top, side, corner, full, diagonal, etc).
- Funciona como “clasificador” para que `TilemapVisualizer` elija tile.

### E) Core Algorítmico (puro/estático)
**`ProceduralGenerationAlgorithms`**
- Random Walk:
  - `SimpleRandomWalk`, `IteratedSimpleRandomWalk`, y variantes `List`.
  - `RandomWalkCorridor`.
- Cellular Automata:
  - `ApplyCAStep(int[,] oldMap, lDeathLimit, cDeathLimit, birthLimit)`.
- BSP:
  - `BinarySpacePartitioning(...)` + helpers internos para split.
- También incluye utilidades:
  - `Direction2D` (listas de direcciones cardinal/diagonal/8).
  - `MinMaxInt` (clase utilitaria usada por otros configs).

**`ShapeAlgorithms`**
- Geometría sobre grid:
  - líneas (con enum de algoritmos),
  - rectángulos/cuadrados,
  - círculo,
  - flood fill (iterativo/recursivo),
  - utilidades para insertar shapes en un canvas.

### F) Sub-sistemas auxiliares (no directamente dungeon)
**`OverworldGenerator : AbstractTilemapGenerator`**
- Generación simple de overworld por Perlin + thresholds → tiles de bioma.
- Tiene `OnValidate()` con autogeneración si `onValidateGeneration`.

**Generative Art + Textures**
- `GenerativeCanvas`, `GenerativeShape`, `GenerativeParameters`, `TextureCreator`.
- Estado actual: parcialmente implementado; `TextureCreator` mezcla `UnityEditor` en un script runtime (ver recomendaciones).

---

## 3) Snapshot del flujo de ejecución (call-flow)

### Flujo común (todos los generators basados en `AbstractTilemapGenerator`)
1) **(Actor)** diseñador o script llama `generator.Generate()`.
2) `Generate()`:
   - `tilemapVisualizer.Clear()`
   - `RunProceduralGeneration()` (polimórfico)
3) `RunProceduralGeneration()` típicamente:
   - crea `HashSet<Vector2Int> floorPositions`
   - `tilemapVisualizer.PaintFloorTiles(floorPositions)`
   - opcional: `WallGenerator.CreateWalls(floorPositions, tilemapVisualizer)`

### Post-proceso Cellular Automata
4) **(Actor)** llama `generator.ApplyAutomata()`.
5) `ApplyAutomata()`:
   - construye `int[,] cellStates` leyendo del `Tilemap` (no del set original).
   - ejecuta `ProceduralGenerationAlgorithms.ApplyCAStep(...)` con límites desde `CellularAutomataSO`.
   - reconstruye `newFloorPositions` desde `cellStates`.
   - `tilemapVisualizer.Clear(); PaintFloorTiles(newFloorPositions)`
   - si es dungeon (`AbstractDungeonGenerator`), también crea walls.

---

## 4) Casos de uso posibles (con el codebase actual)

1) **Cave-like dungeon** (cavernas/orgánico):
   - `SimpleRandomWalkDungeonGenerator` + (opcional) `ApplyAutomata()` para suavizar.

2) **Dungeon “corridor-first”**:
   - `CorridorFirstDungeonGenerator` → spina dorsal de corredores + rooms en endpoints/dead-ends.

3) **Dungeon “room-first”**:
   - `RoomFirstDungeonGenerator` → BSP rooms + corredores Manhattan.

4) **Grid dungeons / roguelike rooms**:
   - `RoomGridDungeonGenerator` (requiere el manager externo) → rooms discretas conectadas, puertas, tracking por coordenadas.

5) **Prototipos de shapes**:
   - `SimpleRectangularRoomGenerator`, `CircularRoomGenerator`, `CorridorGenerator` para testear “brushes”/walls/door placement.

6) **Overworld tilemap**:
   - `OverworldGenerator` para biomas por Perlin + thresholds.

---

## 5) “Lo más importante” (si lo quieres portar a Islands)

**Clases núcleo (alto ROI):**
- `ProceduralGenerationAlgorithms` (RandomWalk, BSP, CA).
- `ShapeAlgorithms` (líneas/rect/circle/floodfill).
- `WallGenerator` + `WallTypesHelper` (si sigues con tilemaps y tileset por bitmask).
- `AbstractTilemapGenerator` / `AbstractDungeonGenerator` (si mantienes el patrón de entrypoints).

**Clases más Unity-específicas (adaptador, no core):**
- `TilemapVisualizer` (conviene abstraerlo si Islands va a mesh/surfaces/noise).
- Los MonoBehaviours concretos (pueden quedarse como “samples” o “tools”).

**Dependencias faltantes a resolver al migrar:**
- `DungeonTilesetSO`, `SimpleRandomWalkSO`, `CellularAutomataSO`, `RectangularRoomDataSO`, `EllipticalRoomDataSO`.
- `GridDungeonRoomManager` y su API.

---

## 6) Análisis SOLID (fortalezas, problemas, y refactors sugeridos)

### S — Single Responsibility
**Bien:**
- `ProceduralGenerationAlgorithms` y `ShapeAlgorithms` son bibliotecas enfocadas en algoritmos.
- `WallTypesHelper` es un catálogo puro.

**Riesgos:**
- `TilemapVisualizer` mezcla: *rendering*, *selección aleatoria de variantes*, *clasificación por bitmask*, *tracking de bounds*.
- `AbstractTilemapGenerator.ApplyAutomata()` mezcla: *lectura de tilemap*, *transformación a modelo*, *ejecución CA*, *repintado*.

**Refactor recomendado:**
- separar:
  - `IGridPainter` (pintar),
  - `IGridReader` (leer),
  - `IWallPainter` (walls),
  - `IAutomataProcessor` (CA sobre modelo, no sobre tilemap).

### O — Open/Closed
**Bien:**
- Nuevo generator = nueva clase que override `RunProceduralGeneration()`. Extensión fácil.

**Riesgo:**
- Herencia profunda acopla demasiado (ej. `RoomFirstDungeonGenerator : SimpleRandomWalkDungeonGenerator` solo para reutilizar `RunRandomWalk`).

**Refactor recomendado:**
- composición por “steps”:
  - `IGenerationStep<HashSet<Vector2Int>>` o `IGenerationStep<GridMap>`.
  - pipeline configurable en el inspector (lista de steps).

### L — Liskov Substitution
- Generalmente OK: todos los `AbstractTilemapGenerator` cumplen el contrato de `Generate()`.
- Precaución: si un derived asume que `tilemapVisualizer` tiene tilesets específicos o wall tilemap asignado, la substitución se rompe a nivel de configuración.

### I — Interface Segregation
- Falta “capas” pequeñas: hoy todo depende de clases concretas.
- Interfaces propuestas:
  - `IFloorGenerator`, `IWallGenerator`, `IGridPostProcess`, `ITilemapVisualizer`.

### D — Dependency Inversion
**Problema principal:**
- Los generators dependen de `TilemapVisualizer` (concreto) y `WallGenerator` (estático), lo que dificulta portar a un backend distinto (mesh/noise).

**Refactor recomendado:**
- Invertir dependencias hacia interfaces:
  - `IGenerationOutput` (grid/mask/mesh),
  - `IRenderSink` (tilemap/mesh),
  - `IWallBuilder`.

---

## 7) WAUC (Workflow–Actors–Use cases–Couplings)

### Workflows (workflows reales que habilita)
- “Generate dungeon once” (editor/runtime).
- “Iterate: tweak params → regenerate”.
- “Generate → ApplyAutomata (smoothing) → Walls”.

### Actors
- **Level designer** (inspector): configura params y dispara generación.
- **Runtime system**: llama `Generate()` en start o por evento.
- **Tooling** (Editor): idealmente un botón o custom inspector (hoy no está).

### Use cases (API surface mínima)
- `Generate()` (pipeline base)
- `ApplyAutomata()` (post-proceso)
- `WallGenerator.CreateWalls(...)`
- `ProceduralGenerationAlgorithms.*` (para construir nuevos generators)

### Couplings (acoplamientos que importan)
- El modelo de datos real es `HashSet<Vector2Int>` (grid cells).
- `ApplyAutomata()` acoplado al estado del `Tilemap` → no es “pure function”.
- `TextureCreator` acoplado a `UnityEditor` → debería ir a carpeta/asmdef Editor o `#if UNITY_EDITOR`.

---

## 8) Recomendaciones puntuales (si lo quieres “productizar” para Islands)

1) **Crear un modelo intermedio** `GridMask` o `GridMap`:
   - width/height/origin + `HashSet<Vector2Int>` o bitset.
   - CA, walls, floodfill operan sobre esto (no sobre Tilemap).

2) **Extraer adaptadores Unity**:
   - `TilemapVisualizer` como implementación de `IGridPainter`.
   - Más adelante: `MeshVisualizer` para Islands surfaces/meshes.

3) **Hacer la generación determinista por seed**:
   - evitar `UnityEngine.Random` directo dentro de algoritmos o permitir inyectar RNG.

4) **Mover `TextureCreator` a Editor**:
   - o encapsular llamadas `UnityEditor` con `#if UNITY_EDITOR`.

5) **Reducir herencia en generators**:
   - convertir `RunRandomWalk(...)` en helper estático o servicio inyectable.

---

## 9) Lista rápida de “clases clave” (para leer/portar primero)

1) `AbstractTilemapGenerator`
2) `AbstractDungeonGenerator`
3) `SimpleRandomWalkDungeonGenerator`
4) `CorridorFirstDungeonGenerator`
5) `RoomFirstDungeonGenerator`
6) `RoomGridDungeonGenerator` (si vas a mantener rooms + manager)
7) `TilemapVisualizer`
8) `WallGenerator`
9) `WallTypesHelper`
10) `ProceduralGenerationAlgorithms`
11) `ShapeAlgorithms`

---

### Appendix: dependencias externas referenciadas (no incluidas en el zip)
- `DungeonTilesetSO`
- `SimpleRandomWalkSO`
- `CellularAutomataSO`
- `RectangularRoomDataSO`
- `EllipticalRoomDataSO`
- `GridDungeonRoomManager` (+ API)
