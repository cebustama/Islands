using Islands.Meshes;
using Islands.Meshes.Generators;
using Islands.Meshes.Streams;
using Islands.Surfaces;
using Islands;

using UnityEngine;
using UnityEngine.Rendering;

using static Islands.Noise;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class ProceduralSurface : MonoBehaviour
{
    private static int materialIsPlaneId = Shader.PropertyToID("_IsPlane");

    private static AdvancedMeshJobScheduleDelegate[] meshJobs =
    {
        MeshJob<SharedSquareGrid, SingleStream>.ScheduleParallel,
        MeshJob<SharedTriangleGrid, SingleStream>.ScheduleParallel,
        MeshJob<PointyHexagonGrid, SingleStream>.ScheduleParallel,
        MeshJob<FlatHexagonGrid, SingleStream>.ScheduleParallel,
        MeshJob<Cube, SingleStream>.ScheduleParallel,
        MeshJob<Tetrahedron, SingleStream>.ScheduleParallel,
        MeshJob<Octahedron, SingleStream>.ScheduleParallel,
        MeshJob<Dodecahedron, SingleStream>.ScheduleParallel,
        MeshJob<Icosahedron, SingleStream>.ScheduleParallel,
        MeshJob<SharedCubeSphere, SingleStream>.ScheduleParallel,
        MeshJob<Icosphere, SingleStream>.ScheduleParallel,
        MeshJob<GeoIcosphere, SingleStream>.ScheduleParallel,
        MeshJob<Octasphere, SingleStream>.ScheduleParallel,
        MeshJob<GeoOctasphere, SingleStream>.ScheduleParallel,
        MeshJob<UVSphere, SingleStream>.ScheduleParallel
    };

    private static SurfaceJobScheduleDelegate[,] surfaceJobs =
    {
        {
            SurfaceJob<Lattice1D<LatticeNormal, Perlin>>.ScheduleParallel,
            SurfaceJob<Lattice2D<LatticeNormal, Perlin>>.ScheduleParallel,
            SurfaceJob<Lattice3D<LatticeNormal, Perlin>>.ScheduleParallel
        },
        {
            SurfaceJob<Lattice1D<LatticeNormal, Smoothstep<Turbulence<Perlin>>>>
                .ScheduleParallel,
            SurfaceJob<Lattice2D<LatticeNormal, Smoothstep<Turbulence<Perlin>>>>
                .ScheduleParallel,
            SurfaceJob<Lattice3D<LatticeNormal, Smoothstep<Turbulence<Perlin>>>>
                .ScheduleParallel
        },
        {
            SurfaceJob<Lattice1D<LatticeNormal, Value>>.ScheduleParallel,
            SurfaceJob<Lattice2D<LatticeNormal, Value>>.ScheduleParallel,
            SurfaceJob<Lattice3D<LatticeNormal, Value>>.ScheduleParallel
        },
        {
            SurfaceJob<Simplex1D<Simplex>>.ScheduleParallel,
            SurfaceJob<Simplex2D<Simplex>>.ScheduleParallel,
            SurfaceJob<Simplex3D<Simplex>>.ScheduleParallel
        },
        {
            SurfaceJob<Simplex1D<Smoothstep<Turbulence<Simplex>>>>.ScheduleParallel,
            SurfaceJob<Simplex2D<Smoothstep<Turbulence<Simplex>>>>.ScheduleParallel,
            SurfaceJob<Simplex3D<Smoothstep<Turbulence<Simplex>>>>.ScheduleParallel
        },
        {
            SurfaceJob<Simplex1D<Value>>.ScheduleParallel,
            SurfaceJob<Simplex2D<Value>>.ScheduleParallel,
            SurfaceJob<Simplex3D<Value>>.ScheduleParallel,
        },
        {
            SurfaceJob<Voronoi1D<LatticeNormal, Worley, F1>>.ScheduleParallel,
            SurfaceJob<Voronoi2D<LatticeNormal, Worley, F1>>.ScheduleParallel,
            SurfaceJob<Voronoi3D<LatticeNormal, Worley, F1>>.ScheduleParallel
        },
        {
            SurfaceJob<Voronoi1D<LatticeNormal, Worley, F2>>.ScheduleParallel,
            SurfaceJob<Voronoi2D<LatticeNormal, Worley, F2>>.ScheduleParallel,
            SurfaceJob<Voronoi3D<LatticeNormal, Worley, F2>>.ScheduleParallel
        },
        {
            SurfaceJob<Voronoi1D<LatticeNormal, Worley, F2MinusF1>>.ScheduleParallel,
            SurfaceJob<Voronoi2D<LatticeNormal, Worley, F2MinusF1>>.ScheduleParallel,
            SurfaceJob<Voronoi3D<LatticeNormal, Worley, F2MinusF1>>.ScheduleParallel
        },
        {
            SurfaceJob<Voronoi1D<LatticeNormal, SmoothWorley, F1>>.ScheduleParallel,
            SurfaceJob<Voronoi2D<LatticeNormal, SmoothWorley, F1>>.ScheduleParallel,
            SurfaceJob<Voronoi3D<LatticeNormal, SmoothWorley, F1>>.ScheduleParallel
        },
        {
            SurfaceJob<Voronoi1D<LatticeNormal, SmoothWorley, F2>>.ScheduleParallel,
            SurfaceJob<Voronoi2D<LatticeNormal, SmoothWorley, F2>>.ScheduleParallel,
            SurfaceJob<Voronoi3D<LatticeNormal, SmoothWorley, F2>>.ScheduleParallel
        },
        {
            SurfaceJob<Voronoi1D<LatticeNormal, SmoothWorley, CellAsIslands>>.ScheduleParallel,
            SurfaceJob<Voronoi2D<LatticeNormal, SmoothWorley, CellAsIslands>>.ScheduleParallel,
            SurfaceJob<Voronoi3D<LatticeNormal, SmoothWorley, CellAsIslands>>.ScheduleParallel
        },
        {
            SurfaceJob<Voronoi1D<LatticeNormal, Worley, F1>>.ScheduleParallel,
            SurfaceJob<Voronoi2D<LatticeNormal, Chebyshev, F1>>.ScheduleParallel,
            SurfaceJob<Voronoi3D<LatticeNormal, Chebyshev, F1>>.ScheduleParallel
        },
        {
            SurfaceJob<Voronoi1D<LatticeNormal, Worley, F2>>.ScheduleParallel,
            SurfaceJob<Voronoi2D<LatticeNormal, Chebyshev, F2>>.ScheduleParallel,
            SurfaceJob<Voronoi3D<LatticeNormal, Chebyshev, F2>>.ScheduleParallel
        },
        {
            SurfaceJob<Voronoi1D<LatticeNormal, Worley, F2MinusF1>>.ScheduleParallel,
            SurfaceJob<Voronoi2D<LatticeNormal, Chebyshev, F2MinusF1>>.ScheduleParallel,
            SurfaceJob<Voronoi3D<LatticeNormal, Chebyshev, F2MinusF1>>.ScheduleParallel
        }
    };

    private static FlowJobScheduleDelegate[,] flowJobs = {
        {
            FlowJob<Lattice1D<LatticeNormal, Perlin>>.ScheduleParallel,
            FlowJob<Lattice2D<LatticeNormal, Perlin>>.ScheduleParallel,
            FlowJob<Lattice3D<LatticeNormal, Perlin>>.ScheduleParallel
        },
        {
            FlowJob<Lattice1D<LatticeNormal, Smoothstep<Turbulence<Perlin>>>>.ScheduleParallel,
            FlowJob<Lattice2D<LatticeNormal, Smoothstep<Turbulence<Perlin>>>>.ScheduleParallel,
            FlowJob<Lattice3D<LatticeNormal, Smoothstep<Turbulence<Perlin>>>>.ScheduleParallel
        },
        {
            FlowJob<Lattice1D<LatticeNormal, Value>>.ScheduleParallel,
            FlowJob<Lattice2D<LatticeNormal, Value>>.ScheduleParallel,
            FlowJob<Lattice3D<LatticeNormal, Value>>.ScheduleParallel
        },
        {
            FlowJob<Simplex1D<Simplex>>.ScheduleParallel,
            FlowJob<Simplex2D<Simplex>>.ScheduleParallel,
            FlowJob<Simplex3D<Simplex>>.ScheduleParallel
        },
        {
            FlowJob<Simplex1D<Smoothstep<Turbulence<Simplex>>>>.ScheduleParallel,
            FlowJob<Simplex2D<Smoothstep<Turbulence<Simplex>>>>.ScheduleParallel,
            FlowJob<Simplex3D<Smoothstep<Turbulence<Simplex>>>>.ScheduleParallel
        },
        {
            FlowJob<Simplex1D<Value>>.ScheduleParallel,
            FlowJob<Simplex2D<Value>>.ScheduleParallel,
            FlowJob<Simplex3D<Value>>.ScheduleParallel,
        },
        {
            FlowJob<Voronoi1D<LatticeNormal, Worley, F1>>.ScheduleParallel,
            FlowJob<Voronoi2D<LatticeNormal, Worley, F1>>.ScheduleParallel,
            FlowJob<Voronoi3D<LatticeNormal, Worley, F1>>.ScheduleParallel
        },
        {
            FlowJob<Voronoi1D<LatticeNormal, Worley, F2>>.ScheduleParallel,
            FlowJob<Voronoi2D<LatticeNormal, Worley, F2>>.ScheduleParallel,
            FlowJob<Voronoi3D<LatticeNormal, Worley, F2>>.ScheduleParallel
        },
        {
            FlowJob<Voronoi1D<LatticeNormal, Worley, F2MinusF1>>.ScheduleParallel,
            FlowJob<Voronoi2D<LatticeNormal, Worley, F2MinusF1>>.ScheduleParallel,
            FlowJob<Voronoi3D<LatticeNormal, Worley, F2MinusF1>>.ScheduleParallel
        },
        {
            FlowJob<Voronoi1D<LatticeNormal, SmoothWorley, F1>>.ScheduleParallel,
            FlowJob<Voronoi2D<LatticeNormal, SmoothWorley, F1>>.ScheduleParallel,
            FlowJob<Voronoi3D<LatticeNormal, SmoothWorley, F1>>.ScheduleParallel
        },
        {
            FlowJob<Voronoi1D<LatticeNormal, SmoothWorley, F2>>.ScheduleParallel,
            FlowJob<Voronoi2D<LatticeNormal, SmoothWorley, F2>>.ScheduleParallel,
            FlowJob<Voronoi3D<LatticeNormal, SmoothWorley, F2>>.ScheduleParallel
        },
        {
            FlowJob<Voronoi1D<LatticeNormal, SmoothWorley, CellAsIslands>>.ScheduleParallel,
            FlowJob<Voronoi2D<LatticeNormal, SmoothWorley, CellAsIslands>>.ScheduleParallel,
            FlowJob<Voronoi3D<LatticeNormal, SmoothWorley, CellAsIslands>>.ScheduleParallel
        },
        {
            FlowJob<Voronoi1D<LatticeNormal, Worley, F1>>.ScheduleParallel,
            FlowJob<Voronoi2D<LatticeNormal, Chebyshev, F1>>.ScheduleParallel,
            FlowJob<Voronoi3D<LatticeNormal, Chebyshev, F1>>.ScheduleParallel
        },
        {
            FlowJob<Voronoi1D<LatticeNormal, Worley, F2>>.ScheduleParallel,
            FlowJob<Voronoi2D<LatticeNormal, Chebyshev, F2>>.ScheduleParallel,
            FlowJob<Voronoi3D<LatticeNormal, Chebyshev, F2>>.ScheduleParallel
        },
        {
            FlowJob<Voronoi1D<LatticeNormal, Worley, F2MinusF1>>.ScheduleParallel,
            FlowJob<Voronoi2D<LatticeNormal, Chebyshev, F2MinusF1>>.ScheduleParallel,
            FlowJob<Voronoi3D<LatticeNormal, Chebyshev, F2MinusF1>>.ScheduleParallel
        }
    };

    public enum NoiseType
    {
        Perlin, PerlinSmoothTurbulence, PerlinValue,
        Simplex, SimplexSmoothTurbulence, SimplexValue,
        VoronoiWorleyF1, VoronoiWorleyF2, VoronoiWorleyF2MinusF1, 
        VoronoiWorleySmoothLSE, VoronoiWorleySmoothPoly, VoronoiSmoothIslands,
        VoronoiChebyshevF1, VoronoiChebyshevF2, VoronoiChebyshevF2MinusF1
    }

    [SerializeField] private NoiseType noiseType;
    [SerializeField, Range(1, 3)] private int dimensions = 1;

    public enum MeshType
    {
        SharedSquareGrid,
        SharedTriangleGrid,
        PointyHexagonGrid, FlatHexagonGrid,
        Cube, Tetrahedron, Octahedron, Dodecahedron, Icosahedron,
        // Spheres
        SharedCubeSphere,
        Icosphere, GeoIcosphere,
        Octasphere, GeoOctasphere, UVSphere
    };

    [SerializeField] private MeshType meshType;

    [SerializeField]
    private bool recalculateNormals, recalculateTangents;

    [System.Flags]
    public enum MeshOptimizationMode
    {
        Nothing = 0, ReorderIndices = 1, ReorderVertices = 0b10
    }
    [SerializeField] private MeshOptimizationMode meshOptimization;

    [SerializeField, Range(1, 50)] private int resolution = 1;

    [SerializeField, Range(-1f, 1f)] private float displacement = 0.5f;

    [SerializeField] private Settings noiseSettings = Settings.Default;

    [SerializeField]
    private SpaceTRS domain = new SpaceTRS
    {
        scale = 1f
    };

    public enum FlowMode { Off, Curl, Downhill }

    [SerializeField] private FlowMode flowMode;

    public enum MaterialMode { Displacement, Flat, LatLonMap, CubeMap }
    [SerializeField] private MaterialMode material;
    [SerializeField] private Material[] materials;

    [System.Flags]
    public enum GizmoMode
    {
        Nothing = 0, Vertices = 1, Normals = 0b10, Tangents = 0b100, Triangles = 0b1000
    }
    [SerializeField] private GizmoMode gizmos;

    private Mesh mesh;
    [System.NonSerialized]
    private Vector3[] vertices, normals;
    [System.NonSerialized]
    private Vector4[] tangents;
    [System.NonSerialized]
    private int[] triangles;

    private bool IsPlane => meshType < MeshType.SharedCubeSphere;

    private ParticleSystem flowSystem;

    private void Awake()
    {
        mesh = new Mesh
        {
            name = "Procedural Mesh"
        };

        GetComponent<MeshFilter>().mesh = mesh;
        // replace the displacement material with a duplicate of itself
        materials[(int)displacement] = new Material(materials[(int)displacement]);

        flowSystem = GetComponent<ParticleSystem>();
    }

    private void OnValidate() => enabled = true;

    private void Update()
    {
        GenerateMesh();
        enabled = false;

        vertices = null;
        normals = null;
        tangents = null;
        triangles = null;

        // check whether the displacement material is selected
        if (material == MaterialMode.Displacement)
        {
            // use SetFloat to configure the property,
            // because a boolean shader property uses the float data type
            materials[(int)MaterialMode.Displacement].SetFloat(
                materialIsPlaneId, IsPlane ? 1f : 0f
            );
        }

        GetComponent<MeshRenderer>().material = materials[(int)material];

        if (flowMode == FlowMode.Off)
        {
            flowSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
        else
        {
            flowSystem.Play();
            ParticleSystem.ShapeModule shapeModule = flowSystem.shape;
            shapeModule.shapeType = IsPlane ?
                ParticleSystemShapeType.Rectangle : ParticleSystemShapeType.Sphere;
        }
    }

    private void GenerateMesh()
    {
        Mesh.MeshDataArray meshDataArray = Mesh.AllocateWritableMeshData(1);
        Mesh.MeshData meshData = meshDataArray[0];

        surfaceJobs[(int)noiseType, dimensions - 1](
            meshData, resolution, noiseSettings, domain, displacement, IsPlane,
            meshJobs[(int)meshType](
                mesh, meshData, resolution, default,
                Vector3.one * Mathf.Abs(displacement), true
            )
        ).Complete();

        Mesh.ApplyAndDisposeWritableMeshData(meshDataArray, mesh);

        if (recalculateNormals)
        {
            mesh.RecalculateNormals();
        }
        if (recalculateTangents)
        {
            mesh.RecalculateTangents();
        }

        if (meshOptimization == MeshOptimizationMode.ReorderIndices)
        {
            mesh.OptimizeIndexBuffers();
        }
        else if (meshOptimization == MeshOptimizationMode.ReorderVertices)
        {
            mesh.OptimizeReorderVertexBuffer();
        }
        else if (meshOptimization != MeshOptimizationMode.Nothing)
        {
            mesh.Optimize();
        }
    }

    private void OnParticleUpdateJobScheduled()
    {
        if (flowMode != FlowMode.Off)
        {
            flowJobs[(int)noiseType, dimensions - 1](
                flowSystem, noiseSettings, domain, displacement, 
                IsPlane, flowMode == FlowMode.Curl
            );
        }
    }
    private void OnDrawGizmos()
    {
        if (gizmos == GizmoMode.Nothing || mesh == null)
        {
            return;
        }

        bool drawVertices = (gizmos & GizmoMode.Vertices) != 0;
        bool drawNormals = (gizmos & GizmoMode.Normals) != 0;
        bool drawTangents = (gizmos & GizmoMode.Tangents) != 0;
        bool drawTriangles = (gizmos & GizmoMode.Triangles) != 0;

        if (vertices == null)
        {
            vertices = mesh.vertices;
        }
        if (drawNormals && normals == null)
        {
            drawNormals = mesh.HasVertexAttribute(VertexAttribute.Normal);
            if (drawNormals)
            {
                normals = mesh.normals;
            }
        }
        if (drawTangents && tangents == null)
        {
            drawTangents = mesh.HasVertexAttribute(VertexAttribute.Tangent);
            if (drawTangents)
            {
                tangents = mesh.tangents;
            }
        }
        if (drawTriangles && triangles == null)
        {
            triangles = mesh.triangles;
        }

        //Gizmos.color = Color.cyan;
        Transform t = transform;
        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3 position = t.TransformPoint(vertices[i]);
            if (drawVertices)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawSphere(position, 0.02f);
            }
            if (drawNormals)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawRay(position, t.TransformDirection(normals[i]) * 0.2f);
            }
            if (drawTangents)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawRay(position, t.TransformDirection(tangents[i]) * 0.2f);
            }
        }

        if (drawTriangles)
        {
            float colorStep = 1f / (triangles.Length - 3);
            for (int i = 0; i < triangles.Length; i += 3)
            {
                float c = i * colorStep;
                Gizmos.color = new Color(c, 0f, c);
                Gizmos.DrawSphere(
                    t.TransformPoint((
                        vertices[triangles[i]] +
                        vertices[triangles[i + 1]] +
                        vertices[triangles[i + 2]]
                    ) * (1f / 3f)),
                    0.02f
                );
            }
        }
    }
}
