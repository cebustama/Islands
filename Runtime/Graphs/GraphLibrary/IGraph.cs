// namespace Islands.Patterns // or .DataStructures

// https://www.youtube.com/watch?v=KmVSCv6Bn8E
namespace GraphLibrary
{
    using System.Collections.Generic;

    public interface IVertex
    {
        string GetName();
    }

    // Nodes of type T, edges have associated weight of type K
    public interface IGraph<T, K> 
        where T : class, IVertex
    {
        bool AddVertex(T vertex);

        void AddVertex(IEnumerable<T> vertexSet);

        bool DeleteVertex(T vertex);

        void DeleteVertex(IEnumerable<T> vertexSet);

        bool AddEdge(T v1, T v2, K weight);

        K GetWeight(T v1, T v2);

        bool DeleteEdge(T v1, T v2);

        bool AreAdjacent(T v1, T v2);

        int Degree(T vertex);

        int OutDegree(T vertex);

        int InDegree(T vertex);

        int VerticesNumber();

        int EdgesNumber();

        IEnumerable<T> AdjacentVertices(T vertex);

        IEnumerable<T> GetVertexSet();

        IEnumerable<IPairValue<T>> GetEdgeSet();

        // TODO: Implement indices
    }

    // A pair of the same type of values
    public interface IPairValue<T>
    {
        T GetFirst();

        T GetSecond();

        bool Contains(T value);
    }
}