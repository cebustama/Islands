using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

using static Unity.Mathematics.math;
using Islands;

public class HashVisualization : Visualization
{
    private int hashesId = Shader.PropertyToID("_Hashes");

    [SerializeField] private int seed;

    [SerializeField]
    private SpaceTRS domain = new SpaceTRS
    {
        scale = 8f
    };

    private NativeArray<uint4> hashes;

    private ComputeBuffer hashesBuffer;

    [BurstCompile(FloatPrecision.Standard, FloatMode.Fast, CompileSynchronously = true)]
    struct HashJob : IJobFor
    {
        [ReadOnly]
        public NativeArray<float3x4> positions;

        [WriteOnly]
        public NativeArray<uint4> hashes;

        public SmallXXHash4 hash;
        public float3x4 domainTRS;

        public void Execute(int i)
        {
            // Separate vectors for x, y and z components required, so we transpose
            // Four vectors of three components each (x,y,z)
            float4x3 p = domainTRS.TransformVectors(transpose(positions[i]));

            int4 u = (int4)floor(p.c0); // X
            int4 v = (int4)floor(p.c1); // Y
            int4 w = (int4)floor(p.c2); // Z

            hashes[i] = hash.Eat(u).Eat(v).Eat(w);
        }
    }

    protected override void EnableVisualization(
        int dataLength, MaterialPropertyBlock propertyBlock)
    {
        hashes = new NativeArray<uint4>(dataLength, Allocator.Persistent);
        hashesBuffer = new ComputeBuffer(dataLength * 4, 4);

        propertyBlock.SetBuffer(hashesId, hashesBuffer);
    }

    protected override void DisableVisualization()
    {
        hashes.Dispose();
        hashesBuffer.Release();
        hashesBuffer = null;
    }

    protected override void UpdateVisualization(
        NativeArray<float3x4> positions, int resolution, JobHandle handle)
    {
        new HashJob
        {
            positions = positions,
            hashes = hashes,
            hash = SmallXXHash.Seed(seed),
            domainTRS = domain.Matrix
        }.ScheduleParallel(hashes.Length, resolution, handle).Complete();

        hashesBuffer.SetData(hashes.Reinterpret<uint>(4 * 4));
    }
}
