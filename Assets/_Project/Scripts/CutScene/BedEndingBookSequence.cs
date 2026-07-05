using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[RequireComponent(typeof(Animator))]
public sealed class BedEndingBookSequence : MonoBehaviour
{
    private enum SequenceStep
    {
        WaitingForOpen,
        Opening,
        WaitingForFirstMemory,
        RevealingFirstMemory,
        WaitingForSecondMemory,
        RevealingSecondMemory,
        WaitingForFlip,
        Flipping,
        RevealingText,
        Complete
    }

    [Header("Book")]
    [SerializeField] private Animator bookAnimator;
    [SerializeField] private string openAnimationStateName = "AnimationBookOpen";
    [SerializeField] private string flipAnimationStateName = "BookFlip";
    [SerializeField] private string flipBoolParameter = "isFlip";
    [SerializeField] private bool freezeBookOnLastFrame = true;

    [Header("Memory Images")]
    [SerializeField] private SpriteRenderer[] memoryImages;
    [SerializeField, Min(0f)] private float memoryRevealDuration = 1.2f;
    [SerializeField, Range(0.1f, 1f)] private float memoryStartScale = 0.92f;
    [SerializeField, Min(0f)] private float memoryFadeOutDuration = 0.35f;

    [Header("Ending Text")]
    [SerializeField] private TextMeshProUGUI[] endingTexts;
    [SerializeField, Min(0.01f)] private float textRevealDuration = 6f;
    [SerializeField, Min(1f)] private float textFadeCharacterWidth = 8f;

    [Header("Return To Menu")]
    [SerializeField] private bool showReturnPromptOnComplete = true;
    [SerializeField] private SceneId returnScene = SceneId.MainMenu;
    [SerializeField] private string returnPromptText = "(Back to Main Menu)";
    [SerializeField] private TextMeshProUGUI returnPromptTextView;
    [SerializeField] private CanvasGroup returnPromptCanvasGroup;
    [SerializeField, Min(0f)] private float returnPromptFadeDuration = 0.35f;
    [SerializeField] private Vector2 returnPromptAnchoredPosition = new(0f, 84f);
    [SerializeField, Min(1f)] private float returnPromptFontSize = 32f;

    private readonly Vector3[] _memoryBaseScales = new Vector3[2];
    private readonly SceneLoader _sceneLoader = new();
    private CancellationTokenSource _playCts;
    private SequenceStep _step = SequenceStep.WaitingForOpen;
    private float _cachedAnimatorSpeed = 1f;
    private bool _isReturnPromptVisible;
    private bool _isLoadingReturnScene;

    private void Awake()
    {
        ResolveReferences();
        CacheAnimatorSpeed();
        CacheMemoryScales();
        ResetSequenceState();
    }

    private void OnEnable()
    {
        CancelPlayback();
        _playCts = new CancellationTokenSource();
        ResetSequenceState();
    }

    private void Update()
    {
        if (!WasPrimaryClickPressed())
        {
            return;
        }

        AdvanceSequence();
    }

    private void OnDisable()
    {
        CancelPlayback();
    }

    private void OnDestroy()
    {
        CancelPlayback();
    }

    private void AdvanceSequence()
    {
        switch (_step)
        {
            case SequenceStep.WaitingForOpen:
                PlayOpenAsync(_playCts.Token).Forget();
                break;
            case SequenceStep.WaitingForFirstMemory:
                RevealMemoryAsync(0, SequenceStep.WaitingForSecondMemory, _playCts.Token).Forget();
                break;
            case SequenceStep.WaitingForSecondMemory:
                RevealMemoryAsync(1, SequenceStep.WaitingForFlip, _playCts.Token).Forget();
                break;
            case SequenceStep.WaitingForFlip:
                PlayFlipAsync(_playCts.Token).Forget();
                break;
            case SequenceStep.Complete:
                LoadReturnSceneAsync(_playCts.Token).Forget();
                break;
        }
    }

    private async UniTaskVoid PlayOpenAsync(CancellationToken cancellationToken)
    {
        _step = SequenceStep.Opening;

        try
        {
            await PlayAnimatorStateAsync(openAnimationStateName, true, cancellationToken);
            _step = SequenceStep.WaitingForFirstMemory;
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async UniTaskVoid RevealMemoryAsync(
        int imageIndex,
        SequenceStep nextStep,
        CancellationToken cancellationToken)
    {
        _step = imageIndex == 0 ? SequenceStep.RevealingFirstMemory : SequenceStep.RevealingSecondMemory;

        try
        {
            await RevealMemoryImageAsync(imageIndex, cancellationToken);
            _step = nextStep;
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async UniTaskVoid PlayFlipAsync(CancellationToken cancellationToken)
    {
        _step = SequenceStep.Flipping;

        try
        {
            await FadeOutMemoriesAsync(cancellationToken);

            SetFlipBool(true);
            await PlayAnimatorStateAsync(flipAnimationStateName, true, cancellationToken);

            _step = SequenceStep.RevealingText;
            await RevealEndingTextsAsync(cancellationToken);
            _step = SequenceStep.Complete;
            await ShowReturnPromptAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async UniTask PlayAnimatorStateAsync(
        string stateName,
        bool freezeOnLastFrame,
        CancellationToken cancellationToken)
    {
        if (bookAnimator == null)
        {
            Debug.LogWarning("BedEndingBookSequence: no book Animator is assigned.", this);
            return;
        }

        if (!bookAnimator.gameObject.activeInHierarchy || !bookAnimator.isActiveAndEnabled)
        {
            Debug.LogWarning("BedEndingBookSequence: book Animator must be active before playback.", bookAnimator);
            return;
        }

        RuntimeAnimatorController controller = bookAnimator.runtimeAnimatorController;
        if (controller == null || bookAnimator.layerCount <= 0)
        {
            Debug.LogWarning("BedEndingBookSequence: book Animator has no playable controller.", bookAnimator);
            return;
        }

        string resolvedStateName = ResolveStateName(stateName);
        if (string.IsNullOrWhiteSpace(resolvedStateName))
        {
            Debug.LogWarning($"BedEndingBookSequence: no playable Animator state named '{stateName}' was found.", bookAnimator);
            return;
        }

        int stateHash = Animator.StringToHash(resolvedStateName);
        bookAnimator.enabled = true;
        bookAnimator.speed = Mathf.Approximately(_cachedAnimatorSpeed, 0f) ? 1f : _cachedAnimatorSpeed;
        bookAnimator.Play(stateHash, 0, 0f);
        bookAnimator.Update(0f);

        float clipLength = ResolveClipLength(resolvedStateName);
        if (clipLength > 0f)
        {
            float speed = Mathf.Max(0.01f, Mathf.Abs(bookAnimator.speed));
            await UniTask.Delay(TimeSpan.FromSeconds(clipLength / speed), cancellationToken: cancellationToken);
        }

        if (!freezeBookOnLastFrame || !freezeOnLastFrame || cancellationToken.IsCancellationRequested)
        {
            return;
        }

        bookAnimator.Play(stateHash, 0, 0.999f);
        bookAnimator.Update(0f);
        bookAnimator.speed = 0f;
    }

    private async UniTask RevealMemoryImageAsync(int imageIndex, CancellationToken cancellationToken)
    {
        if (memoryImages == null || imageIndex < 0 || imageIndex >= memoryImages.Length)
        {
            return;
        }

        SpriteRenderer spriteRenderer = memoryImages[imageIndex];
        if (spriteRenderer == null)
        {
            return;
        }

        GameObject imageObject = spriteRenderer.gameObject;
        Transform imageTransform = spriteRenderer.transform;
        Vector3 baseScale = ResolveMemoryBaseScale(imageIndex, imageTransform);

        imageObject.SetActive(true);
        SetSpriteAlpha(spriteRenderer, 0f);
        imageTransform.localScale = baseScale * memoryStartScale;

        float duration = Mathf.Max(0.01f, memoryRevealDuration);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            cancellationToken.ThrowIfCancellationRequested();

            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float easedT = Mathf.SmoothStep(0f, 1f, t);
            SetSpriteAlpha(spriteRenderer, easedT);
            imageTransform.localScale = Vector3.Lerp(baseScale * memoryStartScale, baseScale, easedT);
            await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
        }

        SetSpriteAlpha(spriteRenderer, 1f);
        imageTransform.localScale = baseScale;
    }

    private async UniTask FadeOutMemoriesAsync(CancellationToken cancellationToken)
    {
        float duration = Mathf.Max(0.01f, memoryFadeOutDuration);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            cancellationToken.ThrowIfCancellationRequested();

            elapsed += Time.deltaTime;
            float alpha = 1f - Mathf.Clamp01(elapsed / duration);
            SetAllMemoryAlpha(alpha);
            await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
        }

        HideMemoryImages();
    }

    private async UniTask RevealEndingTextsAsync(CancellationToken cancellationToken)
    {
        if (endingTexts == null || endingTexts.Length == 0)
        {
            return;
        }

        for (int i = 0; i < endingTexts.Length; i++)
        {
            await RevealTextAsync(endingTexts[i], cancellationToken);
        }
    }

    private async UniTask RevealTextAsync(TextMeshProUGUI text, CancellationToken cancellationToken)
    {
        if (text == null)
        {
            return;
        }

        text.gameObject.SetActive(true);
        text.maxVisibleCharacters = int.MaxValue;
        text.ForceMeshUpdate(true);

        int visibleCount = text.textInfo.characterCount;
        SetTextAlpha(text, 0);

        if (visibleCount <= 0)
        {
            return;
        }

        float duration = Mathf.Max(0.01f, textRevealDuration);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            cancellationToken.ThrowIfCancellationRequested();

            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            RevealTextAlpha(text, t);
            await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
        }

        SetTextAlpha(text, 255);
    }

    private void ResetSequenceState()
    {
        _step = SequenceStep.WaitingForOpen;
        SetFlipBool(false);
        PrepareAnimatorState(openAnimationStateName);
        HideMemoryImages();
        HideEndingTexts();
        HideReturnPromptInstant();
        _isLoadingReturnScene = false;
    }

    private void PrepareAnimatorState(string stateName)
    {
        if (bookAnimator == null
            || bookAnimator.runtimeAnimatorController == null
            || bookAnimator.layerCount <= 0)
        {
            return;
        }

        string resolvedStateName = ResolveStateName(stateName);
        if (string.IsNullOrWhiteSpace(resolvedStateName))
        {
            return;
        }

        bookAnimator.enabled = true;
        bookAnimator.speed = 0f;
        bookAnimator.Play(Animator.StringToHash(resolvedStateName), 0, 0f);
        bookAnimator.Update(0f);
    }

    private string ResolveStateName(string requestedStateName)
    {
        if (bookAnimator == null || bookAnimator.runtimeAnimatorController == null || bookAnimator.layerCount <= 0)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(requestedStateName))
        {
            string trimmedName = requestedStateName.Trim();
            if (bookAnimator.HasState(0, Animator.StringToHash(trimmedName)))
            {
                return trimmedName;
            }
        }

        foreach (AnimationClip clip in bookAnimator.runtimeAnimatorController.animationClips)
        {
            if (clip == null)
            {
                continue;
            }

            if (bookAnimator.HasState(0, Animator.StringToHash(clip.name)))
            {
                return clip.name;
            }
        }

        return null;
    }

    private float ResolveClipLength(string stateName)
    {
        if (bookAnimator == null || bookAnimator.runtimeAnimatorController == null)
        {
            return 0f;
        }

        foreach (AnimationClip clip in bookAnimator.runtimeAnimatorController.animationClips)
        {
            if (clip == null)
            {
                continue;
            }

            if (string.Equals(clip.name, stateName, StringComparison.Ordinal))
            {
                return clip.length;
            }
        }

        return bookAnimator.GetCurrentAnimatorStateInfo(0).length;
    }

    private void HideMemoryImages()
    {
        if (memoryImages == null)
        {
            return;
        }

        for (int i = 0; i < memoryImages.Length; i++)
        {
            SpriteRenderer spriteRenderer = memoryImages[i];
            if (spriteRenderer == null)
            {
                continue;
            }

            SetSpriteAlpha(spriteRenderer, 0f);
            spriteRenderer.transform.localScale = ResolveMemoryBaseScale(i, spriteRenderer.transform);
            spriteRenderer.gameObject.SetActive(false);
        }
    }

    private void HideEndingTexts()
    {
        if (endingTexts == null)
        {
            return;
        }

        for (int i = 0; i < endingTexts.Length; i++)
        {
            if (endingTexts[i] == null)
            {
                continue;
            }

            endingTexts[i].maxVisibleCharacters = 0;
            SetTextAlpha(endingTexts[i], 0);
            endingTexts[i].gameObject.SetActive(false);
        }
    }

    private async UniTask ShowReturnPromptAsync(CancellationToken cancellationToken)
    {
        if (!showReturnPromptOnComplete)
        {
            return;
        }

        TextMeshProUGUI prompt = ResolveReturnPromptTextView();
        if (prompt == null)
        {
            return;
        }

        prompt.text = returnPromptText;
        prompt.gameObject.SetActive(true);

        CanvasGroup canvasGroup = ResolveReturnPromptCanvasGroup(prompt);
        if (canvasGroup == null)
        {
            _isReturnPromptVisible = true;
            return;
        }

        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        float duration = Mathf.Max(0f, returnPromptFadeDuration);
        if (duration <= 0f)
        {
            canvasGroup.alpha = 1f;
            _isReturnPromptVisible = true;
            return;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            cancellationToken.ThrowIfCancellationRequested();

            elapsed += Time.unscaledDeltaTime;
            canvasGroup.alpha = Mathf.Clamp01(elapsed / duration);
            await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
        }

        canvasGroup.alpha = 1f;
        _isReturnPromptVisible = true;
    }

    private void HideReturnPromptInstant()
    {
        _isReturnPromptVisible = false;

        if (returnPromptCanvasGroup != null)
        {
            returnPromptCanvasGroup.alpha = 0f;
            returnPromptCanvasGroup.interactable = false;
            returnPromptCanvasGroup.blocksRaycasts = false;
        }

        if (returnPromptTextView != null)
        {
            returnPromptTextView.gameObject.SetActive(false);
        }
    }

    private async UniTaskVoid LoadReturnSceneAsync(CancellationToken cancellationToken)
    {
        if (_isLoadingReturnScene || !_isReturnPromptVisible)
        {
            return;
        }

        _isLoadingReturnScene = true;

        try
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.QuitToMenu();
            }

            await _sceneLoader.FadeLoadAsync(returnScene);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private TextMeshProUGUI ResolveReturnPromptTextView()
    {
        if (returnPromptTextView != null)
        {
            return returnPromptTextView;
        }

        returnPromptTextView = CreateReturnPromptTextView();
        return returnPromptTextView;
    }

    private CanvasGroup ResolveReturnPromptCanvasGroup(TextMeshProUGUI prompt)
    {
        if (returnPromptCanvasGroup != null)
        {
            return returnPromptCanvasGroup;
        }

        returnPromptCanvasGroup = prompt != null
            ? prompt.GetComponentInParent<CanvasGroup>(true)
            : null;

        if (returnPromptCanvasGroup == null && prompt != null)
        {
            returnPromptCanvasGroup = prompt.gameObject.AddComponent<CanvasGroup>();
        }

        return returnPromptCanvasGroup;
    }

    private TextMeshProUGUI CreateReturnPromptTextView()
    {
        GameObject canvasObject = new("BookReturnPromptCanvas");
        canvasObject.transform.SetParent(transform, false);

        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200;

        CanvasScaler canvasScaler = canvasObject.AddComponent<CanvasScaler>();
        canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasScaler.referenceResolution = new Vector2(1920f, 1080f);
        canvasObject.AddComponent<GraphicRaycaster>();

        returnPromptCanvasGroup = canvasObject.AddComponent<CanvasGroup>();
        returnPromptCanvasGroup.alpha = 0f;

        GameObject promptObject = new("ReturnPromptText");
        promptObject.transform.SetParent(canvasObject.transform, false);

        RectTransform rectTransform = promptObject.AddComponent<RectTransform>();
        rectTransform.anchorMin = new Vector2(0.5f, 0f);
        rectTransform.anchorMax = new Vector2(0.5f, 0f);
        rectTransform.pivot = new Vector2(0.5f, 0f);
        rectTransform.anchoredPosition = returnPromptAnchoredPosition;
        rectTransform.sizeDelta = new Vector2(760f, 84f);

        TextMeshProUGUI prompt = promptObject.AddComponent<TextMeshProUGUI>();
        prompt.text = returnPromptText;
        prompt.alignment = TextAlignmentOptions.Center;
        prompt.fontSize = returnPromptFontSize;
        prompt.fontStyle = FontStyles.Bold;
        prompt.color = Color.white;
        prompt.raycastTarget = false;

        return prompt;
    }

    private void RevealTextAlpha(TextMeshProUGUI text, float normalizedProgress)
    {
        if (text == null)
        {
            return;
        }

        text.ForceMeshUpdate(true);
        TMP_TextInfo textInfo = text.textInfo;
        if (textInfo == null)
        {
            return;
        }

        int characterCount = textInfo.characterCount;
        float fadeWidth = Mathf.Max(1f, textFadeCharacterWidth);
        float revealPosition = normalizedProgress * (characterCount + fadeWidth);

        for (int i = 0; i < characterCount; i++)
        {
            TMP_CharacterInfo characterInfo = textInfo.characterInfo[i];
            if (!characterInfo.isVisible)
            {
                continue;
            }

            float alphaT = Mathf.Clamp01((revealPosition - i) / fadeWidth);
            alphaT = Mathf.SmoothStep(0f, 1f, alphaT);
            SetCharacterAlpha(textInfo, characterInfo, (byte)Mathf.RoundToInt(alphaT * 255f));
        }

        UpdateTextGeometry(text);
    }

    private void SetTextAlpha(TextMeshProUGUI text, byte alpha)
    {
        if (text == null)
        {
            return;
        }

        text.ForceMeshUpdate(true);
        TMP_TextInfo textInfo = text.textInfo;
        if (textInfo == null)
        {
            return;
        }

        for (int i = 0; i < textInfo.characterCount; i++)
        {
            TMP_CharacterInfo characterInfo = textInfo.characterInfo[i];
            if (characterInfo.isVisible)
            {
                SetCharacterAlpha(textInfo, characterInfo, alpha);
            }
        }

        UpdateTextGeometry(text);
    }

    private void SetCharacterAlpha(TMP_TextInfo textInfo, TMP_CharacterInfo characterInfo, byte alpha)
    {
        int materialIndex = characterInfo.materialReferenceIndex;
        int vertexIndex = characterInfo.vertexIndex;
        if (textInfo == null
            || textInfo.meshInfo == null
            || materialIndex < 0
            || materialIndex >= textInfo.meshInfo.Length)
        {
            return;
        }

        Color32[] vertexColors = textInfo.meshInfo[materialIndex].colors32;
        if (vertexColors == null || vertexIndex + 3 >= vertexColors.Length)
        {
            return;
        }

        vertexColors[vertexIndex + 0].a = alpha;
        vertexColors[vertexIndex + 1].a = alpha;
        vertexColors[vertexIndex + 2].a = alpha;
        vertexColors[vertexIndex + 3].a = alpha;
    }

    private void UpdateTextGeometry(TextMeshProUGUI text)
    {
        if (text == null || text.textInfo == null || text.textInfo.meshInfo == null)
        {
            return;
        }

        TMP_TextInfo textInfo = text.textInfo;
        for (int i = 0; i < textInfo.meshInfo.Length; i++)
        {
            TMP_MeshInfo meshInfo = textInfo.meshInfo[i];
            if (meshInfo.mesh == null || meshInfo.colors32 == null)
            {
                continue;
            }

            meshInfo.mesh.colors32 = meshInfo.colors32;
            text.UpdateGeometry(meshInfo.mesh, i);
        }
    }

    private void SetAllMemoryAlpha(float alpha)
    {
        if (memoryImages == null)
        {
            return;
        }

        for (int i = 0; i < memoryImages.Length; i++)
        {
            if (memoryImages[i] != null)
            {
                SetSpriteAlpha(memoryImages[i], alpha);
            }
        }
    }

    private void SetSpriteAlpha(SpriteRenderer spriteRenderer, float alpha)
    {
        Color color = spriteRenderer.color;
        color.a = alpha;
        spriteRenderer.color = color;
    }

    private void SetFlipBool(bool value)
    {
        if (bookAnimator == null || string.IsNullOrWhiteSpace(flipBoolParameter))
        {
            return;
        }

        foreach (AnimatorControllerParameter parameter in bookAnimator.parameters)
        {
            if (parameter.type == AnimatorControllerParameterType.Bool
                && string.Equals(parameter.name, flipBoolParameter, StringComparison.Ordinal))
            {
                bookAnimator.SetBool(flipBoolParameter, value);
                return;
            }
        }
    }

    private bool WasPrimaryClickPressed()
    {
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            return true;
        }

        if (Touchscreen.current != null
            && Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
        {
            return true;
        }

        Keyboard keyboard = Keyboard.current;
        return keyboard != null
            && (keyboard.spaceKey.wasPressedThisFrame
                || keyboard.enterKey.wasPressedThisFrame
                || keyboard.numpadEnterKey.wasPressedThisFrame
                || keyboard.escapeKey.wasPressedThisFrame);
    }

    private void ResolveReferences()
    {
        if (bookAnimator == null)
        {
            bookAnimator = GetComponent<Animator>();
        }
    }

    private void CacheAnimatorSpeed()
    {
        if (bookAnimator == null)
        {
            return;
        }

        _cachedAnimatorSpeed = bookAnimator.speed;
    }

    private void CacheMemoryScales()
    {
        if (memoryImages == null)
        {
            return;
        }

        int count = Mathf.Min(_memoryBaseScales.Length, memoryImages.Length);
        for (int i = 0; i < count; i++)
        {
            if (memoryImages[i] != null)
            {
                _memoryBaseScales[i] = memoryImages[i].transform.localScale;
            }
        }
    }

    private Vector3 ResolveMemoryBaseScale(int imageIndex, Transform imageTransform)
    {
        if (imageIndex >= 0
            && imageIndex < _memoryBaseScales.Length
            && _memoryBaseScales[imageIndex] != Vector3.zero)
        {
            return _memoryBaseScales[imageIndex];
        }

        return imageTransform != null ? imageTransform.localScale : Vector3.one;
    }

    private void CancelPlayback()
    {
        if (_playCts == null)
        {
            return;
        }

        _playCts.Cancel();
        _playCts.Dispose();
        _playCts = null;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        ResolveReferences();
    }
#endif
}
