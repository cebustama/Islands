# Map Tilemap Scene Setup Guide

Status: Active reference  
Authority: Developer workflow reference. Not a runtime SSoT.  
Scope: Setting up the PCG Map Tilemap sample scene — tilemap hierarchy, physics layers, collider integration, and player navigation.  
Home: `Documentation~/reference/` — consulted at implementation time, not a governed contract surface.

---

## Scene hierarchy

```
PCG Map Tilemap
├── Main Camera              (Camera, CameraFollow2D)
├── Player                   (Rigidbody2D, CircleCollider2D, MapPlayerController2D,
│                             SpriteRenderer, CharacterAnimator)
├── Grid
│   ├── Tilemap              (base layers — Default layer)
│   ├── Overlay Tilemap      (overlay layers — Default layer)
│   └── Collider Tilemap     (physics collider — MapCollider layer, renderer disabled)
├── PCGMapTilemapSample
└── PCGMapTilemapVisualization
```

## Tilemap layer partition (Phase H5)

| Tilemap | Layers stamped | Unity layer | Renderer |
|---------|---------------|-------------|----------|
| Tilemap (base) | DeepWater, ShallowWater, Land, LandCore, LandEdge | Default | Enabled, Order 0 |
| Overlay Tilemap | Vegetation, HillsL1, HillsL2, Stairs | Default | Enabled, Order 1+ |
| Collider Tilemap | DeepWater, ShallowWater, HillsL2 (sentinel tile) | MapCollider | **Disabled** |

The collider tilemap uses a single sentinel `TileBase` asset for all non-walkable cells.
The specific sprite on the sentinel tile does not matter — the renderer is off.

## Physics layers

Create two custom layers in **Edit → Project Settings → Tags and Layers**:

| Layer slot | Name |
|-----------|------|
| 8 (or any free) | `Player` |
| 9 (or any free) | `MapCollider` |

## Layer collision matrix

**Edit → Project Settings → Physics 2D → Layer Collision Matrix:**

| Pair | Collides? | Why |
|------|-----------|-----|
| Player ↔ MapCollider | **Yes** | Player blocked by non-walkable terrain |
| Player ↔ Default | No | Base/overlay tilemaps are visual only |
| Player ↔ Player | No | Single player; no self-collision needed |
| MapCollider ↔ MapCollider | No | Collider tilemap doesn't collide with itself |
| MapCollider ↔ Default | No | No physics interaction between tilemaps |

All other pairs involving Player or MapCollider: **disabled**.

## Collider Tilemap setup

1. Create a child of **Grid**: right-click Grid → 2D Object → Tilemap → Rectangular.
   Rename to `Collider Tilemap`.
2. Set the GameObject's **Layer** to `MapCollider`.
3. **Disable** the TilemapRenderer component (uncheck the component toggle).
4. Do **not** manually add physics components — `TilemapAdapter2D.SetupCollider()`
   auto-adds `Rigidbody2D` (Static), `TilemapCollider2D` (usedByComposite), and
   `CompositeCollider2D` when `enableColliderAutoSetup` is checked on
   `PCGMapTilemapVisualization`.

### Sentinel tile asset

Create any `TileBase` asset (Assets → Create → 2D → Tiles → Tile). Assign any sprite.
Drag into the **Collider Tile** slot on `PCGMapTilemapVisualization`.

## PCGMapTilemapVisualization wiring (H5 section)

| Inspector field | Value |
|----------------|-------|
| Enable Multi Layer | ✓ |
| Overlay Tilemap | `Overlay Tilemap` (from Hierarchy) |
| Collider Tilemap | `Collider Tilemap` (from Hierarchy) |
| Collider Tile | sentinel `TileBase` asset (from Project) |
| Enable Collider Auto Setup | ✓ |

## Player GameObject (Phase H7)

| Component | Key settings |
|-----------|-------------|
| **Transform** | Position near map centre (e.g. `32, 32, 0` for a 64×64 map) |
| **SpriteRenderer** | Order in Layer ≥ 10 (above tilemaps). Sprite assignment managed by CharacterAnimator at runtime; leave sprite field empty. |
| **Rigidbody2D** | Dynamic, Gravity Scale = 0, Freeze Rotation Z = ✓ |
| **CircleCollider2D** | Radius ≈ 0.3 |
| **MapPlayerController2D** | Move Speed = 5. **Leave Sprite Renderer field empty** — CharacterAnimator owns all visuals. |
| **CharacterAnimator** | Assign walk/idle sprites per direction (see below) |
| **Layer** | `Player` |

### CharacterAnimator setup

`CharacterAnimator` handles directional walk animation and idle frames independently from
`MapPlayerController2D`. Both scripts read `Input.GetAxisRaw` — no conflict.
`MapPlayerController2D` owns physics; `CharacterAnimator` owns visuals.

The split works because `MapPlayerController2D`'s facing logic is guarded by
`if (spriteRenderer == null) return;`. Leaving its Sprite Renderer field empty
disables the built-in facing and lets `CharacterAnimator` take full control.

**Sprite preparation:** import the character spritesheet following
`Documentation~/reference/tileset-import-guide.md` (same process: PPU = tile size,
Point filter, no compression, slice by cell size, remove background color).
Identify sub-sprite indices for the hero's walk frames and idle frames per direction.

| Inspector field | Assign |
|----------------|--------|
| `walkDown` | Down-facing walk frame sprites (typically 2) |
| `walkUp` | Up-facing walk frame sprites (typically 2) |
| `walkLeft` | Left-facing walk frame sprites (typically 2) |
| `walkRight` | Right-facing walk frame sprites (typically 2; may reuse left + flipX) |
| `idleDown` | Single down-facing idle sprite |
| `idleUp` | Single up-facing idle sprite |
| `idleLeft` | Single left-facing idle sprite |
| `idleRight` | Single right-facing idle sprite |

**File location:** `Runtime/PCG/Samples/PCG Map Tilemap/CharacterAnimator.cs`
(under `Islands.PCG.Samples` namespace, covered by `Islands.PCG.Samples.asmdef`).

### Camera

| Component on Main Camera | Key settings |
|-------------------------|-------------|
| **CameraFollow2D** | Target = Player Transform; Smooth Time = 0.1 |
| **Camera** | Orthographic; Size ≈ 8–12 to taste |

## Smoke-test checklist

| # | Test | Pass condition |
|---|------|---------------|
| 1 | Walk on land | Free movement on all land cells |
| 2 | Walk into water | Blocked at DeepWater and ShallowWater edges |
| 3 | Walk into mountain | Blocked at HillsL2 boundary |
| 4 | Diagonal wall slide | Smooth sliding, no corner-catching |
| 5 | Camera follow | Camera tracks player smoothly |
| 6 | Seed change | Map + colliders regenerate; new obstacles respected |
| 7 | Walk animation | Sprite cycles through walk frames while moving |
| 8 | Idle animation | Sprite shows idle frame in last-moved direction when stopped |
| 9 | Directional facing | Walk sprites change when changing direction (up/down/left/right) |

### Failure triage

| Symptom | Likely cause |
|---------|-------------|
| Player falls off screen | Rigidbody2D GravityScale ≠ 0 |
| Player passes through everything | Layer collision matrix not wired, or Collider Tilemap on wrong layer |
| Player blocked everywhere | CircleCollider2D radius > 0.5, or base Tilemap has a collider component |
| Player spawns inside water | Starting position on a water cell — reposition to land |
| Player invisible | SpriteRenderer Order in Layer too low, or no sprites assigned in CharacterAnimator |
| Camera static | CameraFollow2D target not assigned |
| No animation, static sprite | CharacterAnimator walk arrays empty, or CharacterAnimator component missing |
| Sprite faces wrong direction | Walk sprite arrays assigned to wrong direction fields |
| Sprite flips unexpectedly | MapPlayerController2D Sprite Renderer field not empty — clear it so CharacterAnimator has sole control |
