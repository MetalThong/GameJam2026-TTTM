using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;

// Scene-scoped dialogue driver. Plays a DialogueSO line by line, advancing on the Interact press.
// Not a global singleton on purpose: RULE.md discourages new singletons, so callers hold a
// serialized reference to the scene DialogueManager instead.
public sealed class DialogueManager : MonoBehaviour
{
    [Header("Scene References")]
    [SerializeField] private DialogueView dialogueView;
    [SerializeField] private InteractionPromptView interactionPromptView;

    [Header("Input")]
    [Tooltip("Key used to reveal-skip and advance dialogue lines.")]
    [SerializeField] private Key advanceKey = Key.E;
    [SerializeField] private string advancePromptKey = "prompt.continue";

    public bool IsPlaying { get; private set; }

    private CancellationTokenSource _lineRevealCts;

    public void SetDialogueView(DialogueView view)
    {
        dialogueView = view;
    }

    private void Awake()
    {
        if (dialogueView == null)
        {
            TryGetComponent(out dialogueView);
        }

        if (interactionPromptView == null)
        {
            interactionPromptView = Object.FindFirstObjectByType<InteractionPromptView>(FindObjectsInactive.Include);
        }
    }

    private void Start()
    {
        if (dialogueView != null)
        {
            dialogueView.Hide();
        }
    }

    public void PlayDialogue(DialogueSO dialogue)
    {
        PlayDialogueAsync(dialogue).Forget();
    }

    // Plays the whole dialogue and returns when the last line is dismissed.
    // Re-entrant calls are ignored while a dialogue is already playing.
    public async UniTask PlayDialogueAsync(DialogueSO dialogue, CancellationToken cancellationToken = default)
    {
        if (IsPlaying)
        {
            return;
        }

        if (dialogueView == null)
        {
            Debug.LogWarning("DialogueManager: no DialogueView assigned.", this);
            return;
        }

        if (dialogue == null || !dialogue.HasLines)
        {
            Debug.LogWarning("DialogueManager: dialogue is null or has no lines.", this);
            return;
        }

        IsPlaying = true;
        GameState previousState = GameState.Playing;
        bool shouldRestoreGameState = false;

        if (GameManager.Instance != null)
        {
            previousState = GameManager.Instance.CurrentState;
            shouldRestoreGameState = true;

            if (previousState != GameState.OnDialog)
            {
                GameManager.Instance.SetState(GameState.OnDialog);
            }
        }

        try
        {
            ShowAdvancePrompt();

            if (dialogue.Background != null)
            {
                dialogueView.SetBackground(dialogue.Background);
            }

            IReadOnlyList<DialogueLine> lines = dialogue.Lines;
            for (int i = 0; i < lines.Count; i++)
            {
                await PlayLineAsync(lines[i], cancellationToken);
            }
        }
        finally
        {
            HideAdvancePrompt();
            dialogueView.Hide();
            IsPlaying = false;

            if (shouldRestoreGameState && GameManager.Instance != null)
            {
                GameManager.Instance.SetState(previousState == GameState.OnDialog ? GameState.Playing : previousState);
            }
        }
    }

    private async UniTask PlayLineAsync(DialogueLine line, CancellationToken cancellationToken)
    {
        _lineRevealCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        try
        {
            UniTask revealTask = dialogueView.ShowAsync(line, _lineRevealCts.Token);

            // First press: if still revealing, snap to full instead of advancing.
            await WaitForAdvancePressAsync(cancellationToken);

            if (dialogueView.IsRevealing && !dialogueView.IsLineFullyVisible)
            {
                dialogueView.CompleteReveal();
                _lineRevealCts.Cancel();
                await revealTask.SuppressCancellationThrow();

                // Second press: advance to the next line.
                await WaitForAdvancePressAsync(cancellationToken);
            }
            else
            {
                await revealTask.SuppressCancellationThrow();
            }
        }
        finally
        {
            _lineRevealCts.Dispose();
            _lineRevealCts = null;
        }
    }

    // Waits until the advance key is pressed. Skips the frame of the press that opened the panel.
    private async UniTask WaitForAdvancePressAsync(CancellationToken cancellationToken)
    {
        await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);

        while (!WasAdvancePressed())
        {
            await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
        }
    }

    private bool WasAdvancePressed()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return false;
        }

        return keyboard[advanceKey].wasPressedThisFrame;
    }

    private void ShowAdvancePrompt()
    {
        InteractionPromptView promptView = ResolvePromptView();
        if (promptView != null)
        {
            promptView.Show(this, advancePromptKey);
        }
    }

    private void HideAdvancePrompt()
    {
        if (interactionPromptView != null)
        {
            interactionPromptView.Hide(this);
        }
    }

    private InteractionPromptView ResolvePromptView()
    {
        if (interactionPromptView != null)
        {
            return interactionPromptView;
        }

        interactionPromptView = Object.FindFirstObjectByType<InteractionPromptView>(FindObjectsInactive.Include);
        return interactionPromptView;
    }
}
