using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class DialogueView : MonoBehaviour
{
    private const float ReferenceWidth = 1920f;
    private const float ReferenceHeight = 1080f;

    [Header("Sprites")]
    [SerializeField] private Sprite backgroundSprite;
    [SerializeField] private Sprite defaultPortrait;

    [Header("Layout")]
    [SerializeField] private Vector2 panelSize = new Vector2(1240f, 280f);
    [SerializeField] private Vector2 panelOffset = new Vector2(0f, 58f);
    [SerializeField] private Vector2 portraitSize = new Vector2(190f, 210f);

    private GameObject _root;
    private Image _backgroundImage;
    private Image _portraitImage;
    private TMP_Text _speakerNameText;
    private TMP_Text _bodyText;

    public bool IsVisible => _root != null && _root.activeSelf;

    private void Awake()
    {
        Build();
        Hide();
    }

    public void SetFallbackSprites(Sprite background, Sprite portrait)
    {
        if (backgroundSprite == null)
        {
            backgroundSprite = background;
        }

        if (defaultPortrait == null)
        {
            defaultPortrait = portrait;
        }

        ApplyBackgroundSprite();
    }

    public void Show(DialogueLine line)
    {
        if (line == null)
        {
            Hide();
            return;
        }

        _speakerNameText.text = string.IsNullOrWhiteSpace(line.SpeakerName) ? "???" : line.SpeakerName;
        _bodyText.text = string.IsNullOrWhiteSpace(line.Text) ? string.Empty : line.Text;
        _portraitImage.sprite = line.Portrait != null ? line.Portrait : defaultPortrait;
        _portraitImage.enabled = _portraitImage.sprite != null;
        _root.SetActive(true);
    }

    public void Hide()
    {
        if (_root != null)
        {
            _root.SetActive(false);
        }
    }

    private void Build()
    {
        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(ReferenceWidth, ReferenceHeight);
        scaler.matchWidthOrHeight = 0.5f;

        gameObject.AddComponent<GraphicRaycaster>();

        _root = CreateRect("DialoguePanel", transform, Vector2.zero, Vector2.zero, Vector2.one).gameObject;
        RectTransform panel = (RectTransform)_root.transform;
        panel.anchorMin = new Vector2(0.5f, 0f);
        panel.anchorMax = new Vector2(0.5f, 0f);
        panel.pivot = new Vector2(0.5f, 0f);
        panel.sizeDelta = panelSize;
        panel.anchoredPosition = panelOffset;

        _backgroundImage = _root.AddComponent<Image>();
        _backgroundImage.color = new Color(0.08f, 0.08f, 0.09f, 0.94f);
        ApplyBackgroundSprite();

        RectTransform portraitFrame = CreateRect("PortraitFrame", panel, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f));
        portraitFrame.sizeDelta = portraitSize + new Vector2(18f, 18f);
        portraitFrame.anchoredPosition = new Vector2(64f, 0f);
        Image portraitFrameImage = portraitFrame.gameObject.AddComponent<Image>();
        portraitFrameImage.color = new Color(0.02f, 0.02f, 0.025f, 0.82f);

        RectTransform portrait = CreateRect("Portrait", portraitFrame, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
        portrait.offsetMin = new Vector2(9f, 9f);
        portrait.offsetMax = new Vector2(-9f, -9f);
        _portraitImage = portrait.gameObject.AddComponent<Image>();
        _portraitImage.preserveAspect = true;

        RectTransform nameRoot = CreateRect("SpeakerName", panel, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f));
        nameRoot.sizeDelta = new Vector2(360f, 52f);
        nameRoot.anchoredPosition = new Vector2(276f, -34f);
        _speakerNameText = CreateText(nameRoot, 34f, FontStyles.Bold, TextAlignmentOptions.Left);
        _speakerNameText.color = new Color(1f, 0.95f, 0.78f, 1f);

        RectTransform bodyRoot = CreateRect("BodyText", panel, Vector2.zero, Vector2.one, new Vector2(0f, 1f));
        bodyRoot.offsetMin = new Vector2(276f, 42f);
        bodyRoot.offsetMax = new Vector2(-74f, -88f);
        _bodyText = CreateText(bodyRoot, 30f, FontStyles.Normal, TextAlignmentOptions.TopLeft);
        _bodyText.color = Color.white;
        _bodyText.textWrappingMode = TextWrappingModes.Normal;
        _bodyText.overflowMode = TextOverflowModes.Ellipsis;
        _bodyText.lineSpacing = 10f;
    }

    private void ApplyBackgroundSprite()
    {
        if (_backgroundImage == null)
        {
            return;
        }

        _backgroundImage.sprite = backgroundSprite;
        _backgroundImage.type = Image.Type.Simple;
    }

    private static RectTransform CreateRect(string objectName, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot)
    {
        GameObject rectObject = new GameObject(objectName, typeof(RectTransform));
        rectObject.transform.SetParent(parent, false);

        RectTransform rectTransform = (RectTransform)rectObject.transform;
        rectTransform.anchorMin = anchorMin;
        rectTransform.anchorMax = anchorMax;
        rectTransform.pivot = pivot;
        rectTransform.anchoredPosition = Vector2.zero;
        rectTransform.sizeDelta = Vector2.zero;

        return rectTransform;
    }

    private static TMP_Text CreateText(RectTransform parent, float fontSize, FontStyles fontStyle, TextAlignmentOptions alignment)
    {
        TextMeshProUGUI text = parent.gameObject.AddComponent<TextMeshProUGUI>();
        text.fontSize = fontSize;
        text.fontStyle = fontStyle;
        text.alignment = alignment;
        text.raycastTarget = false;

        return text;
    }
}
