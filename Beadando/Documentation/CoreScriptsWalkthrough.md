# Core Scripts Walkthrough

This document is a detailed walkthrough of the main gameplay and environment scripts in the predator-vs-prey project. It is written to be close to line-by-line, but still grouped into meaningful chunks so it stays readable.

The focus is on the scripts that currently define:

- predator learning and control
- prey learning and control
- shared episode endings
- randomized spawn and weather state
- terrain generation

The key files covered here are:

- [PredatorAgent.cs](C:\Users\daneb\beadando_wrapper\Beadando\Assets\Scripts\Agents\PredatorAgent.cs)
- [PreyAgent.cs](C:\Users\daneb\beadando_wrapper\Beadando\Assets\Scripts\Agents\PreyAgent.cs)
- [EpisodeEnvironmentState.cs](C:\Users\daneb\beadando_wrapper\Beadando\Assets\Scripts\Environment\EpisodeEnvironmentState.cs)
- [SharedEpisodeCoordinator.cs](C:\Users\daneb\beadando_wrapper\Beadando\Assets\Scripts\Environment\SharedEpisodeCoordinator.cs)
- [RollingTerrainSurface.cs](C:\Users\daneb\beadando_wrapper\Beadando\Assets\Scripts\Environment\RollingTerrainSurface.cs)
- [IPreyTarget.cs](C:\Users\daneb\beadando_wrapper\Beadando\Assets\Scripts\Environment\IPreyTarget.cs)

## Big Picture

At runtime, one shared episode works like this:

1. The predator starts the episode.
2. The predator asks [EpisodeEnvironmentState.cs](C:\Users\daneb\beadando_wrapper\Beadando\Assets\Scripts\Environment\EpisodeEnvironmentState.cs) to prepare spawn points and weather for both agents.
3. The predator forces the prey to reset into the shared episode state.
4. Both agents act every physics step.
5. Either agent can detect a terminal condition, but the actual ending goes through [SharedEpisodeCoordinator.cs](C:\Users\daneb\beadando_wrapper\Beadando\Assets\Scripts\Environment\SharedEpisodeCoordinator.cs) so both agents end together.
6. Each agent records its own episode stats before `EndEpisode()`.

That shared-end logic is important because earlier versions let the predator and prey reset independently, which made the metrics unreliable.

## 1. PredatorAgent.cs

File: [PredatorAgent.cs](C:\Users\daneb\beadando_wrapper\Beadando\Assets\Scripts\Agents\PredatorAgent.cs)

This is the main learning script for the predator. It handles:

- action application
- observation generation
- reward shaping
- stamina and hunger state
- obstacle sensing
- spawn logic
- episode ending checks

### Namespace and class setup

Lines 1-4 import:

- `UnityEngine` for transforms, rigidbody, physics, math, colors
- `Unity.MLAgents` for `Agent`
- `Unity.MLAgents.Sensors` for vector observations
- `Unity.MLAgents.Actuators` for continuous actions

Line 7 defines:

- `public class PredatorAgent : Agent`

This means the predator is a Unity ML-Agents `Agent`, so ML-Agents will call lifecycle methods such as:

- `Initialize()`
- `OnEpisodeBegin()`
- `CollectObservations()`
- `OnActionReceived()`

### Public references and movement tuning

Lines 9-18 declare the core movement and normalization parameters:

- `rb`
  - the predator rigidbody
- `moveForce = 10f`
  - how strongly forward/strafe input accelerates the body
- `turnSpeed = 200f`
  - yaw rotation speed
- `jumpForce = 1.5f`
  - upward impulse when the jump action is activated
- `groundCheckDistance = 1.1f`
  - how far downward the grounded raycast checks
- `maxObservationSpeed = 10f`
  - normalization denominator for the predator's own observed local speed
- `maxTargetObservationSpeed = 5f`
  - normalization denominator for the prey velocity observation
- `maxTargetDistance = 56f`
  - normalization distance for prey distance observation
- `maxMoveSpeed = 6f`
  - hard horizontal speed cap

### Capture and obstacle sensing parameters

Lines 19-22 define the main chase threshold and obstacle sensor geometry:

- `successDistance = 2f`
  - if the predator gets this close to the prey, the prey is considered caught
- `obstacleSensorLength = 6f`
  - ray length for obstacle sensing
- `obstacleSensorHeight = 0.5f`
  - vertical offset for obstacle rays
- `obstacleSensorAngle = 35f`
  - left/right side ray angle

These produce the three obstacle observations:

- forward clearance
- forward-left clearance
- forward-right clearance

### Reward and arena parameters

Lines 23-32 define the reward shape and arena layout:

- `stepPenalty = -0.001f`
  - small constant pressure to finish efficiently
- `progressRewardScale = 0.05f`
  - reward multiplier for reducing distance to the prey
- `nearCaptureDistance = 3f`
  - secondary near-prey threshold
- `nearCaptureReward = 0f`
  - currently disabled bonus near the prey
- `targetReachReward = 1.5f`
  - reward for catching the prey
- `arenaRadius = 28f`
  - allowed roam distance from the predator's start origin
- `outOfBoundsPenalty = -1f`
  - penalty if predator exits the arena
- `predatorSpawnRadius = 20f`
  - spawn jitter radius for the predator
- `preySpawnRadius = 19f`
  - spawn jitter radius for the prey
- `minimumSpawnSeparation = 12f`
  - desired minimum distance between predator and prey spawn points

### Weather parameters

Lines 33-38 control how strong weather can become:

- `maxWindAcceleration = 0.75f`
  - maximum randomized horizontal wind push
- `maxRainIntensity = 1f`
  - upper bound of randomized storm intensity
- `clearFogDensity = 0.006f`
- `stormFogDensity = 0.012f`
- `clearLightIntensity = 1f`
- `stormLightIntensity = 0.7f`

Weather is mostly visual plus a mild wind force, but it is also part of the predator's observation space.

### Internal state parameters

Lines 39-43 define predator metabolism and movement degradation:

- `hungerIncreasePerSecond = 0.025f`
  - hunger steadily rises every physics step
- `hungerPenaltyScale = 0.0002f`
  - per-step penalty proportional to current hunger
- `staminaDrainPerSecond = 1f`
  - stamina drains while the predator exerts itself
- `staminaRecoveryPerSecond = 0.12f`
  - stamina recovers when effort is low
- `minimumStaminaMoveMultiplier = 0.55f`
  - even at zero stamina, movement still keeps 55 percent effectiveness

This last parameter was one of the most important balancing fixes, because earlier versions made the predator too crippled when stamina was low.

### Spawn search settings and weather colors

Lines 44-51 define terrain-aware spawn correction and weather visual colors:

- `spawnSearchRadius`
  - if a preferred spawn is blocked, sample around it
- `spawnClearanceRadius`
  - overlap radius used to reject blocked spawn positions
- `spawnRaycastHeight`
  - how high above the candidate point to start the ground ray
- `spawnRaycastDistance`
  - how far downward the terrain snap ray may search
- `clearFogColor`, `stormFogColor`
  - fog colors for clear and rainy conditions
- `clearLightColor`, `stormLightColor`
  - light tint values for clear and rainy conditions

### Private runtime state

Lines 53-62 hold state that changes during the episode:

- `episodeTimer`
  - total episode duration in seconds
- `previousDistanceToTarget`
  - used to compute progress reward
- `currentHunger`
  - current hunger value in `[0, 1]`
- `currentStamina`
  - current stamina value in `[0, 1]`
- `maxEpisodeTime = 30f`
  - timeout threshold
- `target`
  - assigned prey transform
- `startLocalPosition`
  - predator's scene-origin anchor
- `startLocalRotation`
  - rotation restored on episode begin
- `preyTarget`
  - cached interface reference to the prey script

### Initialize()

Lines 64-71 run once when the agent is initialized:

- line 66 caches the rigidbody
- lines 67-68 capture the starting local pose
- line 69 tries to find an `IPreyTarget` on the assigned target
- line 70 registers this predator in the shared episode coordinator

That registration is what allows shared episode endings later.

### OnEpisodeBegin()

Lines 73-115 reset the predator and prepare the shared episode:

- lines 75-78 make sure the rigidbody reference still exists
- line 80 refreshes the prey interface cache

If a valid prey exists:

- lines 84-91 call `EpisodeEnvironmentState.PrepareEpisode(...)`
  - this generates:
    - predator spawn point
    - prey spawn point
    - wind vector
    - rain intensity
- lines 93-101 apply fog and lighting for this episode
- line 103 forces the prey to reset using the prepared shared spawn

Then the predator resets itself:

- line 106 picks a terrain-snapped, obstacle-cleared predator spawn
- line 107 restores the starting local rotation
- lines 108-109 zero rigidbody velocity and angular velocity
- lines 111-114 reset timer, hunger, stamina, and reference distance to prey

This method is the real start of each encounter.

### CollectObservations()

Lines 117-175 define the predator observation vector.

First, lines 119-126 ensure the rigidbody exists and add the predator's local velocity:

- local velocity x
- local velocity z

These are normalized by `maxObservationSpeed`.

If the prey target is missing, lines 128-143 fill the remaining observation slots with zeros and return early.

If the prey exists:

- lines 146-151 compute:
  - local direction to prey x
  - local direction to prey z
  - normalized prey distance

Then lines 153-163 add prey velocity if available through the `IPreyTarget` interface:

- prey local velocity x
- prey local velocity z

Lines 165-167 add the obstacle sensor values:

- forward clearance
- left clearance
- right clearance

These are values from `0` to `1` where:

- `1` means no obstacle within range
- lower values mean something is nearby

Lines 169-174 add weather and internal-state observations:

- local wind x
- local wind z
- rain intensity
- hunger
- stamina

So the current predator observation count is 15 total:

1. local velocity x
2. local velocity z
3. local direction to prey x
4. local direction to prey z
5. normalized prey distance
6. prey local velocity x
7. prey local velocity z
8. forward obstacle clearance
9. left obstacle clearance
10. right obstacle clearance
11. local wind x
12. local wind z
13. rain intensity
14. hunger
15. stamina

### OnActionReceived()

Lines 177-223 are the predator's action application and immediate reward logic.

Lines 179-182 guard against a missing rigidbody reference.

Lines 184-189 handle the missing-target failure case:

- penalize the predator
- ask the shared coordinator to end the episode for both agents

Lines 191-194 unpack four continuous actions:

- `forward`
- `turn`
- `strafe`
- `jump`

Each is clamped into `[-1, 1]`.

Line 196 computes a stamina-based movement multiplier:

- `currentStamina = 1` gives full movement
- `currentStamina = 0` still gives `minimumStaminaMoveMultiplier`

Lines 197-199 apply forces:

- move in local forward/right directions
- apply wind acceleration every step

Line 201 rotates the predator using `MoveRotation`, also scaled by stamina.

Lines 203-206 apply jump:

- only if the jump action is above `0.5`
- only if the grounded raycast says the agent is on the ground

Lines 208-213 compute reward:

- `distance = current distance to prey`
- `progressReward = previousDistanceToTarget - distance`
- reward for getting closer
- step penalty
- hunger penalty

Lines 214-217 would add a near-capture reward, but `nearCaptureReward` is currently zero.

Lines 219-220 update:

- `previousDistanceToTarget`
- stamina through `UpdateInternalStates`

Line 222 clamps horizontal speed so the agent does not accelerate forever.

### FixedUpdate()

Lines 225-260 check terminal conditions every physics step.

Lines 227-230 ignore the call if the episode has not really started yet.

Lines 232-233 update:

- `episodeTimer`
- `currentHunger`

Then the script checks for terminal cases in a specific order:

- lines 235-240:
  - missing target
- lines 242-245:
  - timeout
- lines 248-253:
  - predator out of bounds
- lines 255-259:
  - predator reaches catch distance

Each case goes through [SharedEpisodeCoordinator.cs](C:\Users\daneb\beadando_wrapper\Beadando\Assets\Scripts\Environment\SharedEpisodeCoordinator.cs), which keeps the predator and prey metrics synchronized.

### CompleteSharedEpisode()

Lines 262-266 are called by the coordinator:

- line 264 records final stats
- line 265 ends the ML-Agents episode

The coordinator decides the outcome; this method only finalizes the predator side.

### Grounding and target helpers

Lines 268-281 define:

- `IsGrounded()`
  - raycast downward to see if jumping is allowed
- `GetDistanceToTarget()`
  - returns actual prey distance
  - falls back to `maxTargetDistance` if no prey exists

### CachePreyTarget()

Lines 283-302 search the assigned target's components and cache the first one implementing `IPreyTarget`.

This is how the predator stays generic:

- it does not require a hard reference specifically to `PreyAgent`
- it only needs something that behaves like a prey target

### GetObstacleSensorObservation()

Lines 304-336 are the core obstacle sensing routine.

How it works:

- line 306 starts the ray slightly above the ground
- line 307 uses `Physics.RaycastAll`
- lines 311-328 loop through all hits
- lines 313-316 ignore the predator itself
- lines 318-321 ignore the prey target
- lines 323-327 keep the nearest real obstacle hit

Return value:

- no obstacle found -> `1f`
- obstacle found -> normalized fraction of remaining space

This means closer obstacles yield smaller values.

### Arena and speed helpers

Lines 338-357 define:

- `IsOutOfBounds()`
  - checks horizontal local displacement from the predator's original center
- `LimitHorizontalSpeed()`
  - clamps only the x/z velocity
  - preserves vertical velocity for jumping and falling

### RecordEpisodeStats()

Lines 359-369 push metrics into ML-Agents `StatsRecorder`:

- `Predator/Success`
- `Predator/OutOfBounds`
- `Predator/FinalDistance`
- `Predator/EpisodeTime`
- `Predator/FinalHunger`
- `Predator/FinalStamina`
- `Environment/RainIntensity`
- `Environment/WindMagnitude`

These are the numbers later exported to TensorBoard and CSV.

### UpdateInternalStates()

Lines 371-376 implement stamina drain and recovery.

The idea:

- large actions mean higher `movementEffort`
- low effort means more recovery
- high effort means more drain

The final stamina update is clamped into `[0, 1]`.

This function is one of the most balance-sensitive parts of the predator.

### Spawn correction helpers

Lines 378-453 make spawns terrain-safe and obstacle-safe.

`FindValidSpawnPosition()`:

- tries the preferred shared spawn first
- snaps it to the ground
- rejects it if blocked
- samples nearby alternatives up to 12 times

`TryGetGroundAlignedLocalPosition()`:

- converts local spawn to world space
- raycasts down from above
- snaps to the hit point
- offsets upward by 1 unit so the agent sits above the ground

`IsSpawnLocationClear()`:

- checks an overlap sphere at the candidate point
- ignores the predator itself
- ignores the prey target
- rejects anything else

This is what keeps agents from spawning inside trees or terrain.

## 2. PreyAgent.cs

File: [PreyAgent.cs](C:\Users\daneb\beadando_wrapper\Beadando\Assets\Scripts\Agents\PreyAgent.cs)

This script is the prey-side counterpart to the predator. It is simpler:

- no jumping
- no hunger or stamina
- no obstacle rays
- more direct survival reward design

It also implements `IPreyTarget`, which allows the predator to treat it as a generic prey target.

### Class and core fields

Lines 6-9 define:

- `PreyAgent : Agent, IPreyTarget`
- `rb`
- `predator`

The prey knows its predator transform directly, while the predator only depends on the prey interface.

### Movement and reward fields

Lines 11-27 define prey tuning:

- `moveForce = 9f`
- `turnSpeed = 200f`
- `maxMoveSpeed = 6.5f`
- `arenaRadius = 28f`
- `maxObservationSpeed = 10f`
- `maxPredatorDistance = 56f`
- `caughtDistance = 2f`
- `stepSurvivalReward = 0.001f`
- `distanceRewardScale = 0.015f`
- `caughtPenalty = -1.5f`
- `outOfBoundsPenalty = -2f`
- `boundaryPenaltyScale = 0.002f`
- `maxEpisodeTime = 30f`
- spawn helper parameters

Conceptually:

- the predator is rewarded for closing distance
- the prey is rewarded for surviving and increasing distance
- the prey gets punished both for being caught and for drifting too close to the edge

### Private state and interface properties

Lines 29-35 define:

- `startLocalPosition`
- `startLocalRotation`
- `episodeTimer`
- `previousDistanceToPredator`

And the `IPreyTarget` implementation:

- `Velocity`
  - exposes prey velocity to the predator
- `SpawnOriginLocalPosition`
  - exposes the prey's original scene-center anchor for coordinated spawn planning

### Initialize()

Lines 37-43:

- cache rigidbody
- remember local starting pose
- register in the shared coordinator

### OnEpisodeBegin()

Lines 45-50:

- `ResetPrey()`
- zero the timer
- store the starting predator distance

The prey does not prepare episode state itself. It trusts the predator-triggered shared episode setup if it exists.

### CollectObservations()

Lines 52-82 build the prey's 7-vector observation set.

Lines 59-61:

- local velocity x
- local velocity z

If there is no predator, lines 63-70 fill the rest with zeros.

If the predator exists, lines 73-81 add:

- local direction to predator x
- local direction to predator z
- normalized predator distance
- normalized center offset x
- normalized center offset z

So the prey observation vector is:

1. local velocity x
2. local velocity z
3. local direction to predator x
4. local direction to predator z
5. normalized predator distance
6. normalized center offset x
7. normalized center offset z

That last pair helps the prey know where it is relative to the center so it can avoid boundary deaths.

### OnActionReceived()

Lines 84-108 apply the prey's actions and rewards.

Lines 91-93 unpack three continuous actions:

- forward
- turn
- strafe

Lines 95-98:

- apply movement acceleration
- apply the same wind acceleration as the predator
- apply turn rotation

Line 100 clamps horizontal speed.

Lines 102-107 compute rewards:

- survival reward every step
- distance gain reward if it opens the gap
- boundary penalty scaled by how far it is from center

The prey does not have any hunger or stamina systems.

### ResetPrey()

Lines 110-128 reset the prey's pose and velocity.

If the predator has prepared a coordinated episode:

- line 112 checks `EpisodeEnvironmentState.TryGetPreparedPreySpawn(...)`
- line 114 uses the prepared spawn

Otherwise:

- line 118 picks a local random jitter around the start position

Then:

- line 121 restores the starting rotation
- lines 123-127 zero linear and angular velocity

This method is also the entry point used by the predator through the `IPreyTarget` interface.

### FixedUpdate()

Lines 130-164 check terminal conditions each physics step.

Order:

- missing predator target
- timeout
- out of bounds
- caught by predator

Just like the predator, the prey does not end the episode directly on its own. It routes through the shared coordinator.

### Distance and boundary helpers

Lines 166-203 define:

- `GetDistanceToPredator()`
- `IsOutOfBounds()`
- `GetBoundaryRatio()`
- `LimitHorizontalSpeed()`

`GetBoundaryRatio()` is especially important because it turns raw distance from center into a normalized `[0, 1]` value for the boundary penalty.

### RecordEpisodeStats() and CompleteSharedEpisode()

Lines 205-218 log and finalize prey-side outcomes:

- `Prey/SurvivedTimeout`
- `Prey/Caught`
- `Prey/OutOfBounds`
- `Prey/FinalDistance`
- `Prey/EpisodeTime`

Then `CompleteSharedEpisode()` records the stats and calls `EndEpisode()`.

### Spawn correction helpers

Lines 220-295 mirror the predator's spawn helper logic:

- ground snap
- overlap clearance test
- local fallback sampling

The difference is that the prey ignores the predator in the overlap test, while the predator ignores the prey.

## 3. EpisodeEnvironmentState.cs

File: [EpisodeEnvironmentState.cs](C:\Users\daneb\beadando_wrapper\Beadando\Assets\Scripts\Environment\EpisodeEnvironmentState.cs)

This is a shared static state container for one episode.

Its job is to coordinate:

- shared spawn planning
- per-episode weather values
- weather visuals

### Shared state fields

Lines 5-9 expose the current shared episode data:

- `PredatorSpawnLocalPosition`
- `PreySpawnLocalPosition`
- `WindAcceleration`
- `RainIntensity`
- `HasPreparedEpisode`

Because these are `static`, both agents can read the same values without needing a scene manager instance.

### PrepareEpisode()

Lines 11-44 do the episode randomization.

Inputs:

- predator spawn origin and radius
- prey spawn origin and radius
- minimum separation
- weather strength bounds

Process:

- lines 20-21 initialize both spawn positions to their default origins
- line 23 clamps required separation to at least zero
- lines 25-37 try up to 12 random spawn pairs
- lines 27-28 sample random horizontal offsets
- lines 30-33 test whether the distance between spawn points is large enough

Then weather is created:

- line 39 creates a random horizontal wind vector
- line 41 stores it as `WindAcceleration`
- line 42 randomizes `RainIntensity`
- line 43 marks the episode as prepared

This function is called only once per episode, by the predator.

### TryGetPreparedPreySpawn()

Lines 46-50 let the prey ask:

- "Did the predator already prepare a shared spawn for me?"

If yes, it returns the prepared local position.

### ApplyWeatherVisuals()

Lines 52-74 make the scene look like the sampled weather.

What changes:

- fog color
- fog density
- directional light color
- directional light intensity

This uses `RainIntensity` as the blend parameter between clear and storm values.

So rain is not simulated with particles here; it is mainly:

- mild wind in physics
- fog/light mood in visuals

### Helper methods

`GetRandomHorizontalOffset()` lines 76-80:

- samples inside a 2D circle
- converts it into x/z world offset

`FindDirectionalLight()` lines 82-95:

- finds all `Light` components in the scene
- returns the first one whose type is `Directional`

That is how weather lighting is applied without storing a hard scene reference.

## 4. SharedEpisodeCoordinator.cs

File: [SharedEpisodeCoordinator.cs](C:\Users\daneb\beadando_wrapper\Beadando\Assets\Scripts\Environment\SharedEpisodeCoordinator.cs)

This is the synchronization layer that keeps predator and prey episode endings aligned.

Without this class, each agent could call `EndEpisode()` independently and the metrics would drift apart.

### Registered agents

Lines 3-5 store:

- `predatorAgent`
- `preyAgent`
- `isEndingEpisode`

The boolean is a guard against double-ending the same episode.

### Registration methods

Lines 7-15:

- `RegisterPredator(...)`
- `RegisterPrey(...)`

These are called during each agent's `Initialize()`.

### Shared ending entry points

Lines 17-75 define one method for each terminal cause:

- `EndBecauseMissingTarget()`
- `EndBecauseTimeout()`
- `EndBecausePredatorOutOfBounds()`
- `EndBecausePreyOutOfBounds()`
- `EndBecausePredatorCaughtPrey()`

Each one follows the same pattern:

1. call `TryBeginEpisodeEnd()`
2. if already ending, do nothing
3. call each agent's `CompleteSharedEpisode(...)` with outcome flags
4. call `FinishEpisodeEnd()`

This is what keeps:

- `Predator/Success`
- `Prey/Caught`

and also the episode times, aligned to the same actual encounter outcome.

### Re-entry guard

Lines 77-91 define:

- `TryBeginEpisodeEnd()`
- `FinishEpisodeEnd()`

This prevents a race where both agents detect the same terminal event in the same physics window and both try to end the episode.

## 5. RollingTerrainSurface.cs

File: [RollingTerrainSurface.cs](C:\Users\daneb\beadando_wrapper\Beadando\Assets\Scripts\Environment\RollingTerrainSurface.cs)

This script deforms the scene's ground mesh into a terrain-like surface while keeping a valid collider.

It is not the Unity `Terrain` system. It is a procedural mesh deformation script attached to the plane.

### Attributes and fields

Lines 6-9:

- `[ExecuteAlways]`
  - also runs in edit mode
- `[RequireComponent(typeof(MeshFilter))]`
- `[RequireComponent(typeof(MeshCollider))]`

That guarantees the object has the components needed to deform and collide.

Lines 11-15 are the main tuning knobs:

- `heightScale`
- `outerHillStrength`
- `centralFlatRadius`
- `noiseTiling`
- `noiseOffset`

Lines 17-18 store:

- `sourceMesh`
  - the original undeformed plane mesh
- `generatedMesh`
  - the runtime/editor clone that gets modified

### Lifecycle

`OnEnable()` lines 20-23:

- regenerate the terrain mesh when the component becomes active

`OnValidate()` lines 25-28:

- schedule regeneration when an inspector value changes

`Awake()` lines 30-33:

- regenerate on scene start

`Update()` lines 35-41:

- if not playing, keep regenerating in edit mode

That makes terrain edits visible live in the Unity editor.

### Safe editor regeneration

Lines 43-63 handle a Unity editor quirk.

Directly replacing `MeshFilter.sharedMesh` during `OnValidate()` can trigger Unity warnings like:

- `SendMessage cannot be called during Awake, CheckConsistency, or OnValidate`

So this script avoids that by:

- using `EditorApplication.delayCall`
- delaying the actual `Regenerate()` call one step later

This was an important stability fix during scene editing.

### Regenerate()

Lines 65-113 do the actual mesh deformation.

Process:

1. fetch `MeshFilter` and `MeshCollider`
2. get the original source mesh
3. allocate or reuse `generatedMesh`
4. copy source mesh geometry
5. compute a new y height for every vertex
6. assign the modified mesh back to the mesh filter and mesh collider

Important implementation details:

- line 84 uses `Instantiate(sourceMesh)` for the first generated copy
- lines 89-94 refill geometry on later regenerations
- lines 99-104 deform every source vertex independently
- lines 107-108 recalculate normals and bounds
- line 111 clears collider mesh first
- line 112 reassigns the new collider mesh

That collider reassignment ensures physics uses the updated shape.

### GetSourceMesh()

Lines 115-128 capture and return the original mesh.

The main idea:

- if `sourceMesh` has not been explicitly assigned yet
- and the mesh filter currently has a usable mesh
- and that mesh is not the generated instance
- then store that as the source

This keeps the script from deforming its own already-deformed output recursively.

### EvaluateHeight()

Lines 130-148 define the terrain height function.

This is the heart of the procedural terrain.

Inputs:

- original plane vertex x/z
- current plane local scale

Derived values:

- `sampleX`, `sampleZ`
  - scaled horizontal coordinates
- `radialDistance`
  - how far from center the point is
- `normalizedRadius`
  - center-to-edge normalization
- `centerFlattening`
  - suppresses roughness near the center so the clearing stays more playable

Noise:

- `perlinA`
- `perlinB`
- blended into `rollingNoise`

Large terrain forms:

- `southRidge`
- `northKnoll`
- `eastRise`

These are summed, scaled by `outerHillStrength`, and combined with the rolling noise.

Final result:

- return `composedHeight * heightScale`

So the center stays relatively smoother, while the outer zone gets more visible variation.

### GaussianHill()

Lines 150-155 define a reusable soft hill function.

It:

- computes normalized x/z distance from a center
- uses a Gaussian falloff
- multiplies by amplitude

This makes terrain lumps smooth instead of sharp.

## 6. IPreyTarget.cs

File: [IPreyTarget.cs](C:\Users\daneb\beadando_wrapper\Beadando\Assets\Scripts\Environment\IPreyTarget.cs)

This small interface is what lets the predator work with "anything that behaves like a prey."

It requires:

- `Velocity`
- `SpawnOriginLocalPosition`
- `ResetPrey()`

That means the predator can:

- observe prey velocity
- coordinate prey spawn planning
- force the prey to reset when beginning a shared episode

without needing a hard-coded dependency on a specific prey class.

## How The Scripts Work Together In One Episode

Here is the practical flow of a single shared episode:

1. [PredatorAgent.cs](C:\Users\daneb\beadando_wrapper\Beadando\Assets\Scripts\Agents\PredatorAgent.cs) enters `OnEpisodeBegin()`.
2. It calls [EpisodeEnvironmentState.cs](C:\Users\daneb\beadando_wrapper\Beadando\Assets\Scripts\Environment\EpisodeEnvironmentState.cs) to prepare:
   - predator spawn
   - prey spawn
   - wind
   - rain
3. It applies fog/light weather visuals.
4. It calls `preyTarget.ResetPrey()`.
5. The prey reads the prepared spawn and snaps itself safely onto the terrain.
6. The predator snaps itself safely onto the terrain too.
7. Both agents begin acting:
   - predator uses 15 observations and 4 actions
   - prey uses 7 observations and 3 actions
8. Every step:
   - wind affects both agents
   - terrain collider affects both agents
   - predator updates hunger and stamina
9. If timeout, catch, missing target, or out-of-bounds occurs, either agent notifies [SharedEpisodeCoordinator.cs](C:\Users\daneb\beadando_wrapper\Beadando\Assets\Scripts\Environment\SharedEpisodeCoordinator.cs).
10. The coordinator ends both episodes together and each side records synchronized stats.

## The Most Important Knobs To Tweak

If you later rebalance the system, these are the most important fields to look at first.

### Predator balance

In [PredatorAgent.cs](C:\Users\daneb\beadando_wrapper\Beadando\Assets\Scripts\Agents\PredatorAgent.cs):

- `moveForce`
- `maxMoveSpeed`
- `jumpForce`
- `progressRewardScale`
- `targetReachReward`
- `hungerPenaltyScale`
- `staminaDrainPerSecond`
- `staminaRecoveryPerSecond`
- `minimumStaminaMoveMultiplier`

### Prey balance

In [PreyAgent.cs](C:\Users\daneb\beadando_wrapper\Beadando\Assets\Scripts\Agents\PreyAgent.cs):

- `moveForce`
- `maxMoveSpeed`
- `distanceRewardScale`
- `stepSurvivalReward`
- `boundaryPenaltyScale`
- `caughtPenalty`
- `outOfBoundsPenalty`

### Environment balance

In [PredatorAgent.cs](C:\Users\daneb\beadando_wrapper\Beadando\Assets\Scripts\Agents\PredatorAgent.cs) and [PreyAgent.cs](C:\Users\daneb\beadando_wrapper\Beadando\Assets\Scripts\Agents\PreyAgent.cs):

- `arenaRadius`
- `predatorSpawnRadius`
- `preySpawnRadius`
- `minimumSpawnSeparation`

In [RollingTerrainSurface.cs](C:\Users\daneb\beadando_wrapper\Beadando\Assets\Scripts\Environment\RollingTerrainSurface.cs):

- `heightScale`
- `outerHillStrength`
- `centralFlatRadius`

## Final Notes

If you only remember four structural ideas from this codebase, make them these:

1. The predator is the main episode orchestrator.
2. The prey is intentionally simpler and survival-focused.
3. Shared episode synchronization is essential for trustworthy metrics.
4. Terrain, weather, and trees matter mainly by shaping navigation, not by changing the observation model into image-based learning.

If you want, the next useful documentation step would be a second companion doc focused only on:

- scene objects
- model assignments
- training workflow
- how to freeze and swap predator/prey checkpoints cleanly
