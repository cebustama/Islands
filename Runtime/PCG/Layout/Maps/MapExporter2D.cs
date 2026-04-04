using System;

namespace Islands.PCG.Layout.Maps
{
    /// <summary>
    /// Adapter that reads a completed <see cref="MapContext2D"/> and produces a
    /// <see cref="MapDataExport"/> — a managed, context-independent snapshot of all
    /// created layers and scalar fields.
    ///
    /// Contract invariants:
    /// - Adapters-last: this class only reads the context; it never writes to it.
    /// - Determinism: given the same context state, Export always produces identical output.
    /// - All present layers and fields are exported; absent ones leave a null slot.
    /// - The returned snapshot is independent of the source context's lifetime.
    /// </summary>
    public static class MapExporter2D
    {
        /// <summary>
        /// Exports all created layers and fields from <paramref name="ctx"/> into a
        /// managed <see cref="MapDataExport"/> snapshot.
        ///
        /// Copies native memory (NativeArray) into managed arrays. The snapshot
        /// remains valid after <c>ctx.Dispose()</c>.
        /// </summary>
        /// <param name="ctx">A completed (post-pipeline) map context. Must not be disposed.</param>
        /// <returns>An immutable managed snapshot of all created layers and fields.</returns>
        /// <exception cref="ArgumentNullException">If <paramref name="ctx"/> is null.</exception>
        public static MapDataExport Export(MapContext2D ctx)
        {
            if (ctx == null) throw new ArgumentNullException(nameof(ctx));

            int w = ctx.Domain.Width;
            int h = ctx.Domain.Height;
            int n = w * h;

            // ── Layers ────────────────────────────────────────────────────────
            int layerCount = (int)MapLayerId.COUNT;
            bool[][] layers = new bool[layerCount][];

            for (int i = 0; i < layerCount; i++)
            {
                var layerId = (MapLayerId)i;
                if (!ctx.IsLayerCreated(layerId))
                    continue;

                bool[] buf = new bool[n];
                ref var grid = ref ctx.GetLayer(layerId);

                // Row-major scan: index = x + y * w (consistent with GridDomain2D.Index)
                for (int y = 0; y < h; y++)
                {
                    int rowBase = y * w;
                    for (int x = 0; x < w; x++)
                    {
                        buf[rowBase + x] = grid.GetUnchecked(x, y);
                    }
                }

                layers[i] = buf;
            }

            // ── Fields ────────────────────────────────────────────────────────
            int fieldCount = (int)MapFieldId.COUNT;
            float[][] fields = new float[fieldCount][];

            for (int i = 0; i < fieldCount; i++)
            {
                var fieldId = (MapFieldId)i;
                if (!ctx.IsFieldCreated(fieldId))
                    continue;

                float[] buf = new float[n];
                ref var field = ref ctx.GetField(fieldId);

                // Flat copy from NativeArray<float> — Values is row-major by contract.
                for (int j = 0; j < n; j++)
                {
                    buf[j] = field.Values[j];
                }

                fields[i] = buf;
            }

            return new MapDataExport(w, h, ctx.Seed, layers, fields);
        }
    }
}