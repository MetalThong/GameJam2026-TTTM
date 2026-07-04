using TMPro;
using UnityEngine;

public sealed class InteractionPromptView : MonoBehaviour
{
    [SerializeField] private GameObject root;
    [SerializeField] private TMP_Text label;
    [SerializeField] private string defaultPromptKey = "prompt.interact";

    private Component _owner;
    private string _currentPromptKey;

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
        RefreshLabel();
        SetVisible(true);
    }

    public void Hide(Component owner)
    {
        if (owner != null && _owner != null && owner != _owner)
        {
            return;
        }

        _owner = null;
        _currentPromptKey = null;
        SetVisible(false);
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

        string key = string.IsNullOrWhiteSpace(_currentPromptKey) ? defaultPromptKey : _currentPromptKey;
        string action = LocalizationManager.Instance != null
            ? LocalizationManager.Instance.Get(key)
            : key;

        label.text = $"E : {VietnameseTextUtility.Normalize(action)}";
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
