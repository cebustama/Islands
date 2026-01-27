using System;
using System.Collections.Generic;
using System.Linq;

namespace GraphLibrary
{
    // TODO: GenericGraph
    // TODO: Add option to be labeled (weighted) or unlabeled
    public abstract class AbstractGraph<T, K> : IGraph<T, K>
        where T : class, IVertex
    {
        protected readonly List<T> VertexSet = new List<T>();
        // TODO: Use name hash as id
        protected readonly Dictionary<string, int> VertexIndices = new Dictionary<string, int>();

        // TODO: Create edge struct that holds weight
        protected readonly List<IPairValue<T>> EdgeSet = new List<IPairValue<T>>();
        protected readonly Dictionary<IPairValue<T>, K> Weights = new Dictionary<IPairValue<T>, K>();

        protected AbstractGraph(T[] vertices = null, IPairValue<T>[] edges = null)
        {
            if (vertices != null) VertexSet = vertices.ToList();
            if (edges != null) EdgeSet = edges.ToList();
        }

        public bool AddVertex(T vertex)
        {
            if (vertex == null)
                throw new ArgumentNullException();
            if (VertexSet.Contains(vertex))
                return false;
            VertexSet.Add(vertex);

            if (!VertexIndices.ContainsKey(vertex.GetName()))
                VertexIndices.Add(vertex.GetName(), VertexSet.Count - 1);

            return true;
        }

        public void AddVertex(IEnumerable<T> vertexSet)
        {
            if (vertexSet == null)
                throw new ArgumentNullException();

            // Iterate new set and only add new ones
            using (var it = vertexSet.GetEnumerator())
            {
                while (it.MoveNext())
                {
                    if (it.Current != null && !VertexSet.Contains(it.Current))
                        AddVertex(it.Current);
                }
            }
        }

        public T GetVertex(string ID)
        {
            if (!VertexIndices.ContainsKey(ID))
                throw new ArgumentException();

            return VertexSet[VertexIndices[ID]];
        }

        public bool DeleteVertex(T vertex)
        {
            if (vertex == null)
                throw new ArgumentNullException();
            if (!VertexSet.Contains(vertex))
                return false;
            VertexSet.Remove(vertex);

            // TODO: Delete all edges containing vertex

            return true;
        }

        public void DeleteVertex(IEnumerable<T> vertexSet)
        {
            if (vertexSet == null)
                throw new ArgumentNullException();
            using (var it = vertexSet.GetEnumerator())
            {
                while (it.MoveNext())
                {
                    if (it.Current != null && VertexSet.Contains(it.Current))
                        VertexSet.Remove(it.Current);
                }
            }
        }

        public abstract bool AddEdge(T v1, T v2, K weight);

        public abstract K GetWeight(T v1, T v2);

        public abstract bool DeleteEdge(T v1, T v2);

        public abstract bool AreAdjacent(T v1, T v2);

        public abstract int Degree(T vertex);

        public abstract int InDegree(T vertex);

        public abstract int OutDegree(T vertex);

        public int VerticesNumber()
        {
            return VertexSet.Count;
        }

        public int EdgesNumber()
        {
            return EdgeSet.Count;
        }

        public abstract IEnumerable<T> AdjacentVertices(T vertex);

        public IEnumerable<T> GetVertexSet()
        {
            return VertexSet;
        }

        public IEnumerable<IPairValue<T>> GetEdgeSet()
        {
            return EdgeSet;
        }
        
        public List<T> GetVertexList()
        {
            return VertexSet;
        }

        public T GetVertexOtherThan(T v1, Random rng = null)
        {
            if (VertexSet.Count < 2)
                throw new ArgumentOutOfRangeException();

            T v2 = VertexSet[
                (rng != null) ? rng.Next(0, VerticesNumber()) :
                UnityEngine.Random.Range(0, VerticesNumber())
            ];

            while (v1.Equals(v2)) 
                v2 = VertexSet[
                    (rng != null) ? rng.Next(0, VerticesNumber()) :
                    UnityEngine.Random.Range(0, VerticesNumber())
                ];

            return v2;
        }

        // TODO: Implement distributed somehow
        public AbstractGraph<T,K> GetDFSTree()
        {
            return null;
        }
    }
}