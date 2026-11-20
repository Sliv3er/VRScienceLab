using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace VRScienceLab.Module2.Tests
{
    /// <summary>
    /// PlayMode tests for the combined heating station, the storage cabinet and the robot control
    /// screen. The first part builds small objects in code; the "LabScene_" tests load the
    /// generated laboratory scene and operate it the way a user does (UI clicks, door presses,
    /// objects moved between the cabinet and the instruments).
    /// </summary>
    public class Module2StationCabinetRobotTests
    {
        private const string LabScenePath = "Assets/Module2_Equipment/Scenes/Module2_Lab.unity";

        private readonly List<Object> created = new List<Object>();

        private GameObject NewObject(string name)
        {
            var go = new GameObject(name);
            created.Add(go);
            return go;
        }

        [TearDown]
        public void TearDown()
        {
            Time.timeScale = 1f;
            foreach (Object item in created)
            {
                if (item != null) Object.Destroy(item);
            }

            created.Clear();
        }

        private static void SetField(object target, string fieldName, object value)
        {
            System.Type type = target.GetType();
            while (type != null)
            {
                FieldInfo field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                if (field != null)
                {
                    field.SetValue(target, value);
                    return;
                }

                type = type.BaseType;
            }

            Assert.Fail($"Field '{fieldName}' not found on {target.GetType().Name}.");
        }

        /// <summary>Adds a component on an inactive object, fills its fields, then activates it (so Awake sees the references).</summary>
        private T CreateConfigured<T>(string name, System.Action<T> configure) where T : Component
        {
            GameObject go = NewObject(name);
            go.SetActive(false);
            T component = go.AddComponent<T>();
            configure(component);
            go.SetActive(true);
            return component;
        }

        private MeasurementZone CreateZone(string name, Vector3 size)
        {
            GameObject go = NewObject(name);
            var box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = size;
            var body = go.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            return go.AddComponent<MeasurementZone>();
        }

        private Labware CreateLabware(string name, LabwareKind kind, float massKilograms)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            created.Add(go);
            go.name = name;
            go.transform.localScale = Vector3.one * 0.05f;
            var body = go.AddComponent<Rigidbody>();
            body.mass = massKilograms;
            body.useGravity = false;
            var labware = go.AddComponent<Labware>();
            SetField(labware, "displayName", name);
            SetField(labware, "kind", kind);
            return labware;
        }

        // ---------------------------------------------------------------- Combined heating station

        [UnityTest]
        public IEnumerator HeatingStation_HeaterAndTimerAreIndependent()
        {
            HotPlate plate = NewObject("HotPlate").AddComponent<HotPlate>();
            StopwatchTimer timer = NewObject("Stopwatch").AddComponent<StopwatchTimer>();
            CombinedHeatingStation station = CreateConfigured<CombinedHeatingStation>("HeatingStation", s =>
            {
                SetField(s, "hotPlate", plate);
                SetField(s, "stopwatch", timer);
            });
            yield return null;

            Assert.AreSame(plate, station.HotPlate);
            Assert.AreSame(timer, station.Stopwatch);
            Assert.IsFalse(station.IsActive);

            // Heating does not start the timer.
            plate.SwitchOn();
            plate.AdvanceTime(2f);
            Assert.IsTrue(station.IsHeating);
            Assert.AreEqual(plate.InitialTemperature + 2f * plate.HeatingRate, plate.CurrentTemperature, 0.001f);
            Assert.AreEqual(StopwatchState.Idle, timer.State, "Switching the heater on must not start the timer.");
            Assert.AreEqual(0.0, timer.ElapsedSeconds, 0.0001);

            // Switching the heater off stops the temperature rise.
            plate.SwitchOff();
            float afterOff = plate.CurrentTemperature;
            plate.AdvanceTime(1f);
            Assert.LessOrEqual(plate.CurrentTemperature, afterOff, "The temperature must not rise while the heater is off.");

            // The timer runs, pauses, resumes and resets without touching the heater.
            HotPlateState heaterState = plate.State;
            timer.StartTimer();
            timer.AdvanceTime(1.5f);
            Assert.IsTrue(station.IsTiming);
            timer.PauseTimer();
            timer.AdvanceTime(5f);
            Assert.AreEqual(1.5, timer.ElapsedSeconds, 0.0001, "A paused timer keeps its time.");
            timer.StartTimer();
            timer.AdvanceTime(0.5f);
            Assert.AreEqual(2.0, timer.ElapsedSeconds, 0.0001, "Resume continues from the paused time.");
            Assert.AreEqual(heaterState, plate.State, "Operating the timer must not change the heater.");
            timer.ResetTimer();
            Assert.AreEqual(0.0, timer.ElapsedSeconds, 0.0001);
            Assert.AreEqual(StopwatchState.Idle, timer.State);

            StringAssert.Contains("HEATER", station.StatusSummary);
            StringAssert.Contains("TIMER IDLE", station.StatusSummary);
        }

        [UnityTest]
        public IEnumerator HeatingStation_StationLevelControlsUseBothInstruments()
        {
            HotPlate plate = NewObject("HotPlate").AddComponent<HotPlate>();
            StopwatchTimer timer = NewObject("Stopwatch").AddComponent<StopwatchTimer>();
            CombinedHeatingStation station = CreateConfigured<CombinedHeatingStation>("HeatingStation", s =>
            {
                SetField(s, "hotPlate", plate);
                SetField(s, "stopwatch", timer);
            });
            yield return null;

            int notifications = 0;
            station.onStatusChanged.AddListener(_ => notifications++);

            plate.SwitchOn();
            timer.StartTimer();
            plate.AdvanceTime(3f);
            timer.AdvanceTime(3f);
            Assert.GreaterOrEqual(notifications, 2, "The station reports the state changes of both instruments.");

            station.SwitchEverythingOff();
            Assert.AreEqual(HotPlateState.Off, plate.State);
            Assert.AreEqual(StopwatchState.Paused, timer.State);
            Assert.AreEqual(3.0, timer.ElapsedSeconds, 0.0001, "Switching off keeps the measured time.");
            Assert.Greater(plate.CurrentTemperature, plate.InitialTemperature, "Switching off keeps the temperature reading.");

            station.ResetStation();
            Assert.AreEqual(plate.InitialTemperature, plate.CurrentTemperature, 0.001f);
            Assert.AreEqual(0.0, timer.ElapsedSeconds, 0.0001);
            Assert.IsFalse(station.IsActive);
        }

        [UnityTest]
        public IEnumerator HeatingSurface_WarmsTheSampleStandingOnThePlate()
        {
            HotPlate plate = NewObject("HotPlate").AddComponent<HotPlate>();
            MeasurementZone zone = CreateZone("HeatingZone", Vector3.one * 0.3f);
            HeatingSurface surface = CreateConfigured<HeatingSurface>("HeatingSurface", s =>
            {
                SetField(s, "hotPlate", plate);
                SetField(s, "zone", zone);
            });
            yield return null;

            Labware beaker = CreateLabware("Beaker", LabwareKind.Beaker, 0.25f);
            var sample = beaker.gameObject.AddComponent<TemperatureSample>();
            sample.SetTemperature(22f);
            Labware emptyFlask = CreateLabware("Empty flask", LabwareKind.ErlenmeyerFlask, 0.2f);

            // Cold plate: nothing happens.
            zone.AddOccupant(beaker.GetComponent<Collider>());
            zone.AddOccupant(emptyFlask.GetComponent<Collider>());
            surface.AdvanceTime(5f);
            Assert.AreEqual(2, surface.ContainerCount);
            Assert.AreEqual(22f, sample.TemperatureCelsius, 0.001f, "A plate at room temperature does not heat the sample.");

            // Hot plate: the sample warms at the configured rate.
            plate.SwitchOn();
            plate.AdvanceTime(10f); // 20 + 10 * 10 = 120 °C
            surface.AdvanceTime(3f);
            Assert.AreEqual(22f + 3f * surface.HeatTransferRate, sample.TemperatureCelsius, 0.001f);
            Assert.AreEqual(1, surface.HeatedSampleCount, "Only the container with a sample is heated.");

            // It never exceeds the configured maximum.
            surface.AdvanceTime(1000f);
            Assert.AreEqual(surface.MaximumSampleTemperature, sample.TemperatureCelsius, 0.001f);

            // Off the plate, the sample is no longer heated.
            zone.RemoveOccupant(beaker.GetComponent<Collider>());
            sample.SetTemperature(30f);
            surface.AdvanceTime(5f);
            Assert.AreEqual(30f, sample.TemperatureCelsius, 0.001f);
        }

        // ---------------------------------------------------------------- Cabinet doors and contents

        private CabinetDoor CreateDoor(string name, float openAngle)
        {
            GameObject go = NewObject(name);
            go.SetActive(false);
            go.AddComponent<BoxCollider>();
            go.AddComponent<XRSimpleInteractable>();
            var door = go.AddComponent<CabinetDoor>();
            SetField(door, "doorName", name);
            SetField(door, "openAngle", openAngle);
            SetField(door, "swingDuration", 0.8f);
            go.SetActive(true);
            return door;
        }

        [UnityTest]
        public IEnumerator CabinetDoor_OpensAndClosesAroundItsHinge()
        {
            CabinetDoor door = CreateDoor("Left door", 105f);
            yield return null;

            Assert.AreEqual(CabinetDoorState.Closed, door.State);
            Assert.AreEqual(0f, door.OpenFraction, 0.0001f);

            Assert.IsTrue(door.Press(), "A select press toggles the door.");
            Assert.AreEqual(CabinetDoorState.Opening, door.State);
            Assert.IsFalse(door.Press(), "A second press within the cooldown is ignored.");

            door.AdvanceTime(0.4f);
            Assert.Greater(door.OpenFraction, 0.4f);
            Assert.Less(door.OpenFraction, 0.6f);

            door.AdvanceTime(1f);
            Assert.AreEqual(CabinetDoorState.Open, door.State);
            Assert.AreEqual(105f, door.CurrentAngle, 0.01f);
            Assert.AreEqual(105f, Quaternion.Angle(Quaternion.identity, door.transform.localRotation), 0.5f, "The hinge transform is rotated by the open angle.");

            // A point on the leaf (local +X) swings out toward -Z.
            Vector3 swung = door.transform.localRotation * Vector3.right;
            Assert.Less(swung.z, -0.9f);

            door.Close();
            Assert.AreEqual(CabinetDoorState.Closing, door.State);
            door.Open();
            Assert.AreEqual(CabinetDoorState.Opening, door.State, "A closing door can be re-opened half way.");
            door.Close();
            door.AdvanceTime(2f);
            Assert.AreEqual(CabinetDoorState.Closed, door.State);
            Assert.AreEqual(0f, Quaternion.Angle(Quaternion.identity, door.transform.localRotation), 0.01f);

            door.SetOpenImmediate(true);
            Assert.AreEqual(CabinetDoorState.Open, door.State);
            door.SetOpenImmediate(false);
            Assert.IsTrue(door.IsClosed);
        }

        [UnityTest]
        public IEnumerator CabinetDoor_SwingsOverTimeInPlayMode()
        {
            CabinetDoor door = CreateDoor("Right door", -105f);
            yield return null;

            door.Open();
            yield return new WaitForSeconds(1.2f);
            Assert.AreEqual(CabinetDoorState.Open, door.State, "The door finishes opening on its own (Update).");

            Vector3 swung = door.transform.localRotation * Vector3.left;
            Assert.Less(swung.z, -0.9f, "The right leaf (local -X) also swings out toward -Z.");
        }

        [UnityTest]
        public IEnumerator LaboratoryCabinet_TracksDoorsAndStoredLabware()
        {
            CabinetDoor left = CreateDoor("Left door", 105f);
            CabinetDoor right = CreateDoor("Right door", -105f);
            MeasurementZone zone = CreateZone("InteriorZone", Vector3.one);
            Labware beaker = CreateLabware("Beaker", LabwareKind.Beaker, 0.2f);
            Labware flask = CreateLabware("Flask", LabwareKind.ErlenmeyerFlask, 0.25f);
            LaboratoryCabinet cabinet = CreateConfigured<LaboratoryCabinet>("Cabinet", c =>
            {
                SetField(c, "doors", new[] { left, right });
                SetField(c, "interiorZone", zone);
                SetField(c, "contents", new[] { beaker, flask });
            });
            yield return null;

            Assert.IsFalse(cabinet.IsOpen);
            Assert.AreEqual(2, cabinet.TotalCount);

            zone.AddOccupant(beaker.GetComponent<Collider>());
            zone.AddOccupant(flask.GetComponent<Collider>());
            Assert.AreEqual(2, cabinet.StoredCount);
            StringAssert.Contains("DOORS CLOSED", cabinet.StatusLine);
            StringAssert.Contains("2 / 2", cabinet.StatusLine);

            cabinet.OpenDoors();
            Assert.IsTrue(cabinet.IsOpen);
            left.AdvanceTime(2f);
            right.AdvanceTime(2f);
            Assert.IsTrue(cabinet.IsFullyOpen);
            StringAssert.Contains("DOORS OPEN", cabinet.StatusLine);

            // Take an object out, then put it back.
            zone.RemoveOccupant(beaker.GetComponent<Collider>());
            Assert.AreEqual(1, cabinet.StoredCount);
            Assert.IsFalse(cabinet.IsStored(beaker));
            Assert.IsTrue(cabinet.IsStored(flask));
            zone.AddOccupant(beaker.GetComponent<Collider>());
            Assert.AreEqual(2, cabinet.StoredCount);

            cabinet.ToggleDoors();
            left.AdvanceTime(2f);
            right.AdvanceTime(2f);
            Assert.IsFalse(cabinet.IsOpen);

            // One door alone counts as open.
            left.Open();
            Assert.IsTrue(cabinet.IsOpen);
            Assert.IsFalse(cabinet.IsFullyOpen);
            cabinet.CloseDoorsImmediate();
            Assert.IsFalse(cabinet.IsOpen);
        }

        [Test]
        public void Labware_ReportsItsRigidbodyMassInGrams()
        {
            Labware beaker = CreateLabware("Beaker", LabwareKind.Beaker, 0.25f);
            Assert.AreEqual(250f, beaker.MassGrams, 0.001f);
            Assert.AreEqual(LabwareKind.Beaker, beaker.Kind);
            Assert.AreEqual("Beaker", beaker.DisplayName);
        }

        // ---------------------------------------------------------------- Robot and control screen

        private struct RobotRig
        {
            public LabAssistantRobot Robot;
            public RobotControlPanel Panel;
            public RobotStation[] Stations;
        }

        /// <summary>A floor with a wall in the middle, baked at runtime, three stations, a robot and its control panel.</summary>
        private RobotRig CreateRobotRig(bool withNavMesh)
        {
            var origin = new Vector3(60f, 0f, -60f);
            if (withNavMesh)
            {
                GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
                created.Add(ground);
                ground.transform.position = origin + new Vector3(0f, -0.5f, 0f);
                ground.transform.localScale = new Vector3(14f, 1f, 14f);
                GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                created.Add(wall);
                // World size first, then parent (the ground is scaled).
                wall.transform.position = origin + new Vector3(0f, 1f, 0f);
                wall.transform.localScale = new Vector3(0.4f, 2f, 5f);
                wall.transform.SetParent(ground.transform, true);

                var surface = ground.AddComponent<NavMeshSurface>();
                surface.collectObjects = CollectObjects.Children;
                surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
                surface.BuildNavMesh();
            }

            string[] names = { "Starting Position", "Digital Scale", "Heating Station" };
            Vector3[] offsets = { new Vector3(-4f, 0f, 0f), new Vector3(4f, 0f, 0f), new Vector3(4f, 0f, 4f) };
            var stations = new RobotStation[names.Length];
            for (int i = 0; i < names.Length; i++)
            {
                stations[i] = NewObject(names[i]).AddComponent<RobotStation>();
                stations[i].transform.position = origin + offsets[i];
                SetField(stations[i], "stationName", names[i]);
            }

            GameObject robotObject = NewObject("Robot");
            robotObject.SetActive(false);
            robotObject.transform.position = stations[0].transform.position;
            var agent = robotObject.AddComponent<NavMeshAgent>();
            agent.radius = 0.3f;
            agent.speed = 3.5f;
            agent.acceleration = 14f;
            agent.stoppingDistance = 0.05f;
            var robot = robotObject.AddComponent<LabAssistantRobot>();
            SetField(robot, "stations", stations);
            SetField(robot, "logCommands", false);
            robotObject.SetActive(true);

            RobotControlPanel panel = CreateConfigured<RobotControlPanel>("Panel", p =>
            {
                SetField(p, "robot", robot);
                SetField(p, "logPresses", false);
            });

            return new RobotRig { Robot = robot, Panel = panel, Stations = stations };
        }

        private static IEnumerator WaitWhileMoving(LabAssistantRobot robot, float timeoutSeconds)
        {
            float timeout = timeoutSeconds;
            while (robot.State == RobotState.Moving && timeout > 0f)
            {
                timeout -= Time.deltaTime;
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator RobotControlPanel_EveryCommandReachesTheRobot()
        {
            RobotRig rig = CreateRobotRig(true);
            LabAssistantRobot robot = rig.Robot;
            RobotControlPanel panel = rig.Panel;
            yield return null;

            Assert.IsTrue(robot.IsOnNavMesh, "The robot must start on the NavMesh.");
            Assert.AreEqual(RobotState.Idle, robot.State);

            // Destination button: the robot receives the destination and starts to move.
            panel.GoToStation(1);
            Assert.AreEqual("GO TO DIGITAL SCALE", panel.LastCommand);
            Assert.AreEqual(RobotState.Moving, robot.State);
            Assert.AreEqual("Digital Scale", robot.DestinationName);
            Assert.IsTrue(robot.LastCommandAccepted);
            yield return new WaitForSeconds(0.6f);
            Assert.Greater(Vector3.Distance(robot.transform.position, rig.Stations[0].transform.position), 0.3f, "The NavMeshAgent is driving.");

            // Stop button: the robot really stops.
            panel.StopRobot();
            Assert.AreEqual(RobotState.Paused, robot.State);
            yield return new WaitForSeconds(0.4f);
            Vector3 stoppedAt = robot.transform.position;
            yield return new WaitForSeconds(0.4f);
            Assert.Less(Vector3.Distance(stoppedAt, robot.transform.position), 0.05f, "A stopped robot stays where it is.");

            // Resume, then arrival: the robot stops by itself at the destination.
            panel.ResumeRobot();
            Assert.AreEqual(RobotState.Moving, robot.State);
            yield return WaitWhileMoving(robot, 15f);
            Assert.AreEqual(RobotState.Arrived, robot.State, "The robot must reach the station around the wall.");
            Vector3 offset = robot.transform.position - rig.Stations[1].transform.position;
            offset.y = 0f;
            Assert.Less(offset.magnitude, 0.3f);
            yield return new WaitForSeconds(0.3f);
            Assert.Less(robot.Speed, 0.05f, "The robot is at rest after arriving.");

            // A new destination from the arrived state.
            panel.GoToStation(2);
            Assert.AreEqual("Heating Station", robot.DestinationName);
            Assert.AreEqual(RobotState.Moving, robot.State);
            yield return WaitWhileMoving(robot, 15f);
            Assert.AreEqual(RobotState.Arrived, robot.State);

            // Return to the starting position.
            panel.ReturnToStart();
            Assert.AreEqual("RETURN TO STARTING POSITION", panel.LastCommand);
            Assert.AreEqual(LabAssistantRobot.StartStationIndex, robot.DestinationIndex);
            yield return WaitWhileMoving(robot, 20f);
            Assert.AreEqual(RobotState.Arrived, robot.State);
            offset = robot.transform.position - rig.Stations[0].transform.position;
            offset.y = 0f;
            Assert.Less(offset.magnitude, 0.3f, "The robot is back where it started.");

            Assert.AreEqual(5, panel.CommandCount);
        }

        [UnityTest]
        public IEnumerator Robot_ReportsWhyItCannotMoveWithoutNavMesh()
        {
            LogAssert.ignoreFailingMessages = true; // Unity itself logs an error for an agent without NavMesh
            RobotRig rig = CreateRobotRig(false);
            yield return null;

            rig.Panel.GoToStation(1);
            Assert.AreNotEqual(RobotState.Moving, rig.Robot.State);
            Assert.IsFalse(rig.Robot.LastCommandAccepted, "The command must be reported as failed, not silently ignored.");
            StringAssert.Contains("NavMesh", rig.Robot.LastMessage);
            Assert.IsFalse(rig.Robot.IsOnNavMesh);
            LogAssert.ignoreFailingMessages = false;
        }

        [UnityTest]
        public IEnumerator Robot_AimsForTheNearestReachablePointWhenTheStopIsOccupied()
        {
            RobotRig rig = CreateRobotRig(true);
            yield return null;

            // An obstacle (for example the user) stands exactly on the station.
            GameObject blocker = NewObject("Blocker");
            blocker.transform.position = rig.Stations[1].transform.position;
            var obstacle = blocker.AddComponent<NavMeshObstacle>();
            obstacle.shape = NavMeshObstacleShape.Capsule;
            obstacle.radius = 0.35f;
            obstacle.height = 1.8f;
            obstacle.carving = true;
            obstacle.carveOnlyStationary = false;
            yield return new WaitForSeconds(0.3f);

            rig.Robot.GoToStation(1);
            yield return WaitWhileMoving(rig.Robot, 20f);

            Assert.AreEqual(RobotState.Arrived, rig.Robot.State, "The robot parks next to an occupied stop instead of giving up.");
            Vector3 offset = rig.Robot.transform.position - rig.Stations[1].transform.position;
            offset.y = 0f;
            Assert.Less(offset.magnitude, 1.5f);
            Assert.Greater(offset.magnitude, 0.3f, "It does not drive into the obstacle.");
        }

        // ---------------------------------------------------------------- Generated lab scene (end to end)

        private static IEnumerator LoadLabScene()
        {
            if (SceneUtility.GetBuildIndexByScenePath(LabScenePath) < 0)
            {
                Assert.Ignore("The lab scene is not in the build settings. Run VR Science Lab > Module 2 > Build Lab Scene first.");
            }

            SceneManager.LoadScene(LabScenePath, LoadSceneMode.Single);
            yield return null;
            yield return null;
        }

        private static Button FindButton(Transform root, string buttonName)
        {
            foreach (Button button in root.GetComponentsInChildren<Button>(true))
            {
                if (button.name == buttonName) return button;
            }

            Assert.Fail($"Button '{buttonName}' not found under '{root.name}'.");
            return null;
        }

        /// <summary>Clicks a UI button exactly as the EventSystem does for a pointer click.</summary>
        private static void Click(Button button)
        {
            Assert.IsTrue(button.isActiveAndEnabled && button.interactable, $"Button '{button.name}' is not clickable.");
            var pointer = new PointerEventData(EventSystem.current);
            ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerClickHandler);
        }

        [UnityTest]
        public IEnumerator LabScene_HeatingStationIsOnePieceOfEquipment()
        {
            yield return LoadLabScene();

            var station = Object.FindFirstObjectByType<CombinedHeatingStation>();
            Assert.IsNotNull(station, "The combined heating station is missing.");
            Assert.IsNotNull(station.HotPlate);
            Assert.IsNotNull(station.Stopwatch);
            Assert.AreSame(station.transform, station.HotPlate.transform.parent, "The heater is part of the station.");
            Assert.AreSame(station.transform, station.Stopwatch.transform.parent, "The timer is part of the station.");
            Assert.AreEqual(1, Object.FindObjectsByType<HotPlate>(FindObjectsSortMode.None).Length, "No separate hot plate is left in the scene.");
            Assert.AreEqual(1, Object.FindObjectsByType<StopwatchTimer>(FindObjectsSortMode.None).Length, "No separate stopwatch is left in the scene.");
            Assert.IsTrue(station.HotPlate.HasValueDisplay);
            Assert.IsTrue(station.Stopwatch.HasValueDisplay);

            // The station's own push buttons operate the two instruments.
            InstrumentButton[] buttons = station.GetComponentsInChildren<InstrumentButton>(true);
            Assert.GreaterOrEqual(buttons.Length, 6, "ON, OFF, RESET, START, PAUSE, RESET.");

            InstrumentButton Find(Transform module, string buttonName)
            {
                Transform found = module.Find(buttonName);
                Assert.IsNotNull(found, $"{buttonName} not found on {module.name}.");
                return found.GetComponent<InstrumentButton>();
            }

            Transform heater = station.HotPlate.transform;
            Transform timer = station.Stopwatch.transform;

            Assert.IsTrue(Find(heater, "OnButton").Press());
            Assert.AreEqual(HotPlateState.Heating, station.HotPlate.State);
            Assert.AreEqual(StopwatchState.Idle, station.Stopwatch.State);
            yield return new WaitForSeconds(1f);
            float heated = station.HotPlate.CurrentTemperature;
            Assert.Greater(heated, station.HotPlate.InitialTemperature + 5f, "The temperature rises while heating.");
            StringAssert.Contains("°C", station.HotPlate.ValueDisplayText);

            Assert.IsTrue(Find(heater, "OffButton").Press());
            yield return new WaitForSeconds(0.5f);
            Assert.LessOrEqual(station.HotPlate.CurrentTemperature, heated + 0.01f, "The temperature stops rising when switched off.");

            Assert.IsTrue(Find(heater, "ResetButton").Press());
            Assert.AreEqual(station.HotPlate.InitialTemperature, station.HotPlate.CurrentTemperature, 0.01f);

            Assert.IsTrue(Find(timer, "StartButton").Press());
            yield return new WaitForSeconds(0.5f);
            Assert.AreEqual(StopwatchState.Running, station.Stopwatch.State);
            Assert.Greater(station.Stopwatch.ElapsedSeconds, 0.3);

            Assert.IsTrue(Find(timer, "PauseButton").Press());
            double paused = station.Stopwatch.ElapsedSeconds;
            yield return new WaitForSeconds(0.4f);
            Assert.AreEqual(paused, station.Stopwatch.ElapsedSeconds, 0.0001, "Paused time is preserved.");

            Assert.IsTrue(Find(timer, "StartButton").Press());
            yield return new WaitForSeconds(0.4f);
            Assert.Greater(station.Stopwatch.ElapsedSeconds, paused + 0.2, "Resume continues counting.");

            Assert.IsTrue(Find(timer, "ResetButton").Press());
            Assert.AreEqual(0.0, station.Stopwatch.ElapsedSeconds, 0.0001);
            Assert.AreEqual("00:00.00", station.Stopwatch.ValueDisplayText);
        }

        [UnityTest]
        public IEnumerator LabScene_ScreensAcceptBothMouseAndControllerRays()
        {
            yield return LoadLabScene();

            Canvas[] canvases = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
            int worldCanvases = 0;
            foreach (Canvas canvas in canvases)
            {
                // Only the three monitors: the robot's chest text and the simulator overlay are not screens.
                if (canvas.renderMode != RenderMode.WorldSpace || canvas.transform.root.name != "Interface") continue;

                worldCanvases++;
                Assert.IsNotNull(canvas.GetComponent<GraphicRaycaster>(), canvas.transform.parent.name + ": GraphicRaycaster missing (mouse and simulator clicks would not reach the buttons).");
                Assert.IsNotNull(canvas.GetComponent<TrackedDeviceGraphicRaycaster>(), canvas.transform.parent.name + ": TrackedDeviceGraphicRaycaster missing (controller rays would not reach the buttons).");
                Assert.IsTrue(canvas.worldCamera != null || Camera.main != null, canvas.transform.parent.name + ": no event camera.");
            }

            Assert.AreEqual(3, worldCanvases, "Main display, sample console and robot console.");
            Assert.IsNotNull(Object.FindFirstObjectByType<EventSystem>(), "EventSystem missing.");
            Assert.IsNotNull(Object.FindFirstObjectByType<XRUIInputModule>(), "XR UI Input Module missing.");
        }

        [UnityTest]
        public IEnumerator LabScene_RobotControlScreenButtonsDriveTheRobot()
        {
            yield return LoadLabScene();

            var robot = Object.FindFirstObjectByType<LabAssistantRobot>();
            var panel = Object.FindFirstObjectByType<RobotControlPanel>();
            Assert.IsNotNull(robot);
            Assert.IsNotNull(panel);
            Assert.IsTrue(robot.gameObject.activeInHierarchy, "The robot must be visible in the laboratory.");
            Assert.Greater(robot.GetComponentsInChildren<MeshRenderer>().Length, 10, "The robot has a 3D model.");
            Assert.IsNotNull(Object.FindFirstObjectByType<NavMeshSurface>(), "NavMeshSurface missing.");
            Assert.IsNotNull(Object.FindFirstObjectByType<NavMeshSurface>().navMeshData, "The NavMesh is not baked.");
            Assert.IsTrue(robot.Agent.enabled, "The NavMeshAgent must be enabled.");
            Assert.IsTrue(robot.IsOnNavMesh, "The robot must stand on the NavMesh.");

            // Every station is a valid, reachable destination.
            for (int i = 0; i < robot.StationCount; i++)
            {
                RobotStation station = robot.GetStation(i);
                Assert.IsNotNull(station, $"Station {i} is missing.");
                Assert.IsTrue(NavMesh.SamplePosition(station.StopPosition, out NavMeshHit hit, 1.5f, NavMesh.AllAreas), $"'{station.StationName}' is not near the NavMesh.");
                var path = new NavMeshPath();
                Assert.IsTrue(NavMesh.CalculatePath(robot.transform.position, hit.position, NavMesh.AllAreas, path), $"No path to '{station.StationName}'.");
                Assert.AreEqual(NavMeshPathStatus.PathComplete, path.status, $"'{station.StationName}' cannot be reached.");
            }

            Vector3 start = robot.transform.position;
            Time.timeScale = 8f;

            string[] destinationButtons = { "GO TO DIGITAL SCALE", "GO TO HEATING STATION", "GO TO STORAGE CABINET", "GO TO LABORATORY WORKBENCH" };
            string[] destinationNames = { "Digital Scale", "Heating Station", "Storage Cabinet", "Laboratory Workbench" };
            for (int i = 0; i < destinationButtons.Length; i++)
            {
                Click(FindButton(panel.transform, destinationButtons[i]));
                Assert.AreEqual(destinationButtons[i], panel.LastCommand, "The screen registers the click.");
                Assert.AreEqual(destinationNames[i], robot.DestinationName, "The robot receives the destination.");
                Assert.AreEqual(RobotState.Moving, robot.State, "The NavMeshAgent starts moving.");

                yield return WaitWhileMoving(robot, 90f);
                Assert.AreEqual(RobotState.Arrived, robot.State, $"The robot must reach '{destinationNames[i]}'. Last message: {robot.LastMessage}");
                Vector3 offset = robot.transform.position - robot.GetStation(robot.DestinationIndex).StopPosition;
                offset.y = 0f;
                Assert.Less(offset.magnitude, 1.3f, $"The robot stops at '{destinationNames[i]}'.");
            }

            // Stop Robot: pressed on the way to the starting position.
            Click(FindButton(panel.transform, "RETURN TO STARTING POSITION"));
            Assert.AreEqual(RobotState.Moving, robot.State);
            Assert.AreEqual(LabAssistantRobot.StartStationIndex, robot.DestinationIndex);
            yield return new WaitForSeconds(1.5f);
            Click(FindButton(panel.transform, "STOP ROBOT"));
            Assert.AreEqual(RobotState.Paused, robot.State, "Stop Robot stops the robot.");
            yield return new WaitForSeconds(1f);
            Vector3 stoppedAt = robot.transform.position;
            yield return new WaitForSeconds(1f);
            Assert.Less(Vector3.Distance(stoppedAt, robot.transform.position), 0.05f, "The robot does not move while stopped.");

            // Return to Starting Position.
            Click(FindButton(panel.transform, "RETURN TO STARTING POSITION"));
            yield return WaitWhileMoving(robot, 90f);
            Assert.AreEqual(RobotState.Arrived, robot.State);
            Vector3 home = robot.transform.position - start;
            home.y = 0f;
            Assert.Less(home.magnitude, 0.5f, "The robot is back at its initial location.");
            Time.timeScale = 1f;
        }

        [UnityTest]
        public IEnumerator LabScene_CabinetHoldsItsEquipmentAndTheDoorsWork()
        {
            yield return LoadLabScene();
            yield return new WaitForSeconds(2f); // let every object settle on its shelf

            var cabinet = Object.FindFirstObjectByType<LaboratoryCabinet>();
            Assert.IsNotNull(cabinet, "The storage cabinet is missing.");
            Assert.AreEqual(2, cabinet.DoorCount);
            Assert.GreaterOrEqual(cabinet.TotalCount, 20);

            // Required equipment is present.
            var kinds = new HashSet<LabwareKind>();
            for (int i = 0; i < cabinet.TotalCount; i++) kinds.Add(cabinet.GetContent(i).Kind);
            foreach (LabwareKind required in new[] { LabwareKind.TestTube, LabwareKind.ErlenmeyerFlask, LabwareKind.TestTubeRack, LabwareKind.Beaker, LabwareKind.LaboratoryTape })
            {
                Assert.IsTrue(kinds.Contains(required), required + " is missing from the cabinet.");
            }

            // Nothing fell through a shelf or out of the cabinet, and everything can be grabbed.
            Assert.AreEqual(cabinet.TotalCount, cabinet.StoredCount, "Every object rests inside the cabinet. " + cabinet.StatusLine);
            for (int i = 0; i < cabinet.TotalCount; i++)
            {
                Labware item = cabinet.GetContent(i);
                Assert.Greater(item.transform.position.y, 0.9f, item.DisplayName + " fell through its shelf.");
                Assert.IsNotNull(item.GetComponent<XRGrabInteractable>(), item.DisplayName + " cannot be grabbed.");
                Assert.IsNotNull(item.GetComponentInChildren<Collider>(), item.DisplayName + " has no collider.");
                Assert.Less(item.Body.linearVelocity.magnitude, 0.05f, item.DisplayName + " is still moving.");
            }

            // The test tubes stand in the rack; one hole is free.
            var rack = Object.FindFirstObjectByType<TestTubeRack>();
            Assert.IsNotNull(rack);
            Assert.AreEqual(6, rack.Capacity);
            Assert.AreEqual(5, rack.OccupiedCount, "Five test tubes start in the rack.");
            Assert.AreEqual(5, rack.FirstFreeSlot());

            // Doors: a press on each door opens it; the cabinet reports it.
            Assert.IsFalse(cabinet.IsOpen);
            Assert.IsTrue(cabinet.GetDoor(0).Press());
            Assert.IsTrue(cabinet.GetDoor(1).Press());
            yield return new WaitForSeconds(1.5f);
            Assert.IsTrue(cabinet.IsFullyOpen, "Both doors are open.");
            StringAssert.Contains("DOORS OPEN", cabinet.StatusLine);

            // Take a beaker with a sample to the instruments, then put it back.
            Labware beaker = null;
            for (int i = 0; i < cabinet.TotalCount && beaker == null; i++)
            {
                Labware candidate = cabinet.GetContent(i);
                if (candidate.Kind == LabwareKind.Beaker && candidate.GetComponent<TemperatureSample>() != null) beaker = candidate;
            }

            Assert.IsNotNull(beaker, "A beaker with a sample is expected in the cabinet.");
            Vector3 shelfPosition = beaker.transform.position;
            Quaternion shelfRotation = beaker.transform.rotation;

            var scale = Object.FindFirstObjectByType<DigitalScale>();
            scale.PowerOn();
            Transform pan = scale.transform.Find("SampleAnchor");
            Assert.IsNotNull(pan);
            Teleport(beaker.Body, pan.position, Quaternion.identity);
            yield return new WaitForSeconds(1f);
            Assert.AreEqual(cabinet.TotalCount - 1, cabinet.StoredCount, "The cabinet notices the missing object.");
            Assert.AreEqual(beaker.MassGrams, scale.DisplayedMassGrams, 1f, "The scale weighs the beaker taken from the cabinet.");

            var thermometer = Object.FindFirstObjectByType<Thermometer>();
            thermometer.StartMeasuring();
            Transform dock = thermometer.transform.Find("SampleAnchor");
            Teleport(beaker.Body, dock.position, Quaternion.identity);
            yield return new WaitForSeconds(1f);
            Assert.IsTrue(thermometer.HasSample, "The thermometer measures the sample in the beaker.");
            float before = beaker.GetComponent<TemperatureSample>().TemperatureCelsius;
            Assert.AreEqual(before, thermometer.CurrentReading, 0.2f);

            // On the heating surface the sample warms up.
            var station = Object.FindFirstObjectByType<CombinedHeatingStation>();
            Transform plate = station.HotPlate.transform.Find("SampleAnchor");
            Teleport(beaker.Body, plate.position, Quaternion.identity);
            station.HotPlate.SwitchOn();
            yield return new WaitForSeconds(4f);
            Assert.Greater(beaker.GetComponent<TemperatureSample>().TemperatureCelsius, before + 2f, "A container on the heating surface is heated.");
            station.HotPlate.SwitchOff();

            // Back on its shelf.
            Teleport(beaker.Body, shelfPosition, shelfRotation);
            yield return new WaitForSeconds(1f);
            Assert.AreEqual(cabinet.TotalCount, cabinet.StoredCount, "The object is back in the cabinet.");

            cabinet.CloseDoors();
            yield return new WaitForSeconds(1.5f);
            Assert.IsFalse(cabinet.IsOpen, "Both doors are closed again.");
            Assert.AreEqual(cabinet.TotalCount, cabinet.StoredCount, "Closing the doors does not push anything out.");
        }

        private static void Teleport(Rigidbody body, Vector3 position, Quaternion rotation)
        {
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.transform.SetPositionAndRotation(position, rotation);
            body.position = position;
            body.rotation = rotation;
            Physics.SyncTransforms();
        }
    }
}
