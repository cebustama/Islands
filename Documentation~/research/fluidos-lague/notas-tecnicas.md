# Sebastian Lague — "Fluid Simulations" playlist: technical notes

Notes written 2026-11-05 from the YouTube transcript panel (English captions, uploader-provided for video 1) plus, where marked **[code]**, the source on GitHub. Everything not marked [code] comes from the video narration. Minute marks are video timestamps (mm:ss). These are paraphrased notes, not a transcript.

---

## 1. Coding Adventure: Simulating Fluids (rSKMYc1CQHE, 47:52)

**Source:** full transcript (English UK captions, clearly hand-written; 704 segments). Code read from `github.com/SebLague/Fluid-Sim`, branch `Episode-01`: `Assets/Scripts/Sim 2D/Simulation2D.cs`, `Compute/FluidSim2D.compute`, `Compute/FluidMaths2D.hlsl`, `Compute/SpatialHash.hlsl`. Engine: Unity, C# + HLSL compute.

**Chapters:** 0:00 Intro · 0:21 Gravity and Collisions · 2:35 Smoothed Particles · 3:55 Calculating Density · 7:50 The Interpolation Equation · 12:00 Gradient Calculations · 15:35 The Pressure Force · 19:15 Trying to Make it Work... · 22:05 Optimizing Particle Lookups · 26:35 Spatial Grid Code · 27:45 Position Predictions · 30:30 Mouse Force · 33:20 Artificial Viscosity · 36:50 Pressure Problems · 39:50 Bugs · 41:05 Parallel Sorting · 43:20 Some Tests and Experiments · 44:45 The Third Dimension · 47:20 Outro.

Framing (2:45): the goal is "a rough starting point", not an accurate simulation. The method is SPH (Smoothed Particle Hydrodynamics), introduced in the 1970s for astrophysics.

### Step 1 — One particle, gravity, box collisions (0:21–1:40)
- State: `position`, `velocity`, global `gravity`. Per step: `velocity += down * gravity * dt; position += velocity * dt`.
- `ResolveCollisions`: if the particle is outside an axis-aligned box (half size h), clamp the position to the edge and flip that velocity component, multiplied by `collisionDamping` in [0,1] (energy lost per bounce).
- [code] 2D version, box centred at origin:
  ```hlsl
  float2 halfSize = boundsSize * 0.5;
  float2 edgeDst = halfSize - abs(pos);
  if (edgeDst.x <= 0) { pos.x = halfSize.x * sign(pos.x); vel.x *= -1 * collisionDamping; }
  if (edgeDst.y <= 0) { pos.y = halfSize.y * sign(pos.y); vel.y *= -1 * collisionDamping; }
  ```
  Default `collisionDamping = 0.95` in the C# inspector. There is also a test obstacle box: if inside it, push out along the axis with the smaller penetration (`obstacleEdgeDst.x < obstacleEdgeDst.y`).
- 1:41: one particle is not a fluid → arrays of positions/velocities, initialised in a grid (size and spacing tweakable). 2:10 problem: particles all collapse on top of each other → need a force pushing them apart.

### Step 2 — Density via a smoothing kernel (3:55–7:50)
- Idea (3:05): we can never have enough particles, so we "blur" each particle into a continuous field. Each particle has a **smoothing radius** h: maximum influence at the centre, zero at distance h.
- Candidate kernels, with h = 1 (4:33):
  1. `max(0, h - r)` — linear, not smooth.
  2. `max(0, h - r)^3` — eases out to zero.
  3. `max(0, h^2 - r^2)^3` — smooth at both ends (this is the poly6 shape). Chosen for now.
- Density at a point x: `ρ(x) = Σ_j m_j · W(|x - x_j|, h)`, mass m = 1 for all particles (5:14).
- Test (5:40): uniform grid, radius 0.5 → density ≈ 0.48; squeezing particles raises it. **Bug:** increasing h made density shoot up, but for a uniform grid it should not change.
- **Fix: normalise the kernel** (6:23–7:30). Summing kernel heights over a grid of points approximates the kernel's volume (integral). For density to be independent of h, the kernel's volume must be constant (=1). Volume of `(h^2 - r^2)^3` over the 2D disc = `π h^8 / 4` (computed with Wolfram). Divide by it → `W_poly6(r) = 4/(π h^8) · (h^2 - r^2)^3`. After that the test density stays at 187 as h changes (until h is too small to contain enough particles).

### Step 3 — The SPH interpolation equation (7:50–11:55)
- Liquids are practically **incompressible** (8:13): density should be equal everywhere, so particles must move from high-density to low-density regions.
- Detour with an abstract scalar function f(x,y) sampled at random particle positions ("particle properties" array), then reconstructed. First attempt `A(x) = Σ A_j m_j W` exaggerates values, especially where particles are dense. Fix: divide each contribution by the particle's density. The core SPH equation (11:07):
  `A(x) = Σ_j A_j · (m_j / ρ_j) · W(|x - x_j|, h)`
- Sanity check (11:35): substituting A = ρ cancels ρ_j and gives back `ρ(x) = Σ m_j W` — consistent.

### Step 4 — Gradients (12:00–15:35)
- Need the direction in which a property changes fastest (to push density down its gradient).
- Naive: finite differences, step size ε along x and y using `CalculateProperty`: `∇A ≈ ((A(x+ε,y)-A)/ε, (A(x,y+ε)-A)/ε)`. Visualised as arrows; correct but took **~20 s** for the grid of arrows.
- Analytic: for one particle, the gradient points along the line to/from it; magnitude = slope of the kernel at that distance. So
  `∇A(x) = Σ_j A_j (m_j/ρ_j) · W'(|x - x_j|) · dir_j`, with `dir_j = (x_j - x)/|x_j - x|` and a sign fix (he "stuck a minus sign in", 14:35; trial-and-error). Time 20 s → ~5 s.
- 15:02: still calling `CalculateDensity` (O(N)) inside the loop → **cache densities** in an array once per step. 5 s → **18 ms**.

### Step 5 — Pressure force (15:35–19:15)
- Parameters: `targetDensity`, `pressureMultiplier`. Equation of state:
  `P = (ρ - ρ_0) · k` — he notes this is closer to gas behaviour but popular for simplicity (16:10).
  [code] `float PressureFromDensity(float density) { return (density - targetDensity) * pressureMultiplier; }`
- Visualisation: 3 colours — negative pressure (under target density), positive (over), and white at the target.
- Pressure force = gradient function with A = P: `F_i = -Σ_j P_j (m/ρ_j) ∇W`. Acceleration: F = m a, but for a "tiny volume of fluid" divide by **density**, not mass: `a_i = F_i / ρ_i` (17:31). Then `v += a·dt`.
- **Bug (18:01): NaN everywhere.** The particle itself is included in the loop → distance 0 → division by zero for the direction. Fix: pass the particle index and skip `j == i`; if two different particles share a position, use a random direction ([code] uses `float2(0,1)` when `dst == 0`).
- **Bug (18:50): particles got denser** → another sign flip.
- 19:07: still lots of red (over-dense).

### Step 6 — Debugging stability (19:15–22:05)
- Experiment: assign acceleration directly to velocity (no inertia) and crank the multiplier, to see the pure pressure response. Particles still sit very close together.
- Reason (19:54, from a paper): the poly6 slope becomes shallow as r → 0, so close particles barely repel. Fix: switch the pressure kernel to a **spiky** kernel whose slope is steepest at 0. He redid the volume and derivative calculations. 20:25: much better with a high multiplier.
- Back to proper inertia (20:44): ok initially, but it gets worse over time.
- **Newton's 3rd law / symmetric pressure (21:13):** the force between i and j must be equal and opposite. Simple version: use the **shared pressure** `(P_i + P_j)/2`. Big improvement ("maybe that Newton guy was onto something").

### Step 7 — Spatial grid / hashing for neighbour search (22:05–27:45)
- With a few thousand particles: **5 fps** (O(N²)).
- Grid cell size = smoothing radius h → neighbours of a point are only in the 3×3 block of cells around it (3×3×3 in 3D).
- Per-cell growable lists are not GPU-friendly (memory must be fixed up front). He uses the approach from the NVIDIA CUDA particles paper (Simon Green), modified so grid dimensions need not be known: an **infinite hashed grid**.
- Algorithm (23:42–26:30):
  1. Array `spatialLookup` of length N (≥ particle count). For each particle i: cell = floor(pos / h); `hash = cell.x * p1 + cell.y * p2` (two primes); `key = hash % N`. Store (i, key).
  2. **Sort** the array by key → particles in the same cell become contiguous.
  3. `startIndices[key]` = first index in the sorted array whose key is `key` (computed by checking where key differs from the previous entry). Unused keys hold a sentinel.
  4. Query point: for each of the 9 cells around it, compute key, jump to `startIndices[key]`, iterate while the key matches, then do the distance check `|x_j - x|² ≤ h²`. Hash collisions (different cells → same key) only cost extra distance checks; that is "the price of an infinite grid with non-infinite memory" (26:19).
- [code] Episode-01 GPU version stores `uint3(index, hash, key)` and also checks the full hash, skipping entries whose hash differs (removes most collision cost):
  ```hlsl
  static const uint hashK1 = 15823;
  static const uint hashK2 = 9737333;
  int2 GetCell2D(float2 p, float r) { return (int2)floor(p / r); }
  uint HashCell2D(int2 cell) { cell = (uint2)cell; return cell.x * hashK1 + cell.y * hashK2; }
  uint KeyFromHash(uint hash, uint tableSize) { return hash % tableSize; }

  // neighbour loop (same in density, pressure and viscosity kernels)
  for (int i = 0; i < 9; i++) {
      uint hash = HashCell2D(originCell + offsets2D[i]);
      uint key  = KeyFromHash(hash, numParticles);
      uint currIndex = SpatialOffsets[key];          // start index; = numParticles when empty
      while (currIndex < numParticles) {
          uint3 indexData = SpatialIndices[currIndex++];
          if (indexData[2] != key) break;            // left this bucket
          if (indexData[1] != hash) continue;        // hash collision, other cell
          uint j = indexData[0];
          float2 off = PredictedPositions[j] - pos;
          float d2 = dot(off, off);
          if (d2 > sqrRadius) continue;
          ... // accumulate
      }
  }
  ```
  The `UpdateSpatialHash` kernel resets `SpatialOffsets[id] = numParticles` and writes `SpatialIndices[id] = uint3(id, hash, key)`, then `GPUSort.SortAndCalculateOffsets()` sorts and fills the offsets.
- Result (27:48): **5 fps → 120 fps** (CPU still).

### Step 8 — Predicted positions (27:48–30:30)
- Problem: chaotic start. Clunky workaround: start with a low pressure multiplier and ramp it up.
- Better (28:21, idea "read about"): compute densities and pressure forces at **predicted positions** `x* = x + v·Δt` instead of current positions; lets particles react to upcoming situations and partly compensates for discrete time. "Pretty massive difference" (29:01).
- Visualisation: colour by speed with a gradient (blue slow → red fast); controls for pause, frame step, reset.
- **Timestep inconsistency (29:41):** a grid of sims from 60 to ~1000 steps/s: smaller Δt = shorter look-ahead = particles settle more slowly. Fix (30:12): use a **constant look-ahead factor** instead of Δt — "feels a bit wrong", but behaviour becomes consistent. [code] `const float predictionFactor = 1 / 120.0; PredictedPositions[i] = Positions[i] + Velocities[i] * predictionFactor;` (applied after external forces were added to velocity).

### Step 9 — Mouse interaction (30:30–33:20)
- Pull particles toward the mouse within a radius (LMB), push with negative strength (RMB).
- [code] implemented as a modification of the external acceleration:
  ```hlsl
  // inside radius:
  float edgeT = dst / interactionInputRadius; float centreT = 1 - edgeT;
  float gravityWeight = 1 - (centreT * saturate(strength / 10));
  accel = gravity * gravityWeight + dirToCentre * centreT * strength;
  accel -= velocity * centreT;   // damping near the centre
  ```
- 31:00: clearly not incompressible (compresses/expands), but density evens out over time. Gravity back on, target density raised, pressure multiplier raised. 32:25: "all we have really done is make a bunch of points that don't like to be too close together, but not too far apart either" — fluid-like behaviour *emerges* from that.

### Step 10 — Artificial viscosity (33:20–36:50)
- Problems seen: particles squeezed along the walls leave a gap (boundary problem, not solved in this episode); chaotic velocities in fast regions.
- He shows the incompressible **Navier–Stokes** equations (33:50): `∇·u = 0` (density constant) and `ρ Du/Dt = -∇p + μ∇²u + f`. Pressure gradient and external forces (gravity, mouse) are done; the viscosity term blurs velocities of nearby fluid together.
- Simple approach instead of the Laplacian: for each neighbour, add `(v_j - v_i) · W_visc(r)` → velocities drift toward neighbours'. Kernel = the original smooth poly6.
  [code] `viscosityForce += (neighbourVelocity - velocity) * ViscosityKernel(dst, h); ... Velocities[i] += viscosityForce * viscosityStrength * deltaTime;`
- Values: 5 → weird clumping (particles match velocities exactly); **0.5** looks good; comparison grid with gravity; he keeps it low to not smooth away detail.

### Step 11 — Pressure problems: clumping droplets → near-pressure (36:50–39:50)
- Lifting fluid and dropping it splits it into tiny droplets of a few particles. Clamping pressure ≥ 0 removes the attraction but particles then rain down individually: negative pressure was acting as a crude **surface tension**, so he keeps it.
- Fix from Clavet et al. 2005 ("double density relaxation"): a second, purely repulsive **near density** computed with an even spikier kernel, `nearPressure = nearPressureMultiplier · nearDensity` (no target). Negative multiplier → collapse; positive → the blob holds its shape (38:25).
- Jelly-like bouncing remains (38:51; e.g. changing target density at rest). Raising the pressure multiplier (= **stiffness constant**) to **1000** settles faster, but bigger forces need more substeps; removing one iteration per frame brings back chaos (39:40).

### Step 12 — Compute shader port, bitonic sort (39:50–43:20)
- GPU port: "~500 fps" with the initial count (40:59). Hardest part was replacing `Array.Sort` in the neighbour search with a parallel sort: **bitonic merge sort** (sorting network; each stage compares/swaps fixed pairs in parallel; CPU loops over stages and dispatches; GPU kernel computes which pair a thread owns, compares, swaps). Shown with 16 inputs, sorting high to low.
- [code] simulation step order (`Simulation2D.RunSimulationStep`), all dispatches with 64 threads/group:
  1. `ExternalForces`: `v += (gravity + mouse) · dt`; `predicted = x + v / 120`.
  2. `UpdateSpatialHash` (on predicted positions).
  3. `gpuSort.SortAndCalculateOffsets()`.
  4. `CalculateDensities` → `float2(density, nearDensity)` per particle.
  5. `CalculatePressureForce` → `v += (F/ρ) dt`.
  6. `CalculateViscosity` → `v += F_visc · strength · dt`.
  7. `UpdatePositions` → `x += v·dt`, then collisions.
- [code] time stepping: `timeStep = frameTime / iterationsPerFrame * timeScale`; `iterationsPerFrame` substeps per frame; optional fixed timestep 1/60 s; skips the first 10 frames when variable (large initial deltaTime).

### [code] Kernels used in Episode 01 (2D) and their normalisation
Spiky kernels are of form (h - r)^n; scaling constants set from C# each frame:

| Function | Formula (r < h) | Scale (2D) | Used for |
|---|---|---|---|
| `SmoothingKernelPoly6` | (h²-r²)³ | 4 / (π h⁸) | viscosity |
| `SpikyKernelPow2` | (h-r)² | 6 / (π h⁴) | density |
| `SpikyKernelPow3` | (h-r)³ | 10 / (π h⁵) | near density |
| `DerivativeSpikyPow2` | -(h-r) | 12 / (π h⁴) | pressure gradient |
| `DerivativeSpikyPow3` | -(h-r)² | 30 / (π h⁵) | near-pressure gradient |

Final pressure loop [code]:
```hlsl
float sharedPressure     = (pressure + neighbourPressure) * 0.5;
float sharedNearPressure = (nearPressure + neighbourNearPressure) * 0.5;
pressureForce += dirToNeighbour * DensityDerivative(dst, h)     * sharedPressure     / neighbourDensity;
pressureForce += dirToNeighbour * NearDensityDerivative(dst, h) * sharedNearPressure / neighbourNearDensity;
...
float2 acceleration = pressureForce / density;
Velocities[i] += acceleration * deltaTime;
```
Note: `dirToNeighbour = (x_j - x_i)/r` and the derivative is negative, so the product points away from the neighbour when shared pressure > 0. Mass is implicitly 1. Default inspector `smoothingRadius = 2` in code (scene values not read).

### Step 13 — Tests (43:20–44:45)
- 100 particles: cute wobbling droplets. 100,000: "my computer was not happy". **~40,000** runs okay, with weird tendrils (parameters finicky). Obstacle with a gap underneath: water levels equalise on both sides (communicating vessels) — not entirely unrealistic.

### Step 14 — 3D (44:45–47:20)
- float2 → float3; new kernel scale factors for 3D; neighbour search over 3×3×3 = 27 cells; collision box can be moved/rotated/scaled: transform positions and velocities into the box's local space, resolve as before, transform back. First result goopy; after tweaking acceptable. Because the hashed grid is unbounded, the box can be stretched arbitrarily.
- Wishlist (46:51): more stable and performant (more particles), less finicky parameters, better boundary behaviour, **boats bobbing on the water** (two-way object coupling), better rendering (raymarching, done in video 2).

### Key concepts (video 1)
SPH; smoothing kernel and normalisation by its volume; SPH interpolation `A = Σ A_j m_j/ρ_j W`; analytic gradient via kernel derivative; equation of state `P = k(ρ-ρ0)`; acceleration = F/ρ; symmetric (shared) pressure; spiky vs poly6 kernels; near density / near pressure (Clavet 2005); predicted positions with fixed look-ahead; XSPH-like artificial viscosity; infinite hashed grid with sort + start indices; bitonic merge sort on GPU; substeps vs stiffness trade-off.

### Performance (video 1)
Finite-difference gradient grid ~20 s → analytic 5 s → cached densities 18 ms. Few thousand particles O(N²): 5 fps → spatial hash 120 fps (CPU). GPU: ~500 fps; ~40k particles OK; 100k too slow.

### References (video 1)
Müller, Charypar, Gross 2003, *Particle-Based Fluid Simulation for Interactive Applications* (SCA); Clavet, Beaudoin, Poulin 2005, *Particle-based Viscoelastic Fluid Simulation* (double density relaxation, near pressure); Koschier et al., *SPH Techniques for the Physics Based Simulation of Fluids and Solids* (Eurographics tutorial); Simon Green, NVIDIA, *Particle Simulation using CUDA* (grid via sort + cell start); bitonic sort (Batcher).

---

## 2. Coding Adventure: Rendering Fluids (kOkfC5fLfgE, 58:41)

**Source:** full transcript (English UK captions, hand-written quality; 868 segments). Code not read (same Fluid-Sim repo, later branch). Description: make the particle fluid *look* like a fluid; several techniques plus a simple foam/spray simulation.

**Chapters (approx. start, from transcript headers):** 0:00 Intro · 0:22 Marching Cubes · 5:57 Raymarching · 10:38 Refract, Reflect, Fresnel · 13:33 More Raymarching · 18:28 A Simple Environment · 20:40 Testing the Raymarched Fluid · 25:03 Screen Space Technique: Depth Map · 28:33 Simple Smoothing · 33:40 Surface Smoothing · 36:42 Rendering the Fluid · 41:58 Spray, Foam, and Bubbles · 55:15 Testing the Screen Space Fluid.

Three rendering approaches tried in order: (A) marching cubes mesh of a density volume, (B) raymarching the density volume with refraction/reflection, (C) screen-space fluid rendering from particle billboards (GDC slides, Simon Green, NVIDIA). Then (D) a white-water particle system (Ihmsen et al. 2012).

### A. Density volume + marching cubes (0:22–5:57)
- **Density texture (0:40):** a 3D texture; at the end of each frame a compute pass samples the SPH density at regular grid points and writes it (same density function as the sim). A viewer shows a slice with adjustable depth.
- Marching cubes (reused from his terrain episodes) extracts the iso-surface at a threshold. Initial resolution **30×18×18**, then doubled. Threshold ~**700** shows only the densest regions; ~0 is chunky; **~70** chosen (3:17). Density is not uniform because the sim is not truly incompressible.
- Closed sides (2:11): in the density sampling helper, if the coordinate is on the edge of the map, return a value below threshold ("lie about the density") so the surface closes against the walls.
- **Bug (3:28):** doubling resolution again made the simulation explode, because the frame rate dropped (sim timestep tied to frame time). Cause: GPU marching cubes, then reading the triangle buffer back to the CPU to build a Mesh, then uploading it again.
- **Fix, GPU-only draw (3:58):** bind the marching-cubes triangle (append) buffer to a material with a custom shader, put the triangle count in an indirect **render-arguments** buffer, issue an indirect procedural draw; the vertex shader uses the vertex index (`SV_VertexID`) to read its vertex from the buffer. Second bug: vertices were (position, normal) pairs, not just positions → fix the struct/stride (5:04).
- Rejected (5:23): a mesh is only a "shell"; making it look like a volume would need ray tracing the mesh with a BVH rebuilt every frame on the GPU, a project of its own.

### B. Raymarching the density volume (5:57–25:03)
- **Basic (6:19):** post-processing shader. Per pixel: view ray; ray–AABB intersection with the simulation bounds for entry/exit; march with a fixed `stepSize`; accumulate `opticalDepth += density(p) * stepSize * densityMultiplier`. All white at first → lower `densityMultiplier`; visible slicing → smaller step.
- **Atmosphere-style single scattering (7:26):** at each step, sunlight scatters toward the camera in proportion to local density and per-channel (RGB) scattering coefficients; phase function ignored. Light reaching the camera is attenuated by **Beer–Lambert transmittance** `T = exp(-σ · ∫ρ ds)`. Then also attenuate the sunlight reaching each sample by marching toward the sun with a much larger step (8:57). Result: a cloudy, misty cube with coloured fringes; raising red scattering gives reddish thin edges and blue/green through thick parts (9:59). Ethereal, but not water.
- **Refraction (10:38):** Snell's law `n_1 sin θ_1 = n_2 sin θ_2` (first in terms of speeds: ~299 M m/s air vs ~225 M m/s water, i.e. n ≈ 1.33). First an angle-based 2D function; replaced by the standard 3D vector refract formula. Water→air bends away from the normal; past the critical angle → **total internal reflection** (12:29). Reflection `r = d - 2(d·n)n`.
- **Fresnel (12:51):** copied the Fresnel equations (not derived); reflectance R in [0,1], higher at grazing angles; refracted fraction 1 - R. Gives a crossfade instead of an abrupt jump. (Tutorial writer may use exact dielectric Fresnel or Schlick.)
- **Multi-bounce raymarcher (13:38):** loop over `numRefractions`:
  1. march to the next **surface** (air↔fluid boundary, where density crosses a threshold / `densityOffset`), accumulating density → reduce transmittance;
  2. normal at the surface (from the density gradient), reflect/refract directions and R;
  3. follow only the "more interesting" path, scored by fraction of light × how much fluid lies in that direction (a density march). The other path is approximated: `light += Light(dir) * transmittanceAlong(dir) * fraction`, no further bounces;
  4. after the last iteration, approximate the remaining light the same way.
- Bugs: GPU crash (15:25); a faulty surface-finding function (two fix attempts, 15:54). Debug views: normals per iteration; green = took refraction, blue = took reflection (16:25). First iteration almost all refraction (reflections escape into empty space) except droplet undersides; second iteration has more reflection (internal reflection off the far side; strong at grazing angles along the floor).
- Parameters: index of refraction (near 1 → nearly invisible; higher → stronger bending), colours, `densityOffset` (sets droplet size; too far → fluid disappears).
- **Test environment (18:28):** floor = ray–box hit, 4 coloured quadrants (to see where reflections come from), checkerboard with dark-tile factor, tile scale, random hue/value jitter per tile; a test cube inside the water with a hard shadow on the floor (ray from floor to sun); simple sky from his ray-tracing episode; movable sun. `Light()` now samples this environment. Ray inside fluid hitting the floor/cube (diffuse): he just breaks out of the loop. Fluid shadow on the floor: march density from the floor point toward the sun → transmittance.
- Results (20:48): `numRefractions` 1, 2, 3 improve; 4 gives diminishing returns. Colour comes mostly from the environment → watery. Chopstick in a real glass confirms refraction direction (22:14). Two-cube spawn (bug: particle count split wrong → tiny puddle; fixed). Droplets show upside-down world copies; step-size artifacts close up (23:26).
- **Performance:** with the camera underwater (screen fully covered) it is too slow and the sim becomes unstable (frame-time coupling); works in a smaller window (24:07). Underwater: reflections off the underside, and a region overhead where you can see out (low reflectance, Snell's window) (24:36).

### C. Screen-space fluid rendering (25:03–41:58)
Following Simon Green's GDC 2010 slides "Screen Space Fluid Rendering for Games".
- **Billboards (25:54):** each particle as a camera-facing quad instead of a 128-triangle sphere. Vertex shader: centre from the simulation position buffer; `worldPos = centre + camRight * offset.x * size + camUp * offset.y * size`. Four attempts to get the math right (26:22–26:48). **1 million particles drawn** without trouble; the simulation is the bottleneck (26:59). He mentions simulation optimisations from viewer suggestions (no detail).
- Fragment: offset from quad centre in UV; `discard` if length > 1 → circles (27:27).
- **Depth pass (27:45):** output distance camera→fragment → depth map. Holes between particles → bigger particles (blobbier) + blur.
- **Blur experiments on a test image (28:35):** box blur (boxy) → Gaussian `w = exp(-d²/(2σ²))`; σ derived from the blur radius times a `smoothness` factor (1 = good default) so it adapts to radius (29:42). ~3 fps → **separable** blur (horizontal then vertical) → ~150 fps (30:32).
- On the depth map (30:38): blur ate edges because empty pixels have infinite depth → ignore invalid samples. Then foreground droplets melted into the background.
- **Normals from depth (31:19):** reconstruct view-space position at the pixel and at +1 px in x and y; `n = normalize(cross(dx, dy))`. Bug: discarding invalid pixels broke it → return 0 instead (32:02).
- **Spherical depth (32:14):** in the billboard fragment, `z = sqrt(1 - x² - y²)` (unit radius) subtracted from depth → spheres. Writing it as colour does not affect depth testing → write `SV_Depth`; still wrong because the engine's depth buffer is non-linear and reversed on some platforms → convert linear eye depth to device depth with a helper (33:03–33:25).
- **Bilateral ("surface") blur (33:40):** weight = spatial Gaussian × depth weight, `w_depth(Δz)` ≈ 1 for small depth differences, → 0 for large (Gaussian in Δz with a `depthFactor`). Dragons test: objects separate while their interiors blur; higher depth factor keeps more detail. Separable trick is no longer exact for a bilateral filter; slides say artifacts are minor. Alternatives mentioned: **screen-space curvature flow** (van der Laan et al. 2009) and the **narrow-range filter** (Truong & Yuksel 2018).
- **World-space blur radius (36:00):** fixed pixel radius blurs near things less than far ones and depends on resolution → specify radius in world units and convert to pixels using the pixel's depth (≈ `worldRadius * screenHeight / (2 · depth · tan(fov/2))`).
- **Shading test (36:44):** `dot(n, L)` remapped to [0,1] + ambient. Fine from above; artifacts and "bubbly" look at grazing angles. Future: **anisotropic kernels** (Yu & Turk 2013).
- **Composite pass (37:32):** post-process reads fluid depth; invalid → environment (the raymarching test scene). Else: surface point = camera + ray·depth; normal; reflect & refract.
- **Thickness pass (38:13):** render particles again with **additive blending** → fluid thickness per pixel. Render at reduced resolution (overdraw). Only one refraction possible. Exit point ≈ `surface + refractDir * thickness * refractionMultiplier` (tweak until it looks right); sample environment there; transmittance `exp(-thickness * extinction_rgb)`. Extinction colours: absorb blue → yellow; absorb green → reddish; absorb red → dark green-blue ocean (40:14). Final = lerp(refracted, reflected, Fresnel) (40:24).
- **Shadows (40:51):** render thickness from the sun with an **orthographic** projection (directional light); floor point → shadow camera view-projection → UV → thickness → transmittance (coloured shadows). Bug: wrong camera's projection matrix (41:30). Fluid self-shadowing skipped.

### D. Spray, foam, bubbles: white-water particles (41:58–55:15)
- Failed first idea (42:24): render particle speed into the screen-space buffers, blur, paint fast regions white → "pretty bad".
- Paper: **Ihmsen, Akinci, Akinci, Teschner 2012, "Unified spray, foam and air bubbles for particle-based fluids"** (The Visual Computer).
- **Data (43:13):** `WhiteParticle { float3 position; float3 velocity; float lifetime; }` (later + type, spawnFactor); main buffer + temporary compaction buffer; two counters: active at frame start and survivors.
- **Spawn test (43:58):** random seed from position and time; 0.01 % chance → atomically increment count, if under the max, init with random upward velocity.
- **Update kernel:** move, decrement lifetime; if alive, append to the survivor buffer (compaction keeps free slots at the end). **Copy kernel:** copy survivors back; active = survivors. Drawn with the same billboard shader, scale shrinking with remaining lifetime.
- **Trapped-air potential (45:04)**, over neighbours j within radius h:
  `I_ta(i) = Σ_j |v_ij| · (1 - v̂_ij · x̂_ij) · W(x_ij)`, `v_ij = v_i - v_j`, `x_ij = x_i - x_j`, `W` linear from 1 at r = 0 to 0 at r = h. The `(1 - dot)` factor lowers the contribution when particles move apart (no air trapped; 0 if opposite). Computed inside the pressure kernel's neighbour loop. Remapped to [0,1] with min/max params (paper: `Φ(I, τmin, τmax) = (min(I,τmax) - min(I,τmin)) / (τmax - τmin)`).
- **Kinetic energy** `E_k = ½ m |v|²`, also remapped. Debug: red = trapped air, green = kinetic; yellow overlap = where to spawn. Spawn factor = trappedAir × kinetic. (Paper's wave-crest term mentioned, not implemented, 55:00.)
- **Spawning (47:33):** `n = spawnFactor * spawnRate * dt`; spawn `floor(n)` + one more with probability `frac(n)` (frame-rate independent). Placed randomly in a small **cylinder** around the fluid particle aligned with its velocity; velocity inherited plus jitter from position in the cylinder.
- **Classification (49:10)** by fluid-neighbour count within h: many → **bubble**; very few → **spray**; otherwise **foam**. Thresholds tuned visually; debug colours red/green/blue. Bug: buffer stride not updated after adding a field (50:05).
- **Behaviour (51:06):** sum neighbour velocities during the count. Foam: velocity = average neighbour velocity (advected), lifetime ticks down (only foam dissolves). Bubble: buoyancy against gravity + acceleration toward the average fluid velocity. Spray: gravity + drag + bounds collision. Tweaks: linear → **quadratic drag** (`a = -k|v|v`); fade in spawn rate during the first ~1 s (start-up settling).
- **Foam lifetime ∝ spawn factor (53:20):** paper's cheap proxy for cluster stability. His A/B test (normal vs inverted) inconclusive.

### E. Final composite and limits (55:15–58:41)
- White particles drawn white over the screen-space fluid. **~300k fluid + 500k white particles** in real time; both doubled is at his PC's limit (55:48).
- Camera inside the fluid in the screen-space version: giant reflective balls pop in and out (smoothing limits). Not done: caustics, god rays. Future: better raymarcher or offline ray tracing.

### Key concepts (video 2)
Density splatting to a 3D texture; marching cubes with closed borders; indirect GPU draw from an append buffer; volume raymarching with Beer–Lambert transmittance, single scattering with RGB coefficients, sun-shadow march; Snell, total internal reflection, Fresnel; single-path multi-bounce refraction tracer; billboards; screen-space depth, separable Gaussian, bilateral filter, normal reconstruction, world-space filter radius; additive thickness; orthographic thickness shadow map; white-water particles (trapped air × kinetic energy, cylinder spawn, bubble/foam/spray by neighbour count, compaction buffers).

### Performance (video 2)
Marching cubes CPU readback → sim explosion (fixed with indirect draw). Gaussian 3 fps → separable 150 fps (test image). 1M billboards fine. Final 300k fluid + 500k white real time; 2× at the limit. Raymarcher too slow full-screen.

### References (video 2)
Simon Green (NVIDIA), *Screen Space Fluid Rendering for Games*, GDC 2010; van der Laan, Green, Sainz 2009, *Screen Space Fluid Rendering with Curvature Flow*; Truong & Yuksel 2018, *A Narrow-Range Filter for Screen-Space Fluid Rendering*; Ihmsen et al. 2012, *Unified Spray, Foam and Air Bubbles for Particle-Based Fluids*; Yu & Turk 2013 anisotropic kernels (future); his earlier episodes on marching cubes terrain, clouds, atmosphere, ray tracing, BVH.

---

## 3. I tried putting my fluid simulation on a planet (8nIB7e_eds4, 27:23)

**Source:** full transcript (English UK captions; 576 segments). Title and description shown in Spanish by YouTube auto-translation; original English title is likely "Coding Adventure: ... on a planet" (not verified). No code read. Same SPH sim (video 1) with screen-space rendering (video 2).

**Chapters:** 0:00 Intro · 0:24 Playing with Gravity · 3:10 Adding Simple Collisions · 4:20 Implementing Orbits · 5:36 Visualizing Tidal Forces · 7:45 Fluid Shell · 8:48 Tide Watching · 11:52 Implementing the Earth · 17:27 Oceans on Earth · 20:24 A Terraforming Experiment · 23:50 3-Body System.

### Step 1 — Position-dependent gravity (0:24–3:10)
- Gravity function originally returned a constant down vector. Now: `down = normalize(planetCentre - p)`, constant 10 m/s² first → fluid forms a mound around the centre (bug: forgot to save the file, 1:05).
- Then Newtonian magnitude: `F = G m1 m2 / r²`, with F = m a → `a = G M / r²` toward the body (own mass cancels). Planet reaction ignored (particles' mass negligible). Planet mass arbitrary **5000** → too high; tune M or G (2:08).
- Problem (2:23): point-mass singularity: particles passing near the centre get huge acceleration and are flung away between frames (no surface yet).

### Step 2 — Sphere collision (3:10–4:20)
- If `|p - c| < R`: move to the surface; remove the inward velocity component: `vn = dot(v, -n); if vn > 0: v += n * vn` (n = outward normal). Otherwise particles accumulate downward velocity and "phase through".
- Result: fluid envelops the planet, a fountain bursts on the far side, a wave sloshes back and forth until it fizzles out.

### Step 3 — N-body orbits (4:20–5:36)
- `CelestialBody { position, velocity, radius, mass }`. Each step: loop over all pairs, accumulate accelerations from `G m / r²`; then update velocities, then positions (semi-implicit Euler).
- Red planet + blue moon. To tune initial velocities he simulates a few thousand steps ahead and **draws predicted orbit paths** (5:04). Increasing the moon's mass makes the planet wobble too.

### Step 4 — Visualising tidal forces (5:36–7:45)
- Test points over the sphere surface (point-on-sphere generator); at each, arrow = moon's gravitational acceleration (padding and length scale parameters). All point to the moon, slightly shorter on the far side.
- **Tidal acceleration = point acceleration − planet's (centre) acceleration** (6:52): near side points toward the moon, far side away; elsewhere squeezed inward and toward the two ends → two bulges (two high tides).

### Step 5 — Fluid shell spawn (7:45–8:48)
- Spawning a cube takes long to settle; instead spawn in a **shell**: random points on a sphere scaled to a random radius in [minSpawnRadius, maxSpawnRadius]. **~2.2 million particles** ("not sure my computer can handle much more").

### Step 6 — Tide watching (8:48–11:52)
- Bug (9:20): rippling in the direction of planet motion → forgot initial particle velocities; they must **inherit the planet's velocity**.
- Moon mass cranked very high → giant wave chases the moon and wraps around it; the moon "sucks the planet dry" (10:25). Idea: make moon and planet from particles too (tidal disruption).
- Toned down: visible subtle bulges on both ends and low tides at the sides (planet rendered on top to judge levels). Bulges grow over time and eventually spray into space (11:30) — unresolved: sim instability or moon still too massive.

### Step 7 — Earth collider via SDF (11:52–17:27)
- Earth mesh from his older "Geographical Adventures" project (an early commit that has a single mesh, not split by country; height multiplier + base radius).
- **SDF by brute-force ray casting (13:08):** for each sample point, cast many rays in all directions, keep the nearest hit distance; sign negative if the nearest hit is a triangle backface (inside). Ray–triangle queries accelerated by the **BVH** from his ray tracing project. ~**500 rays** per point; grid **70³**; written to disk (takes a few minutes), loaded at runtime into a 3D texture via a compute shader.
- Verified with the SDF raymarcher from video 2. **Bug: floating specks** in the sky — near-parallel triangles giving wrong front/back decisions (precision). Fix: count front vs back hits over all rays and use a **majority vote** for inside/outside (15:22).
- Collision in the sim (15:43): `sdf(p)` and its gradient (direction of fastest distance increase, via finite differences); if `sdf(p) < 0`, push the particle along the gradient to the surface (velocity handling presumably as before). Early bugs: "oceanic death rays" into space; long spikes that fell and made the Earth "explode" — fixed after tweaks (no details given).
- Visuals: normal map, satellite imagery (later greyscale), his atmosphere shader (play with RGB wavelengths), moon with normal and colour maps.

### Step 8 — Oceans on Earth (17:27–20:24)
- Continents start flooded; water drains into basins; terrain heavily vertically exaggerated (true scale impractical). Tide arrives as a giant wave hitting North America, then the **Andes** (some spills over), rebounding toward Antarctica.
- Real tides need: larger and more accurate sim, farther moon, Earth spin and tilt, the Sun, etc.
- Computation (19:19): apparent weight reduction when the moon is overhead: **~15 %** in his tiny world vs **~0.0003 %** on the real Earth — tiny per-parcel forces that add up across a whole ocean.
- Spinning the Earth on its axis → everything exploded; left for later (20:16).

### Step 9 — Terraforming planet (20:24–23:50)
- Procedural planet from his old terraforming project (density texture + marching cubes; noise scale, layers, base radius).
- Removed the CPU readback for mouse picking: a compute kernel steps along the mouse ray through the terrain **density texture** until below the surface, then edits the density texture there directly (21:00). Initial brush too strong (blasted a hole through the planet).
- **The terrain density field is used directly as the collision field** for the fluid, even though it is not a true distance field; "close enough in this case" (21:55).
- Play: carve a channel joining a lake to a lower basin (water flows), second channel, waterfall, raise an archway of rock and drop it (big splash), dig a tunnel under the ocean and open it from the sea floor → the sea drains and gushes out elsewhere.

### Step 10 — Three-body system (23:50–27:23)
- Chaotic 3-body initial conditions found by fiddling a few minutes. **Reference frame choice:** pick a "main" body; its acceleration is subtracted from all bodies **and from the fluid particles**, so that body stays fixed and fluid physics stays consistent (24:29). Shows paths relative to each body.
- Fluid on the red planet; run at ~10× speed; interesting waves.

### Key concepts (video 3)
Variable gravity field (inverse-square, point mass); tidal acceleration as differential acceleration; n-body semi-implicit Euler with orbit prediction; shell spawn; inherited velocity; mesh → SDF by ray casting with BVH and majority-vote sign; SDF stored as 3D texture; collision by pushing along the SDF gradient; using a density field as an approximate SDF; GPU-side mouse picking by marching a density texture; non-inertial reference frame by subtracting one body's acceleration.

### Performance (video 3)
~2.2 M particles near the limit of his PC. SDF bake 70³ × 500 rays: a few minutes, offline, cached to disk.

### References (video 3)
His own earlier projects: Geographical Adventures (Earth mesh), ray tracing (BVH), terraforming (density planet), atmosphere shader, Solar System (orbits). Newton's law of gravitation. No papers cited.

---

## 4. Coding Adventure: Simulating Smoke (Q78wvrQ9xsU, 41:40)

**Source:** full transcript (English UK captions; 626 segments). Code read from `github.com/SebLague/Smoke-Simulation` (Unity 2022.3, branch `main`): `Assets/SmokeCompute/SmokeCompute.compute` and `SmokeComputeManager.cs` (the GPU version). The CPU prototype is in `Assets/SmokeCPU/` (`FluidGrid.cs`, `FluidDrawer.cs`, `FluidTest.cs`), not read. Main reference in the description: **Robert Bridson & Matthias Müller-Fischer, "Fluid Simulation" SIGGRAPH 2007 course notes** (`cs.ubc.ca/~rbridson/fluidsimulation/fluids_notes.pdf`); also Ten Minute Physics #17 "How to write an Eulerian fluid simulator" (Matthias Müller).

**Chapters (from the description):** 00:00 Intro · 00:50 Navier-Stokes Equations · 02:00 Divergence · 05:09 Solving for Pressure · 08:26 Gauss-Seidel Iteration · 10:36 Solid Cells · 12:26 Velocity Interpolation · 15:15 Advection (with derivatives) · 18:37 Semi-Lagrangian Advection · 22:12 Testing · 23:43 Successive Over-Relaxation · 25:16 Visualizing Speed · 26:32 Smoke Map · 28:14 Parallel Pressure Solving · 31:13 Vortex Shedding · 35:48 A Few Little Experiments · 37:55 Temperature · 39:17 Final Smoke Experiments · 40:38 Outro.

Framing (0:34): deliberately a *different* approach from the particle sim: **Eulerian grid** instead of **Lagrangian particles**.

### Step 1 — Equations (0:50–2:00)
- Simplified incompressible Navier–Stokes: `∂u/∂t + (u·∇)u = -(1/ρ)∇p + f + ν∇²u` and `∇·u = 0`. He sets viscosity ν = 0 and (for now) f = 0. Pressure accelerates fluid from high to low pressure; division by density = inertia.

### Step 2 — Divergence and the staggered (MAC) grid (2:00–5:09)
- Divergence of a cell: net flow across its edges. Storing **normal velocity components on cell edges** (u on left/right edges, v on top/bottom) makes the balance check direct → **staggered / MAC grid**.
- For an nx × ny grid: horizontal velocities array (nx+1) × ny, vertical nx × (ny+1) (e.g. 3×2 grid → 4×2 and 3×3).
- Discrete divergence: `div = (u_r - u_l)/Δx + (v_t - v_b)/Δx` (his sign convention in the video: negative when outflow dominates — the formula as written in code is `velRight - velLeft + velTop - velBottom`, i.e. positive = outflow; the visualisation colours one sign red, the other blue).
- Visualisation script: boxes for cells, arrows per edge component, keyboard shortcut to randomise velocities; colour = divergence. Test grid **5×3**. Editing one edge affects two cells.

### Step 3 — Pressure equation derivation (5:09–8:26)
- Labels: centre cell C with neighbours L, R, T, B; edge velocities u_l, u_r, u_t, u_b; new velocities v.
- Update: `v_r = u_r - Δt/ρ · (p_R - p_C)/Δx` (pressure gradient across that edge lines up with the staggered storage). Constants Δt, Δx, ρ (constant because divergence-free ⇒ mass in each cell cannot change) folded into `k = Δt/(ρ Δx)`:
  `v_r = u_r - k (p_R - p_C)`, `v_l = u_l - k (p_C - p_L)`, `v_t = u_t - k (p_T - p_C)`, `v_b = u_b - k (p_C - p_B)`.
- Impose `(v_r - v_l) + (v_t - v_b) = 0`, solve for p_C:
  `p_C = (p_L + p_R + p_T + p_B)/4 - (ρ Δx/(4Δt)) · (u_r - u_l + u_t - u_b)`
  i.e. "average of the neighbours minus the cell size × density × velocity deltas over 4Δt" (7:54). This is the discrete Poisson equation `∇²p = (ρ/Δt) ∇·u`.
- The notes recommend **MICCG (modified incomplete Cholesky conjugate gradient)**; he starts with Gauss–Seidel.

### Step 4 — Gauss–Seidel (8:26–10:36)
- Pressure grid initialised to 0; update cells in place, each using the latest neighbour values; sweep the whole grid repeatedly until values stop changing. Run every frame while dragging velocities.
- Velocity update: subtract `k · (pressure difference)` at each edge. **Bug (10:05):** divergence not driven to zero — the constant was typed inverted; after fixing it, divergence → 0 and dragging one arrow makes others react to keep it zero.

### Step 5 — Solid cells (10:36–12:26)
- Mark rim cells solid; fluid cannot cross solid edges: velocities on edges of solid cells are set to 0.
- Re-derived pressure: for a solid neighbour, its pressure and edge velocity drop out and the divisor drops by one:
  `p_C = (Σ_{open n} p_n)/N_open - (velocity term)/(N_open · k)` with only open edges. If `N_open = 0`, return 0 (no meaningful pressure).
- [code] GPU version precomputes per cell a 4-bit mask of flowable edges and the velocity term:
  ```hlsl
  // PreparePressureSolver
  int flowTop = 1 - (isSolid | IsSolidCell(x, y+1)); ... // same for L, R, B
  int packed = flowLeft<<0 | flowRight<<1 | flowBottom<<2 | flowTop<<3;
  float velTop = VelocityMap[(x, y+1)].y; float velRight = VelocityMap[(x+1, y)].x;
  float velBottom = VelocityMap[(x,y)].y; float velLeft = VelocityMap[(x,y)].x;
  int n = flowTop + flowBottom + flowLeft + flowRight;
  velocityTerm = n == 0 ? 0 : (velRight - velLeft + velTop - velBottom) / (n * K);
  PressureSolveData[xy] = float2(velocityTerm, packed);
  ```
  Storage detail [code]: the velocity texture is the **same size as the cell grid** (resolution.x × resolution.y, RG float); each texel stores its cell's **left** edge u (x) and **bottom** edge v (y); the right/top edges are read from the neighbour texel. `K = timeStep / (density * cellSize)`, density = 1.

### Step 6 — Velocity interpolation (12:26–15:15)
- To get velocity anywhere: bilinear interpolation separately on the u-grid and the v-grid (each staggered by half a cell). Explained with a colour quad (70 % across, 50 % up example).
- Bugs on first attempt; fixed. With the solver on, a velocity brush (radius around the mouse) drawing a straight stroke produces flow that curls back on itself (divergence-free) instead of a "sausage". Interpolated velocities look not very smooth (low resolution, crude interpolation).
- [code] world → UV for the two staggered sample grids, sampled with a hardware bilinear sampler:
  ```hlsl
  // u (x component) is offset by -0.5 cell in x; v (y component) by -0.5 cell in y
  float2 bl_X = float2(-nx/2.0 - 0.5, -ny/2.0) * cellSize; float2 tr_X = float2(nx/2.0 - 0.5, ny/2.0) * cellSize;
  float vx = VelocityMapSample.SampleLevel(sampler, (p - bl_X)/(tr_X - bl_X), 0).r;
  // same for y with offsets on y
  ```

### Step 7 — Advection with derivatives: failed attempt (15:15–18:37)
- Problem: velocities are stored on fixed cells; they must move with the flow (in particles they did automatically).
- 1D test: values on a row; finite-difference slope; update `q -= c · slope · Δt` (upwind-less forward scheme) with c = 1 → mostly works but introduces oscillations/artifacts. Using the velocities themselves as c (self-advection, `u -= u ∂u/∂x Δt`) works until it **explodes**. On the grid: "angry sea anemone"; with pressure solver on more stable, but stirring makes it explode. Notes warn of high-frequency wiggles; better slope approximations exist, but the notes recommend semi-Lagrangian instead.

### Step 8 — Semi-Lagrangian advection (18:37–22:12)
- Lagrangian = particles; Eulerian = grid; semi-Lagrangian = imagine a particle at each sample point without creating it.
- Forward version (move the particle and splat to neighbouring edges) is messy. **Backward version:** for each edge sample at position x, get the full interpolated velocity there, trace back `x_old = x - u(x)·Δt`, and set the new value = interpolated value at x_old. Assumption improves as Δt shrinks.
- First test: nothing moves where the velocity is zero (backtrace from zero velocity stays put) — the pressure solver spreads velocity around and makes it work.
- He also built a forward-splatting variant: works after fixes but spreads velocity fast for non-grid-aligned motion and is hard to parallelise (many particles write to the same edges, race conditions). Backward is independent per edge → chosen.
- Bigger grid; divergence display shows the solver not keeping up when stirring → **30 iterations** (just about 60 fps on CPU).
- [code] GPU:
  ```hlsl
  float2 RewindPos(float2 p) { return p - GetVelocityAtWorldPos(p) * deltaTime; }
  // VelocitySelfAdvection: for the left edge (unless it borders an obstacle)
  velAdvected.x = GetVelocityAtWorldPos(RewindPos(CellEdgeLeft(x,y))).x;
  velAdvected.y = GetVelocityAtWorldPos(RewindPos(CellEdgeBottom(x,y))).y;
  // written to VelocityMapAdvected, then copied back (ping-pong)
  ```
  Single Euler backtrace (no RK2).

### Step 9 — Testing and the error metric (22:12–23:43)
- Order per frame: pressure solve → velocity update (remove divergence) → advection (reintroduces divergence). Visualise divergence right after the solve, not after advection.
- **Total divergence error** summed per step. Recorded mouse input at fixed frame rate for reproducible A/B tests: 3 iterations → error ~**190k**; 15 iterations → ~**27k**.

### Step 10 — Successive over-relaxation (23:43–25:16)
- `p ← p_old + ω (p_GS - p_old)`, ω ∈ [1, 2). Bug: ω set to 0 (no update). ω = 2 never stabilises; 1.9 rippling; sweep of values plotted → **ω = 1.7 best** for 15 GS iterations (still visible overshoot ripples). [code] `weightSOR = 1.7`, inspector range 1–1.9.

### Step 11 — Visualising speed, numerical dissipation (25:16–26:32)
- Colour cells by speed ("donut" from a circular stroke). Velocities **diffuse and slow down by themselves** — numerical dissipation from repeated interpolation and big time steps; would prefer it as an explicit parameter; left for the future.

### Step 12 — Smoke map (26:32–28:14)
- Another grid with "smoke matter" per cell, advected exactly like velocity (backtrace from the cell centre, sample bilinear). Smoke is **not conserved**: counter from 5400 units drops to ~4000 while stirring (semi-Lagrangian is not mass-conserving).
- [code] smoke map is RGBA float: rgb = three smoke colours, a = temperature.

### Step 13 — Parallel pressure solving (28:14–31:13)
- Gauss–Seidel uses fresh values → not parallel. **Jacobi**: read old map, write new map, swap; parallel but needs ~**2× the iterations** of GS for equal accuracy; under/over-relaxation gave lacklustre results.
- **Red-black Gauss–Seidel**: checkerboard; update all red cells in parallel (their neighbours are black), then all black. Fresher data → SOR works again. Compared 15 GS iterations vs 30 red-black passes (each pass = half the cells): worse than serial GS but promising for parallel.
- [code]:
  ```hlsl
  uint2 CheckerboardCoord(uint i, uint passIndex, uint2 res) {
      uint y = i / (res.x / 2);           // width assumed even
      uint x = (2 * i) % res.x;
      if (y % 2 != passIndex % 2) x++;
      return uint2(x, y);
  }
  // PressureSolve_RedBlack (64 threads, halfCellCount threads)
  float pT = GetPressure(x, y+1) * flowTop; ... // out of bounds → 0
  float pressureNew = (pL + pR + pB + pT) / n - velocityTerm;
  PressureMap[cell] = pOld + (pressureNew - pOld) * weightSOR;
  ```
  C# loops `pressureSolveIts * 2` passes (default `pressureSolveIts = 40`), setting `passIndex` each dispatch. Optional `clearPressure`; by default pressure is **warm-started** from the previous frame.
- [code] velocity update:
  ```hlsl
  if (canFlowL) velocityLeft   -= K * (pCentre - pLeft);
  if (canFlowD) velocityBottom -= K * (pCentre - pDown);
  ```

### [code] Full GPU step order (`SmokeComputeManager.RunSim`)
1. `ExternalForces`: scene elements heat cells toward their target temperature (`a += (T_target - T)·temperatureRate·dt`), non-obstacle elements add velocity sources and smoke (instant set or rate·dt); smoke in obstacles zeroed; optional constant wind: column x = 1 set to `(windX, 0)`.
2. `Buoyancy`.
3. `Interaction` (mouse: left adds velocity = mouse delta × multiplier × radial weight on left/bottom edges, masked by obstacles; right adds smoke with a smoothstep brush).
4. `UpdateObstacleMap` (map border solid except optionally right/top open; border cells forced to ambient temperature) and `UpdateObstacleEdges` (4-bit mask).
5. Pressure: prepare + 2×its red-black passes.
6. `VelocityPressureUpdate`, debug divergence.
7. `SmokeAdvection` (backtrace from cell centres; outside domain returns (0,0,0, ambient)) → `SmokeDiffusionAndReadback`.
8. `VelocitySelfAdvection` → `AdvectedVelocityReadback`.
Default resolution 640×360, `worldWidth = 35`, `gravity = -1`, `velocityMultiplier = 0.4`, `numStepsPerFrame = 1`, fixed or variable timestep with time scale.

### Step 14 — Wind tunnel and vortex shedding (31:13–35:48)
- Constant inflow velocity on the left edge, constant smoke source region, solid walls except the right side open (pressure out there = 0). Resolution **160×90**, **500 fps** with **80 solver iterations** per frame.
- Circular obstacle (cells set solid): smoke splits, rejoins, then starts oscillating spontaneously until a steady alternating pattern: **vortex shedding / Kármán vortex street** — same phenomenon as cloud streets behind islands; photograph of glycerol vapour around a cylinder (32:41).
- **Smoke diffusion (33:03):** Laplacian `∇²q ≈ (q_L + q_R + q_T + q_B - 4q_C)/Δx²`; `q += D·Δt·∇²q` (explicit). Notes that the same operator on velocity would be viscosity. [code] also exponential decay: `q *= exp(-dt * decay)` for smoke and temperature.
- Coloured smoke, velocity arrows, speed colouring, pressure map view.
- **Resolution scaling (34:25):** 640×360 starts "hiccupping" and goes pink (smoke diffusion spiralling — explicit diffusion unstable when `D·Δt/Δx²` too big). Fix: **4× simulation frame rate and 10× iterations** → behaves. He does not know exact scaling laws; more advanced pressure solvers needed (especially for 3D). 1920×1080 with the same settings: **10 fps**, still sheds vortices but irregularly (multiple swirls per side, varied sizes).

### Step 15 — Experiments (35:48–37:55)
- Smoke map initialised from an image (ray tracing render) and stirred; **velocity dissipation** setting decays all velocities toward zero for control. Brush controlling both velocity and smoke to "write" (illegible fast → more dissipation). Tangent: a painting program (future). Random velocities each frame + dissipation on a photo → abstract splotches.

### Step 16 — Temperature and buoyancy (37:55–39:17)
- Temperature in the smoke map's alpha (advected with the flow). Buoyancy model from the course notes: force up proportional to `(T - T_ambient)`, down proportional to smoke concentration; ambient temperature enforced at boundaries; applied to velocities except at solid edges.
  [code]:
  ```hlsl
  float4 s = GetSmokeAtWorldPos(CellEdgeBottom(x, y));   // sampled at the v-edge
  float fT = -buoyancyFactor_temperature * (s.a - ambientTemperature) * gravity;   // gravity < 0 → hot rises
  float fS =  buoyancyFactor_smoke * (dot(s.rgb, 1)/3) * gravity;                  // smoke sinks
  VelocityMap[xy].y += (fT + fS) * deltaTime * (1 - obstacleS);
  ```
  (Bridson's notes: `f_buoy = (-α s + β (T - T_amb)) · up`.)
- Scene objects (circle/quad/line) in a structured buffer: obstacles, heat sources with target temperature, smoke emitters, velocity sources. Display: red = hot, blue = cold, green = smoke.
- **Bug (39:03):** smoke flowing through walls — a missing `!` in the code.

### Step 17 — Final experiments (39:17–41:40)
- Lacks fine detail → future: sharper interpolation, more accurate integration, **vorticity confinement** (mentioned as a future trick, not implemented), 3D, maybe a gas giant like Jupiter.
- Scene with a line of freezing-cold balls at the top (cool air sinks) and an obstacle: smoke first forks symmetrically, then switches into vortex shedding (symmetry-breaking instability; he wants to learn the mechanism).

### Key concepts (video 4)
Eulerian vs Lagrangian; staggered MAC grid; discrete divergence; pressure projection (Poisson equation) derived from the divergence-free constraint; Gauss–Seidel, SOR (ω = 1.7), Jacobi (2× iterations), red-black GS on GPU; solid cell boundary conditions (drop terms, divide by open-edge count); open boundary with p = 0; bilinear interpolation on staggered grids; failed finite-difference advection (oscillation, instability); semi-Lagrangian backward advection (unconditionally stable, dissipative, not conservative); numerical dissipation; smoke as passive scalar; explicit Laplacian diffusion and its instability at high resolution; exponential decay; temperature + buoyancy (Boussinesq-style); vortex shedding.

### Performance (video 4)
CPU: 30 GS iterations ≈ 60 fps on a modest grid. GPU: 160×90, 80 iterations, 500 fps. 640×360 needs 4× steps and 10× iterations. 1920×1080: 10 fps. Error: 3 its → 190k; 15 its → 27k; SOR ω=1.7 lowest.

### References (video 4)
Bridson & Müller-Fischer, *Fluid Simulation*, SIGGRAPH 2007 course notes (main guide; MICCG, semi-Lagrangian, buoyancy, boundary conditions); Ten Minute Physics #17, Matthias Müller (linked in description); his Coding Adventure: Sound (particle sim used for pressure waves).

---

## 5. Bad Apple!! but it's a fluid simulation (2Ni13dnAbSA, 3:59)

**Source:** no narration. The "transcript" is only the song's lyric subtitles (not used, not reproduced). Notes come from the description and two paused frames I inspected (0:08 and 1:15). No chapters. No code public (project files on Patreon).

- Description: renders the *Bad Apple!!* shadow-art music video (Touhou; animation by Anira, music Alstroemeria Records feat. nomico) "inside of a particle-based fluid simulation" — i.e. the SPH sim of videos 1–2, 2D. Recommends 4K because compression hates the dots.
- **Observed (not stated):** the particles fill the regions that are **white** in the original animation and leave the **black** silhouettes empty, so the black pixels of each video frame act as a moving solid/obstacle mask (frame 0:08: dense blue dots around an empty black human figure; frame 1:15: a hand shape made of particles on black, white/bright particles where they move fast). Particles are drawn as small dots coloured by speed (blue → white), the visualisation from video 1.
- Implied technique for a tutorial: per frame, sample a binary image as a collision field (like the obstacle box in video 1 or the SDF collision in video 3): particles inside a "solid" pixel are pushed out along the gradient of a distance field of the mask (or toward the nearest free pixel) and lose their normal velocity. The frame changes every 1/30 s, so the mask is a moving boundary that shoves fluid around. Exact method unknown; mark as inference.
- Useful to Islands only as a demonstration that an image/mask can drive a fluid boundary.

---

## 6. Bad Apple!! but it's a smoke simulation (8BCdKJ4ObZ4, 3:51)

**Source:** no narration (lyrics-only subtitles, not used). Technique stated in the description; one frame inspected (1:15). No chapters, no code (Patreon).

- Description (paraphrased): the **white pixels of the original animation are treated as solid cells** (drawn in black); a **constant outward force is applied along the outline** of the solids; **smoke is gradually added along the outline**. Simulation explained in video 4 (grid smoke sim).
- So per video frame: update the obstacle map from the image (the GPU sim's `UpdateObstacleMap` + `UpdateObstacleEdges` already rebuild obstacles every step); for fluid cells adjacent to solid cells, add velocity along the outward normal (normal ≈ direction away from the solid neighbours, e.g. from the gradient of the blurred mask) and add smoke there. The pressure projection then makes the outward push swirl into the rest of the domain, and the smoke shows it.
- Observed frame (1:15): black silhouettes (figure with a scythe) with grey/white turbulent smoke wisps peeling off the outlines; detailed curling structures (resolution appears high, ~video resolution).
- Relevance: the mask → obstacles + boundary forcing + boundary emission is exactly the pattern for "wind flowing around terrain above a height threshold" (threshold the heightmap into solid cells) and for coast-hugging emission.

---

## Cross-cutting

### Grid (Eulerian) vs particles (Lagrangian), as he frames it
- Video 4 (0:34, 15:15, 18:37): particles carry their own velocity, so advection is free ("the velocities themselves were moving through space"); on a grid, values live on fixed cells and must be **advected** explicitly. "Particle-based simulations are referred to as Lagrangian… grid-based… Eulerian"; semi-Lagrangian = imagine a particle at each grid point and trace it backward, without creating it.
- Particles (SPH, video 1): incompressibility is only approximate (density relaxes over time, jelly bounce, stiffness vs substeps trade-off); free surfaces, splashes, unbounded domain via hashed grid; cost dominated by neighbour search + sort; needs heavy rendering work (video 2) to look continuous.
- Grid (video 4): incompressibility is enforced *exactly* up to solver convergence by pressure projection; boundaries are trivial (solid cells); good for gases/smoke filling a box; suffers numerical dissipation and non-conservation of advected quantities (smoke 5400 → 4000), needs many solver iterations as resolution grows.
- He uses the grid for smoke/air and particles for liquid with a free surface. A density/SDF grid is also used *alongside* particles: for rendering (video 2, density 3D texture) and for collisions (video 3, SDF / terrain density texture).

### Determinism (what in his code is or is not deterministic)
His code makes no determinism claims; this is analysis from the code read.
- **SPH (video 1 code):** each thread writes only its own particle (no atomics) and the neighbour loop order is fixed by the sorted spatial index, so for one GPU/driver it is repeatable step to step. But: the bitonic sort is not stable — particles with equal keys can come out in different orders if the input order differs, changing float summation order; float results vary across GPUs/drivers/compilers (fused multiply-add, `sqrt`, `exp`, `pow` precision); it uses variable `Time.deltaTime` unless `fixedTimeStep` is on; the "random direction" for coincident particles is a constant `(0,1)` in code (deterministic). The video 2 white-water uses random seeds from position and time and **atomic counters + append compaction** → slot order is non-deterministic.
- **Smoke grid (video 4 code):** every kernel is a gather (each cell reads neighbours and writes only itself), red-black passes are race-free, ping-pong buffers for advection: deterministic on one GPU for a fixed timestep. Hardware bilinear sampling (`SampleLevel`) uses reduced-precision filter weights (often 8-bit fractional) that differ across vendors → cross-platform divergence. Warm-started pressure (previous frame) means results depend on full history, not just current inputs. Variable time step unless `useFixedTimeStep`.
- Lesson for a deterministic generator: use fixed Δt, fixed iteration counts, gather-only kernels, a fixed traversal order, manual bilinear interpolation (his `SampleBilinear` function exists in the shader and could replace the sampler), CPU/Burst for golden tests, no atomics or append buffers in authoritative data, and seed all randomness from a hash of (seed, cell, step), never from time.

### Mapping to Islands.PCG needs
Context: deterministic, grid-first 2D world generator; draft design proposes a stream function ψ stored as a scalar field, curl noise, a Laplace solve with Jacobi/multigrid, and Stam 1999 stable fluids only for visuals.

1. **Wind that flows around terrain above a height threshold**
   - Threshold the heightmap → solid mask (exactly what video 6 does with image pixels and what `UpdateObstacleMap` does with scene shapes). Cells above the threshold are solid; edges touching them have zero normal velocity.
   - Steady option (matches the draft): potential/stream-function flow. Solve Laplace `∇²ψ = 0` on fluid cells with ψ constant on each solid island (ψ = value of the streamline it sits on) and ψ = U·y at the far field → `u = ∂ψ/∂y, v = -∂ψ/∂x` is divergence-free by construction and tangent to obstacles. The solver is **the same Gauss–Seidel/SOR/red-black/Jacobi machinery from video 4**: his pressure update `p_C = mean(open neighbours) - term` is exactly a Poisson relaxation with Neumann walls (drop solid neighbours, divide by open count). Reuse: red-black GS + SOR ω≈1.7, fixed iteration count; his measured data point (Jacobi needs ~2× GS iterations) supports multigrid or at least red-black SOR for large grids. Note: for ψ the walls are Dirichlet (ψ fixed on solids), for pressure they are Neumann (drop the term) — a tutorial must state which.
   - Alternative (projection, video 4): start from a uniform wind field + curl noise, zero normal velocity on solid edges, run his pressure projection once → divergence-free wind that goes around mountains. One projection (no advection) is fully deterministic and cheap; no time stepping needed.
   - Unsteady wakes (vortex shedding behind islands, 31:13; he explicitly ties it to cloud vortex streets behind islands) appear only with the full time-stepping loop (advection + projection). That is the "Stam/visual only" branch; it is history-dependent and should not be authoritative data.
   - Wind sampled on a staggered MAC grid needs his two-offset bilinear lookup (`GetVelocityAtWorldPos`) when querying at arbitrary points.
2. **Ocean currents along coasts**
   - Same ψ solve but with the land mask as solid and the ocean as fluid; along-coast flow comes from ψ = const on each landmass. Boundary forcing as in video 6 (force along the outline; for currents, a tangential rather than outward force) or wind stress as a source term `∇²ψ = -ω` (vorticity from wind curl) gives gyres; his solid-cell handling (bitmask of open edges, `edgeFlowCount`) is directly reusable. Velocity dissipation (video 4 experiments) ≈ bottom friction.
3. **Animated clouds**
   - Smoke map of video 4 = cloud density as a passive scalar advected semi-Lagrangian by the (static or slowly varying) wind field: `c_new(x) = c(x - u(x)Δt)` sampled bilinearly. Unconditionally stable, cheap, embarrassingly parallel, deterministic with fixed Δt and manual bilinear. Add explicit Laplacian diffusion `+ DΔt∇²c` (keep `DΔt/Δx² < 0.25`; his high-res pink blow-up at 34:25 is that limit) and exponential decay `c *= exp(-kΔt)`; emit along windward slopes/coasts (video 6 emission along outlines; ExternalForces "smokeRate" sources). Temperature + buoyancy (video 4, 37:55) only matter for a vertical slice, not for a top-down 2D map, but a "moisture/temperature" scalar can ride along in other channels exactly like his RGBA smoke map (rgb smoke, a temperature).
   - Rendering ideas from video 2 for clouds: Beer–Lambert transmittance `exp(-σ·density)` with per-RGB coefficients and a light march toward the sun (single scattering) for top-down cloud shading and shadows on terrain (march toward the sun through a 2.5D density).
   - Known artefacts to anticipate: numerical dissipation (clouds blur and fade), mass loss (non-conservative advection; renormalise or accept).
4. **Drift of boats / seeds**
   - Lagrangian tracers advected by the grid velocity: `x += u(x)·Δt` (or RK2) using the staggered bilinear lookup. This is the "imagine a particle" step of semi-Lagrangian, run forward. For bobbing boats on liquid he only lists it as a wish (video 1, 47:02); for foam (video 2) the rule "velocity = average fluid velocity of neighbours" and bubbles "accelerate toward fluid velocity" are simple drag models usable for boats/seeds: `v += (u_fluid(x) - v)·k_drag·Δt`. Collision with land: push out along the SDF gradient as in video 3 (an SDF of the coastline computed from the land mask; or the terrain density field used directly, which he says works "close enough").
   - Deterministic if tracers are updated in a fixed order with fixed Δt; per-tracer updates are independent (no hashing/sort needed unless tracers interact).
5. **Things NOT to port for these uses:** SPH (video 1) — liquid free surfaces are not needed for a top-down world map, it is the costly, finicky part (stiffness, substeps, neighbour sort); the screen-space liquid renderer (video 2) and white-water system; n-body gravity (video 3). Possibly useful from SPH: the kernel/interpolation formula `A(x) = Σ A_j m_j/ρ_j W(|x-x_j|, h)` and the hashed-grid neighbour search (unbounded, GPU-friendly) for scattered-point interpolation or for tracer-tracer interactions; and the near-pressure trick for keeping tracers/boats apart.

### Numbers worth carrying into a tutorial
- SPH: kernels and 2D normalisation constants (table in §1), predicted-position look-ahead 1/120 s, viscosity ~0.5, stiffness up to 1000 needs more substeps, hash constants 15823 / 9737333, 64 threads/group.
- Grid: SOR ω = 1.7 (15 GS its), Jacobi ≈ 2× iterations of GS, red-black needs ~2× passes, 160×90 @ 80 its @ 500 fps, 640×360 needs 4× steps + 10× its, `K = Δt/(ρ Δx)` with ρ = 1, default 640×360 / 40 its / world width 35 / gravity −1.
