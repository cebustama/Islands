using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

using static Unity.Mathematics.math;

namespace Islands
{
    public abstract class Visualization : MonoBehaviour
    {
        public enum Shape
        {
            Plane,
            Tetrahedron, TetrahedronSphere,
            Octahedron, OctahedronSphere,
            UVSphere,
            Torus
        }

        private static Shapes.ScheduleDelegate[] shapeJobs =
        {
        Shapes.Job<Shapes.Plane>.ScheduleParallel,

        Shapes.Job<Shapes.Tetrahedron>.ScheduleParallel,
        Shapes.Job<Shapes.TetrahedronSphere>.ScheduleParallel,

        Shapes.Job<Shapes.Octahedron>.ScheduleParallel,
        Shapes.Job<Shapes.OctahedronSphere>.ScheduleParallel,

        Shapes.Job<Shapes.UVSphere>.ScheduleParallel,

        Shapes.Job<Shapes.Torus>.ScheduleParallel
    };

        private int
            positionsId = Shader.PropertyToID("_Positions"),
            normalsId = Shader.PropertyToID("_Normals"),
            configId = Shader.PropertyToID("_Config");

        [SerializeField] private Mesh instanceMesh;
        [SerializeField] private Material material;
        [SerializeField] private Shape shape;
        [SerializeField, Range(0.1f, 10f)] private float instanceScale = 2f;
        [SerializeField, Range(1, 512)] private int resolution = 16;
        [SerializeField, Range(-0.5f, 0.5f)] private float displacement = 0.1f;

        private NativeArray<float3x4> positions, normals;

        private ComputeBuffer positionsBuffer, normalsBuffer;
        private MaterialPropertyBlock propertyBlock;

        private bool isDirty;
        private Bounds bounds;

        protected abstract void EnableVisualization(
            int dataLength, MaterialPropertyBlock propertyBlock);
        protected abstract void DisableVisualization();
        protected abstract void UpdateVisualization(
            NativeArray<float3x4> positions, int resolution, JobHandle handle
        );

        private void OnEnable()
        {
            isDirty = true;

            int length = resolution * resolution;
            length = length / 4 + (length & 1);

            positions = new NativeArray<float3x4>(length, Allocator.Persistent);
            normals = new NativeArray<float3x4>(length, Allocator.Persistent);
            positionsBuffer = new ComputeBuffer(length * 4, 3 * sizeof(float));
            normalsBuffer = new ComputeBuffer(length * 4, 3 * sizeof(float));

            propertyBlock ??= new MaterialPropertyBlock();
            EnableVisualization(length, propertyBlock);
            propertyBlock.SetBuffer(positionsId, positionsBuffer);
            propertyBlock.SetBuffer(normalsId, normalsBuffer);
            propertyBlock.SetVector(configId, new Vector4(
                resolution, instanceScale / resolution, displacement)
            );
        }

        private void OnDisable()
        {
            positions.Dispose();
            normals.Dispose();
            positionsBuffer.Release();
            normalsBuffer.Release();
            positionsBuffer = null;
            normalsBuffer = null;
            DisableVisualization();
        }

        protected virtual void OnValidate()
        {
            if (positionsBuffer != null && enabled)
            {
                OnDisable();
                OnEnable();
            }
        }

        private void Update()
        {
            if (isDirty || transform.hasChanged)
            {
                isDirty = false;
                transform.hasChanged = false;

                UpdateVisualization(positions, resolution,
                    shapeJobs[(int)shape](
                        positions, normals, resolution, transform.localToWorldMatrix, default
                    )
                );

                positionsBuffer.SetData(positions.Reinterpret<float3>(3 * 4 * 4));
                normalsBuffer.SetData(normals.Reinterpret<float3>(3 * 4 * 4));

                bounds = new Bounds(
                    transform.position,
                    float3(2f * cmax(abs(transform.lossyScale)) + displacement)
                );
            }

            Graphics.DrawMeshInstancedProcedural(
                instanceMesh, 0, material,
                bounds,
                resolution * resolution,
                propertyBlock
            );
        }
    }
}