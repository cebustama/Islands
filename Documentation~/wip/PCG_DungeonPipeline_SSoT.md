# PCG Dungeon Generation Pipeline — SSoT / Design Bible (Estado Actual)

> **Scope:** Este documento describe el pipeline **tal como existe hoy** en tu carpeta PCG (Tilemap-based).  
> Objetivo: que sea un **Single Source of Truth (SSoT)** del “contract” actual: componentes, datos, responsabilidades, flujo de ejecución, puntos de extensión y limitaciones.

---

## 0) TL;DR (qué hace el sistema)
- Genera sets de celdas de piso (`HashSet<Vector2Int> floorPositions`) usando algoritmos clásicos:
  - random walk iterado, corridor-first, room-first (BSP + corredores), room-grid (camino de rooms), y generadores de formas (rect/circle/line).
- Pinta el resultado en un **Tilemap** (piso) y luego calcula **walls** en un Tilemap separado usando **bitmasks de vecinos** y un tileset configurable.
- Permite post-proceso con **Cellular Automata** (CA) que “suaviza” el piso, pero actualmente el CA **lee desde el Tilemap** (no desde un modelo de datos puro).

---

## 1) Modelo de datos canónico (contract de datos)

### 1.1 Coordenadas
- Todo el pipeline opera sobre **celdas en grid** representadas como `Vector2Int` (x,y).

### 1.2 Representación de piso (floor)
- El “output” principal de cualquier generator es:
  - `HashSet<Vector2Int> floorPositions`
- Convención: una celda en `floorPositions` se considera “ocupada / floor = 1”.

### 1.3 Representación de walls
- Las walls NO se almacenan como modelo. Se derivan “on the fly” desde `floorPositions`:
  - “Wall position” = celda adyacente al piso (cardinal o diagonal, según tipo).
- El **tipo** de wall se decide con un **bitmask de vecinos**:
  - basic walls: 4 direcciones (N,E,S,W) → string binario de 4 bits.
  - corner walls: 8 direcciones → string binario de 8 bits.
- Ese bitmask se convierte a int (base 2) y se clasifica con `WallTypesHelper` para elegir qué tile pintar.

### 1.4 Bounds / límites del área pintada
- `TilemapVisualizer` mantiene `minX/maxX/minY/maxY` basándose en las celdas que se pintan.
- Esos límites se usan por `AbstractTilemapGenerator.ApplyAutomata()` para saber qué área convertir a `cellStates[,]`.

---

## 2) Componentes principales y responsabilidades

## 2.1 Orquestación (entrypoints)
### `AbstractTilemapGenerator` (MonoBehaviour)
**Responsabilidad:** “pipeline template” para generar y (opcionalmente) aplicar CA.

**API pública (contract)**
- `Generate()`
  - `tilemapVisualizer.Clear()`
  - `RunProceduralGeneration()` (abstract)
- `ApplyAutomata()`
  - construye `cellStates[,]` leyendo tiles existentes del `floorTilemap`
  - aplica `ProceduralGenerationAlgorithms.ApplyCAStep(...)` por N iteraciones
  - reconstruye `newFloorPositions` y repinta el piso

**Hook principal**
- `protected abstract void RunProceduralGeneration();`

> Implicación: el pipeline actual mezcla “modelo” + “render”. `ApplyAutomata()` depende del estado pintado.

---

### `AbstractDungeonGenerator : AbstractTilemapGenerator`
**Responsabilidad:** extender el post-proceso de CA para que, si corresponde, también genere walls.

**Comportamiento**
- Override de `ApplyAutomata()`:
  - llama a `base.ApplyAutomata()`
  - si `addWalls`: llama `WallGenerator.CreateWalls(newFloorPositions, tilemapVisualizer)`

---

## 2.2 Visualización / Render
### `TilemapVisualizer` (MonoBehaviour)
**Responsabilidad:** pintar tiles y seleccionar variantes del tileset.

**Inputs principales**
- `Tilemap floorTilemap`
- `Tilemap wallTilemap`
- `DungeonTilesetSO tileset`

**API pública (contract)**
- `PaintFloorTiles(IEnumerable<Vector2Int> floorPositions)`
  - pinta un tile de piso aleatorio desde `tileset.floorTile[]`
- `PaintTiles(Dictionary<Vector2Int, TileBase> tiles, Tilemap tilemap)`
  - útil para overworld
- `PaintSingleBasicWall(Vector2Int position, string binaryType)`
- `PaintSingleCornerWall(Vector2Int position, string binaryType)`
- `Clear()`
- `GetFloorTilemap()`

**Detalle importante**
- La selección de tile (variantes) usa `UnityEngine.Random.Range(...)`.
- `PaintSingleTile` convierte `Vector2Int` a `Vector3Int` y usa `tilemap.WorldToCell(...)`.

---

### `DungeonTilesetSO` (ScriptableObject)
**Responsabilidad:** “atlas lógico” de tiles para el visualizador.

- `floorTile[]`
- walls simples: `wallTop`, `wallSideRight`, `wallBottom`, `wallSideLeft`, `wallFull`
- walls esquinas/diagonales: `wallInnerCornerDownLeft/Right`, `wallDiagonalCorner...`

---

## 2.3 Walls
### `WallGenerator` (static)
**Responsabilidad:** derivar walls desde `floorPositions` y pedir al visualizer que pinte el tile correcto.

**Contract**
- `CreateWalls(HashSet<Vector2Int> floorPositions, TilemapVisualizer tilemapVisualizer)`
- Overload: `CreateWalls(..., BoundsInt bounds)` (para limitar pintura a un área)

**Algoritmo (resumen)**
1. `FindWallsInDirections(floor, cardinalDirs)` → `basicWallPositions`
2. `FindWallsInDirections(floor, diagonalDirs)` → `cornerWallPositions`
3. Por cada posición de wall:
   - `BuildNeighborBinaryType(position, floor, dirs)` → string "0101..."
   - `tilemapVisualizer.PaintSingleBasicWall/CornerWall(position, binaryType)`

---

### `WallTypesHelper` (static catalog)
**Responsabilidad:** sets de patrones binarios válidos que corresponden a “categorías” de wall:
- top/side/bottom/full
- inner corners, diagonal corners, etc.

> Nota: quien realmente “consume” esto es `TilemapVisualizer` al decidir qué tile usar.

---

## 2.4 Algoritmos puros (core)
### `ProceduralGenerationAlgorithms` (static)
**Responsabilidad:** producir conjuntos/listas de celdas (floor) y utilidades de partición/CA.

Incluye:
- Random walk:
  - `SimpleRandomWalk`, `IteratedSimpleRandomWalk`
  - variantes `List` (`SimpleRandomWalkList`, `IteratedSimpleRandomWalkList`)
  - `RandomWalkCorridor`
- BSP rooms:
  - `BinarySpacePartitioning(BoundsInt spaceToSplit, int minWidth, int minHeight)`
- Cellular Automata:
  - `ApplyCAStep(int[,] oldMap, lonelyDeathLimit, crowdDeathLimit, birthLimit)`
- `Direction2D` (dirs cardinal/diagonal/8 + random dir con skew)
- `MinMaxInt` (rango con `GetValue()`)

---

### `ShapeAlgorithms` (static)
**Responsabilidad:** rasterizar formas en grid (HashSet<Vector2Int>).

Incluye (según el archivo):
- `Rectangle(width, height, origin)`
- `Circle(radius, origin, fill, algorithm)`
- `Line(pointA, pointB, brushSize, algorithm)`
- Flood fill y otras utilidades geométricas (si están en tu versión).

---

## 3) Configuración (ScriptableObjects que definen “el contrato” real)

### `SimpleRandomWalkSO`
- `iterations`
- `walkLength : MinMaxInt`
- `steps : MinMaxInt` (override/limit para cantidad de pasos únicos)
- `skewX`, `skewY`
- `randomStartChance`

**Uso típico**
- Es consumido por `SimpleRandomWalkDungeonGenerator` (y derivados) para `RunRandomWalk(...)`.
- Es usado también por `RoomGridDungeonGenerator` para generar una secuencia de coords de rooms vía `IteratedSimpleRandomWalkList(...)`.

---

### `CellularAutomataSO`
- `iterations`
- `lonelyDeathLimit`
- `crowdDeathLimit`
- `birthLimit`

**Uso**
- `AbstractTilemapGenerator.ApplyAutomata()` aplica el CA N veces.

---

### `RectangularRoomDataSO`
- `roomWidth : MinMaxInt`
- `roomHeight : MinMaxInt`
- `wallSize`

**Uso**
- `SimpleRectangularRoomGenerator` y `RoomGridDungeonGenerator`.

---

### `EllipticalRoomDataSO`
- `radius : MinMaxInt`
- `algorithm : ShapeAlgorithms.CircleAlgorithms`
- `fill`
- `wallSize`

**Uso**
- `CircularRoomGenerator`

---

## 4) Generadores concretos (estrategias actuales)

## 4.1 Dungeon generators
### `SimpleRandomWalkDungeonGenerator : AbstractDungeonGenerator`
**Estrategia:** floor = random walk iterado.
**Flujo interno:**
1. `floorPositions = RunRandomWalk(randomWalkParameters, startPosition)`
2. `tilemapVisualizer.Clear()`
3. `tilemapVisualizer.PaintFloorTiles(floorPositions)`
4. si `addWalls`: `WallGenerator.CreateWalls(...)`

---

### `CorridorFirstDungeonGenerator : SimpleRandomWalkDungeonGenerator`
**Estrategia:** corredores primero + rooms en endpoints/dead-ends.
**Flujo:**
1. `CreateCorridors(...)` → agrega floor y acumula `potentialRoomPositions`
2. `CreateRooms(...)` → subset de potential positions + `RunRandomWalk(...)`
3. `FindAllDeadEnds(...)`
4. `CreateRoomsAtDeadEnd(...)` si falta room en dead-end
5. pinta + walls

> Caveat: usa `Guid.NewGuid()` para shuffle (no seedable fácilmente).

---

### `RoomFirstDungeonGenerator : SimpleRandomWalkDungeonGenerator`
**Estrategia:** BSP rooms + conectar centros con corredores Manhattan.
**Flujo:**
1. `roomsList = BinarySpacePartitioning(...)`
2. `CreateSimpleRooms` o `CreateRandomWalkRooms`
3. `ConnectRooms(roomCenters)` → `CreateCorridor(current, closest)`
4. pinta + walls

---

### `RoomGridDungeonGenerator : SimpleRectangularRoomGenerator`
**Estrategia:** dungeon como “camino” de rooms discretas en una grilla, con puertas y metadata de conexiones.

**Dependencias externas**
- `roomManager` (y `GridRoomManager`) *no están en este SSoT* (se requiere para contract completo).

**Flujo general**
- genera `roomCoordsList` con `IteratedSimpleRandomWalkList(...)`
- por cada coord: crea room rectangular + doors con `CreateDoor(...)`
- registra rooms y conexiones en el manager
- pinta + walls

---

## 4.2 Shape/room generators (pruebas unitarias de formas)
### `SimpleRectangularRoomGenerator : AbstractDungeonGenerator`
- crea un rectángulo base con `ShapeAlgorithms.Rectangle(...)`
- (door placement está implementado como helper `CreateDoor(...)` pero actualmente no se mezcla al floor)
- walls con `BoundsInt` para “door placement” futuro

---

### `CircularRoomGenerator : AbstractDungeonGenerator`
- crea un círculo (o futuro elipse) con `ShapeAlgorithms.Circle(...)`
- walls usando overload con `BoundsInt`
- tiene TODOs: `wallSize configurable` y “FIX WALLS”

---

### `CorridorGenerator : AbstractDungeonGenerator`
- crea un corredor simple con `ShapeAlgorithms.Line(pointA, pointB, brushSize=1, algorithm)`
- dibuja gizmos para debug

---

## 4.3 Overworld generator (no dungeon, comparte infra)
### `OverworldGenerator : AbstractTilemapGenerator`
- crea un mapa `Dictionary<Vector2Int, TileBase>` a partir de Perlin Noise (Unity)
- pinta con `tilemapVisualizer.PaintTiles(...)` en el `floorTilemap`
- soporta regeneración en `OnValidate()` si `onValidateGeneration`

---

## 5) Flujo de ejecución (call flow) por escenario

## 5.1 “Generate dungeon” (cualquier dungeon generator)
1. Usuario llama `Generate()` en el componente generator.
2. `AbstractTilemapGenerator.Generate()`:
   - `tilemapVisualizer.Clear()`
   - `RunProceduralGeneration()` del generator concreto
3. En `RunProceduralGeneration()`:
   - calcula `floorPositions`
   - pinta piso con `tilemapVisualizer.PaintFloorTiles(...)`
   - si corresponde: `WallGenerator.CreateWalls(...)` → `TilemapVisualizer.PaintSingleBasicWall/CornerWall(...)`

---

## 5.2 “Apply cellular automata smoothing”
1. Usuario llama `ApplyAutomata()`.
2. `AbstractTilemapGenerator.ApplyAutomata()`:
   - bounds = `minX..maxX` etc
   - lee del `floorTilemap` → `cellStates[,]`
   - aplica `ApplyCAStep` N veces
   - reconstruye `newFloorPositions`
   - `tilemapVisualizer.Clear()` + `PaintFloorTiles(newFloorPositions)`
3. Si es dungeon generator:
   - `AbstractDungeonGenerator.ApplyAutomata()` agrega walls post-CA.

---

## 6) Contratos implícitos (lo que “debe existir” para que funcione)

### Requerimientos mínimos para dungeons tilemap-based
- Un `TilemapVisualizer` configurado con:
  - `floorTilemap` asignado
  - `wallTilemap` asignado
  - `tileset : DungeonTilesetSO` con arrays no vacíos
- Un generator que herede de `AbstractTilemapGenerator` (o dungeon) con:
  - `startPosition` definido
  - SOs asignados (si el generator lo requiere)

### Contrato de walls
- `WallTypesHelper` define el espacio de “tipos” posibles.
- `TilemapVisualizer` debe mapear bitmask→tile. Si un bitmask cae fuera de sets, no pinta nada.

---

## 7) Limitaciones y riesgos del diseño actual (importante para migración a Islands)

1) **Modelo acoplado al Tilemap en CA**
   - `ApplyAutomata()` lee tiles reales; no se puede reutilizar en backends sin Tilemap sin reescribir.

2) **No determinismo / seed**
   - `TilemapVisualizer` usa `UnityEngine.Random` para elegir variantes.
   - Generadores usan `Random.Range(...)` y en un caso `Guid.NewGuid()` para shuffle.
   - Esto hace difícil garantizar reproducibilidad.

3) **Performance**
   - `HashSet<Vector2Int>` + loops + conversiones tilemap: OK para prototipo, subóptimo para Burst/SIMD.

4) **Coordenadas / conversión**
   - `PaintSingleTile` usa `WorldToCell((Vector3Int)position)` con un cast no convencional para “grid coords”. Funciona si tu world coords == cell coords, pero es un punto a revisar.

5) **RoomGridDungeonGenerator está incompleto sin managers**
   - Para documentar el contract completo (doors/connections/room data), faltan los scripts del manager.

---

## 8) Puntos de extensión “oficiales” (dónde crecer sin romper todo)
- Crear nuevos generators: heredar `AbstractTilemapGenerator` y definir `RunProceduralGeneration()`.
- Crear nuevas shapes: agregar métodos a `ShapeAlgorithms`.
- Crear nuevos algoritmos: agregar a `ProceduralGenerationAlgorithms`.
- Cambiar tilesets: implementar `DungeonTilesetSO` distinto.
- Cambiar clasificación de walls: editar `WallTypesHelper` + tileset arrays.

---

## 9) Qué falta para un contract 100% completo (siguiente batch recomendado)
Para cerrar completamente `RoomGridDungeonGenerator`, y para tener el “contrato de rooms/connections”:
- `GridDungeonRoomManager` / `GridRoomManager` y cualquier `Room`, `RoomConnection`, etc.
- Cualquier Editor script que haga UI de botones o tooling (si existe).

---

---

## 10) Capa de ejemplo: Room System (GridRoomManager) y cómo usar el pipeline en un proyecto

> **Idea clave:** el **pipeline PCG** (floors/walls) funciona sin este sistema.  
> El **Room System** es una **capa de gameplay/ejemplo** que toma lo generado (tiles) y lo transforma en “habitaciones” con:
> - bounds + collider,
> - cámara por habitación (Cinemachine),
> - activación/desactivación de enemigos/objetos,
> - y triggers de transición (`RoomConnection`) basados en “puertas”.

### 10.1 Componentes del Room System

#### `RoomManager` (base)
- Mantiene:
  - `rooms : List<Room>` y `entrances : List<int>` (índices de rooms marcadas como entrada).
  - `virtualCameraPrefab` para instanciar una cámara por room.
- Ciclo típico:
  - `Clear()` destruye rooms existentes (hijos del manager) y reinicia listas.
  - `CreateRoomConnections()` llama `CreateConnections()` en cada `Room`.
  - `FillRoomsData()` es un hook para postprocesos (vacío en base).
- En `Start()`, busca al player y activa la cámara del room que contiene su posición.

#### `GridRoomManager : RoomManager`
- Agrega un “índice espacial”:
  - `roomGrid : Dictionary<Vector3Int, GridRoom>` (coords discretas → room).
- Construcción de rooms:
  - `CreateGridRoom(BoundsInt roomBounds, Vector3Int roomCoords, bool isEntrance)`:
    1) `CreateRoomObject()` (GameObject hijo)
    2) añade componente `GridRoom`
    3) `GridRoom.Setup(this, bounds)` (collider + containers)
    4) `GridRoom.SetupCamera(virtualCameraPrefab)` (Cinemachine confiner)
    5) asigna `coords`, `isEntrance`, registra en `roomGrid` y en `rooms/entrances`.
- Almacenamiento de tiles por room:
  - `AddRoomTiles(roomCoords, Tilemap map, HashSet<Vector2Int> newTiles)` → `GridRoom.AddTiles(...)`.
- Post-proceso “grid graph”:
  - `FillRoomsData()` recorre desde cada entrada y calcula:
    - `gridDistanceFromEntrance`,
    - y resuelve links de conexiones (`RoomConnection.connectsTo`) usando coordenadas en grilla.
  - `TraverseRecursive(GridRoom room, int pathDistance, ref HashSet<Vector3Int> traversed)`:
    - para cada `RoomConnection rc` en `room.roomConnections`:
      - `nextRoomCoords = room.coords + rc.entranceDirection`
      - `nextRoom = roomGrid[nextRoomCoords]` si existe
      - `rc.connectsTo = nextRoom`
      - `room.AddGridRoomConnection(rc.entranceDirection, nextRoom)`

#### `GridDungeonRoomManager : GridRoomManager`
- Clase vacía en el ejemplo: sirve como “tipo concreto” para dungeons.

#### `Room` (base gameplay room)
- `Setup(RoomManager m, BoundsInt bounds)`:
  - guarda `roomBounds`,
  - crea `PolygonCollider2D` con path rectangular,
  - crea contenedores hijos (Enemies/Doors/etc.).
- `SetupCamera(prefab)`:
  - instancia un `CinemachineVirtualCamera`,
  - setea `Follow` al objeto con tag `"Player"`,
  - configura `CinemachineConfiner` con el `roomBoundsCollider`.
- **Tile storage**:
  - `tilesDictionary : Dictionary<Tilemap, HashSet<Vector2Int>>`
  - `AddTiles(map, newTiles)` une sets (por layer/tilemap).
  - `FindTile(coords)` busca si una celda pertenece a algún tilemap registrado para esta room.
- **Conexiones**:
  - `CreateConnections()` detecta “door tiles” recorriendo el borde del rectángulo (min/max x/y) y llamando `FindTile`:
    - por cada door tile encontrado, crea un GameObject “Connection N”
    - le agrega `RoomConnection` + `BoxCollider2D` trigger
    - setea `entranceDirection` según el lado del borde
    - agrega el `RoomConnection` a `roomConnections`

#### `RoomConnection`
- `SetupConnection(Room room, HashSet<Vector2Int> connectionTiles, Vector3Int entranceDir, bool door)`
  - registra `parentRoom`, marca `isDoor`, crea `BoxCollider2D` trigger,
  - calcula `exitDirection = -entranceDirection`.
- Runtime:
  - En `OnTriggerEnter2D` / `OnTriggerExit2D` decide si el player está:
    - entrando al área “visible” (cambio de cámara/activación),
    - cruzando puerta (playable enter/exit).
  - `OnEnterVisibleRoom(...)` hace el “swap”:
    - desactiva objetos/cámara del room anterior (`connectsTo`)
    - activa objetos/cámara del room actual (`parentRoom`)
    - actualiza `player.currentRoomID`.

#### `GridRoom : Room`
- Agrega:
  - `coords : Vector3Int`
  - `gridDistanceFromEntrance : int`
  - `gridRoomConnections : List<(direction, GridRoom)>` (vecinos resueltos)

#### Rooms especializadas (ejemplo)
- `DungeonEnemyRoom : GridRoom`
  - En `OnTriggerEnter2D`, activa enemigos/objetos y **cierra puertas**.
  - `CheckEnemies()` abre puertas al quedar sin enemigos.
- `DungeonSwitchRoom`, `OverworldRoom` son placeholders.

---

### 10.2 Cómo encaja esto con el pipeline PCG (flujo “end-to-end”)

**El pipeline PCG genera tiles. El Room System los consume.**  
El punto de unión es: **“a qué room pertenece este tile”**.

Un flujo típico con `RoomGridDungeonGenerator` (del batch anterior) es:

1) **Generación**
- El generator crea un path de coordenadas de rooms (grid coords).
- Para cada room:
  - calcula `BoundsInt roomBounds` y `Vector3Int roomCoords`,
  - pide al manager crear la room: `CreateGridRoom(roomBounds, roomCoords, isEntrance)`,
  - genera los tiles de piso (HashSet<Vector2Int>) y los pinta,
  - y registra los tiles en la room: `AddRoomTiles(roomCoords, floorTilemap, newTiles)`.

2) **Conexiones**
- Al terminar, el generator llama:
  - `roomManager.CreateRoomConnections()`
    - cada `Room` ejecuta `CreateConnections()` y crea triggers en door tiles.

3) **Linking + Data**
- Luego llama:
  - `roomManager.FillRoomsData()`
    - `GridRoomManager` recorre la grilla desde entradas y:
      - resuelve `RoomConnection.connectsTo`,
      - calcula `gridDistanceFromEntrance`,
      - rellena `gridRoomConnections`.

4) **Runtime**
- Cuando el player toca un `RoomConnection`:
  - se cambia cámara/activación de objetos por room,
  - y (si quieres) se gatillan lógicas por tipo de room (ej. cerrar puertas en `DungeonEnemyRoom`).

---

### 10.3 Ejemplo de integración (escena mínima)

**Objetivo:** usar el pipeline PCG para generar un dungeon y tener rooms con cámaras por habitación.

1) **Tilemaps**
- Crea dos Tilemaps: `FloorTilemap` y `WallTilemap`.
- Crea `DungeonTilesetSO` y asigna floor + wall tiles.

2) **Visualizer**
- Agrega `TilemapVisualizer` a un GameObject y asigna:
  - `floorTilemap`, `wallTilemap`, `tileset`.

3) **Room Manager**
- Crea un GameObject `RoomManager` con `GridDungeonRoomManager`.
- Asigna `virtualCameraPrefab`:
  - prefab con `CinemachineVirtualCamera` + `CinemachineConfiner`.
- (Opcional) configura prefabs de puertas, switches, enemigos.

4) **Generator**
- En un GameObject `Generator`, agrega `RoomGridDungeonGenerator` (o el generator que uses).
- Asigna:
  - `tilemapVisualizer`,
  - `roomManager`,
  - `startPosition`,
  - SOs: `SimpleRandomWalkSO`, `RectangularRoomDataSO`, etc.
- Ejecuta `Generate()`.

5) **Player**
- Asegura que el player tenga tag `"Player"` y un componente `PlayerMovement`
  (en este ejemplo, `RoomConnection` depende de eso).

> Resultado: generas dungeon → rooms almacenan sus tiles → se crean triggers de puertas → al moverte, cambia la cámara y se activan/desactivan objetos por habitación.

---

### 10.4 Qué conviene “genericizar” si lo queremos dentro de Islands

Si queremos reutilizar esta capa en Islands Engine (sin depender del proyecto ejemplo), conviene:

- Separar “core room graph” de runtime:
  - **Core:** `RoomBounds`, `RoomId`, `RoomCoords`, lista de conexiones, distancia a entrada.
  - **Runtime adapters:** Cinemachine, PlayerMovement, puertas/enemigos, activación de GameObjects.
- Reemplazar `GetHashCode()` como ID con un ID estable/determinista.
- Hacer `RoomConnection` event-driven:
  - emitir eventos `OnEnterVisible`, `OnEnterPlayable`, etc. en vez de tocar directamente cámaras/activación.
- No basar CA ni búsquedas en Tilemap:
  - en Islands-style, el modelo sería `GridMask`/layers y el Tilemap sería un adapter.


## 10) Notas de migración a Islands (solo como puente)
- El “corazón portable” a Burst/SIMD es:
  - `ProceduralGenerationAlgorithms`, `ShapeAlgorithms`, la lógica de walls por vecindad.
- El primer paso Islands-style es reemplazar:
  - `HashSet<Vector2Int>` → `GridMask` (bitset/byte) + `GridDomain`
  - `UnityEngine.Random` → `Unity.Mathematics.Random` con seed
  - `TilemapVisualizer` → adapter (Tilemap/Texture/Mesh)
