using UnityEngine;

public abstract class MovementFormBehaviour : MonoBehaviour
{
    public abstract MovementForm Form { get; }

    public virtual void Enter(Rigidbody2D rigidbody, float defaultGravityScale)
    {
    }

    public virtual void Exit(Rigidbody2D rigidbody, float defaultGravityScale)
    {
    }

    public abstract void Move(Rigidbody2D rigidbody, Vector2 moveInput);
}
