using UnityEngine;

public class CatMeowInteractable : StoryInteractable
{
    // [SerializeField] private DialogConfig ...;

    protected override void OnInteractSucceeded()
    {
        // AudioManager.Instance.PlaySfx("cat_meow");
        // await DialogueManager.Instance.PlayAsync(dialogConfig);
        ExecuteAction();
    }
}
