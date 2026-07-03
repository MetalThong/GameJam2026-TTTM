using UnityEngine;
using UnityEngine.InputSystem;

public sealed class DialogueTestRunner : MonoBehaviour
{
    [Header("Input")]
    [SerializeField] private Key triggerKey = Key.Enter;

    [Header("Test Content")]
    [SerializeField] private DialogueLine[] testLines;

    private DialogueView _view;
    private int _lineIndex = -1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureRunner()
    {
        if (FindAnyObjectByType<DialogueTestRunner>() != null)
        {
            return;
        }

        GameObject runnerObject = new GameObject(nameof(DialogueTestRunner));
        DontDestroyOnLoad(runnerObject);
        runnerObject.AddComponent<DialogueTestRunner>();
    }

    private void Awake()
    {
        _view = gameObject.AddComponent<DialogueView>();

        Sprite background = LoadFirstSprite("Dialogue/Green");
        Sprite portrait = LoadFirstSprite("Dialogue/Khong_Co_Tieu_e51_20260318172540");
        _view.SetFallbackSprites(background, portrait);

        if (testLines == null || testLines.Length == 0)
        {
            testLines = new[]
            {
                new DialogueLine("Xuan", "Day la dialogue test. Nhan Enter de chuyen sang cau tiep theo.", portrait),
                new DialogueLine("Meo", "Khung nen, chan dung, ten nhan vat va noi dung da duoc can trong mot panel rieng.", portrait),
                new DialogueLine("Xuan", "Het doan test thi panel se an di. Sau nay co the thay noi dung nay bang du lieu that.", portrait)
            };
        }
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null || (!keyboard[triggerKey].wasPressedThisFrame && !keyboard.numpadEnterKey.wasPressedThisFrame))
        {
            return;
        }

        Advance();
    }

    private void Advance()
    {
        if (!_view.IsVisible)
        {
            _lineIndex = 0;
            _view.Show(testLines[_lineIndex]);
            return;
        }

        _lineIndex++;
        if (_lineIndex >= testLines.Length)
        {
            _lineIndex = -1;
            _view.Hide();
            return;
        }

        _view.Show(testLines[_lineIndex]);
    }

    private static Sprite LoadFirstSprite(string resourcePath)
    {
        Sprite sprite = Resources.Load<Sprite>(resourcePath);
        if (sprite != null)
        {
            return sprite;
        }

        Sprite[] sprites = Resources.LoadAll<Sprite>(resourcePath);
        return sprites != null && sprites.Length > 0 ? sprites[0] : null;
    }
}
