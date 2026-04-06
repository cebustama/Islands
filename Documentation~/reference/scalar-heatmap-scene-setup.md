# Scalar Heatmap Tilemap — Scene Setup Guide

**Context:** Post-N2 Issue 2. Adds a dedicated overlay tilemap for scalar field
visualization (Height, CoastDist, etc.) as a standalone color heatmap.

## Prerequisites

- `PCGMapTilemapVisualization` component with the post-N2 patch applied.
- An existing PCG Map Tilemap scene with a working base tilemap.

## Setup Steps

### 1. Create the Heatmap Grid + Tilemap

The heatmap tilemap needs its **own Grid** component so its cell size can differ
from the base tilemap (e.g., 0.5×0.5 for sub-cell fidelity, or 1×1 for 1:1).

1. In the Hierarchy, right-click → **Create Empty** → name it `ScalarHeatmapGrid`.
2. Add a **Grid** component. Set `Cell Size` to `(1, 1, 0)` for 1:1 mapping with
   the base grid (or `(0.5, 0.5, 0)` for 2× sub-cell resolution).
3. Create a child GameObject under `ScalarHeatmapGrid` → name it `HeatmapTilemap`.
4. Add a **Tilemap** component to `HeatmapTilemap`.
5. Add a **Tilemap Renderer** component. Set:
   - `Sorting Layer`: same as the base tilemap's layer (e.g., `Default`).
   - `Order in Layer`: **+1** above the base tilemap (and above the overlay tilemap
     if multi-layer mode is used). The heatmap must render on top.
   - `Mode`: `Individual` (default) is fine; `Chunk` works too.

### 2. Wire into the Visualization Component

1. Select the GameObject carrying `PCGMapTilemapVisualization`.
2. In the **Scalar Heatmap Tilemap** section:
   - Drag `HeatmapTilemap` into the `Scalar Heatmap Tilemap` slot.
   - Set `Heatmap Alpha` to taste (0.65 is a good starting point for a semi-transparent
     overlay that still shows the art tiles beneath).
3. In the **Scalar Field Overlay** section:
   - Enable `Enable Scalar Overlay`.
   - Choose a field (`Height` or `CoastDist`).
   - Adjust `Overlay Min` / `Overlay Max` and gradient colors as needed.

### 3. Verify

- The heatmap should appear as a colored overlay on top of the art tilemap.
- Adjusting `Heatmap Alpha` should smoothly fade the overlay in/out.
- Toggling `Enable Scalar Overlay` off should clear the heatmap tilemap.
- Switching `Overlay Field` should re-color the heatmap with the new field's data.
- Changing seed should regenerate both the map and the heatmap.

### 4. Fallback behavior

If the `Scalar Heatmap Tilemap` slot is left empty (null), the per-cell tint
fallback is used instead (colors the base tilemap's tiles directly via
`Tilemap.SetColor`). The tint approach is lighter-weight but less precise —
it multiplies the tile's art color rather than replacing it.

## Notes

- The heatmap path quantizes colors to 256 steps for tile-cache efficiency.
  This produces at most 256 cached `Tile` instances in `ProceduralTileFactory`,
  which is well within performance budget.
- If `useProceduralTiles` is enabled, the procedural tile cache may grow by up
  to 256 entries when the heatmap is active. This is additive; the base
  procedural tiles are unaffected.
- The heatmap tilemap's Grid cell size is independent of the base Grid. If you
  use a different cell size (e.g., 0.5), the heatmap tiles will be smaller than
  base tiles. This is intentional for sub-cell visualization but means the
  heatmap loop would need adjustment to stamp 2× tiles per dimension. **The
  current implementation stamps 1:1 with the map grid**, so set the heatmap Grid
  cell size to match the base Grid for correct alignment.
