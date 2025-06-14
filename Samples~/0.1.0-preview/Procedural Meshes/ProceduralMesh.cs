using Islands.Meshes;
using Islands.Meshes.Generators;
using Islands.Meshes.Streams;
using UnityEngine;
using UnityEngine.Rendering;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class ProceduralMesh : MonoBehaviour 
{
    private static MeshJobScheduleDelegate[] jobs =
    {
        MeshJob<SquareGrid, SingleStream>.ScheduleParallel,
        MeshJob<SharedSquareGrid, SingleStream>.ScheduleParallel,
        MeshJob<SharedTriangleGrid, SingleStream>.ScheduleParallel,
        MeshJob<PointyHexagonGrid, SingleStream>.ScheduleParallel,
        MeshJob<FlatHexagonGrid, SingleStream>.ScheduleParallel,
        MeshJob<Cube, SingleStream>.ScheduleParallel,
        MeshJob<Tetrahedron, SingleStream>.ScheduleParallel,
        MeshJob<Octahedron, SingleStream>.ScheduleParallel,
        MeshJob<Dodecahedron, SingleStream>.ScheduleParallel,
        MeshJob<Icosahedron, PositionStream>.ScheduleParallel,
        MeshJob<CubeSphere, SingleStream>.ScheduleParallel,
        MeshJob<SharedCubeSphere, PositionStream>.ScheduleParallel,
        MeshJob<Icosphere, PositionStream>.ScheduleParallel,
        MeshJob<GeoIcosphere, PositionStream>.ScheduleParallel,
        MeshJob<Octasphere, SingleStream>.ScheduleParallel,
        MeshJob<GeoOctasphere, SingleStream>.ScheduleParallel,
        MeshJob<UVSphere, SingleStream>.ScheduleParallel
    };

    public enum MeshType
    {
        SquareGrid, SharedSquareGrid, 
        SharedTriangleGrid, 
        PointyHexagonGrid, FlatHexagonGrid,
        Cube, Tetrahedron, Octahedron, Dodecahedron, Icosahedron,
        CubeSphere, SharedCubeSphere, 
        Icosphere, GeoIcosphere,
        Octasphere, GeoOctasphere, UVSphere
    };

    [SerializeField] private MeshType meshType;

    [System.Flags]
    public enum MeshOptimizationMode
    {
        Nothing = 0, ReorderIndices = 1, ReorderVertices = 0b10
    }
    [SerializeField] private MeshOptimizationMode meshOptimization;

    [SerializeField, Range(1, 50)] private int resolution = 1;

    public enum MaterialMode { Flat, Ripple, LatLonMap, CubeMap }
    [SerializeField] private MaterialMode material;
    [SerializeField] private Material[] materials;

    [System.Flags]
    public enum GizmoMode { 
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

    private void Awake()
    {
        mesh = new Mesh
        {
            name = "Procedural Mesh"
        };

        GetComponent<MeshFilter>().mesh = mesh;
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

        GetComponent<MeshRenderer>().material = materials[(int)material];
    }

    private void GenerateMesh()
    {
        Mesh.MeshDataArray meshDataArray = Mesh.AllocateWritableMeshData(1);
        Mesh.MeshData meshData = meshDataArray[0];

        jobs[(int)meshType](mesh, meshData, resolution, default).Complete();

        Mesh.ApplyAndDisposeWritableMeshData(meshDataArray, mesh);

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
