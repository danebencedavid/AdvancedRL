using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;


public class PredatorAgent : Agent
{
    public Rigidbody rb;

    public float moveForce = 10f;
    public float turnSpeed = 200f;
    public float jumpForce = 5f;
    public float groundCheckDistance = 1.1f;
    public float maxObservationSpeed = 10f;
    public float maxTargetObservationSpeed = 5f;
    public float maxTargetDistance = 20f;
    public float maxMoveSpeed = 6f;
    public float successDistance = 1.5f;
    public float stepPenalty = -0.001f;
    public float progressRewardScale = 0.05f;
    public float targetReachReward = 1.0f;
    public float arenaRadius = 15f;
    public float outOfBoundsPenalty = -1f;

    private float episodeTimer = 0f;
    private float previousDistanceToTarget = 0f;
    public float maxEpisodeTime = 30f;

    public Transform target;
    private Vector3 startLocalPosition;
    private Quaternion startLocalRotation;

    public override void Initialize()
    {
        rb = GetComponent<Rigidbody>();
        startLocalPosition = transform.localPosition;
        startLocalRotation = transform.localRotation;
    }

    public override void OnEpisodeBegin()
    {
        if (rb == null)
        {
            rb = GetComponent<Rigidbody>();
        }

        if (target != null && target.TryGetComponent<MovingTarget>(out MovingTarget movingTarget))
        {
            movingTarget.ResetTarget();
        }

        transform.localPosition = startLocalPosition + new Vector3(Random.Range(-5f, 5f), 0f, Random.Range(-5f, 5f));
        transform.localRotation = startLocalRotation;
        rb.velocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        episodeTimer = 0f;
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
            return;
        }

        Vector3 dirToTarget = target.position - transform.position;
        Vector3 localDirToTarget = transform.InverseTransformDirection(dirToTarget.normalized);

        sensor.AddObservation(localDirToTarget.x);
        sensor.AddObservation(localDirToTarget.z);
        sensor.AddObservation(Mathf.Clamp01(dirToTarget.magnitude / maxTargetDistance));

        if (target.TryGetComponent<MovingTarget>(out MovingTarget movingTarget))
        {
            Vector3 localTargetVelocity = transform.InverseTransformDirection(movingTarget.Velocity);
            sensor.AddObservation(Mathf.Clamp(localTargetVelocity.x / maxTargetObservationSpeed, -1f, 1f));
            sensor.AddObservation(Mathf.Clamp(localTargetVelocity.z / maxTargetObservationSpeed, -1f, 1f));
        }
        else
        {
            sensor.AddObservation(0f);
            sensor.AddObservation(0f);
        }
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
            RecordEpisodeStats(false, false);
            EndEpisode();
            return;
        }

        float forward = Mathf.Clamp(actions.ContinuousActions[0], -1f, 1f);
        float turn = Mathf.Clamp(actions.ContinuousActions[1], -1f, 1f);
        float strafe = Mathf.Clamp(actions.ContinuousActions[2], -1f, 1f);
        float jump = Mathf.Clamp(actions.ContinuousActions[3], -1f, 1f);

        Vector3 move = transform.forward * forward + transform.right * strafe;
        rb.AddForce(move * moveForce, ForceMode.Acceleration);

        rb.MoveRotation(rb.rotation * Quaternion.Euler(Vector3.up * (turn * turnSpeed * Time.fixedDeltaTime)));

        if (jump > 0.5f && IsGrounded())
        {
            rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
        }

        float distance = GetDistanceToTarget();
        float progressReward = previousDistanceToTarget - distance;
        AddReward(progressReward * progressRewardScale);
        AddReward(stepPenalty);
        previousDistanceToTarget = distance;

        LimitHorizontalSpeed();
    }

    private void FixedUpdate()
    {
        if (StepCount <= 0)
        {
            return;
        }

        episodeTimer += Time.fixedDeltaTime;

        if (target == null)
        {
            AddReward(-1f);
            RecordEpisodeStats(false, false);
            EndEpisode();
            return;
        }

        if (episodeTimer >= maxEpisodeTime)
        {
            RecordEpisodeStats(false, false);
            EndEpisode();
            return;
        }

        if (IsOutOfBounds())
        {
            AddReward(outOfBoundsPenalty);
            RecordEpisodeStats(false, true);
            EndEpisode();
            return;
        }

        if (GetDistanceToTarget() <= successDistance)
        {
            AddReward(targetReachReward);
            RecordEpisodeStats(true, false);
            EndEpisode();
        }
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
    }
}
