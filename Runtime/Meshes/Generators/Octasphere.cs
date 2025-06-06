// https://catlikecoding.com/unity/tutorials/procedural-meshes/octasphere/
using Unity.Mathematics;
using UnityEngine;

using static Unity.Mathematics.math;

namespace Islands.Meshes.Generators
{
    public struct Octasphere : IMeshGenerator
    {
        // Four rhombus sides (4xr^2), seam (2r - 1) and polar vertices (2 per rhombus = 8)
        public int VertexCount => 4 * Resolution * Resolution + 2 * Resolution - 1 + 8;

        public int IndexCount => 6 * 4 * Resolution * Resolution;

        // One special case for the seam
        public int JobLength => 4 * Resolution + 1;

        public Bounds Bounds => new Bounds(Vector3.zero, new Vector3(2f, 2f, 2f));
        public int Resolution { get; set; }

        private struct Rhombus
        {
            public int id;
            public float3 leftCorner, rightCorner;
        }

        // Polar unwrap of the cube sphere (analogue to tetrahedron polar unwrap)
        private static Rhombus GetRhombus(int id) => id switch
        {
            // Back side
            0 => new Rhombus
            {
                id = id,
                leftCorner = back(),
                rightCorner = right()
            },
            // Right side
            1 => new Rhombus
            {
                id = id,
                leftCorner = right(),
                rightCorner = forward()
            },
            // Bottom side
            2 => new Rhombus
            {
                id = id,
                leftCorner = forward(),
                rightCorner = left()
            },
            // Top side
            _ => new Rhombus
            {
                id = id,
                leftCorner = left(),
                rightCorner = back()
            }
        };

        public void Execute<S>(int i, S streams) where S : struct, IMeshStreams
        {
            if (i == 0)
            {
                ExecutePolesAndSeam(streams);
            }
            else
            {
                ExecuteRegular(i - 1, streams);
            }
        }

        public void ExecuteRegular<S>(int i, S streams) where S : struct, IMeshStreams
        {
            int u = i / 4;
            Rhombus rhombus = GetRhombus(i - 4 * u);
            int vi = Resolution * (Resolution * rhombus.id + u + 2) + 7;
            int ti = 2 * Resolution * (Resolution * rhombus.id + u);
            bool firstColumn = (u == 0);

            int4 quad = int4(
                vi,
                firstColumn ? rhombus.id : vi - Resolution,
                firstColumn ?
                    rhombus.id == 0 ? 8 : vi - Resolution * (Resolution + u) :
                    vi - Resolution + 1,
                vi + 1
            );

            u += 1;

            // South pole to left corner
            float3 columnBottomDir = rhombus.rightCorner - down();
            float3 columnBottomStart = down() + columnBottomDir * u / Resolution;
            float3 columnBottomEnd = rhombus.leftCorner + columnBottomDir * u / Resolution;

            // Left corner to north pole
            float3 columnTopDir = up() - rhombus.leftCorner;
            float3 columnTopStart =
                rhombus.rightCorner + columnTopDir * ((float)u / Resolution - 1f);
            float3 columnTopEnd = rhombus.leftCorner + columnTopDir * u / Resolution;

            var vertex = new Vertex();
            // Bottom vertex of each column
            vertex.normal = vertex.position = normalize(columnBottomStart);
            vertex.tangent.xz = GetTangentXZ(vertex.position);
            vertex.tangent.w = -1f;
            vertex.texCoord0 = GetTexCoord(vertex.position);
            streams.SetVertex(vi, vertex);
            vi += 1;

            for (int v = 1; v < Resolution; v++, vi++, ti += 2)
            {
                if (v <= Resolution - u)
                {
                    vertex.position =
                        lerp(columnBottomStart, columnBottomEnd, (float)v / Resolution);
                }
                else
                {
                    vertex.position =
                        lerp(columnTopStart, columnTopEnd, (float)v / Resolution);
                }
                vertex.normal = vertex.position = normalize(vertex.position);
                vertex.tangent.xz = GetTangentXZ(vertex.position);
                vertex.texCoord0 = GetTexCoord(vertex.position);
                streams.SetVertex(vi, vertex);
                streams.SetTriangle(ti + 0, quad.xyz);
                streams.SetTriangle(ti + 1, quad.xzw);

                quad.y = quad.z;
                quad += int4(1, 0, firstColumn && rhombus.id != 0 ? Resolution : 1, 1);
            }

            quad.z = Resolution * Resolution * rhombus.id + Resolution + u + 6;
            quad.w = u < Resolution ? quad.z + 1 : rhombus.id + 4;

            streams.SetTriangle(ti + 0, quad.xyz);
            streams.SetTriangle(ti + 1, quad.xzw);
        }

        public void ExecutePolesAndSeam<S>(S streams) where S : struct, IMeshStreams 
        {
            var vertex = new Vertex();
            vertex.tangent = float4(sqrt(0.5f), 0f, sqrt(0.5f), -1f);
            vertex.texCoord0.x = 0.125f;

            for (int i = 0; i < 4; i++)
            {
                // South pole vertices
                vertex.position = vertex.normal = down();
                vertex.texCoord0.y = 0f;
                streams.SetVertex(i, vertex);
                // North pole vertices
                vertex.position = vertex.normal = up();
                vertex.texCoord0.y = 1f;
                streams.SetVertex(i + 4, vertex);
                vertex.tangent.xz = float2(-vertex.tangent.z, vertex.tangent.x);
                vertex.texCoord0.x += 0.25f;
            }

            vertex.tangent.xz = float2(1f, 0f);
            vertex.texCoord0.x = 0f;

            for (int v = 1; v < 2 * Resolution; v++)
            {
                if (v < Resolution)
                {
                    vertex.position = lerp(down(), back(), (float)v / Resolution);
                }
                else
                {
                    vertex.position =
                        lerp(back(), up(), (float)(v - Resolution) / Resolution);
                }
                vertex.normal = vertex.position = normalize(vertex.position);
                vertex.texCoord0.y = GetTexCoord(vertex.position).y;
                streams.SetVertex(v + 7, vertex);
            }
        }

        private static float2 GetTangentXZ(float3 p) => normalize(float2(-p.z, p.x));

        private static float2 GetTexCoord(float3 p)
        {
            var texCoord = float2(
                atan2(p.x, p.z) / (-2f * PI) + 0.5f,
                asin(p.y) / PI + 0.5f
            );
            if (texCoord.x < 1e-6f)
            {
                texCoord.x = 1f;
            }
            return texCoord;
        }
    }
}
