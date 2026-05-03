# Tileset Import Guide

Status: Active reference  
Authority: Developer workflow reference. Not a runtime SSoT.  
Scope: Converting tileset PNG files into Unity TileBase assets for use with `TilemapAdapter2D`,
including biome-conditional tile art setup (Phase Q).  
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
7. Wire into TilesetConfig SO → assign to PCGMapTilemapVisualization
8. (Phase Q) Wire biome-specific tiles into BiomeTileOverride SO → assign to component
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

**Naming convention for biome-specific tiles (Phase Q):**
When tiles will be used in a `BiomeTileOverride`, name them `Tile_<Biome>_<Layer>`:
e.g. `Tile_Snow_Land`, `Tile_Boreal_Vegetation`, `Tile_Desert_Land`.
This makes override slot assignment unambiguous in the Inspector.

---

### Phase 6 — Wire into TilesetConfig

Create or select a `TilesetConfig` asset (Create → Islands → PCG → Tileset Config).
In the Inspector, populate the **layers** array (low → high priority):

| Slot | `layerId` | Tile asset | Visual role |
|------|-----------|-----------|-------------|
| 0 | `DeepWater` | `Tile_DeepWater` | Open ocean |
| 1 | `Lakes` | `Tile_Lakes` | Inland water bodies |
| 2 | `MidWater` | `Tile_MidWater` | Intermediate depth |
| 3 | `ShallowWater` | `Tile_ShallowWater` | Coastal water ring |
| 4 | `Land` | `Tile_Land` | Grass / ground |
| 5 | `LandInterior` | `Tile_LandInterior` | Interior tint |
| 6 | `LandCore` | `Tile_LandCore` | Deep interior tint |
| 7 | `Vegetation` | `Tile_Vegetation` | Forest |
| 8 | `HillsL1` | `Tile_Hills` | Rolling hills |
| 9 | `HillsL2` | `Tile_Mountains` | Mountain peaks |
| 10 | `Rivers` | `Tile_Rivers` | Rivers |
| 11 | `Stairs` | `Tile_Pass` | Mountain pass / steps |
| 12 | `LandEdge` | `Tile_Beach` | Sandy shoreline |

Set **Fallback Tile** to `Tile_DeepWater` to catch any cells that don't match a layer entry.

Later entries overwrite earlier ones when a cell belongs to multiple layers simultaneously,
so place the most visually prominent terrain types last (highest priority).

Assign the `TilesetConfig` asset to the **Tileset Config** field on your
`PCGMapTilemapVisualization` component.

---

### Phase 7 — Wire biome-specific tile overrides (Phase Q)

The `BiomeTileOverride` ScriptableObject layers biome-specific tile art on top of the base
`TilesetConfig`. It is purely additive — any (biome, layer) pair not listed in the override
falls through silently to the base tile.

**Fallback chain per cell:** override tile → base TilesetConfig tile → fallbackTile → null

**When to use:** When you want different tile art per biome (snow ground, desert sand, jungle
canopy) without duplicating the entire `TilesetConfig` for each biome.

**Creating the asset:**

1. Right-click in Project → **Create → Islands → PCG → Biome Tile Override**.
   Name it `<SetName>_BiomeOverride` (e.g. `Overworld16bit_BiomeOverride`).
2. In the Inspector, right-click the asset header → **Populate Default Biome Groups**.
   This creates 12 groups (one per biome except Unclassified) with 4 recommended layer
   slots each (Land, Vegetation, HillsL1, HillsL2), all tile references null.
3. For each biome you want to customize, expand its group and drag tile assets into
   the layer slots.
4. Assign the `BiomeTileOverride` asset to the **Biome Tile Override** field on the
   `PCGMapTilemapVisualization` component (appears directly below Tileset Config).

**Key behaviors:**
- Null override = identical output to pre-Q (zero regression risk).
- Empty slot = silent fallthrough to base TilesetConfig tile for that layer.
- Tile priority within each slot: ruleTile > animatedTile > tile (H6 convention).
- Duplicate biome groups: last-wins by array order.
- Override is ignored when `useProceduralTiles = true`.
- Override is ignored when `enableBiomeStage = false` (all cells read as Unclassified,
  no override matches, all tiles come from base TilesetConfig).

---

## Biome × Layer override matrix

12 biomes × 6 recommended override layers = 72 possible slots.
You do NOT need to fill all 72 — any empty slot silently falls through to base TilesetConfig.

### Legend

- **●** = High-value override (visually distinctive, recommended)
- **○** = Nice-to-have (adds variety, not essential)
- **—** = Low value (layer rarely appears in this biome, or biome-neutral)

### Cold biomes (low temperature)

| Biome (id)         | Land | Vegetation | HillsL1 | HillsL2 | LandInterior | LandCore |
|---------------------|------|------------|---------|---------|--------------|----------|
| Snow (1)            | ●    | ○          | ●       | ●       | ○            | —        |
| Tundra (2)          | ●    | ●          | ○       | ○       | —            | —        |
| BorealForest (3)    | ○    | ●          | ○       | ○       | —            | —        |

Visual intent: Snow → white/ice ground, snowy peaks, sparse icy vegetation.
Tundra → grey-brown earth, lichen/moss patches, rocky hills.
Boreal → dark soil, pine/conifer trees, dark rocky hills.

### Temperate biomes

| Biome (id)              | Land | Vegetation | HillsL1 | HillsL2 | LandInterior | LandCore |
|--------------------------|------|------------|---------|---------|--------------|----------|
| TemperateDesert (4)      | ●    | ○          | ○       | —       | —            | —        |
| Shrubland (5)            | ○    | ●          | —       | —       | —            | —        |
| TemperateForest (6)      | ○    | ●          | ○       | —       | —            | —        |
| TemperateRainforest (7)  | ○    | ●          | ○       | —       | —            | —        |

Visual intent: Temperate Desert → tan/beige earth, scrub brush.
Shrubland → olive ground, low bushes. Temperate Forest → green grass, broadleaf trees.
Temperate Rainforest → dark green ground, dense lush trees.

### Hot biomes (high temperature)

| Biome (id)                 | Land | Vegetation | HillsL1 | HillsL2 | LandInterior | LandCore |
|-----------------------------|------|------------|---------|---------|--------------|----------|
| SubtropicalDesert (8)       | ●    | ○          | ●       | ○       | —            | —        |
| Grassland (9)               | ●    | ○          | —       | —       | —            | —        |
| TropicalSeasonalForest (10) | ○    | ●          | —       | —       | —            | —        |
| TropicalRainforest (11)     | ○    | ●          | ○       | —       | —            | —        |

Visual intent: Subtropical Desert → yellow sand, cacti, sandstone hills.
Grassland → golden/savanna grass. Tropical Seasonal → warm green, mixed canopy.
Tropical Rainforest → deep green, dense jungle canopy.

### Special

| Biome (id)   | Land | Vegetation | HillsL1 | HillsL2 | LandInterior | LandCore |
|---------------|------|------------|---------|---------|--------------|----------|
| Beach (12)    | ●    | —          | —       | —       | —            | —        |

Visual intent: Sand/shore tile for LandEdge cells classified as Beach.

### Unclassified (0) — NOT overridden

Water cells and cells with no biome assignment. Falls through to base TilesetConfig
(DeepWater, ShallowWater, MidWater tiles). No override needed or expected.

### Minimum tiles for a smoke test

To prove the system works, assign tiles to **3 distinct biome-layer pairs** from visually
different climate bands:

| Biome               | Layer      | What to look for                      |
|---------------------|------------|---------------------------------------|
| Snow (1)            | Land       | White/snowy ground in cold regions    |
| SubtropicalDesert (8) | Land    | Sandy/yellow ground in hot regions    |
| TemperateForest (6) | Vegetation | Deciduous tree in temperate regions   |

Cross-reference against the Biome scalar overlay to confirm cells switch correctly.

### Layers NOT recommended for biome override

These layers are biome-independent and should stay in the base TilesetConfig only:

| Layer         | Reason                                        |
|---------------|-----------------------------------------------|
| DeepWater     | All water is Unclassified — override never matches |
| MidWater      | Same as DeepWater                             |
| ShallowWater  | Same as DeepWater                             |
| Lakes         | Unclassified (inland water body)              |
| Rivers        | Unclassified (water cell)                     |
| Walkable      | Informational layer, usually no tile          |
| Paths         | Reserved (Phase O), no tile yet               |
| Stairs        | Structural (mountain passes), biome-neutral   |
| LandEdge      | Biome-neutral highlight (except Beach)        |

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
| All cells same tile | Priority table not wired or empty | Check TilesetConfig Inspector assignment |
| Map appears upside down | Y-axis convention mismatch | Enable `flipY` on the component |
| Tile assets saved to wrong folder | Missed the save dialog | Move `.asset` files to correct `Tiles/` folder; re-assign in Inspector |
| Biome override has no visible effect | `enableBiomeStage` is off | Enable the Biome stage toggle |
| Biome override has no visible effect | `useProceduralTiles` is on | Disable procedural tiles (override only works with TilesetConfig) |
| Biome override has no visible effect | Override asset not assigned | Drag the BiomeTileOverride SO into the component's field |
| Override affects wrong cells | Biome field mismatch | Enable the Biome scalar overlay to verify biome boundaries |
| Changing override tile doesn't regenerate | Dirty tracking miss | Modify any tunable to force regeneration, or toggle a stage off/on |
