# Tileset Import Guide

Status: Active reference  
Authority: Developer workflow reference. Not a runtime SSoT.  
Scope: Converting tileset PNG files into Unity TileBase assets for use with `TilemapAdapter2D`.  
Home: `Documentation~/reference/` — consulted at implementation time, not a governed contract surface.

---

## Folder hierarchy — Islands project convention

```
Runtime/PCG/Samples/PCG Map Tilemap/
├── Tilesets/
│   └── <SetName>/                         e.g. DragonWarrior, Overworld16bit
│       ├── _src/                          original PNG, untouched (source of truth)
│       │   └── <OriginalFilename>.png
│       ├── <SetName>_Tiles.png            transparency-cleaned import copy
│       └── Tiles/                         auto-generated Tile assets (Unity prompt target)
│           ├── Tile_DeepWater.asset
│           ├── Tile_Land.asset
│           └── ...
└── Palettes/
    └── <SetName>.asset                    Tile Palette (optional; useful for debug/preview)
```

**Rules:**
- `_src/` keeps the original rip untouched. Never import from it directly into Unity.
- The cleaned PNG sits one level up and is the actual Unity import target.
- `Tiles/` is where Unity saves auto-generated `.asset` files — always point the save dialog here.
- One `Tilesets/<SetName>/` folder per tileset source.
- `Palettes/` is optional for programmatic use (our adapter places tiles in code, not by painting),
  but keep the palette for debug/preview and manual testing.

---

## Quick-reference card

```
1. Measure tile size (px)
2. Remove background color → save clean PNG → keep original in _src/
3. Unity import: Texture Type = Sprite, Sprite Mode = Multiple, PPU = tile size px,
   Filter Mode = Point, Compression = None → Apply
4. Sprite Editor: Slice → Grid By Cell Size = tile size px → Apply
5. Tile Palette window: drag cleaned PNG onto palette → save assets to Tiles/ folder
6. Rename used tile assets by terrain type
7. Wire into PCGMapTilemapSample / PCGMapTilemapVisualization priority table
```

---

## Step-by-step process

### Phase 0 — Know your tile size

Confirm the pixel dimensions of a single tile before starting. Open the PNG in any image viewer
and count pixels along one row/column of tiles.

Common sizes:
- NES games (Dragon Warrior, Zelda): **16 × 16 px**
- SNES / GBA games: **16 × 16** or **32 × 32 px**
- Modern indie pixel art: **16 × 16**, **32 × 32**, or **64 × 64 px**

> **Rule:** Always confirm tile size before starting. Slicing with the wrong cell size produces
> misaligned sprites that are painful to redo after Tile assets have been generated.

---

### Phase 1 — Remove the background color (outside Unity)

Many ripped or exported tilesets have a solid color background rather than transparency (alpha).
Unity has no built-in color-keying on import — the background must be removed in an external tool
before the PNG is placed in the project.

**Recommended tools:**

| Tool | Method | Notes |
|------|---------|-------|
| **Aseprite** | *Edit → Replace Color* → set to alpha=0 → Export PNG | Best for pixel art; lossless; preserves exact pixel values |
| **GIMP** | *Colors → Color to Alpha* | Free; good for solid single-color backgrounds |
| **Photoshop** | Magic Wand (tolerance 0) → Delete | Watch anti-aliasing — set to no anti-alias for pixel art |
| **Online (spritebuff.com)** | Magic Wand / color range tool | Quick; no install; verify output quality before using |
| **Python + Pillow** | Script below | Repeatable; best for batches or future automation |

**Python script — single-color background removal:**
```python
from PIL import Image

# Sample the exact background color from your PNG before running.
# For the Dragon Warrior NES sheet the background is approximately (0, 168, 168).
BG_COLOR  = (0, 168, 168)   # ← adjust to match your sheet
TOLERANCE = 10              # increase if edge pixels remain; decrease if tile colors bleed

img  = Image.open("input.png").convert("RGBA")
data = img.load()

for y in range(img.height):
    for x in range(img.width):
        r, g, b, a = data[x, y]
        if (abs(r - BG_COLOR[0]) < TOLERANCE and
            abs(g - BG_COLOR[1]) < TOLERANCE and
            abs(b - BG_COLOR[2]) < TOLERANCE):
            data[x, y] = (r, g, b, 0)   # fully transparent

img.save("output_clean.png")
```

**How to sample the exact background color:**
Open the original PNG in any image editor, use the eyedropper tool on the background area,
and read the RGB values. Use those values in `BG_COLOR` above.

> **Rule:** Always save the cleaned file as a new PNG alongside the original. Never overwrite
> `_src/`. Verify the result by opening it in a viewer that displays a checkerboard pattern
> for transparent areas — every background pixel should show checkerboard, every tile pixel
> should show opaque color.

---

### Phase 2 — Import into Unity

1. Copy the cleaned PNG into `Tilesets/<SetName>/` in your project.
2. Select it in the **Project window**.
3. In the **Inspector**, configure:

| Setting | Value | Reason |
|---------|-------|--------|
| Texture Type | `Sprite (2D and UI)` | Required; other types are not supported for Tilemaps |
| Sprite Mode | `Multiple` | One sheet contains many individual tiles |
| Pixels Per Unit (PPU) | tile size in px (e.g. `16`) | Makes one tile = 1 Unity unit = one Tilemap cell |
| Filter Mode | `Point (no filter)` | Pixel art must not be blurred; bilinear/trilinear destroys crispness |
| Compression | `None` | Prevents compression artifacts on pixel-exact art |

4. Click **Apply**.

> **Rule:** PPU = tile width in pixels. For 16px tiles → PPU 16. This ensures one tile occupies
> exactly one cell on the default Tilemap (cell size 1×1 Unity units). Wrong PPU causes tiles to
> appear too large, too small, or misaligned on the grid.

---

### Phase 3 — Slice in Sprite Editor

1. With the PNG selected, click **Open Sprite Editor** in the Inspector.
   - If the button is greyed out or missing, install the **2D Sprite** package via Package Manager.
2. In the Sprite Editor, open the **Slice** dropdown (top-left).
3. Set:
   - **Type:** `Grid By Cell Size`
   - **Pixel Size X/Y:** tile size (e.g. `16` × `16`)
   - **Offset X/Y:** `0, 0` — adjust only if the sheet has a border (check visually)
   - **Padding X/Y:** `0, 0` — adjust only if tiles have gaps between them
4. Click **Slice**, then **Apply** (top-right). Close the Sprite Editor.

After applying, click the arrow on the PNG asset in the Project window to expand it.
You should see individual sub-sprites named `<SheetName>_0`, `_1`, `_2`, etc.

> **Rule:** Verify the slice grid visually before applying. Red grid lines in the Sprite Editor
> preview must align exactly with the borders between tiles. If they don't match, adjust
> Offset/Padding and re-slice.

---

### Phase 4 — Generate Tile assets via Tile Palette

The fastest path to `.asset` files is dragging the spritesheet directly onto a Tile Palette.
Unity auto-generates one `Tile.asset` per sprite slice.

1. Open `Window → 2D → Tile Palette`.
2. In the Tile Palette window, click the palette dropdown → **Create New Palette**.
   - Name: `<SetName>` (e.g. `DragonWarrior`)
   - Grid: Rectangular
   - Cell Size: Automatic
   - Save to: `Palettes/`
3. Drag the sliced PNG from the Project window **onto the Tile Palette window**.
4. Unity prompts: *"Where do you want to save the Tile assets?"*
   → Navigate to `Tilesets/<SetName>/Tiles/` and confirm.

Unity generates `<SheetName>_0.asset`, `<SheetName>_1.asset`, etc. in the `Tiles/` folder.
These are `Tile` (a concrete subclass of `TileBase`) and are ready to assign to the priority table.

> **Rule:** Always point the save dialog to the `Tiles/` subfolder of the correct tileset.
> Tile assets saved to the wrong location are hard to track down later.

---

### Phase 5 — Identify and rename tiles

Unity names generated assets by index (`_0`, `_1`, ...). You need to identify which index
corresponds to which terrain type, then rename the assets you will actually use.

**Method:**
1. Hover tiles in the Tile Palette window — the bottom bar shows the asset name and index.
2. Cross-reference with the original sheet: tiles are indexed left-to-right, top-to-bottom,
   starting from index 0 at the top-left.
3. Select the `.asset` file in the Project window, press **F2** (or right-click → Rename),
   and rename to the terrain type: e.g. `Tile_DeepWater`, `Tile_Land`, `Tile_Vegetation`.

> **Rule:** Only rename tiles you will actually use in the priority table. Leave unused tiles
> with their index names. Do not delete auto-generated tiles — they may be needed later.

---

### Phase 6 — Wire into the priority table

In the Inspector on your `PCGMapTilemapSample` or `PCGMapTilemapVisualization` component,
populate the **Priority Table** (low → high priority):

| Slot | `LayerId` | Tile asset | Visual role |
|------|-----------|-----------|-------------|
| 0 | `DeepWater` | `Tile_DeepWater` | Open ocean |
| 1 | `ShallowWater` | `Tile_ShallowWater` | Coastal water ring |
| 2 | `Land` | `Tile_Land` | Grass / ground |
| 3 | `Vegetation` | `Tile_Vegetation` | Forest |
| 4 | `HillsL1` | `Tile_Hills` | Rolling hills |
| 5 | `HillsL2` | `Tile_Mountains` | Mountain peaks |
| 6 | `LandCore` | `Tile_Plains` | Inner flat land |
| 7 | `LandEdge` | `Tile_Beach` | Sandy shoreline |
| 8 | `Stairs` | `Tile_Pass` | Mountain pass / steps |

Set **Fallback Tile** to `Tile_DeepWater` to catch any cells that don't match a layer entry.

Later entries overwrite earlier ones when a cell belongs to multiple layers simultaneously,
so place the most visually prominent terrain types last (highest priority).

---

## Common gotchas

| Symptom | Cause | Fix |
|---------|-------|-----|
| Blue/colored fringe around tiles | Background not fully removed | Lower tolerance slightly; re-clean |
| Tiles appear too large on grid | PPU too low | Set PPU = tile pixel width, Apply |
| Tiles appear too small on grid | PPU too high | Set PPU = tile pixel width, Apply |
| Blurry / soft pixel art | Filter Mode not Point | Set Filter Mode = Point (no filter), Apply |
| Color banding / artifacts on tiles | Compression active | Set Compression = None, Apply |
| Slice grid misaligned | Wrong cell size or non-zero offset needed | Adjust in Sprite Editor, re-slice |
| All cells same tile | Priority table not wired or empty | Check Inspector assignment |
| Map appears upside down | Y-axis convention mismatch | Enable `flipY` on the sample component |
| Tile assets saved to wrong folder | Missed the save dialog | Move `.asset` files to correct `Tiles/` folder; re-assign in Inspector |
