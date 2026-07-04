using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;
using DG.Tweening;

public class InteractButton : MonoBehaviour, IInteractionAvailability
{
    [SerializeField] private GameObject button;
    [SerializeField] private GameObject[] extraPromptObjects;
    [SerializeField] private string fallbackPromptKey = "prompt.interact";

    [Header("Extra Prompt Fade")]
    [SerializeField] private bool animateExtraPromptObjects = true;
    [SerializeField, Min(0f)] private float extraPromptFadeInDuration = 0.2f;
    [SerializeField, Min(0f)] private float extraPromptFadeOutDuration = 0.12f;

    [Header("Form Requirement")]
    [FormerlySerializedAs("requiredCatForm")]
    [SerializeField] private bool restrictByForm;
    [SerializeField] private MovementForm requiredForm = MovementForm.Cat;

    private bool _isPlayerInside;
    private IInteractionPromptProvider _promptProvider;
    private InteractionPromptView _promptView;
    private Movement _playerMovement;
    private bool _hasAppliedLocalPromptVisibility;
    private bool _isLocalPromptVisible;

    private void Awake()
    {
        _promptProvider = GetComponent<IInteractionPromptProvider>();
        SetLocalPromptVisible(false, false, true);
        HideGlobalPrompt();
    }

    private void OnEnable()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.StateChanged += OnGameStateChanged;
        }

        RefreshPrompt();
    }

    private void OnDisable()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.StateChanged -= OnGameStateChanged;
        }

        SetPlayerMovement(null);
        SetLocalPromptVisible(false, false, true);
        HideGlobalPrompt();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        Movement movement = ResolvePlayerMovement(other);
        if (movement != null)
        {
            _isPlayerInside = true;
            SetPlayerMovement(movement);
            RefreshPrompt();
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        Movement movement = ResolvePlayerMovement(other);
        if (movement == null || (_playerMovement != null && movement != _playerMovement))
        {
            return;
        }

        _isPlayerInside = false;
        SetPlayerMovement(null);
        RefreshPrompt();
    }

    private void OnGameStateChanged(GameState previousState, GameState currentState)
    {
        RefreshPrompt();
    }

    private void RefreshPrompt()
    {
        bool shouldShowPrompt = _isPlayerInside
            && !IsPromptBlocked()
            && IsInteractionAvailable(_playerMovement);

        SetLocalPromptVisible(shouldShowPrompt);

        if (shouldShowPrompt)
        {
            ShowGlobalPrompt();
            return;
        }

        HideGlobalPrompt();
    }

    public bool IsInteractionAvailable(Movement playerMovement)
    {
        return !restrictByForm || (playerMovement != null && playerMovement.CurrentForm == requiredForm);
    }

    public void HidePromptImmediately()
    {
        _isPlayerInside = false;
        SetPlayerMovement(null);
        SetLocalPromptVisible(false, false, true);
        HideGlobalPrompt();
    }

    private static bool IsPromptBlocked()
    {
        return GameManager.Instance != null && GameManager.Instance.CurrentState == GameState.OnDialog;
    }

    private void ShowGlobalPrompt()
    {
        InteractionPromptView promptView = ResolvePromptView();
        if (promptView == null)
        {
            return;
        }

        string promptKey = _promptProvider != null
            ? _promptProvider.PromptLocalizationKey
            : fallbackPromptKey;

        promptView.Show(this, promptKey);
    }

    private void HideGlobalPrompt()
    {
        if (_promptView != null)
        {
            _promptView.Hide(this);
        }
    }

    private InteractionPromptView ResolvePromptView()
    {
        if (_promptView != null)
        {
            return _promptView;
        }

        _promptView = Object.FindFirstObjectByType<InteractionPromptView>(FindObjectsInactive.Include);
        return _promptView;
    }

    private void SetLocalPromptVisible(bool isVisible, bool animateExtras = true, bool force = false)
    {
        if (!force && _hasAppliedLocalPromptVisibility && _isLocalPromptVisible == isVisible)
        {
            return;
        }

        _hasAppliedLocalPromptVisibility = true;
        _isLocalPromptVisible = isVisible;

        SetPromptObjectVisible(button, isVisible);

        if (extraPromptObjects == null)
        {
            return;
        }

        for (int i = 0; i < extraPromptObjects.Length; i++)
        {
            SetExtraPromptObjectVisible(extraPromptObjects[i], isVisible, animateExtras && animateExtraPromptObjects);
        }
    }

    private static void SetPromptObjectVisible(GameObject promptObject, bool isVisible)
    {
        if (promptObject != null)
        {
            promptObject.SetActive(isVisible);
        }
    }

    private void SetExtraPromptObjectVisible(GameObject promptObject, bool isVisible, bool animate)
    {
        if (promptObject == null)
        {
            return;
        }

        DOTween.Kill(promptObject);
        KillPromptObjectTweens(promptObject);

        float duration = isVisible ? extraPromptFadeInDuration : extraPromptFadeOutDuration;
        if (!animate || duration <= 0f)
        {
            promptObject.SetActive(isVisible);
            SetPromptObjectAlpha(promptObject, isVisible ? 1f : 0f);
            return;
        }

        if (isVisible)
        {
            promptObject.SetActive(true);
            SetPromptObjectAlpha(promptObject, 0f);
        }
        else if (!promptObject.activeSelf)
        {
            SetPromptObjectAlpha(promptObject, 0f);
            return;
        }

        Sequence sequence = DOTween.Sequence().SetTarget(promptObject);
        SpriteRenderer[] spriteRenderers = promptObject.GetComponentsInChildren<SpriteRenderer>(true);
        Graphic[] graphics = promptObject.GetComponentsInChildren<Graphic>(true);

        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            SpriteRenderer spriteRenderer = spriteRenderers[i];
            if (spriteRenderer != null)
            {
                sequence.Join(spriteRenderer.DOFade(isVisible ? 1f : 0f, duration));
            }
        }

        for (int i = 0; i < graphics.Length; i++)
        {
            Graphic graphic = graphics[i];
            if (graphic != null)
            {
                sequence.Join(graphic.DOFade(isVisible ? 1f : 0f, duration));
            }
        }

        if (spriteRenderers.Length == 0 && graphics.Length == 0)
        {
            promptObject.SetActive(isVisible);
            return;
        }

        sequence.SetEase(isVisible ? Ease.OutQuad : Ease.InQuad);

        if (!isVisible)
        {
            sequence.OnComplete(() => promptObject.SetActive(false));
        }
    }

    private static void SetPromptObjectAlpha(GameObject promptObject, float alpha)
    {
        SpriteRenderer[] spriteRenderers = promptObject.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            SpriteRenderer spriteRenderer = spriteRenderers[i];
            if (spriteRenderer == null)
            {
                continue;
            }

            Color color = spriteRenderer.color;
            color.a = alpha;
            spriteRenderer.color = color;
        }

        Graphic[] graphics = promptObject.GetComponentsInChildren<Graphic>(true);
        for (int i = 0; i < graphics.Length; i++)
        {
            Graphic graphic = graphics[i];
            if (graphic == null)
            {
                continue;
            }

            Color color = graphic.color;
            color.a = alpha;
            graphic.color = color;
        }
    }

    private static void KillPromptObjectTweens(GameObject promptObject)
    {
        SpriteRenderer[] spriteRenderers = promptObject.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            if (spriteRenderers[i] != null)
            {
                DOTween.Kill(spriteRenderers[i]);
            }
        }

        Graphic[] graphics = promptObject.GetComponentsInChildren<Graphic>(true);
        for (int i = 0; i < graphics.Length; i++)
        {
            if (graphics[i] != null)
            {
                DOTween.Kill(graphics[i]);
            }
        }
    }

    private void SetPlayerMovement(Movement playerMovement)
    {
        if (_playerMovement == playerMovement)
        {
            return;
        }

        if (_playerMovement != null)
        {
            _playerMovement.FormChanged -= OnPlayerFormChanged;
        }

        _playerMovement = playerMovement;

        if (_playerMovement != null)
        {
            _playerMovement.FormChanged += OnPlayerFormChanged;
        }
    }

    private void OnPlayerFormChanged(MovementForm previousForm, MovementForm currentForm)
    {
        RefreshPrompt();
    }

    private static Movement ResolvePlayerMovement(Collider2D collider)
    {
        if (collider == null)
        {
            return null;
        }

        return collider.GetComponentInParent<Movement>();
    }
}
