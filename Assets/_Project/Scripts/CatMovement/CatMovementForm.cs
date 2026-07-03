using UnityEngine;

public sealed class CatMovementForm : MovementFormBehaviour
{
    [Header("Cat")]
    [SerializeField] private float moveSpeed = 6f;

    public override MovementForm Form => MovementForm.Cat;

    public override void Enter(Rigidbody2D rigidbody, float defaultGravityScale)
    {
        rigidbody.gravityScale = defaultGravityScale;
    }

    public override void Move(Rigidbody2D rigidbody, Vector2 moveInput)
    {
        float horizontalInput = Mathf.Clamp(moveInput.x, -1f, 1f);
        rigidbody.linearVelocity = new Vector2(horizontalInput * moveSpeed, rigidbody.linearVelocity.y);
    }
}
