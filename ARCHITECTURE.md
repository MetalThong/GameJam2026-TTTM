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

Main transitions:

- `Initialize()` -> `Booting`
- `Pause()` -> `Paused` only from `Playing`
- `Resume()` -> `Playing` only from `Paused`
- `RestartRun()` -> `Booting`
- `QuitToMenu()` -> `MainMenu`

### Scene Loading

Files:

- `Assets/_Project/Scripts/Core/Manager/SceneLoader.cs`
- `Assets/_Project/Scripts/Enum/SceneId.cs`

`SceneLoader` is a plain C# service class, not a `MonoBehaviour`. It loads by `SceneId` or string scene name through `SceneManager.LoadSceneAsync`.

It uses UniTask and an `IsLoading` flag to avoid overlapping loads. When loading by enum, `sceneId.ToString()` is used, so enum names must match scene names.

Current scene ids:

- `MainMenu`
- `BedRoom`
- `Hall`

Current Build Settings include `Bootstrap`, `MainMenu`, and `BedRoom`. `Hall` exists in the enum but is not currently present in Build Settings.

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

Animation and facing are currently handled directly by `Movement`. It drives Animator bools `IsMoving` and `IsGhost`, flips the `SpriteRenderer` from horizontal input or rigidbody velocity, and mirrors collider offsets when configured.

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
- `Flags`

`FlagManager` implements `ISaveable`, so story flags are included in the same save file. Flag changes call `SaveManager.SaveGame()` when a save manager is available. On load, `SaveManager` pushes saved flags back into `FlagManager` and publishes `FlagsLoadedEvent` so flag-dependent scene objects can refresh.

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
- `Assets/_Project/Scripts/Trigger/BedRoom/CatMeowTrigger.cs`
- `Assets/_Project/Scripts/Trigger/BedRoom/CatMeowInteractable.cs`
- `Assets/_Project/Scripts/Trigger/BedRoom/CatMeowMashInteractable.cs`

The story system is flag-driven. `FlagManager` is the current singleton owner for a `StoryFlagStore`, exposes `HasFlag` and `SetFlag`, publishes `FlagChangedEvent` when a flag value changes, and saves after flag changes when `SaveManager` exists.

`StoryFlagStore` stores flags as string/bool pairs. Empty or whitespace flag ids are ignored on set/remove, and invalid save entries are skipped on load. `StoryFlagId` currently defines `GameStart = "game_start"` as a shared constant.

`StoryFlagCondition` gates behavior with two lists:

- `requiredFlags`: every listed flag must be true.
- `blockedFlags`: every listed flag must be false or absent.

`StoryFlagAction` applies story results with two lists:

- `setFlags`: each listed flag is set true.
- `unsetFlags`: each listed flag is set false.

`StoryTrigger` runs from `OnTriggerEnter2D` when the entering collider has the `Player` tag. It checks `FlagManager`, optional one-shot completion flag, and `StoryFlagCondition`, then executes `StoryFlagAction`. One-shot triggers write `trigger_completed_{triggerId}`.

`StoryInteractable` implements `IInteractable`. It follows the same condition/action model as `StoryTrigger`, but is activated by interaction code instead of trigger entry. `TryInteract()` returns false when flags, one-shot completion, or `CanInteract()` block the interaction; it returns true only after `Interact()` starts. One-shot interactables write `interact_completed_{interactId}`.

`MashStoryInteractable` extends `StoryInteractable` for repeated presses. It increments progress by one per successful interact, decays progress after `decayDelay` by `decayPerSecond`, succeeds when progress reaches `requiredPressCount`, and can reset progress after success. It exposes `NormalizedProgress` (0..1) and the `Pressed`, `ProgressChanged`, and `Succeeded` events so views can render mash feedback without owning the mash state.

`MashProgressView` renders that feedback. It lives on the same GameObject as a `MashStoryInteractable` (`RequireComponent`), subscribes to its events, and serializes `visualRoot`, `hideWhenEmpty`, `fillRenderer`, and `emoteTransform`. If any references are not assigned, it can resolve `visualRoot` to its own GameObject, choose a child `SpriteRenderer` named like a full bar (for example `ProgressBarFull`) as the fill, and use the visual root transform as the punch target. It fills the bar by scaling X from 0 to the renderer's original local scale, lerps color from warning yellow/orange toward red, punch-scales on each press (DOTween), snaps to full with a stronger punch on success, and can hide the visual root when progress decays back to zero.

`InteractButton` is a simple prompt helper. It toggles an assigned button/prompt GameObject when a `Player` tagged collider enters or exits its trigger. It hides the prompt in `Awake` so a button left active in the scene does not show until the player enters the trigger.

`FlagBasedObject` listens to `FlagChangedEvent` and `FlagsLoadedEvent` and toggles an assigned target based on a required flag plus an optional blocked flag. The target is active only when the required flag condition is met and the blocked flag is absent, then inverted when `activeWhenFlagExists` is false. The listener object should stay active; if `target == gameObject` and the target would be disabled, it warns and refuses to disable itself so future flag events are not lost.

`FadeFlagObject` extends `FlagBasedObject` by fading a target `SpriteRenderer` with DOTween before disabling it. If no sprite renderer is available, it falls back to normal active-state toggling.

`DialogueStoryInteractable` extends `StoryInteractable` to play a `DialogueSO` (and optional SFX id) on success, then run the flag action, and optionally deactivate its own GameObject. On success it can fade a `hideBeforeDialogue` visual (for example the WakeUpPanel `Visual`) over `hideBeforeDialogueFadeDuration`, wait `dialogueDelayAfterHide`, then await `PlayDialogueAsync` before executing the action, so flags are only set after the dialogue finishes. The hide fade supports `SpriteRenderer`, UI `Graphic`, and TMP children. It guards against re-entry while a dialogue is playing. This is the reusable base for flag-gated interactions that should show dialogue (WakeUp panel, cat meow, etc.).

The scene `DialogueManager` is resolved lazily through `ResolveDialogueManager`: it uses the serialized `dialogueManager` field if assigned, otherwise falls back to `FindFirstObjectByType<DialogueManager>` once and caches the result. This lets interactions work with only a `DialogueSO` assigned, as long as one `DialogueManager` exists in the scene. The find call is one-shot and cached, not a per-frame lookup, so it stays within acceptable use of the `RULE.md` anti-pattern for game-jam speed; prefer assigning the field directly when convenient.

Current BedRoom story components:

- `CatMeowTrigger` executes its configured flag action after success.
- `CatMeowInteractable` derives from `DialogueStoryInteractable`, so the first `E` press near the bed plays `SO_Dialogue_CatMeowInitial` and sets `met_boss` only after that dialogue finishes. The `met_boss` flag disables this first interaction target and enables the mash target plus the `PhungThanhNo` visual.
- `CatMeowMashInteractable` extends `MashStoryInteractable` for the repeated-`E` phase. When the mash succeeds, it plays `SO_Dialogue_PhungThanhNo`, spawns the owner from `Resources/Main/thang chu di.aseprite`, plays `SO_Dialogue_PhungThanhNo_Exit`, starts the owner exit animation state `thang chu di_clip`, runs a light screen fade, then sets `waked_boss_up`, hides completion targets, and deactivates its own GameObject.
- WakeUpPanel starts with its `Visual` active and shows the prompt text `Ấn E để tỉnh dậy`. Interacting fades that visual out over `hideBeforeDialogueFadeDuration` (currently 1 second), waits `dialogueDelayAfterHide` (currently 2 seconds), then plays the assigned wake-up `DialogueSO`; `waked_up` is applied only after dialogue completion, which keeps the cat locked until the wake-up flow finishes.
- The PhungThanhNo mash setup lives on the boss `InteractE` object under `HolderAfter`. `PhungThanhNo` is inactive by default and only becomes visible when `met_boss` is set after the first cat-meow dialogue. Both `HolderAfter/InteractE` and `PhungThanhNo` are blocked by `waked_boss_up`, so loading a completed save keeps them hidden. `CatMeowMashInteractable` requires `met_boss`, asks for repeated `E` presses (`requiredPressCount` currently 12), decays after `decayDelay` (currently 0.3 seconds) at `decayPerSecond` (currently 3.5 progress per second), uses `PhungThanhNo` as the spawn point/visual root, loads the owner prefab from `Resources` path `Main/thang chu di`, and plays fade/animation before setting `waked_boss_up`. `MashProgressView` points at the same visual root, keeps it visible during the mash phase (`hideWhenEmpty = false`), auto-finds the full progress bar renderer, warms the bar color toward red, and punch-scales the emote/root to make spam input feel like it is making the character angrier.

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
- `Assets/_Project/Scripts/Core/UI/UIPanelView.cs`
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

Language is managed by `LocalizationManager` through `Assets/_Project/Resources/SO_LocalizationTable.asset` and saved in `PlayerPrefs`.

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
- Keep each `DialogueLine` short enough for the TMP box at the target resolution. Split long thoughts into multiple lines instead of relying on wrapping to carry the whole beat.
- Use line order intentionally: immediate reaction first, player or world response next, and objective/input hint last only when the player needs a nudge.
- Preserve the interaction contract: while text is revealing, `E` reveals the full current line; once the current line is fully visible, `E` advances to the next line.
- Prompt text should be brief, player-facing, and action-first, such as `Ấn E để tỉnh dậy`. Do not put debug/how-to documentation text into production dialogue unless it is intentionally part of the game voice.

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
```

The architecture is manager-centric. Cross-scene infrastructure is global and scene content calls into it as needed.

## Current Gaps And Risks

- `SceneId.Hall` and `Hall.unity` exist, but Hall is not currently in Build Settings.
- `UIPanelView.Id` is not serialized or abstract, so the base class alone cannot configure unique panel ids in the Inspector.
- Managers are singleton-based, which is simple for game jam speed but can make tests and scene isolation harder later.
- `SaveManager.FindSaveables` only finds currently loaded active `MonoBehaviour` objects, so inactive objects or unloaded scenes are not saved.
- `AudioManager` depends on correctly configured `AudioLibrary`, `AudioMixer`, and mixer parameter names.
- `InputReader` throws if expected action map/action names are missing, which is useful during setup but should be accounted for when editing the input asset.
- `CatInteractor` currently reads the `E` key directly instead of using `InputReader.InteractPressed`, so interaction rebinding is not fully wired yet.
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
