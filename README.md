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
      BedRoomEnding.unity
      HallUp.unity
      HallDown.unity
      GhostKitchen.unity
      Kitchen.unity
      LivingRoom.unity
      LivingRoomPart4.unity
      Picture.unity
      StoreRoom.unity
      Test/SCN_TriggerDemo.unity
    Scripts/
      CatMovement/
      Core/
        Audio/
        Bootstrap/
        Camera/
        Event/
        Input/
        Localization/
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
9. `Assets/_Project/Scenes/GhostKitchen.unity`
10. `Assets/_Project/Scenes/Kitchen.unity`
11. `Assets/_Project/Scenes/BedRoomEnding.unity`
12. `Assets/_Project/Scenes/StoreRoom.unity`
13. `Assets/_Project/Scenes/LivingRoomPart4.unity`
14. `Assets/_Project/Scenes/Picture.unity`

`Bootstrap.unity` owns the startup object `Bootstraper`. It instantiates `Assets/_Project/Prefabs/Core/PersistantRoot.prefab` if the persistent root is not already present, initializes managers, then loads the configured start scene.

The current `Bootstrap.unity` asset is configured with `SceneId.Picture` as the start scene in the working tree. `MainMenu.unity` still exists as the menu scene, and `Bootstrapper` currently sets `GameManager` to `MainMenu` after loading its configured start scene.

All current `SceneId` entries are represented in Build Settings. `Scenes/Test/SCN_TriggerDemo.unity` exists as a project scene asset but is not represented in `SceneId` or Build Settings. `HallUp.unity` is listed multiple times in Build Settings; clean this up when scene routing settles.

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
- `CarryManager`
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
- UI: `UIManager` opens/closes registered `UIPanelView` instances by `PanelId`; `InteractionPromptView` owns the global `E : ...` tutorial prompt in `UI.prefab`, including owner-scoped fallback prompts for ability-style interactions.
- Dialogue: `DialogueSO` assets feed the scene `DialogueManager` and `DialogueView`; `E` reveals the current typed line first, then advances once the line is fully visible. Dialogue locks movement with `GameState.OnDialog` and restores the previous state when complete.
- Main Menu: runtime components under `Assets/_Project/Scripts/MainMenu` bind `Start`, `Setting`, close, and quit buttons, hide the current Continue button GameObject, and provide settings controls for music, SFX, and language.
- Story flags and triggers: runtime components under `Assets/_Project/Scripts/Trigger` store story flags, gate triggers/interactions with required and blocked flag conditions, execute set/unset flag actions, refresh flag-based objects through events, and persist flags through `SaveData`.
- Interaction: `CatInteractor` tracks overlapping `IInteractable` targets, skips gameplay interactions while dialogue is playing or just closed, checks `IInteractionAvailability`, then calls the first valid `TryInteract()` on `E`; `InteractButton` uses `IInteractionPromptProvider` to show the global prompt while still toggling any assigned scene-local prompt object in range.
- Carry: `CarryManager` lets the player carry objects only in configured painting scenes, currently `GhostKitchen`, `LivingRoomPart4`, and `Picture`; pressing `E` on a carryable item puts an active visual on Cat's centered `CarryAnchor` with Sorting Layer `Player` and Order `2`, pressing `E` on a matching `CarryDropZone` hands/drops it into that target, `Q` remains a free-drop fallback, and story sequences listen to `CarriedObjectDropped`.
- Localization: `LocalizationManager`, `LocalizationTable`, and `LocalizedText` support Vietnamese, English, and Cat language. Cat language returns `Meow` for normal strings/dialogue except the three readable language picker labels.
- Mission HUD: `MissionView` watches story flags and shows assigned missions with fade/slide animation, normalizes localized mission titles before rendering Vietnamese text with accents, then strikethrough and fades completed missions out.
- Cutscenes: `CutSceneDialoguePlayer` can play an Animator or legacy Animation once, optionally freeze on the last frame, then play a `DialogueSO`. `BedEndingBookSequence` drives the click-stepped `BedRoomEnding` book/memory/text beat.
- Minigames: `WashingMinigameController` drives the Kitchen washing minigame through `WashingMinigameInteractable`, uses `E` for timing hits, sets `dishes_washed`, and can play a failure cutscene plus post-failure dialogue/mission flow.
- LivingRoom story flow: `LivingRoomChatTransformSequence` handles the form-change/dialogue/mission beat after the first ChatTrigger bubble completes, while `LivingToyBoxDropInteractable`, `OwnerBoxPickupSequence`, and `PushFlagObject` handle the toy-box/drop-owner pickup sequence with flags, SFX, tweening, cutscene, and dialogue.
- StoreRoom/Kitchen continuation: `KitchenDroppedBowlSequence`, `PostBowlStoreStoryCoordinator`, `CobwebInteractable`, and `StoreRoomCatMeowTransformAbility` connect the carried bowl, StoreRoom cobweb cleanup, temporary meow-transform ability, dish mission, and washing minigame.
- LivingRoomPart4/Picture painting flow: after `dishes_washed`, Kitchen routes to `LivingRoomPart4`; the old-picture trigger loads `Picture` as ghost form, where `E` picks up cookie/fish/gift onto Cat's mouth anchor and `E` at the Rcat/fishsign `CarryDropZone` targets runs the cookie -> Rcat and fish -> fishsign beats. Picking up the gift completes the picture flow and immediately returns to `LivingRoomPart4`.
- Pooling: `PrefabPool<T>` wraps Unity `ObjectPool<T>` for prefab instances implementing `IPoolable`.
- Events: `EventBus` is a static typed pub/sub helper for gameplay events.

## Current Enums

- `SceneId`: `MainMenu`, `BedRoom`, `HallUp`, `LivingRoom`, `HallDown`, `Kitchen`, `GhostKitchen`, `BedRoomEnding`, `StoreRoom`, `LivingRoomPart4`, `Picture`
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
