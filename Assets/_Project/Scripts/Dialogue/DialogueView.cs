using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class DialogueView : MonoBehaviour
{
    [Header("Scene References")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Image portraitImage;
    [SerializeField] private TMP_Text speakerNameText;
    [SerializeField] private TMP_Text bodyText;
    [SerializeField] private Sprite defaultPortrait;

    [Header("Typewriter")]
    [SerializeField] private bool useTypewriter = true;
    [Tooltip("Characters revealed per second while the typewriter effect runs.")]
    [SerializeField] private float charactersPerSecond = 40f;

    public bool IsVisible => panelRoot != null && panelRoot.activeSelf;
    public bool IsRevealing { get; private set; }
    public bool IsLineFullyVisible =>
        !IsRevealing
        || bodyText == null
        || _visibleCharacterCount <= 0
        || bodyText.maxVisibleCharacters >= _visibleCharacterCount;

    private string _fullBodyText = string.Empty;
    private int _visibleCharacterCount;
    private DialogueLine _currentLine;
    private LocalizationManager _localizationManager;

    public void SetReferences(
        GameObject root,
        Image background,
        Image portrait,
        TMP_Text speakerName,
        TMP_Text body,
        Sprite fallbackPortrait)
    {
        panelRoot = root;
        backgroundImage = background;
        portraitImage = portrait;
        speakerNameText = speakerName;
        bodyText = body;
        defaultPortrait = fallbackPortrait;
    }

    private void OnEnable()
    {
        TrySubscribeLocalization();
    }

    private void OnDisable()
    {
        UnsubscribeLocalization();
    }

    // Instant show, no typewriter. Kept for simple callers and prototype flows.
    public void Show(DialogueLine line)
    {
        if (!PrepareLine(line))
        {
            return;
        }

        bodyText.text = _fullBodyText;
        bodyText.maxVisibleCharacters = int.MaxValue;
        IsRevealing = false;
    }

    // Reveals the line with the typewriter effect. Completes early if CompleteReveal is called.
    public async UniTask ShowAsync(DialogueLine line, CancellationToken cancellationToken)
    {
        if (!PrepareLine(line))
        {
            return;
        }

        if (!useTypewriter || charactersPerSecond <= 0f)
        {
            bodyText.text = _fullBodyText;
            bodyText.maxVisibleCharacters = int.MaxValue;
            IsRevealing = false;
            return;
        }

        IsRevealing = true;
        bodyText.text = _fullBodyText;
        bodyText.maxVisibleCharacters = 0;

        // TMP needs the mesh generated before per-character reveal is accurate.
        bodyText.ForceMeshUpdate();
        _visibleCharacterCount = bodyText.textInfo.characterCount;

        float revealed = 0f;
        while (IsRevealing && bodyText.maxVisibleCharacters < _visibleCharacterCount)
        {
            revealed += charactersPerSecond * Time.deltaTime;
            bodyText.maxVisibleCharacters = Mathf.Clamp(Mathf.FloorToInt(revealed), 0, _visibleCharacterCount);
            if (bodyText.maxVisibleCharacters >= _visibleCharacterCount)
            {
                break;
            }

            await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
        }

        CompleteReveal();
    }

    // Snaps the current line to fully revealed. Safe to call any time.
    public void CompleteReveal()
    {
        if (bodyText != null)
        {
            bodyText.text = _fullBodyText;
            bodyText.maxVisibleCharacters = int.MaxValue;
        }

        IsRevealing = false;
    }

    public void Hide()
    {
        IsRevealing = false;
        _visibleCharacterCount = 0;

        if (panelRoot != null)
        {
            panelRoot.SetActive(false);
        }

        _currentLine = null;
    }

    public void SetBackground(Sprite sprite)
    {
        if (backgroundImage == null)
        {
            return;
        }

        backgroundImage.sprite = sprite;
        backgroundImage.type = Image.Type.Simple;
    }

    // Applies speaker/portrait and caches body text. Returns false when the line cannot be shown.
    private bool PrepareLine(DialogueLine line)
    {
        if (!HasRequiredReferences())
        {
            Debug.LogWarning("DialogueView: missing scene UI references.", this);
            return false;
        }

        if (line == null)
        {
            Hide();
            return false;
        }

        TrySubscribeLocalization();
        _currentLine = line;
        ApplyLineLocalization(line);
        _visibleCharacterCount = 0;

        if (portraitImage != null)
        {
            portraitImage.sprite = line.Portrait != null ? line.Portrait : defaultPortrait;
            portraitImage.enabled = portraitImage.sprite != null;
        }

        panelRoot.SetActive(true);
        return true;
    }

    private void ApplyLineLocalization(DialogueLine line)
    {
        LocalizationManager localizationManager = LocalizationManager.Instance;
        string speakerName = line.GetSpeakerName(localizationManager);
        string body = line.GetText(localizationManager);

        speakerNameText.text = string.IsNullOrWhiteSpace(speakerName)
            ? "???"
            : VietnameseTextUtility.Normalize(speakerName);
        _fullBodyText = string.IsNullOrWhiteSpace(body) ? string.Empty : VietnameseTextUtility.Normalize(body);
        bodyText.text = _fullBodyText;
    }

    private void OnLanguageChanged(Language language)
    {
        RefreshCurrentLineLocalization();
    }

    private void RefreshCurrentLineLocalization()
    {
        if (_currentLine == null || !IsVisible || !HasRequiredReferences())
        {
            return;
        }

        int currentVisibleCharacters = bodyText.maxVisibleCharacters;
        ApplyLineLocalization(_currentLine);
        bodyText.ForceMeshUpdate();
        _visibleCharacterCount = bodyText.textInfo.characterCount;
        bodyText.maxVisibleCharacters = IsRevealing
            ? Mathf.Clamp(currentVisibleCharacters, 0, _visibleCharacterCount)
            : int.MaxValue;
    }

    private void TrySubscribeLocalization()
    {
        if (_localizationManager != null || LocalizationManager.Instance == null)
        {
            return;
        }

        _localizationManager = LocalizationManager.Instance;
        _localizationManager.LanguageChanged += OnLanguageChanged;
    }

    private void UnsubscribeLocalization()
    {
        if (_localizationManager == null)
        {
            return;
        }

        _localizationManager.LanguageChanged -= OnLanguageChanged;
        _localizationManager = null;
    }

    private bool HasRequiredReferences()
    {
        return panelRoot != null
            && speakerNameText != null
            && bodyText != null;
    }
}
