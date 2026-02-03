using System;
using Islands.PCG.Core;
using Islands.PCG.Fields;
using Islands.PCG.Grids;
using Unity.Collections;
using Unity.Mathematics;
using Random = Unity.Mathematics.Random;

namespace Islands.PCG.Layout.Maps
{
    /// <summary>
    /// Single Source of Truth (SSoT) for a headless map-generation run.
    ///
    /// Owns native memory inside its layers/fields; must be disposed.
    /// Registries are stable index-based arrays (no dictionaries) to preserve determinism.
    ///
    /// Determinism rule:
    /// - NEVER create layers/fields with UninitializedMemory.
    ///   Otherwise unchanged bits/floats become nondeterministic and break SnapshotHash64 + goldens.
    /// </summary>
    public sealed class MapContext2D : IDisposable
    {
        public GridDomain2D Domain { get; }

        /// <summary>Sanitized run seed (>= 1).</summary>
        public uint Seed { get; private set; } = 1u;

        public MapTunables2D Tunables { get; private set; } = MapTunables2D.Default;

        /// <summary>
        /// The single RNG for the run. Stages must use this (not UnityEngine.Random).
        /// Mutated by stages (state advances deterministically with stage order).
        /// </summary>
        public Random Rng;

        private readonly Allocator allocator;

        private readonly MaskGrid2D[] layers;
        private readonly bool[] layerCreated;

        private readonly ScalarField2D[] fields;
        private readonly bool[] fieldCreated;

        private bool disposed;

        public MapContext2D(in GridDomain2D domain, Allocator allocator)
        {
            if (domain.Width <= 0 || domain.Height <= 0)
                throw new ArgumentOutOfRangeException(nameof(domain), "Domain must be >= 1x1.");

            Domain = domain;
            this.allocator = allocator;

            int lc = (int)MapLayerId.COUNT;
            layers = new MaskGrid2D[lc];
            layerCreated = new bool[lc];

            int fc = (int)MapFieldId.COUNT;
            fields = new ScalarField2D[fc];
            fieldCreated = new bool[fc];

            // Initialize deterministic baseline RNG state
            Seed = 1u;
            Rng = new Random(Seed);
        }

        /// <summary>
        /// Prepares the context for a new deterministic run (sets seed/tunables and resets RNG).
        /// Optionally clears all created layers (fields are left as-is unless stages overwrite them).
        /// </summary>
        public void BeginRun(in MapInputs inputs, bool clearLayers = true)
        {
            ThrowIfDisposed();

            if (inputs.Domain.Width != Domain.Width || inputs.Domain.Height != Domain.Height)
                throw new ArgumentException("Inputs.Domain must match the context Domain.", nameof(inputs));

            Seed = inputs.Seed;           // already sanitized to >= 1
            Tunables = inputs.Tunables;
            Rng = new Random(Seed);       // Unity.Mathematics.Random requires non-zero seed

            if (clearLayers)
                ClearAllCreatedLayers();
        }

        public bool IsLayerCreated(MapLayerId id) => layerCreated[(int)id];
        public bool IsFieldCreated(MapFieldId id) => fieldCreated[(int)id];

        /// <summary>
        /// Ensures a layer exists; allocates it lazily using the context allocator.
        ///
        /// IMPORTANT:
        /// - On first creation we ALWAYS allocate deterministically (clearToZero:true).
        /// - The clearToZero parameter only affects whether we Clear() an already-created layer.
        /// </summary>
        public ref MaskGrid2D EnsureLayer(MapLayerId id, bool clearToZero = true)
        {
            ThrowIfDisposed();

            int i = (int)id;

            if (!layerCreated[i])
            {
                // Always deterministic on creation (never UninitializedMemory).
                layers[i] = new MaskGrid2D(Domain, allocator, clearToZero: true);
                layerCreated[i] = true;

                // If caller asked not to clear, we still return a deterministic zero-initialized layer.
                return ref layers[i];
            }

            if (clearToZero)
                layers[i].Clear();

            return ref layers[i];
        }

        /// <summary>
        /// Gets an existing layer by ref; throws if missing to keep stage failures obvious/deterministic.
        /// </summary>
        public ref MaskGrid2D GetLayer(MapLayerId id)
        {
            ThrowIfDisposed();

            int i = (int)id;
            if (!layerCreated[i])
                throw new InvalidOperationException($"Layer {id} was not created. Call EnsureLayer({id}) first.");

            return ref layers[i];
        }

        /// <summary>
        /// Ensures a field exists; allocates it lazily using the context allocator.
        ///
        /// IMPORTANT:
        /// - On first creation we ALWAYS allocate deterministically (ClearMemory).
        /// - The clearToZero parameter only affects whether we Clear() an already-created field.
        /// </summary>
        public ref ScalarField2D EnsureField(MapFieldId id, bool clearToZero = true)
        {
            ThrowIfDisposed();

            int i = (int)id;

            if (!fieldCreated[i])
            {
                // Always deterministic on creation (never UninitializedMemory).
                fields[i] = new ScalarField2D(Domain, allocator, NativeArrayOptions.ClearMemory);
                fieldCreated[i] = true;

                return ref fields[i];
            }

            if (clearToZero)
            {
                // Deterministically reset when requested.
                fields[i].Clear(0f);
            }

            return ref fields[i];
        }

        public ref ScalarField2D GetField(MapFieldId id)
        {
            ThrowIfDisposed();

            int i = (int)id;
            if (!fieldCreated[i])
                throw new InvalidOperationException($"Field {id} was not created. Call EnsureField({id}) first.");

            return ref fields[i];
        }

        public void ClearAllCreatedLayers()
        {
            ThrowIfDisposed();

            for (int i = 0; i < layers.Length; i++)
            {
                if (layerCreated[i])
                    layers[i].Clear();
            }
        }

        public void Dispose()
        {
            if (disposed) return;

            // Dispose layers
            for (int i = 0; i < layers.Length; i++)
            {
                if (layerCreated[i])
                {
                    layers[i].Dispose();
                    layers[i] = default;
                    layerCreated[i] = false;
                }
            }

            // Dispose fields
            for (int i = 0; i < fields.Length; i++)
            {
                if (fieldCreated[i])
                {
                    fields[i].Dispose();
                    fields[i] = default;
                    fieldCreated[i] = false;
                }
            }

            disposed = true;
        }

        private void ThrowIfDisposed()
        {
            if (disposed) throw new ObjectDisposedException(nameof(MapContext2D));
        }
    }
}
