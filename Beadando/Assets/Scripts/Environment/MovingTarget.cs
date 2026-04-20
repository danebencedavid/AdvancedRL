using UnityEngine;

public class MovingTarget : MonoBehaviour
{
    public float moveSpeed = 2f;
    public float moveRadius = 6f;
    public float directionChangeInterval = 2f;

    private Vector3 startPosition;
    private Vector3 moveDirection;
    private float directionTimer;

    public Vector3 Velocity { get; private set; }

    private void Awake()
    {
        startPosition = transform.position;
        ResetTarget();
    }

    private void FixedUpdate()
    {
        directionTimer -= Time.fixedDeltaTime;

        if (directionTimer <= 0f)
        {
            PickNewDirection();
        }

        Vector3 nextPosition = transform.position + moveDirection * moveSpeed * Time.fixedDeltaTime;
        nextPosition.y = startPosition.y;

        if (IsOutsideBoundary(nextPosition))
        {
            nextPosition = ClampToBoundary(nextPosition);
            PickDirectionTowardCenter();
        }

        transform.position = nextPosition;
        Velocity = moveDirection * moveSpeed;
    }

    public void ResetTarget()
    {
        Vector2 randomOffset = Random.insideUnitCircle * moveRadius;
        transform.position = startPosition + new Vector3(randomOffset.x, 0f, randomOffset.y);
        PickNewDirection();
        Velocity = moveDirection * moveSpeed;
    }

    private void PickNewDirection()
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
            PickNewDirection();
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
