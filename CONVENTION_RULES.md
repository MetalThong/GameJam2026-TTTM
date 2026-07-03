# Fruit Fortress - Convention Rules

This document outlines the coding standards, architectural principles, and version control guidelines for the **Result Fruit Fortress** project. All contributors must adhere to these rules to maintain codebase quality and consistency.

## 1. Architecture: U6PHA
The project follows the **Unity 6+ Professional Hybrid Architecture (U6PHA)**.

### Core Principles
- **Decoupling**: Features (e.g., Combat, Gacha) must **not** reference each other directly. Communication handles via **Event Channels (SOA)**.
- **Dependency Injection**: Use **Reflex** for dependency management.
- **Reactive Programming**: Use **R3** (Reactive3) for data binding and event handling.
- **MVVM**: Use Model-View-ViewModel pattern for UI.
- **No Singletons**: Avoid the Singleton pattern. Use ScriptableObject event channels, dependency injection, or service locators instead to prevent tight coupling.
- **Addressables over Resources**: Always use Addressables for runtime asset loading instead of `Resources.Load()`, which bloats builds and cannot unload assets.

### Layer Structure
1.  **Application Layer** (`Game.App`): Entry points, scene management, global composition.
2.  **Foundation Layer** (`Core.Foundation`): Shared utilities, base classes, extensions.
3.  **Feature Layer** (`Game.Features.*`): Independent vertical slices (Economy, Combat, WaveSystem, etc.).

---

## 2. Coding Conventions (C#)

### Naming Standards
| Element | Convention | Example |
| :--- | :--- | :--- |
| **Classes / Structs** | PascalCase | `CombatService`, `HeroUnit` |
| **Interfaces** | I + PascalCase | `IEnemyTarget`, `IDamageable` |
| **Methods** | PascalCase | `CalculateDamage()`, `SpawnUnit()` |
| **Public Fields / Props** | PascalCase | `Health`, `IsAlive` |
| **Serialized Fields** | camelCase | `prefabReference`, `spawnPoint` |
| **Private Fields** | _ + camelCase | `_currentHealth`, `_enemyTarget` |
| **Local Variables** | camelCase | `damageAmount`, `nearestEnemy` |
| **Parameters** | camelCase | `targetUnit`, `spawnPosition` |
| **Constants** | PascalCase | `MaxHealth`, `DefaultSpeed` |
| **Static Fields (Public)** | PascalCase | `Instance`, `SharedPool` |
| **Static Fields (Private)** | _ + camelCase | `_instance`, `_sharedPool` |
| **Enums** | PascalCase (Singular) | `DamageType`, `WeaponClass` |
| **Enums ([Flags])** | PascalCase (Plural) | `LayerMasks`, `GameStates` |
| **Delegates** | PascalCase + "Callback" | `ProcessUserCallback`, `OnCompleteCallback` |

### Advanced Naming
- **Booleans**: **Must** be prefixed with a verb question.
    - `IsActive`, `HasKey`, `CanMove`, `ShouldRespawn`.
    - *Avoid*: `Dead`, `Active`, `Flag`.
- **Async Methods**: **Must** end with `Async` suffix and return `Task` / `Task<T>`.
    - `LoadAssetAsync()`, `SaveDataAsync()`.
    - *Avoid*: `void` async methods (except event handlers).
- **Events**:
    - **Name**: Verb phrase indicating state change (Past/Present).
        - `DoorUnknown` (BAD) -> `DoorOpened` (GOOD), `DoorOpening` (GOOD).
    - **Handlers**: `On` + Event Name.
        - `OnDoorOpened()`.
- **Generics**:
    - Single param: `T`.
    - Multiple/Specific: `T` + Descriptive Name.
        - `TSession`, `TKey`, `TValue`.
- **Collections**:
    - **List, Array, Set**: Must use **Plural Noun**.
        - `enemies`, `spawnPoints`, `items`.
    - **Dictionary**: **Must** use `[KeyName]2[ValueName]`.
        - `id2Unit`, `name2Stats`, `type2Prefab`.

### Specific Suffixes
- **ScriptableObjects**: Suffix with `SO` (e.g., `UnitStatsSO`, `PullConfigSO`).
- **Event Channels**: Suffix with `ChannelSO` (e.g., `UnitDiedChannelSO`, `EnergyRequestChannelSO`).
- **Services**: Suffix with `Service` (e.g., `MergeService`, `WaveService`).
- **ViewModels**: Suffix with `ViewModel` (e.g., `QueueViewModel`).

### Best Practices

#### Code Structure
- **Namespaces**: Always use block-scoped namespaces matching folder structure (e.g., `namespace Game.Features.Combat { ... }`). _File-scoped namespaces are not used because Unity 6000.3.x has a MonoBehaviour discovery regression where MonoScripts in file-scoped namespaces fail to register, surfacing as "class cannot be found" when assigning scripts to GameObjects._
- **Access Modifiers**: Always explicitly declare access modifiers (`private`, `public`, `protected`). Never rely on the implicit `private` default.
- **Bracing Style**: Use Allman style (opening brace on its own line). Always use braces, even for single-line `if` statements.
- **One Class Per File**: One class/interface per source file. The filename must match the class name exactly.
- **Use `var` Judiciously**: Use `var` only when the type is obvious from the right side of the assignment. Prefer explicit types otherwise.

#### Serialization & Inspector
- **Serialization**: Prefer `[SerializeField] private` over `public` fields to preserve encapsulation.
- **Inspector Organization**: Use `[Header("Category")]`, `[Space(10)]`, `[Tooltip("Description")]`, `[Range(min, max)]` to organize the Inspector.
- **Component Dependencies**: Use `[RequireComponent(typeof(Rigidbody2D))]` to declare component dependencies explicitly.
- **Validation**: Use `OnValidate()` to validate Inspector values and catch configuration errors early.

#### Performance & Memory
- **Null Checks**: Use regular `== null` for Unity Objects (due to extensive lifetime checks).
- **Loops**: Cache commonly used properties/calculations outside loops.
- **LINQ**: Avoid LINQ in `Update()` or hot paths (allocations).
- **String**: Use `StringBuilder` or string interpolation `$` for complex strings; avoid frequent concatenation in `Update()`.
- **Tag Comparison**: Use `CompareTag("Enemy")` instead of `gameObject.tag == "Enemy"` to avoid GC allocation.
- **Empty Lifecycle Methods**: Remove empty `Update()`, `Start()`, `Awake()`, `FixedUpdate()` methods. Unity still invokes them with overhead even when empty.

#### Anti-Patterns to Avoid
- **Never use `GameObject.Find()`**, `FindObjectOfType()`, or `SendMessage()` at runtime. Use `[SerializeField]` references, events, or dependency injection.
- **Never use `Invoke("MethodName", delay)`**. Use coroutines or direct method calls for compile-time safety.
- **Never cache or store references to `transform`** — it's already cached by Unity.
- **Never modify ScriptableObject fields at runtime** in the Editor. Copy to runtime variables instead to avoid overwriting asset data.

---

## 3. Asset Naming Conventions
Assets must follow a strict prefix/suffix system for quick identification and filtering.

### General Rules
- **PascalCase** for all custom file and folder names: `PlayerCharacter`, `ForestEnvironment`.
- **No spaces** in file or folder names. Use PascalCase consistently.
- **Most specific descriptor first**: `TreeSmall` not `SmallTree`, `EnemyGoblin` not `GoblinEnemy`. This groups related assets together.
- **Descriptive names over iterative**: `Vehicle_Truck_Damaged` is better than `Vehicle_Truck_01`.

### 3.1. Art Assets
| Asset Type | Prefix | Suffix | Example |
| :--- | :--- | :--- | :--- |
| **Material** | `M_` | `_Mat` (Optional) | `M_BrickWall` |
| **Texture** | `T_` | N/A | `T_WoodGrain` |
| **Shader** | `SHD_` | N/A | `SHD_WaterEffects` |
| **Static Mesh** | `SM_` | N/A | `SM_TreeStump` |
| **Skeletal Mesh** | `SK_` | N/A | `SK_WarriorHero` |
| **Animation Clip** | `ANM_` | N/A | `ANM_RunCycle` |
| **Anim Controller**| `AC_` | N/A | `AC_HeroController` |
| **Prefab** | N/A | N/A | `HeroCharacter` (PascalCase) |
| **Prefab Variant** | `V_` | N/A | `V_HeroRed` |
| **Sprite (2D)** | `SPR_` | N/A | `SPR_Coin`, `SPR_ButtonIdle` |
| **Scene** | `SCN_` | N/A | `SCN_MainMenu`, `SCN_Level01` |
| **ScriptableObject** | `SO_` | N/A | `SO_EnemyStats_Goblin` |

### 3.2. Texture Types (Suffixes)
When a texture has a specific role, append these suffixes:
- **Albedo/Diffuse**: `_D` (e.g., `T_Wood_D`)
- **Normal Map**: `_N` (e.g., `T_Wood_N`)
- **Metallic/Roughness**: `_M` (e.g., `T_Wood_M`)
- **Ambient Occlusion**: `_AO` (e.g., `T_Wood_AO`)
- **Emission**: `_E` (e.g., `T_Glow_E`)
- **UI Sprite**: `_UI` (e.g., `T_Button_UI`)

### 3.3. Audio Assets
| Asset Type | Prefix | Suffix | Example |
| :--- | :--- | :--- | :--- |
| **Music** | `MX_` | N/A | `MX_BossBattle` |
| **Sound FX** | `SX_` | N/A | `SX_Explosion` |
| **Voice/Dialog** | `VO_` | N/A | `VO_WelcomeMessage` |

---

## 4. Project Structure

### Folder Organization (`Assets/`)
- **_Project/**: Root folder for all custom game code and assets.
    - **Core/**: Foundation and shared systems.
    - **Features/**: Feature-specific folders (Combat, Economy, etc.).
    - **Game/**: Application composition and scene installers.
- **Art/**: Models, Textures, Materials, Animations.
- **Prefabs/**: Global or Shared prefabs.
- **Scenes/**: Unity scene files.
- **Settings/**: Global configuration assets.
- **SystemDesign/**: Architecture documentation.
- **Plugins/**: Third-party SDKs and libraries.
- **ThirdParty/**: Asset Store imports (keep separate from custom code).
- **Sandbox/**: Experimental code and prototypes. Each team member can have a subfolder.

### Folder Organization Best Practices
- **Underscore-prefixed root folder** (`_Project`) keeps your content at the top, separated from third-party assets.
- **Never modify third-party folder structures.** Keep Asset Store and plugin imports in their own folders.
- **Avoid the `Resources/` folder.** Use Addressables for runtime asset loading. The Resources folder increases build size and memory usage regardless of whether assets are used.
- **Know Unity's special folders**: `Editor`, `Resources`, `StreamingAssets`, `Gizmos`, `Editor Default Resources` have special meaning to Unity.
- **Create assets/folders from within Unity**, not the file system, so `.meta` files are properly generated.

### Scene & Feature Organization
Scenes must be organized based on their scope:
1.  **Global Scenes**: Located in `Assets/Scenes`. These are application-level scenes like `Boot`, `MainMenu`, `GameplayLoop`.
2.  **Feature Scenes**: Located in `Game.Features.[Name]/Scenes`. These are scenes specific to a feature (e.g., a specific level, a UI testbed).
3.  **Demo Scenes**: Located in `[PathToModule]/Demo/Scenes`.

#### Feature Folder Structure Example
```text
Game.Features.Combat/
├── Demo/                           <-- NEW: Demo specific content
│   ├── Scenes/                     <-- Demo/Test scenes for this feature
│   ├── Scripts/                    <-- Demo-only scripts
│   └── Game.Features.Combat.Demo.asmdef <-- MANDATORY: Separate asmdef for demos
├── Docs/
├── Editor/
├── Scenes/                         <-- Production scenes belonging to this feature
├── Scripts/
├── Tests/
└── Game.Features.Combat.asmdef
```

#### Demo Folder Rules
- **Separate Assembly**: All `Demo/` folders **must** contain a separate Assembly Definition (`.asmdef`) file (e.g., `Game.Features.Combat.Demo.asmdef`).
    - This assembly must reference the main feature assembly.
    - This allows demo code to be easily excluded from release builds or stripped designated as "Editor Only" or "Development Build" if needed.
- **Isolation**: Demo scripts should not be mixed with production `Scripts/`.

### Asset Placement Strategy
To maintain a clean and modular codebase, assets must be placed according to their scope.

1.  **Feature-Specific Assets**: Assets used exclusively by a single feature **MUST** be placed inside that feature's folder.
    - Path: `Game.Features.[Name]/[AssetType]/`
    - Allowed Subfolders: `Art/`, `Audio/`, `Prefabs/`, `Data/` (for ScriptableObjects), `Materials/`, `Textures/`, `Animations/`.
    - *Example*: `Game.Features.Combat/Audio/SFX_SwordHit.wav`

2.  **Shared/Global Assets**: Assets used by multiple features or the global application **MUST** be placed in the root asset folders.
    - Path: `Assets/Art/`, `Assets/Audio/`, `Assets/Prefabs/`.
    - *Example*: `Assets/Audio/Music/MX_MainMenu.mp3`

3.  **Folder Internal Organization**: Within any asset folder (Feature or Global), use standard category names:
    - `Materials`
    - `Textures`
    - `Models`
    - `Audio`
    - `Prefabs`
    - `Animations`

### Assembly Definitions
- **Core.Foundation.asmdef**: Utilities, shared types.
- **Game.App.asmdef**: Composition root, references everything.
- **Game.Features.[Name].asmdef**: Feature logic.
    - *Constraint*: Features should **never** reference other Features.

---

## 5. Git Conventions

### Branch Naming
Format: `type/description-slug`
- **feat/**: New features (e.g., `feat/add-gacha-pull-logic`)
- **fix/**: Bug fixes (e.g., `fix/infinite-energy-glitch`)
- **refactor/**: Code restructuring (e.g., `refactor/extract-combat-interface`)
- **chore/**: Maintenance, build scripts (e.g., `chore/update-reflex-package`)
- **docs/**: Documentation updates (e.g., `docs/update-gqm-model`)

### Commit Messages
Format: `type(scope): imperative description`

**Types**:
- `feat`: New feature
- `fix`: Bug fix
- `docs`: Documentation only
- `style`: Formatting, missing semi-colons, etc. (no code change)
- `refactor`: Refactoring production code
- `test`: Adding tests, refactoring test (no production code change)
- `chore`: Updating build tasks, package manager configs, etc.

**Scopes** (Unity Specific):
- `ui`: HUD, Menus, Widgets
- `audio`: Sound effects, Music
- `gfx`: Shaders, Particles, Post-processing
- `core`: Foundation, Utilities
- `input`: Input System
- `scene`: Level design, Scene files
- `[feature]`: Specific feature name (e.g., `economy`, `combat`)

**Example**:
```text
feat(economy): implement soft currency cap

Added logic to prevent energy regeneration above the soft cap (20).
Updated EnergyService to check cap before applying timed rewards.
```

### .gitignore Checklist
Always ignore the following in your `.gitignore`:
```
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

### Git LFS & Settings
- **Force Text Serialization**: Editor Settings > Asset Serialization > Force Text.
- **Visible Meta Files**: Editor Settings > Version Control > Visible Meta Files.
- **Always commit `.meta` files**: CRITICAL — `.meta` files must be committed alongside their assets. Missing `.meta` files break asset references and GUIDs.
- **UnityYAMLMerge**: Configure as the merge tool for `.unity`, `.prefab`, and `.asset` files to reduce merge conflicts.
- **LFS Tracking** (track binary files over ~500KB):
    - Images: `*.psd`, `*.png`, `*.jpg`, `*.tga`, `*.tif`, `*.exr`
    - Audio: `*.wav`, `*.mp3`, `*.ogg`
    - Video: `*.mp4`, `*.mov`
    - 3D: `*.fbx`, `*.obj`, `*.blend`
    - Resources: `*.pdf`, `*.dll`

---

## 6. Documentation Conventions

### Feature & Component Documentation
Every major Feature or Complex Component **must** have a `Docs/` folder containing a `README.md`.

#### File Hierarchy
```text
Game.Features.Combat/
├── Docs/
│   ├── README.md           <-- Entry point
│   ├── Architecture.md     <-- High-level diagrams/flowups
│   └── API_Reference.md    <-- Public API usage examples
├── Scripts/
├── Tests/
└── Game.Features.Combat.asmdef
```

#### Naming Conventions
- **Folder**: `Docs/` (Always plural, title case not strictly required but PascalCase preferred for others).
- **Entry File**: `README.md` (Standard markdown entry point).
- **Supplemental Files**: PascalCase (e.g., `Architecture.md`, `GettingStarted.md`).

#### Content Guidelines (README.md)
1.  **Title & Summary**: What is this component?
2.  **Dependencies**: What other features/modules does it rely on?
3.  **Key Classes**: Links to main entry points (e.g., `CombatService`, `DamageCalculator`).
4.  **Usage Example**: Small code snippet showing how to use the feature.
5.  **Architecture Notes**: Brief explanation of internal logic or patterns (State Machine, formatting, etc.).

### Code Comments
- **Inline Comments**: Explain **why**, never **what** the code does.
    - **GOOD**: `// Reset counter after wave ends to prevent overflow`
    - **BAD**: `// Increment counter` (obvious from code)
- **Complex Logic Only**: If the code is self-explanatory, no comment is needed. Only comment non-obvious decisions, workarounds, or algorithmic choices.

---

## 7. Editor & Tools Conventions

### Editor Script Location
All Editor-only scripts must be strictly separated from runtime code to prevent build errors.

#### File Hierarchy
```text
Game.Features.Combat/
├── Editor/                         <-- Special Unity folder
│   ├── Combat.Editor.asmdef        <-- Editor-only assembly
│   ├── Inspectors/
│   ├── Drawers/
│   └── Windows/
├── Scripts/
└── Game.Features.Combat.asmdef
```

### Naming Conventions (`Editor`)
| Element | Convention | Example |
| :--- | :--- | :--- |
| **Custom Inspector** | `[ClassName]Editor` | `HeroUnitEditor`, `SpawnConfigEditor` |
| **Editor Window** | `[Feature]Window` | `WaveDebuggerWindow`, `LootTableWindow` |
| **Property Drawer** | `[TypeName]Drawer` | `RangeFloatDrawer`, `ItemRefDrawer` |
| **Menu Items** | `Tools/[Feature]/[Action]` | `Tools/Combat/Reset Cooldowns` |

### Assembly Definitions (Editor)
- **Naming**: `[RuntimeName].Editor` (e.g., `Game.Features.Combat.Editor`).
- **Platforms**: **Editor Only** (Uncheck "Any Platform", check "Editor").
- **References**: Must reference the Runtime assembly (e.g., `Game.Features.Combat`).

### Validation & Static Analysis
- **`OnValidate()`**: Use in MonoBehaviours and ScriptableObjects to validate Inspector values and catch configuration errors early:
```csharp
private void OnValidate() {
    if (_moveSpeed < 0) _moveSpeed = 0;
    if (_attackDamage < 0) Debug.LogWarning("Attack damage should not be negative", this);
}
```
- **Roslyn Analyzers**: Use `Microsoft.Unity.Analyzers` for static code analysis. Configure with `.editorconfig` for team-wide enforcement.
- **Project Auditor**: Use Unity's Project Auditor package for static analysis of scripts, assets, and project settings.

---

## 8. Testing Conventions

### Test Location
Tests must be separated from runtime code but located within the same Feature folder.

#### File Hierarchy
```text
Game.Features.Combat/
├── Tests/
│   ├── Editor/                     <-- Unit Tests (EditMode)
│   │   ├── Combat.UnitTests.asmdef
│   │   └── DamageUtilsTests.cs
│   └── Runtime/                    <-- Integration Tests (PlayMode)
│       ├── Combat.IntegrationTests.asmdef
│       └── HeroSpawnTests.cs
```

### Naming Conventions
| Element | Convention | Example |
| :--- | :--- | :--- |
| **Test Class** | `[ClassName]Tests` | `DamageCalculatorTests` |
| **Test Method** | `[Method]_[Scenario]_[Expected]` | `Calculate_NegativeArmor_ReturnsZero` |
| **Test Variable**| `[Noun]` or `[Verb][Noun]` | `sut` (System Under Test), `mockData` |

### Assembly Definitions (Tests)
- **Unit Tests (`Editor`)**:
    - **Naming**: `[RuntimeName].UnitTests`
    - **Platforms**: **Editor Only**
    - **References**: `[RuntimeName]`, `UnityEngine.TestRunner`, `UnityEditor.TestRunner`
- **Integration Tests (`Runtime`)**:
    - **Naming**: `[RuntimeName].IntegrationTests`
    - **Platforms**: **Any Platform** (for running on device)
    - **References**: `[RuntimeName]`, `UnityEngine.TestRunner`

### Best Practices
- **AAA Pattern**: Structure tests with **Arrange**, **Act**, **Assert**.
- **TDD**: Write tests for pure logic classes (Calculators, Utils) *before* implementation.
- **Mocks**: Use `NSubstitute` (if available) or interfaces for mocking external dependencies.
- **Prefer `[Test]` over `[UnityTest]`**: Use `[Test]` unless you need to yield instructions or wait across frames. `[UnityTest]` has higher overhead.
- **Design for Testability**: Use interfaces and dependency injection so systems can be tested in isolation. Avoid tight coupling to MonoBehaviour lifecycle in logic classes.
- **CI Integration**: Run tests in CI pipelines. Use command-line test execution for automation (see CLAUDE.md for commands).

---

## 9. Performance Conventions

This section is **critical for mobile games** where performance directly impacts user experience and battery life.

### Garbage Collection (GC) Management
- **Allocation Budget**: Target **< 1 KB/frame** during gameplay. Use Unity Profiler's GC Alloc column and aim for all zeros during gameplay.
- **Cache Component References**: Retrieve components in `Awake()`/`Start()`, never in `Update()` or hot paths.
```csharp
// GOOD
private Rigidbody2D _rigidbody;
void Awake() => _rigidbody = GetComponent<Rigidbody2D>();
void Update() => _rigidbody.velocity = Vector2.zero;

// BAD
void Update() => GetComponent<Rigidbody2D>().velocity = Vector2.zero;
```
- **Avoid String Operations in Update**: Use `StringBuilder` for concatenation. Cache strings when possible.
- **Avoid Boxing**: Don't pass value types where `object` is expected. Use generics instead.
- **Avoid LINQ in Hot Paths**: LINQ creates garbage from enumerator boxing and lambda captures.
- **Use Non-Allocating APIs**:
    - `CompareTag("Enemy")` instead of `gameObject.tag == "Enemy"`
    - `Physics2D.OverlapCircleNonAlloc()` instead of `Physics2D.OverlapCircle()`
- **Favor Structs for Temporary Data**: Use structs instead of classes for short-lived data objects.

### Object Pooling
- **Mandatory for Frequently Instantiated Objects**: Pool bullets, projectiles, enemies, VFX, audio sources, UI elements.
- **Benefits**: Can decrease heap allocations by up to 80% on mobile.
- **Implementation**: Use Unity's `ObjectPool<T>` (Unity 2021+) or create custom pools.
```csharp
// Example pattern
public class BulletPool {
    private readonly ObjectPool<Bullet> _pool;

    public BulletPool(Bullet prefab, Transform parent) {
        _pool = new ObjectPool<Bullet>(
            createFunc: () => Instantiate(prefab, parent),
            actionOnGet: bullet => bullet.gameObject.SetActive(true),
            actionOnRelease: bullet => bullet.gameObject.SetActive(false)
        );
    }

    public Bullet Get() => _pool.Get();
    public void Release(Bullet bullet) => _pool.Release(bullet);
}
```

### Update Loop Optimization
- **Only Use `Update()` for Per-Frame Logic**: If logic doesn't need to run every frame, don't use `Update()`.
- **Use Coroutines for Timed Sequences**: Multi-step processes, delays, or interval-based logic.
- **Use `InvokeRepeating` for Simple Periodic Tasks**: Fire-and-forget periodic behavior (but be aware it's string-based).
- **Disable MonoBehaviours When Inactive**: Disable components that don't need to run Update when off-screen or paused.

### Profiling & Monitoring
- **Profile Early and Often**: Use Unity Profiler (CPU, GPU, Memory modules) during development, not just at the end.
- **Memory Profiler Package**: Use `com.unity.memoryprofiler` to track memory usage and find leaks.
- **Frame Budget**: Define target frame time (e.g., 16.6ms for 60 FPS, 33.3ms for 30 FPS) and stay within it.
- **Enable Deep Profiling Selectively**: Deep profiling shows all method calls but has massive overhead. Use only for targeted investigations.

### Manual GC Control (Advanced)
- **GC During Loading/Transitions Only**: Call `System.GC.Collect()` during loading screens, scene transitions, or pauses.
- **Disable GC During Critical Gameplay** (Unity 2019+):
```csharp
// Disable GC during critical section
GarbageCollector.GCMode = GarbageCollector.Mode.Disabled;
// ... critical gameplay code ...
// Re-enable afterward
GarbageCollector.GCMode = GarbageCollector.Mode.Enabled;
```
- **Use Sparingly**: Only use when you have a well-defined critical section and know what you're doing.

---

## 10. MonoBehaviour Lifecycle Conventions

Understanding and following proper lifecycle patterns prevents ~80% of timing bugs and memory leaks.

### Execution Order

The execution order is: `Awake()` → `OnEnable()` → `Start()` → (Update loops) → `OnDisable()` → `OnDestroy()`

| Method | Purpose | Frequency | Rules |
|---|---|---|---|
| `Awake()` | Self-initialization. Cache own components. Set internal state. | Once (on creation) | **Use for self-contained initialization**. Never access other GameObjects. |
| `OnEnable()` | Subscribe to events. Enable runtime behavior. | Every time object is enabled | **Subscribe to events here**, not in Start. |
| `Start()` | Cross-object initialization. Access other objects safely. | Once (first frame active) | **All `Awake()` calls are complete**. Safe to access other objects. |
| `OnDisable()` | Unsubscribe from events. Pause runtime behavior. | Every time object is disabled | **Unsubscribe from events here**, not in OnDestroy. |
| `OnDestroy()` | Final cleanup. Release resources. | Once (on destruction) | **Release unmanaged resources** only. |

### Update Methods

| Method | When | Use For |
|---|---|---|
| `Update()` | Every frame (variable time) | Input, non-physics movement, general game logic |
| `FixedUpdate()` | Fixed timestep (default 0.02s) | Physics calculations, Rigidbody movement |
| `LateUpdate()` | After all `Update()` calls | Camera follow, post-processing adjustments |

### Critical Rules

#### 1. **Awake for Self, Start for Others**
This single rule prevents most timing bugs:
```csharp
// GOOD
private Rigidbody2D _rigidbody;

void Awake() {
    // Self-initialization: Cache own components
    _rigidbody = GetComponent<Rigidbody2D>();
}

void Start() {
    // Cross-object initialization: Safe to access other objects
    _targetTransform = GameObject.FindGameObjectWithTag("Player").transform;
}

// BAD
void Awake() {
    // DON'T access other objects in Awake - they may not be initialized yet
    _targetTransform = GameObject.FindGameObjectWithTag("Player").transform; // Risky!
}
```

#### 2. **Subscribe in OnEnable, Unsubscribe in OnDisable**
Prevents memory leaks and handles re-activation properly:
```csharp
// GOOD
void OnEnable() {
    _eventChannel.OnEventRaised += HandleEvent;
    _playerHealth.OnDeath += HandlePlayerDeath;
}

void OnDisable() {
    _eventChannel.OnEventRaised -= HandleEvent;
    _playerHealth.OnDeath -= HandlePlayerDeath;
}

// BAD - Creates memory leaks
void Start() {
    _eventChannel.OnEventRaised += HandleEvent; // Never unsubscribed if object is disabled
}
```

#### 3. **Never Use Constructors on MonoBehaviours**
MonoBehaviour constructors are invoked by the Editor during serialization, which can cause unexpected side effects:
```csharp
// BAD - Don't do this
public class MyComponent : MonoBehaviour {
    public MyComponent() {
        // This runs during Editor serialization - very risky!
    }
}

// GOOD - Use Init methods or Awake
public class MyComponent : MonoBehaviour {
    public void Init(Config config) {
        // Explicit initialization
    }

    void Awake() {
        // Or use Awake
    }
}
```

#### 4. **Use `[RequireComponent]` for Dependencies**
Make component dependencies explicit:
```csharp
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class PlayerMovement : MonoBehaviour {
    private Rigidbody2D _rigidbody;
    void Awake() => _rigidbody = GetComponent<Rigidbody2D>();
}
```

#### 5. **Use `[DefaultExecutionOrder]` When Order Matters**
When the execution order between different script types matters:
```csharp
[DefaultExecutionOrder(-100)] // Runs before default (0)
public class GameManager : MonoBehaviour { }

[DefaultExecutionOrder(100)] // Runs after default (0)
public class UIManager : MonoBehaviour { }
```

#### 6. **Remove Empty Lifecycle Methods**
Unity still invokes empty methods with overhead:
```csharp
// BAD - Delete these if empty
void Start() { }
void Update() { }

// GOOD - Only include methods you actually use
void Awake() {
    // Actual initialization code
}
```

---

## 11. Serialization Conventions

Unity's serialization system has specific rules and patterns you must follow.

### `[SerializeField]` vs `[SerializeReference]`

#### Use `[SerializeField]` (Default, By-Value) for:
- All standard serialization needs
- Primitives, enums, Unity types, `[Serializable]` structs/classes
- Arrays/Lists of the above
- More efficient in storage, memory, and load/save time

```csharp
[SerializeField] private float _health = 100f;
[SerializeField] private List<Enemy> _enemies;
[SerializeField] private UnitConfig _config; // [Serializable] class
```

#### Use `[SerializeReference]` (By-Reference) ONLY when you need:
1. **Polymorphism** — derived class fields preserved on a base-type field
```csharp
[SerializeReference] private IAbility _ability; // Can be FireballAbility, HealAbility, etc.
```

2. **Null Support** — value serialization replaces null with a default instance

3. **Shared References within One Host** — value serialization duplicates shared references

**Caveats**:
- Renaming types/namespaces/assemblies breaks deserialization (use `[MovedFrom]` attribute)
- Not shared across different `UnityEngine.Object` instances
- More overhead than by-value serialization

### Auto-Properties
Supported via `[field: SerializeField]`:
```csharp
[field: SerializeField] public float Health { get; private set; }
```

### What Unity Does NOT Serialize
- `static`, `const`, `readonly` fields
- Multidimensional arrays (`[,]`)
- Jagged arrays (`[][]`)
- `Dictionary<TKey, TValue>` (use custom serializable wrapper or third-party solutions)
- Nested containers (`List<List<T>>`)

### ScriptableObject Runtime Values
**CRITICAL**: Never modify ScriptableObject asset fields at runtime in the Editor. Copy to runtime variables instead:
```csharp
public class EnergyConfigSO : ScriptableObject {
    public float regenRate = 1f; // Design-time value

    [NonSerialized] public float runtimeRegenRate; // Runtime value

    void OnEnable() {
        runtimeRegenRate = regenRate; // Copy on enable
    }
}
```

### Best Practices
- Prefer `[SerializeField] private` over `public` fields to maintain encapsulation
- Use `[NonSerialized]` to exclude a public field from serialization
- Use `[HideInInspector]` to hide a serialized field from the Inspector
- Use `[Tooltip("Description")]` to document serialized fields
- Use `OnValidate()` to validate serialized data and maintain invariants

---

