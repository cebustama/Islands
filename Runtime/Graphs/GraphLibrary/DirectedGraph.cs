using System;
using System.Collections.Generic;

namespace GraphLibrary
{
    // TODO: Implement GenericGraph with directer boolean
    public class DirectedGraph<T, K> : AbstractGraph<T, K>
        where T : class, IVertex
    {
        public DirectedGraph(T[] vertices = null, IPairValue<T>[] edges = null) 
            : base(vertices, edges)
        {
        }

        public override bool AddEdge(T v1, T v2, K weight)
        {
            if (v1 == null || v2 == null || weight == null)
                throw new ArgumentNullException();
            // Check that vertices exist in graph
            if (!VertexSet.Contains(v1) || !VertexSet.Contains(v2))
                return false;

            // Create and add new pair with corresponding weight
            IPairValue<T> pair = new DirectedPairValue<T>(v1, v2);
            if (EdgeSet.Contains(pair))
                return false;
            EdgeSet.Add(pair);
            Weights[pair] = weight;
            return true;
        }

        public override K GetWeight(T v1, T v2)
        {
            if (v1 == null || v2 == null)
                throw new ArgumentNullException();

            IPairValue<T> pair = new DirectedPairValue<T>(v1, v2);
            if (!Weights.ContainsKey(pair))
                throw new ArgumentException();

            return Weights[pair];
        }

        public override bool DeleteEdge(T v1, T v2)
        {
            if (v1 == null || v2 == null)
                throw new ArgumentNullException();

            IPairValue<T> pair = new DirectedPairValue<T>(v1, v2);
            if (EdgeSet.Contains(pair))
            {
                EdgeSet.Remove(pair);
                Weights.Remove(pair);
                return true;
            }

            return false;
        }

        public override bool AreAdjacent(T v1, T v2)
        {
            if (v1 == null || v2 == null)
                throw new ArgumentNullException();

            if (!VertexSet.Contains(v1) || !VertexSet.Contains(v2))
                throw new ArgumentException();

            return EdgeSet.Contains(new DirectedPairValue<T>(v1, v2));
        }

        public override int Degree(T vertex)
        {
            if (vertex == null)
                throw new ArgumentNullException();
            if (!VertexSet.Contains(vertex))
                throw new ArgumentException();

            return InDegree(vertex) + OutDegree(vertex);
        }

        public override int OutDegree(T vertex)
        {
            if (vertex == null)
                throw new ArgumentNullException();
            if (!VertexSet.Contains(vertex))
                throw new ArgumentException();
            int counter = 0;
            foreach (IPairValue<T> pair in EdgeSet)
                if (pair.GetFirst().Equals(vertex))
                    counter++;
            return counter;
        }

        public override int InDegree(T vertex)
        {
            if (vertex == null)
                throw new ArgumentNullException();
            if (!VertexSet.Contains(vertex))
                throw new ArgumentException();
            int counter = 0;
            foreach (IPairValue<T> pair in EdgeSet)
                if (pair.GetSecond().Equals(vertex))
                    counter++;
            return counter;
        }

        public override IEnumerable<T> AdjacentVertices(T vertex)
        {
            if (vertex == null)
                throw new ArgumentNullException();
            if (!VertexSet.Contains(vertex))
                throw new ArgumentException();

            foreach(IPairValue<T> pair in EdgeSet)
            {
                if (pair.GetFirst().Equals(vertex))
                    yield return pair.GetSecond();
            }
        }
    }
}
