# HeadlessSim: run the project without Unity

`HeadlessSim` is a functional stand-in for the Unity engine and editor, written in plain C#.
It lets the Module 2 code (runtime scripts, the editor scene builder and the PlayMode tests) be
compiled with the Roslyn compiler and **executed in a console process** on a machine that has
no Unity editor. Nothing in this folder is part of the Unity project and nothing here must
ever be copied into `Assets`.

## Run it

```bash
powershell -ExecutionPolicy Bypass -File Tools/HeadlessSim/run-headless-sim.ps1
```

Exit code 0 means everything passed, 1 means a run failure (exception, logged error, failed
check or failed test), 2 means a compile error.

| Option | Effect |
|---|---|
| `-AssetsRoot <path>` | Compile another copy of the project's `Assets` folder (default: the repository's). |
| `-SkipTests` | Do not run the PlayMode tests. |
| `-SkipBuild` | Do not build and play the lab scene (tests that load it are skipped). |
| `-Tests <text>` | Only test fixtures whose full name contains the text. |
| `-StarterAssets` | Pretend the XRI "Starter Assets" rig and the XR simulator prefab are imported. |
| `-NavMeshMap`, `-Mark <object>` | Print an ASCII map of the baked NavMesh; the children of the named object are lettered on it. |
| `-NoSelfTest` | Skip the simulator's own self-tests. |
| `-VerboseLog` | Also print plain `Debug.Log` messages. |
| `-NoRun` | Compile only. |

Requirements: the Visual Studio Build Tools C# compiler (Roslyn `csc.exe`) and .NET Framework 4.x.
Output goes to `Tools/HeadlessSim/out/` (git-ignored). The generated PNG files the scene
builder writes land in `out/project/Assets/...`, never in the repository.

## What a run does

0. **Self-tests** of the simulator itself (transform math, lifecycle, cloning, serialization,
   events, triggers, gravity, navigation, sockets, UI, editor API).
1. **Edit mode**: calls `Module2SceneBuilder.BuildLabScene()`. Any exception or `Debug.LogError`
   is a failure; warnings are printed.
2. **Scene summary**: root objects, component histogram, NavMesh statistics.
3. **Play mode**: reloads the scene from its saved (serialized) state, enters play mode and
   simulates 3 seconds. Reports exceptions, errors and where every Rigidbody rests.
4. **Project checks**: `Harness/ProjectChecks.cs` holds the checks of the combined heating
   station, the storage cabinet and the robot control screen (Inspector references, button
   wiring, shelf fit, door sweep, grab / release / socket flows, NavMesh reachability, every
   robot screen button). Add more with `ctx.Check(condition, "message")`; the scene is still running.
5. **PlayMode tests**: every `[Test]` / `[UnityTest]` of the project, each in a fresh world.
   Tests that load the lab scene use the scene saved in step 1.

## What is simulated

- **Scene graph** with real transform math (position, rotation, scale through the hierarchy,
  `RectTransform` anchors), `GameObject.Find`, `GetComponent*`, activation, Unity-style null.
- **Lifecycle**: edit mode runs no messages (only `Reset` on `AddComponent`); play mode runs
  `Awake`, `OnEnable`, `Start`, `FixedUpdate` (50 Hz), `Update`, `LateUpdate`, `OnDisable`,
  `OnDestroy`, `Invoke` timers, coroutines, deferred `Destroy`, `Time.timeScale`.
- **Serialization rules**: `Instantiate`, prefab save / instantiate and scene save / load copy
  only what Unity serializes (public or `[SerializeField]` fields), remap references inside the
  copied hierarchy and keep persistent `UnityEvent` listeners. `SerializedObject.FindProperty`
  returns null for a field that does not exist, and refuses values of the wrong type.
- **UnityEvent** persistent listeners (`UnityEventTools`), invoked in play mode only, like Unity.
- **UI**: `Button.onClick` through `ExecuteEvents` / `IPointerClickHandler`, interactable and
  `CanvasGroup` rules, TextMeshPro text as data.
- **Physics**: colliders with correct world-space bounds, trigger enter / stay / exit from
  box overlaps, bounds-based raycasts and overlap queries, and a vertical-only gravity step
  (a body falls straight down onto the collider box below it and gets `OnCollisionEnter`).
- **Navigation**: `NavMeshSurface.BuildNavMesh` bakes a 5 cm occupancy grid for the default
  Humanoid agent (radius 0.5 m, height 2 m), honouring `NavMeshModifier` (ignore / area),
  carving `NavMeshObstacle`s, `NavMesh.SamplePosition` / `CalculatePath`, and a `NavMeshAgent`
  that plans with A* and walks its path (`isOnNavMesh`, `SetDestination`, `isStopped`,
  `pathStatus`, `remainingDistance`, `Warp`, Unity's own error messages when off the mesh).
- **XR Interaction Toolkit**: interactables and sockets with selection state.
  `Simulation.Select(simpleInteractable)` emulates a ray press, `Simulation.Grab / Release`
  an emulated hand; a free socket takes a released grab interactable whose collider overlaps
  the socket's trigger collider.
- **Editor**: in-memory `AssetDatabase` (with `FindAssets`), `PrefabUtility`,
  `EditorSceneManager`, `EditorBuildSettings`, `UnityEventTools`, animator-controller and
  importer types as data.
- **Input System**: `Keyboard.SimCreate().SimTap(Key.F1)` and `Mouse.SimCreate()`.

## What it does not prove

- It is a model of Unity, not Unity. A green run means the C# is valid, the builder executes,
  the object graph is wired as intended and the logic behaves as written. It does not replace
  opening the project in the Unity editor.
- **API existence**: the stand-in declares the members the project uses. A member that exists
  here could still be missing or different in the real packages (see the list below).
- **No rendering**, no shaders, no lighting, no audio output.
- **No real rigid-body dynamics**: no rotation, lateral pushes, friction, bouncing or stacking
  stability. Bounds are axis-aligned boxes, so rotated or hollow shapes are approximated.
- **NavMesh**: a grid approximation of the bake (flat floors only, no slopes, steps or
  off-mesh links); agents do not avoid each other.
- **XR**: no real controllers, rays or poke; UI raycasters do not raycast (clicks are sent
  straight to the button). `TrackedDeviceGraphicRaycaster` vs `GraphicRaycaster` behaviour with
  a mouse is therefore not exercised.
- The avatar model (FBX), reflection probe bake and package samples are absent: the builder's
  own fallbacks run and its warnings are printed.
- `OnValidate` is never called.

## Members whose real-Unity behaviour was assumed

These were written from memory of the Unity 6 / XRI 3.x / AI Navigation 2.x API and could not be
checked against the real assemblies on this machine:

- `XRSocketInteractor` selection rules (recycle delay, hover before select) and the
  `XRGrabInteractable` changes to `Rigidbody.isKinematic` / `useGravity` while held.
- `XRBaseInteractable` collecting child colliders when its collider list is empty.
- `NavMeshAgent.hasPath` becoming false on arrival, and the 1 m placement radius of an agent.
- Carving obstacles being inflated by the agent radius.
- `MonoBehaviour.Reset` being called by `AddComponent` in edit mode.
- `TMP_Text.SetText("{0}", value)` formatting without an explicit precision (`{0:1}` is exact).
- Persistent `UnityEvent` listeners added by `UnityEventTools` being runtime-only.
- `GetComponentInChildren` / `GetComponentInParent` skipping inactive objects by default.
- `Shader.Find` returning null for render-pipeline shaders (the project then uses `Standard`).

## Files

- `Engine/` the simulated engine (one file per area: math, core objects, simulation loop,
  graphics, physics, events, UI and TextMeshPro, navigation, XR and input, editor, test framework).
- `Harness/Program.cs` the console program; `Harness/ProjectChecks.cs` the extension point;
  `Harness/CheckContext.cs` the helper passed to it; `Harness/SelfTests.cs` the engine self-tests.
- `run-headless-sim.ps1` compiles and runs everything.
