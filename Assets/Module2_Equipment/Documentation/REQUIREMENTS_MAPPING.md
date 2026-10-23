# Module 2 – Requirement to implementation mapping

Paths are relative to `Assets/Module2_Equipment/`. "Builder" means `Editor/Module2SceneBuilder.cs`,
which creates the scene objects, prefabs and materials. Checklist IDs refer to `TEST_CHECKLIST.md`.

## Digital scale

| Requirement | Implementation | Verified by |
|---|---|---|
| 3D scale model | Builder `BuildDigitalScale`: body cube, metal pan cylinder, display, LED, nameplate → `Prefabs/DigitalScale.prefab` | visual |
| Display showing mass in grams | `DigitalScale.RefreshMeasurement` → `InstrumentBase.ShowValue(grams, decimals, " g")` | S2–S7 |
| Interactive power button | `PowerButton` (`InstrumentButton` + `XRSimpleInteractable`) → `DigitalScale.TogglePower` | V1, S2 |
| States Off / Reading | `DigitalScaleState`, `DigitalScale.State`, `PowerOn/PowerOff` | S1, S2, S8 |
| Detect object on the scale | `MeasurementZone` trigger above the pan (`WeighingZone`) | S3, `DigitalScale_DetectsObjectThroughPhysicsTrigger` |
| kg → g conversion | `DigitalScale.KilogramsToGrams`, `GramsPerKilogram = 1000` | `DigitalScale_ConvertsRigidbodyMassToGramsAndTares` |
| Zero reading when empty | `RecomputeRawMass` sums zero Rigidbodies → `0.0 g` | S2, S5 |
| Tare / reset | `DigitalScale.Tare`, `ClearTare`, tare cleared on power off | S6–S8 |
| Clear off / measuring indication | Display `OFF`, state line `OFF` / `READING` / `READING (TARE)`, LED color | S1, S2, S6 |
| No invented mass | Reading is only `Rigidbody.mass` of detected objects; documented in README §10 | code review |

## Hot plate

| Requirement | Implementation | Verified by |
|---|---|---|
| 3D hot plate model | Builder `BuildHotPlate` → `Prefabs/HotPlate.prefab` | visual |
| ON and OFF controls | `OnButton` → `HotPlate.SwitchOn`, `OffButton` → `HotPlate.SwitchOff` | H2, H5 |
| Temperature display in °C | `HotPlate.RefreshDisplay` → `ShowValue(temp, decimals, " °C")` | H1–H4 |
| States Off / Heating (+ max) | `HotPlateState`, `HotPlate.State` | H2, H4, H5 |
| Configurable initial / maximum / rate | `initialTemperature`, `maximumTemperature`, `heatingRate` (Inspector) | H3, H4 |
| Temperature rises over time | `HotPlate.AdvanceTime(Time.deltaTime)` from `Update` | H3, `HotPlate_HeatsAtConfiguredRateAndClampsAtMaximum` |
| Reset to initial temperature | `HotPlate.ResetTemperature` → `ResetButton` | H6 |
| Visible operating state | State line + LED (`SetIndicator`) | H2, H5 |
| Optional heating indicator | Emission on `HeatingElement` + `HeatLight` via `UpdateHeatVisuals` | H2 |
| Stops heating when off, never exceeds max | `AdvanceTime` only heats while `IsSwitchedOn`; `Mathf.Min(..., maximumTemperature)`; `OnValidate` clamp | H4, H5, R7, automated |
| Simple documented model | README §10 | – |

## Stopwatch

| Requirement | Implementation | Verified by |
|---|---|---|
| Start, Pause, Reset controls | `StartButton`/`PauseButton`/`ResetButton` → `StartTimer`/`PauseTimer`/`ResetTimer` | T2–T5 |
| Digital time display | `ShowTime` → `DisplayFormatter.FormatTime` (mm:ss.ff) | T1–T5, `DisplayFormatter_FormatsStopwatchTime` |
| States Idle / Running / Paused | `StopwatchState`, `StopwatchTimer.State` | `Stopwatch_StartPauseResumeReset` |
| Accurate elapsed time from Unity time | `AdvanceTime(Time.deltaTime)` in `Update`, `double ElapsedSeconds` | `Stopwatch_AccumulatesFrameTimeWhileRunning` |
| Pause preserves time, Reset zeroes | `PauseTimer` keeps `ElapsedSeconds`; `ResetTimer` sets 0 and Idle | T3–T5, automated |
| Does not run after pause/reset | `AdvanceTime` returns unless `IsRunning` | automated |

## Thermometer

| Requirement | Implementation | Verified by |
|---|---|---|
| 3D thermometer model | Builder `BuildThermometer` (base, dock, probe stand/arm/tip, display) → `Prefabs/Thermometer.prefab` | visual |
| Digital temperature display in °C | `Thermometer.RefreshDisplay` → `ShowValue(reading, decimals, " °C")` | M3–M5 |
| Interactive measurement control | `MeasureButton` → `ToggleMeasuring` | M2 |
| States Off / Reading | `ThermometerState`, `Thermometer.State` | M1, M2, M7 |
| Select or detect a sample | `MeasurementZone` on the dock + `SetSample()` manual override | M3, `Thermometer_DetectsSampleInZone`, `Thermometer_ReadsSelectedSample…` |
| Indication when unavailable | Display `-- °C`, state `NO SAMPLE` | M2, M6 |
| Reset / stop | `StopMeasuring`, `ResetMeasurement` → `ResetButton` | M7 |
| Temperature sample component | `TemperatureSample.cs` (name + configured °C), three beaker prefabs | M3, M4 |
| Reading tied to the sample | `AdvanceTime` copies `CurrentSample.TemperatureCelsius`; README §10 states the limits | automated (value follows sample change) |

## VR interaction

| Requirement | Implementation | Verified by |
|---|---|---|
| Controller ray interaction for buttons | `XRSimpleInteractable` on each button; Starter Assets rig with Near-Far (ray) interactors | V1–V3 |
| Colliders and interaction components | Box collider on every button cap (cylinder capsule replaced by a box in `CreatePrimitive`) | V3 |
| Button events connected to C# methods | `UnityEventTools.AddPersistentListener(button.onPressed, method)` in the builder (visible in the Inspector) | V3 |
| Visible feedback | `InstrumentButton.BeginVisual` (press depth + color), haptics | V1 |
| Readable displays | World-space TextMeshPro, tilted 30° toward the user, auto-sized | V3 |
| Consistent behavior across instruments | All buttons are `InstrumentButton`; all instruments derive from `InstrumentBase` | V3 |
| Editor interaction for debugging | `Module2DebugInput` (F1–F10 keys, mouse click raycast) | README §5 |
| XR Device Simulator support | Scene uses the standard rig; simulator enabled via project setting (README §5) | V1–V6 |
| Complete interaction system | Scene contains `XR Interaction Manager`, `EventSystem` + `XRUIInputModule`, rig with interactors; grabbables have `XRGrabInteractable` + Rigidbody | V1–V5 |

## Architecture, structure, quality

| Requirement | Implementation |
|---|---|
| One primary script per instrument | `DigitalScale.cs`, `HotPlate.cs`, `StopwatchTimer.cs`, `Thermometer.cs` |
| Supporting scripts | `TemperatureSample.cs`, `InstrumentState.cs`, `InstrumentBase.cs`, `InstrumentButton.cs` (shared feedback), `MeasurementZone.cs`, `DisplayFormatter.cs`, `Module2DebugInput.cs` |
| Serialized fields, null checks, error messages | `[SerializeField]` + tooltips everywhere; `Awake` warnings for missing references; `MeasurementZone` disables itself without a collider |
| Behavior separated from presentation | Instruments compute; `InstrumentBase` presents |
| Instruments independent | No instrument references another |
| No external services | None used |
| Project structure | `Scenes/`, `Scripts/`, `Prefabs/`, `Materials/`, `Documentation/`, plus `Editor/` and `Tests/` |
| Module independent of other modules | Own scene, rig, test objects |
| Compiles, compatible APIs, no deprecated APIs | XRI 3.x namespaces; `FindFirstObjectByType`/`FindObjectsByType`; TMP `textWrappingMode`; verified with the Roslyn compile check (README §13) |
| No allocations in Update | `DisplayFormatter` char buffer, `SetCharArray`, reused lists / property blocks |
| Invalid transitions prevented | Early returns in every transition method |
| Inspector parameters | README §9 |
| Comments | XML summaries on every class and public member |
| `Time.deltaTime` for elapsed time | `HotPlate.Update`, `StopwatchTimer.Update`, `Thermometer.Update` |
| Displays consistent with model | Displays are written from the model values each refresh; tests assert the model, checklist asserts the display |
| Test checklist | `TEST_CHECKLIST.md` |
| README | `README.md` |
| Required packages and setup steps | README §2–§3 |
