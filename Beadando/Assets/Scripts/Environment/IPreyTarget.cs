using UnityEngine;

public interface IPreyTarget
{
    Vector3 Velocity { get; }
    Vector3 SpawnOriginLocalPosition { get; }
    void ResetPrey();
}
