using TMPro;
using UnityEngine;

public sealed class InteractionPromptView : MonoBehaviour
{
    [SerializeField] private GameObject root;
    [SerializeField] private TMP_Text label;
    [SerializeField] private string defaultPromptKey = "prompt.interact";

    private Component _owner;
    private string _currentPromptKey;
    private Component _fallbackOwner;
    private string _fallbackPromptKey;

    private void Awake()
    {
        ResolveReferences();
        SetVisible(false);
    }

    private void OnEnable()
    {
        if (LocalizationManager.Instance != null)
        {
            LocalizationManager.Instance.LanguageChanged += OnLanguageChanged;
        }

        RefreshLabel();
    }

    private void OnDisable()
    {
        if (LocalizationManager.Instance != null)
        {
            LocalizationManager.Instance.LanguageChanged -= OnLanguageChanged;
        }
    }

    public void Show(Component owner, string localizationKey)
    {
        if (owner == null)
        {
            return;
        }

        _owner = owner;
        _currentPromptKey = string.IsNullOrWhiteSpace(localizationKey) ? defaultPromptKey : localizationKey;
        RefreshDisplay();
    }

    public void Hide(Component owner)
    {
        if (owner != null && _owner != null && owner != _owner)
        {
            return;
        }

        _owner = null;
        _currentPromptKey = null;
        RefreshDisplay();
    }

    public void ShowFallback(Component owner, string localizationKey)
    {
        if (owner == null)
        {
            return;
        }

        string promptKey = string.IsNullOrWhiteSpace(localizationKey) ? defaultPromptKey : localizationKey;
        if (_fallbackOwner == owner && _fallbackPromptKey == promptKey)
        {
            RefreshDisplay();
            return;
        }

        _fallbackOwner = owner;
        _fallbackPromptKey = promptKey;
        RefreshDisplay();
    }

    public void HideFallback(Component owner)
    {
        if (owner != null && _fallbackOwner != null && owner != _fallbackOwner)
        {
            return;
        }

        _fallbackOwner = null;
        _fallbackPromptKey = null;
        RefreshDisplay();
    }

    private void OnLanguageChanged(Language language)
    {
        RefreshLabel();
    }

    private void RefreshLabel()
    {
        if (label == null)
        {
            return;
        }

        string key = ResolveActivePromptKey();
        string action = LocalizationManager.Instance != null
            ? LocalizationManager.Instance.Get(key)
            : key;

        label.text = $"E : {VietnameseTextUtility.Normalize(action)}";
    }

    private void RefreshDisplay()
    {
        bool hasPrompt = _owner != null || _fallbackOwner != null;
        if (hasPrompt)
        {
            RefreshLabel();
        }

        SetVisible(hasPrompt);
    }

    private string ResolveActivePromptKey()
    {
        string key = _owner != null ? _currentPromptKey : _fallbackPromptKey;
        return string.IsNullOrWhiteSpace(key) ? defaultPromptKey : key;
    }

    private void SetVisible(bool isVisible)
    {
        if (root != null)
        {
            root.SetActive(isVisible);
        }
    }

    private void ResolveReferences()
    {
        if (root == null)
        {
            root = gameObject;
        }

        if (label == null)
        {
            label = GetComponentInChildren<TMP_Text>(true);
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        ResolveReferences();
    }
#endif
}
