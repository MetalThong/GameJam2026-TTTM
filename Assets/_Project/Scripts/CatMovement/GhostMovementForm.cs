using UnityEngine;

public sealed class GhostMovementForm : MovementFormBehaviour
{
    [Header("Ghost")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float acceleration = 12f;

    public override MovementForm Form => MovementForm.Ghost;

    public override void Enter(Rigidbody2D rigidbody, float defaultGravityScale)
    {
        rigidbody.gravityScale = 0f;
        rigidbody.linearVelocity = new Vector2(rigidbody.linearVelocity.x, 0f);
    }

    public override void Move(Rigidbody2D rigidbody, Vector2 moveInput)
    {
        Vector2 targetVelocity = Vector2.ClampMagnitude(moveInput, 1f) * moveSpeed;
        rigidbody.linearVelocity = Vector2.MoveTowards(
            rigidbody.linearVelocity,
            targetVelocity,
            acceleration * Time.fixedDeltaTime
        );
    }
}
