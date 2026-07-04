using UnityEngine;

public interface ICarryable
{
    string CarryId { get; }
    GameObject CarryPrefab { get; }
    Vector3 CarryWorldScale { get; }
}
