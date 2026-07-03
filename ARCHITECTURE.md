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
7. When load completes, `GameManager` enters:
   - `Playing` if `startScene == SceneId.Gameplay`
   - `MainMenu` otherwise

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
- `Gameplay`

Current Build Settings only include `Bootstrap` and `MainMenu`; `Gameplay` exists in enum but no gameplay scene is currently present in Build Settings.

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

`Movement` is the current player movement coordinator. It reads movement through `MovementInput`, switches form with the temporary `T` test key, and delegates actual movement behavior to `MovementFormBehaviour` components.

Current forms:

- `CatMovementForm`: horizontal platform movement with normal gravity.
- `GhostMovementForm`: smooth free movement in the air with gravity disabled while active.

`Movement` expects a `Rigidbody2D` reference on the player. Rigidbody and collider setup stays in Unity/Inspector so level and character collision rules remain designer-controlled.

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

Current `SaveData` fields:

- `coin`
- `playerPosition`

### UI

Files:

- `Assets/_Project/Scripts/Core/UI/UIManager.cs`
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

The main menu scripts are split around the first two SOLID principles:

- Single Responsibility: `MainMenuController` binds scene buttons and initializes the menu state, `MainMenuGameFlow` owns start/continue/quit scene flow, and `MainMenuSettingsPanel` owns panel visibility.
- Open/Closed direction: button UI wiring is kept separate from game-flow and panel behavior, so future menu features should be added as new focused components or commands instead of growing one large menu controller.

`MainMenuGameFlow` defaults to loading a scene named `Gameplay`. That scene id already exists in `SceneId`, but a gameplay scene still needs to be created and added to Build Settings before the Start/Continue buttons can load it.

### Dialogue Prototype

Files:

- `Assets/_Project/Scripts/Dialogue/DialogueLine.cs`
- `Assets/_Project/Scripts/Dialogue/DialogueView.cs`
- `Assets/_Project/Scripts/Dialogue/DialogueTestRunner.cs`

`DialogueTestRunner` is a temporary runtime test harness. It auto-creates itself after scene load, persists across scene transitions, and listens for the Enter key through the Unity Input System.

Runtime test flow:

1. Press Enter while playing.
2. `DialogueTestRunner` opens the dialogue panel with the first test line.
3. Further Enter presses advance through the test lines.
4. After the final line, the panel hides.

`DialogueView` builds a Screen Space Overlay canvas at runtime. The panel includes a background image, a portrait image slot, a speaker-name text label, and a body-text area. Current fallback sprites are loaded from `Assets/_Project/Resources/Dialogue` for fast prototype testing; this should be replaced with serialized references, Addressables, or feature-owned configuration when the dialogue system becomes production data.

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

MainMenu Scripts
  -> SaveManager
  -> GameManager
  -> SceneLoader
  -> Unity UI
```

The architecture is manager-centric. Cross-scene infrastructure is global and scene content calls into it as needed.

## Current Gaps And Risks

- `SceneId.Gameplay` exists, but no `Gameplay.unity` scene is currently in Build Settings.
- `UIPanelView.Id` is not serialized or abstract, so the base class alone cannot configure unique panel ids in the Inspector.
- Managers are singleton-based, which is simple for game jam speed but can make tests and scene isolation harder later.
- `SaveManager.FindSaveables` only finds currently loaded active `MonoBehaviour` objects, so inactive objects or unloaded scenes are not saved.
- `AudioManager` depends on correctly configured `AudioLibrary`, `AudioMixer`, and mixer parameter names.
- `InputReader` throws if expected action map/action names are missing, which is useful during setup but should be accounted for when editing the input asset.

## Adding New Features

For a new gameplay feature:

1. Put scripts under `Assets/_Project/Scripts`.
2. Keep scene-local behavior in scene objects.
3. Use existing managers for cross-cutting behavior.
4. Add persistent services to `PersistantRoot.prefab` only when they truly need to survive scene loads.
5. Add saveable behavior through `ISaveable`.
6. Add input through `InputSystem_Actions.inputactions`, `IInputReader`, and `InputReader`.
7. Add UI panels by deriving from `UIPanelView`, adding a `PanelId`, and registering the panel in `UIManager`.
8. Add scenes to Build Settings before loading them by name or `SceneId`.
