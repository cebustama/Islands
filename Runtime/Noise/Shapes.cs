// https://catlikecoding.com/unity/tutorials/pseudorandom-noise/hashing-space/

using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

using static Unity.Mathematics.math;

namespace Islands
{
    public static class Shapes
    {
        public delegate JobHandle ScheduleDelegate(
            NativeArray<float3x4> positions, NativeArray<float3x4> normals,
            int resolution, float4x4 trs, JobHandle dependency
        );

        public struct Point4
        {
            public float4x3 positions, normals;
        }

        public interface IShape
        {
            Point4 GetPoint4(int i, float resolution, float invResolution);
        }

        public struct Plane : IShape
        {
            public Point4 GetPoint4(int i, float resolution, float invResolution)
            {
                float4x2 uv = IndexTo4UV(i, resolution, invResolution);
                return new Point4
                {
                    // Substract 0.5f to center the plane in the world origin
                    positions = float4x3(uv.c0 - 0.5f, 0f, uv.c1 - 0.5f),
                    normals = float4x3(0f, 1f, 0f)
                };
            }
        }

        // TODO: Fix non uniform distributions of points along edges of one of the faces
        public struct Tetrahedron : IShape
        {
            public Point4 GetPoint4(int i, float resolution, float invResolution)
            {
                float4x2 uv = IndexTo4UV(i, resolution, invResolution);

                Point4 p;

                // Map UV space to regions
                float4 u = uv.c0;
                float4 v = uv.c1;

                // Regular tetrahedron vertices
                float3 v0 = float3(0f, 0f, 0.5f);
                float3 v1 = float3(0f, 0.47f, -0.17f);
                float3 v2 = float3(-0.4f, -0.24f, -0.17f);
                float3 v3 = float3(0.4f, -0.24f, -0.17f);

                // Determine which region each point belongs to
                bool4 isLeftHalf = u < 0.5f;
                bool4 isBottomHalf = v < 0.5f;

                // Initialize position arrays
                p.positions.c0 = 0f;
                p.positions.c1 = 0f;
                p.positions.c2 = 0f;

                // Calculate normalized triangle coordinates
                float4 normalizedU = select(u * 2f - 1f, u * 2f, isLeftHalf);
                float4 normalizedV = select(v * 2f - 1f, v * 2f, isBottomHalf);

                // Scale to fit within triangles
                float4 maxCoord = max(normalizedU, normalizedV);
                bool4 needsScaling = normalizedU + normalizedV > 1f;
                float4 scale = select(1f, 1f / (normalizedU + normalizedV), needsScaling);
                normalizedU *= scale;
                normalizedV *= scale;

                // Calculate barycentric coordinates
                float4 a = 1f - normalizedU - normalizedV;
                float4 b = normalizedU;
                float4 c = normalizedV;

                // Use for tracking which face each point belongs to (for normal calculation)
                int4 faceIndices = int4(0, 0, 0, 0);

                // Map each point to a tetrahedron face based on UV region
                for (int j = 0; j < 4; j++)
                {
                    float3 pos;

                    if (isLeftHalf[j] && isBottomHalf[j])
                    {
                        // Face 0-1-2: Top-Front-Left
                        pos = a[j] * v0 + b[j] * v1 + c[j] * v2;
                        faceIndices[j] = 0;
                    }
                    else if (!isLeftHalf[j] && isBottomHalf[j])
                    {
                        // Face 0-3-1: Top-Right-Front
                        pos = a[j] * v0 + b[j] * v3 + c[j] * v1;
                        faceIndices[j] = 1;
                    }
                    else if (isLeftHalf[j] && !isBottomHalf[j])
                    {
                        // Face 0-2-3: Top-Left-Right
                        pos = a[j] * v0 + b[j] * v2 + c[j] * v3;
                        faceIndices[j] = 2;
                    }
                    else // (!isLeftHalf[j] && !isBottomHalf[j])
                    {
                        // Face 1-2-3: Front-Left-Right (base)
                        pos = a[j] * v1 + b[j] * v3 + c[j] * v2;
                        faceIndices[j] = 3;
                    }

                    p.positions.c0[j] = pos.x;
                    p.positions.c1[j] = pos.y;
                    p.positions.c2[j] = pos.z;
                }

                // Calculate normals
                p.normals.c0 = 0f;
                p.normals.c1 = 0f;
                p.normals.c2 = 0f;

                // Define face normals
                float3 n0 = normalize(cross(v1 - v0, v2 - v0));  // Face 0-1-2
                float3 n1 = normalize(cross(v3 - v0, v1 - v0));  // Face 0-3-1
                float3 n2 = normalize(cross(v2 - v0, v3 - v0));  // Face 0-2-3
                float3 n3 = normalize(cross(v2 - v1, v3 - v1));  // Face 1-2-3 (base)

                // Assign normals based on face indices
                for (int j = 0; j < 4; j++)
                {
                    float3 normal;

                    switch (faceIndices[j])
                    {
                        case 0:
                            normal = n0;
                            break;
                        case 1:
                            normal = n1;
                            break;
                        case 2:
                            normal = n2;
                            break;
                        default: // case 3
                            normal = n3;
                            break;
                    }

                    p.normals.c0[j] = normal.x;
                    p.normals.c1[j] = normal.y;
                    p.normals.c2[j] = normal.z;
                }

                return p;
            }
        }

        public struct TetrahedronSphere : Shapes.IShape
        {
            public Shapes.Point4 GetPoint4(int i, float resolution, float invResolution)
            {
                float4x2 uv = Shapes.IndexTo4UV(i, resolution, invResolution);

                Shapes.Point4 p;

                float4 u = uv.c0;
                float4 v = uv.c1;

                // Vertices of a regular tetrahedron (normalized)
                float3 v0 = float3(0f, 0f, 0.5f);
                float3 v1 = float3(0f, 0.47f, -0.17f);
                float3 v2 = float3(-0.4f, -0.24f, -0.17f);
                float3 v3 = float3(0.4f, -0.24f, -0.17f);

                bool4 isLeftHalf = u < 0.5f;
                bool4 isBottomHalf = v < 0.5f;

                float4 normalizedU = select(u * 2f - 1f, u * 2f, isLeftHalf);
                float4 normalizedV = select(v * 2f - 1f, v * 2f, isBottomHalf);

                float4 needsScaling = step(1f, normalizedU + normalizedV);
                float4 scale = select(1f, 1f / (normalizedU + normalizedV), needsScaling > 0f);
                normalizedU *= scale;
                normalizedV *= scale;

                float4 a = 1f - normalizedU - normalizedV;
                float4 b = normalizedU;
                float4 c = normalizedV;

                p.positions.c0 = 0f;
                p.positions.c1 = 0f;
                p.positions.c2 = 0f;

                p.normals.c0 = 0f;
                p.normals.c1 = 0f;
                p.normals.c2 = 0f;

                for (int j = 0; j < 4; j++)
                {
                    float3 pos;

                    if (isLeftHalf[j] && isBottomHalf[j])
                    {
                        // Face v0, v1, v2
                        pos = a[j] * v0 + b[j] * v1 + c[j] * v2;
                    }
                    else if (!isLeftHalf[j] && isBottomHalf[j])
                    {
                        // Face v0, v3, v1
                        pos = a[j] * v0 + b[j] * v3 + c[j] * v1;
                    }
                    else if (isLeftHalf[j] && !isBottomHalf[j])
                    {
                        // Face v0, v2, v3
                        pos = a[j] * v0 + b[j] * v2 + c[j] * v3;
                    }
                    else
                    {
                        // Face v1, v3, v2
                        pos = a[j] * v1 + b[j] * v3 + c[j] * v2;
                    }

                    // Normalize to project onto unit sphere
                    float len = rsqrt(dot(pos, pos));
                    pos *= len;

                    // Assign positions
                    p.positions.c0[j] = pos.x;
                    p.positions.c1[j] = pos.y;
                    p.positions.c2[j] = pos.z;

                    // Use normalized position as normal for smooth shading
                    p.normals.c0[j] = pos.x;
                    p.normals.c1[j] = pos.y;
                    p.normals.c2[j] = pos.z;
                }

                return p;
            }
        }


        public struct Octahedron : IShape
        {
            public Point4 GetPoint4(int i, float resolution, float invResolution)
            {
                float4x2 uv = IndexTo4UV(i, resolution, invResolution);

                Point4 p;
                p.positions.c0 = uv.c0 - 0.5f;
                p.positions.c1 = uv.c1 - 0.5f;
                p.positions.c2 = 0.5f - abs(p.positions.c0) - abs(p.positions.c1);
                float4 offset = max(-p.positions.c2, 0f);
                p.positions.c0 += select(-offset, offset, p.positions.c0 < 0f);
                p.positions.c1 += select(-offset, offset, p.positions.c1 < 0f);

                float4 norm = rsqrt(
                    p.positions.c0 * p.positions.c0 +
                    p.positions.c1 * p.positions.c1 +
                    p.positions.c2 * p.positions.c2
                );

                p.normals.c0 = p.positions.c0 * norm;
                p.normals.c1 = p.positions.c1 * norm;
                p.normals.c2 = p.positions.c2 * norm;

                return p;
            }
        }

        public struct OctahedronSphere : IShape
        {
            public Point4 GetPoint4(int i, float resolution, float invResolution)
            {
                float4x2 uv = IndexTo4UV(i, resolution, invResolution);

                Point4 p;
                p.positions.c0 = uv.c0 - 0.5f;
                p.positions.c1 = uv.c1 - 0.5f;
                p.positions.c2 = 0.5f - abs(p.positions.c0) - abs(p.positions.c1);
                float4 offset = max(-p.positions.c2, 0f);
                p.positions.c0 += select(-offset, offset, p.positions.c0 < 0f);
                p.positions.c1 += select(-offset, offset, p.positions.c1 < 0f);

                float4 scale = 0.5f * rsqrt(
                    p.positions.c0 * p.positions.c0 +
                    p.positions.c1 * p.positions.c1 +
                    p.positions.c2 * p.positions.c2
                );
                p.positions.c0 *= scale;
                p.positions.c1 *= scale;
                p.positions.c2 *= scale;

                p.normals = p.positions;
                return p;
            }
        }

        public struct UVSphere : IShape
        {
            public Point4 GetPoint4(int i, float resolution, float invResolution)
            {
                float4x2 uv = IndexTo4UV(i, resolution, invResolution);

                float r = 0.5f;
                float4 s = r * sin(PI * uv.c1);

                Point4 p;
                p.positions.c0 = s * sin(2f * PI * uv.c0);
                p.positions.c1 = r * cos(PI * uv.c1);
                p.positions.c2 = s * cos(2f * PI * uv.c0);
                p.normals = p.positions;
                return p;
            }
        }

        public struct Torus : IShape
        {
            public Point4 GetPoint4(int i, float resolution, float invResolution)
            {
                float4x2 uv = IndexTo4UV(i, resolution, invResolution);

                float r1 = 0.375f;
                float r2 = 0.125f;
                float4 s = r1 + r2 * cos(2f * PI * uv.c1);

                Point4 p;
                p.positions.c0 = s * sin(2f * PI * uv.c0);
                p.positions.c1 = r2 * sin(2f * PI * uv.c1);
                p.positions.c2 = s * cos(2f * PI * uv.c0);
                p.normals = p.positions;
                p.normals.c0 -= r1 * sin(2f * PI * uv.c0);
                p.normals.c2 -= r1 * cos(2f * PI * uv.c0);
                return p;
            }
        }

        public static float4x2 IndexTo4UV(int i, float resolution, float invResolution)
        {
            float4x2 uv;
            float4 i4 = 4f * i + float4(0f, 1f, 2f, 3f);
            uv.c1 = floor(invResolution * i4 + 0.00001f);
            uv.c0 = invResolution * (i4 - resolution * uv.c1 + 0.5f);
            uv.c1 = invResolution * (uv.c1 + 0.5f);
            return uv;
        }

        [BurstCompile(FloatPrecision.Standard, FloatMode.Fast, CompileSynchronously = true)]
        public struct Job<S> : IJobFor where S : struct, IShape
        {
            [WriteOnly] private NativeArray<float3x4> positions, normals;
            public float resolution, invResolution;

            public float3x4 positionTRS, normalTRS;

            public void Execute(int i)
            {
                Point4 p = default(S).GetPoint4(i, resolution, invResolution);

                positions[i] =
                    transpose(positionTRS.TransformVectors(p.positions));

                float3x4 n =
                    transpose(normalTRS.TransformVectors(p.normals, 0f));
                normals[i] = float3x4(
                    normalize(n.c0), normalize(n.c1), normalize(n.c2), normalize(n.c3)
                );
            }

            /// <summary>
            /// Encapsulates the creation of the job and its scheduling
            /// </summary>
            /// <param name="positions"></param>
            /// <param name="resolution"></param>
            /// <param name="dependency"></param>
            /// <returns></returns>
            public static JobHandle ScheduleParallel(
                NativeArray<float3x4> positions, NativeArray<float3x4> normals,
                int resolution, float4x4 trs, JobHandle dependency) =>
                new Job<S>
                {
                    positions = positions,
                    normals = normals,
                    resolution = resolution,
                    invResolution = 1f / resolution,
                    positionTRS = trs.Get3x4(),
                    normalTRS = transpose(inverse(trs)).Get3x4()
                }.ScheduleParallel(positions.Length, resolution, dependency);
        }
    }

}