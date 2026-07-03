# Project Rules

This is the single source of truth for project rules. Update this file when the team adds or changes conventions.

Codex and contributors must read this file before changing code or assets.

## 1. Architecture Rules

The target architecture is Unity 6+ Professional Hybrid Architecture (U6PHA).

Core rules:

- Decouple features from each other.
- Features must not directly reference other features.
- Use ScriptableObject event channels for cross-feature communication.
- Use Reflex for dependency injection when composition roots/installers are introduced.
- Use R3 for reactive data binding and event handling.
- Use MVVM for UI.
- Avoid adding new singleton systems.
- Prefer Addressables over `Resources.Load`.
- Keep runtime asset loading unloadable and explicit.

Target layer structure:

```text
Game.App
  Entry points, scene management, global composition

Core.Foundation
  Shared utilities, base classes, extensions

Game.Features.*
  Independent vertical slices such as Combat, Economy, WaveSystem
```

Current-base note:

- The current project still has an early game-jam core using `PersistantRoot` and singleton managers.
- Treat those existing systems as current baseline, documented in `ARCHITECTURE.md`.
- New feature work should move toward U6PHA instead of expanding singleton coupling.
- If changing current core managers or adding feature architecture, update `ARCHITECTURE.md` in the same task.

## 2. C# Naming Rules

| Element | Convention | Example |
| --- | --- | --- |
| Classes / Structs | PascalCase | `CombatService`, `HeroUnit` |
| Interfaces | I + PascalCase | `IEnemyTarget`, `IDamageable` |
| Methods | PascalCase | `CalculateDamage()` |
| Public Fields / Properties | PascalCase | `Health`, `IsAlive` |
| Serialized Fields | camelCase | `prefabReference`, `spawnPoint` |
| Private Fields | _ + camelCase | `_currentHealth` |
| Local Variables | camelCase | `damageAmount` |
| Parameters | camelCase | `targetUnit` |
| Constants | PascalCase | `MaxHealth` |
| Public Static Fields | PascalCase | `SharedPool` |
| Private Static Fields | _ + camelCase | `_sharedPool` |
| Enums | PascalCase singular | `DamageType` |
| Flags Enums | PascalCase plural | `LayerMasks` |
| Delegates | PascalCase + Callback | `ProcessUserCallback` |

Advanced naming:

- Boolean names must start with `Is`, `Has`, `Can`, or `Should`.
- Async methods must end with `Async`.
- Events should be verb phrases such as `DoorOpened` or `DoorOpening`.
- Event handlers should be named `On` + event name, such as `OnDoorOpened`.
- Single generic type parameter: `T`.
- Multiple/specific generic parameters: `TKey`, `TValue`, `TSession`.
- Lists, arrays, and sets use plural nouns, such as `enemies` or `spawnPoints`.
- Dictionaries use `[KeyName]2[ValueName]`, such as `id2Unit`.
- ScriptableObject types use `SO` suffix, such as `UnitStatsSO`.
- ScriptableObject assets use `SO_` prefix, such as `SO_EnemyStats_Goblin`.
- Event channels use `ChannelSO` suffix.
- Services use `Service` suffix.
- ViewModels use `ViewModel` suffix.

## 3. C# Style Rules

- Use block-scoped namespaces matching folder/module structure.
- Do not use file-scoped namespaces.
- Explicitly declare access modifiers.
- Use Allman braces.
- Always use braces, even for single-line `if` statements.
- Keep one class/interface per source file.
- File name must match the primary type name.
- Use `var` only when the type is obvious from the right side.
- Prefer explicit types when clarity is better.
- Add comments only to explain why, not what.
- Remove empty `Awake`, `Start`, `Update`, `FixedUpdate`, and similar lifecycle methods.

Unity 6000.3.x note:

- Do not use file-scoped namespaces because MonoBehaviour discovery can fail to register MonoScripts correctly in this Unity version.

## 4. Serialization And Inspector Rules

- Prefer `[SerializeField] private` over public mutable fields.
- Use `[Header]`, `[Space]`, `[Tooltip]`, and `[Range]` to organize inspector data.
- Use `[RequireComponent]` for required components.
- Use `OnValidate()` to validate inspector values and catch configuration issues early.
- Use regular `== null` for Unity object null checks.
- Do not modify ScriptableObject asset fields at runtime in the Editor; copy design-time values into runtime fields first.

Use `[SerializeField]` for:

- Primitives
- Enums
- Unity types
- Serializable structs/classes
- Arrays/lists of supported types

Use `[SerializeReference]` only when needed for:

- Polymorphism
- Null support for managed references
- Shared references inside one serialized host

Unity does not serialize:

- `static`
- `const`
- `readonly`
- Multidimensional arrays
- Jagged arrays
- `Dictionary<TKey, TValue>` without a wrapper
- Nested containers such as `List<List<T>>`

Auto-property serialization is allowed:

```csharp
[field: SerializeField] public float Health { get; private set; }
```

## 5. MonoBehaviour Lifecycle Rules

Lifecycle order:

```text
Awake -> OnEnable -> Start -> Update loops -> OnDisable -> OnDestroy
```

Use each method for:

| Method | Use |
| --- | --- |
| `Awake` | Self-initialization, own component caching, internal state |
| `OnEnable` | Subscribe to events and enable runtime behavior |
| `Start` | Cross-object initialization after all `Awake` calls |
| `OnDisable` | Unsubscribe from events and pause runtime behavior |
| `OnDestroy` | Final cleanup and unmanaged resource release |
| `Update` | Per-frame non-physics logic and input |
| `FixedUpdate` | Physics and Rigidbody movement |
| `LateUpdate` | Camera follow and post-update adjustment |

Critical rules:

- Awake is for self; Start is for other objects.
- Subscribe in `OnEnable`.
- Unsubscribe in `OnDisable`.
- Never use constructors on MonoBehaviours.
- Use `[DefaultExecutionOrder]` only when execution order genuinely matters.
- Disable MonoBehaviours that do not need to run while inactive.

## 6. Performance Rules

- Target less than 1 KB/frame GC allocation during gameplay.
- Cache components in `Awake` or `Start`; never call `GetComponent` in hot paths.
- Avoid LINQ in `Update` or other hot paths.
- Avoid string concatenation in hot paths.
- Use `StringBuilder` or cached strings for repeated string work.
- Use `CompareTag` instead of `gameObject.tag ==`.
- Use non-allocating APIs where available, such as `Physics2D.OverlapCircleNonAlloc`.
- Cache repeated calculations outside loops.
- Avoid boxing value types.
- Prefer structs for short-lived temporary data where appropriate.
- Pool frequently spawned objects such as bullets, enemies, VFX, audio sources, and UI items.
- Profile early with Unity Profiler.
- Use Memory Profiler when tracking memory usage and leaks.
- Call `System.GC.Collect()` only during loading screens, transitions, or pauses.
- Disable GC only for well-defined critical sections and re-enable it afterward.

## 7. Anti-Patterns

Avoid these patterns:

- `GameObject.Find()` at runtime
- `FindObjectOfType()` or old object-finding APIs at runtime
- `SendMessage()`
- `Invoke("MethodName", delay)`
- Caching or storing `transform`
- Modifying ScriptableObject asset values at runtime in the Editor
- Feature-to-feature direct references
- New global singletons unless there is a short-term game-jam reason
- Runtime `Resources.Load`

Use serialized references, event channels, dependency injection, service boundaries, or explicit composition instead.

## 8. Asset Naming Rules

General rules:

- Use PascalCase for custom file and folder names.
- Do not use spaces in custom file or folder names.
- Put the most specific descriptor first when it improves grouping.
- Prefer descriptive names over iterative suffixes.

Art asset prefixes:

| Asset Type | Prefix | Example |
| --- | --- | --- |
| Material | `M_` | `M_BrickWall` |
| Texture | `T_` | `T_WoodGrain` |
| Shader | `SHD_` | `SHD_WaterEffects` |
| Static Mesh | `SM_` | `SM_TreeStump` |
| Skeletal Mesh | `SK_` | `SK_WarriorHero` |
| Animation Clip | `ANM_` | `ANM_RunCycle` |
| Animator Controller | `AC_` | `AC_HeroController` |
| Prefab Variant | `V_` | `V_HeroRed` |
| Sprite 2D | `SPR_` | `SPR_Coin` |
| Scene | `SCN_` | `SCN_MainMenu` |
| ScriptableObject Asset | `SO_` | `SO_EnemyStats_Goblin` |

Texture suffixes:

- Albedo/Diffuse: `_D`
- Normal: `_N`
- Metallic/Roughness: `_M`
- Ambient Occlusion: `_AO`
- Emission: `_E`
- UI Sprite: `_UI`

Audio asset prefixes:

| Asset Type | Prefix | Example |
| --- | --- | --- |
| Music | `MX_` | `MX_BossBattle` |
| Sound FX | `SX_` | `SX_Explosion` |
| Voice/Dialog | `VO_` | `VO_WelcomeMessage` |

## 9. Folder Structure Rules

Project-owned content belongs under `Assets/_Project`.

Target structure:

```text
Assets/
  _Project/
    Core/
    Features/
    Game/
  Art/
  Audio/
  Prefabs/
  Scenes/
  Settings/
  SystemDesign/
  Plugins/
  ThirdParty/
  Sandbox/
```

Folder meanings:

- `_Project`: custom game code and assets.
- `Core`: foundation/shared systems.
- `Features`: feature-specific vertical slices.
- `Game`: application composition and scene installers.
- `Art`: shared art assets.
- `Audio`: shared audio assets.
- `Prefabs`: shared/global prefabs.
- `Scenes`: global scenes.
- `Settings`: global configuration assets.
- `SystemDesign`: architecture/design documentation.
- `Plugins`: SDKs and plugin imports.
- `ThirdParty`: Asset Store and external imports.
- `Sandbox`: experiments and prototypes.

Rules:

- Never modify third-party folder structures.
- Avoid the `Resources` folder.
- Create assets/folders from Unity when possible so `.meta` files are generated correctly.
- Know Unity special folders: `Editor`, `Resources`, `StreamingAssets`, `Gizmos`, `Editor Default Resources`.
- Feature-specific assets belong inside that feature folder.
- Shared/global assets belong in shared root folders.

Feature folder target:

```text
Game.Features.Combat/
  Docs/
  Editor/
  Scenes/
  Scripts/
  Tests/
  Demo/
  Game.Features.Combat.asmdef
```

Demo rules:

- Demo folders must have a separate `.asmdef`, such as `Game.Features.Combat.Demo.asmdef`.
- Demo scripts must not be mixed with production scripts.
- Demo assemblies should reference the main feature assembly.

Assembly definition rules:

- `Core.Foundation.asmdef`: utilities and shared types.
- `Game.App.asmdef`: composition root, references needed modules.
- `Game.Features.[Name].asmdef`: feature logic.
- Feature assemblies must not reference other feature assemblies.
- Editor assemblies use `[RuntimeName].Editor` and are Editor-only.
- Test assemblies use `[RuntimeName].UnitTests` or `[RuntimeName].IntegrationTests`.

## 10. Documentation Rules

- `README.md` is the repository entrypoint for Codex and contributors.
- `RULE.md` is the single source of truth for rules.
- `ARCHITECTURE.md` is the living source of truth for implemented architecture.
- Update `ARCHITECTURE.md` whenever project structure, scenes, core systems, services, feature boundaries, dependencies, or important runtime flows change.
- Major features or complex components should have a `Docs/README.md`.
- Supplemental docs use PascalCase, such as `Architecture.md` or `GettingStarted.md`.

Feature docs should include:

- Title and summary
- Dependencies
- Key classes
- Usage example
- Architecture notes

Code comments:

- Explain why, not what.
- Comment only non-obvious decisions, workarounds, or algorithms.

## 11. Editor And Tooling Rules

Editor-only scripts must be isolated from runtime code.

Editor folder target:

```text
Game.Features.Combat/
  Editor/
    Combat.Editor.asmdef
    Inspectors/
    Drawers/
    Windows/
  Scripts/
```

Editor naming:

| Element | Convention | Example |
| --- | --- | --- |
| Custom Inspector | `[ClassName]Editor` | `HeroUnitEditor` |
| Editor Window | `[Feature]Window` | `WaveDebuggerWindow` |
| Property Drawer | `[TypeName]Drawer` | `RangeFloatDrawer` |
| Menu Item | `Tools/[Feature]/[Action]` | `Tools/Combat/Reset Cooldowns` |

Static analysis:

- Use `OnValidate()` for inspector validation.
- Prefer Microsoft Unity Analyzers if available.
- Use Project Auditor when practical for scripts, assets, and project settings.

## 12. Testing Rules

Tests belong near the feature they validate.

Test folder target:

```text
Game.Features.Combat/
  Tests/
    Editor/
      Combat.UnitTests.asmdef
      DamageUtilsTests.cs
    Runtime/
      Combat.IntegrationTests.asmdef
      HeroSpawnTests.cs
```

Naming:

| Element | Convention | Example |
| --- | --- | --- |
| Test Class | `[ClassName]Tests` | `DamageCalculatorTests` |
| Test Method | `[Method]_[Scenario]_[Expected]` | `Calculate_NegativeArmor_ReturnsZero` |
| Test Variable | noun or verb+noun | `sut`, `mockData` |

Rules:

- Use Arrange, Act, Assert.
- Prefer `[Test]` over `[UnityTest]` unless frame waiting is needed.
- Write tests for pure logic classes when feasible.
- Use interfaces and dependency injection for testability.
- Unit test assemblies are Editor-only.
- Runtime/integration test assemblies can target runtime platforms.

## 13. Git Rules

Branch naming:

```text
type/description-slug
```

Types:

- `feat`
- `fix`
- `refactor`
- `chore`
- `docs`

Commit message format:

```text
type(scope): imperative description
```

Commit types:

- `feat`: new feature
- `fix`: bug fix
- `docs`: documentation only
- `style`: formatting only
- `refactor`: production code refactor
- `test`: tests only
- `chore`: build, package, and maintenance work

Unity scopes:

- `ui`
- `audio`
- `gfx`
- `core`
- `input`
- `scene`
- feature name, such as `economy` or `combat`

Always commit `.meta` files with their assets.

Unity settings:

- Use Force Text Serialization.
- Use Visible Meta Files.
- Configure UnityYAMLMerge for `.unity`, `.prefab`, and `.asset` files when possible.

Ignore generated/local files:

```gitignore
/[Ll]ibrary/
/[Tt]emp/
/[Oo]bj/
/[Bb]uild/
/[Bb]uilds/
/[Ll]ogs/
/[Uu]ser[Ss]ettings/
/[Mm]emoryCaptures/
*.csproj
*.sln
*.suo
*.user
*.userprefs
.DS_Store
.vscode/
```

Use Git LFS for large binary files when configured:

- `*.psd`
- `*.png`
- `*.jpg`
- `*.tga`
- `*.tif`
- `*.exr`
- `*.wav`
- `*.mp3`
- `*.ogg`
- `*.mp4`
- `*.mov`
- `*.fbx`
- `*.obj`
- `*.blend`
- `*.pdf`
- `*.dll`

## 14. Current Project-Specific Rules

- Current engine version is Unity `6000.3.19f1`.
- Current game code is under `Assets/_Project/Scripts`.
- Current scenes are under `Assets/_Project/Scenes`.
- Current project prefabs are under `Assets/_Project/Prefabs`.
- Current persistent root prefab is `Assets/_Project/Prefabs/Core/PersistantRoot.prefab`.
- Current architecture details live in `ARCHITECTURE.md`.
- If the existing base conflicts with target U6PHA rules, preserve working behavior first and document migration direction in `ARCHITECTURE.md`.
