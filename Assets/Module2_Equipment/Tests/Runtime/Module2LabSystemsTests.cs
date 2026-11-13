using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace VRScienceLab.Module2.Tests
{
    /// <summary>
    /// PlayMode tests for the laboratory systems added around the instruments: sample
    /// instantiation / destruction, Transform manipulation, GameObject search, physics and
    /// collisions, AI navigation, reset and measurement log. The last tests load the generated
    /// lab scene and check the whole setup end to end.
    /// </summary>
    public class Module2LabSystemsTests
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

        // Inactive template that stands in for a sample prefab.
        private LabSample CreateSampleTemplate(string displayName, float massKilograms)
        {
            GameObject go = NewObject(displayName + " Template");
            go.SetActive(false);
            go.AddComponent<BoxCollider>().size = Vector3.one * 0.05f;
            var body = go.AddComponent<Rigidbody>();
            body.mass = massKilograms;
            body.useGravity = false;
            var sample = go.AddComponent<LabSample>();
            SetField(sample, "displayName", displayName);
            return sample;
        }

        private SampleManager CreateManager(int slots, int maximum, params int[] initial)
        {
            GameObject managerObject = NewObject("Sample Manager");
            managerObject.SetActive(false);
            var manager = managerObject.AddComponent<SampleManager>();

            var points = new Transform[slots];
            for (int i = 0; i < slots; i++)
            {
                points[i] = NewObject("Slot " + i).transform;
                points[i].position = new Vector3(i * 0.3f, 5f, 0f);
            }

            LabSample[] templates = { CreateSampleTemplate("Beaker", 0.2f), CreateSampleTemplate("Box", 0.15f) };
            SetField(manager, "samplePrefabs", templates);
            SetField(manager, "spawnPoints", points);
            SetField(manager, "initialSamples", initial);
            SetField(manager, "maximumSamples", maximum);
            return manager;
        }

        // Instantiating an inactive template gives an inactive clone; the tests activate what the manager created.
        private static void Activate(SampleManager manager)
        {
            for (int i = 0; i < manager.SampleCount; i++) manager.GetSample(i).gameObject.SetActive(true);
        }

        // ---------------------------------------------------------------- Instantiate / Destroy

        [UnityTest]
        public IEnumerator SampleManager_InstantiatesInitialSamplesAndSelectsTheFirst()
        {
            SampleManager manager = CreateManager(4, 8, 0, 1);
            manager.gameObject.SetActive(true);
            yield return null;

            Assert.AreEqual(2, manager.SampleCount);
            Assert.AreEqual(2, manager.TotalCreated);
            Assert.IsNotNull(manager.Selected);
            Assert.AreEqual("Beaker 1", manager.Selected.DisplayName);
            Assert.AreEqual("Box 1", manager.GetSample(1).DisplayName);
        }

        [UnityTest]
        public IEnumerator SampleManager_CreatePlacesSampleOnFirstFreeSlot()
        {
            SampleManager manager = CreateManager(3, 8, 0);
            manager.gameObject.SetActive(true);
            yield return null;

            LabSample second = manager.CreateSample(0);
            Assert.IsNotNull(second);
            Assert.AreEqual(0.3f, second.transform.position.x, 0.001f, "The second sample must use the second slot.");
            Assert.AreSame(second, manager.Selected, "A new sample becomes the selected one.");
        }

        [UnityTest]
        public IEnumerator SampleManager_RefusesToExceedLimitOrFillOccupiedTray()
        {
            SampleManager manager = CreateManager(2, 5, 0);
            manager.gameObject.SetActive(true);
            yield return null;

            Assert.IsNotNull(manager.CreateSample(1));
            Assert.IsNull(manager.CreateSample(1), "Both slots are occupied.");
            Assert.AreEqual(2, manager.SampleCount);
            StringAssert.Contains("full", manager.LastMessage);
            Assert.IsNull(manager.CreateSample(9), "Unknown prefab index.");
        }

        [UnityTest]
        public IEnumerator SampleManager_RemoveSelectedDestroysTheGameObject()
        {
            SampleManager manager = CreateManager(4, 8, 0, 1);
            manager.gameObject.SetActive(true);
            yield return null;

            GameObject doomed = manager.Selected.gameObject;
            manager.RemoveSelected();
            yield return null; // Destroy() takes effect at the end of the frame.

            Assert.IsTrue(doomed == null, "The GameObject must have been destroyed.");
            Assert.AreEqual(1, manager.SampleCount);
            Assert.AreEqual(1, manager.TotalDestroyed);
            Assert.AreEqual("Box 1", manager.Selected.DisplayName, "The next sample becomes selected.");
        }

        [UnityTest]
        public IEnumerator SampleManager_ResetDestroysEverythingAndRecreatesTheInitialSet()
        {
            SampleManager manager = CreateManager(4, 8, 0);
            manager.gameObject.SetActive(true);
            yield return null;

            manager.CreateSample(1);
            manager.CreateSample(1);
            GameObject extra = manager.GetSample(2).gameObject;
            manager.ResetSamples();
            yield return null;

            Assert.IsTrue(extra == null);
            Assert.AreEqual(1, manager.SampleCount);
            Assert.AreEqual("Beaker 1", manager.GetSample(0).DisplayName, "Numbering restarts after a reset.");
            Assert.AreEqual(3, manager.TotalDestroyed);
        }

        // ---------------------------------------------------------------- Transform operations

        [UnityTest]
        public IEnumerator SampleManager_ScaleChangesLocalScaleAndMass()
        {
            SampleManager manager = CreateManager(4, 8, 0);
            manager.gameObject.SetActive(true);
            yield return null;
            Activate(manager);

            LabSample sample = manager.Selected;
            float massBefore = sample.MassGrams;
            manager.ScaleSelected(0.5f);

            Assert.AreEqual(1.5f, sample.ScaleFactor, 0.0001f);
            Assert.AreEqual(1.5f, sample.transform.localScale.x, 0.0001f);
            Assert.AreEqual(massBefore * 1.5f * 1.5f * 1.5f, sample.MassGrams, 0.01f, "Mass follows the volume.");

            manager.ScaleSelected(5f);
            Assert.AreEqual(1.5f, sample.ScaleFactor, 0.0001f, "Scale is clamped.");
        }

        [UnityTest]
        public IEnumerator SampleManager_MoveSelectedToStationChangesPosition()
        {
            SampleManager manager = CreateManager(4, 8, 0);
            Transform anchor = NewObject("Scale Anchor").transform;
            anchor.position = new Vector3(-2f, 7f, 3f);
            SetField(manager, "stationAnchors", new[] { new SampleManager.StationAnchor { label = "Scale pan", anchor = anchor } });
            manager.gameObject.SetActive(true);
            yield return null;
            Activate(manager);

            manager.MoveSelectedToStation(0);
            Assert.Less(Vector3.Distance(anchor.position, manager.Selected.transform.position), 0.001f);

            manager.ReturnSelectedToTray();
            Assert.AreEqual(5f, manager.Selected.transform.position.y, 0.001f, "Back on a tray slot.");
        }

        [UnityTest]
        public IEnumerator RotatableMount_TurnsInStepsAndClamps()
        {
            var mount = NewObject("Mount").AddComponent<RotatableMount>();
            yield return null;

            mount.RotateRight();
            mount.RotateRight();
            Assert.AreEqual(30f, mount.TargetAngle, 0.001f);

            yield return new WaitForSeconds(0.5f);
            Assert.AreEqual(30f, mount.CurrentAngle, 0.5f);
            Assert.AreEqual(30f, Quaternion.Angle(Quaternion.identity, mount.transform.localRotation), 0.5f);

            for (int i = 0; i < 10; i++) mount.RotateLeft();
            Assert.AreEqual(-60f, mount.TargetAngle, 0.001f, "Clamped to the maximum angle.");
        }

        [UnityTest]
        public IEnumerator TransformAnimator_RotatesContinuously()
        {
            var animator = NewObject("Fan").AddComponent<TransformAnimator>();
            SetField(animator, "rotationSpeed", new Vector3(0f, 90f, 0f));
            yield return new WaitForSeconds(0.3f);

            Assert.Greater(Quaternion.Angle(Quaternion.identity, animator.transform.localRotation), 10f);
        }

        // ---------------------------------------------------------------- Searching

        [UnityTest]
        public IEnumerator LabDirectory_FindsInstrumentByNameAndReadsItsDisplay()
        {
            GameObject instrument = NewObject("TestHotPlate_ForDirectory");
            var plate = instrument.AddComponent<HotPlate>();
            GameObject display = NewObject("Display");
            display.transform.SetParent(instrument.transform, false);
            GameObject value = NewObject("ValueText");
            value.transform.SetParent(display.transform, false);
            var text = value.AddComponent<TextMeshPro>();
            SetField(plate, "valueDisplay", text);

            GameObject beacon = NewObject("Beacon");
            var directory = NewObject("Directory").AddComponent<LabDirectory>();
            SetField(directory, "instrumentObjectNames", new[] { "TestHotPlate_ForDirectory", "DoesNotExist_ForDirectory" });
            SetField(directory, "locatorBeacon", beacon.transform);
            instrument.transform.position = new Vector3(4f, 1f, -2f);
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("DoesNotExist_ForDirectory"));
            yield return null;
            yield return null;

            Assert.IsTrue(directory.TryGetInstrument("TestHotPlate_ForDirectory", out HotPlate found));
            Assert.AreSame(plate, found);
            Assert.IsTrue(directory.TryGetInstrument("TestHotPlate_ForDirectory", out LabDirectory.InstrumentEntry entry));
            Assert.AreSame(text, entry.ValueDisplay, "Transform.Find must locate Display/ValueText.");
            Assert.IsFalse(directory.TryGetInstrument("DoesNotExist_ForDirectory", out HotPlate _));

            int searches = directory.SceneSearchCount;
            directory.LocateInstrument(0);
            Assert.AreEqual(searches, directory.SceneSearchCount, "Locating uses the cache, not a new scene search.");
            StringAssert.Contains("HotPlate", directory.LastReport);
            Assert.IsTrue(beacon.activeSelf);
            Assert.AreEqual(4f, beacon.transform.position.x, 0.001f, "The beacon is moved above the found object.");
        }

        // ---------------------------------------------------------------- Physics and collisions

        [UnityTest]
        public IEnumerator Physics_DroppedObjectFallsCollidesAndIsWeighed()
        {
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            created.Add(floor);
            floor.name = "Bench";
            floor.transform.position = new Vector3(50f, -0.5f, 50f);
            floor.transform.localScale = new Vector3(4f, 1f, 4f);

            // Scale with a trigger zone just above the floor.
            GameObject zoneObject = NewObject("Zone");
            zoneObject.transform.position = new Vector3(50f, 0.15f, 50f);
            var trigger = zoneObject.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.size = new Vector3(0.4f, 0.3f, 0.4f);
            var zoneBody = zoneObject.AddComponent<Rigidbody>();
            zoneBody.isKinematic = true;
            var zone = zoneObject.AddComponent<MeasurementZone>();
            var scale = NewObject("Scale").AddComponent<DigitalScale>();
            SetField(scale, "weighingZone", zone);

            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            created.Add(box);
            box.name = "Dropped Box";
            box.transform.position = new Vector3(50f, 0.8f, 50f);
            box.transform.localScale = Vector3.one * 0.1f;
            var body = box.AddComponent<Rigidbody>();
            body.mass = 0.25f;
            var physicsObject = box.AddComponent<LabPhysicsObject>();

            int impacts = 0;
            string hitName = null;
            System.Action<LabPhysicsObject.Impact> handler = impact =>
            {
                impacts++;
                hitName = impact.OtherName;
            };
            LabPhysicsObject.AnyImpact += handler;

            yield return null;
            scale.PowerOn();
            yield return new WaitForSeconds(1.5f);
            LabPhysicsObject.AnyImpact -= handler;

            Assert.Less(box.transform.position.y, 0.2f, "Gravity must have pulled the box down.");
            Assert.Greater(box.transform.position.y, 0f, "The collider must have stopped it on the bench.");
            Assert.GreaterOrEqual(physicsObject.ImpactCount, 1, "OnCollisionEnter must have been reported.");
            Assert.GreaterOrEqual(impacts, 1);
            Assert.AreEqual("Bench", hitName);
            Assert.AreEqual(250f, scale.DisplayedMassGrams, 0.5f, "The trigger zone detected the box and the scale reads its mass.");

            // Leaving the zone is detected too.
            body.position = new Vector3(55f, 0.1f, 50f);
            yield return new WaitForSeconds(0.3f);
            Assert.AreEqual(0f, scale.DisplayedMassGrams, 0.5f);
        }

        // ---------------------------------------------------------------- AI navigation

        [UnityTest]
        public IEnumerator Robot_DrivesToStationsAndCanBeStoppedAndResumed()
        {
            // Small NavMesh baked at runtime: a floor with a wall in the middle.
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            created.Add(ground);
            ground.transform.position = new Vector3(-60f, -0.5f, -60f);
            ground.transform.localScale = new Vector3(12f, 1f, 12f);
            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            created.Add(wall);
            wall.transform.SetParent(ground.transform, true);
            wall.transform.position = new Vector3(-60f, 1f, -60f);
            wall.transform.localScale = new Vector3(0.4f, 2f, 5f);

            var surface = ground.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.BuildNavMesh();

            var stationA = NewObject("Station A").AddComponent<RobotStation>();
            stationA.transform.position = new Vector3(-63f, 0f, -60f);
            var stationB = NewObject("Station B").AddComponent<RobotStation>();
            stationB.transform.position = new Vector3(-57f, 0f, -60f);
            SetField(stationA, "stationName", "A");
            SetField(stationB, "stationName", "B");

            GameObject robotObject = NewObject("Robot");
            robotObject.SetActive(false);
            robotObject.transform.position = stationA.transform.position;
            var agent = robotObject.AddComponent<NavMeshAgent>();
            agent.radius = 0.3f;
            agent.speed = 3f;
            agent.acceleration = 12f;
            agent.stoppingDistance = 0.05f;
            var robot = robotObject.AddComponent<LabAssistantRobot>();
            SetField(robot, "stations", new[] { stationA, stationB });
            robotObject.SetActive(true);
            yield return null;

            Assert.IsTrue(agent.isOnNavMesh, "The robot must start on the NavMesh.");
            Assert.AreEqual(RobotState.Idle, robot.State);

            robot.GoToStation(1);
            Assert.AreEqual(RobotState.Moving, robot.State);
            Assert.AreEqual("B", robot.DestinationName);
            yield return new WaitForSeconds(0.6f);

            robot.Stop();
            Assert.AreEqual(RobotState.Paused, robot.State);
            yield return new WaitForSeconds(0.5f);
            Vector3 pausedAt = robotObject.transform.position;
            yield return new WaitForSeconds(0.3f);
            Assert.Less(Vector3.Distance(pausedAt, robotObject.transform.position), 0.05f, "A stopped robot stays where it is.");
            Assert.Greater(Vector3.Distance(robotObject.transform.position, stationB.transform.position), 0.5f);

            robot.Resume();
            float timeout = 12f;
            while (robot.State != RobotState.Arrived && timeout > 0f)
            {
                timeout -= Time.deltaTime;
                yield return null;
            }

            Assert.AreEqual(RobotState.Arrived, robot.State, "The robot must reach station B around the wall.");
            Vector3 offset = robotObject.transform.position - stationB.transform.position;
            offset.y = 0f;
            Assert.Less(offset.magnitude, 0.3f);

            robot.GoToNextStation();
            Assert.AreEqual("A", robot.DestinationName, "The destination changes on request.");
        }

        // ---------------------------------------------------------------- Log and reset

        [UnityTest]
        public IEnumerator MeasurementLog_RecordsInstrumentReadingsAndResetClearsThem()
        {
            var plate = NewObject("Plate").AddComponent<HotPlate>();
            var stopwatch = NewObject("Watch").AddComponent<StopwatchTimer>();
            var log = NewObject("Log").AddComponent<MeasurementLog>();
            SetField(log, "hotPlate", plate);
            SetField(log, "stopwatch", stopwatch);
            var reset = NewObject("Reset").AddComponent<LabResetController>();
            SetField(reset, "hotPlate", plate);
            SetField(reset, "stopwatch", stopwatch);
            SetField(reset, "measurementLog", log);
            yield return null;

            plate.SwitchOn();
            plate.AdvanceTime(3f);
            stopwatch.StartTimer();
            stopwatch.AdvanceTime(12.5f);
            log.RecordMeasurement();

            Assert.AreEqual(1, log.Count);
            MeasurementLog.Entry entry = log.GetEntry(0);
            Assert.AreEqual(plate.CurrentTemperature, entry.PlateTemperatureCelsius, 0.001f);
            Assert.AreEqual(12.5, entry.ElapsedSeconds, 0.1);
            Assert.IsFalse(entry.HasMass, "No scale is connected to this log.");

            reset.ResetLab();
            Assert.AreEqual(0, log.Count);
            Assert.AreEqual(HotPlateState.Off, plate.State);
            Assert.AreEqual(plate.InitialTemperature, plate.CurrentTemperature, 0.001f);
            Assert.AreEqual(0.0, stopwatch.ElapsedSeconds, 0.0001);
            Assert.AreEqual(1, reset.ResetCount);
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

        [UnityTest]
        public IEnumerator LabScene_ContainsEveryInstrumentSystemAndInterface()
        {
            yield return LoadLabScene();

            foreach (string instrumentName in new[] { "DigitalScale", "HotPlate", "Stopwatch", "Thermometer" })
            {
                GameObject instrument = GameObject.Find(instrumentName);
                Assert.IsNotNull(instrument, instrumentName + " is missing.");
                var behaviour = instrument.GetComponent<InstrumentBase>();
                Assert.IsNotNull(behaviour);
                Assert.IsTrue(behaviour.HasValueDisplay, instrumentName + " has no display.");
                Assert.IsNotNull(instrument.GetComponent<InstrumentStateVisuals>());
            }

            Assert.IsNotNull(Object.FindFirstObjectByType<SampleManager>());
            Assert.IsNotNull(Object.FindFirstObjectByType<LabAssistantRobot>());
            Assert.IsNotNull(Object.FindFirstObjectByType<LabDirectory>());
            Assert.IsNotNull(Object.FindFirstObjectByType<WelcomePanel>());
            Assert.IsNotNull(Object.FindFirstObjectByType<ScientistAvatar>());
            Assert.IsNotNull(Object.FindFirstObjectByType<NavMeshSurface>());
            Assert.GreaterOrEqual(Object.FindObjectsByType<UnityEngine.UI.Button>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length, 25);

            yield return new WaitForSeconds(0.5f);
            Assert.AreEqual(3, Object.FindFirstObjectByType<SampleManager>().SampleCount, "Three samples are instantiated at start.");
            Assert.AreEqual(3, Object.FindObjectsByType<LabSample>(FindObjectsSortMode.None).Length);
        }

        [UnityTest]
        public IEnumerator LabScene_SampleMovedToScaleIsWeighedAndThermometerReadsIt()
        {
            yield return LoadLabScene();
            yield return new WaitForSeconds(1f); // let the samples settle on the tray

            var manager = Object.FindFirstObjectByType<SampleManager>();
            var scale = Object.FindFirstObjectByType<DigitalScale>();
            var thermometer = Object.FindFirstObjectByType<Thermometer>();

            scale.PowerOn();
            Assert.AreEqual(0f, scale.DisplayedMassGrams, 0.5f, "Empty pan.");

            manager.MoveSelectedToStation(0);
            yield return new WaitForSeconds(1f);
            Assert.AreEqual(manager.Selected.MassGrams, scale.DisplayedMassGrams, 1f, "The scale reads the sample placed on the pan.");

            thermometer.StartMeasuring();
            manager.MoveSelectedToStation(2);
            yield return new WaitForSeconds(1f);
            Assert.AreEqual(0f, scale.DisplayedMassGrams, 0.5f, "The sample left the pan.");
            Assert.IsTrue(thermometer.HasSample, "The sample is on the thermometer dock.");
            Assert.AreEqual("READING", thermometer.CurrentStateName);
        }

        [UnityTest]
        public IEnumerator LabScene_RobotReachesTheMainWorkbenchOnTheBakedNavMesh()
        {
            yield return LoadLabScene();

            var robot = Object.FindFirstObjectByType<LabAssistantRobot>();
            Assert.IsTrue(robot.Agent.isOnNavMesh, "The robot must stand on the baked NavMesh.");

            Time.timeScale = 6f;
            robot.GoToStation(1);
            float timeout = 60f;
            while (robot.State == RobotState.Moving && timeout > 0f)
            {
                timeout -= Time.deltaTime;
                yield return null;
            }

            Time.timeScale = 1f;
            Assert.AreEqual(RobotState.Arrived, robot.State);
            Assert.AreEqual("Main Workbench", robot.DestinationName);
        }
    }
}
