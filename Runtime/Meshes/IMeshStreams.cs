using Unity.Mathematics;
using UnityEngine;

namespace Islands.Meshes
{
    public interface IMeshStreams 
    {
        void Setup(Mesh.MeshData data, Bounds bounds, int vertexCount, int indexCount);
        void SetVertex(int index, Vertex data);
        void SetTriangle(int index, int3 triangle);
    }
}
