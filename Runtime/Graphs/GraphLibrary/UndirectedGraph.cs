using System;
using System.Collections.Generic;

namespace GraphLibrary
{
    // Stores two directions of each edge
    public class UndirectedGraph<T, K> : AbstractGraph<T, K> 
        where T: class, IVertex
    {
        public UndirectedGraph(T[] vertices = null, IPairValue<T>[] edges = null) 
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

            // Create and add new pairs with corresponding weight
            IPairValue<T> pair1 = new DirectedPairValue<T>(v1, v2);
            IPairValue<T> pair2 = new DirectedPairValue<T>(v2, v1);
            if (EdgeSet.Contains(pair1) || EdgeSet.Contains(pair2))
                return false;

            EdgeSet.Add(pair1);
            EdgeSet.Add(pair2);

            Weights[pair1] = weight;
            Weights[pair2] = weight;

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

            IPairValue<T> pair1 = new DirectedPairValue<T>(v1, v2);
            IPairValue<T> pair2 = new DirectedPairValue<T>(v2, v1);
            if (EdgeSet.Contains(pair1) && EdgeSet.Contains(pair2))
            {
                EdgeSet.Remove(pair1);
                EdgeSet.Remove(pair2);
                Weights.Remove(pair1);
                Weights.Remove(pair2);
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

            return EdgeSet.Contains(new DirectedPairValue<T>(v1, v2))
                && EdgeSet.Contains(new DirectedPairValue<T>(v2, v1));
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

            foreach (IPairValue<T> pair in EdgeSet)
            {
                if (pair.GetFirst().Equals(vertex))
                    yield return pair.GetSecond();
            }
        }
    }
}
