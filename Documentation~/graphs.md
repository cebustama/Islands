# GraphLibrary – Directed & Undirected Weighted Graphs

> **Version** 0.1.0 · generated 2026-01-21  
> **Recommended package location:** `Runtime/Graphs/` (or `Runtime/.../Data Structures/Graphs/` if you keep the legacy layout)

GraphLibrary is a small, generic **graph data-structure layer** that Islands can reuse in higher-level systems (roads, river networks, island/biome adjacency graphs, quest/relationship graphs, etc.).

It is **not** tied to Jobs/Burst; it is plain C# collections and is safe to use in Editor tooling as well as runtime logic.

---

## 1. Concepts & type roles

### 1.1 `IVertex`
Minimum vertex contract:

- `string GetName()`

Used to build the `VertexIndices` lookup (`name -> index`) inside the graph and to support `GetVertex(string id)`.

### 1.2 `AbstractNode` and `GenericNode`
`AbstractNode` is a convenience base type implementing `IVertex`:

- `GetName()` (abstract)
- `GetID()` (abstract)

`GenericNode` provides:
- `public string Name`
- `GetName() => Name`
- `GetID() => Name.GetHashCode()`

> If you need persistent IDs, store your own int/Guid; don’t rely on `GetHashCode()` stability.

### 1.3 `IPairValue<T>` (edge key)
Edges are represented by `IPairValue<T>`:

- `T GetFirst()`
- `T GetSecond()`
- `bool Contains(T value)`

Implementations:
- `DirectedPairValue<T>`: `(A,B) != (B,A)`
- `UndirectedPairValue<T>`: `(A,B) == (B,A)` (order-insensitive equality)

Edges live in:
- `EdgeSet : List<IPairValue<T>>`
- `Weights : Dictionary<IPairValue<T>, K>`

---

## 2. `IGraph<T,K>` API

Generic parameters:
- `T` = vertex type (`class, IVertex`)
- `K` = weight/label type

Key operations:
- Vertices: `AddVertex`, `DeleteVertex`, `GetVertexSet`, counts.
- Edges: `AddEdge`, `DeleteEdge`, `AreAdjacent`, `GetWeight`.
- Degree: `Degree`, `InDegree`, `OutDegree`.
- Traversal: `AdjacentVertices(vertex)`.

---

## 3. `AbstractGraph<T,K>` – shared plumbing

Stored state:
- `VertexSet : List<T>`
- `VertexIndices : Dictionary<string,int>` keyed by `vertex.GetName()`
- `EdgeSet : List<IPairValue<T>>`
- `Weights : Dictionary<IPairValue<T>, K>`

Vertex behavior:
- `AddVertex` avoids duplicates and registers an index in `VertexIndices`.
- `GetVertex(id)` uses `VertexIndices` to fetch from `VertexSet`.
- `DeleteVertex` removes from `VertexSet` but **does not yet remove incident edges** (TODO).

Helpers:
- `GetVertexOtherThan(v1, rng)` picks a random vertex different from `v1`.
- `GetDFSTree()` is a placeholder (not implemented).

---

## 4. Edge semantics

### 4.1 `DirectedGraph<T,K>`
Stores one edge for `(A -> B)`.

- `AddEdge`: requires both vertices exist; creates `DirectedPairValue(A,B)`; stores weight.
- `AreAdjacent(A,B)`: checks for `(A -> B)` existence.
- `AdjacentVertices(A)`: yields all `B` where `(A -> B)` exists.
- Degrees:
  - `OutDegree(A)` counts edges with `First == A`
  - `InDegree(A)` counts edges with `Second == A`
  - `Degree = In + Out`

### 4.2 `UndirectedGraph<T,K>`
Implemented by storing **both directions**:
- Adding `(A,B)` adds `(A->B)` and `(B->A)` with the same weight.

Consequences:
- `EdgesNumber()` counts *two* per undirected connection.
- Degree stays symmetric.

---

## 5. Example usage

```csharp
public class BiomeNode : GraphLibrary.AbstractNode
{
    public int Id;
    public string Name;

    public override string GetName() => Name;
    public override int GetID() => Id;
}

var a = new BiomeNode { Id = 1, Name = "Beach" };
var b = new BiomeNode { Id = 2, Name = "Jungle" };

var g = new GraphLibrary.UndirectedGraph<BiomeNode, float>();
g.AddVertex(new [] { a, b });
g.AddEdge(a, b, 0.7f); // "transition cost"

bool adjacent = g.AreAdjacent(a, b);
float w = g.GetWeight(a, b);

foreach (var n in g.AdjacentVertices(a))
{
    // ...
}
```

---

## 6. Complexity notes

With `List<>`-backed sets:
- `VertexSet.Contains` is **O(V)**
- `EdgeSet.Contains` is **O(E)**
- Degree computations are **O(E)** per call

Fine for small/medium graphs used in generation/editor tooling.  
If you need very large graphs, consider adjacency dictionaries/sets.

---

## 7. Next improvements worth doing

1. `DeleteVertex` should delete incident edges and weights.
2. `VertexIndices` can become stale if you remove vertices from the middle.
3. Add stable vertex IDs / indices (noted as TODO in the interface).
4. Consider adjacency maps for faster degree/neighborhood queries.

---

## 8. Where it fits in Islands

GraphLibrary is a utility layer you’ll likely use *above* the current mesh/noise/surface/shader stack:

- Region adjacency graphs (biomes/islands)
- Road/river networks
- POI connectivity graphs
- Quest and relationship graphs
