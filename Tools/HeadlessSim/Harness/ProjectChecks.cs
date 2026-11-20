// HeadlessSim harness: project-specific checks for the combined heating station, the storage
// cabinet and the robot control screen.
//
// Program.cs calls ProjectChecks.Run(ctx) after the scene has been built in edit mode, reloaded
// from its saved state, play mode has been entered and 3 seconds have been simulated. The scene
// is still running. Every failed check makes the run fail.
//
// These checks complement the PlayMode tests (which also run in real Unity): they look at things
// a test inside Unity does not need to prove but that cannot be seen without the editor, such as
// "does every object fit on its shelf", "does a swinging door hit anything", "is every Inspector
// reference assigned" and "is every button wired to the method its label promises".
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HeadlessSim;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.UI;
using VRScienceLab.Module2;
using Object = UnityEngine.Object;

public static class ProjectChecks
{
    public static void Run(CheckContext ctx)
    {
        InspectorReferences(ctx);
        HeatingStation(ctx);
        CabinetGeometry(ctx);
        CabinetInteraction(ctx);
        Screens(ctx);
        RobotNavigation(ctx);
        RobotControlScreen(ctx);
        KeyboardShortcuts(ctx);
    }

    // ------------------------------------------------------------------ helpers

    private static Bounds BodyBounds(Component owner)
    {
        Collider[] colliders = owner.GetComponentsInChildren<Collider>().Where(c => !c.isTrigger).ToArray();
        Bounds bounds = colliders[0].bounds;
        for (int i = 1; i < colliders.Length; i++) bounds.Encapsulate(colliders[i].bounds);
        return bounds;
    }

    private static bool Overlap(Bounds a, Bounds b, float tolerance)
    {
        return a.min.x < b.max.x - tolerance && a.max.x > b.min.x + tolerance &&
               a.min.y < b.max.y - tolerance && a.max.y > b.min.y + tolerance &&
               a.min.z < b.max.z - tolerance && a.max.z > b.min.z + tolerance;
    }

    private static string Describe(Bounds b)
    {
        return $"x {b.min.x:0.000}..{b.max.x:0.000}, y {b.min.y:0.000}..{b.max.y:0.000}, z {b.min.z:0.000}..{b.max.z:0.000}";
    }

    private static InstrumentButton FindButton(Transform root, string name)
    {
        Transform found = root.Find(name);
        return found != null ? found.GetComponent<InstrumentButton>() : null;
    }

    /// <summary>Presses an instrument push button the way a controller does: an XR select on its interactable.</summary>
    private static bool PressWithRay(CheckContext ctx, Transform root, string buttonName)
    {
        InstrumentButton button = FindButton(root, buttonName);
        if (button == null)
        {
            ctx.Check(false, $"{root.name}/{buttonName} exists");
            return false;
        }

        ctx.Step(0.2f); // let the button's own repeat cooldown pass
        int before = button.PressCount;
        Simulation.Select(button.GetComponent<XRSimpleInteractable>());
        return button.PressCount == before + 1;
    }

    private static float Horizontal(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }

    // ------------------------------------------------------------------ Inspector references

    // Fields that are legitimately empty in this run (optional by design, or filled only when the
    // XR Interaction Toolkit samples and the character model are imported in the real editor).
    private static readonly HashSet<string> OptionalReferences = new HashSet<string>
    {
        "InstrumentButton.clickAudio", "Thermometer.manualSample", "Module2DebugInput.rayCamera",
        "XRSimulatorLoader.simulatorPrefab", "ScientistAvatar.animator", "ScientistAvatar.headBone",
        "ScientistAvatar.leftUpperArm", "ScientistAvatar.leftLowerArm", "ScientistAvatar.leftHand",
        "ScientistAvatar.rightUpperArm", "ScientistAvatar.rightLowerArm", "ScientistAvatar.rightHand",
        "ScientistAvatar.leftHandTarget", "ScientistAvatar.rightHandTarget", "ScientistAvatar.floorReference",
        "DigitalScale.statusIndicator", "NavMeshRuntimeBaker.surface", "CabinetDoor.hinge",
        "RotatableMount.pivot", "LabPhysicsObject.impactAudio", "LabSample.selectionMarker", "LabSample.nameLabel",
        "TransformAnimator.target", "StatusTextDisplay.target", "RobotStation.marker", "HeatingSurface.zone",
        // Inherited XR Socket Interactor fields: the toolkit creates default hover materials and finds
        // the interaction manager itself; the sixth rack hole deliberately starts empty.
        "LabwareSocket.interactableHoverMeshMaterial", "LabwareSocket.interactableCantHoverMeshMaterial",
        "LabwareSocket.m_InteractionManager", "LabwareSocket.m_StartingSelectedInteractable",
    };

    private static void InspectorReferences(CheckContext ctx)
    {
        ctx.Section("Inspector references of every Module 2 component in the scene");
        var missing = new SortedDictionary<string, int>();
        int components = 0, references = 0;
        foreach (MonoBehaviour behaviour in ctx.FindAll<MonoBehaviour>())
        {
            Type type = behaviour.GetType();
            if (type.Namespace != "VRScienceLab.Module2") continue;
            components++;

            for (Type t = type; t != null && t != typeof(MonoBehaviour); t = t.BaseType)
            {
                foreach (FieldInfo field in t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    bool serialized = field.IsPublic || field.GetCustomAttribute<SerializeField>() != null;
                    if (!serialized || field.IsStatic) continue;

                    if (typeof(Object).IsAssignableFrom(field.FieldType))
                    {
                        references++;
                        if ((Object)field.GetValue(behaviour) == null) Count(missing, type.Name + "." + field.Name);
                    }
                    else if (field.FieldType.IsArray && typeof(Object).IsAssignableFrom(field.FieldType.GetElementType()))
                    {
                        var array = (Array)field.GetValue(behaviour);
                        if (array == null) continue;
                        for (int i = 0; i < array.Length; i++)
                        {
                            references++;
                            if ((Object)array.GetValue(i) == null) Count(missing, type.Name + "." + field.Name + "[]");
                        }
                    }
                }
            }
        }

        ctx.Info($"{components} components, {references} object references inspected");
        var unexpected = new List<string>();
        foreach (KeyValuePair<string, int> pair in missing)
        {
            bool optional = OptionalReferences.Contains(pair.Key.Replace("[]", string.Empty));
            ctx.Info($"empty: {pair.Key} x{pair.Value}{(optional ? "  (optional)" : "  <-- NOT EXPECTED")}");
            if (!optional) unexpected.Add(pair.Key);
        }

        ctx.Check(unexpected.Count == 0, "No required Inspector reference is left empty" + (unexpected.Count > 0 ? ": " + string.Join(", ", unexpected) : string.Empty));
    }

    private static void Count(IDictionary<string, int> map, string key)
    {
        map.TryGetValue(key, out int value);
        map[key] = value + 1;
    }

    // ------------------------------------------------------------------ Combined heating station

    private static void HeatingStation(CheckContext ctx)
    {
        ctx.Section("Combined heating station");
        var station = ctx.Find<CombinedHeatingStation>();
        if (!ctx.Check(station != null, "CombinedHeatingStation exists")) return;

        HotPlate plate = station.HotPlate;
        StopwatchTimer timer = station.Stopwatch;
        ctx.Check(plate != null && timer != null, "The station references its HotPlate and its StopwatchTimer");
        ctx.Check(ctx.FindAll<HotPlate>().Count == 1 && ctx.FindAll<StopwatchTimer>().Count == 1, "Exactly one hot plate and one stopwatch in the scene (no separate equipment left)");
        ctx.Check(plate.transform.parent == station.transform && timer.transform.parent == station.transform, "Both instruments are modules of the same station object");
        ctx.Check(GameObject.Find("HotPlate") == plate.gameObject && GameObject.Find("Stopwatch") == timer.gameObject, "GameObject.Find still locates 'HotPlate' and 'Stopwatch' (LabDirectory contract)");

        // One housing: every part of both modules stands on the shared body, on the bench, clear of its neighbours.
        Collider body = station.transform.Find("Body").GetComponent<Collider>();
        Bounds stationBounds = BodyBounds(station);
        Bounds bench = GameObject.Find("Workbench").GetComponentInChildren<Collider>().bounds;
        ctx.Info("station " + Describe(stationBounds));
        ctx.Check(body != null && stationBounds.size.x < 0.7f && stationBounds.size.z < 0.5f, "One compact housing (under 0.7 m x 0.5 m)");
        ctx.Check(stationBounds.min.x > bench.min.x && stationBounds.max.x < bench.max.x && stationBounds.min.z > bench.min.z && stationBounds.max.z < bench.max.z, "The station stands entirely on the workbench");
        ctx.Check(!Overlap(stationBounds, BodyBounds(ctx.Find<DigitalScale>()), 0f) && !Overlap(stationBounds, BodyBounds(ctx.Find<Thermometer>()), 0f), "The station does not touch the digital scale or the thermometer");
        foreach (string module in new[] { "HotPlate", "Stopwatch" })
        {
            Bounds moduleBounds = BodyBounds(station.transform.Find(module));
            ctx.Check(moduleBounds.min.x >= body.bounds.min.x - 0.001f && moduleBounds.max.x <= body.bounds.max.x + 0.001f &&
                      moduleBounds.min.z >= body.bounds.min.z - 0.05f && moduleBounds.max.z <= body.bounds.max.z + 0.001f,
                $"The {module} module sits within the shared housing");
        }

        // Displays and wiring.
        ctx.Check(plate.HasValueDisplay && timer.HasValueDisplay, "Both instruments have their own display");
        foreach (InstrumentButton button in station.GetComponentsInChildren<InstrumentButton>())
        {
            IReadOnlyList<UnityEngine.Events.PersistentCall> calls = button.onPressed.SimPersistentCalls;
            bool wired = calls.Count == 1 && calls[0].target != null && !string.IsNullOrEmpty(calls[0].methodName);
            ctx.Check(wired, $"{button.transform.parent.name}/{button.name} -> " + (wired ? calls[0].target.GetType().Name + "." + calls[0].methodName : "NOT WIRED"));
        }

        // Operate it with the controller path (XR select on each push button).
        Transform heater = plate.transform, clock = timer.transform;
        ctx.Check(plate.State == HotPlateState.Off && timer.State == StopwatchState.Idle, "Starts with the heater off and the timer idle");

        ctx.Check(PressWithRay(ctx, heater, "OnButton") && plate.State == HotPlateState.Heating, "ON (controller select) -> HEATING");
        ctx.Check(timer.State == StopwatchState.Idle, "Switching the heater on does not start the timer");
        float t0 = plate.CurrentTemperature;
        ctx.Step(2f);
        ctx.Check(Mathf.Abs(plate.CurrentTemperature - t0 - 2f * plate.HeatingRate) < 1.5f, $"Temperature rises at the configured rate ({t0:0.0} -> {plate.CurrentTemperature:0.0} °C in 2 s, rate {plate.HeatingRate} °C/s)");
        ctx.Check(plate.ValueDisplayText != null && plate.ValueDisplayText.EndsWith("°C") && plate.StateDisplayText == "HEATING", $"Temperature display shows '{plate.ValueDisplayText}' / '{plate.StateDisplayText}'");
        ctx.Check(station.IsHeating && station.IsActive, "The station reports that it is heating");

        ctx.Check(PressWithRay(ctx, heater, "OffButton") && plate.State == HotPlateState.Off, "OFF -> heater off");
        float atOff = plate.CurrentTemperature;
        ctx.Step(1f);
        ctx.Check(plate.CurrentTemperature <= atOff + 0.001f, $"Temperature stops rising when switched off ({atOff:0.0} -> {plate.CurrentTemperature:0.0} °C)");
        ctx.Check(PressWithRay(ctx, heater, "ResetButton") && Mathf.Abs(plate.CurrentTemperature - plate.InitialTemperature) < 0.01f, "RESET -> initial temperature");

        ctx.Check(PressWithRay(ctx, clock, "StartButton") && timer.State == StopwatchState.Running, "START -> RUNNING");
        ctx.Check(plate.State == HotPlateState.Off, "Starting the timer does not switch the heater on");
        ctx.Step(1.5f);
        ctx.Check(Math.Abs(timer.ElapsedSeconds - 1.5) < 0.05, $"Timer counts elapsed time ({timer.ElapsedSeconds:0.00} s, display '{timer.ValueDisplayText}')");
        ctx.Check(PressWithRay(ctx, clock, "PauseButton") && timer.State == StopwatchState.Paused, "PAUSE -> PAUSED");
        double paused = timer.ElapsedSeconds;
        ctx.Step(1f);
        ctx.Check(Math.Abs(timer.ElapsedSeconds - paused) < 0.0001, "Paused time is preserved");
        ctx.Check(PressWithRay(ctx, clock, "StartButton") && timer.State == StopwatchState.Running, "START again -> resumes");
        ctx.Step(0.5f);
        ctx.Check(timer.ElapsedSeconds > paused + 0.4, $"Resume continues from the paused time ({paused:0.00} -> {timer.ElapsedSeconds:0.00} s)");
        ctx.Check(PressWithRay(ctx, clock, "ResetButton") && timer.State == StopwatchState.Idle && timer.ElapsedSeconds == 0.0 && timer.ValueDisplayText == "00:00.00", "RESET -> 00:00.00, IDLE");

        var mount = timer.GetComponent<RotatableMount>();
        ctx.Check(mount != null && PressWithRay(ctx, clock, "TurnLeftButton") && Mathf.Abs(mount.TargetAngle) > 1f, "The swivel buttons turn the timer display");
        mount.ResetRotation();
        ctx.Step(1f);
    }

    // ------------------------------------------------------------------ Cabinet geometry

    private static void CabinetGeometry(CheckContext ctx)
    {
        ctx.Section("Storage cabinet: geometry and contents");
        var cabinet = ctx.Find<LaboratoryCabinet>();
        if (!ctx.Check(cabinet != null, "LaboratoryCabinet exists")) return;

        Bounds whole = BodyBounds(cabinet.transform.Find("Carcass"));
        ctx.Info("carcass " + Describe(whole));
        ctx.Check(Mathf.Abs(whole.size.x - 0.55f) < 0.03f && Mathf.Abs(whole.size.z - 0.9f) < 0.03f && Mathf.Abs(whole.size.y - 2.25f) < 0.03f, "Realistic dimensions: 0.90 m wide, 0.55 m deep, 2.25 m high");
        ctx.Check(whole.min.x >= -4.51f && whole.max.x < -3.9f, "The cabinet stands against the left wall, in line with the storage units");
        ctx.Check(cabinet.DoorCount == 2 && cabinet.GetDoor(0) != null && cabinet.GetDoor(1) != null, "Two doors");
        ctx.Check(cabinet.TotalCount == 23, $"23 objects belong in the cabinet (found {cabinet.TotalCount})");

        var kinds = new Dictionary<LabwareKind, int>();
        for (int i = 0; i < cabinet.TotalCount; i++)
        {
            Labware item = cabinet.GetContent(i);
            kinds.TryGetValue(item.Kind, out int n);
            kinds[item.Kind] = n + 1;
        }

        ctx.Info(string.Join(", ", kinds.Select(k => k.Key + " x" + k.Value)));
        foreach (LabwareKind required in new[] { LabwareKind.TestTube, LabwareKind.ErlenmeyerFlask, LabwareKind.TestTubeRack, LabwareKind.Beaker, LabwareKind.LaboratoryTape })
        {
            ctx.Check(kinds.ContainsKey(required), required + " present");
        }

        // Shelf surfaces: the counter (top of the carcass's own collider) and the two shelf colliders.
        var shelves = new List<Bounds>();
        Collider counter = cabinet.transform.Find("Carcass").GetComponent<Collider>();
        shelves.Add(counter.bounds);
        foreach (Collider collider in cabinet.GetComponentsInChildren<Collider>())
        {
            if (collider.name.StartsWith("Shelf") && collider.name.EndsWith("Collider")) shelves.Add(collider.bounds);
        }

        ctx.Check(shelves.Count == 3, "Three storage levels with their own colliders");

        var rack = ctx.Find<TestTubeRack>();
        var bodies = new List<KeyValuePair<Labware, Bounds>>();
        int resting = 0, moving = 0;
        for (int i = 0; i < cabinet.TotalCount; i++)
        {
            Labware item = cabinet.GetContent(i);
            Bounds bounds = BodyBounds(item);
            bodies.Add(new KeyValuePair<Labware, Bounds>(item, bounds));
            if (item.Body.linearVelocity.magnitude > 0.01f) moving++;

            if (item.Kind == LabwareKind.TestTube) continue; // held by the rack, checked below

            bool supported = false;
            foreach (Bounds shelf in shelves)
            {
                bool onTop = Mathf.Abs(bounds.min.y - shelf.max.y) < 0.004f;
                bool inside = bounds.min.x >= shelf.min.x - 0.001f && bounds.max.x <= shelf.max.x + 0.001f && bounds.min.z >= shelf.min.z - 0.001f && bounds.max.z <= shelf.max.z + 0.001f;
                if (onTop && inside) supported = true;
            }

            if (supported) resting++;
            else ctx.Check(false, $"{item.name} rests fully on a shelf ({Describe(bounds)})");
        }

        ctx.Check(resting == cabinet.TotalCount - kinds[LabwareKind.TestTube], $"Every free-standing object rests fully on a shelf surface ({resting} objects)");
        ctx.Check(moving == 0, "Nothing is still moving three seconds after the start");

        int overlaps = 0;
        for (int a = 0; a < bodies.Count; a++)
        {
            for (int b = a + 1; b < bodies.Count; b++)
            {
                bool rackPair = (bodies[a].Key.Kind == LabwareKind.TestTubeRack && bodies[b].Key.Kind == LabwareKind.TestTube) ||
                                (bodies[b].Key.Kind == LabwareKind.TestTubeRack && bodies[a].Key.Kind == LabwareKind.TestTube);
                if (rackPair) continue;
                if (Overlap(bodies[a].Value, bodies[b].Value, 0.001f))
                {
                    overlaps++;
                    ctx.Info($"overlap: {bodies[a].Key.name} / {bodies[b].Key.name}");
                }
            }
        }

        ctx.Check(overlaps == 0, "No two stored objects overlap");

        // Test tubes: upright in a hole, clear of the rack's solid colliders and of each other.
        if (ctx.Check(rack != null && rack.Capacity == 6, "Test tube rack with six holes"))
        {
            ctx.Check(rack.OccupiedCount == 5 && rack.FirstFreeSlot() == 5, $"Five tubes stand in the rack, the sixth hole is free ({rack.OccupiedCount} / {rack.Capacity})");
            Collider[] solid = rack.GetComponents<Collider>().Where(c => !c.isTrigger).ToArray();
            int clean = 0;
            for (int i = 0; i < rack.Capacity; i++)
            {
                LabwareSocket slot = rack.GetSlot(i);
                Labware tube = slot.HeldLabware;
                if (tube == null) continue;
                Bounds tubeBounds = BodyBounds(tube);
                bool hitsRack = solid.Any(c => Overlap(tubeBounds, c.bounds, 0.0005f));
                bool upright = Vector3.Angle(tube.transform.up, Vector3.up) < 1f;
                bool atAttach = Vector3.Distance(tube.transform.position, slot.attachTransform.position) < 0.002f;
                if (!hitsRack && upright && atAttach) clean++;
                else ctx.Info($"tube {tube.name}: hitsRack={hitsRack} upright={upright} atAttach={atAttach}");
            }

            ctx.Check(clean == 5, "Every tube stands upright at the bottom of its hole without touching the rack's colliders");
        }

        ctx.Check(cabinet.StoredCount == cabinet.TotalCount, $"The interior zone counts every object as stored ({cabinet.StatusLine})");

        // Closed doors: clear of the contents and of the carcass.
        foreach (CabinetDoor door in cabinet.GetComponentsInChildren<CabinetDoor>())
        {
            Bounds doorBounds = BodyBounds(door);
            bool hitsContents = bodies.Any(b => Overlap(doorBounds, b.Value, 0.001f));
            bool hitsCarcass = cabinet.transform.Find("Carcass").GetComponentsInChildren<Collider>().Any(c => !c.isTrigger && Overlap(doorBounds, c.bounds, 0.001f));
            ctx.Check(!hitsContents && !hitsCarcass, $"{door.DoorName} (closed) touches neither the contents nor the carcass");
        }
    }

    // ------------------------------------------------------------------ Cabinet interaction

    private static void CabinetInteraction(CheckContext ctx)
    {
        ctx.Section("Storage cabinet: doors, taking objects out, putting them back");
        var cabinet = ctx.Find<LaboratoryCabinet>();
        if (cabinet == null) return;

        // Things a swinging door must not hit.
        var obstacles = new List<KeyValuePair<string, Bounds>>();
        foreach (GameObject unit in ctx.FindObjects("Storage Unit 1").Concat(ctx.FindObjects("Storage Unit 3")))
        {
            obstacles.Add(new KeyValuePair<string, Bounds>(unit.name, unit.GetComponent<Collider>().bounds));
        }

        var robot = ctx.Find<LabAssistantRobot>();
        RobotStation cabinetStation = robot.GetStation(robot.FindStationIndex("Storage Cabinet"));
        obstacles.Add(new KeyValuePair<string, Bounds>("robot parked at the Storage Cabinet station", new Bounds(cabinetStation.StopPosition + new Vector3(0f, 0.52f, 0f), new Vector3(0.52f, 1.04f, 0.52f))));
        for (int i = 0; i < cabinet.TotalCount; i++)
        {
            obstacles.Add(new KeyValuePair<string, Bounds>(cabinet.GetContent(i).name, BodyBounds(cabinet.GetContent(i))));
        }

        ctx.Check(obstacles.Count == 26, "Neighbouring units, robot stop and contents collected for the door sweep");

        for (int d = 0; d < cabinet.DoorCount; d++)
        {
            CabinetDoor door = cabinet.GetDoor(d);
            Bounds closed = BodyBounds(door);
            ctx.Step(0.3f);
            Simulation.Select(door.GetComponent<XRSimpleInteractable>());
            ctx.Check(door.State == CabinetDoorState.Opening, $"{door.DoorName}: controller select starts opening it");

            var hits = new HashSet<string>();
            int frames = 0;
            while (door.IsMoving && frames < 300)
            {
                ctx.StepFrames(1);
                frames++;
                Bounds swept = BodyBounds(door);
                foreach (KeyValuePair<string, Bounds> obstacle in obstacles)
                {
                    if (Overlap(swept, obstacle.Value, 0.004f)) hits.Add(obstacle.Key);
                }
            }

            Bounds open = BodyBounds(door);
            ctx.Check(door.State == CabinetDoorState.Open && Mathf.Abs(Mathf.Abs(door.CurrentAngle) - 105f) < 0.1f, $"{door.DoorName}: open after {frames / 60f:0.00} s at {door.CurrentAngle:0} degrees");
            ctx.Check(open.max.x > closed.max.x + 0.3f, $"{door.DoorName}: swings out into the room ({closed.max.x:0.00} -> {open.max.x:0.00} m)");
            ctx.Check(hits.Count == 0, $"{door.DoorName}: its swing is clear of the neighbouring units, the robot stop and the contents" + (hits.Count > 0 ? " -- hits " + string.Join(", ", hits) : string.Empty));
        }

        ctx.Check(cabinet.IsFullyOpen && cabinet.StatusLine.StartsWith("DOORS OPEN"), $"Cabinet reports '{cabinet.StatusLine}'");
        ctx.Check(cabinet.StoredCount == cabinet.TotalCount, "Opening the doors does not disturb the contents");

        // ---- Take a test tube out of the rack with an (emulated) hand, carry it to the bench, bring it back.
        var rack = ctx.Find<TestTubeRack>();
        LabwareSocket slot = rack.GetSlot(0);
        Labware tube = slot.HeldLabware;
        var tubeGrab = tube.GetComponent<XRGrabInteractable>();
        Vector3 rackPose = tube.transform.position;

        ctx.Check(Simulation.Grab(tubeGrab), "A test tube can be grabbed");
        ctx.Check(!slot.IsOccupied && rack.OccupiedCount == 4, $"Grabbing it frees its hole (rack shows {rack.OccupiedCount} / {rack.Capacity})");
        Vector3 benchSpot = new Vector3(-1.2f, 1.05f, 0.45f);
        tube.transform.position = benchSpot;
        tube.Body.position = benchSpot;
        ctx.Step(0.3f);
        ctx.Check(cabinet.StoredCount == cabinet.TotalCount - 1, $"The cabinet notices the missing tube ({cabinet.StatusLine})");
        Simulation.Release(tubeGrab);
        ctx.Step(1.5f);
        ctx.Check(tube.transform.position.y > 0.89f && tube.transform.position.y < 0.95f, $"Released above the workbench, the tube comes to rest on the bench top (y = {tube.transform.position.y:0.000})");

        // Back over the free sixth hole: the socket takes it.
        LabwareSocket freeSlot = rack.GetSlot(5);
        Simulation.Grab(tubeGrab);
        Vector3 overHole = freeSlot.transform.position;
        tube.transform.SetPositionAndRotation(overHole, rack.transform.rotation);
        tube.Body.position = overHole;
        ctx.Step(0.2f);
        Simulation.Release(tubeGrab);
        ctx.Step(1.2f);
        ctx.Check(freeSlot.IsOccupied && freeSlot.HeldLabware == tube, "Released over a free hole, the tube is taken by that hole's socket");
        ctx.Check(rack.OccupiedCount == 5 && Vector3.Distance(tube.transform.position, freeSlot.attachTransform.position) < 0.002f, $"The tube stands in the new hole (rack shows {rack.OccupiedCount} / {rack.Capacity})");
        ctx.Check(cabinet.StoredCount == cabinet.TotalCount, $"The cabinet counts it as stored again ({cabinet.StatusLine})");

        // A beaker is not accepted by a rack hole.
        Labware beaker = null, flask = null;
        for (int i = 0; i < cabinet.TotalCount; i++)
        {
            Labware item = cabinet.GetContent(i);
            if (beaker == null && item.Kind == LabwareKind.Beaker && item.GetComponent<TemperatureSample>() != null) beaker = item;
            if (flask == null && item.Kind == LabwareKind.ErlenmeyerFlask && item.GetComponent<TemperatureSample>() != null) flask = item;
        }

        ctx.Check(!rack.GetSlot(0).Accepts(beaker) && rack.GetSlot(0).Accepts(tube), "Rack holes accept test tubes only");

        // ---- Beaker: cabinet -> scale -> thermometer -> heating surface -> back on its shelf.
        Vector3 shelfPosition = beaker.transform.position;
        Quaternion shelfRotation = beaker.transform.rotation;
        var beakerGrab = beaker.GetComponent<XRGrabInteractable>();
        var sample = beaker.GetComponent<TemperatureSample>();

        var scale = ctx.Find<DigitalScale>();
        scale.PowerOn();
        ctx.Check(Simulation.Grab(beakerGrab), "A beaker can be grabbed");
        Place(beaker, scale.transform.Find("SampleAnchor").position + Vector3.up * 0.03f);
        Simulation.Release(beakerGrab);
        ctx.Step(1.2f);
        ctx.Check(Mathf.Abs(scale.DisplayedMassGrams - beaker.MassGrams) < 0.5f, $"On the digital scale: {scale.ValueDisplayText} (Rigidbody mass {beaker.MassGrams:0} g)");

        var thermometer = ctx.Find<Thermometer>();
        thermometer.StartMeasuring();
        Simulation.Grab(beakerGrab);
        Place(beaker, thermometer.transform.Find("SampleAnchor").position + Vector3.up * 0.03f);
        Simulation.Release(beakerGrab);
        ctx.Step(1.2f);
        ctx.Check(Mathf.Abs(scale.DisplayedMassGrams) < 0.5f, "Taken off the scale, it reads zero again");
        ctx.Check(thermometer.HasSample && Mathf.Abs(thermometer.CurrentReading - sample.TemperatureCelsius) < 0.2f, $"On the thermometer dock: {thermometer.ValueDisplayText} ({sample.SampleName})");

        var station = ctx.Find<CombinedHeatingStation>();
        var surface = station.HotPlate.GetComponent<HeatingSurface>();
        float before = sample.TemperatureCelsius;
        Simulation.Grab(beakerGrab);
        Place(beaker, station.HotPlate.transform.Find("SampleAnchor").position + Vector3.up * 0.03f);
        Simulation.Release(beakerGrab);
        ctx.Step(1.2f);
        Bounds onPlate = BodyBounds(beaker);
        Bounds ceramic = station.HotPlate.transform.Find("CeramicTop").GetComponent<Collider>().bounds;
        ctx.Check(Mathf.Abs(onPlate.min.y - ceramic.max.y) < 0.004f && surface.HasContainer, $"On the heating surface: it rests on the ceramic top and is detected ({surface.ContainerCount} container)");
        station.HotPlate.SwitchOn();
        ctx.Step(5f);
        ctx.Check(sample.TemperatureCelsius > before + 5f, $"The heated sample warms up ({before:0.0} -> {sample.TemperatureCelsius:0.0} °C in 5 s)");
        station.HotPlate.SwitchOff();
        station.HotPlate.ResetTemperature();

        Simulation.Grab(beakerGrab);
        beaker.transform.rotation = shelfRotation;
        Place(beaker, shelfPosition + Vector3.up * 0.03f);
        Simulation.Release(beakerGrab);
        ctx.Step(1.2f);
        ctx.Check(Vector3.Distance(beaker.transform.position, shelfPosition) < 0.01f, "Put back, the beaker rests on its shelf again");
        ctx.Check(cabinet.StoredCount == cabinet.TotalCount, $"Everything is back in the cabinet ({cabinet.StatusLine})");

        // An Erlenmeyer flask on the bench.
        var flaskGrab = flask.GetComponent<XRGrabInteractable>();
        Vector3 flaskShelf = flask.transform.position;
        ctx.Check(Simulation.Grab(flaskGrab), "An Erlenmeyer flask can be grabbed");
        Place(flask, new Vector3(-1.3f, 1.0f, 0.75f));
        Simulation.Release(flaskGrab);
        ctx.Step(1.2f);
        ctx.Check(Mathf.Abs(BodyBounds(flask).min.y - 0.9f) < 0.004f, "Released above the workbench, the flask rests on the bench top");
        Simulation.Grab(flaskGrab);
        Place(flask, flaskShelf + Vector3.up * 0.03f);
        Simulation.Release(flaskGrab);
        ctx.Step(1.2f);

        // Close both doors with the cabinet command; reset closes them too.
        cabinet.CloseDoors();
        ctx.Step(1.5f);
        ctx.Check(!cabinet.IsOpen && cabinet.StatusLine.StartsWith("DOORS CLOSED"), $"Doors close again ('{cabinet.StatusLine}')");
        ctx.Check(cabinet.StoredCount == cabinet.TotalCount, "Closing the doors pushes nothing out");

        scale.PowerOff();
        thermometer.StopMeasuring();
    }

    private static void Place(Labware item, Vector3 position)
    {
        item.transform.position = position;
        item.Body.position = position;
        item.Body.linearVelocity = Vector3.zero;
        Physics.SyncTransforms();
    }

    // ------------------------------------------------------------------ Screens

    private static void Screens(CheckContext ctx)
    {
        ctx.Section("Screens: what a click needs to arrive");
        ctx.Check(ctx.FindAll<EventSystem>().Count == 1 && ctx.Find<XRUIInputModule>() != null, "One EventSystem with an XR UI Input Module");

        Transform ui = GameObject.Find("Interface").transform;
        int screens = 0;
        foreach (Canvas canvas in ui.GetComponentsInChildren<Canvas>(true))
        {
            screens++;
            string name = canvas.transform.parent.name;
            ctx.Check(canvas.renderMode == RenderMode.WorldSpace, $"{name}: world-space canvas");
            ctx.Check(canvas.GetComponent<GraphicRaycaster>() != null, $"{name}: Graphic Raycaster (mouse, simulator point-and-click)");
            ctx.Check(canvas.GetComponent<TrackedDeviceGraphicRaycaster>() != null, $"{name}: Tracked Device Graphic Raycaster (controller rays)");
            ctx.Check(canvas.worldCamera != null, $"{name}: event camera assigned");
        }

        ctx.Check(screens == 3, "Three screens (main display, sample console, robot console)");

        // Every button of every screen: wired, clickable, inside its screen, not covered by another button.
        int buttons = 0, unwired = 0, outside = 0, covered = 0;
        foreach (Canvas canvas in ui.GetComponentsInChildren<Canvas>(true))
        {
            var rects = new List<KeyValuePair<string, Rect>>();
            Vector2 size = ((RectTransform)canvas.transform).sizeDelta;
            foreach (Button button in canvas.GetComponentsInChildren<Button>(true))
            {
                buttons++;
                if (button.onClick.GetPersistentEventCount() == 0 || button.onClick.GetPersistentTarget(0) == null) { unwired++; ctx.Info("not wired: " + button.name); }
                if (button.targetGraphic == null || !button.targetGraphic.raycastTarget) { unwired++; ctx.Info("no raycast target: " + button.name); }

                var rt = (RectTransform)button.transform;
                var rect = new Rect(rt.anchoredPosition.x, -rt.anchoredPosition.y, rt.sizeDelta.x, rt.sizeDelta.y);
                if (rect.xMin < 0f || rect.yMin < 0f || rect.xMax > size.x || rect.yMax > size.y) { outside++; ctx.Info($"outside its screen: {button.name} {rect}"); }
                if (button.gameObject.activeInHierarchy)
                {
                    foreach (KeyValuePair<string, Rect> other in rects)
                    {
                        if (rect.Overlaps(other.Value)) { covered++; ctx.Info($"overlap: {button.name} / {other.Key}"); }
                    }

                    rects.Add(new KeyValuePair<string, Rect>(button.name, rect));
                }
            }
        }

        ctx.Check(buttons >= 30 && unwired == 0, $"All {buttons} screen buttons have a target graphic and a persistent onClick listener");
        ctx.Check(outside == 0 && covered == 0, "Every button lies inside its screen and no button covers another");
    }

    // ------------------------------------------------------------------ Robot navigation

    private static void RobotNavigation(CheckContext ctx)
    {
        ctx.Section("Robot: model, agent, NavMesh, stations");
        var robot = ctx.Find<LabAssistantRobot>();
        if (!ctx.Check(robot != null && robot.gameObject.activeInHierarchy, "The robot is in the scene and active")) return;

        ctx.Check(robot.GetComponentsInChildren<MeshRenderer>().Length >= 20, $"3D model: {robot.GetComponentsInChildren<MeshRenderer>().Length} mesh parts");
        NavMeshAgent agent = robot.Agent;
        ctx.Check(agent != null && agent.enabled, "NavMeshAgent present and enabled");
        var surface = ctx.Find<NavMeshSurface>();
        ctx.Check(surface != null && surface.navMeshData != null, "NavMeshSurface present with baked data");
        ctx.Check(surface.collectObjects == CollectObjects.Children && surface.useGeometry == NavMeshCollectGeometry.PhysicsColliders, "Surface configuration: children, physics colliders");
        var baker = ctx.Find<NavMeshRuntimeBaker>();
        ctx.Check(baker != null && !baker.BakedAtRuntime, "Runtime baker present and not needed (the baked data loaded)");
        ctx.Check(robot.IsOnNavMesh, "The robot stands on the NavMesh");
        ctx.Check(robot.StationCount == 7, $"Seven stations ({robot.StationCount})");

        string[] expected = { "Starting Position", "Digital Scale", "Heating Station", "Storage Cabinet", "Laboratory Workbench", "Analysis Desk", "Fume Hood" };
        for (int i = 0; i < robot.StationCount && i < expected.Length; i++)
        {
            RobotStation station = robot.GetStation(i);
            bool named = station != null && station.StationName == expected[i];
            bool onMesh = NavMesh.SamplePosition(station.StopPosition, out NavMeshHit hit, 1.5f, NavMesh.AllAreas);
            float offset = onMesh ? Horizontal(hit.position, station.StopPosition) : float.PositiveInfinity;
            var path = new NavMeshPath();
            bool reachable = onMesh && NavMesh.CalculatePath(robot.transform.position, hit.position, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete;
            ctx.Check(named && onMesh && offset < 0.08f && reachable, $"Station {i} '{expected[i]}': stop point on the NavMesh (offset {offset:0.000} m), complete path from the robot");
        }

        Vector3 home = robot.GetStation(0).StopPosition;
        ctx.Check(Horizontal(robot.transform.position, home) < 0.05f, "The robot starts at its starting position");
    }

    // ------------------------------------------------------------------ Robot control screen

    private static readonly string[] ScreenButtons =
    {
        "GO TO DIGITAL SCALE", "GO TO HEATING STATION", "GO TO STORAGE CABINET", "GO TO LABORATORY WORKBENCH",
        "STOP ROBOT", "RETURN TO STARTING POSITION", "RESUME", "GO TO ANALYSIS DESK", "GO TO FUME HOOD", "AUTO TOUR",
    };

    private static readonly string[] ScreenMethods =
    {
        "GoToStation(1)", "GoToStation(2)", "GoToStation(3)", "GoToStation(4)",
        "StopRobot()", "ReturnToStart()", "ResumeRobot()", "GoToStation(5)", "GoToStation(6)", "ToggleTour()",
    };

    private static Button ScreenButton(RobotControlPanel panel, string name)
    {
        return panel.GetComponentsInChildren<Button>(true).FirstOrDefault(b => b.name == name);
    }

    private static void RobotControlScreen(CheckContext ctx)
    {
        ctx.Section("Robot control screen: every button, clicked through the EventSystem");
        var robot = ctx.Find<LabAssistantRobot>();
        var panel = ctx.Find<RobotControlPanel>();
        if (!ctx.Check(panel != null, "RobotControlPanel exists on the robot console")) return;

        // Wiring: label -> RobotControlPanel method (+ argument).
        for (int i = 0; i < ScreenButtons.Length; i++)
        {
            Button button = ScreenButton(panel, ScreenButtons[i]);
            if (!ctx.Check(button != null, $"Button '{ScreenButtons[i]}' exists")) continue;
            IReadOnlyList<UnityEngine.Events.PersistentCall> calls = button.onClick.SimPersistentCalls;
            string wired = calls.Count == 1 && calls[0].target is RobotControlPanel ? calls[0].methodName + "(" + (calls[0].argument != null ? calls[0].argument.ToString() : string.Empty) + ")" : "NOT WIRED";
            ctx.Check(wired == ScreenMethods[i], $"'{ScreenButtons[i]}' -> RobotControlPanel.{wired}");
        }

        TMPro.TMP_Text state = Text(panel, "stateText"), destination = Text(panel, "destinationText"), command = Text(panel, "commandText"), navigation = Text(panel, "navigationText");
        ctx.Check(navigation != null && navigation.text == "NAVMESH OK", $"Screen shows '{navigation.text}'");

        var indicator = ctx.Find<RobotPathIndicator>();
        Vector3 start = robot.transform.position;

        // The four destination buttons, one after the other.
        string[] destinations = { "Digital Scale", "Heating Station", "Storage Cabinet", "Laboratory Workbench" };
        for (int i = 0; i < destinations.Length; i++)
        {
            string buttonName = "GO TO " + destinations[i].ToUpperInvariant();
            int commands = panel.CommandCount;
            Vector3 from = robot.transform.position;
            ctx.Check(ctx.Click(ScreenButton(panel, buttonName)) && panel.CommandCount == commands + 1 && panel.LastCommand == buttonName, $"[{buttonName}] 1. the UI detects the click");
            ctx.Check(robot.DestinationName == destinations[i], $"[{buttonName}] 2-3. destination identified and received by the robot: '{robot.DestinationName}'");
            ctx.Check(robot.State == RobotState.Moving && robot.LastCommandAccepted, $"[{buttonName}] 4. the NavMeshAgent starts ('{robot.LastMessage}')");
            ctx.Check(state.text == "MOVING" && destination.text == destinations[i] && command.text.StartsWith(buttonName), $"[{buttonName}] screen: {state.text} / {destination.text} / '{command.text}'");

            ctx.Step(1f);
            ctx.Check(Horizontal(robot.transform.position, from) > 0.3f && robot.Speed > 0.1f, $"[{buttonName}] 5. the robot follows its path ({Horizontal(robot.transform.position, from):0.00} m after 1 s, {indicator.PointCount} route points drawn)");
            ctx.Check(indicator.PointCount >= 2, $"[{buttonName}] the route line is visible while it drives");

            bool arrived = ctx.StepUntil(() => robot.State != RobotState.Moving, 60f);
            RobotStation station = robot.GetStation(robot.DestinationIndex);
            float miss = Horizontal(robot.transform.position, station.StopPosition);
            ctx.Check(arrived && robot.State == RobotState.Arrived && miss < 0.15f, $"[{buttonName}] 6. the robot stops at the destination (state {robot.State}, {miss:0.00} m from the stop point)");
            ctx.Step(1.5f);
            ctx.Check(robot.Speed < 0.01f && indicator.PointCount == 0 && state.text == "ARRIVED", $"[{buttonName}] it stays there (speed {robot.Speed:0.00}, screen {state.text})");
            ctx.Check(Quaternion.Angle(robot.transform.rotation, station.StopRotation) < 2f, $"[{buttonName}] it turned to face the station");
        }

        // Stop Robot on the way back, then Resume, then a new destination while moving.
        ctx.Click(ScreenButton(panel, "RETURN TO STARTING POSITION"));
        ctx.Check(robot.State == RobotState.Moving && robot.DestinationIndex == LabAssistantRobot.StartStationIndex, "[RETURN TO STARTING POSITION] the robot heads for station 0");
        ctx.Step(1.5f);
        ctx.Click(ScreenButton(panel, "STOP ROBOT"));
        ctx.Check(robot.State == RobotState.Paused && state.text == "STOPPED", $"[STOP ROBOT] state {robot.State}, screen {state.text}");
        ctx.Step(0.3f);
        Vector3 stopped = robot.transform.position;
        ctx.Step(2f);
        ctx.Check(Horizontal(stopped, robot.transform.position) < 0.01f && robot.Speed < 0.01f, "[STOP ROBOT] the robot does not move while stopped");
        ctx.Check(Horizontal(robot.transform.position, start) > 1f, "[STOP ROBOT] it stopped on the way, not at the destination");

        ctx.Click(ScreenButton(panel, "RESUME"));
        ctx.Check(robot.State == RobotState.Moving, "[RESUME] the robot continues");
        ctx.Step(1f);
        ctx.Check(Horizontal(stopped, robot.transform.position) > 0.3f, "[RESUME] it is moving again");

        ctx.Click(ScreenButton(panel, "GO TO FUME HOOD"));
        ctx.Check(robot.State == RobotState.Moving && robot.DestinationName == "Fume Hood", "[GO TO FUME HOOD] a new destination is accepted while it moves");
        ctx.Check(ctx.StepUntil(() => robot.State != RobotState.Moving, 60f) && robot.State == RobotState.Arrived, "[GO TO FUME HOOD] arrived");

        ctx.Click(ScreenButton(panel, "GO TO ANALYSIS DESK"));
        ctx.Check(ctx.StepUntil(() => robot.State != RobotState.Moving, 60f) && robot.State == RobotState.Arrived && Horizontal(robot.transform.position, robot.GetStation(5).StopPosition) < 0.15f, "[GO TO ANALYSIS DESK] arrived at the stop point (the cart no longer blocks it)");

        ctx.Click(ScreenButton(panel, "STOP ROBOT"));
        ctx.Check(robot.State == RobotState.Arrived && command.text.Contains("already stopped"), $"[STOP ROBOT] when parked it answers '{robot.LastMessage}'");

        ctx.Click(ScreenButton(panel, "RETURN TO STARTING POSITION"));
        ctx.Check(ctx.StepUntil(() => robot.State != RobotState.Moving, 60f) && robot.State == RobotState.Arrived && Horizontal(robot.transform.position, start) < 0.15f, "[RETURN TO STARTING POSITION] the robot is back at its initial location");

        // Automatic tour: starts, visits a station, and Stop cancels it.
        ctx.Click(ScreenButton(panel, "AUTO TOUR"));
        ctx.Check(robot.IsTouring && robot.State == RobotState.Moving && state.text.Contains("TOUR"), "[AUTO TOUR] the tour starts");
        ctx.Step(2f);
        ctx.Click(ScreenButton(panel, "STOP ROBOT"));
        ctx.Check(!robot.IsTouring && robot.State == RobotState.Paused, "[STOP ROBOT] cancels the tour and stops the robot");

        // The user standing on a stop point: the robot parks next to them.
        var avatar = ctx.Find<ScientistAvatar>();
        var obstacle = avatar != null ? avatar.GetComponent<NavMeshObstacle>() : null;
        if (ctx.Check(obstacle != null, "The user carves the NavMesh (NavMeshObstacle on the avatar)"))
        {
            // The avatar follows the headset, so the user is moved by moving the XR rig.
            Transform rig = Camera.main.transform.root;
            Vector3 rigWas = rig.position;
            Vector3 scaleStop = robot.GetStation(1).StopPosition;
            rig.position += scaleStop - new Vector3(avatar.transform.position.x, scaleStop.y, avatar.transform.position.z);
            ctx.Step(0.5f);
            ctx.Check(Horizontal(avatar.transform.position, scaleStop) < 0.1f, "The avatar (and its obstacle) follows the headset onto the stop point");
            ctx.Click(ScreenButton(panel, "GO TO DIGITAL SCALE"));
            bool done = ctx.StepUntil(() => robot.State != RobotState.Moving, 60f);
            float distance = Horizontal(robot.transform.position, scaleStop);
            ctx.Check(done && robot.State == RobotState.Arrived && distance > 0.5f && distance < 1.3f, $"With the user standing on the stop point the robot parks {distance:0.00} m away ('{robot.LastMessage}')");
            rig.position = rigWas;
            ctx.Step(0.5f);
        }

        ctx.Click(ScreenButton(panel, "RETURN TO STARTING POSITION"));
        ctx.StepUntil(() => robot.State != RobotState.Moving, 60f);
    }

    private static TMPro.TMP_Text Text(RobotControlPanel panel, string field)
    {
        return (TMPro.TMP_Text)typeof(RobotControlPanel).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(panel);
    }

    // ------------------------------------------------------------------ Keyboard shortcuts

    private static void KeyboardShortcuts(CheckContext ctx)
    {
        ctx.Section("Desktop shortcuts for the new features");
        var cabinet = ctx.Find<LaboratoryCabinet>();
        var robot = ctx.Find<LabAssistantRobot>();
        Keyboard keyboard = Keyboard.SimCreate();

        keyboard.SimTap(Key.C);
        ctx.StepFrames(2);
        ctx.Check(cabinet.IsOpen, "C opens the cabinet doors");
        ctx.Step(1.5f);
        keyboard.SimTap(Key.C);
        ctx.StepFrames(2);
        ctx.Step(1.5f);
        ctx.Check(!cabinet.IsOpen, "C closes them again");

        keyboard.SimTap(Key.Digit7);
        ctx.StepFrames(2);
        ctx.Check(robot.State == RobotState.Moving && robot.DestinationName == "Digital Scale", "7 sends the robot to the digital scale");
        ctx.Step(1f);
        keyboard.SimTap(Key.X);
        ctx.StepFrames(2);
        ctx.Check(robot.State == RobotState.Paused, "X stops the robot");
        keyboard.SimTap(Key.X);
        ctx.StepFrames(2);
        ctx.Check(robot.State == RobotState.Moving, "X resumes it");
        keyboard.SimTap(Key.Digit6);
        ctx.StepFrames(2);
        ctx.Check(robot.DestinationIndex == LabAssistantRobot.StartStationIndex, "6 sends it back to its starting position");
        ctx.StepUntil(() => robot.State != RobotState.Moving, 60f);

        // Reset: instruments, loose objects, cabinet doors, robot.
        cabinet.OpenDoors();
        ctx.Step(1.5f);
        ctx.Find<LabResetController>().ResetLab();
        ctx.Step(2f);
        ctx.Check(!cabinet.IsOpen && cabinet.StoredCount == cabinet.TotalCount, $"RESET LABORATORY closes the cabinet and returns its contents ({cabinet.StatusLine})");
    }
}
