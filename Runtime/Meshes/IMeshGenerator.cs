using UnityEngine;

namespace Islands.Meshes
{
    public interface IMeshGenerator
    {
        int VertexCount { get; }
        int IndexCount { get; }
        int JobLength { get; }
        Bounds Bounds { get; }
        int Resolution { get; set; }

        void Execute<S>(int i, S streams) where S : struct, IMeshStreams;
    }
}
