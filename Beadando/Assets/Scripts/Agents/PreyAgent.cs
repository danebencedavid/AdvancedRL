using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;

public class PreyAgent : Agent, IPreyTarget
{
    public Rigidbody rb;
    public Transform predator;

    public float moveForce = 9f;
    public float turnSpeed = 200f;
    public float maxMoveSpeed = 6.5f;
    public float arenaRadius = 28f;
    public float maxObservationSpeed = 10f;
    public float maxPredatorDistance = 56f;
    public float caughtDistance = 2f;
    public float stepSurvivalReward = 0.001f;
    public float distanceRewardScale = 0.015f;
    public float caughtPenalty = -1.5f;
    public float outOfBoundsPenalty = -2f;
    public float boundaryPenaltyScale = 0.002f;
    public float maxEpisodeTime = 30f;
    public float spawnSearchRadius = 2f;
    public float spawnClearanceRadius = 0.8f;
    public float spawnRaycastHeight = 10f;
    public float spawnRaycastDistance = 30f;

    private Vector3 startLocalPosition;
    private Quaternion startLocalRotation;
    private float episodeTimer = 0f;
    private float previousDistanceToPredator = 0f;

    public Vector3 Velocity => rb != null ? rb.velocity : Vector3.zero;
    public Vector3 SpawnOriginLocalPosition => startLocalPosition;

    public override void Initialize()
    {
        rb = GetComponent<Rigidbody>();
        startLocalPosition = transform.localPosition;
        startLocalRotation = transform.localRotation;
        SharedEpisodeCoordinator.RegisterPrey(this);
    }

    public override void OnEpisodeBegin()
    {
        ResetPrey();
        episodeTimer = 0f;
        previousDistanceToPredator = GetDistanceToPredator();
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

        if (predator == null)
        {
            sensor.AddObservation(0f);
            sensor.AddObservation(0f);
            sensor.AddObservation(0f);
            sensor.AddObservation(0f);
            sensor.AddObservation(0f);
            return;
        }

        Vector3 dirToPredator = predator.position - transform.position;
        Vector3 localDirToPredator = transform.InverseTransformDirection(dirToPredator.normalized);
        sensor.AddObservation(localDirToPredator.x);
        sensor.AddObservation(localDirToPredator.z);
        sensor.AddObservation(Mathf.Clamp01(dirToPredator.magnitude / maxPredatorDistance));

        Vector3 centerOffset = transform.localPosition - startLocalPosition;
        sensor.AddObservation(Mathf.Clamp(centerOffset.x / arenaRadius, -1f, 1f));
        sensor.AddObservation(Mathf.Clamp(centerOffset.z / arenaRadius, -1f, 1f));
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        if (rb == null)
        {
            rb = GetComponent<Rigidbody>();
        }

        float forward = Mathf.Clamp(actions.ContinuousActions[0], -1f, 1f);
        float turn = Mathf.Clamp(actions.ContinuousActions[1], -1f, 1f);
        float strafe = Mathf.Clamp(actions.ContinuousActions[2], -1f, 1f);

        Vector3 move = transform.forward * forward + transform.right * strafe;
        rb.AddForce(move * moveForce, ForceMode.Acceleration);
        rb.AddForce(EpisodeEnvironmentState.WindAcceleration, ForceMode.Acceleration);
        rb.MoveRotation(rb.rotation * Quaternion.Euler(Vector3.up * (turn * turnSpeed * Time.fixedDeltaTime)));

        LimitHorizontalSpeed();

        float distanceToPredator = GetDistanceToPredator();
        float distanceReward = distanceToPredator - previousDistanceToPredator;
        AddReward(stepSurvivalReward);
        AddReward(distanceReward * distanceRewardScale);
        AddReward(-GetBoundaryRatio() * boundaryPenaltyScale);
        previousDistanceToPredator = distanceToPredator;
    }

    public void ResetPrey()
    {
        if (EpisodeEnvironmentState.TryGetPreparedPreySpawn(out Vector3 spawnLocalPosition))
        {
            transform.localPosition = FindValidSpawnPosition(spawnLocalPosition);
        }
        else
        {
            transform.localPosition = FindValidSpawnPosition(startLocalPosition + new Vector3(Random.Range(-3f, 3f), 0f, Random.Range(-3f, 3f)));
        }

        transform.localRotation = startLocalRotation;

        if (rb != null)
        {
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
    }

    private void FixedUpdate()
    {
        if (StepCount <= 0)
        {
            return;
        }

        episodeTimer += Time.fixedDeltaTime;

        if (predator == null)
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
            SharedEpisodeCoordinator.EndBecausePreyOutOfBounds();
            return;
        }

        if (GetDistanceToPredator() <= caughtDistance)
        {
            AddReward(caughtPenalty);
            SharedEpisodeCoordinator.EndBecausePredatorCaughtPrey();
        }
    }

    private float GetDistanceToPredator()
    {
        if (predator == null)
        {
            return maxPredatorDistance;
        }

        return Vector3.Distance(transform.position, predator.position);
    }

    private bool IsOutOfBounds()
    {
        Vector3 offsetFromStart = transform.localPosition - startLocalPosition;
        offsetFromStart.y = 0f;

        return offsetFromStart.sqrMagnitude > arenaRadius * arenaRadius;
    }

    private float GetBoundaryRatio()
    {
        Vector3 offsetFromStart = transform.localPosition - startLocalPosition;
        offsetFromStart.y = 0f;

        return Mathf.Clamp01(offsetFromStart.magnitude / arenaRadius);
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

    private void RecordEpisodeStats(bool survivedTimeout, bool caught, bool outOfBounds)
    {
        Academy.Instance.StatsRecorder.Add("Prey/SurvivedTimeout", survivedTimeout ? 1f : 0f);
        Academy.Instance.StatsRecorder.Add("Prey/Caught", caught ? 1f : 0f);
        Academy.Instance.StatsRecorder.Add("Prey/OutOfBounds", outOfBounds ? 1f : 0f);
        Academy.Instance.StatsRecorder.Add("Prey/FinalDistance", GetDistanceToPredator());
        Academy.Instance.StatsRecorder.Add("Prey/EpisodeTime", episodeTimer);
    }

    public void CompleteSharedEpisode(bool survivedTimeout, bool caught, bool outOfBounds)
    {
        RecordEpisodeStats(survivedTimeout, caught, outOfBounds);
        EndEpisode();
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

            if (predator != null && (overlap.transform == predator || overlap.transform.IsChildOf(predator)))
            {
                continue;
            }

            return false;
        }

        return true;
    }
}
