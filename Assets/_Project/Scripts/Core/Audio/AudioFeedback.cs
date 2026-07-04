using UnityEngine.UI;

public static class AudioFeedback
{
    public const string ButtonClickId = "button_click";
    public const string CatMeowId = "cat_meow";

    public static void AddButtonClick(Button button)
    {
        if (button != null)
        {
            button.onClick.AddListener(PlayButtonClick);
        }
    }

    public static void RemoveButtonClick(Button button)
    {
        if (button != null)
        {
            button.onClick.RemoveListener(PlayButtonClick);
        }
    }

    public static void PlayButtonClick()
    {
        PlaySfx(ButtonClickId);
    }

    public static void PlayCatMeow()
    {
        PlaySfx(CatMeowId);
    }

    public static void PlaySfx(string id)
    {
        if (AudioManager.Instance == null || string.IsNullOrWhiteSpace(id))
        {
            return;
        }

        AudioManager.Instance.PlaySfx(id);
    }
}
