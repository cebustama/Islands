using System;

namespace Islands.PCG.Layout.Maps
{
    /// <summary>
    /// Immutable managed snapshot of a completed map run.
    ///
    /// Produced by <see cref="MapExporter2D.Export"/>. Holds managed copies of all
    /// layers and fields that were created in the source <see cref="MapContext2D"/>.
    ///
    /// Lifetime: independent of the source context. Safe to read after ctx.Dispose().
    ///
    /// Indexing: row-major, index = x + y * Width (consistent with GridDomain2D.Index).
    /// </summary>
    public sealed class MapDataExport
    {
        /// <summary>Grid width in cells.</summary>
        public int Width { get; }

        /// <summary>Grid height in cells.</summary>
        public int Height { get; }

        /// <summary>Total cells (Width * Height).</summary>
        public int Length { get; }

        /// <summary>Run seed (>= 1).</summary>
        public uint Seed { get; }

        // Indexed by (int)MapLayerId. Null slot means the layer was not created.
        private readonly bool[][] _layers;

        // Indexed by (int)MapFieldId. Null slot means the field was not created.
        private readonly float[][] _fields;

        /// <summary>
        /// Internal constructor. Use <see cref="MapExporter2D.Export"/> to produce instances.
        /// </summary>
        internal MapDataExport(int width, int height, uint seed, bool[][] layers, float[][] fields)
        {
            if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
            if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));
            if (layers == null) throw new ArgumentNullException(nameof(layers));
            if (fields == null) throw new ArgumentNullException(nameof(fields));
            if (layers.Length != (int)MapLayerId.COUNT)
                throw new ArgumentException($"Expected {(int)MapLayerId.COUNT} layer slots.", nameof(layers));
            if (fields.Length != (int)MapFieldId.COUNT)
                throw new ArgumentException($"Expected {(int)MapFieldId.COUNT} field slots.", nameof(fields));

            Width = width;
            Height = height;
            Length = width * height;
            Seed = seed;
            _layers = layers;
            _fields = fields;
        }

        // ─────────────────────────────────────────────
        // Layer access
        // ─────────────────────────────────────────────

        /// <summary>Returns true if the layer was created and exported.</summary>
        public bool HasLayer(MapLayerId id) => _layers[(int)id] != null;

        /// <summary>
        /// Returns the flat bool array for the given layer (row-major).
        /// Throws <see cref="InvalidOperationException"/> if the layer was not exported.
        /// </summary>
        public bool[] GetLayer(MapLayerId id)
        {
            bool[] buf = _layers[(int)id];
            if (buf == null)
                throw new InvalidOperationException(
                    $"Layer {id} was not exported. Check HasLayer({id}) before calling GetLayer.");
            return buf;
        }

        /// <summary>
        /// Returns the cell value for the given layer at (x, y).
        /// Throws if the layer was not exported or the coordinate is out of bounds.
        /// </summary>
        public bool GetCell(MapLayerId id, int x, int y)
        {
            EnsureInBounds(x, y);
            return GetLayer(id)[x + y * Width];
        }

        // ─────────────────────────────────────────────
        // Field access
        // ─────────────────────────────────────────────

        /// <summary>Returns true if the field was created and exported.</summary>
        public bool HasField(MapFieldId id) => _fields[(int)id] != null;

        /// <summary>
        /// Returns the flat float array for the given field (row-major).
        /// Throws <see cref="InvalidOperationException"/> if the field was not exported.
        /// </summary>
        public float[] GetField(MapFieldId id)
        {
            float[] buf = _fields[(int)id];
            if (buf == null)
                throw new InvalidOperationException(
                    $"Field {id} was not exported. Check HasField({id}) before calling GetField.");
            return buf;
        }

        /// <summary>
        /// Returns the scalar value for the given field at (x, y).
        /// Throws if the field was not exported or the coordinate is out of bounds.
        /// </summary>
        public float GetValue(MapFieldId id, int x, int y)
        {
            EnsureInBounds(x, y);
            return GetField(id)[x + y * Width];
        }

        // ─────────────────────────────────────────────
        // Helpers
        // ─────────────────────────────────────────────

        private void EnsureInBounds(int x, int y)
        {
            if ((uint)x >= (uint)Width || (uint)y >= (uint)Height)
                throw new ArgumentOutOfRangeException(
                    $"Coordinate ({x},{y}) is out of bounds for export {Width}x{Height}.");
        }
    }
}