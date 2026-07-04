using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public sealed class PostBowlStoreStoryCoordinator : MonoBehaviour
{
    private const string LivingRoomDialogueResourcePath = "Dialogue/SO_Dialogue_LivingRoomCarryBoxToStore";
    private const string StoreRoomDialogueResourcePath = "Dialogue/SO_Dialogue_StoreRoomCleanCobwebsIntro";

    [Header("Scenes")]
    [SerializeField] private SceneId livingRoomScene = SceneId.LivingRoom;
    [SerializeField] private SceneId storeRoomScene = SceneId.StoreRoom;

    [Header("Flags")]
    [SerializeField] private string bowlCompletedFlag = "kitchen_bowl_taken";
    [SerializeField] private string livingRoomHintFlag = "living_room_store_hint_seen";
    [SerializeField] private string storeMissionAssignedFlag = "went_to_store";
    [SerializeField] private string storeMissionCompletedFlag = "store_cobwebs_cleared";
    [SerializeField] private bool allowStoreIntroWithoutLivingRoomHint = true;

    [Header("Dialogue")]
    [SerializeField] private DialogueSO livingRoomDialogue;
    [SerializeField] private DialogueSO storeRoomDialogue;
    [SerializeField, Min(0f)] private float sceneStartDelay = 0.35f;
    [SerializeField, Min(0f)] private float dialogueManagerWaitTimeout = 2f;

    private bool _isScheduled;
    private bool _isPlaying;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureInstance()
    {
        if (FindFirstObjectByType<PostBowlStoreStoryCoordinator>(FindObjectsInactive.Include) != null)
        {
            return;
        }

        GameObject coordinator = new(nameof(PostBowlStoreStoryCoordinator));
        DontDestroyOnLoad(coordinator);
        coordinator.AddComponent<PostBowlStoreStoryCoordinator>();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        EventBus.Subscribe<FlagChangedEvent>(OnFlagChanged);
        EventBus.Subscribe<FlagsLoadedEvent>(OnFlagsLoaded);

        ScheduleTryRun();
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        EventBus.Unsubscribe<FlagChangedEvent>(OnFlagChanged);
        EventBus.Unsubscribe<FlagsLoadedEvent>(OnFlagsLoaded);
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ScheduleTryRun();
    }

    private void OnFlagChanged(FlagChangedEvent eventData)
    {
        if (IsStoryFlag(eventData.FlagId))
        {
            ScheduleTryRun();
        }
    }

    private void OnFlagsLoaded(FlagsLoadedEvent eventData)
    {
        ScheduleTryRun();
    }

    private void ScheduleTryRun()
    {
        if (!isActiveAndEnabled || _isScheduled || _isPlaying)
        {
            return;
        }

        RunScheduledAsync(this.GetCancellationTokenOnDestroy()).Forget();
    }

    private async UniTaskVoid RunScheduledAsync(CancellationToken cancellationToken)
    {
        _isScheduled = true;

        try
        {
            if (sceneStartDelay > 0f)
            {
                await UniTask.Delay(TimeSpan.FromSeconds(sceneStartDelay), cancellationToken: cancellationToken);
            }

            await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
            await TryRunForCurrentSceneAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            _isScheduled = false;
        }
    }

    private async UniTask TryRunForCurrentSceneAsync(CancellationToken cancellationToken)
    {
        if (_isPlaying || FlagManager.Instance == null)
        {
            return;
        }

        Scene activeScene = SceneManager.GetActiveScene();
        if (!activeScene.IsValid())
        {
            return;
        }

        if (IsScene(activeScene, livingRoomScene))
        {
            await TryPlayLivingRoomHintAsync(cancellationToken);
            return;
        }

        if (IsScene(activeScene, storeRoomScene))
        {
            await TryPlayStoreRoomIntroAsync(cancellationToken);
        }
    }

    private async UniTask TryPlayLivingRoomHintAsync(CancellationToken cancellationToken)
    {
        if (!ShouldPlayLivingRoomHint())
        {
            return;
        }

        await PlayDialogueThenSetFlagAsync(
            ResolveLivingRoomDialogue(),
            livingRoomHintFlag,
            ShouldPlayLivingRoomHint,
            cancellationToken);
    }

    private async UniTask TryPlayStoreRoomIntroAsync(CancellationToken cancellationToken)
    {
        if (!ShouldPlayStoreRoomIntro())
        {
            return;
        }

        await PlayDialogueThenSetFlagAsync(
            ResolveStoreRoomDialogue(),
            storeMissionAssignedFlag,
            ShouldPlayStoreRoomIntro,
            cancellationToken);
    }

    private async UniTask PlayDialogueThenSetFlagAsync(
        DialogueSO dialogue,
        string flagAfterDialogue,
        Func<bool> shouldStillPlay,
        CancellationToken cancellationToken)
    {
        _isPlaying = true;

        try
        {
            DialogueManager manager = await ResolveDialogueManagerAsync(cancellationToken);
            if (manager == null || dialogue == null)
            {
                Debug.LogWarning("PostBowlStoreStoryCoordinator: cannot play story dialogue because DialogueManager or DialogueSO is missing.", this);
                return;
            }

            while (manager.IsPlaying)
            {
                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
            }

            if (!shouldStillPlay())
            {
                return;
            }

            await manager.PlayDialogueAsync(dialogue, cancellationToken);

            if (shouldStillPlay() && FlagManager.Instance != null)
            {
                FlagManager.Instance.SetFlag(flagAfterDialogue, true);
            }
        }
        finally
        {
            _isPlaying = false;
            ScheduleTryRun();
        }
    }

    private async UniTask<DialogueManager> ResolveDialogueManagerAsync(CancellationToken cancellationToken)
    {
        DialogueManager manager = null;
        float elapsed = 0f;

        while (manager == null && elapsed <= dialogueManagerWaitTimeout)
        {
            manager = FindFirstObjectByType<DialogueManager>(FindObjectsInactive.Include);
            if (manager != null)
            {
                return manager;
            }

            await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
            elapsed += Time.unscaledDeltaTime;
        }

        return null;
    }

    private DialogueSO ResolveLivingRoomDialogue()
    {
        if (livingRoomDialogue == null)
        {
            livingRoomDialogue = Resources.Load<DialogueSO>(LivingRoomDialogueResourcePath);
        }

        return livingRoomDialogue;
    }

    private DialogueSO ResolveStoreRoomDialogue()
    {
        if (storeRoomDialogue == null)
        {
            storeRoomDialogue = Resources.Load<DialogueSO>(StoreRoomDialogueResourcePath);
        }

        return storeRoomDialogue;
    }

    private bool ShouldPlayLivingRoomHint()
    {
        return HasFlag(bowlCompletedFlag)
            && !HasFlag(livingRoomHintFlag)
            && !HasFlag(storeMissionAssignedFlag)
            && !HasFlag(storeMissionCompletedFlag);
    }

    private bool ShouldPlayStoreRoomIntro()
    {
        return HasFlag(bowlCompletedFlag)
            && !HasFlag(storeMissionAssignedFlag)
            && !HasFlag(storeMissionCompletedFlag)
            && (allowStoreIntroWithoutLivingRoomHint || HasFlag(livingRoomHintFlag));
    }

    private bool HasFlag(string flagId)
    {
        return !string.IsNullOrWhiteSpace(flagId)
            && FlagManager.Instance != null
            && FlagManager.Instance.HasFlag(flagId);
    }

    private bool IsStoryFlag(string flagId)
    {
        return flagId == bowlCompletedFlag
            || flagId == livingRoomHintFlag
            || flagId == storeMissionAssignedFlag
            || flagId == storeMissionCompletedFlag;
    }

    private static bool IsScene(Scene scene, SceneId sceneId)
    {
        return scene.IsValid() && scene.name == sceneId.ToString();
    }
}
