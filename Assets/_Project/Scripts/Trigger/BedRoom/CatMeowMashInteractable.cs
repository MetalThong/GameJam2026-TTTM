using UnityEngine;

public class CatMeowMashInteractable : MashStoryInteractable
{
    // [SerializeField] private DialogConfig ...;

    protected override void OnInteractSucceeded()
    {
        // AudioManager.Instance.PlaySfx("cat_meow");
        // await DialogueManager.Instance.PlayAsync(dialogConfig);
        ExecuteAction();
        gameObject.SetActive(false);
    }
}
