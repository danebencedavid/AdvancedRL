using UnityEngine;

public class SimplePrey : MonoBehaviour, IPreyTarget
{
    public Transform predator;
    public float wanderSpeed = 1.5f;
    public float fleeSpeed = 1.75f;
    public float fleeDistance = 2.25f;
    public float fleeRandomness = 0.5f;
    public float moveRadius = 6f;
    public float directionChangeInterval = 2f;

    private Vector3 startPosition;
    private Vector3 moveDirection;
    private float directionTimer;

    public Vector3 Velocity { get; private set; }
    public Vector3 SpawnOriginLocalPosition => transform.parent != null ? transform.localPosition : transform.position;

    private void Awake()
    {
        startPosition = transform.position;

        if (predator == null)
        {
            PredatorAgent predatorAgent = FindFirstObjectByType<PredatorAgent>();
            predator = predatorAgent != null ? predatorAgent.transform : null;
        }

        ValidateFleeDistance();

        ResetPrey();
    }

    private void FixedUpdate()
    {
        directionTimer -= Time.fixedDeltaTime;

        float currentSpeed;

        if (ShouldFlee())
        {
            PickDirectionAwayFromPredator();
            currentSpeed = fleeSpeed;
        }
        else
        {
            if (directionTimer <= 0f)
            {
                PickRandomDirection();
            }

            currentSpeed = wanderSpeed;
        }

        Vector3 nextPosition = transform.position + moveDirection * currentSpeed * Time.fixedDeltaTime;
        nextPosition.y = startPosition.y;

        if (IsOutsideBoundary(nextPosition))
        {
            nextPosition = ClampToBoundary(nextPosition);
            PickDirectionTowardCenter();
        }

        transform.position = nextPosition;
        Velocity = moveDirection * currentSpeed;
    }

    public void ResetPrey()
    {
        Vector2 randomOffset = Random.insideUnitCircle * moveRadius;
        transform.position = startPosition + new Vector3(randomOffset.x, 0f, randomOffset.y);
        PickRandomDirection();
        Velocity = moveDirection * wanderSpeed;
    }

    private bool ShouldFlee()
    {
        if (predator == null)
        {
            return false;
        }

        Vector3 offsetFromPredator = transform.position - predator.position;
        offsetFromPredator.y = 0f;

        return offsetFromPredator.sqrMagnitude <= fleeDistance * fleeDistance;
    }

    private void ValidateFleeDistance()
    {
        if (predator == null || !predator.TryGetComponent<PredatorAgent>(out PredatorAgent predatorAgent))
        {
            return;
        }

        if (fleeDistance <= predatorAgent.successDistance)
        {
            Debug.LogWarning(
                $"SimplePrey fleeDistance ({fleeDistance}) should be greater than " +
                $"PredatorAgent successDistance ({predatorAgent.successDistance}). " +
                "Otherwise the prey starts fleeing only when it is already capturable.",
                this);
        }
    }

    private void PickDirectionAwayFromPredator()
    {
        Vector3 directionAway = transform.position - predator.position;
        directionAway.y = 0f;

        if (directionAway.sqrMagnitude < 0.01f)
        {
            PickRandomDirection();
            return;
        }

        Vector2 randomDirection = Random.insideUnitCircle.normalized;
        Vector3 randomOffset = new Vector3(randomDirection.x, 0f, randomDirection.y) * fleeRandomness;
        moveDirection = (directionAway.normalized + randomOffset).normalized;
        directionTimer = directionChangeInterval;
    }

    private void PickRandomDirection()
    {
        Vector2 randomDirection = Random.insideUnitCircle;

        if (randomDirection.sqrMagnitude < 0.01f)
        {
            randomDirection = Vector2.right;
        }

        randomDirection.Normalize();
        moveDirection = new Vector3(randomDirection.x, 0f, randomDirection.y);
        directionTimer = directionChangeInterval;
    }

    private void PickDirectionTowardCenter()
    {
        Vector3 directionToCenter = startPosition - transform.position;
        directionToCenter.y = 0f;

        if (directionToCenter.sqrMagnitude < 0.01f)
        {
            PickRandomDirection();
            return;
        }

        moveDirection = directionToCenter.normalized;
        directionTimer = directionChangeInterval;
    }

    private bool IsOutsideBoundary(Vector3 position)
    {
        Vector3 offsetFromStart = position - startPosition;
        offsetFromStart.y = 0f;

        return offsetFromStart.sqrMagnitude > moveRadius * moveRadius;
    }

    private Vector3 ClampToBoundary(Vector3 position)
    {
        Vector3 offsetFromStart = position - startPosition;
        offsetFromStart.y = 0f;

        Vector3 clampedOffset = offsetFromStart.normalized * moveRadius;
        return startPosition + clampedOffset;
    }
}
