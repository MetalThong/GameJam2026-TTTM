using Cysharp.Threading.Tasks;
using UnityEngine;

public class CatMeowMashInteractable : MashStoryInteractable
{
    [Header("Dialogue")]
    [SerializeField] private DialogueManager dialogueManager;
    [SerializeField] private DialogueSO dialogue;
    [SerializeField] private string meowSfxId = "cat_meow";

    private bool _isPlaying;

    protected override void OnInteractSucceeded()
    {
        if (_isPlaying)
        {
            return;
        }

        PlaySequenceAsync().Forget();
    }

    private async UniTaskVoid PlaySequenceAsync()
    {
        _isPlaying = true;

        try
        {
            if (AudioManager.Instance != null && !string.IsNullOrWhiteSpace(meowSfxId))
            {
                AudioManager.Instance.PlaySfx(meowSfxId);
            }

            // Play dialogue before deactivating: DialogueManager lives on a separate scene
            // object, so it keeps running even after this GameObject is disabled.
            DialogueManager manager = ResolveDialogueManager();
            if (manager != null && dialogue != null)
            {
                await manager.PlayDialogueAsync(dialogue);
            }
            else if (dialogue != null)
            {
                Debug.LogWarning("CatMeowMashInteractable: dialogue assigned but no DialogueManager was found.", this);
            }

            ExecuteAction();
            gameObject.SetActive(false);
        }
        finally
        {
            _isPlaying = false;
        }
    }

    private DialogueManager ResolveDialogueManager()
    {
        if (dialogueManager != null)
        {
            return dialogueManager;
        }

        dialogueManager = Object.FindFirstObjectByType<DialogueManager>(FindObjectsInactive.Include);
        return dialogueManager;
    }
}
