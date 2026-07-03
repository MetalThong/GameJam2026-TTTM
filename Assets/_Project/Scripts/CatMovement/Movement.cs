using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(BoxCollider2D))]
[RequireComponent(typeof(MovementInput))]
[RequireComponent(typeof(CatMovementForm))]
[RequireComponent(typeof(GhostMovementForm))]
public sealed class Movement : MonoBehaviour
{
    private static readonly int IsMovingHash = Animator.StringToHash("IsMoving");
    private static readonly int IsGhostHash = Animator.StringToHash("IsGhost");

    [Header("Form")]
    [SerializeField] private MovementForm startingForm = MovementForm.Cat;

    [Header("Story Lock")]
    [SerializeField] private bool shouldLockMovementUntilFlag;
    [SerializeField] private string unlockMovementFlag = "waked_up";

    [Header("Input")]
    [SerializeField] private InputActionAsset inputActions;

    [Header("Physics")]
    [SerializeField] private Rigidbody2D targetRigidbody;
    [SerializeField] private BoxCollider2D bodyCollider;
    [SerializeField] private BoxCollider2D catBodyCollider;
    [SerializeField] private BoxCollider2D ghostBodyCollider;
    [SerializeField] private ColliderProfile catCollider = ColliderProfile.CatDefault;
    [SerializeField] private ColliderProfile ghostCollider = ColliderProfile.GhostDefault;
    [SerializeField] private bool mirrorColliderOffsetOnFlip = true;

    [Header("Animation")]
    [SerializeField] private Animator animator;
    [SerializeField] private RuntimeAnimatorController animatorController;
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private float movingThreshold = 0.05f;
    [SerializeField] private bool spriteFacesRightByDefault;

    private MovementInput _input;
    private MovementFormBehaviour[] _forms;
    private MovementFormBehaviour _currentForm;
    private float _defaultGravityScale;
    private bool _hasDefaultGravityScale;
    private bool _isFacingRight = true;
    private bool _isMovementLocked;
    private Vector2 _catColliderBaseOffset;
    private Vector2 _ghostColliderBaseOffset;

    public MovementForm CurrentForm => _currentForm != null ? _currentForm.Form : startingForm;
    public Vector2 MoveInput => _input != null ? _input.Move : Vector2.zero;

    [System.Serializable]
    private struct ColliderProfile
    {
        public Vector2 Size;
        public Vector2 Offset;

        public static ColliderProfile CatDefault => new()
        {
            Size = new Vector2(1.41f, 0.87f),
            Offset = Vector2.zero
        };

        public static ColliderProfile GhostDefault => new()
        {
            Size = new Vector2(0.68f, 0.82f),
            Offset = Vector2.zero
        };
    }

    private void Awake()
    {
        ResolveReferences();
        if (!enabled)
        {
            return;
        }

        _input.Initialize(inputActions);
        _defaultGravityScale = targetRigidbody.gravityScale;
        _hasDefaultGravityScale = true;
        CacheColliderOffsets();

        SetForm(startingForm);
        RefreshMovementLock();
    }

    private void OnEnable()
    {
        EventBus.Subscribe<FlagChangedEvent>(OnFlagChanged);
        EventBus.Subscribe<FlagsLoadedEvent>(OnFlagsLoaded);
        RefreshMovementLock();
    }

    private void Update()
    {
        _input.Refresh();

        if (_isMovementLocked)
        {
            StopMovement();
            UpdateAnimator(Vector2.zero);
            return;
        }

        if (_input.WasToggleFormPressed)
        {
            ToggleForm();
        }

        UpdateAnimator(_input.Move);
        UpdateFlip(_input.Move);
    }

    private void FixedUpdate()
    {
        if (_isMovementLocked)
        {
            StopMovement();
            return;
        }

        if (_currentForm == null)
        {
            return;
        }

        _currentForm.Move(targetRigidbody, _input.Move);
    }

    private void OnDisable()
    {
        EventBus.Unsubscribe<FlagChangedEvent>(OnFlagChanged);
        EventBus.Unsubscribe<FlagsLoadedEvent>(OnFlagsLoaded);

        if (_hasDefaultGravityScale && targetRigidbody != null)
        {
            targetRigidbody.gravityScale = _defaultGravityScale;
        }
    }

    private void ToggleForm()
    {
        MovementForm nextForm = _currentForm != null && _currentForm.Form == MovementForm.Cat
            ? MovementForm.Ghost
            : MovementForm.Cat;

        SetForm(nextForm);
    }

    private void SetForm(MovementForm form)
    {
        MovementFormBehaviour nextForm = FindForm(form);
        if (nextForm == null)
        {
            Debug.LogWarning($"Movement: form '{form}' was not found.", this);
            return;
        }

        if (_currentForm == nextForm)
        {
            ApplyColliderForForm(form);
            UpdateAnimator(_input != null ? _input.Move : Vector2.zero);
            return;
        }

        _currentForm?.Exit(targetRigidbody, _defaultGravityScale);
        _currentForm = nextForm;
        ApplyColliderForForm(form);
        _currentForm.Enter(targetRigidbody, _defaultGravityScale);

        UpdateAnimator(_input != null ? _input.Move : Vector2.zero);
    }

    private MovementFormBehaviour FindForm(MovementForm form)
    {
        for (int i = 0; i < _forms.Length; i++)
        {
            if (_forms[i].Form == form)
            {
                return _forms[i];
            }
        }

        return null;
    }

    private void ResolveReferences()
    {
        if (targetRigidbody == null)
        {
            targetRigidbody = GetComponent<Rigidbody2D>();
        }

        if (targetRigidbody == null)
        {
            Debug.LogError("Movement: missing Rigidbody2D reference.", this);
            enabled = false;
            return;
        }

        if (bodyCollider == null)
        {
            bodyCollider = GetComponent<BoxCollider2D>();
        }

        if (catBodyCollider == null)
        {
            catBodyCollider = bodyCollider;
        }

        if (ghostBodyCollider == null)
        {
            ghostBodyCollider = FindExtraBodyCollider(catBodyCollider);
        }

        if (bodyCollider == null)
        {
            Debug.LogError("Movement: missing BoxCollider2D reference.", this);
            enabled = false;
            return;
        }

        _input = GetComponent<MovementInput>();
        if (_input == null)
        {
            Debug.LogError("Movement: missing MovementInput component.", this);
            enabled = false;
            return;
        }

        _forms = GetComponents<MovementFormBehaviour>();

        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }

        if (animator != null && animator.runtimeAnimatorController == null && animatorController != null)
        {
            animator.runtimeAnimatorController = animatorController;
        }

        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        }
    }

    private BoxCollider2D FindExtraBodyCollider(BoxCollider2D colliderToSkip)
    {
        BoxCollider2D[] colliders = GetComponents<BoxCollider2D>();
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null && colliders[i] != colliderToSkip)
            {
                return colliders[i];
            }
        }

        return null;
    }

    private void CacheColliderOffsets()
    {
        if (catBodyCollider != null)
        {
            _catColliderBaseOffset = catBodyCollider.offset;
        }

        if (ghostBodyCollider != null)
        {
            _ghostColliderBaseOffset = ghostBodyCollider.offset;
        }
    }

    private void ApplyColliderForForm(MovementForm form)
    {
        if (catBodyCollider != null && ghostBodyCollider != null && catBodyCollider != ghostBodyCollider)
        {
            bool isGhost = form == MovementForm.Ghost;
            catBodyCollider.enabled = !isGhost;
            ghostBodyCollider.enabled = isGhost;
            catBodyCollider.offset = GetFacingOffset(_catColliderBaseOffset);
            ghostBodyCollider.offset = GetFacingOffset(_ghostColliderBaseOffset);
            return;
        }

        if (bodyCollider == null)
        {
            return;
        }

        ColliderProfile profile = form == MovementForm.Ghost ? ghostCollider : catCollider;
        bodyCollider.size = profile.Size;
        bodyCollider.offset = GetFacingOffset(profile.Offset);
    }

    private void UpdateAnimator(Vector2 moveInput)
    {
        if (animator == null)
        {
            return;
        }

        bool isGhost = CurrentForm == MovementForm.Ghost;
        bool isMoving = IsMoving(moveInput);

        animator.SetBool(IsGhostHash, isGhost);
        animator.SetBool(IsMovingHash, isMoving);
    }

    private void UpdateFlip(Vector2 moveInput)
    {
        if (spriteRenderer == null)
        {
            return;
        }

        float horizontal = Mathf.Abs(moveInput.x) > movingThreshold
            ? moveInput.x
            : targetRigidbody.linearVelocity.x;

        if (Mathf.Abs(horizontal) <= movingThreshold)
        {
            return;
        }

        _isFacingRight = horizontal > 0f;
        spriteRenderer.flipX = spriteFacesRightByDefault ? !_isFacingRight : _isFacingRight;
        ApplyColliderForForm(CurrentForm);
    }

    private Vector2 GetFacingOffset(Vector2 offset)
    {
        if (!mirrorColliderOffsetOnFlip)
        {
            return offset;
        }

        bool spriteIsFlippedFromDefault = spriteFacesRightByDefault ? !_isFacingRight : _isFacingRight;
        return spriteIsFlippedFromDefault ? new Vector2(-offset.x, offset.y) : offset;
    }

    private bool IsMoving(Vector2 moveInput)
    {
        if (CurrentForm == MovementForm.Cat)
        {
            return Mathf.Abs(moveInput.x) > movingThreshold;
        }

        return moveInput.sqrMagnitude > movingThreshold * movingThreshold;
    }

    private void OnFlagChanged(FlagChangedEvent eventData)
    {
        if (eventData.FlagId == unlockMovementFlag)
        {
            RefreshMovementLock();
        }
    }

    private void OnFlagsLoaded(FlagsLoadedEvent eventData)
    {
        RefreshMovementLock();
    }

    private void RefreshMovementLock()
    {
        if (!shouldLockMovementUntilFlag || string.IsNullOrWhiteSpace(unlockMovementFlag))
        {
            _isMovementLocked = false;
            return;
        }

        FlagManager flagManager = FlagManager.Instance;
        _isMovementLocked = flagManager != null && !flagManager.HasFlag(unlockMovementFlag);

        if (_isMovementLocked)
        {
            StopMovement();
        }
    }

    private void StopMovement()
    {
        if (targetRigidbody != null)
        {
            targetRigidbody.linearVelocity = Vector2.zero;
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (targetRigidbody == null)
        {
            targetRigidbody = GetComponent<Rigidbody2D>();
        }

        if (bodyCollider == null)
        {
            bodyCollider = GetComponent<BoxCollider2D>();
        }

        if (catBodyCollider == null)
        {
            catBodyCollider = bodyCollider;
        }

        if (ghostBodyCollider == null)
        {
            ghostBodyCollider = FindExtraBodyCollider(catBodyCollider);
        }

        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }

        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        }

        if (animatorController == null)
        {
            animatorController = UnityEditor.AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
                "Assets/_Project/Animation/Cat/Cat.controller"
            );
        }
    }
#endif
}
