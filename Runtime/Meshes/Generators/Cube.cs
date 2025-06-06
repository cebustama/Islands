using Unity.Mathematics;
using UnityEngine;

using static Unity.Mathematics.math;

namespace Islands.Meshes.Generators
{
    public struct Cube : IMeshGenerator
    {
        public int VertexCount => 6 * 4 * Resolution * Resolution;

        public int IndexCount => 6 * 6 * Resolution * Resolution;

        public int JobLength => 6 * Resolution;

        public Bounds Bounds => new Bounds(Vector3.zero, new Vector3(2f, 2f, 2f));
        public int Resolution { get; set; }

        private struct Side
        {
            public int id;
            public float3 uvOrigin, uVector, vVector;
            public float3 normal;
            public float4 tangent;
        }

        public void Execute<S>(int i, S streams) where S : struct, IMeshStreams
        {
            int u = i / 6;
            Side side = GetSide(i - 6 * u);

            int vi = 4 * Resolution * (Resolution * side.id + u);
            int ti = 2 * Resolution * (Resolution * side.id + u);

            float3 uA = side.uvOrigin + side.uVector * u / Resolution;
            float3 uB = side.uvOrigin + side.uVector * (u + 1) / Resolution;
            float3 pA = uA, pB = uB;

            for (int v = 1; v <= Resolution; v++, vi += 4, ti += 2)
            {
                float3 pC = uA + side.vVector * v / Resolution;
                float3 pD = uB + side.vVector * v / Resolution;

                var vertex = new Vertex();
                vertex.normal = side.normal;
                vertex.tangent = side.tangent;

                vertex.position = pA;
                streams.SetVertex(vi + 0, vertex);

                vertex.position = pB;
                vertex.texCoord0 = float2(1f, 0f);
                streams.SetVertex(vi + 1, vertex);

                vertex.position = pC;
                vertex.texCoord0 = float2(0f, 1f);
                streams.SetVertex(vi + 2, vertex);

                vertex.position = pD;
                vertex.texCoord0 = 1f;
                streams.SetVertex(vi + 3, vertex);

                streams.SetTriangle(ti + 0, vi + int3(0, 2, 1));
                streams.SetTriangle(ti + 1, vi + int3(1, 2, 3));

                pA = pC;
                pB = pD;
            }
        }

        static Side GetSide(int id) => id switch
        {
            0 => new Side
            {
                id = id,
                uvOrigin = -1f,
                uVector = 2f * right(),
                vVector = 2f * up(),
                normal = back(),
                tangent = float4(1f, 0f, 0f, -1f)
            },
            1 => new Side
            {
                id = id,
                uvOrigin = float3(1f, -1f, -1f),
                uVector = 2f * forward(),
                vVector = 2f * up(),
                normal = right(),
                tangent = float4(0f, 0f, 1f, -1f)
            },
            2 => new Side
            {
                id = id,
                uvOrigin = -1f,
                uVector = 2f * forward(),
                vVector = 2f * right(),
                normal = down(),
                tangent = float4(0f, 0f, 1f, -1f)
            },
            3 => new Side
            {
                id = id,
                uvOrigin = float3(-1f, -1f, 1f),
                uVector = 2f * up(),
                vVector = 2f * right(),
                normal = forward(),
                tangent = float4(0f, 1f, 0f, -1f)
            },
            4 => new Side
            {
                id = id,
                uvOrigin = -1f,
                uVector = 2f * up(),
                vVector = 2f * forward(),
                normal = left(),
                tangent = float4(0f, 1f, 0f, -1f)
            },
            _ => new Side
            {
                id = id,
                uvOrigin = float3(-1f, 1f, -1f),
                uVector = 2f * right(),
                vVector = 2f * forward(),
                normal = up(),
                tangent = float4(1f, 0f, 0f, -1f)
            }
        };
    }
}
