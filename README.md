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
      Core/
        PersistantRoot.prefab
    Scenes/
      Bootstrap.unity
      MainMenu.unity
    Scripts/
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
      Enum/
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

`Bootstrap.unity` owns the startup object `Bootstraper`. It instantiates `Assets/_Project/Prefabs/Core/PersistantRoot.prefab` if the persistent root is not already present, initializes managers, then loads the configured start scene.

`MainMenu.unity` is currently the configured start scene through `SceneId.MainMenu`.

## Core Prefab

`Assets/_Project/Prefabs/Core/PersistantRoot.prefab` is the persistent runtime root. It contains the current global services:

- `PersistantRoot`
- `GameManager`
- `AudioManager`
- `CameraManager`
- `SaveManager`
- `InputManager`
- `UIManager`
- `PanelCanvas`

The root is marked with `DontDestroyOnLoad`, so these services survive scene changes.

## Main Systems

- Bootstrap: `Bootstrapper` creates the persistent root, initializes `GameManager`, and loads the start scene with `SceneLoader`.
- Game state: `GameManager` stores `GameState` and exposes state transitions such as pause, resume, restart, and quit-to-menu.
- Scene loading: `SceneLoader` loads scenes asynchronously with UniTask and guards against duplicate concurrent loads.
- Input: `InputManager` wraps an `InputActionAsset`; `InputReader` exposes movement/look values and gameplay action events.
- Audio: `AudioManager` plays music and SFX from `AudioLibrary`, supports spatial one-shot SFX, and writes volume values to an `AudioMixer`.
- Camera: `CameraManager` manages a Cinemachine camera, follow/look-at target, zoom, priority, bounds, and impulse shake.
- Save: `SaveManager` stores `SaveData` as JSON in `Application.persistentDataPath`; saveable objects implement `ISaveable`.
- UI: `UIManager` opens/closes registered `UIPanelView` instances by `PanelId`.
- Pooling: `PrefabPool<T>` wraps Unity `ObjectPool<T>` for prefab instances implementing `IPoolable`.
- Events: `EventBus` is a static typed pub/sub helper for gameplay events.

## Current Enums

- `SceneId`: `MainMenu`, `Gameplay`
- `GameState`: `Booting`, `MainMenu`, `Playing`, `Paused`, `GameOver`
- `PanelId`: `Loading`, `Settings`, `Pause`, `Win`, `Lose`

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
