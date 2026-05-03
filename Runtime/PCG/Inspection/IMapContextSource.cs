// Phase V.a — Inspection seam.
// Spec: planning/active/Phase_V_Design.md §4.
// Decision rationale: V-DD-1 (minimal pull surface), V-DD-2 (source owns world-to-cell).
//
// This file lives in the Islands.PCG.Inspection asmdef so visualization classes in
// Adapters.Tilemap and Samples can implement it without taking on heavier inspection
// dependencies. The interface itself is engine-light — only UnityEngine.Tilemaps.Tilemap
// and Vector3 are referenced from Unity.

using Islands.PCG.Layout.Maps;

namespace Islands.PCG.Inspection
{
    /// <summary>
    /// Read-only handle that exposes a live <see cref="MapContext2D"/> together with
    /// the alignment information required to translate world-space cursor positions
    /// into grid cells. Implemented by visualization components so Phase V tools
    /// (hover tooltip, runtime overlay) can attach without coupling to any specific
    /// visualization class.
    ///
    /// Implementations MUST NOT allocate or mutate pipeline state from any member
    /// of this interface. All members are pure observers.
    /// </summary>
    public interface IMapContextSource
    {
        /// <summary>
        /// The live context. Returns null when the source has not yet generated
        /// (e.g. before first <c>Update()</c>) or has been disposed.
        /// </summary>
        MapContext2D Context { get; }

        /// <summary>
        /// The tilemap whose world-space rect corresponds to the context grid.
        /// May be null if the source does not render to a tilemap; consumers
        /// requiring a tilemap (V.a, V.b) MUST handle null by going inactive.
        /// </summary>
        UnityEngine.Tilemaps.Tilemap Tilemap { get; }

        /// <summary>
        /// True when the source flips rows on render. Drives the row inversion
        /// inside <see cref="TryWorldToCell"/>. Phase V consumers do not need
        /// to know flipY directly; they call <see cref="TryWorldToCell"/>.
        /// flipY handling is the source's responsibility.
        /// </summary>
        bool FlipY { get; }

        /// <summary>
        /// Monotonically increasing counter incremented once per successful pipeline
        /// regeneration. Phase V consumers poll this in their Update() to invalidate
        /// caches without subscribing to events. The numeric value is opaque; only
        /// "changed since last read" matters.
        /// </summary>
        int RegenerationVersion { get; }

        /// <summary>
        /// Convert a world-space position to a context cell. Returns true and writes
        /// (x, y) in context coordinates (i.e. with flipY already resolved) when the
        /// position lands inside the context grid. Returns false (and writes (0, 0))
        /// otherwise, including when <see cref="Context"/> or <see cref="Tilemap"/> is
        /// null.
        /// </summary>
        bool TryWorldToCell(UnityEngine.Vector3 world, out int x, out int y);
    }
}