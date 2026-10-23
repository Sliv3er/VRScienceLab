# Module 2 – Manual scene construction

The menu item **VR Science Lab > Module 2 > Build Lab Scene** does everything below
automatically. This page documents the same steps so that the scene can be rebuilt by hand,
understood, or adapted in the group project. Dimensions are in meters; the workbench top is
at y = 0.9.

## 1. Scene and environment

1. File > New Scene (Basic), save as `Assets/Module2_Equipment/Scenes/Module2_Lab.unity`.
2. Delete the default Main Camera (the XR rig brings its own).
3. Directional Light: rotation (50, −30, 0), intensity 1, soft shadows.
   Window > Rendering > Lighting > Environment: Ambient = Color, (0.42, 0.44, 0.48).
4. Empty `Environment` object containing:
   * `Floor`: Plane, scale (1.5, 1, 1.5), material `Lab_Floor`.
   * `Back Wall`: Cube, position (0, 1.5, 1.35), scale (8, 3, 0.1), material `Lab_Wall`.
   * `Workbench`: Cube, position (0, 0.45, 0.6), scale (2.8, 0.9, 0.8), material `Lab_Bench`.
   * Two 3D TextMeshPro signs on the wall (title and instructions).

Materials (`Assets/Module2_Equipment/Materials`): Standard shader (Built-in) or
Universal Render Pipeline/Lit (URP). `Instrument_DisplayPanel` and `Instrument_Led` use an
Unlit shader so the LED color set from code is shown as-is. `HotPlate_Element` has
**Emission** enabled (black) so the hot plate can brighten it at runtime.

## 2. Common instrument parts

**Display**: empty `Display` object rotated (30, 0, 0) (top tilted away from the user), with
a thin Cube `Panel` (no collider, material `Instrument_DisplayPanel`) and two 3D TextMeshPro
children placed 5.5 mm in front of the panel (local z = −0.0055): `ValueText` (bold, green,
auto-size, no wrapping) and `StateText` (smaller, same color). Assign them to the
instrument's *Value Display* and *State Display*.

**Status LED**: Sphere, scale 0.014, collider removed, material `Instrument_Led`. Assign to
*Status Indicator*.

**Button** (repeat per control):

1. Empty object `XxxButton` at the position given below.
2. Child `Cap`: Cylinder, position (0, 0.006, 0), scale (0.05, 0.006, 0.05), the button color
   material. Remove its Capsule Collider and add a **Box Collider** (size 1, 2, 1).
3. On `XxxButton` add **XR Simple Interactable** (XRI) and **Instrument Button**
   (VR Science Lab/Module 2). Set *Button Label*, *Press Visual* = `Cap`,
   *Highlight Renderer* = `Cap`'s Mesh Renderer.
4. In *On Pressed ()* add the instrument object and pick the method (for example
   `HotPlate.SwitchOn`).
5. Child 3D TextMeshPro `Label` at (0, 0.001, −0.036), rotation (90, 0, 0), bold, size 0.11.

**Measurement zone** (scale pan, thermometer dock): empty object with a **Box Collider**
(*Is Trigger* on), a **Rigidbody** (*Is Kinematic* on, *Use Gravity* off) and the
**Measurement Zone** component.

**Nameplate**: 3D TextMeshPro on the front face of the body (bold, size 0.2).

## 3. Instruments (local positions relative to the instrument root placed on the bench)

### DigitalScale – root at (−0.95, 0.9, 0.55)

| Part | Primitive | Position | Scale | Notes |
|---|---|---|---|---|
| Body | Cube | (0, 0.02, 0) | (0.30, 0.04, 0.32) | `Instrument_Body` |
| Pan | Cylinder | (0, 0.045, 0.06) | (0.16, 0.005, 0.16) | `Scale_Pan`, box collider |
| WeighingZone | zone | (0, 0.10, 0.06) | collider size (0.16, 0.10, 0.16) | → *Weighing Zone* |
| Display | – | (−0.04, 0.07, −0.09) | panel (0.15, 0.06, 0.01) | initial text `OFF` |
| StatusLed | Sphere | (−0.125, 0.045, −0.12) | 0.014 | |
| PowerButton | button | (0.07, 0.04, −0.12) | red | → `DigitalScale.TogglePower` |
| TareButton | button | (0.125, 0.04, −0.12) | blue | → `DigitalScale.Tare` |
| Nameplate | text | (0, 0.02, −0.161) | | `DIGITAL SCALE` |

Add the **Digital Scale** component to the root, *Instrument Name* `Digital Scale`.

### HotPlate – root at (−0.32, 0.9, 0.55)

| Part | Primitive | Position | Scale | Notes |
|---|---|---|---|---|
| Body | Cube | (0, 0.04, 0) | (0.36, 0.08, 0.34) | |
| HeatingElement | Cylinder | (0, 0.084, 0.05) | (0.20, 0.004, 0.20) | `HotPlate_Element`, box collider → *Heating Element Renderer* |
| HeatLight | Point Light | (0, 0.16, 0.05) | | color (1, 0.45, 0.15), range 0.6, intensity 0, disabled → *Heating Light* |
| Display | – | (−0.09, 0.11, −0.10) | panel (0.15, 0.06) | initial `20.0 °C` |
| StatusLed | Sphere | (−0.165, 0.09, −0.14) | 0.014 | |
| OnButton | button | (0.02, 0.08, −0.12) | green | → `HotPlate.SwitchOn` |
| OffButton | button | (0.08, 0.08, −0.12) | red | → `HotPlate.SwitchOff` |
| ResetButton | button | (0.14, 0.08, −0.12) | gray | → `HotPlate.ResetTemperature` |
| Nameplate | text | (0, 0.04, −0.171) | | `HOT PLATE` |

Add the **Hot Plate** component to the root.

### Stopwatch – root at (0.32, 0.9, 0.55)

| Part | Primitive | Position | Scale | Notes |
|---|---|---|---|---|
| Body | Cube | (0, 0.015, 0) | (0.22, 0.03, 0.16) | |
| Display | – | (0, 0.06, 0.03) | panel (0.18, 0.06) | initial `00:00.00` |
| StatusLed | Sphere | (0.10, 0.035, 0.03) | 0.014 | |
| StartButton | button | (−0.07, 0.03, −0.045) | green | → `StopwatchTimer.StartTimer` |
| PauseButton | button | (0, 0.03, −0.045) | blue | → `StopwatchTimer.PauseTimer` |
| ResetButton | button | (0.07, 0.03, −0.045) | gray | → `StopwatchTimer.ResetTimer` |
| Nameplate | text | (0, 0.015, −0.081) | | `STOPWATCH` |

Add the **Stopwatch Timer** component to the root.

### Thermometer – root at (0.95, 0.9, 0.55)

| Part | Primitive | Position | Scale | Notes |
|---|---|---|---|---|
| Body | Cube | (0, 0.02, 0) | (0.30, 0.04, 0.32) | |
| SampleDock | Cylinder | (−0.06, 0.044, 0.06) | (0.12, 0.004, 0.12) | `Thermometer_Dock`, box collider |
| SampleZone | zone | (−0.06, 0.10, 0.06) | collider (0.13, 0.10, 0.13) | → *Sample Zone* |
| ProbeStand | Cylinder | (0.08, 0.14, 0.06) | (0.012, 0.10, 0.012) | no collider |
| ProbeArm | Cylinder | (0.01, 0.235, 0.06), rot (0, 0, 90) | (0.008, 0.075, 0.008) | no collider |
| ProbeTip | Cylinder | (−0.06, 0.19, 0.06) | (0.006, 0.045, 0.006) | no collider |
| Display | – | (0.06, 0.07, −0.09) | panel (0.15, 0.06) | initial `OFF` |
| StatusLed | Sphere | (0.135, 0.045, −0.12) | 0.014 | |
| MeasureButton | button | (−0.12, 0.04, −0.12) | green | → `Thermometer.ToggleMeasuring` |
| ResetButton | button | (−0.06, 0.04, −0.12) | gray | → `Thermometer.ResetMeasurement` |
| Nameplate | text | (0, 0.02, −0.161) | | `THERMOMETER` |

Add the **Thermometer** component to the root.

Save each instrument root as a prefab in `Assets/Module2_Equipment/Prefabs`.

## 4. Test objects (`Test Objects` parent, z = 0.85 on the bench)

| Object | Primitive | Scale | Rigidbody mass | Extra |
|---|---|---|---|---|
| Weight_100g | Cube | 0.05 | 0.1 kg | label `100 g` |
| Weight_250g | Cube | 0.06 | 0.25 kg | label `250 g` |
| Weight_500g | Cube | 0.07 | 0.5 kg | label `500 g` |
| Sample_A_ColdWater | Cylinder (box collider) | (0.07, 0.05, 0.07) | 0.2 kg | `TemperatureSample` 4 °C |
| Sample_B_RoomWater | Cylinder | same | 0.2 kg | `TemperatureSample` 22 °C |
| Sample_C_HotWater | Cylinder | same | 0.2 kg | `TemperatureSample` 80 °C |

Each has a **Rigidbody** (Interpolate, Continuous Dynamic) and an **XR Grab Interactable**
(*Use Dynamic Attach* on, *Throw On Detach* on). Place weights near the scale (x ≈ −1.0)
and beakers near the thermometer (x ≈ 0.9).

## 5. XR setup

1. GameObject > XR > **Interaction Manager** (or an empty object with `XR Interaction Manager`).
2. Empty `EventSystem` with **Event System** and **XR UI Input Module** (no other input module).
3. Drag `Assets/Samples/XR Interaction Toolkit/<version>/Starter Assets/Prefabs/XR Origin (XR Rig)`
   into the scene at (0, 0, −0.35), rotation 0.
4. Optional: empty object with **Debug Input (keyboard + mouse)** and the four instruments
   assigned.
5. Project Settings > XR Plug-in Management > XR Interaction Toolkit > tick
   *Use XR Interaction Simulator in scenes* for editor testing; enable OpenXR for the headset.
