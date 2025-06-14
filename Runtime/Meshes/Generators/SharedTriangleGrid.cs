﻿using Unity.Mathematics;
using UnityEngine;

using static Unity.Mathematics.math;

namespace Islands.Meshes.Generators
{
    public struct SharedTriangleGrid : IMeshGenerator
    {
        // Each row of vertices for a resolution R grid has R + 1 vertices
        public int VertexCount => (Resolution + 1) * (Resolution + 1);

        public int IndexCount => 6 * Resolution * Resolution;

        public int JobLength => Resolution + 1;

        public Bounds Bounds => new Bounds(
            Vector3.zero, new Vector3(1f + 0.5f / Resolution, 0f, sqrt(3f) / 2f)
        );
        public int Resolution { get; set; }

        public void Execute<S>(int z, S streams) where S : struct, IMeshStreams
        {
            int vi = (Resolution + 1) * z, ti = 2 * Resolution * (z - 1);

            float xOffset = -0.25f;
            float uOffset = 0f;

            int iA = -Resolution - 2, iB = -Resolution - 1, iC = -1, iD = 0;
            // different layout for triangles below even vertex rows
            var tA = int3(iA, iC, iD);
            var tB = int3(iA, iD, iB);

            // shift even vertex rows by −0.25 and odd vertex rows by 0.25
            if ((z & 1) == 1)
            {
                xOffset = 0.25f;
                uOffset = 0.5f / (Resolution + 0.5f);
                tA = int3(iA, iC, iB);
                tB = int3(iB, iC, iD);
            }

            // keep the grid centered on the origin
            xOffset = xOffset / Resolution - 0.5f;

            var vertex = new Vertex();
            vertex.normal.y = 1f;
            vertex.tangent.xw = float2(1f, -1f);

            vertex.position.x = xOffset;
            // make the triangles equilateral, multiply by height of an equilateral triangle
            vertex.position.z = ((float)z / Resolution - 0.5f) * sqrt(3f) / 2f;
            vertex.texCoord0.x = uOffset;
            // scale down texture vertically
            vertex.texCoord0.y = vertex.position.z / (1f + 0.5f / Resolution) + 0.5f;
            streams.SetVertex(vi, vertex);
            vi += 1;

            for (int x = 1; x <= Resolution; x++, vi++, ti += 2)
            {
                vertex.position.x = (float)x / Resolution + xOffset;
                vertex.texCoord0.x = x / (Resolution + 0.5f) + uOffset;
                streams.SetVertex(vi, vertex);

                if (z > 0)
                {
                    streams.SetTriangle(ti + 0, vi + tA);
                    streams.SetTriangle(ti + 1, vi + tB);
                }
            }
        }
    }
}
