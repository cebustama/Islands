# GraphLibrary — Technical Reference Snapshot (Historical Support)

> **Status:** Historical technical support  
> **Version:** 0.1.2 · normalized 2026-03-31 after Batch 7  
> **Primary implemented truth:** `Runtime/Graphs/GraphLibrary/*.cs`  
> **Reference-facing governed summary:** `Documentation~/reference/graphs.md`  
> **Superseded by:** `Documentation~/reference/graphs.md` as the active governed reference surface  
> **Absorbed into:** `Documentation~/reference/graphs.md` for the active reusable explanation layer  
> **Authority note:** This file is retained inside the governed documentation tree as a **deep technical snapshot / historical support** document. It is **not** primary subsystem authority.

> Basado en los archivos runtime: `IGraph.cs`, `AbstractGraph.cs`, `DirectedGraph.cs`, `UndirectedGraph.cs`, `PairValueImplementation.cs`, `AbstractNode.cs`.


Este documento explica cómo funciona internamente GraphLibrary, pero después de Batch 5 su rol queda explícitamente acotado:

- **runtime code** = verdad implementada actual
- **`Documentation~/reference/graphs.md`** = superficie reference/support gobernada
- **este archivo** = apoyo técnico histórico / deep-support para detalles internos, caveats y contexto de rescate

---

## 1) Modelo mental de uso

1. Definir el tipo de vértice: implementar `IVertex` (`GetName()`).
2. Instanciar un grafo: `DirectedGraph<T,K>` o `UndirectedGraph<T,K>`.
3. Agregar vértices: `AddVertex(...)`.
4. Conectar aristas: `AddEdge(v1, v2, weight)`.
5. Consultar / recorrer: `AdjacentVertices`, `AreAdjacent`, `Degree`, `GetWeight`, etc.
6. Mutar: `DeleteEdge`, `DeleteVertex`.

> Importante: la parte de mutación sigue teniendo caveats relevantes. GraphLibrary existe y es usable, pero no está endurecida todavía como superficie de autoridad propia.

---

## 2) Tipos principales y responsabilidades

### 2.1 `IVertex` (contrato mínimo de nodo)
- **Responsabilidad**: identificar un nodo al menos por nombre
- Método:
  - `string GetName()`

> Aunque existe `AbstractNode` con `GetID()`, GraphLibrary no lo requiere para funcionar. El contrato mínimo real sigue siendo `GetName()`.

### 2.2 `IGraph<T,K>` (contrato mínimo de grafo)
- **T**: tipo de vértice (`class`, `IVertex`)
- **K**: tipo de peso de arista

Define las operaciones principales:
- gestión de vértices: `AddVertex`, `DeleteVertex`, `VerticesNumber`, `GetVertexSet`
- gestión de aristas: `AddEdge`, `DeleteEdge`, `AreAdjacent`, `EdgesNumber`, `GetEdgeSet`
- métricas: `Degree`, `InDegree`, `OutDegree`
- consulta de vecinos: `AdjacentVertices`
- pesos: `GetWeight`

> Corrección importante: `GetVertex(string id)` **no** forma parte de `IGraph<T,K>`; existe como conveniencia en `AbstractGraph<T,K>`.

### 2.3 `AbstractGraph<T,K>` (almacenamiento + lógica común)
Implementa casi toda la plomería genérica y delega a subclases la semántica dirigida / no-dirigida.

**Estructuras internas**
- `VertexSet : List<T>`
- `VertexIndices : Dictionary<string, int>`
- `EdgeSet : List<IPairValue<T>>`
- `Weights : Dictionary<IPairValue<T>, K>`

**Responsabilidad**
- mantener colecciones e índices
- proveer alta / baja de vértices
- proveer utilidades (`GetVertexOtherThan`, `GetVertexList`, `GetVertex(string id)`)
- declarar operaciones abstractas dependientes del tipo de grafo

> `GetDFSTree()` sigue sin implementación y retorna `null`.

> El constructor que acepta arrays de `vertices` y `edges` copia esos valores a `VertexSet` / `EdgeSet`, pero no reconstruye `VertexIndices` ni `Weights`.

### 2.4 `DirectedGraph<T,K>` (grafo dirigido)
- una arista es exactamente un par ordenado `(v1 -> v2)`
- implementa `AddEdge`, `DeleteEdge`, `AreAdjacent`, `GetWeight`, grados y vecinos salientes

### 2.5 `UndirectedGraph<T,K>` (grafo no dirigido)
Una arista lógica no dirigida se representa guardando dos pares dirigidos:
- `(v1 -> v2)`
- `(v2 -> v1)`

Eso simplifica `AdjacentVertices`, pero implica que varias métricas se interpretan sobre la representación almacenada, no sobre una arista lógica única.

### 2.6 `IPairValue<T>` + `DirectedPairValue<T>` / `UndirectedPairValue<T>`
Representan la clave estructural de aristas.

- `DirectedPairValue<T>` usa igualdad sensible al orden
- `UndirectedPairValue<T>` usa igualdad insensible al orden

> Importante: `UndirectedGraph<T,K>` hoy usa dos `DirectedPairValue<T>`, no `UndirectedPairValue<T>`, para representar cada conexión lógica no dirigida.

---

## 3) Flujo interno: vértices

### 3.1 `AddVertex(T vertex)`
1. valida `vertex != null`
2. si `VertexSet.Contains(vertex)`, no agrega
3. agrega a `VertexSet`
4. si `VertexIndices` no contiene `vertex.GetName()`, registra el índice

**Implicación**
- la librería mezcla dos nociones de identidad:
  - identidad / duplicidad por `Contains`
  - lookup por nombre usando `GetName()`

### 3.2 `GetVertex(string id)`
- busca `id` en `VertexIndices`
- retorna el elemento de `VertexSet` en ese índice

**Implicación**
- aquí `id` es en la práctica el nombre del vértice, no un id numérico independiente

### 3.3 `DeleteVertex(T vertex)`
- quita el vértice de `VertexSet`
- **no elimina aristas asociadas**
- **no reconstruye `VertexIndices`**

**Efecto**
- pueden quedar aristas colgantes
- los índices pueden quedar desfasados si se elimina desde el medio de la lista

---

## 4) Flujo interno: aristas y pesos

### 4.1 `AddEdge` (dirigido)
Condiciones:
- `v1`, `v2`, `weight` no pueden ser `null`
- ambos vértices deben existir en `VertexSet`

Proceso:
1. construye `pair = new DirectedPairValue<T>(v1, v2)`
2. si `EdgeSet.Contains(pair)`, no agrega
3. agrega `pair`
4. registra `Weights[pair] = weight`

### 4.2 `AddEdge` (no dirigido)
Condiciones equivalentes.

Proceso:
1. crea `pair1 = (v1->v2)` y `pair2 = (v2->v1)`
2. si alguno ya existe, no agrega
3. agrega ambos pares a `EdgeSet`
4. registra el mismo peso para ambos

**Consecuencia**
- hay dos entradas en `EdgeSet` y dos claves en `Weights` por una sola arista lógica

### 4.3 `GetWeight(v1, v2)`
- construye `new DirectedPairValue<T>(v1, v2)`
- busca ese par en `Weights`

### 4.4 `AreAdjacent(v1, v2)`
- dirigido: verifica existencia de `(v1->v2)`
- no dirigido: exige existencia de ambas direcciones almacenadas

### 4.5 `DeleteEdge(v1, v2)`
- dirigido: elimina una sola dirección y su peso
- no dirigido: elimina ambas direcciones y ambos pesos

---

## 5) Consultas y recorridos

### 5.1 Vecinos: `AdjacentVertices(vertex)`
Implementación común:
- recorre `EdgeSet`
- si `pair.GetFirst().Equals(vertex)`, retorna `pair.GetSecond()`

Interpretación:
- en ambos grafos, los vecinos expuestos son vecinos salientes respecto de la representación almacenada

### 5.2 Grados
- dirigido:
  - `OutDegree` cuenta `first == vertex`
  - `InDegree` cuenta `second == vertex`
  - `Degree = In + Out`
- no dirigido:
  - se implementa del mismo modo, pero con representación duplicada

---

## 6) Decisiones de diseño e implicaciones

### 6.1 `weight == null`
`AddEdge` valida `weight == null`.

**Implicación práctica**
- esto encaja mejor con reference types o nullable value types (`float?`, `int?`)
- es menos claro si se pretende usar structs no-nullable directamente

### 6.2 Duplicación de aristas en `UndirectedGraph`
Al almacenar ambas direcciones:
- `EdgesNumber()` cuenta el doble respecto a aristas lógicas
- los grados deben interpretarse sobre la representación almacenada

### 6.3 Identidad por nombre
`VertexIndices` usa `GetName()` como clave, pero la prevención de duplicados depende de `Contains`.

**Riesgo**
- dos vértices distintos con el mismo nombre pueden producir inconsistencias de lookup / indexado

### 6.4 Mutación incompleta
`DeleteVertex` es hoy el principal hueco funcional de la librería si se quiere usar con mutación real.

### 6.5 Complejidad
La estructura actual favorece simplicidad y legibilidad:
- `VertexSet.Contains(...)` es O(V)
- `EdgeSet.Contains(...)` es O(E)
- `AdjacentVertices` es O(E)
- grados también son O(E)

Es razonable para grafos pequeños o medianos en tooling / generación.

---

## 7) Ejemplo de uso de referencia

```csharp
using GraphLibrary;

public class BiomeNode : AbstractNode
{
    public int Id;
    public string Name;

    public override string GetName() => Name;
    public override int GetID() => Id;
}

var a = new BiomeNode { Id = 1, Name = "Beach" };
var b = new BiomeNode { Id = 2, Name = "Jungle" };

var g = new UndirectedGraph<BiomeNode, float?>();
g.AddVertex(new[] { a, b });
g.AddEdge(a, b, 0.7f);

foreach (var n in g.AdjacentVertices(a))
    UnityEngine.Debug.Log(n.GetName());

var w = g.GetWeight(a, b);
```

> El archivo `DirectedGraphExample.cs` actual no debe tratarse como ejemplo canónico: está desactualizado y marcado `TODO: REDO`.

---

## 8) Nota histórica de integración / rescate

Esta sección se conserva por trazabilidad, no como autoridad de diseño actual.

### 8.1 ¿Basta copiar / pegar la carpeta GraphLibrary?
Para rescatar la librería en un package nuevo, normalmente sí basta con copiar la carpeta bajo `Runtime/`, siempre que:

1. quede dentro del assembly runtime correcto
2. no haya colisiones de namespaces
3. se valide el comportamiento de pesos / borrado de vértices antes de usarla en producción

### 8.2 Recomendación mínima antes de uso más serio
- endurecer `DeleteVertex`
- decidir una política estable de identidad de vértices
- clarificar el uso previsto de `K` si se usarán tipos valor no-nullable
- reemplazar el ejemplo stale por uno vigente
- revalidar si GraphLibrary merece promoción futura o si debe seguir como utility support surface
