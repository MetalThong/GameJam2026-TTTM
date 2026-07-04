# Architecture

This document describes the current architecture of `GameJam2026-TTTM` based on the code and Unity assets in the repository.

This is a living document. Update it whenever project structure, scenes, core systems, services, feature boundaries, dependencies, or important runtime flows change.

For coding and architecture rules, read `RULE.md`.

## High-Level Shape

The project is a Unity 2D game using a small persistent core layer and scene-specific content. Startup begins in `Bootstrap.unity`, creates a persistent root prefab, initializes global managers, then loads the configured start scene.


```text
Bootstrap.unity
  Bootstraper GameObject
    Bootstrapper
      -> Instantiate PersistantRoot.prefab if needed
      -> GameManager.Initialize()
      -> SceneLoader.LoadSceneAsync(startScene)

PersistantRoot.prefab
  PersistantRoot
  GameManager
  AudioManager
  CameraManager
  SaveManager
  InputManager
  FlagManager
  LocalizationManager
  UIManager
  PanelCanvas
```

The current start scene is `SceneId.MainMenu`, mapped to `Assets/_Project/Scenes/MainMenu.unity`.

## Runtime Lifecycle

1. Unity opens `Assets/_Project/Scenes/Bootstrap.unity`.
2. `Bootstrapper.Start` runs.
3. `Bootstrapper.EnsurePersistantRoot` checks `PersistantRoot.Instance`.
4. If missing, it instantiates `Assets/_Project/Prefabs/Core/PersistantRoot.prefab`.
5. `GameManager.Instance.Initialize()` sets state to `Booting`.
6. `SceneLoader.LoadSceneAsync(startScene)` loads the target scene in `LoadSceneMode.Single`.
7. When load completes, `Bootstrapper` currently sets `GameManager` to `MainMenu`.

## Persistent Root

`PersistantRoot` is a `MonoBehaviour` singleton that calls `DontDestroyOnLoad(gameObject)`. It prevents duplicate persistent roots by destroying later instances.

The prefab currently acts as the cross-scene service container. Managers under it survive scene changes and provide global gameplay infrastructure.

Note: the class and prefab currently use the spelling `PersistantRoot`. Keep references consistent unless doing a deliberate rename across code, prefab, and scene references.

## Core Systems

### Game Management

Files:

- `Assets/_Project/Scripts/Core/Manager/GameManager.cs`
- `Assets/_Project/Scripts/Enum/GameState.cs`

`GameManager` owns the current game state and exposes `StateChanged`. Current states are:

- `Booting`
- `MainMenu`
- `Playing`
- `Paused`
- `GameOver`
- `OnDialog`

Main transitions:

- `Initialize()` -> `Booting`
- `Pause()` -> `Paused` only from `Playing`
- `Resume()` -> `Playing` only from `Paused`
- `RestartRun()` -> `Booting`
- `QuitToMenu()` -> `MainMenu`
- Dialogue, temporary chat, movement-lock story beats, and the Washing minigame use `OnDialog` as a runtime lock state so `Movement` stops while UI/dialogue input still works.

### Scene Loading

Files:

- `Assets/_Project/Scripts/Core/Manager/SceneLoader.cs`
- `Assets/_Project/Scripts/Enum/SceneId.cs`

`SceneLoader` is a plain C# service class, not a `MonoBehaviour`. It loads by `SceneId` or string scene name through `SceneManager.LoadSceneAsync`.

It uses UniTask and an `IsLoading` flag to avoid overlapping loads. When loading by enum, `sceneId.ToString()` is used, so enum names must match scene names.

Current scene ids:

- `MainMenu`
- `BedRoom`
- `HallUp`
- `LivingRoom`
- `HallDown`

Current enabled Build Settings entries are:

1. `Assets/_Project/Scenes/Bootstrap.unity`
2. `Assets/_Project/Scenes/MainMenu.unity`
3. `Assets/_Project/Scenes/BedRoom.unity`
4. `Assets/_Project/Scenes/HallUp.unity`
5. `Assets/_Project/Scenes/HallDown.unity`
6. `Assets/_Project/Scenes/HallUp.unity`
7. `Assets/_Project/Scenes/LivingRoom.unity`
8. `Assets/_Project/Scenes/HallUp.unity`

`Assets/_Project/Scenes/Kitchen.unity` exists, but it is not in `SceneId` and is not currently enabled in Build Settings. `HallUp.unity` currently appears more than once in Build Settings, so scene routing should be cleaned before relying on build-index order.

`SceneLoader.FadeLoadAsync` wraps normal async scene loading with `FadePanel.FadeInAsync` and `FadePanel.FadeOutAsync` when a `FadePanel` exists. If no fade panel is found, it logs a warning and loads without transition.

### Input

Files:

- `Assets/_Project/Scripts/Core/Input/InputManager.cs`
- `Assets/_Project/Scripts/Core/Input/InputReader.cs`
- `Assets/_Project/Scripts/Core/Input/IInputReader.cs`
- `Assets/InputSystem_Actions.inputactions`

`InputManager` is the singleton bridge between Unity and gameplay code. It owns an `InputReader`, which clones the serialized `InputActionAsset` so runtime subscriptions do not mutate the source asset.

`InputReader` expects an action map named `Player` and these actions:

- `Move`
- `Look`
- `Attack`
- `Interact`
- `Crouch`
- `Jump`
- `Previous`
- `Next`
- `Sprint`

Gameplay code should depend on `IInputReader` where possible. It exposes continuous values (`Move`, `Look`), held-state booleans, and press/release events.

### Movement Prototype

Files:

- `Assets/_Project/Scripts/Enum/MovementForm.cs`
- `Assets/_Project/Scripts/CatMovement/Movement.cs`
- `Assets/_Project/Scripts/CatMovement/MovementInput.cs`
- `Assets/_Project/Scripts/CatMovement/MovementFormBehaviour.cs`
- `Assets/_Project/Scripts/CatMovement/CatMovementForm.cs`
- `Assets/_Project/Scripts/CatMovement/GhostMovementForm.cs`
- `Assets/_Project/Scripts/CatMovement/CatInteractor.cs`

`Movement` is the current player movement coordinator. It reads movement through `MovementInput`, switches form through the global `Next` input, local `Next` input, or temporary `T` keyboard fallback, and delegates actual movement behavior to `MovementFormBehaviour` components.

`Movement` can also lock player movement until a story flag is set. When `shouldLockMovementUntilFlag` is enabled, it holds the Rigidbody2D velocity at zero and ignores movement/form-toggle input until `unlockMovementFlag` exists in `FlagManager`.

Current forms:

- `CatMovementForm`: horizontal platform movement with normal gravity.
- `GhostMovementForm`: smooth free movement in the air with gravity disabled while active.

`Movement` requires `Rigidbody2D`, `Animator`, `SpriteRenderer`, `BoxCollider2D`, `MovementInput`, `CatMovementForm`, and `GhostMovementForm`. It resolves missing serialized references in `Awake`/`OnValidate`, disables itself on missing critical references, and logs errors for missing Rigidbody, body collider, or `MovementInput`.

Animation and facing are currently handled directly by `Movement`. It drives Animator bools `IsMoving` and `IsGhost`, snaps the animator to the configured cat/ghost state when form is changed or loaded, flips the `SpriteRenderer` from horizontal input or rigidbody velocity, and mirrors collider offsets when configured.

Collider handling supports either separate cat/ghost `BoxCollider2D` references or one shared body collider with serialized cat/ghost collider profiles. Switching form enables the appropriate collider or applies the selected collider size/offset profile.

`CatInteractor` is the current player interaction bridge. It tracks all overlapping `IInteractable` colliders, refreshes newly enabled colliders through `OnTriggerStay2D`, and tries them from newest to oldest when `E` is pressed. It skips interaction while the scene `DialogueManager` is playing and ignores the frame where dialogue just closed, so the same `E` press only reveals/advances dialogue. `IInteractable.TryInteract()` returns whether an interaction actually ran, letting the interactor skip flag-blocked objects and fall through to the next valid target.

In `BedRoom.unity`, the Cat prefab instance starts as `MovementForm.Ghost` and has movement locked until the WakeUpPanel interaction sets `waked_up`. Interaction remains active while movement is locked so the player can press `E` to wake up.

### Audio

Files:

- `Assets/_Project/Scripts/Core/Audio/AudioManager.cs`
- `Assets/_Project/Scripts/Core/Audio/AudioLibrary.cs`
- `Assets/_Project/Scripts/Core/Audio/AudioEntry.cs`

`AudioManager` is a singleton that creates two `AudioSource` components at runtime:

- music source
- SFX source

It resolves clips from an `AudioLibrary` ScriptableObject by string id. `AudioLibrary` builds a lazy dictionary from serialized `AudioEntry` records.

Supported behavior:

- looped/non-looped music playback
- non-spatial one-shot SFX
- spatial one-shot SFX through temporary GameObjects
- master/music/SFX volume writes to an `AudioMixer`
- music/SFX volume also applies directly to runtime `AudioSource` volume, so settings sliders work even when no mixer asset is assigned yet

Expected mixer parameters:

- `MasterVolume`
- `MusicVolume`
- `SfxVolume`

### Camera

Files:

- `Assets/_Project/Scripts/Core/Camera/CameraManager.cs`
- `Assets/_Project/Scripts/Core/Camera/CameraBounds2D.cs`

`CameraManager` is a Cinemachine-based singleton. It can find a scene `CinemachineCamera`, set follow/look-at targets, adjust orthographic zoom, set priority, prioritize the camera, apply 2D bounds, and generate impulse shake.

It listens to `SceneManager.sceneLoaded` and resolves scene camera references after scene loads.

`CameraBounds2D` registers a `Collider2D` with `CameraManager.Instance.SetBounds`, allowing scene objects to define camera limits.

### Save System

Files:

- `Assets/_Project/Scripts/Core/Save/SaveManager.cs`
- `Assets/_Project/Scripts/Core/Save/SaveFileHandler.cs`
- `Assets/_Project/Scripts/Core/Save/SaveData.cs`
- `Assets/_Project/Scripts/Core/Save/ISaveable.cs`

`SaveManager` is a singleton that owns the current `SaveData` and a `SaveFileHandler`. It can create a new save, load a save, save all active saveables, and delete the save file.

Save flow:

1. `SaveManager` finds active `MonoBehaviour` instances implementing `ISaveable`.
2. On save, each saveable writes into the shared `SaveData`.
3. `SaveFileHandler` serializes `SaveData` to JSON with `JsonUtility`.
4. The save file is stored under `Application.persistentDataPath`.

`SaveManager` does not create a new save file on application quit unless a save file already exists. This prevents opening and closing the main menu from creating empty progress.

Current `SaveData` fields:

- `Coin`
- `PlayerPosition`
- `PlayerSceneName`
- `HasPlayerState`
- `PlayerForm`
- `PlayerFacingRight`
- `PlayerScenePositions`
- `Flags`

`FlagManager` implements `ISaveable`, so story flags are included in the same save file. Flag changes call `SaveManager.SaveGame()` when a save manager is available. On load, `SaveManager` pushes saved flags back into `FlagManager` and publishes `FlagsLoadedEvent` so flag-dependent scene objects can refresh.

`Movement` implements `ISaveable` for the player. It stores the current form, facing direction, current scene name, and the last known Cat position for each scene. `SceneLoadInteractable` saves before loading the next scene, and `SaveManager` reloads flags and active scene saveables after `SceneManager.sceneLoaded`, so returning to a scene can restore the Cat position for that scene while preserving completed story state.

### Story Flags, Triggers, And Interactions

Files:

- `Assets/_Project/Scripts/Trigger/FlagManager.cs`
- `Assets/_Project/Scripts/Trigger/StoryFlagStore.cs`
- `Assets/_Project/Scripts/Trigger/StoryFlagCondition.cs`
- `Assets/_Project/Scripts/Trigger/StoryFlagAction.cs`
- `Assets/_Project/Scripts/Trigger/FlagSaveEntry.cs`
- `Assets/_Project/Scripts/Trigger/StoryFlagId.cs`
- `Assets/_Project/Scripts/Event/FlagChangedEvent.cs`
- `Assets/_Project/Scripts/Trigger/IInteractable.cs`
- `Assets/_Project/Scripts/Trigger/StoryTrigger.cs`
- `Assets/_Project/Scripts/Trigger/StoryInteractable.cs`
- `Assets/_Project/Scripts/Trigger/MashStoryInteractable.cs`
- `Assets/_Project/Scripts/Trigger/DialogueStoryInteractable.cs`
- `Assets/_Project/Scripts/Trigger/MashProgressView.cs`
- `Assets/_Project/Scripts/Trigger/InteractButton.cs`
- `Assets/_Project/Scripts/Trigger/FlagBasedObject.cs`
- `Assets/_Project/Scripts/Trigger/FadeFlagObject.cs`
- `Assets/_Project/Scripts/Trigger/DeactivateOnFlag.cs`
- `Assets/_Project/Scripts/Trigger/Hall/TemporaryChat.cs`
- `Assets/_Project/Scripts/Trigger/BedRoom/CatMeowTrigger.cs`
- `Assets/_Project/Scripts/Trigger/BedRoom/CatMeowInteractable.cs`
- `Assets/_Project/Scripts/Trigger/BedRoom/CatMeowMashInteractable.cs`
- `Assets/_Project/Scripts/Trigger/LivingRoom/LivingRoomChatTransformSequence.cs`
- `Assets/_Project/Scripts/Trigger/LivingRoom/LivingToyBoxDropInteractable.cs`
- `Assets/_Project/Scripts/Trigger/LivingRoom/OwnerBoxPickupSequence.cs`
- `Assets/_Project/Scripts/Trigger/LivingRoom/PushFlagObject.cs`

The story system is flag-driven. `FlagManager` is the current singleton owner for a `StoryFlagStore`, exposes `HasFlag` and `SetFlag`, publishes `FlagChangedEvent` when a flag value changes, and saves after flag changes when `SaveManager` exists.

`StoryFlagStore` stores flags as string/bool pairs. Empty or whitespace flag ids are ignored on set/remove, and invalid save entries are skipped on load. `StoryFlagId` currently defines `GameStart = "game_start"` as a shared constant.

`StoryFlagCondition` gates behavior with two lists:

- `requiredFlags`: every listed flag must be true.
- `blockedFlags`: every listed flag must be false or absent.

`StoryFlagAction` applies story results with two lists:

- `setFlags`: each listed flag is set true.
- `unsetFlags`: each listed flag is set false.

`StoryTrigger` runs from `OnTriggerEnter2D` when the entering collider has the `Player` tag. It checks `FlagManager`, optional one-shot completion flag, and `StoryFlagCondition`, then executes `StoryFlagAction`. One-shot triggers write `trigger_completed_{triggerId}`.

`StoryInteractable` implements `IInteractable`. It follows the same condition/action model as `StoryTrigger`, but is activated by interaction code instead of trigger entry. It refreshes its colliders whenever story flags load or change, so condition-blocked or one-shot-completed interactables cannot be called again after returning to a scene. `TryInteract()` returns false when flags, one-shot completion, or `CanInteract()` block the interaction; it returns true only after `Interact()` starts. One-shot interactables write `interact_completed_{interactId}`.

`MashStoryInteractable` extends `StoryInteractable` for repeated presses. It increments progress by one per successful interact, decays progress after `decayDelay` by `decayPerSecond`, succeeds when progress reaches `requiredPressCount`, and can reset progress after success. It exposes `NormalizedProgress` (0..1) and the `Pressed`, `ProgressChanged`, and `Succeeded` events so views can render mash feedback without owning the mash state.

`MashProgressView` renders that feedback. It lives on the same GameObject as a `MashStoryInteractable` (`RequireComponent`), subscribes to its events, and serializes `visualRoot`, `hideWhenEmpty`, `fillRenderer`, and `emoteTransform`. If any references are not assigned, it can resolve `visualRoot` to its own GameObject, choose a child `SpriteRenderer` named like a full bar (for example `ProgressBarFull`) as the fill, and use the visual root transform as the punch target. It fills the bar by scaling X from 0 to the renderer's original local scale, lerps color from warning yellow/orange toward red, punch-scales on each press (DOTween), snaps to full with a stronger punch on success, and can hide the visual root when progress decays back to zero.

`InteractButton` is a simple prompt helper. It toggles an assigned button/prompt GameObject when a `Player` tagged collider enters or exits its trigger. It hides the prompt in `Awake` so a button left active in the scene does not show until the player enters the trigger.

`FlagBasedObject` listens to `FlagChangedEvent` and `FlagsLoadedEvent` and toggles an assigned target based on a required flag plus an optional blocked flag. The target is active only when the required flag condition is met and the blocked flag is absent, then inverted when `activeWhenFlagExists` is false. The listener object should stay active; if `target == gameObject` and the target would be disabled, it warns and refuses to disable itself so future flag events are not lost.

`FadeFlagObject` extends `FlagBasedObject` by fading a target `SpriteRenderer` with DOTween before disabling it. If no sprite renderer is available, it falls back to normal active-state toggling.

`DeactivateOnFlag` disables its target GameObject when a configured flag is already set or becomes set. BedRoom uses it on WakeUpPanel with `waked_up` so returning from another room after wake-up removes the whole WakeUpPanel object, not only its visual or collider.

`TemporaryChat` runs short one-off chat bubbles. It can wait before showing, fade all child `SpriteRenderer` and UI `Graphic` visuals, optionally set `GameState.OnDialog` while visible so `Movement` locks, set a completion flag, and deactivate the chat object after fade-out. HallDown uses this on `ChatTrigger`: `StoryTrigger` sets `went_down_hall`, `FlagBasedObject` activates the child `Chat`, `TemporaryChat` waits 3 seconds, locks movement while the chat is visible, then sets `chat_completed_go_down_hall` and hides `Chat` so the same bubble does not return on later loads. LivingRoom also uses it on its child `Chat`: `StoryTrigger` sets `went_living_room`, `FlagBasedObject` activates `Chat` while `chat_completed_go_living_room` is absent, `TemporaryChat` locks movement while visible, then hides the bubble and writes `chat_completed_go_living_room`.

`LivingRoomChatTransformSequence` is the scene-local continuation for the LivingRoom `ChatTrigger`. It listens for `chat_completed_go_living_room`, so the chat bubble fully finishes first. It then locks `GameState.OnDialog`, plays a built-in ring VFX plus fade/punch polish on the active `Movement` player, switches the Cat from `MovementForm.Ghost` to `MovementForm.Cat`, waits a short polish delay, plays `SO_Dialogue_LivingRoomTransform`, then sets `mission_provoke_owner`. It writes `post_chat_go_living_room_complete` after the sequence so returning to LivingRoom does not replay the transformation/dialogue.

`DialogueStoryInteractable` extends `StoryInteractable` to play a `DialogueSO` (and optional SFX id) on success, then run the flag action, and optionally deactivate its own GameObject. On success it can fade a `hideBeforeDialogue` visual (for example the WakeUpPanel `Visual`) over `hideBeforeDialogueFadeDuration`, wait `dialogueDelayAfterHide`, then await `PlayDialogueAsync` before executing the action, so flags are only set after the dialogue finishes. The hide fade supports `SpriteRenderer`, UI `Graphic`, and TMP children. It guards against re-entry while a dialogue is playing. This is the reusable base for flag-gated interactions that should show dialogue (WakeUp panel, cat meow, etc.).

The scene `DialogueManager` is resolved lazily through `ResolveDialogueManager`: it uses the serialized `dialogueManager` field if assigned, otherwise falls back to `FindFirstObjectByType<DialogueManager>` once and caches the result. This lets interactions work with only a `DialogueSO` assigned, as long as one `DialogueManager` exists in the scene. The find call is one-shot and cached, not a per-frame lookup, so it stays within acceptable use of the `RULE.md` anti-pattern for game-jam speed; prefer assigning the field directly when convenient.

Current HallDown story components:

- `ChatTrigger` uses `StoryTrigger` to set `went_down_hall` the first time the player enters. A `FlagBasedObject` reveals its child `Chat` while `went_down_hall` exists and `chat_completed_go_down_hall` does not.
- The child `Chat` uses `TemporaryChat` with `showDelay = 3`, `lockMovementWhileVisible = true`, `completionFlag = chat_completed_go_down_hall`, and `deactivateAfterFade = true`.

Current BedRoom story components:

- `CatMeowTrigger` executes its configured flag action after success.
- `CatMeowInteractable` derives from `DialogueStoryInteractable`, so the first `E` press near the bed plays `SO_Dialogue_CatMeowInitial` and sets `met_boss` only after that dialogue finishes. The `met_boss` flag disables this first interaction target and enables the mash target plus the `PhungThanhNo` visual.
- `CatMeowMashInteractable` extends `MashStoryInteractable` for the repeated-`E` phase. When the mash succeeds, it plays `SO_Dialogue_PhungThanhNo`, spawns the configured Boss/owner prefab, immediately sets `boss_wake_up` for scene visuals that should react to the owner leaving the bed, fades the owner in at `(2.48, -4.48, 0)`, plays `SO_Dialogue_PhungThanhNo_Exit`, starts `BossWalk`, moves the owner lightly to the right, then fades/despawns it before setting `waked_boss_up`, hiding completion targets, and deactivating its own GameObject.
- WakeUpPanel starts with its `Visual` active and shows the prompt text `Ấn E để tỉnh dậy`. Interacting fades that visual out over `hideBeforeDialogueFadeDuration` (currently 1 second), waits `dialogueDelayAfterHide` (currently 2 seconds), then plays the assigned wake-up `DialogueSO`; `waked_up` is applied only after dialogue completion, which keeps the cat locked until the wake-up flow finishes. When BedRoom is loaded after `waked_up` already exists, `FadeFlagObject` applies the hidden state instantly on its initial refresh so the WakeUpPanel visual does not flash back in before fading.
- The PhungThanhNo mash setup lives on the boss `InteractE` object under `HolderAfter`. `PhungThanhNo` is inactive by default and only becomes visible when `met_boss` is set after the first cat-meow dialogue. Both `HolderAfter/InteractE` and `PhungThanhNo` are blocked by `waked_boss_up`, so loading a completed save keeps them hidden. `CatMeowMashInteractable` requires `met_boss`, asks for repeated `E` presses (`requiredPressCount` currently 12), decays after `decayDelay` (currently 0.3 seconds) at `decayPerSecond` (currently 3.5 progress per second), and can optionally play owner animation/movement if `ownerExitAnimationState` or exit movement fields are assigned. `MashProgressView` points at the same visual root, keeps it visible during the mash phase (`hideWhenEmpty = false`), auto-finds the full progress bar renderer, warms the bar color toward red, and punch-scales the emote/root to make spam input feel like it is making the character angrier.
- `Bed` owns a `FlagBasedSpriteSwap` configured with `flagId = boss_wake_up` and `targetChildName = Bedr bed`. It listens to flag changes and loaded saves, then swaps the `Bedr bed` renderer to the assigned `flaggedSprite`; the flagged sprite is intentionally left as an Inspector slot so the final bed-empty sprite can be dragged in without code changes.

Current LivingRoom story components:

- `ChatTrigger` uses `StoryTrigger` to set `went_living_room`. Its `FlagBasedObject` reveals child `Chat` only while `went_living_room` exists and `chat_completed_go_living_room` does not. The child `Chat` is inactive by default, uses `TemporaryChat` with `lockMovementWhileVisible = true`, sets `chat_completed_go_living_room`, and deactivates itself after fade so it does not replay on later loads.
- `LivingRoomChatTransformSequence` lives on the still-active `ChatTrigger`, not on the child `Chat`, so it can start after `TemporaryChat` hides the chat object. It waits for `chat_completed_go_living_room`, changes the player form to Cat with the built-in ring VFX/fade/punch polish, then plays `SO_Dialogue_LivingRoomTransform` after a short `dialogueDelay` before assigning the `mission_provoke_owner` mission flag.
- `LivingToyBoxDropInteractable` derives from `StoryInteractable`. On a valid `E` interaction, it prevents re-entry, hides configured start targets, optionally plays an SFX before or after the move, DOTween-moves `toyBoxTransform` to `moveTarget` or `moveOffset`, fades the assigned cutscene object in, then awaits its `CutSceneDialoguePlayer`. The LivingRoom cutscene player runs the box animation and `SO_Dialogue_LivingRoomBoxMemories` while the cutscene is visible, so the Boss can recall the objects inside the box before anything disappears. Only after that cutscene dialogue finishes does `LivingToyBoxDropInteractable` hold briefly, fade the cutscene object out, hide configured completion targets and the `toyBoxTransform.gameObject` when `hideToyBoxOnComplete` is enabled, execute its normal `StoryFlagAction`, set `completionFlagId` (default `dropped_box`), show configured completion targets, and deactivate itself if configured. This keeps `OwnerBoxPickupSequence` from starting until the HolderCutScene dialogue has fully closed.
- `CutSceneDialoguePlayer` is the reusable cutscene bridge used by LivingRoom. It can play an Animator state or legacy Animation clip once, optionally freeze on the final frame, wait a delay, then call `DialogueManager.PlayDialogueAsync(dialogue)`. It resolves animation state names directly or by trying the `_clip` suffix, and it supports manual `PlayAsync` as well as `playOnEnable`.
- `OwnerBoxPickupSequence` starts when its GameObject is enabled, unless `completionFlagId` is already set. It spawns an owner prefab from a serialized prefab or `Resources` path `Main/thang chu di`, can override spawn position/scale/flip/animator state from the scene, fades owner sprites in, optionally plays a pickup animation/dialogue, then sets `picked_up_box` and deactivates itself. The current LivingRoom setup spawns Boss at `(-2.28, -3.04, 0)`, scale `(1, 1, 1)`, flips the sprite on spawn, disables the animator, leaves `pickupAnimationState` empty, and does not wait for animation so Boss simply fades in and stands still after the HolderCutScene flow.
- `PushFlagObject` extends `FlagBasedObject` but currently overrides `Refresh()` to DOTween its own transform by an offset every time the flag condition refreshes. Use carefully: repeated flag load/change refreshes can move it again because it does not call `TryGetTargetActiveState` before moving.

Current error and guard behavior:

- `StoryTrigger` logs a warning when `FlagManager` is missing.
- `StoryInteractable` silently returns when `FlagManager` is missing.
- Conditions fail closed: unmet required flags or present blocked flags stop the trigger/interaction without side effects.
- `FlagManager.SetFlag` ignores empty ids, publishes only when the effective value changes, and still attempts save after valid set calls.
- `StoryFlagAction.Execute` safely returns on null `FlagManager`.
- `FlagBasedObject` warns once for missing or invalid targets.

### UI

Files:

- `Assets/_Project/Scripts/Core/UI/UIManager.cs`
- `Assets/_Project/Scripts/Core/UI/SceneUIController.cs`
- `Assets/_Project/Scripts/Core/UI/SettingsPanelController.cs`
- `Assets/_Project/Scripts/Core/UI/FadePanel.cs`
- `Assets/_Project/Scripts/Core/UI/UIPanelView.cs`
- `Assets/_Project/Scripts/Core/UI/MissionDefinition.cs`
- `Assets/_Project/Scripts/Core/UI/MissionView.cs`
- `Assets/_Project/Scripts/Core/Localization/LocalizationManager.cs`
- `Assets/_Project/Scripts/Core/Localization/LocalizationTable.cs`
- `Assets/_Project/Scripts/Core/Localization/LocalizedText.cs`
- `Assets/_Project/Scripts/Dialogue/DialogueLine.cs`
- `Assets/_Project/Scripts/Dialogue/DialogueView.cs`
- `Assets/_Project/Scripts/Dialogue/DialogueTestRunner.cs`
- `Assets/_Project/Scripts/Enum/PanelId.cs`

`UIManager` keeps a serialized list of `UIPanelView` instances and builds a `Dictionary<PanelId, UIPanelView>` in `Awake`.

It supports:

- `OpenPanel(PanelId)`
- `ClosePanel(PanelId)`
- `HideAllPanels()`

`UIPanelView` is a base class with `Show` and `Hide` methods that toggle GameObject active state.

Current panel ids:

- `Loading`
- `Settings`
- `Pause`
- `Win`
- `Lose`

Important current limitation: `UIPanelView.Id` has only a getter and is not serialized, so derived panels need to provide an id or this base will not expose a configurable id in the Inspector.

`BedRoom.unity` owns its scene UI under `SceneUIRoot`. This root contains a Screen Space Overlay canvas, a Settings button, a Settings panel, and the Dialogue panel. `SceneUIController` binds the scene Settings button and close button to the Settings panel without relying on persistent global UI state.

`PersistantRoot.prefab` owns a persistent `EventSystem` with `InputSystemUIInputModule` so global UI buttons continue receiving pointer/click input after `MainMenu` is unloaded. `PersistentEventSystem` removes scene-local duplicate EventSystems on scene load, which avoids the Settings button working only in scenes that happened to define their own EventSystem.

`UI.prefab` also owns a `Mission` HUD object with `MissionView`. `MissionView` listens to `FlagChangedEvent` and `FlagsLoadedEvent`, maps configured `MissionDefinition` entries from an assigned flag to a completed flag, and localizes mission title text through `LocalizationManager`. Missions now require a real assigned flag so they do not appear by default. When an assigned flag turns on, it fades the `(!) Mission title` text in while sliding it down into place. When the matching completed flag turns on, it wraps the text with TMP strikethrough, waits briefly, then fades the mission out while sliding it upward. On loaded saves, an assigned-but-incomplete mission is restored instantly without replaying the assignment animation. The current first Bedroom mission is `Giúp chủ nhân tỉnh dậy`, assigned by `waked_up` after the WakeUpPanel flow finishes and completed by `waked_boss_up` when the owner wakes up. The current LivingRoom mission is `Chọc giận ông chủ`, assigned by `mission_provoke_owner` after `SO_Dialogue_LivingRoomTransform` finishes and completed by `dropped_box` in LivingRoom.

`SettingsPanelController` drives the in-game settings panel. It persists `settings.musicVolume` and `settings.sfxVolume` to `PlayerPrefs`, applies them to `AudioManager`, controls the Vietnamese/English/Cat language buttons, visibly marks the selected language by disabling and highlighting its button, and can quit to `MainMenu` through `GameManager.QuitToMenu()` plus `SceneManager.LoadScene`.

`LocalizationManager` owns the active `Language`, persists it to `PlayerPrefs` key `settings.language`, resolves UI keys through `LocalizationTable`, and exposes `LanguageChanged`. `LocalizedText` subscribes to that event and refreshes its attached TMP text. Cat language returns `Meow` for normal UI keys and dialogue speaker/body text, except the three language picker keys `language.vietnamese`, `language.english`, and `language.cat`, which remain readable.

All player-facing TMP labels should either be driven by `LocalizedText`, dialogue data, `MissionDefinition`, or explicit code that reads `LocalizationManager`. Direct scene TMP strings are only acceptable for debug-only labels or names that intentionally do not translate. Current scene chat/prompt TMP labels use keys such as `prompt.wakeup`, `chat.hall_down.sleep_lost`, and `chat.living_room.familiar_sound` so switching to English or Cat no longer leaves Vietnamese-only text mixed into gameplay UI.

All player-facing TMP text should use `Assets/_Project/Resources/Text/SVN-Determination Sans SDF.asset`. `Assets/TextMesh Pro/Resources/TMP Settings.asset` defaults to this font and uses `LiberationSans SDF - Fallback` as a dynamic fallback for any glyph not already present in the SVN atlas. Do not assign `LiberationSans SDF` directly to `_Project` gameplay UI unless the label is tooling-only.

`FadePanel` derives from `UIPanelView` and wraps a `CanvasGroup`. `SceneLoader.FadeLoadAsync` finds it at runtime, fades it in before scene load, yields after load, then resolves/fades it out in the newly loaded scene. `FadePanel.Show()` and `Hide()` are asynchronous DOTween fades and keep raycast blocking configurable.

### Cutscenes

Files:

- `Assets/_Project/Scripts/CutScene/CutSceneDialoguePlayer.cs`

`CutSceneDialoguePlayer` is scene-local and intentionally reusable across story beats. It resolves an `Animator` first, then a legacy `Animation` fallback. When playing an Animator, it requires an active GameObject and runtime controller, clamps the requested layer, resolves the requested state directly or by toggling the `_clip` suffix, plays from normalized time `0`, waits for the resolved clip length divided by animator speed, and can freeze on the final frame by playing at normalized time `0.999` and setting speed to `0`. When using legacy `Animation`, it plays the named clip or first available clip once and can sample the last frame.

After animation, it waits `delayAfterAnimation`, then plays the assigned `DialogueSO` through the scene `DialogueManager`. `playOnEnable` is useful for cutscene GameObjects that become active through a flag/object flow, while `PlayAsync` lets another script activate and await the whole sequence manually. `deactivateOnComplete` can hide the cutscene object after dialogue. Presentation fades are owned by the caller when the caller needs tighter story timing; for example `LivingToyBoxDropInteractable` fades the HolderCutScene object around `PlayAsync` and delays `dropped_box` until the fade-out finishes.

### Minigames

Files:

- `Assets/_Project/Scripts/Minigame/WashingMinigameController.cs`
- `Assets/_Project/Prefabs/Minigame/Washing.prefab`

`WashingMinigameController` drives the current Kitchen washing prototype. For testing, `Washing.prefab` keeps its root active but hides all minigame children until the player presses `Enter` or numpad Enter; then it reveals the minigame, locks movement by putting `GameManager` into `OnDialog`, and starts the tutorial. It reads `Keyboard.current.eKey.wasPressedThisFrame` directly like the current interaction/dialogue code. The first dish is a tutorial round: `DirtyDish (1)` moves from its starting point to the `Check` collider with an ease-out slowdown, stops at the valid overlap, then fades/pulses `ButtonE` to ask for input. Later dishes start from the same point, move through a fail point past `Check`, and get faster after every success. Pressing `E` inside the timing window around the moving dish hitbox and `Check` trigger plays the `WashingCat` `Wash` animation once, fades/scales the dish as consumed, then starts another round. The window is measured on local X from both collider widths plus a small padding so faster rounds do not miss because of narrow frame-by-frame bounds overlap. Pressing outside the window during active input or letting a normal dish pass the fail point sets `GameState.GameOver` and opens `PanelId.Lose` through `UIManager`.

Default washing tuning is endless-until-fail for testing: `autoStartOnEnable = false`, `startOnEnter = true`, `hideMinigameUntilStarted = true`, tutorial travel duration `3` seconds, normal travel duration `2` seconds, speed multiplier `0.9` per success, minimum travel duration `0.75` seconds, fail distance `1.2` local units past the check point, timing window padding `0.35`, wash lockout `0.33` seconds, prompt fade `0.15` seconds, prompt pulse `0.55` seconds, and check pulse `0.5` seconds. Story/interact/flag gating is intentionally left for a later pass.

### Main Menu

Files:

- `Assets/_Project/Scripts/MainMenu/MainMenuController.cs`
- `Assets/_Project/Scripts/MainMenu/MainMenuGameFlow.cs`
- `Assets/_Project/Scripts/MainMenu/MainMenuSettingsPanel.cs`

`MainMenu.unity` uses runtime-only scripts for the basic main menu. There is no retained editor builder script.

Current button flow:

- Start: creates a new save through `SaveManager.NewGame()` when available, then loads the configured gameplay scene.
- Continue: loads save data through `SaveManager.LoadGame()` when available, then loads the configured gameplay scene.
- Setting: opens the assigned settings panel.
- Close: hides the assigned settings panel.
- Quit: stops Play Mode in the Unity Editor or calls `Application.Quit()` in builds.

The Continue button GameObject is hidden when no save file exists. It is not just disabled, so the main menu only shows Continue when saved progress is available. The main menu settings panel follows the same visual pattern as the current scene settings panel: dim overlay, centered `SettingsWindow`, and an `X` close button in the top-right corner.

Main menu settings expose the same persisted controls as in-game settings:

- background music volume
- SFX volume
- language selection: Vietnamese, English, or Cat

Language is managed by `LocalizationManager` through `Assets/_Project/Resources/SO_LocalizationTable.asset` and saved in `PlayerPrefs`. Vietnamese is the authoring source. English UI strings come from the localization table and English dialogue comes from each `DialogueLine`'s English fields. Cat language intentionally converts normal UI strings and all dialogue speaker/body text to `Meow`; only the language picker labels stay readable as `Tiếng Việt`, `English`, and `Tiếng Mèo`.

The main menu scripts are split around the first two SOLID principles:

- Single Responsibility: `MainMenuController` binds scene buttons and initializes the menu state, `MainMenuGameFlow` owns start/continue/quit scene flow, and `MainMenuSettingsPanel` owns panel visibility.
- Open/Closed direction: button UI wiring is kept separate from game-flow and panel behavior, so future menu features should be added as new focused components or commands instead of growing one large menu controller.

`MainMenuGameFlow` and `MainMenuController` default to loading `BedRoom`, which is present in Build Settings.

### Dialogue

Files:

- `Assets/_Project/Scripts/Dialogue/DialogueLine.cs`
- `Assets/_Project/Scripts/Dialogue/DialogueSO.cs`
- `Assets/_Project/Scripts/Dialogue/DialogueView.cs`
- `Assets/_Project/Scripts/Dialogue/DialogueManager.cs`
- `Assets/_Project/Scripts/Dialogue/DialogueTestRunner.cs`

`DialogueSO` is a ScriptableObject holding an ordered `DialogueLine` list and an optional background sprite. It is created through the `TTTM/Dialogue` create-asset menu (`SO_Dialogue_` prefix). Dialogue content is authored as assets instead of inline scene arrays, so writers can build lines without touching scenes.

`DialogueManager` is a scene-scoped component (intentionally not a global singleton, per `RULE.md`). It exposes `PlayDialogueAsync(DialogueSO)` (UniTask), a fire-and-forget `PlayDialogue(DialogueSO)` wrapper, `SetDialogueView` for code wiring, and an `IsPlaying` guard. It drives the scene `DialogueView` one line at a time and advances on the `Interact` key (`E`):

1. Set the dialogue background when provided.
2. For each line, run the `DialogueView` typewriter reveal.
3. First `E` press while revealing snaps the line to fully shown; a second press advances.
4. If the line is already fully shown, the first press advances directly.
5. After the last line, the panel hides and `IsPlaying` returns to false.

`DialogueView` does not create UI objects at runtime. It receives serialized scene references for the panel root, background image, portrait image, speaker-name text, and body text. It supports a simple TMP `maxVisibleCharacters` typewriter through `ShowAsync`, exposes `IsRevealing`, `IsLineFullyVisible`, and `CompleteReveal` to snap to full text. `Show` remains for instant, non-typewriter display. Prototype sprites come from `Assets/_Project/Resources/Dialogue`; this should move to serialized configuration, Addressables, or feature-owned dialogue data when the dialogue system becomes production-ready.

Dialogue authoring and polish rules:

- Production dialogue belongs in `DialogueSO` assets. Scene objects should reference those assets instead of storing inline line arrays.
- Keep speaker names consistent across assets. Current boss dialogue uses `Phùng Thanh Nộ`; Unity YAML escaped Unicode is acceptable, but mojibake such as `Ná»™` should not be committed.
- Prefer `\uXXXX` escapes for checked-in Unity YAML when a Vietnamese character needs escaping. Avoid mixed `\xNN` escapes in authored dialogue/localization assets because external tools can misread them even though Unity can deserialize them.
- Keep each `DialogueLine` short enough for the TMP box at the target resolution. Split long thoughts into multiple lines instead of relying on wrapping to carry the whole beat.
- Use line order intentionally: immediate reaction first, player or world response next, and objective/input hint last only when the player needs a nudge.
- Preserve the interaction contract: while text is revealing, `E` reveals the full current line; once the current line is fully visible, `E` advances to the next line.
- Prompt text should be brief, player-facing, and action-first, such as `Ấn E để tỉnh dậy`. Do not put debug/how-to documentation text into production dialogue unless it is intentionally part of the game voice.
- Author Vietnamese first, then fill the matching English speaker/text fields on the same `DialogueLine`. Cat language is generated by `LocalizationManager`, so dialogue assets do not need hand-written cat translations.

`DialogueTestRunner` remains a temporary scene-owned Enter-key harness for quickly stepping through inline test lines during development; production interactions should use `DialogueManager.PlayDialogueAsync` with a `DialogueSO`.

### Event Bus

File:

- `Assets/_Project/Scripts/Core/Event/EventBus.cs`

`EventBus` is a static typed publish/subscribe utility.

It stores subscribers by event type, prevents duplicate subscriptions for the same handler, publishes against a snapshot to tolerate modifications while dispatching, and logs exceptions thrown by handlers.

It also exposes `Clear()` for wiping all subscribers.

### Pooling

Files:

- `Assets/_Project/Scripts/Core/Pooling/PrefabPool.cs`
- `Assets/_Project/Scripts/Core/Pooling/IPoolable.cs`

`PrefabPool<T>` wraps `UnityEngine.Pool.ObjectPool<T>` for prefabs where `T : MonoBehaviour, IPoolable`.

Lifecycle:

- create: instantiate prefab under parent and deactivate
- get: activate and call `OnSpawned`
- release: call `OnDespawned` and deactivate
- destroy: call `OnDestroyed` and destroy GameObject

### Editor Tools

Files:

- `Assets/_Project/Scripts/Editor/TMPFontAssetGenerator.cs`

`TMPFontAssetGenerator` is Editor-only under namespace `TTTM.EditorTools`. It keeps `Assets/_Project/Resources/Text/SVN-Determination Sans SDF.asset` available from source font `Assets/_Project/Resources/Text/SVN-Determination Sans.otf`. On editor load, it generates the TMP font asset only if missing. The menu item `Tools/TTTM/Generate TMP Font Assets/SVN Determination Sans` regenerates it by deleting and recreating the existing asset, creates a dynamic TMP font asset, assigns `LiberationSans SDF - Fallback` as its fallback, names its atlas/material, saves assets, and refreshes the AssetDatabase.

## Dependency Direction

Current intended dependency flow:

```text
Scene Content
  -> Core Managers
  -> Feature/Scene Runtime Components
  -> Unity APIs / Packages

Bootstrapper
  -> PersistantRoot prefab
  -> GameManager
  -> SceneLoader

Gameplay Scripts
  -> InputManager/IInputReader
  -> AudioManager
  -> CameraManager
  -> SaveManager/ISaveable
  -> UIManager
  -> EventBus

Story/Trigger Scripts
  -> FlagManager/StoryFlagStore
  -> SaveManager/ISaveable
  -> EventBus
  -> DOTween for fade presentation
  -> DialogueManager for dialogue-backed interactions

LivingRoom Story Scripts
  -> StoryInteractable/FlagBasedObject
  -> CutSceneDialoguePlayer
  -> AudioManager
  -> Resources for temporary owner prefab loading
  -> DOTween

MainMenu Scripts
  -> SaveManager
  -> GameManager
  -> SceneLoader
  -> Unity UI

BedRoom Scene UI
  -> SceneUIController
  -> DialogueView
  -> DialogueTestRunner
  -> Unity UI / TextMeshPro

Minigame Scripts
  -> GameManager state lock
  -> UIManager Lose panel
  -> Unity InputSystem keyboard polling
  -> DOTween
```

The architecture is manager-centric. Cross-scene infrastructure is global and scene content calls into it as needed.

## Current Gaps And Risks

- `Kitchen.unity` exists, but `SceneId.Kitchen` does not exist and Kitchen is not currently in Build Settings.
- `HallUp.unity` is duplicated in Build Settings. Clean this before relying on scene list order or making a build.
- `UIPanelView.Id` is not serialized or abstract, so the base class alone cannot configure unique panel ids in the Inspector.
- Managers are singleton-based, which is simple for game jam speed but can make tests and scene isolation harder later.
- `SaveManager.FindSaveables` only finds currently loaded active `MonoBehaviour` objects, so inactive objects or unloaded scenes are not saved.
- `AudioManager` depends on correctly configured `AudioLibrary`, `AudioMixer`, and mixer parameter names.
- `InputReader` throws if expected action map/action names are missing, which is useful during setup but should be accounted for when editing the input asset.
- `CatInteractor` currently reads the `E` key directly instead of using `InputReader.InteractPressed`, so interaction rebinding is not fully wired yet.
- `WashingMinigameController` currently reads `Enter`, numpad Enter, and `E` directly through `Keyboard.current`, so it is prototype input and not rebinding-ready.
- `CutSceneDialoguePlayer`, `DialogueStoryInteractable`, and `OwnerBoxPickupSequence` use cached `FindFirstObjectByType` fallback paths when serialized references are not assigned. Prefer assigning references in scenes as the project stabilizes.
- Story flags are string ids in Inspector fields; typos will fail silently unless caught by scene testing or future validation tooling.

## Adding New Features

For a new gameplay feature:

1. Put scripts under `Assets/_Project/Scripts`.
2. Keep scene-local behavior in scene objects.
3. Use existing managers for cross-cutting behavior.
4. Add persistent services to `PersistantRoot.prefab` only when they truly need to survive scene loads.
5. Add saveable behavior through `ISaveable`.
6. Add input through `InputSystem_Actions.inputactions`, `IInputReader`, and `InputReader`.
7. Add UI panels by deriving from `UIPanelView`, adding a `PanelId`, and registering the panel in `UIManager`.
8. Add story progression through `StoryFlagCondition`, `StoryFlagAction`, and `FlagManager` when a feature needs persistent world/story state.
9. Add scenes to Build Settings before loading them by name or `SceneId`.
