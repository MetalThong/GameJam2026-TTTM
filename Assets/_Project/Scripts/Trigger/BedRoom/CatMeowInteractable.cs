// Cat meow interaction: plays a DialogueSO (and optional SFX) on success, then runs the flag action.
// All behavior comes from DialogueStoryInteractable; this thin type keeps a scene-friendly name.
public class CatMeowInteractable : DialogueStoryInteractable
{
    protected override void OnInteractSucceeded()
    {
        AudioFeedback.PlayCatMeow();
        base.OnInteractSucceeded();
    }
}
