using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;


public class PredatorAgent : Agent
{
    public Rigidbody rb;

    public float moveForce = 10f;
    public float turnSpeed = 200f;
    public float jumpForce = 1.5f;
    public float groundCheckDistance = 1.1f;
    public float maxObservationSpeed = 10f;
    public float maxTargetObservationSpeed = 5f;
    public float maxTargetDistance = 56f;
    public float maxMoveSpeed = 6f;
    public float successDistance = 2f;
    public float obstacleSensorLength = 6f;
    public float obstacleSensorHeight = 0.5f;
    public float obstacleSensorAngle = 35f;
    public float stepPenalty = -0.001f;
    public float progressRewardScale = 0.05f;
    public float nearCaptureDistance = 3f;
    public float nearCaptureReward = 0f;
    public float targetReachReward = 1.5f;
    public float arenaRadius = 28f;
    public float outOfBoundsPenalty = -1f;
    public float predatorSpawnRadius = 20f;
    public float preySpawnRadius = 19f;
    public float minimumSpawnSeparation = 12f;
    public float maxWindAcceleration = 0.75f;
    public float maxRainIntensity = 1f;
    public float clearFogDensity = 0.006f;
    public float stormFogDensity = 0.012f;
    public float clearLightIntensity = 1f;
    public float stormLightIntensity = 0.7f;
    public float hungerIncreasePerSecond = 0.025f;
    public float hungerPenaltyScale = 0.0002f;
    public float staminaDrainPerSecond = 1f;
    public float staminaRecoveryPerSecond = 0.12f;
    public float minimumStaminaMoveMultiplier = 0.55f;
    public float spawnSearchRadius = 2f;
    public float spawnClearanceRadius = 0.8f;
    public float spawnRaycastHeight = 10f;
    public float spawnRaycastDistance = 30f;
    public Color clearFogColor = new Color(0.55f, 0.64f, 0.53f, 1f);
    public Color stormFogColor = new Color(0.43f, 0.49f, 0.5f, 1f);
    public Color clearLightColor = new Color(1f, 0.95686275f, 0.8392157f, 1f);
    public Color stormLightColor = new Color(0.78f, 0.82f, 0.88f, 1f);

    private float episodeTimer = 0f;
    private float previousDistanceToTarget = 0f;
    private float currentHunger = 0f;
    private float currentStamina = 1f;
    public float maxEpisodeTime = 30f;

    public Transform target;
    private Vector3 startLocalPosition;
    private Quaternion startLocalRotation;
    private IPreyTarget preyTarget;

    public override void Initialize()
    {
        rb = GetComponent<Rigidbody>();
        startLocalPosition = transform.localPosition;
        startLocalRotation = transform.localRotation;
        CachePreyTarget();
        SharedEpisodeCoordinator.RegisterPredator(this);
    }

    public override void OnEpisodeBegin()
    {
        if (rb == null)
        {
            rb = GetComponent<Rigidbody>();
        }

        CachePreyTarget();

        if (preyTarget != null)
        {
            EpisodeEnvironmentState.PrepareEpisode(
                startLocalPosition,
                predatorSpawnRadius,
                preyTarget.SpawnOriginLocalPosition,
                preySpawnRadius,
                minimumSpawnSeparation,
                maxWindAcceleration,
                maxRainIntensity);

            EpisodeEnvironmentState.ApplyWeatherVisuals(
                clearFogColor,
                stormFogColor,
                clearFogDensity,
                stormFogDensity,
                clearLightColor,
                stormLightColor,
                clearLightIntensity,
                stormLightIntensity);

            preyTarget.ResetPrey();
        }

        transform.localPosition = FindValidSpawnPosition(EpisodeEnvironmentState.PredatorSpawnLocalPosition);
        transform.localRotation = startLocalRotation;
        rb.velocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        episodeTimer = 0f;
        currentHunger = 0f;
        currentStamina = 1f;
        previousDistanceToTarget = GetDistanceToTarget();
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        if (rb == null)
        {
            rb = GetComponent<Rigidbody>();
        }

        Vector3 localVelocity = transform.InverseTransformDirection(rb.velocity);
        sensor.AddObservation(Mathf.Clamp(localVelocity.x / maxObservationSpeed, -1f, 1f));
        sensor.AddObservation(Mathf.Clamp(localVelocity.z / maxObservationSpeed, -1f, 1f));

        if (target == null)
        {
            sensor.AddObservation(0f);
            sensor.AddObservation(0f);
            sensor.AddObservation(0f);
            sensor.AddObservation(0f);
            sensor.AddObservation(0f);
            sensor.AddObservation(0f);
            sensor.AddObservation(0f);
            sensor.AddObservation(0f);
            sensor.AddObservation(0f);
            sensor.AddObservation(0f);
            sensor.AddObservation(0f);
            sensor.AddObservation(0f);
            sensor.AddObservation(0f);
            return;
        }

        Vector3 dirToTarget = target.position - transform.position;
        Vector3 localDirToTarget = transform.InverseTransformDirection(dirToTarget.normalized);

        sensor.AddObservation(localDirToTarget.x);
        sensor.AddObservation(localDirToTarget.z);
        sensor.AddObservation(Mathf.Clamp01(dirToTarget.magnitude / maxTargetDistance));

        if (preyTarget != null)
        {
            Vector3 localTargetVelocity = transform.InverseTransformDirection(preyTarget.Velocity);
            sensor.AddObservation(Mathf.Clamp(localTargetVelocity.x / maxTargetObservationSpeed, -1f, 1f));
            sensor.AddObservation(Mathf.Clamp(localTargetVelocity.z / maxTargetObservationSpeed, -1f, 1f));
        }
        else
        {
            sensor.AddObservation(0f);
            sensor.AddObservation(0f);
        }

        sensor.AddObservation(GetObstacleSensorObservation(transform.forward));
        sensor.AddObservation(GetObstacleSensorObservation(Quaternion.AngleAxis(-obstacleSensorAngle, Vector3.up) * transform.forward));
        sensor.AddObservation(GetObstacleSensorObservation(Quaternion.AngleAxis(obstacleSensorAngle, Vector3.up) * transform.forward));

        Vector3 localWindAcceleration = transform.InverseTransformDirection(EpisodeEnvironmentState.WindAcceleration);
        sensor.AddObservation(Mathf.Clamp(localWindAcceleration.x / Mathf.Max(maxWindAcceleration, 0.01f), -1f, 1f));
        sensor.AddObservation(Mathf.Clamp(localWindAcceleration.z / Mathf.Max(maxWindAcceleration, 0.01f), -1f, 1f));
        sensor.AddObservation(EpisodeEnvironmentState.RainIntensity);
        sensor.AddObservation(currentHunger);
        sensor.AddObservation(currentStamina);
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        if (rb == null)
        {
            rb = GetComponent<Rigidbody>();
        }

        if (target == null)
        {
            AddReward(-1f);
            SharedEpisodeCoordinator.EndBecauseMissingTarget();
            return;
        }

        float forward = Mathf.Clamp(actions.ContinuousActions[0], -1f, 1f);
        float turn = Mathf.Clamp(actions.ContinuousActions[1], -1f, 1f);
        float strafe = Mathf.Clamp(actions.ContinuousActions[2], -1f, 1f);
        float jump = Mathf.Clamp(actions.ContinuousActions[3], -1f, 1f);

        float staminaMultiplier = Mathf.Lerp(minimumStaminaMoveMultiplier, 1f, currentStamina);
        Vector3 move = transform.forward * forward + transform.right * strafe;
        rb.AddForce(move * (moveForce * staminaMultiplier), ForceMode.Acceleration);
        rb.AddForce(EpisodeEnvironmentState.WindAcceleration, ForceMode.Acceleration);

        rb.MoveRotation(rb.rotation * Quaternion.Euler(Vector3.up * (turn * turnSpeed * staminaMultiplier * Time.fixedDeltaTime)));

        if (jump > 0.5f && IsGrounded())
        {
            rb.AddForce(Vector3.up * (jumpForce * staminaMultiplier), ForceMode.Impulse);
        }

        float distance = GetDistanceToTarget();
        float progressReward = previousDistanceToTarget - distance;
        AddReward(progressReward * progressRewardScale);
        AddReward(stepPenalty);
        AddReward(-currentHunger * hungerPenaltyScale);

        if (distance <= nearCaptureDistance)
        {
            AddReward(nearCaptureReward);
        }

        previousDistanceToTarget = distance;
        UpdateInternalStates(forward, turn, strafe, jump);

        LimitHorizontalSpeed();
    }

    private void FixedUpdate()
    {
        if (StepCount <= 0)
        {
            return;
        }

        episodeTimer += Time.fixedDeltaTime;
        currentHunger = Mathf.Clamp01(currentHunger + hungerIncreasePerSecond * Time.fixedDeltaTime);

        if (target == null)
        {
            AddReward(-1f);
            SharedEpisodeCoordinator.EndBecauseMissingTarget();
            return;
        }

        if (episodeTimer >= maxEpisodeTime)
        {
            SharedEpisodeCoordinator.EndBecauseTimeout();
            return;
        }

        if (IsOutOfBounds())
        {
            AddReward(outOfBoundsPenalty);
            SharedEpisodeCoordinator.EndBecausePredatorOutOfBounds();
            return;
        }

        if (GetDistanceToTarget() <= successDistance)
        {
            AddReward(targetReachReward);
            SharedEpisodeCoordinator.EndBecausePredatorCaughtPrey();
        }
    }

    public void CompleteSharedEpisode(bool success, bool outOfBounds)
    {
        RecordEpisodeStats(success, outOfBounds);
        EndEpisode();
    }

    private bool IsGrounded()
    {
        return Physics.Raycast(transform.position, Vector3.down, groundCheckDistance);
    }

    private float GetDistanceToTarget()
    {
        if (target == null)
        {
            return maxTargetDistance;
        }

        return Vector3.Distance(transform.position, target.position);
    }

    private void CachePreyTarget()
    {
        preyTarget = null;

        if (target == null)
        {
            return;
        }

        MonoBehaviour[] targetComponents = target.GetComponents<MonoBehaviour>();

        foreach (MonoBehaviour component in targetComponents)
        {
            if (component is IPreyTarget foundPreyTarget)
            {
                preyTarget = foundPreyTarget;
                return;
            }
        }
    }

    private float GetObstacleSensorObservation(Vector3 direction)
    {
        Vector3 origin = transform.position + Vector3.up * obstacleSensorHeight;
        RaycastHit[] hits = Physics.RaycastAll(origin, direction.normalized, obstacleSensorLength, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        float closestDistance = obstacleSensorLength;
        bool foundObstacle = false;

        foreach (RaycastHit hit in hits)
        {
            if (hit.transform == transform || hit.transform.IsChildOf(transform))
            {
                continue;
            }

            if (target != null && (hit.transform == target || hit.transform.IsChildOf(target)))
            {
                continue;
            }

            if (hit.distance < closestDistance)
            {
                closestDistance = hit.distance;
                foundObstacle = true;
            }
        }

        if (!foundObstacle)
        {
            return 1f;
        }

        return Mathf.Clamp01(closestDistance / obstacleSensorLength);
    }

    private bool IsOutOfBounds()
    {
        Vector3 offsetFromStart = transform.localPosition - startLocalPosition;
        offsetFromStart.y = 0f;

        return offsetFromStart.sqrMagnitude > arenaRadius * arenaRadius;
    }

    private void LimitHorizontalSpeed()
    {
        Vector3 horizontalVelocity = new Vector3(rb.velocity.x, 0f, rb.velocity.z);

        if (horizontalVelocity.sqrMagnitude <= maxMoveSpeed * maxMoveSpeed)
        {
            return;
        }

        Vector3 clampedHorizontalVelocity = horizontalVelocity.normalized * maxMoveSpeed;
        rb.velocity = new Vector3(clampedHorizontalVelocity.x, rb.velocity.y, clampedHorizontalVelocity.z);
    }

    private void RecordEpisodeStats(bool success, bool outOfBounds)
    {
        Academy.Instance.StatsRecorder.Add("Predator/Success", success ? 1f : 0f);
        Academy.Instance.StatsRecorder.Add("Predator/OutOfBounds", outOfBounds ? 1f : 0f);
        Academy.Instance.StatsRecorder.Add("Predator/FinalDistance", GetDistanceToTarget());
        Academy.Instance.StatsRecorder.Add("Predator/EpisodeTime", episodeTimer);
        Academy.Instance.StatsRecorder.Add("Predator/FinalHunger", currentHunger);
        Academy.Instance.StatsRecorder.Add("Predator/FinalStamina", currentStamina);
        Academy.Instance.StatsRecorder.Add("Environment/RainIntensity", EpisodeEnvironmentState.RainIntensity);
        Academy.Instance.StatsRecorder.Add("Environment/WindMagnitude", EpisodeEnvironmentState.WindAcceleration.magnitude);
    }

    private void UpdateInternalStates(float forward, float turn, float strafe, float jump)
    {
        float movementEffort = Mathf.Clamp01((Mathf.Abs(forward) + Mathf.Abs(strafe) + Mathf.Abs(turn) * 0.5f + Mathf.Max(0f, jump)) / 3.5f);
        float staminaDelta = staminaRecoveryPerSecond * (1f - movementEffort) - staminaDrainPerSecond * movementEffort;
        currentStamina = Mathf.Clamp01(currentStamina + staminaDelta * Time.fixedDeltaTime);
    }

    private Vector3 FindValidSpawnPosition(Vector3 preferredLocalPosition)
    {
        Vector3 bestLocalPosition = preferredLocalPosition;

        if (TryGetGroundAlignedLocalPosition(preferredLocalPosition, out Vector3 groundedLocalPosition))
        {
            bestLocalPosition = groundedLocalPosition;

            if (IsSpawnLocationClear(groundedLocalPosition))
            {
                return groundedLocalPosition;
            }
        }

        for (int attempt = 0; attempt < 12; attempt++)
        {
            Vector2 horizontalOffset = Random.insideUnitCircle * spawnSearchRadius;
            Vector3 candidateLocalPosition = preferredLocalPosition + new Vector3(horizontalOffset.x, 0f, horizontalOffset.y);

            if (!TryGetGroundAlignedLocalPosition(candidateLocalPosition, out groundedLocalPosition))
            {
                continue;
            }

            bestLocalPosition = groundedLocalPosition;

            if (IsSpawnLocationClear(groundedLocalPosition))
            {
                return groundedLocalPosition;
            }
        }

        return bestLocalPosition;
    }

    private bool TryGetGroundAlignedLocalPosition(Vector3 localPosition, out Vector3 groundedLocalPosition)
    {
        Transform parentTransform = transform.parent;
        Vector3 worldPosition = parentTransform != null ? parentTransform.TransformPoint(localPosition) : localPosition;
        Vector3 rayOrigin = worldPosition + Vector3.up * spawnRaycastHeight;

        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, spawnRaycastHeight + spawnRaycastDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            Vector3 groundedWorldPosition = hit.point;
            groundedWorldPosition.y += 1f;
            groundedLocalPosition = parentTransform != null ? parentTransform.InverseTransformPoint(groundedWorldPosition) : groundedWorldPosition;
            return true;
        }

        groundedLocalPosition = localPosition;
        return false;
    }

    private bool IsSpawnLocationClear(Vector3 localPosition)
    {
        Transform parentTransform = transform.parent;
        Vector3 worldPosition = parentTransform != null ? parentTransform.TransformPoint(localPosition) : localPosition;
        Collider[] overlaps = Physics.OverlapSphere(worldPosition, spawnClearanceRadius, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);

        foreach (Collider overlap in overlaps)
        {
            if (overlap.transform == transform || overlap.transform.IsChildOf(transform))
            {
                continue;
            }

            if (target != null && (overlap.transform == target || overlap.transform.IsChildOf(target)))
            {
                continue;
            }

            return false;
        }

        return true;
    }
}
