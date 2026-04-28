# Predator-Prey ML-Agents Training Notes

This document summarizes the debugging and training process used so far for the Unity ML-Agents predator-prey project. It is intended as a handoff/reference document for reviewing what was tried, why it was tried, what changed in code/config, and what the training results suggest.

## Project Goal

The project is a 3D predator-prey ecosystem simulation using Unity and ML-Agents.

The long-term goal is for a predator RL agent to learn to:

- navigate a 3D environment
- chase and catch prey
- avoid threats
- survive under future internal pressures such as hunger/stamina
- eventually use richer sensors such as raycasts and smell
- operate under stochastic environment elements such as random spawning/weather

The current phase is deliberately simpler:

- one predator is trained with PPO
- prey is scripted, not RL
- the predator uses continuous controls
- the task is to catch the prey before timeout

## Current Tooling And Setup

Environment:

- Unity 3D
- ML-Agents package version observed in logs: `2.0.2`
- Python trainer package: `mlagents 1.1.0`
- PPO trainer
- Windows / PowerShell
- virtual environment: `C:\Users\daneb\beadando_wrapper\rl_env`
- Unity project path: `C:\Users\daneb\beadando_wrapper\Beadando`

Training command pattern:

```powershell
cd C:\Users\daneb\beadando_wrapper
rl_env\Scripts\activate
cd Beadando
mlagents-learn Assets/ML-Agents/Config/predator.yaml --run-id=<run-id>
```

Important path issue discovered:

- Running `mlagents-learn Assets/ML-Agents/Config/predator.yaml` from `C:\Users\daneb\beadando_wrapper` fails because the config is inside the `Beadando` subfolder.
- Correct working directory is:

```text
C:\Users\daneb\beadando_wrapper\Beadando
```

## Current Files Of Interest

Predator agent:

```text
Assets/Scripts/Agents/PredatorAgent.cs
```

Scripted prey:

```text
Assets/Scripts/Environment/SimplePrey.cs
```

Training config:

```text
Assets/ML-Agents/Config/predator.yaml
```

Scene:

```text
Assets/Scenes/MainEnvironment.unity
```

## Current PPO Config

Current config is:

```yaml
behaviors:
  PredatorAgent:
    trainer_type: ppo
    hyperparameters:
      batch_size: 256
      buffer_size: 2048
      learning_rate: 3.0e-4
    network_settings:
      hidden_units: 128
      num_layers: 2
    reward_signals:
      extrinsic:
        gamma: 0.99
        strength: 1.0
    max_steps: 300000
    time_horizon: 64
    summary_freq: 1000
```

We briefly tried:

```yaml
batch_size: 512
buffer_size: 4096
```

but it did not clearly improve learning and appeared worse than the best earlier run.

## Current Predator Settings

As of the latest checked state, the code and scene serialization are aligned:

```text
successDistance: 2
stepPenalty: -0.001
progressRewardScale: 0.05
nearCaptureDistance: 3
nearCaptureReward: 0
targetReachReward: 1.5
arenaRadius: 15
outOfBoundsPenalty: -1
```

Important note:

- `nearCaptureReward` exists in code but is currently `0`, effectively disabled.
- We tested `nearCaptureReward = 0.002`, but it improved reward numbers without improving actual capture behavior, so it was disabled.

## Current Prey Settings

As of the latest checked state, the code and scene serialization are aligned:

```text
wanderSpeed: 1.5
fleeSpeed: 1.75
fleeDistance: 2.25
fleeRandomness: 0.5
moveRadius: 6
directionChangeInterval: 2
```

The prey is scripted, not an ML-Agents agent.

Behavior:

- wanders randomly when predator is far
- flees when predator enters `fleeDistance`
- flee direction is not perfect; it blends away-from-predator direction with randomness
- remains inside a bounded radius
- exposes velocity to the predator through `SimplePrey.Velocity`

## Observation Space

Current vector observation size is `7`.

Predator observes:

```text
1. predator local velocity x
2. predator local velocity z
3. prey local direction x
4. prey local direction z
5. normalized prey distance
6. prey local velocity x
7. prey local velocity z
```

This was evolved from an earlier 5-observation setup without target/prey velocity.

Adding prey velocity helped the predator learn interception better than just chasing the current prey position.

## Action Space

Continuous action space with 4 actions:

```text
1. forward/backward movement
2. turn
3. strafe
4. jump
```

Actions are clamped to `[-1, 1]` in code before use.

Movement uses Rigidbody physics:

- movement force via `ForceMode.Acceleration`
- rotation via `Rigidbody.MoveRotation`
- jump via impulse if grounded
- horizontal speed is capped

## Early Debugging Issues Fixed

### Observation Warning

Unity warning:

```text
Fewer observations (0) made than vector observation size (7). The observations will be padded.
```

Root causes found:

- There were duplicate/conflicting ML-Agents components in the scene.
- A base `Agent` component was attached in addition to the custom `PredatorAgent : Agent`.
- A duplicate `PredatorAgent` component had also existed at one point.
- `target` was initially unassigned.

Fixes:

- removed duplicate custom agent component
- removed stray base `Agent` component
- kept one `Behavior Parameters`
- kept one `Decision Requester`
- kept one `PredatorAgent`
- assigned the scene target transform

Result:

- observation warnings resolved
- trainer connected correctly

### Training Not Printing Steps

Initial issue:

- Trainer connected but did not print useful progress often.

Cause:

- default `summary_freq` was effectively too high for quick debugging.

Fix:

```yaml
summary_freq: 1000
```

Also initially reduced debug PPO settings:

```yaml
batch_size: 256
buffer_size: 2048
```

### Python / ONNX Export Failure

Training failed after reaching max steps with:

```text
ModuleNotFoundError: No module named 'onnxscript'
```

Attempting to install `onnxscript` caused package conflicts and a broken protobuf install due to Windows file locking.

Resolution:

- stopped Python/TensorBoard processes
- removed broken `~rotobuf` leftover metadata
- restored ML-Agents-compatible packages

Final clean package set:

```text
numpy     1.23.5
protobuf  3.20.3
onnx      1.15.0
torch     2.2.2+cpu
mlagents  1.1.0
```

`pip check` reported:

```text
No broken requirements found.
```

Important conclusion:

- the original problem was caused by too-new Torch / ONNX exporter dependencies conflicting with ML-Agents' pinned `onnx==1.15.0`.
- aligning Torch to `2.2.2` avoided the newer `onnxscript` export path.

## Scene Serialization Issue

Important Unity behavior discovered:

Changing public C# field defaults does not update already serialized scene component values.

Example:

```csharp
public float targetReachReward = 1.5f;
```

does not update an existing scene component that already serialized:

```yaml
targetReachReward: 1
```

Unity uses the scene value at runtime.

This caused some confusion during reward/prey tuning.

Current practice:

- whenever changing public/serialized values, update both:
  - C# default
  - `MainEnvironment.unity` serialized value
- verify using search before training

Useful verification command:

```powershell
rg -n "targetReachReward|nearCaptureReward|fleeDistance|fleeSpeed|fleeRandomness|successDistance" Assets/Scripts/Agents/PredatorAgent.cs Assets/Scripts/Environment/SimplePrey.cs Assets/Scenes/MainEnvironment.unity
```

Recommended future improvement:

- move tuning values into ScriptableObject config assets or add runtime debug logs printing actual runtime values.

## Stage 1: Static Target

Initial task:

- predator reaches static target

Main fixes:

- episode termination moved away from only `OnActionReceived`
- timeout/success checks were handled in `FixedUpdate`
- reward shaping changed from raw distance penalty to progress-based reward
- observations normalized
- movement made more physics-stable

Reward approach:

```text
progressReward = previousDistance - currentDistance
reward += progressReward * progressRewardScale
reward += stepPenalty
```

Result:

- basic pipeline worked
- agent trained and moved correctly

## Stage 2: Moving Target

Added `MovingTarget`:

- target wandered randomly inside radius
- exposed velocity
- reset on episode start

Added prey/target velocity observations:

```text
target local velocity x
target local velocity z
```

Observation size changed:

```text
5 -> 7
```

Scene `VectorObservationSize` updated accordingly.

Result:

- training improved
- target velocity helped the predator learn tracking/interception

### Moving Target Boundary Bug

Problem:

- target could drift far outside intended radius because boundary handling picked random directions, including outward directions.

Evidence:

- very large `FinalDistance` values, e.g. over `200`

Fix:

- clamp next target position to movement radius
- when at boundary, choose direction back toward center

Result:

- large runaway final distances greatly reduced.

## Stage 3: Predator Arena Boundary

Problem:

- predator could run far away during failed episodes.

Added:

```text
arenaRadius: 15
outOfBoundsPenalty: -1
```

If predator leaves arena:

```text
AddReward(-1)
RecordEpisodeStats(success=false, outOfBounds=true)
EndEpisode()
```

Added TensorBoard stat:

```text
Predator/OutOfBounds
```

Result:

- out-of-bounds failures dropped to near zero by the end of training.

## Stage 4: Stats Logging

Added ML-Agents stats:

```text
Predator/Success
Predator/OutOfBounds
Predator/FinalDistance
Predator/EpisodeTime
```

These were critical for distinguishing:

- reward improvement vs real capture improvement
- timeout failures vs out-of-bounds failures
- close-but-not-catching behavior

## Stage 5: Scripted Prey

Replaced `MovingTarget` with `SimplePrey`.

Reason for not using RL prey yet:

- adding RL prey creates a non-stationary multi-agent problem
- both predator and prey policies would change simultaneously
- debugging reward/observation/environment issues becomes much harder
- scripted prey provides a stable benchmark

Initial scripted prey behavior:

```text
if predator is far:
    wander
if predator is close:
    flee directly away
```

Initial result:

- task became much harder
- predator success dropped strongly
- most failures were timeouts, not boundary exits

Conclusion:

- direct-away fleeing was too optimal/perfect for this stage.

## Prey Difficulty Tuning

### Initial Scripted Prey Settings

```text
wanderSpeed: 2
fleeSpeed: 3
fleeDistance: 5
```

Result:

- predator could not reliably catch prey

### Reduced Prey Difficulty

Changed to:

```text
wanderSpeed: 1.5
fleeSpeed: 2
fleeDistance: 3
```

Result:

- small improvement, but still low success

### Made Fleeing Less Perfect

Changed to:

```text
fleeSpeed: 1.75
fleeDistance: 2.5
fleeRandomness: 0.3 -> 0.5
```

Flee direction became:

```text
awayFromPredator + randomOffset * fleeRandomness
```

Result:

- significant improvement
- success rose into roughly 25-50% range depending on run

### Increased Capture Radius

Changed:

```text
successDistance: 1.5 -> 2.0
```

Reason:

- predator was getting close but not close enough
- more successful examples improve PPO learning

Result:

- helped make task learnable

## Reward Experiments

### `targetReachReward = 1.5`

This was the best performing reward setting so far.

Best observed run with this setting:

```text
run28
Success last20:       ~0.59
FinalDistance last20: ~3.56
EpisodeTime last20:   ~15.76
Reward last20:        ~-0.13
OutOfBounds last20:   0.00
```

### Near-Capture Reward

Tried:

```text
nearCaptureDistance: 3
nearCaptureReward: 0.002
```

Hypothesis:

- reward staying close to prey might help finish the chase

Result:

- cumulative reward improved
- success did not improve
- final distance worsened
- episode time remained high

Interpretation:

- agent may have learned to hover near prey and collect shaping instead of finishing capture

Decision:

```text
nearCaptureReward: 0
```

### `targetReachReward = 2.0`

Tried:

```text
targetReachReward: 2
nearCaptureReward: 0
```

Result:

- reward graph improved
- success did not improve
- behavior was worse than best `1.5` run

Decision:

```text
targetReachReward: 1.5
```

## PPO Batch/Buffer Experiment

Tried:

```yaml
batch_size: 512
buffer_size: 4096
```

Hypothesis:

- larger buffer/batch might reduce run-to-run variance

Result:

- did not improve performance
- success lower than best earlier run

Decision:

```yaml
batch_size: 256
buffer_size: 2048
```

## Key Training Runs Summary

The CSV file order sometimes differed or was unclear, but metrics were inferred by value ranges:

- Reward: can be negative/positive
- OutOfBounds: 0 to 1, high early, 0 late
- Success: 0 to 1, desired high late
- FinalDistance: distance values
- EpisodeTime: seconds, often up to 30

### Moving Target Stage

Moving target with velocity observations achieved:

```text
Success around 0.75-0.85
OutOfBounds near 0 after boundary fix
FinalDistance roughly 2-3
EpisodeTime roughly 10-13s
```

Conclusion:

- moving target stage was successful.

### Scripted Prey Early Runs

Initial scripted prey was too difficult:

```text
Success around 0.08-0.13
EpisodeTime near timeout
OutOfBounds near 0 late
```

Conclusion:

- failures were timeouts, not arena exits.

### Scripted Prey Improved Runs

With softened prey and reward/capture tuning:

Best observed so far:

```text
run28:
Success last20:       ~0.59
FinalDistance last20: ~3.56
EpisodeTime last20:   ~15.76s
OutOfBounds last20:   0.00
```

Other repeat runs showed variance:

```text
Success last20 often around 0.40-0.50
```

Conclusion:

- task is learnable but not yet fully stable.

## Current Diagnosis

What is working:

- ML-Agents pipeline works
- observation size is correct
- predator can learn target tracking
- target/prey velocity observations help
- arena out-of-bounds is solved
- scripted prey is learnable
- stats logging provides useful insight

What is still unstable:

- scripted prey capture success varies across runs
- best run reached about 59% success, but repeats can fall to 40-50%
- policy sometimes gets close but still times out

Likely reasons:

- predator-prey interaction is stochastic and somewhat chaotic
- prey fleeing can still create difficult final-approach situations
- reward shaping beyond terminal reward can mislead policy
- PPO variance is still present

## Current Recommended Baseline

Use:

```text
targetReachReward: 1.5
nearCaptureReward: 0
successDistance: 2.0
fleeSpeed: 1.75
fleeDistance: 2.25
fleeRandomness: 0.5
batch_size: 256
buffer_size: 2048
max_steps: 300000
```

This is the current intended experiment state.

## Suggested Next Experiments

### 1. Train Current Baseline

Run a new 300k training with the current aligned scene/script values.

Target metrics:

```text
Success last20:       >= 0.60
FinalDistance last20: <= 3.5
EpisodeTime last20:   <= 16s
OutOfBounds last20:   ~0
```

### 2. If Success Remains Below 0.55

Try slightly easier prey:

```text
fleeDistance: 2.25 -> 2.0
```

Keep everything else fixed.

Reason:

- prey starts fleeing later, giving more capture opportunities
- less reward hacking than changing rewards

### 3. If Success Exceeds 0.65 Consistently

Begin increasing difficulty slowly, one value at a time:

Option A:

```text
successDistance: 2.0 -> 1.75
```

Option B:

```text
fleeSpeed: 1.75 -> 2.0
```

Do not change both at once.

### 4. Longer-Term Improvements

Once scripted prey is stable:

- add obstacle-aware prey movement
- add predator sensing limitations
- replace direct target transform with raycast/smell style sensing
- add stamina/hunger
- introduce threats
- only then consider RL prey or self-play

## Why RL Prey Should Wait

RL prey should wait because it would make the environment non-stationary:

```text
predator policy changes while prey policy changes
```

This makes it hard to know why training fails:

- predator reward issue?
- prey reward issue?
- observation issue?
- speed balance issue?
- capture condition issue?
- multi-agent instability?

Scripted prey is the controlled stepping stone.

## Useful Commands

Run training:

```powershell
cd C:\Users\daneb\beadando_wrapper
rl_env\Scripts\activate
cd Beadando
mlagents-learn Assets/ML-Agents/Config/predator.yaml --run-id=<run-id>
```

Run TensorBoard:

```powershell
tensorboard --logdir results
```

Check important serialized values:

```powershell
rg -n "targetReachReward|nearCaptureReward|fleeDistance|fleeSpeed|fleeRandomness|successDistance" Assets/Scripts/Agents/PredatorAgent.cs Assets/Scripts/Environment/SimplePrey.cs Assets/Scenes/MainEnvironment.unity
```

Check PPO config:

```powershell
rg -n "batch_size|buffer_size|max_steps|summary_freq" Assets/ML-Agents/Config/predator.yaml
```

## Current Open Question

The main question now is:

```text
Can the predator consistently reach 60-70% success against simple scripted prey?
```

If yes:

- move toward harder prey or sensing limitations.

If no:

- continue curriculum adjustment, likely by reducing `fleeDistance` slightly or improving final-approach observations/reward without adding misleading shaping.

