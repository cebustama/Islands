using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace Islands.Meshes
{
    [BurstCompile(FloatPrecision.Standard, FloatMode.Fast, CompileSynchronously = true)]
    public struct MeshJob<G, S> : IJobFor
        where G : struct, IMeshGenerator
        where S : struct, IMeshStreams
    {
        private G generator;
        [WriteOnly] private S streams;

        public void Execute(int i) => generator.Execute(i, streams);

        public static JobHandle ScheduleParallel(
            Mesh mesh, Mesh.MeshData meshData, int resolution, JobHandle dependency
        ) => 
            ScheduleParallel(mesh, meshData, resolution, dependency, Vector3.zero, false);

        public static JobHandle ScheduleParallel (
            Mesh mesh, Mesh.MeshData meshData, int resolution, JobHandle dependency,
            Vector3 extraBoundsExtents, bool supportVectorization
        )
        {
            var job = new MeshJob<G, S>();
            job.generator.Resolution = resolution;

            int vertexCount = job.generator.VertexCount;
            // remainder of an integer division by four, at most three extra unused vertices
            if (supportVectorization && (vertexCount & 0b11) != 0)
            {
                vertexCount += 4 - (vertexCount & 0b11);
            }

            Bounds bounds = job.generator.Bounds;
            bounds.extents += extraBoundsExtents;

            job.streams.Setup(
                meshData,
                mesh.bounds = bounds,
                vertexCount, 
                job.generator.IndexCount
            );

            return job.ScheduleParallel(job.generator.JobLength, 1, dependency);
        }
    }

    public delegate JobHandle MeshJobScheduleDelegate(
        Mesh mesh, Mesh.MeshData meshData, int resolution, JobHandle dependency
    );

    public delegate JobHandle AdvancedMeshJobScheduleDelegate(
        Mesh mesh, Mesh.MeshData meshData, int resolution, JobHandle dependency,
        Vector3 extraBoundsExtents, bool supportVectorization
    );
}
