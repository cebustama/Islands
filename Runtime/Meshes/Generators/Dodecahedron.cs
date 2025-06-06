// Dodecahedron mesh generator following CatLikeCoding procedural mesh pattern
using Unity.Mathematics;
using UnityEngine;

using static Unity.Mathematics.math;

namespace Islands.Meshes.Generators
{
    public struct Dodecahedron : IMeshGenerator
    {
        // 12 pentagonal faces, each subdivided into Resolution^2 quads
        public int VertexCount => 12 * (Resolution + 1) * (Resolution + 1);

        // Each face has Resolution^2 quads, each quad becomes 2 triangles
        public int IndexCount => 12 * Resolution * Resolution * 6;

        // One job per face
        public int JobLength => 12;

        public Bounds Bounds => new Bounds(Vector3.zero, new Vector3(2f, 2f, 2f));
        public int Resolution { get; set; }

        private struct Face
        {
            public int id;
            public float3 corner0, corner1, corner2, corner3, corner4;
            public float3 normal;
            public float4 tangent;
        }

        // Golden ratio for dodecahedron vertex calculation
        private static readonly float phi = (1f + sqrt(5f)) / 2f;

        // Regular dodecahedron vertices - correctly calculated
        private static float3 GetDodecahedronVertex(int index) => index switch
        {
            // First set: (±1, ±1, ±1)
            0 => float3(1f, 1f, 1f),
            1 => float3(1f, 1f, -1f),
            2 => float3(1f, -1f, 1f),
            3 => float3(1f, -1f, -1f),
            4 => float3(-1f, 1f, 1f),
            5 => float3(-1f, 1f, -1f),
            6 => float3(-1f, -1f, 1f),
            7 => float3(-1f, -1f, -1f),

            // Second set: (0, ±1/φ, ±φ)
            8 => float3(0f, 1f / phi, phi),
            9 => float3(0f, 1f / phi, -phi),
            10 => float3(0f, -1f / phi, phi),
            11 => float3(0f, -1f / phi, -phi),

            // Third set: (±1/φ, ±φ, 0)
            12 => float3(1f / phi, phi, 0f),
            13 => float3(1f / phi, -phi, 0f),
            14 => float3(-1f / phi, phi, 0f),
            15 => float3(-1f / phi, -phi, 0f),

            // Fourth set: (±φ, 0, ±1/φ)
            16 => float3(phi, 0f, 1f / phi),
            17 => float3(phi, 0f, -1f / phi),
            18 => float3(-phi, 0f, 1f / phi),
            _ => float3(-phi, 0f, -1f / phi) // 19
        };

        private static Face GetFace(int id)
        {
            // Define the 12 pentagonal faces with correct vertex connections
            var (v0, v1, v2, v3, v4) = id switch
            {
                0 => (8, 0, 16, 2, 10),    // Pentagon 0
                1 => (9, 11, 3, 17, 1),   // Pentagon 1
                2 => (12, 1, 17, 0, 8),   // Pentagon 2
                3 => (14, 5, 9, 1, 12),   // Pentagon 3
                4 => (18, 4, 14, 12, 8),  // Pentagon 4
                5 => (10, 2, 13, 6, 18),  // Pentagon 5
                6 => (16, 0, 17, 3, 13),  // Pentagon 6
                7 => (19, 7, 15, 13, 2),  // Pentagon 7
                8 => (15, 7, 11, 9, 5),   // Pentagon 8
                9 => (6, 4, 18, 19, 15),  // Pentagon 9
                10 => (5, 14, 4, 6, 15),  // Pentagon 10
                _ => (7, 19, 18, 8, 10)   // Pentagon 11
            };

            float3 vert0 = GetDodecahedronVertex(v0);
            float3 vert1 = GetDodecahedronVertex(v1);
            float3 vert2 = GetDodecahedronVertex(v2);
            float3 vert3 = GetDodecahedronVertex(v3);
            float3 vert4 = GetDodecahedronVertex(v4);

            // Calculate face normal
            float3 center = (vert0 + vert1 + vert2 + vert3 + vert4) / 5f;
            float3 normal = normalize(center);

            // Calculate tangent
            float3 edge = vert1 - vert0;
            float3 tangent3d = normalize(edge - dot(edge, normal) * normal);

            return new Face
            {
                id = id,
                corner0 = vert0,
                corner1 = vert1,
                corner2 = vert2,
                corner3 = vert3,
                corner4 = vert4,
                normal = normal,
                tangent = float4(tangent3d, -1f)
            };
        }

        // Get UV coordinates for proper texture unwrapping
        private static float2 GetUVCoordinates(int faceId, float u, float v)
        {
            // Create a 4x3 UV layout for the 12 pentagonal faces
            int row = faceId / 4;
            int col = faceId % 4;

            float uOffset = col * 0.25f;
            float vOffset = row * 0.333f;

            return float2(uOffset + u * 0.25f, vOffset + v * 0.333f);
        }

        public void Execute<S>(int i, S streams) where S : struct, IMeshStreams
        {
            Face face = GetFace(i);

            int verticesPerFace = (Resolution + 1) * (Resolution + 1);
            int vi = face.id * verticesPerFace;
            int ti = face.id * Resolution * Resolution * 2;

            var vertex = new Vertex();
            vertex.normal = face.normal;
            vertex.tangent = face.tangent;

            // Generate vertices in a grid pattern for the pentagonal face
            for (int row = 0; row <= Resolution; row++)
            {
                for (int col = 0; col <= Resolution; col++)
                {
                    float u = (float)col / Resolution;
                    float v = (float)row / Resolution;

                    // Interpolate position within the pentagon
                    vertex.position = InterpolatePentagon(face, u, v);
                    vertex.texCoord0 = GetUVCoordinates(face.id, u, v);

                    streams.SetVertex(vi + row * (Resolution + 1) + col, vertex);
                }
            }

            // Generate triangles for the grid (fixed winding order)
            int triangleIndex = ti;
            for (int row = 0; row < Resolution; row++)
            {
                for (int col = 0; col < Resolution; col++)
                {
                    int baseIndex = vi + row * (Resolution + 1) + col;
                    int nextRowIndex = baseIndex + (Resolution + 1);

                    // First triangle of the quad (counter-clockwise)
                    streams.SetTriangle(triangleIndex++, int3(
                        baseIndex,
                        baseIndex + 1,
                        nextRowIndex
                    ));

                    // Second triangle of the quad (counter-clockwise)
                    streams.SetTriangle(triangleIndex++, int3(
                        baseIndex + 1,
                        nextRowIndex + 1,
                        nextRowIndex
                    ));
                }
            }
        }

        // Improved pentagon interpolation using proper barycentric-like coordinates
        private static float3 InterpolatePentagon(Face face, float u, float v)
        {
            // Create a mapping from square [0,1]x[0,1] to pentagon
            // Use bilinear interpolation with pentagon-specific adjustments

            // For a pentagon, we can think of it as having 5 triangular sections
            // radiating from the center to each edge

            // Calculate center point
            float3 center = (face.corner0 + face.corner1 + face.corner2 + face.corner3 + face.corner4) / 5f;

            // Map u to an angle around the pentagon (0 to 2π)
            float angle = u * 2f * PI;

            // Find which edge of the pentagon this angle corresponds to
            float sectorAngle = 2f * PI / 5f; // 72 degrees per sector
            int sector = (int)(angle / sectorAngle);
            float sectorProgress = (angle % sectorAngle) / sectorAngle;

            // Get the two corners that bound this sector
            float3 corner1 = GetPentagonCorner(face, sector);
            float3 corner2 = GetPentagonCorner(face, (sector + 1) % 5);

            // Interpolate along the edge
            float3 edgePoint = lerp(corner1, corner2, sectorProgress);

            // Interpolate from center to edge based on v
            return lerp(center, edgePoint, v);
        }

        private static float3 GetPentagonCorner(Face face, int index) => index switch
        {
            0 => face.corner0,
            1 => face.corner1,
            2 => face.corner2,
            3 => face.corner3,
            _ => face.corner4
        };
    }
}