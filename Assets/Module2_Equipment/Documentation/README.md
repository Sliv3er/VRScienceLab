# VR Science Lab – Module 2: Scientific Equipment and Its Behavior

Module 2 provides four working laboratory instruments for the VR Science Lab training
simulation: a **digital scale**, a **hot plate**, a **stopwatch** and a **thermometer**.
Each instrument has its own C# script, VR push buttons, a TextMeshPro display, a status
LED and a small, explicit state machine. The module is self-contained: it ships with its
own scene, prefabs, materials, test objects, tests and this documentation, and does not
depend on any other student's module.

This is a training simulation, not a game: there are no scores, levels or win conditions,
and every scientific behavior is a simple documented model (see section 10).

Contents

1. [Purpose](#1-purpose)
2. [Required Unity and package versions](#2-required-unity-and-package-versions)
3. [Opening the project and building the scene](#3-opening-the-project-and-building-the-scene)
4. [Configuring the XR Origin](#4-configuring-the-xr-origin)
5. [Testing with the XR Interaction / Device Simulator](#5-testing-with-the-xr-interaction--device-simulator)
6. [Operating each instrument](#6-operating-each-instrument)
7. [How each C# script works](#7-how-each-c-script-works)
8. [State machines](#8-state-machines)
9. [Configuring instrument parameters](#9-configuring-instrument-parameters)
10. [Known limitations and simplified scientific assumptions](#10-known-limitations-and-simplified-scientific-assumptions)
11. [Integrating the module into the group project](#11-integrating-the-module-into-the-group-project)
12. [Requirement-to-implementation mapping](#12-requirement-to-implementation-mapping)
13. [Testing](#13-testing)

---

## 1. Purpose

Module 2 of the project plan ("Concevoir les équipements scientifiques et leur
comportement") covers the measuring instruments of the virtual laboratory: the tools the
user operates and that react visibly. The deliverable is a standalone VR scene with
several working instruments, their controls and their displays, implemented as small
independent scripts with a clear state machine per instrument.

Instruments implemented:

| Instrument | Script | States | Controls |
|---|---|---|---|
| Digital scale | `DigitalScale.cs` | Off, Reading | POWER, TARE |
| Hot plate | `HotPlate.cs` | Off, Heating, MaxTemperatureReached | ON, OFF, RESET |
| Stopwatch | `StopwatchTimer.cs` | Idle, Running, Paused | START, PAUSE, RESET |
| Thermometer | `Thermometer.cs` | Off, Reading (with / without sample) | MEASURE, RESET |

## 2. Required Unity and package versions

The module was written for **Unity 6.3 LTS (6000.3.x)**. `ProjectSettings/ProjectVersion.txt`
names 6000.3.16f1; any 6000.3 patch release works (Unity Hub offers to open the project with
the version you have installed). Unity 6.0 LTS (6000.0.x) also works because every package
below supports it, but the team should agree on one version in week 1.

Packages (already listed in `Packages/manifest.json`):

| Package | Version | Why |
|---|---|---|
| `com.unity.xr.interaction.toolkit` | 3.6.1 | Interactors, `XRSimpleInteractable` buttons, `XRGrabInteractable` test objects, XR UI Input Module, simulators |
| `com.unity.inputsystem` | 1.20.1 | Controller input for XRI and the keyboard/mouse debug helper |
| `com.unity.xr.management` | 4.7.0 | XR Plug-in Management (provider selection, project validation) |
| `com.unity.xr.openxr` | 1.18.0 | OpenXR provider for headset testing (Quest, PC VR) |
| `com.unity.ugui` | 2.0.0 | Contains TextMeshPro (used for all displays) and the EventSystem |
| `com.unity.test-framework` | 1.5.1 | PlayMode tests |

Package samples to import (Window > Package Manager > XR Interaction Toolkit > Samples):

* **Starter Assets** – provides the `XR Origin (XR Rig)` prefab with ray / near-far / poke
  interactors, the default input actions and the controller models. The scene builder uses
  this prefab for the camera rig.
* **XR Device Simulator** – provides the `XR Interaction Simulator` (and the older
  `XR Device Simulator`) prefabs used to test without a headset.

Other one-time project settings:

* **TextMeshPro essential resources**: Window > TextMeshPro > Import TMP Essential Resources.
* **Active Input Handling**: Edit > Project Settings > Player > Other Settings > Active Input
  Handling = *Input System Package (New)* (or *Both*). Unity asks to restart the editor the
  first time the Input System package is installed.
* **Interaction Layers** (only if you use teleportation from the Starter Assets): Project
  Settings > XR Interaction Toolkit > Interaction Layers, name layer 31 `Teleport`.

All XR APIs used are XRI 3.x APIs: `UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable`,
`XRGrabInteractable`, `UnityEngine.XR.Interaction.Toolkit.XRInteractionManager`,
`UnityEngine.XR.Interaction.Toolkit.UI.XRUIInputModule`, and
`UnityEngine.XR.Interaction.Toolkit.Inputs.Haptics.HapticImpulsePlayer`. Deprecated XRI 2.x
controller classes (`ActionBasedController`, `XRBaseController`) are not used.

## 3. Opening the project and building the scene

The Unity scene, prefabs and materials are **generated by an editor script**
(`Assets/Module2_Equipment/Editor/Module2SceneBuilder.cs`) rather than hand-authored YAML.
This keeps the scene reproducible, keeps every component connection visible in code, and
guarantees the prefabs match the scripts. Building takes a few seconds.

1. Open the folder `VRScienceLab` with Unity Hub (Add project from disk). Unity imports the
   packages from `Packages/manifest.json` and asks to enable the new Input System backend:
   answer **Yes** and let the editor restart.
2. Window > TextMeshPro > **Import TMP Essential Resources**.
3. Window > Package Manager > *XR Interaction Toolkit* > **Samples**: import **Starter Assets**
   and **XR Device Simulator**. If the Package Manager reports that the samples need the
   Shader Graph package, let it install it.
4. Run **VR Science Lab > Module 2 > Validate Project Setup** (menu bar). The Console lists
   anything still missing.
5. Run **VR Science Lab > Module 2 > Build Lab Scene**. The builder creates
   `Assets/Module2_Equipment/Materials/*.mat`, `Prefabs/*.prefab`, `Prefabs/TestObjects/*.prefab`
   and `Scenes/Module2_Lab.unity`, and opens the scene. Re-running the builder regenerates
   everything (materials are updated in place, prefabs are overwritten).
6. Later, **VR Science Lab > Module 2 > Open Lab Scene** opens the scene again.

Command-line build (optional, for CI or a clean machine):

```bash
Unity.exe -batchmode -projectPath "C:\Users\ayoub\UnityProjects\VRScienceLab" -executeMethod VRScienceLab.Module2.EditorTools.Module2SceneBuilder.BuildFromCommandLine -quit -logFile build.log
```

If you prefer to build the scene by hand (or want to understand what the builder does),
follow `SCENE_SETUP_MANUAL.md` in this folder.

Scene hierarchy produced by the builder:

```
Module2_Lab
├── Directional Light
├── Environment            Floor, Back Wall, Workbench, Title Sign, Instructions Sign
├── Instruments
│   ├── DigitalScale       (prefab instance) Body, Pan, WeighingZone, Display, StatusLed, PowerButton, TareButton, Nameplate
│   ├── HotPlate           (prefab instance) Body, HeatingElement, HeatLight, Display, StatusLed, OnButton, OffButton, ResetButton, Nameplate
│   ├── Stopwatch          (prefab instance) Body, Display, StatusLed, StartButton, PauseButton, ResetButton, Nameplate
│   └── Thermometer        (prefab instance) Body, SampleDock, SampleZone, ProbeStand/Arm/Tip, Display, StatusLed, MeasureButton, ResetButton, Nameplate
├── Test Objects           Weight_100g, Weight_250g, Weight_500g, Sample_A_ColdWater, Sample_B_RoomWater, Sample_C_HotWater
├── XR Interaction Manager
├── EventSystem            (XR UI Input Module)
├── XR Origin (XR Rig)     (Starter Assets prefab instance)
└── Module2 Debug Input (keyboard + mouse)
```

## 4. Configuring the XR Origin

The builder instantiates the Starter Assets prefab **`XR Origin (XR Rig)`** at
(0, 0, -0.35) facing the workbench. It already contains the Main Camera, both controllers,
the Near-Far Interactor (ray + grab), the Poke Interactor, the Teleport Interactor, the
Input Action Manager and the Haptic Impulse Players. Nothing needs to be changed for the
instruments to work.

Things you may want to adjust on the rig:

* **Tracking Origin Mode** (`XR Origin` component): `Floor` for a standing experience with a
  headset, `Device` plus a Camera Y Offset of about 1.6 m if the device does not report a
  floor height.
* **Locomotion**: the rig includes teleport and snap turn. They are harmless for Module 2 but
  can be disabled on the `Locomotion` child if the group's shared rig does not use them.
* **Shared rig**: if the team agrees on a common XR rig prefab, delete `XR Origin (XR Rig)`
  from the scene and drop the shared prefab in. The only requirements are: a camera, at
  least one interactor able to **select** (ray or near-far), an `XR Interaction Manager`
  somewhere in the scene, and an `EventSystem` with an `XR UI Input Module`.

If the Starter Assets are not imported when the builder runs, it falls back to the minimal
`GameObject > XR > XR Origin (VR)` rig (camera only, no controllers) and logs a warning.
Import the sample and run the builder again to get controllers.

### Headset preparation (OpenXR)

1. Edit > Project Settings > **XR Plug-in Management**: enable **OpenXR** for the Windows tab
   (PC VR / Quest Link) and, for a standalone Quest build, for the Android tab.
2. In the OpenXR settings, add the interaction profile of your controllers (for example
   *Oculus Touch Controller Profile* or *Meta Quest Touch Pro Controller Profile*).
3. Open **Project Validation** (same settings page) and press *Fix All* for the XRI and
   OpenXR entries.
4. Press Play with the headset connected. The Starter Assets rig maps **Grip** to *select*
   (press buttons, grab objects) and **Trigger** to *activate*.

## 5. Testing with the XR Interaction / Device Simulator

1. Import the **XR Device Simulator** sample (section 2).
2. Edit > Project Settings > XR Plug-in Management > **XR Interaction Toolkit**: tick
   **Use XR Interaction Simulator in scenes** (XRI 3.1 or newer). On older XRI 3.0 tick
   *Use XR Device Simulator in scenes*. The simulator prefab is then instantiated
   automatically in every scene when you press Play in the editor. (Alternative: drag the
   `XR Interaction Simulator` prefab from `Assets/Samples/XR Interaction Toolkit/<version>/XR Device Simulator/`
   into the scene.)
3. Press **Play**. The simulator shows an on-screen panel with its key bindings. Typical
   workflow:
   * Move the head with the mouse (hold the right mouse button to look) and the WASD keys.
   * Press **Tab** to cycle which device the keyboard/mouse drives (head, left controller,
     right controller); the bracket keys `[` and `]` target the left or right controller
     directly in the Interaction Simulator.
   * Point the controller ray at a button and press the **Grip** key shown in the panel
     (the shipped controls asset uses *G* for grip and *T* for trigger; check the panel if
     your copy was customized). The button animates, the display updates.
   * Grab a weight or beaker with Grip, move it over the scale pan or the thermometer dock,
     release Grip.

The instruments do not depend on which simulator is used: they only react to the
`selectEntered` event of their `XRSimpleInteractable` buttons.

### Keyboard and mouse debugging (no simulator)

The scene object `Module2 Debug Input (keyboard + mouse)` lets you drive the instruments
without any XR device while debugging logic:

| Key | Action | Key | Action |
|---|---|---|---|
| F1 | Scale power on/off | F6 | Stopwatch start |
| F2 | Scale tare | F7 | Stopwatch pause |
| F3 | Hot plate on | F8 | Stopwatch reset |
| F4 | Hot plate off | F9 | Thermometer measure on/off |
| F5 | Hot plate reset | F10 | Thermometer reset |

A left mouse click casts a ray from the main camera and presses the `InstrumentButton`
under the cursor. If you use the simulator and clicks seem to fire twice, untick
*Enable Mouse Clicks* on the debug object. Mouse clicks are ignored automatically while a
real headset is active. The helper is optional and can be deleted for the final build.

## 6. Operating each instrument

**Digital scale** – Press **POWER**: the LED turns green, the state display reads
`READING` and the value display shows `0.0 g`. Grab a weight and put it on the pan: the
display shows its mass in grams (100 g, 250 g, 500 g test weights; several objects are
summed). Press **TARE** to zero the display with the current load (state shows
`READING (TARE)`); remove the load and the display goes negative, like a real scale. Press
**POWER** again to switch off: display `OFF`, tare cleared.

**Hot plate** – Press **ON**: the LED turns green, the state reads `HEATING`, the
temperature rises from 20.0 °C at 10 °C/s, the heating element glows orange and a small
light brightens. At 300 °C the state becomes `MAX TEMP` and the temperature stops rising.
Press **OFF**: the state reads `OFF (COOLING)` while the plate cools at 5 °C/s back to
20 °C, then `OFF`. Press **RESET** to put the temperature back to 20 °C immediately
(the on/off state is kept).

**Stopwatch** – Press **START**: `RUNNING`, the display counts `mm:ss.ff`. Press **PAUSE**:
`PAUSED`, the time is frozen; **START** resumes from that time. Press **RESET**: `IDLE`,
display `00:00.00`, and the stopwatch stays stopped until START is pressed again.

**Thermometer** – Press **MEASURE**: the LED turns green. With no beaker on the dock the
display shows `-- °C` and the state `NO SAMPLE`. Put a sample beaker on the dock (under the
probe tip): the display shows that sample's configured temperature (Sample A 4.0 °C,
Sample B 22.0 °C, Sample C 80.0 °C) and the state `READING`. Remove the beaker: back to
`NO SAMPLE`. Press **MEASURE** again or **RESET** to switch off (`OFF`).

## 7. How each C# script works

All scripts live in `Assets/Module2_Equipment/Scripts` in the namespace `VRScienceLab.Module2`
and are compiled into the assembly `VRScienceLab.Module2.Runtime` (see the `.asmdef`).

| Script | Role |
|---|---|
| `InstrumentBase.cs` | Abstract base for the four instruments. Holds the presentation references (value `TMP_Text`, state `TMP_Text`, status LED renderer, LED colors) and the `onStateChanged` event. Provides allocation-free display helpers `ShowValue`, `ShowTime`, `ShowValueText`, `ShowStateText`, `SetIndicator` and `NotifyStateChanged`. The displays are only rewritten when the rounded value or the text actually changes. |
| `InstrumentState.cs` | The four state enums (`DigitalScaleState`, `HotPlateState`, `StopwatchState`, `ThermometerState`) and the convention they share. |
| `DisplayFormatter.cs` | Formats numbers (`FormatFixed`) and times (`FormatTime`, mm:ss.ff) into a reusable `char[]` so `Update()` never allocates strings. `ScaledValue` gives the rounded integer key used to detect display changes. |
| `DigitalScale.cs` | Scale behavior: power, tare, mass summation. Every frame while Reading it asks the `MeasurementZone` for the distinct Rigidbodies inside it, sums `Rigidbody.mass` (kilograms) and multiplies by 1000 (`KilogramsToGrams`). Displayed mass = raw mass − tare offset. Raises `onMeasurementChanged(float grams)`. |
| `HotPlate.cs` | Temperature model (see section 10), ON/OFF/RESET, emission color of the heating element and intensity of the optional point light through `MaterialPropertyBlock` (no material instances created). Raises `onMaximumTemperatureReached`. |
| `StopwatchTimer.cs` | Idle/Running/Paused state machine; `ElapsedSeconds` (double) accumulates `Time.deltaTime` (or `unscaledDeltaTime`) only while Running. Pause keeps the value, Reset zeroes it and returns to Idle. |
| `Thermometer.cs` | Off/Reading state machine. Each frame while Reading it resolves the current `TemperatureSample` (manual override first, otherwise the first sample inside the `MeasurementZone`) and copies its `TemperatureCelsius` into the reading (optionally approaching it at `responseRate` °C/s). Without a sample it reports `NO SAMPLE`. Raises `onReadingChanged(float celsius)`. |
| `TemperatureSample.cs` | Marker component with a name and a configured temperature. Anything the thermometer should read gets one. |
| `MeasurementZone.cs` | Trigger volume that tracks the colliders inside it (`OnTriggerEnter/Exit`), prunes destroyed or disabled ones, and offers `CollectRigidbodies` (distinct Rigidbodies, no allocation) and `TryGetOccupantComponent<T>`. Needs a trigger collider and a kinematic Rigidbody. Shared by the scale pan and the thermometer dock. `AddOccupant`/`RemoveOccupant` are public so tests and other scripts can feed it without physics. |
| `InstrumentButton.cs` | A VR push button. Requires an `XRSimpleInteractable`; subscribes to its `selectEntered` event in `OnEnable` and unsubscribes in `OnDisable`. `Press()` applies a cooldown (default 0.1 s, so a held or bouncing select does not double-trigger), moves the cap down for `pressDuration`, tints it with a `MaterialPropertyBlock`, plays an optional `AudioSource`, sends a haptic impulse to the interactor's `HapticImpulsePlayer` (Starter Assets controllers have one) and invokes the `onPressed` UnityEvent, which the prefabs connect to the instrument methods (for example `HotPlate.SwitchOn`). |
| `Module2DebugInput.cs` | Optional editor helper: keyboard shortcuts and mouse-click raycast that call `InstrumentButton.Press()` or the instrument methods directly. Compiled only when `ENABLE_INPUT_SYSTEM` is defined; otherwise it logs one warning. |
| `Editor/Module2SceneBuilder.cs` | Editor-only. Builds materials, instrument prefabs, test objects, environment, XR rig, interaction manager, event system and the debug helper, then saves the scene. Uses `SerializedObject` to fill the private serialized fields and `UnityEventTools.AddPersistentListener` to wire the button events, so every connection is visible in the Inspector. |
| `Tests/Runtime/Module2InstrumentTests.cs` | PlayMode tests (section 13). |

Design notes:

* Behavior and presentation are separated: the instrument classes own the model and call
  the base-class display helpers; nothing reads back from the TextMeshPro components.
* No instrument references another instrument. The scale and thermometer share only the
  generic `MeasurementZone`.
* Every public method is safe to call in any state (invalid transitions are ignored, see
  section 8) and tolerates missing references (missing displays or zones log a warning once
  and the instrument keeps working with a zero / no-sample reading).
* `Update()` methods do not allocate: numbers are written through `DisplayFormatter` into a
  fixed `char[]` and pushed with `TMP_Text.SetCharArray`; Rigidbody lists and property blocks
  are reused.

## 8. State machines

```
DigitalScale                       HotPlate
  Off --PowerOn/TogglePower--> Reading      Off --SwitchOn--> Heating --(T >= max)--> MaxTemperatureReached
  Reading --PowerOff/TogglePower--> Off     Heating/MaxTemperatureReached --SwitchOff--> Off
  Reading --Tare--> Reading (tare offset)   MaxTemperatureReached --ResetTemperature--> Heating (T = initial)
  (Tare while Off is ignored)               Off --ResetTemperature--> Off (T = initial)

StopwatchTimer                     Thermometer
  Idle --StartTimer--> Running              Off --StartMeasuring/ToggleMeasuring--> Reading
  Running --PauseTimer--> Paused            Reading --StopMeasuring/ToggleMeasuring/ResetMeasurement--> Off
  Paused --StartTimer--> Running            Reading: sub-status "READING" (sample found) or "NO SAMPLE"
  Any --ResetTimer--> Idle (t = 0)          (ResetMeasurement while Off just clears the reading)
  (PauseTimer while not Running is ignored; StartTimer while Running is ignored)
```

Each transition updates the state display, the LED and raises `onStateChanged(string)`.
Repeated presses of the same button are harmless: the instrument methods return early when
the requested transition is not applicable, and `InstrumentButton` ignores presses that
arrive within its cooldown.

## 9. Configuring instrument parameters

Select the instrument prefab (or its instance in the scene) and edit the Inspector fields.
Shared (from `InstrumentBase`): *Instrument Name*, *Value Display*, *State Display*,
*Status Indicator*, *Indicator Off/Active Color*, *On State Changed* event.

| Instrument | Field | Default | Meaning |
|---|---|---|---|
| Digital scale | Weighing Zone | pan zone | `MeasurementZone` whose Rigidbodies are weighed |
| | Decimals | 1 | fraction digits shown |
| | Start Switched On | false | start in Reading |
| | Clear Tare On Power Off | true | forget the tare when switched off |
| | On Measurement Changed | – | event with the displayed grams |
| Hot plate | Initial Temperature | 20 °C | start / reset temperature |
| | Maximum Temperature | 300 °C | hard upper clamp (never exceeded) |
| | Heating Rate | 10 °C/s | rise per second while on |
| | Cooling Rate | 5 °C/s | fall per second while off (0 = hold) |
| | Decimals, Unit Suffix | 1, " °C" | display format |
| | Heating Element Renderer, Element Cold/Hot Color | element | emission blend with temperature |
| | Heating Light, Heating Light Max Intensity | light, 1.5 | optional glow |
| | On Maximum Temperature Reached | – | event |
| Stopwatch | Use Unscaled Time | false | ignore `Time.timeScale` |
| Thermometer | Sample Zone | dock zone | zone searched for a `TemperatureSample` |
| | Manual Sample | none | overrides the zone (also via `SetSample()`) |
| | Response Rate | 0 °C/s | 0 = instant reading, otherwise approach speed |
| | Ambient Temperature | 20 °C | starting reading when a response rate is used |
| | Decimals, Unit Suffix, No Sample Text | 1, " °C", "--" | display format |
| | On Reading Changed | – | event with the reading |
| Button | Press Depth / Duration, Pressed Color | 6 mm / 0.15 s | visual feedback |
| | Click Audio | none | optional `AudioSource` (add a clip) |
| | Send Haptics, Amplitude, Duration | on, 0.4, 0.08 s | controller vibration |
| | Repeat Press Cooldown | 0.1 s | debounce |
| Temperature sample | Sample Name, Temperature Celsius | – | the value the thermometer reads |

Test object masses are the `Rigidbody.mass` values in kilograms (0.1, 0.25, 0.5 kg).
Change them on the test-object prefabs to get different readings; the scale shows whatever
mass the Rigidbody has.

## 10. Known limitations and simplified scientific assumptions

* **Hot plate model**: `T += heatingRate × dt` while on, clamped at the maximum;
  `T −= coolingRate × dt` while off, clamped at the initial temperature. There is no heat
  capacity, no heat transfer to objects placed on the plate, and no overshoot.
* **Thermometer**: it reads the *configured* temperature of a `TemperatureSample`. Samples do
  not warm up on the hot plate or cool down over time (a future extension could update
  `TemperatureSample.TemperatureCelsius` from another script). An optional response rate
  smooths the reading but is not a physical model. Without a sample the thermometer shows
  `-- °C` and `NO SAMPLE`; it never shows an ambient value it did not measure.
* **Digital scale**: the mass is the sum of `Rigidbody.mass` (kg × 1000) of every object whose
  collider is inside the weighing zone, including an object still held by the user inside
  the zone. There is no settling animation and no load-cell noise. Objects without a
  Rigidbody are ignored.
* **Stopwatch**: accumulates `Time.deltaTime`, so accuracy equals the frame time (about 11 ms
  at 90 Hz). The display shows hundredths of a second.
* **Interaction**: buttons react to the XRI *select* action (Grip on the Starter Assets rig).
  Poke (finger) interaction works through the Poke Interactor only if you add an
  `XRPokeFilter`; it is not configured by default.
* **Rendering**: the builder picks Standard/Unlit shaders for the Built-in pipeline and the
  Universal Render Pipeline shaders when URP is active. HDRP is not handled.
* **Fonts**: the displays use the default TMP font (LiberationSans SDF). The degree sign is
  part of its default character set; if you switch to a font without it, change the *Unit
  Suffix* fields to " C".
* **Scene file**: `Module2_Lab.unity` is produced by the builder and is not committed by hand.
  The repository ships the builder; run it once after opening the project (section 3).
* **No audio clips** are included (Unity primitives and materials only); `InstrumentButton`
  has an optional `AudioSource` slot.

## 11. Integrating the module into the group project

1. Copy the whole folder `Assets/Module2_Equipment` into the group project (or merge the
   Git branch). Keep the three `.asmdef` files; they reference only Unity packages the group
   project already needs (`Unity.XR.Interaction.Toolkit`, `Unity.InputSystem`,
   `Unity.TextMeshPro`, `Unity.XR.CoreUtils`, `UnityEngine.UI`).
2. Make sure the group project uses the same Unity/XRI versions (section 2) and has the
   TMP resources imported.
3. Run **VR Science Lab > Module 2 > Build Lab Scene** once in the group project to create
   the materials and prefabs there (or copy the generated `Materials` and `Prefabs` folders).
4. In the shared lab room scene, drag `Prefabs/DigitalScale`, `HotPlate`, `Stopwatch`,
   `Thermometer` (and the `Prefabs/TestObjects`) onto the Module 2 workbench. Requirements of
   the host scene: one `XR Interaction Manager`, an `EventSystem` with `XR UI Input Module`,
   and the shared XR rig with a selecting interactor. Nothing else from the Module 2 scene is
   needed; `Module2 Debug Input` is optional.
5. Shared result format (project plan: *experiment, name, value, unit, timestamp*): every
   instrument exposes its current value as a property and an event, so a logging script of
   the integration phase can subscribe without modifying Module 2:

```csharp
// Example: forward readings to the group's result logger.
digitalScale.onMeasurementChanged.AddListener(grams => ResultLog.Write("Module2", "mass", grams, "g"));
hotPlate.onStateChanged.AddListener(state => ResultLog.Write("Module2", "hotPlateState", hotPlate.CurrentTemperature, "°C"));
thermometer.onReadingChanged.AddListener(celsius => ResultLog.Write("Module2", "temperature", celsius, "°C"));
// Properties: digitalScale.DisplayedMassGrams, hotPlate.CurrentTemperature, stopwatch.ElapsedSeconds, thermometer.CurrentReading
```

6. Module 5 (safety) agreement: Module 2 covers measuring instruments only. A "hot plate
   left on" warning can be built by Module 5 from `HotPlate.IsSwitchedOn` and
   `onMaximumTemperatureReached`.

## 12. Requirement-to-implementation mapping

See `REQUIREMENTS_MAPPING.md` in this folder for the complete table (every requirement of
the module specification, the file/method that implements it, and how it is verified).

## 13. Testing

* **Automated**: Window > General > Test Runner > *PlayMode* > Run All. The suite
  `Module2InstrumentTests` (12 tests) covers the state machines, the kg→g conversion, tare,
  the temperature clamp, pause/resume/reset, sample detection, missing samples and button
  debouncing. One test uses PhysX trigger events and one uses real frame time.
* **Manual**: follow `TEST_CHECKLIST.md` (instrument by instrument, VR interaction, state
  transitions, missing references, repeated presses).
* **Outside Unity**: `Tools/CompileCheck/run-compile-check.ps1` compiles every module script
  with the Roslyn compiler against stub Unity/XRI/TMP assemblies and runs the same test
  methods in a console process (10 pass, 2 skipped because they need the Unity runtime).
  This was the verification available while writing the module on a machine without the
  Unity editor; it checks C# correctness and the instrument logic, not Unity-specific
  behavior such as physics, rendering or the real XRI components.
