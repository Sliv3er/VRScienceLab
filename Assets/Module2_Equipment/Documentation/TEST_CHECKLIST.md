# Module 2 – Test checklist

Tick each item in the Unity editor (Play mode with the XR Interaction Simulator, or with a
headset). "Display" means the TextMeshPro value display on the instrument; "State" means the
smaller state line under it; "LED" means the small status sphere.

Automated coverage: `Assets/Module2_Equipment/Tests/Runtime/Module2InstrumentTests.cs`
(Window > General > Test Runner > PlayMode). The last column names the automated test that
covers the same behavior where one exists.

## Digital scale

| # | Check | Expected | Automated test |
|---|---|---|---|
| S1 | Enter Play mode | Display `OFF`, State `OFF`, LED dark | `HotPlate_StartsOff…` (same pattern), `DigitalScale_Converts…` asserts `Off` |
| S2 | Press POWER | Display `0.0 g`, State `READING`, LED green | `DigitalScale_Converts…` |
| S3 | Place Weight_250g on the pan | Display `250.0 g` | `DigitalScale_Converts…`, `DigitalScale_DetectsObjectThroughPhysicsTrigger` |
| S4 | Add Weight_100g | Display `350.0 g` (Rigidbody 0.35 kg × 1000) | `DigitalScale_Converts…` |
| S5 | Remove all weights | Display `0.0 g` | `DigitalScale_Converts…` |
| S6 | With Weight_500g on the pan press TARE | Display `0.0 g`, State `READING (TARE)` | `DigitalScale_Converts…` |
| S7 | Remove Weight_500g | Display `-500.0 g` | – |
| S8 | Press POWER (off) then POWER (on) | Display `OFF` then `0.0 g`; tare cleared (State `READING`) | `DigitalScale_Converts…` |
| S9 | Press TARE while off | Nothing changes (ignored) | – |

## Hot plate

| # | Check | Expected | Automated test |
|---|---|---|---|
| H1 | Enter Play mode | Display `20.0 °C`, State `OFF`, element dark, LED dark | `HotPlate_StartsOffAtInitialTemperature` |
| H2 | Press ON | State `HEATING`, LED green, temperature rising, element glows | `HotPlate_HeatsAtConfiguredRate…` |
| H3 | Watch for 10 s | Display about `120.0 °C` (20 + 10 °C/s × 10 s) | `HotPlate_HeatsAtConfiguredRate…` (1 s → +10 °C) |
| H4 | Wait (or set Maximum Temperature to 60 in the Inspector) | Display stops at `300.0 °C` (or 60.0), State `MAX TEMP` | `HotPlate_HeatsAtConfiguredRate…` |
| H5 | Press OFF | State `OFF (COOLING)`, temperature falls 5 °C/s, never rises | `HotPlate_StopsHeatingWhenSwitchedOff…` |
| H6 | Press RESET | Display `20.0 °C` immediately; if ON it reads `HEATING` again | `HotPlate_StopsHeatingWhenSwitchedOff…` |
| H7 | Press ON twice, OFF twice | No errors, state as after a single press | `HotPlate_StopsHeatingWhenSwitchedOff…` |

## Stopwatch

| # | Check | Expected | Automated test |
|---|---|---|---|
| T1 | Enter Play mode | Display `00:00.00`, State `IDLE` | `Stopwatch_StartPauseResumeReset` |
| T2 | Press START | State `RUNNING`, LED green, display counting | `Stopwatch_StartPauseResumeReset`, `Stopwatch_AccumulatesFrameTime…` |
| T3 | Press PAUSE after ~3 s | State `PAUSED`, display frozen at ~`00:03.00` | `Stopwatch_StartPauseResumeReset` |
| T4 | Press START | Counting resumes from the paused value | `Stopwatch_StartPauseResumeReset` |
| T5 | Press RESET | Display `00:00.00`, State `IDLE`, not counting | `Stopwatch_StartPauseResumeReset` |
| T6 | Press PAUSE while idle, START twice, RESET twice | No errors; behaves as a single press | `Stopwatch_StartPauseResumeReset` |

## Thermometer

| # | Check | Expected | Automated test |
|---|---|---|---|
| M1 | Enter Play mode | Display `OFF`, State `OFF` | `Thermometer_ReadsSelectedSample…` |
| M2 | Press MEASURE with an empty dock | Display `-- °C`, State `NO SAMPLE`, LED green | `Thermometer_ReadsSelectedSample…` |
| M3 | Place Sample_C_HotWater on the dock | Display `80.0 °C`, State `READING` | `Thermometer_DetectsSampleInZone` |
| M4 | Swap for Sample_A_ColdWater | Display `4.0 °C` | `Thermometer_ReadsSelectedSample…` (sample value change) |
| M5 | Unit | Display ends with ` °C` | `DisplayFormatter_FormatsFixedDecimals` |
| M6 | Remove the beaker | Display `-- °C`, State `NO SAMPLE` | `Thermometer_DetectsSampleInZone` |
| M7 | Press RESET | Display `OFF`, State `OFF` | `Thermometer_ReadsSelectedSample…` |
| M8 | Press MEASURE twice | On then off (toggle) | – |

## VR interaction

| # | Check | Expected |
|---|---|---|
| V1 | Simulator: point the right controller ray at POWER and press Grip | Button cap moves down and turns yellow for 0.15 s, scale turns on |
| V2 | Same with the left controller | Same result (both hands work) |
| V3 | Each of the 10 buttons | Each one triggers its action; the display updates within one frame |
| V4 | Grab Weight_100g (Grip), move it over the pan, release | Falls onto the pan, display shows `100.0 g` |
| V5 | Grab Sample_B_RoomWater and place it on the dock | Thermometer reads `22.0 °C` while measuring |
| V6 | Hold Grip on a button | Only one press is registered (no auto-repeat) |
| V7 | Headset (OpenXR) | Same behavior as V1–V5; controller vibrates briefly on each press |
| V8 | Project Settings > XR Plug-in Management | OpenXR enabled, Project Validation shows no errors |

## State transitions, missing references, robustness

| # | Check | Expected |
|---|---|---|
| R1 | Every transition in README section 8, including invalid ones | Invalid requests are ignored without errors |
| R2 | Clear *Value Display* on an instrument and press Play | One warning in the Console, the instrument still works (state line and LED update) |
| R3 | Clear *Weighing Zone* on the scale | One warning, scale reads `0.0 g` while on |
| R4 | Clear *Sample Zone* on the thermometer | One warning, thermometer reports `NO SAMPLE` until `SetSample` is used |
| R5 | Delete the `XR Interaction Manager` from the scene | XRI creates one automatically at runtime; buttons still work |
| R6 | Press every button 10 times quickly | No errors, no double counting on the stopwatch |
| R7 | Set Hot plate Maximum Temperature below Initial Temperature in the Inspector | Value is clamped to the initial temperature (OnValidate) |
| R8 | Profiler (Play mode, Scripts): instruments in Reading/Heating/Running | No per-frame GC allocation from the Module 2 scripts |
| R9 | Test Runner > PlayMode > Run All | 12/12 pass |
