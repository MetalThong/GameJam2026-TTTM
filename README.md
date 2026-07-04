# GameJam2026-TTTM

Unity 2D game project for Game Jam 2026.

## For Codex And Contributors

Read this file first when working in the repository.

Before changing code or assets:

1. Read `RULE.md`.
2. Read `ARCHITECTURE.md` for the current implemented architecture.

After adding or changing project structure, scenes, core systems, services, feature boundaries, dependencies, or important runtime flows, update `ARCHITECTURE.md` in the same task. The architecture file is a living document and should stay current so future Codex sessions do not miss context.

If a user asks for a code change and the change affects architecture, assume `ARCHITECTURE.md` should be updated unless the user explicitly says not to.

## Project Snapshot

- Engine: Unity `6000.3.19f1`
- Render pipeline: Universal Render Pipeline `17.3.0`, 2D renderer assets included
- Input: Unity Input System `1.19.0`
- Camera: Cinemachine `3.1.7`
- Async scene loading: UniTask from Cysharp GitHub package
- Tweening: DOTween plugin under `Assets/Plugins/Demigiant/DOTween`
- Reactive/collections packages: R3 `1.3.1`, ObservableCollections `3.3.4`

## Folder Layout

```text
Assets/
  _Project/
    Prefabs/
      Boss/
      Core/
        PersistantRoot.prefab
      Minigame/
      cAT/
      ui/
    Resources/
    Scenes/
      Bootstrap.unity
      MainMenu.unity
      BedRoom.unity
      HallUp.unity
      HallDown.unity
      Kitchen.unity
      LivingRoom.unity
    Scripts/
      CatMovement/
      Core/
        Audio/
        Bootstrap/
        Camera/
        Event/
        Input/
        Manager/
        Pooling/
        Save/
        UI/
      Dialogue/
      Enum/
      Event/
      CutScene/
      MainMenu/
      Minigame/
      Trigger/
  Plugins/
    Demigiant/DOTween/
  Packages/
    R3.1.3.1/
    ObservableCollections.3.3.4/
    Microsoft/System support packages
  Settings/
Packages/
ProjectSettings/
```

Game code should live under `Assets/_Project`. Generated Unity files such as `.csproj`, `Library`, `Temp`, and local IDE settings should not be treated as source-of-truth gameplay code.

## Scenes

Current Build Settings include:

1. `Assets/_Project/Scenes/Bootstrap.unity`
2. `Assets/_Project/Scenes/MainMenu.unity`
3. `Assets/_Project/Scenes/BedRoom.unity`
4. `Assets/_Project/Scenes/HallUp.unity`
5. `Assets/_Project/Scenes/HallDown.unity`
6. `Assets/_Project/Scenes/HallUp.unity`
7. `Assets/_Project/Scenes/LivingRoom.unity`
8. `Assets/_Project/Scenes/HallUp.unity`

`Bootstrap.unity` owns the startup object `Bootstraper`. It instantiates `Assets/_Project/Prefabs/Core/PersistantRoot.prefab` if the persistent root is not already present, initializes managers, then loads the configured start scene.

`MainMenu.unity` is currently the configured start scene through `SceneId.MainMenu`.

`Kitchen.unity` exists under `Assets/_Project/Scenes`, but there is currently no `SceneId.Kitchen` and Kitchen is not currently enabled in Build Settings. `HallUp.unity` is listed multiple times in Build Settings; clean this up when scene routing settles.

## Core Prefab

`Assets/_Project/Prefabs/Core/PersistantRoot.prefab` is the persistent runtime root. It contains the current global services:

- `PersistantRoot`
- `GameManager`
- `AudioManager`
- `CameraManager`
- `SaveManager`
- `InputManager`
- `FlagManager`
- `LocalizationManager`
- `UIManager`
- `PanelCanvas`

The root is marked with `DontDestroyOnLoad`, so these services survive scene changes.

## Main Systems

- Bootstrap: `Bootstrapper` creates the persistent root, initializes `GameManager`, and loads the start scene with `SceneLoader`.
- Game state: `GameManager` stores `GameState` and exposes state transitions such as pause, resume, restart, and quit-to-menu.
- Scene loading: `SceneLoader` loads scenes asynchronously with UniTask, guards against duplicate concurrent loads, and supports fade transitions through `FadePanel`.
- Input: `InputManager` wraps an `InputActionAsset`; `InputReader` exposes movement/look values and gameplay action events.
- Audio: `AudioManager` plays music and SFX from `AudioLibrary`, supports spatial one-shot SFX, writes volume values to an `AudioMixer` when configured, and falls back to direct runtime `AudioSource` volume.
- Camera: `CameraManager` manages a Cinemachine camera, follow/look-at target, zoom, priority, bounds, and impulse shake.
- Save: `SaveManager` stores `SaveData` as JSON in `Application.persistentDataPath`; saveable objects implement `ISaveable`.
- UI: `UIManager` opens/closes registered `UIPanelView` instances by `PanelId`.
- Dialogue: `DialogueSO` assets feed the scene `DialogueManager` and `DialogueView`; `E` reveals the current typed line first, then advances once the line is fully visible. Dialogue locks movement with `GameState.OnDialog` and restores the previous state when complete.
- Main Menu: runtime components under `Assets/_Project/Scripts/MainMenu` bind the `MainMenu.unity` buttons, control start/continue/quit flow, hide Continue until a save exists, and provide settings controls for music, SFX, and language.
- Story flags and triggers: runtime components under `Assets/_Project/Scripts/Trigger` store story flags, gate triggers/interactions with required and blocked flag conditions, execute set/unset flag actions, refresh flag-based objects through events, and persist flags through `SaveData`.
- Interaction: `CatInteractor` tracks overlapping `IInteractable` targets, skips gameplay interactions while dialogue is playing or just closed, and calls the first valid `TryInteract()` on `E`; `InteractButton` shows or hides an assigned prompt object while the Player is in range.
- Localization: `LocalizationManager`, `LocalizationTable`, and `LocalizedText` support Vietnamese, English, and Cat language. Cat language returns `Meow` for normal strings/dialogue except the three readable language picker labels.
- Mission HUD: `MissionView` watches story flags and shows assigned missions with fade/slide animation, then strikethrough and fades completed missions out.
- Cutscenes: `CutSceneDialoguePlayer` can play an Animator or legacy Animation once, optionally freeze on the last frame, then play a `DialogueSO`.
- Minigames: `WashingMinigameController` currently drives the Kitchen washing prototype. `Washing.prefab` stays hidden until `Enter`/numpad Enter, then uses `E` for timing hits and opens `Lose` on fail.
- LivingRoom story flow: `LivingRoomChatTransformSequence` handles the form-change/dialogue/mission beat after the first ChatTrigger bubble completes, while `LivingToyBoxDropInteractable`, `OwnerBoxPickupSequence`, and `PushFlagObject` handle the toy-box/drop-owner pickup sequence with flags, SFX, tweening, cutscene, and dialogue.
- Pooling: `PrefabPool<T>` wraps Unity `ObjectPool<T>` for prefab instances implementing `IPoolable`.
- Events: `EventBus` is a static typed pub/sub helper for gameplay events.

## Current Enums

- `SceneId`: `MainMenu`, `BedRoom`, `HallUp`, `LivingRoom`, `HallDown`
- `GameState`: `Booting`, `MainMenu`, `Playing`, `Paused`, `GameOver`, `OnDialog`
- `PanelId`: `Loading`, `Settings`, `Pause`, `Win`, `Lose`
- `Language`: `Vietnamese`, `English`, `Cat`

## Setup

1. Open the repository with Unity `6000.3.19f1`.
2. Let Unity restore packages from `Packages/manifest.json`.
3. Open `Assets/_Project/Scenes/Bootstrap.unity`.
4. Press Play from the Bootstrap scene.

## Development Notes

- Treat `RULE.md` as the canonical rule source.
- Treat `ARCHITECTURE.md` as the current source of truth for implemented structure.
- Keep new gameplay scenes in `Assets/_Project/Scenes`.
- Keep project-owned prefabs in `Assets/_Project/Prefabs`.
- Keep C# code under `Assets/_Project/Scripts`, grouped by system.
- Add new scenes to Build Settings before loading them through `SceneLoader`.
- When adding a new scene enum, make sure the enum name matches the Unity scene name if using `SceneLoader.LoadSceneAsync(SceneId)`.
- Any object that needs persistence should live under `PersistantRoot.prefab` or be intentionally recreated per scene.
