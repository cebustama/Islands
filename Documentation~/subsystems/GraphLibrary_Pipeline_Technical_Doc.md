# GraphLibrary — Pipeline técnico (Legacy Islands Engine)

> Basado en los archivos legacy: `IGraph.cs`, `AbstractGraph.cs`, `DirectedGraph.cs`, `UndirectedGraph.cs`, `PairValueImplementation.cs`, `AbstractNode.cs`.

Este documento explica **cómo se usa y cómo funciona internamente** GraphLibrary: desde **definir nodos**, **construir un grafo**, **agregar vértices/aristas**, hasta **consultar vecinos, grados y pesos**.

---

## 1) Modelo mental (pipeline de uso)

1. **Definir el tipo de vértice** (tu “nodo”): implementar `IVertex` (`GetName()`).
2. **Instanciar un grafo**: `DirectedGraph<T,K>` o `UndirectedGraph<T,K>`.
3. **Agregar vértices**: `AddVertex(...)`.
4. **Conectar aristas**: `AddEdge(v1, v2, weight)`.
5. **Consultar / recorrer**: `AdjacentVertices`, `AreAdjacent`, `Degree`, `GetWeight`, etc.
6. **Mutar** (limitado): `DeleteEdge`, `DeleteVertex` (con caveats).

---

## 2) Tipos principales y responsabilidades

### 2.1 `IVertex` (contrato mínimo de nodo)
- **Responsabilidad**: identificar un nodo (al menos por nombre).
- Método:
  - `string GetName()`

> Nota: aunque existe `AbstractNode` con `GetID()`, GraphLibrary **no lo requiere** para funcionar (solo `GetName()`).

### 2.2 `IGraph<T,K>` (contrato mínimo de grafo)
- **T**: tipo de vértice (`class`, `IVertex`)
- **K**: tipo de peso de arista

Define las operaciones típicas:
- Gestión de vértices: `AddVertex`, `GetVertex`, `DeleteVertex`, `VerticesNumber`, `GetVertexSet`
- Gestión de aristas: `AddEdge`, `DeleteEdge`, `AreAdjacent`, `EdgesNumber`, `GetEdgeSet`
- Métricas: `Degree`, `InDegree`, `OutDegree`
- Consulta de vecinos: `AdjacentVertices`
- Pesos: `GetWeight`

### 2.3 `AbstractGraph<T,K>` (almacenamiento + lógica común)
Implementa casi todo lo “genérico” y delega a subclases la semántica dirigida/no-dirigida.

**Estructuras internas (core)**
- `VertexSet : List<T>`  
  Lista de vértices.
- `VertexIndices : Dictionary<string, int>`  
  Índice por `vertex.GetName()` → posición en `VertexSet`.
- `EdgeSet : List<IPairValue<T>>`  
  Lista de aristas (como “pares”).
- `Weights : Dictionary<IPairValue<T>, K>`  
  Peso asociado a cada arista (clave = el par que representa la arista).

**Responsabilidad**
- Mantener colecciones e índices.
- Proveer operaciones de *alta/baja* de vértices.
- Proveer utilidades: `GetVertexOtherThan`, `GetVertexList`, etc.
- Declarar operaciones “dependientes del tipo de grafo” como `abstract`:
  - `AddEdge`, `DeleteEdge`, `AreAdjacent`, `GetWeight`, `Degree`, `InDegree`, `OutDegree`, `AdjacentVertices`.

> Nota: `GetDFSTree()` está declarado pero **no implementado** (retorna `null`).

### 2.4 `DirectedGraph<T,K>` (grafo dirigido)
- **Semántica**: una arista es exactamente un par ordenado `(v1 → v2)`.
- Implementa:
  - `AddEdge(v1, v2, weight)`
  - `DeleteEdge(v1, v2)`
  - `AreAdjacent(v1, v2)` = existe `(v1→v2)`
  - `GetWeight(v1, v2)`
  - `OutDegree`, `InDegree`, `Degree`
  - `AdjacentVertices(v)` = todos los `v2` tales que existe `(v→v2)`

### 2.5 `UndirectedGraph<T,K>` (grafo no dirigido)
**Diseño clave:** el grafo no dirigido se representa **duplicando la arista** como dos pares dirigidos:
- Para una arista lógica `{v1 — v2}`, se almacenan:
  - `(v1 → v2)` y `(v2 → v1)`
- Ambos pares apuntan al mismo peso.

Esto hace que el API sea cómodo (porque `AdjacentVertices` usa “aristas salientes”), pero introduce detalles importantes (ver sección 6).

### 2.6 `IPairValue<T>` + `DirectedPairValue<T>` / `UndirectedPairValue<T>`
Representan la “clave” de aristas.

- `DirectedPairValue<T>`:
  - Igualdad: `(a,b)` == `(a,b)` (orden importa)
  - HashCode: suma de hashes de ambos extremos
- `UndirectedPairValue<T>`:
  - Igualdad: `(a,b)` == `(b,a)` (orden NO importa)
  - HashCode: suma de hashes de ambos extremos

> Nota: en `UndirectedGraph`, la implementación real usa `DirectedPairValue` para las dos direcciones.

---

## 3) Flujo interno: vértices

### 3.1 `AddVertex(T vertex)`
1. Valida `vertex != null`
2. Si `VertexSet.Contains(vertex)`, no agrega.
3. Agrega a `VertexSet`.
4. Si `VertexIndices` no contiene `vertex.GetName()`, lo agrega apuntando al índice recién insertado.

**Implicación**
- La “identidad por nombre” (`GetName()`) se usa para indexar, **pero** el “no duplicar” se decide por `VertexSet.Contains(vertex)` (que depende de `Equals`/referencia).
- Puedes terminar con dos instancias diferentes con el mismo nombre si `Contains` no las considera iguales; el índice por nombre solo guarda la primera.

### 3.2 `GetVertex(string ID)`
- Busca `ID` en `VertexIndices`, retorna el elemento de `VertexSet` en ese índice.

**Implicación**
- Aquí `ID` es el **nombre** del vertex (no un ID numérico). En legacy el nombre “funciona como ID”.

### 3.3 `DeleteVertex(T vertex)` (limitación importante)
- Quita el vértice de `VertexSet`.
- **No** elimina aristas asociadas (hay un TODO).

**Efecto**
- Puedes quedar con aristas colgando en `EdgeSet` y `Weights` que referencian un vértice que ya no está en `VertexSet`.

---

## 4) Flujo interno: aristas y pesos

### 4.1 `AddEdge` (Dirigido)
Condiciones:
- `v1`, `v2`, `weight` no pueden ser `null` (ver sección 6 sobre `K` value-type).
- Ambos vértices deben existir en `VertexSet`.

Proceso:
1. Construye `pair = new DirectedPairValue<T>(v1, v2)`
2. Si `EdgeSet.Contains(pair)`: no agrega.
3. Agrega `pair` a `EdgeSet`.
4. Setea `Weights[pair] = weight`.

### 4.2 `AddEdge` (No dirigido)
Condiciones iguales.

Proceso:
1. Crea dos pares:
   - `pair1 = (v1→v2)`, `pair2 = (v2→v1)`
2. Si alguno ya existe, no agrega.
3. Agrega ambos a `EdgeSet`.
4. Setea `Weights[pair1] = weight` y `Weights[pair2] = weight`.

**Consecuencia**
- Hay dos entradas en `EdgeSet` y dos claves en `Weights` por una sola arista lógica.

### 4.3 `GetWeight(v1, v2)`
- Construye `new DirectedPairValue<T>(v1, v2)` y busca en `Weights`.
- En `UndirectedGraph`, esto funciona porque existen ambas direcciones.

### 4.4 `AreAdjacent(v1, v2)`
- Dirigido: existe `(v1→v2)`
- No dirigido: deben existir **ambas** `(v1→v2)` y `(v2→v1)`

### 4.5 `DeleteEdge(v1, v2)`
- Dirigido: elimina `(v1→v2)` y su peso
- No dirigido: elimina `(v1→v2)` y `(v2→v1)` y ambos pesos

---

## 5) Consultas y recorridos

### 5.1 Vecinos: `AdjacentVertices(vertex)`
Implementación común:
- Itera por `EdgeSet`
- Si `pair.GetFirst().Equals(vertex)`, retorna `pair.GetSecond()`

**Interpretación**
- En ambos grafos, “vecinos” = **vecinos salientes** (out-neighbors).

### 5.2 Grados
- Dirigido:
  - `OutDegree` = cantidad de pares donde `first == vertex`
  - `InDegree` = cantidad de pares donde `second == vertex`
  - `Degree` = In + Out
- No dirigido:
  - Se implementa igual, pero como hay doble arista almacenada, los grados reflejan esa duplicación (ver sección 6).

---

## 6) Decisiones de diseño, implicaciones y caveats

### 6.1 `K weight == null` obliga a pensar el tipo de peso
`AddEdge` valida `weight == null` en ambos grafos.

**Implicación práctica**
- Si pretendes usar `K = int` o `float` (value types), este chequeo puede ser problemático:
  - En C# genérico, `K` puede ser struct y `weight == null` no es una comparación “natural”.
- Soluciones habituales:
  - Usar `float?` / `int?` (nullable)
  - O eliminar esa validación y usar `default(K)` como “peso inválido” solo si te sirve.

### 6.2 Duplicación de aristas en `UndirectedGraph`
Al almacenar `(v1→v2)` y `(v2→v1)`:
- `EdgesNumber()` cuenta el doble de aristas lógicas.
- Los grados (In/Out) también se ven “doblados” respecto a una representación clásica no dirigida.

**Qué ganas**
- Consultas de vecinos triviales y consistentes con dirigido.

**Qué pierdes**
- Métricas y conteos requieren interpretación (“lógico” vs “almacenado”).

### 6.3 Identidad de vértices por `GetName()`
- `VertexIndices` usa `GetName()` como clave.
- `AddVertex` evita duplicados por `VertexSet.Contains(vertex)`.

**Riesgo**
- Dos vértices distintos con mismo `GetName()` → el índice por nombre solo apunta a uno.

**Recomendación**
- Decide una política clara:
  - o “GetName es único” (y validas eso al agregar),
  - o “GetName es etiqueta” (y no lo usas como clave única).

### 6.4 `DeleteVertex` deja aristas colgantes
Actualmente hay un TODO para eliminar edges que contengan el vértice.
Si planeas mutar grafos, esto es lo primero a arreglar.

### 6.5 Complejidad
La estructura actual es “simple y legible” pero:
- `EdgeSet.Contains(...)` es O(E)
- `AdjacentVertices` es O(E)
- `VertexSet.Contains(...)` es O(V)

Para grafos pequeños/medianos (POIs, rutas) es correcto; para grafos masivos conviene un `Dictionary<T, HashSet<T>>` (adjacency map).

---

## 7) Ejemplo de uso (pipeline completo)

```csharp
using GraphLibrary;

// 1) define/usa un nodo (ej. GenericNode)
var a = new GenericNode { Name = "A" };
var b = new GenericNode { Name = "B" };

// 2) crea grafo (no dirigido)
var g = new UndirectedGraph<GenericNode, float?>();

// 3) agrega vertices
g.AddVertex(a);
g.AddVertex(b);

// 4) agrega arista con peso
g.AddEdge(a, b, 3.5f);

// 5) consulta vecinos
foreach (var n in g.AdjacentVertices(a))
    UnityEngine.Debug.Log(n.GetName()); // "B"

// 6) consulta peso
var w = g.GetWeight(a, b); // 3.5
```

---

## 8) Integración en el package nuevo (Islands 0.1.0-preview)

### 8.1 ¿Basta copiar/pegar la carpeta GraphLibrary?
**Sí, en la práctica suele bastar**, con estas condiciones:

1) **Ubicación recomendada**  
   Copia la carpeta completa dentro de `Runtime/` del package nuevo, por ejemplo:
   - `Runtime/Graphs/GraphLibrary/*` **o**
   - `Runtime/DataStructures/Graphs/*` (si quieres reflejar tu estructura legacy)

2) **Assembly Definition (asmdef)**  
   Tu package nuevo ya tiene `Runtime/Islands.Runtime.asmdef`.  
   Si GraphLibrary queda debajo de `Runtime/` y no creas un asmdef nuevo en el subárbol, Unity lo compilará dentro del assembly `Islands.Runtime`.

3) **Namespaces**  
   Hoy la librería vive en `namespace GraphLibrary`. Eso está bien.  
   Si deseas coherencia con el package, puedes renombrar después a `Islands.DataStructures.Graphs`, pero no es necesario para “rescatarla”.

4) **.meta files**  
   Para **código C# puro**, Unity puede regenerar `.meta` sin problema.  
   Si en el futuro referenciaras assets por GUID, entonces sí conviene mantenerlos. En esta carpeta (puro `.cs`) normalmente da igual.

### 8.2 Recomendación mínima antes de usar en producción
Si la quieres usar para generación procedural (rutas/POIs), te recomendaría 2 ajustes rápidos:
- (A) Arreglar `DeleteVertex` para que también elimine edges + weights asociados.
- (B) Definir explícitamente la política de unicidad de `GetName()` (o dejar de usarlo como clave única).

---

## 9) Checklist de “sanidad” al copiar a Islands nuevo
- [ ] Compila sin errores (ojo con `weight == null` si usarás `K` struct).
- [ ] No hay colisión de nombres/espacios de nombres con Samples.
- [ ] Si vas a usar `GetVertex(name)`, garantiza `GetName()` único.
- [ ] Si vas a borrar vértices, implementa borrado de edges.

---

## 10) Glosario rápido

- **Vértice / Nodo**: entidad `T : IVertex`.
- **Arista**: `IPairValue<T>` (par de nodos). En no-dirigido se duplican.
- **Peso**: valor `K` asociado a una arista (guardado en `Weights`).
- **Vecinos salientes**: nodos alcanzables desde un nodo siguiendo `pair.First → pair.Second`.

