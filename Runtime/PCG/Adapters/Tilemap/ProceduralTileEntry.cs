using System;
using Islands.PCG.Layout.Maps;
using UnityEngine;

namespace Islands.PCG.Adapters.Tilemap
{
    /// <summary>
    /// One entry in the procedural color table used by <see cref="ProceduralTileFactory"/>.
    ///
    /// Maps a single <see cref="MapLayerId"/> to a solid <see cref="Color"/> that the factory
    /// will materialise as a runtime <see cref="UnityEngine.Tilemaps.Tile"/> asset —
    /// no pre-authored sprite art required.
    ///
    /// Priority is positional (low→high), matching <see cref="TilemapLayerEntry"/> semantics:
    /// entries earlier in the array are lower priority and are overwritten by later entries.
    ///
    /// Serializable for Inspector assignment.
    /// Phase H2d.
    /// </summary>
    [Serializable]
    public struct ProceduralTileEntry
    {
        /// <summary>The pipeline layer this entry represents.</summary>
        public MapLayerId LayerId;

        /// <summary>Solid fill color for the generated tile.</summary>
        public Color Color;
    }
}