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

## Heating station: heater (left half)

| # | Check | Expected | Automated test |
|---|---|---|---|
| H1 | Enter Play mode | Display `20.0 °C`, State `OFF`, element dark, LED dark | `HotPlate_StartsOffAtInitialTemperature` |
| H2 | Press ON | State `HEATING`, LED green, temperature rising, element glows | `HotPlate_HeatsAtConfiguredRate…` |
| H3 | Watch for 10 s | Display about `120.0 °C` (20 + 10 °C/s × 10 s) | `HotPlate_HeatsAtConfiguredRate…` (1 s → +10 °C) |
| H4 | Wait (or set Maximum Temperature to 60 in the Inspector) | Display stops at `300.0 °C` (or 60.0), State `MAX TEMP` | `HotPlate_HeatsAtConfiguredRate…` |
| H5 | Press OFF | State `OFF (COOLING)`, temperature falls 5 °C/s, never rises | `HotPlate_StopsHeatingWhenSwitchedOff…` |
| H6 | Press RESET | Display `20.0 °C` immediately; if ON it reads `HEATING` again | `HotPlate_StopsHeatingWhenSwitchedOff…` |
| H7 | Press ON twice, OFF twice | No errors, state as after a single press | `HotPlate_StopsHeatingWhenSwitchedOff…` |

## Heating station: timer (right half)

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

## Combined heating station, storage cabinet, robot control screen

Automated tests are in `Tests/Runtime/Module2StationCabinetRobotTests.cs` (PlayMode). Tests whose
name starts with `LabScene_` need the generated scene (run *Build Lab Scene* first).

### Combined heating station

| # | Check | Expected | Automated test |
|---|---|---|---|
| C1 | Look at the bench | One housing carries the heating surface, the temperature display, the timer display and both button groups; no separate hot plate or stopwatch | `LabScene_HeatingStationIsOnePieceOfEquipment` |
| C2 | Press ON, then OFF | `HEATING`, element glows, strip on the front edge turns orange; OFF: `OFF (COOLING)` | `LabScene_HeatingStationIsOnePieceOfEquipment`, `HeatingStation_HeaterAndTimerAreIndependent` |
| C3 | Watch the temperature for 5 s after ON | rises 10 °C per second from 20.0 °C | same |
| C4 | Press OFF | the temperature stops rising and falls 5 °C per second | same |
| C5 | Timer START | `RUNNING`, counts `mm:ss.ff`; the heater state does not change | same |
| C6 | Timer PAUSE | `PAUSED`, time frozen | same |
| C7 | Timer START again | continues from the paused time | same |
| C8 | Timer RESET | `00:00.00`, `IDLE` | same |
| C9 | Put a beaker with water on the heating surface, press ON, then measure it with the thermometer | the ring around the surface lights; the sample warms 2 °C per second (maximum 100 °C) | `HeatingSurface_WarmsTheSampleStandingOnThePlate`, `LabScene_CabinetHoldsItsEquipmentAndTheDoorsWork` |

### Laboratory cabinet

| # | Check | Expected | Automated test |
|---|---|---|---|
| K1 | Look at the left wall | Middle unit of the storage wall: steel-framed glass doors with handles, three levels of equipment, status line `DOORS CLOSED / 23 / 23 STORED` | `LabScene_CabinetHoldsItsEquipmentAndTheDoorsWork` |
| K2 | Ray on a door handle + Grip (or left click, or C) | the door swings open in about one second, the interior light brightens, status `DOOR OPEN` / `DOORS OPEN` | `CabinetDoor_OpensAndClosesAroundItsHinge`, `CabinetDoor_SwingsOverTimeInPlayMode` |
| K3 | Same again | the door closes | same |
| K4 | Look inside | beakers, specimen jars, vials and tape on the counter; test tube rack and Erlenmeyer flasks on shelf 1; reagent bottles on shelf 2 | `LabScene_CabinetHoldsItsEquipmentAndTheDoorsWork` |
| K5 | Grab a test tube from the rack | it leaves its hole, the rack shows `4 / 6`, the cabinet `22 / 23` once it is outside | manual (needs XR input) |
| K6 | Grab an Erlenmeyer flask | follows the controller, collides with the shelf and the bench | manual |
| K7 | Grab a beaker | same | manual |
| K8 | Release objects on the workbench | they rest on the bench top | manual |
| K9 | Put an object back on a shelf | it rests on the shelf, the stored count goes back up | `LaboratoryCabinet_TracksDoorsAndStoredLabware`, `LabScene_CabinetHoldsItsEquipmentAndTheDoorsWork` |
| K10 | Release a test tube over a free hole of the rack | a ghost outline shows the position, the tube snaps upright into the hole, the rack count rises | manual; the start state `5 / 6` is asserted by `LabScene_CabinetHoldsItsEquipmentAndTheDoorsWork` |
| K11 | Wait 5 s after Play | nothing falls through a shelf or out of the cabinet | `LabScene_CabinetHoldsItsEquipmentAndTheDoorsWork` |

### Robot and control screen

| # | Check | Expected | Automated test |
|---|---|---|---|
| B1 | Look at the rear right corner | the robot stands on its charging dock | `LabScene_RobotControlScreenButtonsDriveTheRobot` |
| B2 | Right screen, top right | `NAVMESH OK` | same |
| B3 | GO TO DIGITAL SCALE | command line `GO TO DIGITAL SCALE > Driving to Digital Scale`, state `MOVING`, route line on the floor, the robot drives and stops in front of the scale, state `ARRIVED` | same, `RobotControlPanel_EveryCommandReachesTheRobot` |
| B4 | GO TO HEATING STATION | same for the heating station | same |
| B5 | GO TO STORAGE CABINET | same for the cabinet | same |
| B6 | GO TO LABORATORY WORKBENCH | same for the right end of the workbench | same |
| B7 | STOP ROBOT while it drives | it stops at once, state `STOPPED`; RESUME continues | same |
| B8 | RETURN TO STARTING POSITION | it drives back to the charging dock | same |
| B9 | Stand on a stop point and send the robot there | it parks next to you (`Arrived next to ...`) | `Robot_AimsForTheNearestReachablePointWhenTheStopIsOccupied` |
| B10 | Every screen | has a Graphic Raycaster and a Tracked Device Graphic Raycaster | `LabScene_ScreensAcceptBothMouseAndControllerRays` |
