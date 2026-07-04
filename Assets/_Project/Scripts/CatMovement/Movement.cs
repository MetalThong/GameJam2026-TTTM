using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(BoxCollider2D))]
[RequireComponent(typeof(MovementInput))]
[RequireComponent(typeof(CatMovementForm))]
[RequireComponent(typeof(GhostMovementForm))]
public sealed class Movement : MonoBehaviour, ISaveable
{
    private static readonly int IsMovingHash = Animator.StringToHash("IsMoving");
    private static readonly int IsGhostHash = Animator.StringToHash("IsGhost");

    [Header("Form")]
    [SerializeField] private MovementForm startingForm = MovementForm.Cat;

    [Header("Story Lock")]
    [SerializeField] private bool shouldLockMovementUntilFlag;
    [SerializeField] private string unlockMovementFlag = "waked_up";

    [Header("Save")]
    [SerializeField] private bool restoreSavedScenePosition = true;

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
    [SerializeField] private bool snapAnimatorOnFormChange = true;
    [SerializeField] private string catIdleStateName = "CatIdle";
    [SerializeField] private string catWalkStateName = "CatWalk";
    [SerializeField] private string ghostStateName = "GhostCat";

    [Header("Interact Jump")]
    [SerializeField] private string interactJumpTrigger = "IsJump";
    [SerializeField] private string interactGroundedTrigger = "IsGrounded";
    [SerializeField] private string interactJumpingBool;
    [SerializeField] private LayerMask interactJumpGroundMask = ~0;
    [SerializeField, Min(0.01f)] private float interactJumpGroundCheckDistance = 0.35f;
    [SerializeField, Range(0.1f, 1f)] private float interactJumpGroundCheckWidthRatio = 0.9f;
    [SerializeField] private bool allowCatHorizontalMovementDuringInteractJump = true;

    private MovementInput _input;
    private MovementFormBehaviour[] _forms;
    private MovementFormBehaviour _currentForm;
    private float _defaultGravityScale;
    private bool _hasDefaultGravityScale;
    private bool _isFacingRight = true;
    private bool _isMovementLocked;
    private bool _isInteractJumping;
    private bool _hasAppliedInteractJumpVelocity;
    private float _interactJumpGroundCheckStartTime;
    private Coroutine _interactJumpRoutine;
    private readonly Collider2D[] _groundCheckHits = new Collider2D[8];
    private Vector2 _catColliderBaseOffset;
    private Vector2 _ghostColliderBaseOffset;

    public MovementForm CurrentForm => _currentForm != null ? _currentForm.Form : startingForm;
    public Vector2 MoveInput => _input != null ? _input.Move : Vector2.zero;
    public event Action<MovementForm, MovementForm> FormChanged;

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

        if (ShouldBlockMovement())
        {
            StopMovement();
            UpdateAnimator(Vector2.zero);
            return;
        }

        UpdateInteractJump();
        if (_isInteractJumping)
        {
            UpdateAnimator(Vector2.zero);
            UpdateFlip(_input.Move);
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
        if (ShouldBlockMovement())
        {
            StopMovement();
            return;
        }

        if (_isInteractJumping)
        {
            MoveCatHorizontallyDuringInteractJump();
            return;
        }

        if (_currentForm == null)
        {
            return;
        }

        _currentForm.Move(targetRigidbody, _input.Move);
    }

    public bool TryInteractJump(
        Vector2 jumpVelocity,
        float minGroundCheckDelay,
        float delayDuration = 0f,
        bool useFacingDirection = true,
        bool allowWhileStoryLocked = false,
        bool allowGhostForm = false)
    {
        if (targetRigidbody == null || IsDialogueStateActive() || _isInteractJumping)
        {
            return false;
        }

        if (_isMovementLocked && !allowWhileStoryLocked)
        {
            return false;
        }

        if (!allowGhostForm && CurrentForm != MovementForm.Cat)
        {
            return false;
        }

        float clampedDelayDuration = Mathf.Max(0f, delayDuration);
        float clampedMinGroundCheckDelay = Mathf.Max(0f, minGroundCheckDelay);
        Vector2 resolvedJumpVelocity = ResolveInteractJumpVelocity(jumpVelocity, useFacingDirection);

        _isInteractJumping = true;
        _hasAppliedInteractJumpVelocity = false;
        _interactJumpGroundCheckStartTime = float.PositiveInfinity;
        StopMovement();

        SetAnimatorTriggerIfExists(interactJumpTrigger);
        SetAnimatorBoolIfExists(interactJumpingBool, true);
        UpdateFlip(resolvedJumpVelocity);

        if (clampedDelayDuration <= 0f)
        {
            ApplyInteractJumpVelocity(resolvedJumpVelocity, clampedMinGroundCheckDelay);
        }
        else
        {
            _interactJumpRoutine = StartCoroutine(ApplyInteractJumpVelocityAfterDelay(
                resolvedJumpVelocity,
                clampedDelayDuration,
                clampedMinGroundCheckDelay
            ));
        }

        return true;
    }

    public bool TryPlayAnimationTrigger(string triggerName, bool requireCatForm = false)
    {
        if (requireCatForm && CurrentForm != MovementForm.Cat)
        {
            return false;
        }

        return SetAnimatorTriggerIfExists(triggerName);
    }

    public void Save(SaveData data)
    {
        if (data == null)
        {
            return;
        }

        data.HasPlayerState = true;
        data.PlayerPosition = transform.position;
        data.PlayerSceneName = ResolveCurrentSceneName();
        data.SetPlayerScenePosition(data.PlayerSceneName, transform.position);
        data.PlayerForm = (int)CurrentForm;
        data.PlayerFacingRight = _isFacingRight;
    }

    public void Load(SaveData data)
    {
        if (data == null || !data.HasPlayerState)
        {
            return;
        }

        SetForm(ResolveSavedForm(data.PlayerForm));
        ApplyFacing(data.PlayerFacingRight);
        RestoreScenePosition(data);
        SyncAnimatorToForm(Vector2.zero, true);
        StopMovement();
        RefreshMovementLock();
    }

    private void OnDisable()
    {
        EventBus.Unsubscribe<FlagChangedEvent>(OnFlagChanged);
        EventBus.Unsubscribe<FlagsLoadedEvent>(OnFlagsLoaded);

        if (_interactJumpRoutine != null)
        {
            StopCoroutine(_interactJumpRoutine);
            _interactJumpRoutine = null;
        }

        _isInteractJumping = false;
        _hasAppliedInteractJumpVelocity = false;

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

    public void SetForm(MovementForm form)
    {
        MovementForm previousForm = CurrentForm;
        MovementFormBehaviour nextForm = FindForm(form);
        if (nextForm == null)
        {
            Debug.LogWarning($"Movement: form '{form}' was not found.", this);
            return;
        }

        if (_currentForm == nextForm)
        {
            ApplyColliderForForm(form);
            SyncAnimatorToForm(_input != null ? _input.Move : Vector2.zero, true);
            return;
        }

        _currentForm?.Exit(targetRigidbody, _defaultGravityScale);
        _currentForm = nextForm;
        ApplyColliderForForm(form);
        _currentForm.Enter(targetRigidbody, _defaultGravityScale);

        SyncAnimatorToForm(_input != null ? _input.Move : Vector2.zero, snapAnimatorOnFormChange);

        MovementForm currentForm = CurrentForm;
        if (previousForm != currentForm)
        {
            FormChanged?.Invoke(previousForm, currentForm);
        }
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

    private MovementForm ResolveSavedForm(int formValue)
    {
        return formValue switch
        {
            (int)MovementForm.Cat => MovementForm.Cat,
            (int)MovementForm.Ghost => MovementForm.Ghost,
            _ => startingForm
        };
    }

    private void RestoreScenePosition(SaveData data)
    {
        if (!restoreSavedScenePosition || data == null)
        {
            return;
        }

        string sceneName = ResolveCurrentSceneName();
        if (!data.TryGetPlayerScenePosition(sceneName, out Vector3 savedPosition))
        {
            return;
        }

        transform.position = savedPosition;

        if (targetRigidbody != null)
        {
            targetRigidbody.position = new Vector2(savedPosition.x, savedPosition.y);
            targetRigidbody.linearVelocity = Vector2.zero;
        }
    }

    private string ResolveCurrentSceneName()
    {
        Scene scene = gameObject.scene;
        return scene.IsValid() && !string.IsNullOrWhiteSpace(scene.name)
            ? scene.name
            : SceneManager.GetActiveScene().name;
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
        SyncAnimatorToForm(moveInput, false);
    }

    private void SyncAnimatorToForm(Vector2 moveInput, bool snapState)
    {
        if (animator == null)
        {
            return;
        }

        bool isGhost = CurrentForm == MovementForm.Ghost;
        bool isMoving = IsMoving(moveInput);

        animator.SetBool(IsGhostHash, isGhost);
        animator.SetBool(IsMovingHash, isMoving);

        if (!snapState)
        {
            return;
        }

        string stateName = ResolveAnimatorStateName(isGhost, isMoving);
        if (string.IsNullOrWhiteSpace(stateName))
        {
            return;
        }

        int stateHash = Animator.StringToHash(stateName);
        if (!animator.HasState(0, stateHash))
        {
            return;
        }

        animator.Play(stateHash, 0, 0f);
        animator.Update(0f);
    }

    private string ResolveAnimatorStateName(bool isGhost, bool isMoving)
    {
        if (isGhost)
        {
            return ghostStateName;
        }

        return isMoving ? catWalkStateName : catIdleStateName;
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
        ApplyFacing(_isFacingRight);
    }

    private void ApplyFacing(bool isFacingRight)
    {
        _isFacingRight = isFacingRight;

        if (spriteRenderer != null)
        {
            spriteRenderer.flipX = spriteFacesRightByDefault ? !_isFacingRight : _isFacingRight;
        }

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

    private void UpdateInteractJump()
    {
        if (!_isInteractJumping || !_hasAppliedInteractJumpVelocity || Time.time < _interactJumpGroundCheckStartTime)
        {
            return;
        }

        if (!IsInteractJumpGrounded())
        {
            return;
        }

        CompleteInteractJump();
    }

    private void CompleteInteractJump()
    {
        _isInteractJumping = false;
        _hasAppliedInteractJumpVelocity = false;
        _interactJumpRoutine = null;
        SetAnimatorBoolIfExists(interactJumpingBool, false);
        SetAnimatorTriggerIfExists(interactGroundedTrigger);
    }

    private void MoveCatHorizontallyDuringInteractJump()
    {
        if (!allowCatHorizontalMovementDuringInteractJump
            || !_hasAppliedInteractJumpVelocity
            || CurrentForm != MovementForm.Cat
            || _currentForm == null
            || targetRigidbody == null
            || _input == null)
        {
            return;
        }

        _currentForm.Move(targetRigidbody, new Vector2(_input.Move.x, 0f));
    }

    private IEnumerator ApplyInteractJumpVelocityAfterDelay(
        Vector2 jumpVelocity,
        float delayDuration,
        float minGroundCheckDelay)
    {
        yield return new WaitForSeconds(delayDuration);

        _interactJumpRoutine = null;

        if (!_isInteractJumping || targetRigidbody == null || IsDialogueStateActive())
        {
            yield break;
        }

        ApplyInteractJumpVelocity(jumpVelocity, minGroundCheckDelay);
    }

    private Vector2 ResolveInteractJumpVelocity(Vector2 jumpVelocity, bool useFacingDirection)
    {
        if (!useFacingDirection || Mathf.Approximately(jumpVelocity.x, 0f))
        {
            return jumpVelocity;
        }

        float facingSign = _isFacingRight ? 1f : -1f;
        jumpVelocity.x = Mathf.Abs(jumpVelocity.x) * facingSign;
        return jumpVelocity;
    }

    private void ApplyInteractJumpVelocity(Vector2 jumpVelocity, float minGroundCheckDelay)
    {
        if (targetRigidbody != null)
        {
            targetRigidbody.linearVelocity = jumpVelocity;
            _hasAppliedInteractJumpVelocity = true;
            _interactJumpGroundCheckStartTime = Time.time + minGroundCheckDelay;
        }
    }

    private bool IsInteractJumpGrounded()
    {
        Collider2D groundCheckCollider = ResolveGroundCheckCollider();
        if (groundCheckCollider == null || !groundCheckCollider.enabled)
        {
            return false;
        }

        if (targetRigidbody != null && targetRigidbody.linearVelocity.y > 0.05f)
        {
            return false;
        }

        ContactFilter2D contactFilter = new()
        {
            useLayerMask = true,
            useTriggers = false
        };
        contactFilter.SetLayerMask(interactJumpGroundMask);

        Bounds bounds = groundCheckCollider.bounds;
        float checkDistance = Mathf.Max(0.01f, interactJumpGroundCheckDistance);
        float checkWidth = Mathf.Max(0.01f, bounds.size.x * interactJumpGroundCheckWidthRatio);
        Vector2 checkCenter = new(bounds.center.x, bounds.min.y - checkDistance * 0.5f);
        Vector2 checkSize = new(checkWidth, checkDistance);

        int hitCount = Physics2D.OverlapBox(
            checkCenter,
            checkSize,
            0f,
            contactFilter,
            _groundCheckHits
        );

        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hitCollider = _groundCheckHits[i];
            if (hitCollider != null && !hitCollider.transform.IsChildOf(transform))
            {
                return true;
            }
        }

        return false;
    }

    private Collider2D ResolveGroundCheckCollider()
    {
        if (CurrentForm == MovementForm.Ghost && ghostBodyCollider != null)
        {
            return ghostBodyCollider;
        }

        if (catBodyCollider != null)
        {
            return catBodyCollider;
        }

        return bodyCollider;
    }

    private bool ShouldBlockMovement()
    {
        return _isMovementLocked || IsDialogueStateActive();
    }

    private static bool IsDialogueStateActive()
    {
        return GameManager.Instance != null && GameManager.Instance.CurrentState == GameState.OnDialog;
    }

    private void StopMovement()
    {
        if (targetRigidbody != null)
        {
            targetRigidbody.linearVelocity = Vector2.zero;
        }
    }

    private bool SetAnimatorTriggerIfExists(string parameterName)
    {
        if (animator == null || string.IsNullOrWhiteSpace(parameterName))
        {
            return false;
        }

        int parameterHash = Animator.StringToHash(parameterName);
        if (HasAnimatorParameter(parameterHash, AnimatorControllerParameterType.Trigger))
        {
            animator.SetTrigger(parameterHash);
            return true;
        }

        return false;
    }

    private void SetAnimatorBoolIfExists(string parameterName, bool value)
    {
        if (animator == null || string.IsNullOrWhiteSpace(parameterName))
        {
            return;
        }

        int parameterHash = Animator.StringToHash(parameterName);
        if (HasAnimatorParameter(parameterHash, AnimatorControllerParameterType.Bool))
        {
            animator.SetBool(parameterHash, value);
        }
    }

    private bool HasAnimatorParameter(int parameterHash, AnimatorControllerParameterType parameterType)
    {
        if (animator == null)
        {
            return false;
        }

        foreach (AnimatorControllerParameter parameter in animator.parameters)
        {
            if (parameter.nameHash == parameterHash && parameter.type == parameterType)
            {
                return true;
            }
        }

        return false;
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

        if (!Application.isPlaying)
        {
            PreviewColliderProfileInEditor();
        }
    }

    private void PreviewColliderProfileInEditor()
    {
        if (bodyCollider == null || catBodyCollider != bodyCollider || ghostBodyCollider != null)
        {
            return;
        }

        ColliderProfile profile = startingForm == MovementForm.Ghost ? ghostCollider : catCollider;
        bodyCollider.size = profile.Size;
        bodyCollider.offset = profile.Offset;
    }
#endif
}
