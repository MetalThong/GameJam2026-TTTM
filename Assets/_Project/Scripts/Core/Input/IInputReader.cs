using System;
using UnityEngine;

public interface IInputReader
{
    Vector2 Move { get; }
    Vector2 Look { get; }

    bool IsAttackHeld { get; }
    bool IsInteractHeld { get; }
    bool IsCrouchHeld { get; }
    bool IsJumpHeld { get; }
    bool IsSprintHeld { get; }
    bool WasNextPressedThisFrame { get; }

    event Action AttackPressed;
    event Action AttackReleased;
    event Action InteractPressed;
    event Action InteractReleased;
    event Action CrouchPressed;
    event Action CrouchReleased;
    event Action JumpPressed;
    event Action JumpReleased;
    event Action PreviousPressed;
    event Action NextPressed;
    event Action SprintPressed;
    event Action SprintReleased;

    void Enable();
    void Disable();
}
