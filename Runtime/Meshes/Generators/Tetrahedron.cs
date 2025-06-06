// Tetrahedron mesh generator following CatLikeCoding procedural mesh pattern
using Unity.Mathematics;
using UnityEngine;

using static Unity.Mathematics.math;

namespace Islands.Meshes.Generators
{
    public struct Tetrahedron : IMeshGenerator
    {
        // 4 triangular faces, each subdivided into Resolution^2 triangles
        // Each triangle has (Resolution + 1) * (Resolution + 2) / 2 vertices
        public int VertexCount => 4 * (Resolution + 1) * (Resolution + 2) / 2;

        // Each face has Resolution^2 triangles, each triangle has 3 indices
        public int IndexCount => 4 * Resolution * Resolution * 3;

        // One job per face
        public int JobLength => 4;

        public Bounds Bounds => new Bounds(Vector3.zero, new Vector3(2f, 2f, 2f));
        public int Resolution { get; set; }

        private struct Face
        {
            public int id;
            public float3 corner0, corner1, corner2;
            public float3 normal;
            public float4 tangent;
        }

        // Regular tetrahedron vertices positioned for optimal symmetry
        // Using static methods instead of static readonly array for Burst compatibility
        private static float3 GetTetrahedronVertex(int index) => index switch
        {
            0 => float3(sqrt(8f / 9f), -1f / 3f, 0f),           // Front
            1 => float3(-sqrt(2f / 9f), -1f / 3f, sqrt(2f / 3f)), // Back-left  
            2 => float3(-sqrt(2f / 9f), -1f / 3f, -sqrt(2f / 3f)), // Back-right
            _ => float3(0f, 1f, 0f)                          // Top
        };

        private static Face GetFace(int id)
        {
            float3 v0 = GetTetrahedronVertex(0);
            float3 v1 = GetTetrahedronVertex(1);
            float3 v2 = GetTetrahedronVertex(2);
            float3 v3 = GetTetrahedronVertex(3);

            return id switch
            {
                // Bottom face (looking up from below)
                0 => new Face
                {
                    id = id,
                    corner0 = v0, // Front
                    corner1 = v2, // Back-right
                    corner2 = v1, // Back-left
                    normal = normalize(cross(v2 - v0, v1 - v0)),
                    tangent = float4(normalize(v2 - v0), -1f)
                },
                // Front face
                1 => new Face
                {
                    id = id,
                    corner0 = v0, // Front-bottom
                    corner1 = v1, // Back-left-bottom
                    corner2 = v3, // Top
                    normal = normalize(cross(v1 - v0, v3 - v0)),
                    tangent = float4(normalize(v1 - v0), -1f)
                },
                // Right face
                2 => new Face
                {
                    id = id,
                    corner0 = v1, // Back-left-bottom
                    corner1 = v2, // Back-right-bottom
                    corner2 = v3, // Top
                    normal = normalize(cross(v2 - v1, v3 - v1)),
                    tangent = float4(normalize(v2 - v1), -1f)
                },
                // Left face
                _ => new Face
                {
                    id = id,
                    corner0 = v2, // Back-right-bottom
                    corner1 = v0, // Front-bottom
                    corner2 = v3, // Top
                    normal = normalize(cross(v0 - v2, v3 - v2)),
                    tangent = float4(normalize(v0 - v2), -1f)
                }
            };
        }

        // Get UV coordinates for proper texture unwrapping
        private static float2 GetUVCoordinates(int faceId, float u, float v)
        {
            // Create a 2x2 UV layout for the 4 triangular faces
            // Similar to how octahedron unwraps its faces
            return faceId switch
            {
                0 => float2(u * 0.5f, v * 0.5f),                    // Bottom-left quadrant
                1 => float2(0.5f + u * 0.5f, v * 0.5f),            // Bottom-right quadrant  
                2 => float2(u * 0.5f, 0.5f + v * 0.5f),            // Top-left quadrant
                _ => float2(0.5f + u * 0.5f, 0.5f + v * 0.5f)      // Top-right quadrant
            };
        }

        public void Execute<S>(int i, S streams) where S : struct, IMeshStreams
        {
            Face face = GetFace(i);

            int verticesPerFace = (Resolution + 1) * (Resolution + 2) / 2;
            int vi = face.id * verticesPerFace;
            int ti = face.id * Resolution * Resolution;

            var vertex = new Vertex();
            vertex.normal = face.normal;
            vertex.tangent = face.tangent;

            // Generate vertices for triangular face using barycentric subdivision
            int vertexIndex = vi;
            for (int row = 0; row <= Resolution; row++)
            {
                for (int col = 0; col <= Resolution - row; col++)
                {
                    // Barycentric coordinates
                    float u = (float)col / Resolution;
                    float v = (float)row / Resolution;
                    float w = 1f - u - v;

                    // Interpolate position using barycentric coordinates
                    vertex.position = w * face.corner0 + u * face.corner1 + v * face.corner2;

                    // UV coordinates for proper texture unwrapping
                    vertex.texCoord0 = GetUVCoordinates(face.id, u, v);

                    streams.SetVertex(vertexIndex++, vertex);
                }
            }

            // Generate triangles
            int triangleIndex = ti;
            for (int row = 0; row < Resolution; row++)
            {
                int rowStart = vi + row * (Resolution + 1) - (row * (row - 1)) / 2;
                int nextRowStart = vi + (row + 1) * (Resolution + 1) - (row * (row + 1)) / 2;

                for (int col = 0; col < Resolution - row; col++)
                {
                    int current = rowStart + col;
                    int nextRowCurrent = nextRowStart + col;

                    // Upward pointing triangle
                    streams.SetTriangle(triangleIndex++, int3(
                        current,
                        current + 1,
                        nextRowCurrent
                    ));

                    // Downward pointing triangle (if not at the edge)
                    if (col < Resolution - row - 1)
                    {
                        streams.SetTriangle(triangleIndex++, int3(
                            current + 1,
                            nextRowCurrent + 1,
                            nextRowCurrent
                        ));
                    }
                }
            }
        }
    }
}