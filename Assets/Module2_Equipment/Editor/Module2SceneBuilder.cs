using System.IO;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace VRScienceLab.Module2.EditorTools
{
    /// <summary>
    /// Builds the complete Module 2 laboratory scene from code:
    /// materials, the four instrument prefabs, test objects, environment, lighting,
    /// the XR rig (Starter Assets "XR Origin (XR Rig)" when imported), the XR Interaction
    /// Manager, the EventSystem and the keyboard/mouse debug helper.
    ///
    /// Menu: VR Science Lab > Module 2 > Build Lab Scene.
    /// Command line: Unity.exe -batchmode -projectPath &lt;path&gt;
    ///               -executeMethod VRScienceLab.Module2.EditorTools.Module2SceneBuilder.BuildFromCommandLine -quit
    /// </summary>
    public static class Module2SceneBuilder
    {
        public const string ModuleRoot = "Assets/Module2_Equipment";
        public const string ScenePath = ModuleRoot + "/Scenes/Module2_Lab.unity";
        private const string PrefabFolder = ModuleRoot + "/Prefabs";
        private const string TestObjectPrefabFolder = PrefabFolder + "/TestObjects";
        private const string MaterialFolder = ModuleRoot + "/Materials";

        private const float BenchTopHeight = 0.9f;
        private const float InstrumentRowZ = 0.55f;
        private const float TestObjectRowZ = 0.85f;
        private const float DisplayTiltDegrees = 30f;

        private static readonly Color DisplayTextColor = new Color(0.45f, 1f, 0.55f);
        private static readonly Color LabelTextColor = new Color(0.12f, 0.12f, 0.14f);
        private static readonly Color ButtonLabelColor = new Color(0.1f, 0.1f, 0.12f);

        /// <summary>Materials created by the builder.</summary>
        private class Palette
        {
            public Material Floor, Wall, Bench, Body, DisplayPanel, Led, Pan, HeatingElement, Probe, Dock, Weight, Beaker;
            public Material ButtonRed, ButtonGreen, ButtonBlue, ButtonGray;
        }

        /// <summary>Instrument components of the scene, used to wire the debug helper.</summary>
        private class BuiltInstruments
        {
            public DigitalScale Scale;
            public HotPlate HotPlate;
            public StopwatchTimer Stopwatch;
            public Thermometer Thermometer;
        }

        private struct DisplayParts
        {
            public TextMeshPro Value;
            public TextMeshPro State;
        }

        // ------------------------------------------------------------------
        // Menu entries
        // ------------------------------------------------------------------

        [MenuItem("VR Science Lab/Module 2/Build Lab Scene", false, 0)]
        public static void BuildLabScene()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                Debug.Log("[Module2SceneBuilder] Build cancelled by the user.");
                return;
            }

            if (!CheckTextMeshProResources()) return;

            EnsureFolders();
            Palette palette = CreateMaterials();

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            ConfigureLighting();

            Transform environment = new GameObject("Environment").transform;
            BuildEnvironment(palette, environment);

            Transform instrumentsParent = new GameObject("Instruments").transform;
            BuiltInstruments instruments = BuildInstruments(palette, instrumentsParent);

            Transform testObjectsParent = new GameObject("Test Objects").transform;
            BuildTestObjects(palette, testObjectsParent);

            BuildXRSetup();
            BuildDebugInput(instruments);

            if (!EditorSceneManager.SaveScene(scene, ScenePath))
            {
                Debug.LogError($"[Module2SceneBuilder] Could not save the scene to {ScenePath}.");
                return;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[Module2SceneBuilder] Module 2 lab scene built and saved to {ScenePath}.");
        }

        /// <summary>Entry point for -executeMethod (batch mode).</summary>
        public static void BuildFromCommandLine()
        {
            BuildLabScene();
        }

        [MenuItem("VR Science Lab/Module 2/Open Lab Scene", false, 1)]
        public static void OpenLabScene()
        {
            if (!File.Exists(ScenePath))
            {
                EditorUtility.DisplayDialog("Module 2", "The scene does not exist yet. Run VR Science Lab > Module 2 > Build Lab Scene first.", "OK");
                return;
            }

            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }
        }

        [MenuItem("VR Science Lab/Module 2/Validate Project Setup", false, 20)]
        public static void ValidateProjectSetup()
        {
            bool ok = true;

            if (Resources.Load<TMP_Settings>("TMP Settings") == null)
            {
                Debug.LogWarning("[Module 2 validation] TextMeshPro essential resources are missing. Window > TextMeshPro > Import TMP Essential Resources.");
                ok = false;
            }

            if (FindStarterAssetsRigPrefab() == null)
            {
                Debug.LogWarning("[Module 2 validation] Starter Assets sample not found. Package Manager > XR Interaction Toolkit > Samples > Starter Assets > Import. Without it the scene gets a camera-only rig with no controllers.");
                ok = false;
            }

            if (FindPrefabByName("XR Interaction Simulator") == null && FindPrefabByName("XR Device Simulator") == null)
            {
                Debug.LogWarning("[Module 2 validation] No simulator sample found. Package Manager > XR Interaction Toolkit > Samples > XR Device Simulator > Import, then enable 'Use XR Interaction Simulator in scenes' in Project Settings > XR Plug-in Management > XR Interaction Toolkit.");
                ok = false;
            }

#if !ENABLE_INPUT_SYSTEM
            Debug.LogWarning("[Module 2 validation] The Input System backend is not active. Project Settings > Player > Active Input Handling = Input System Package (New) or Both.");
            ok = false;
#endif

            Debug.Log(ok
                ? "[Module 2 validation] Project setup looks complete."
                : "[Module 2 validation] Some setup steps are missing (see warnings above). See Assets/Module2_Equipment/Documentation/README.md.");
        }

        // ------------------------------------------------------------------
        // Preconditions and assets
        // ------------------------------------------------------------------

        private static bool CheckTextMeshProResources()
        {
            if (Resources.Load<TMP_Settings>("TMP Settings") != null) return true;

            const string message = "TextMeshPro essential resources are not imported. Use Window > TextMeshPro > Import TMP Essential Resources, then run the builder again.";
            if (Application.isBatchMode) Debug.LogError("[Module2SceneBuilder] " + message);
            else EditorUtility.DisplayDialog("TextMeshPro resources missing", message, "OK");
            return false;
        }

        private static void EnsureFolders()
        {
            EnsureFolder(ModuleRoot);
            EnsureFolder(ModuleRoot + "/Scenes");
            EnsureFolder(PrefabFolder);
            EnsureFolder(TestObjectPrefabFolder);
            EnsureFolder(MaterialFolder);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            string folderName = Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, folderName);
        }

        private static bool UsesScriptableRenderPipeline => GraphicsSettings.currentRenderPipeline != null;

        private static Shader LitShader
        {
            get
            {
                Shader shader = UsesScriptableRenderPipeline ? Shader.Find("Universal Render Pipeline/Lit") : null;
                return shader != null ? shader : Shader.Find("Standard");
            }
        }

        private static Shader UnlitShader
        {
            get
            {
                Shader shader = UsesScriptableRenderPipeline ? Shader.Find("Universal Render Pipeline/Unlit") : null;
                return shader != null ? shader : Shader.Find("Unlit/Color");
            }
        }

        private static Palette CreateMaterials()
        {
            var palette = new Palette
            {
                Floor = CreateMaterial("Lab_Floor", new Color(0.78f, 0.80f, 0.82f), 0.2f),
                Wall = CreateMaterial("Lab_Wall", new Color(0.90f, 0.92f, 0.94f), 0.1f),
                Bench = CreateMaterial("Lab_Bench", new Color(0.24f, 0.26f, 0.29f), 0.5f),
                Body = CreateMaterial("Instrument_Body", new Color(0.86f, 0.87f, 0.89f), 0.45f),
                DisplayPanel = CreateMaterial("Instrument_DisplayPanel", new Color(0.03f, 0.06f, 0.05f), 0f, 0f, true),
                Led = CreateMaterial("Instrument_Led", new Color(0.2f, 0.2f, 0.2f), 0f, 0f, true),
                Pan = CreateMaterial("Scale_Pan", new Color(0.75f, 0.76f, 0.78f), 0.7f, 0.8f),
                HeatingElement = CreateMaterial("HotPlate_Element", new Color(0.12f, 0.12f, 0.13f), 0.3f, 0.2f, false, Color.black),
                Probe = CreateMaterial("Thermometer_Probe", new Color(0.80f, 0.80f, 0.83f), 0.7f, 0.9f),
                Dock = CreateMaterial("Thermometer_Dock", new Color(0.30f, 0.30f, 0.33f), 0.4f),
                Weight = CreateMaterial("TestObject_Weight", new Color(0.72f, 0.58f, 0.30f), 0.6f, 0.7f),
                Beaker = CreateMaterial("TestObject_Beaker", new Color(0.60f, 0.80f, 0.95f), 0.8f),
                ButtonRed = CreateMaterial("Button_Red", new Color(0.85f, 0.15f, 0.12f), 0.5f),
                ButtonGreen = CreateMaterial("Button_Green", new Color(0.15f, 0.70f, 0.22f), 0.5f),
                ButtonBlue = CreateMaterial("Button_Blue", new Color(0.20f, 0.45f, 0.90f), 0.5f),
                ButtonGray = CreateMaterial("Button_Gray", new Color(0.55f, 0.55f, 0.58f), 0.5f),
            };
            return palette;
        }

        private static Material CreateMaterial(string name, Color color, float smoothness = 0.4f, float metallic = 0f, bool unlit = false, Color? emission = null)
        {
            string path = $"{MaterialFolder}/{name}.mat";
            Shader shader = unlit ? UnlitShader : LitShader;
            if (shader == null)
            {
                Debug.LogError($"[Module2SceneBuilder] Could not find a shader for material '{name}'.");
                shader = Shader.Find("Standard");
            }

            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            else
            {
                material.shader = shader;
            }

            material.color = color;
            if (!unlit)
            {
                if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", smoothness);
                if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
                if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", metallic);
            }

            if (emission.HasValue)
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", emission.Value);
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }

            EditorUtility.SetDirty(material);
            return material;
        }

        // ------------------------------------------------------------------
        // Environment
        // ------------------------------------------------------------------

        private static void ConfigureLighting()
        {
            var sun = new GameObject("Directional Light");
            sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            Light light = sun.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.97f, 0.92f);
            light.intensity = 1.0f;
            light.shadows = LightShadows.Soft;

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.42f, 0.44f, 0.48f);
        }

        private static void BuildEnvironment(Palette palette, Transform parent)
        {
            CreatePrimitive(PrimitiveType.Plane, "Floor", parent, Vector3.zero, new Vector3(1.5f, 1f, 1.5f), palette.Floor);
            CreatePrimitive(PrimitiveType.Cube, "Back Wall", parent, new Vector3(0f, 1.5f, 1.35f), new Vector3(8f, 3f, 0.1f), palette.Wall);
            CreatePrimitive(PrimitiveType.Cube, "Workbench", parent, new Vector3(0f, BenchTopHeight * 0.5f, 0.6f), new Vector3(2.8f, BenchTopHeight, 0.8f), palette.Bench);

            CreateText("Title Sign", parent, new Vector3(0f, 2.15f, 1.29f), Vector3.zero,
                "VR SCIENCE LAB  |  MODULE 2: SCIENTIFIC EQUIPMENT", 1.4f, LabelTextColor, new Vector2(6f, 0.3f), FontStyles.Bold);
            CreateText("Instructions Sign", parent, new Vector3(0f, 1.85f, 1.29f), Vector3.zero,
                "Point a controller ray at a button and press Grip (select) to operate an instrument.\nGrab the weights and beakers with Grip and place them on the scale pan or the thermometer dock.",
                0.6f, LabelTextColor, new Vector2(6f, 0.5f));
        }

        // ------------------------------------------------------------------
        // Instruments
        // ------------------------------------------------------------------

        private static BuiltInstruments BuildInstruments(Palette palette, Transform parent)
        {
            return new BuiltInstruments
            {
                Scale = BuildDigitalScale(palette, parent, new Vector3(-0.95f, BenchTopHeight, InstrumentRowZ)),
                HotPlate = BuildHotPlate(palette, parent, new Vector3(-0.32f, BenchTopHeight, InstrumentRowZ)),
                Stopwatch = BuildStopwatch(palette, parent, new Vector3(0.32f, BenchTopHeight, InstrumentRowZ)),
                Thermometer = BuildThermometer(palette, parent, new Vector3(0.95f, BenchTopHeight, InstrumentRowZ)),
            };
        }

        private static DigitalScale BuildDigitalScale(Palette palette, Transform parent, Vector3 position)
        {
            GameObject root = CreateRoot("DigitalScale", parent, position);
            var scale = root.AddComponent<DigitalScale>();
            SetString(scale, "instrumentName", "Digital Scale");

            CreatePrimitive(PrimitiveType.Cube, "Body", root.transform, new Vector3(0f, 0.02f, 0f), new Vector3(0.30f, 0.04f, 0.32f), palette.Body);
            CreatePrimitive(PrimitiveType.Cylinder, "Pan", root.transform, new Vector3(0f, 0.045f, 0.06f), new Vector3(0.16f, 0.005f, 0.16f), palette.Pan);
            MeasurementZone zone = CreateZone("WeighingZone", root.transform, new Vector3(0f, 0.10f, 0.06f), new Vector3(0.16f, 0.10f, 0.16f));

            DisplayParts display = CreateDisplay(root.transform, new Vector3(-0.04f, 0.07f, -0.09f), new Vector2(0.15f, 0.06f), palette, "OFF");
            Renderer led = CreateLed(root.transform, new Vector3(-0.125f, 0.045f, -0.12f), palette);
            InstrumentButton power = CreateButton("PowerButton", "POWER", root.transform, new Vector3(0.07f, 0.04f, -0.12f), palette.ButtonRed);
            InstrumentButton tare = CreateButton("TareButton", "TARE", root.transform, new Vector3(0.125f, 0.04f, -0.12f), palette.ButtonBlue);
            CreateNameplate(root.transform, "DIGITAL SCALE", new Vector3(0f, 0.02f, -0.161f), 0.28f);

            SetReference(scale, "valueDisplay", display.Value);
            SetReference(scale, "stateDisplay", display.State);
            SetReference(scale, "statusIndicator", led);
            SetReference(scale, "weighingZone", zone);

            Connect(power, scale.TogglePower);
            Connect(tare, scale.Tare);

            SaveAsPrefab(root, PrefabFolder, "DigitalScale");
            return scale;
        }

        private static HotPlate BuildHotPlate(Palette palette, Transform parent, Vector3 position)
        {
            GameObject root = CreateRoot("HotPlate", parent, position);
            var hotPlate = root.AddComponent<HotPlate>();
            SetString(hotPlate, "instrumentName", "Hot Plate");

            CreatePrimitive(PrimitiveType.Cube, "Body", root.transform, new Vector3(0f, 0.04f, 0f), new Vector3(0.36f, 0.08f, 0.34f), palette.Body);
            GameObject element = CreatePrimitive(PrimitiveType.Cylinder, "HeatingElement", root.transform, new Vector3(0f, 0.084f, 0.05f), new Vector3(0.20f, 0.004f, 0.20f), palette.HeatingElement);

            var lightObject = new GameObject("HeatLight");
            lightObject.transform.SetParent(root.transform, false);
            lightObject.transform.localPosition = new Vector3(0f, 0.16f, 0.05f);
            Light heatLight = lightObject.AddComponent<Light>();
            heatLight.type = LightType.Point;
            heatLight.color = new Color(1f, 0.45f, 0.15f);
            heatLight.range = 0.6f;
            heatLight.intensity = 0f;
            heatLight.enabled = false;

            DisplayParts display = CreateDisplay(root.transform, new Vector3(-0.09f, 0.11f, -0.10f), new Vector2(0.15f, 0.06f), palette, "20.0 °C");
            Renderer led = CreateLed(root.transform, new Vector3(-0.165f, 0.09f, -0.14f), palette);
            InstrumentButton on = CreateButton("OnButton", "ON", root.transform, new Vector3(0.02f, 0.08f, -0.12f), palette.ButtonGreen);
            InstrumentButton off = CreateButton("OffButton", "OFF", root.transform, new Vector3(0.08f, 0.08f, -0.12f), palette.ButtonRed);
            InstrumentButton reset = CreateButton("ResetButton", "RESET", root.transform, new Vector3(0.14f, 0.08f, -0.12f), palette.ButtonGray);
            CreateNameplate(root.transform, "HOT PLATE", new Vector3(0f, 0.04f, -0.171f), 0.34f);

            SetReference(hotPlate, "valueDisplay", display.Value);
            SetReference(hotPlate, "stateDisplay", display.State);
            SetReference(hotPlate, "statusIndicator", led);
            SetReference(hotPlate, "heatingElementRenderer", element.GetComponent<Renderer>());
            SetReference(hotPlate, "heatingLight", heatLight);

            Connect(on, hotPlate.SwitchOn);
            Connect(off, hotPlate.SwitchOff);
            Connect(reset, hotPlate.ResetTemperature);

            SaveAsPrefab(root, PrefabFolder, "HotPlate");
            return hotPlate;
        }

        private static StopwatchTimer BuildStopwatch(Palette palette, Transform parent, Vector3 position)
        {
            GameObject root = CreateRoot("Stopwatch", parent, position);
            var stopwatch = root.AddComponent<StopwatchTimer>();
            SetString(stopwatch, "instrumentName", "Stopwatch");

            CreatePrimitive(PrimitiveType.Cube, "Body", root.transform, new Vector3(0f, 0.015f, 0f), new Vector3(0.22f, 0.03f, 0.16f), palette.Body);

            DisplayParts display = CreateDisplay(root.transform, new Vector3(0f, 0.06f, 0.03f), new Vector2(0.18f, 0.06f), palette, "00:00.00");
            Renderer led = CreateLed(root.transform, new Vector3(0.10f, 0.035f, 0.03f), palette);
            InstrumentButton start = CreateButton("StartButton", "START", root.transform, new Vector3(-0.07f, 0.03f, -0.045f), palette.ButtonGreen);
            InstrumentButton pause = CreateButton("PauseButton", "PAUSE", root.transform, new Vector3(0f, 0.03f, -0.045f), palette.ButtonBlue);
            InstrumentButton reset = CreateButton("ResetButton", "RESET", root.transform, new Vector3(0.07f, 0.03f, -0.045f), palette.ButtonGray);
            CreateNameplate(root.transform, "STOPWATCH", new Vector3(0f, 0.015f, -0.081f), 0.20f);

            SetReference(stopwatch, "valueDisplay", display.Value);
            SetReference(stopwatch, "stateDisplay", display.State);
            SetReference(stopwatch, "statusIndicator", led);

            Connect(start, stopwatch.StartTimer);
            Connect(pause, stopwatch.PauseTimer);
            Connect(reset, stopwatch.ResetTimer);

            SaveAsPrefab(root, PrefabFolder, "Stopwatch");
            return stopwatch;
        }

        private static Thermometer BuildThermometer(Palette palette, Transform parent, Vector3 position)
        {
            GameObject root = CreateRoot("Thermometer", parent, position);
            var thermometer = root.AddComponent<Thermometer>();
            SetString(thermometer, "instrumentName", "Thermometer");

            CreatePrimitive(PrimitiveType.Cube, "Body", root.transform, new Vector3(0f, 0.02f, 0f), new Vector3(0.30f, 0.04f, 0.32f), palette.Body);
            CreatePrimitive(PrimitiveType.Cylinder, "SampleDock", root.transform, new Vector3(-0.06f, 0.044f, 0.06f), new Vector3(0.12f, 0.004f, 0.12f), palette.Dock);
            MeasurementZone zone = CreateZone("SampleZone", root.transform, new Vector3(-0.06f, 0.10f, 0.06f), new Vector3(0.13f, 0.10f, 0.13f));

            // Probe stand, arm and tip are visual only (no colliders) so a beaker can be placed under the tip.
            CreatePrimitive(PrimitiveType.Cylinder, "ProbeStand", root.transform, new Vector3(0.08f, 0.14f, 0.06f), new Vector3(0.012f, 0.10f, 0.012f), palette.Probe, false);
            GameObject arm = CreatePrimitive(PrimitiveType.Cylinder, "ProbeArm", root.transform, new Vector3(0.01f, 0.235f, 0.06f), new Vector3(0.008f, 0.075f, 0.008f), palette.Probe, false);
            arm.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            CreatePrimitive(PrimitiveType.Cylinder, "ProbeTip", root.transform, new Vector3(-0.06f, 0.19f, 0.06f), new Vector3(0.006f, 0.045f, 0.006f), palette.Probe, false);

            DisplayParts display = CreateDisplay(root.transform, new Vector3(0.06f, 0.07f, -0.09f), new Vector2(0.15f, 0.06f), palette, "OFF");
            Renderer led = CreateLed(root.transform, new Vector3(0.135f, 0.045f, -0.12f), palette);
            InstrumentButton measure = CreateButton("MeasureButton", "MEASURE", root.transform, new Vector3(-0.12f, 0.04f, -0.12f), palette.ButtonGreen);
            InstrumentButton reset = CreateButton("ResetButton", "RESET", root.transform, new Vector3(-0.06f, 0.04f, -0.12f), palette.ButtonGray);
            CreateNameplate(root.transform, "THERMOMETER", new Vector3(0f, 0.02f, -0.161f), 0.28f);

            SetReference(thermometer, "valueDisplay", display.Value);
            SetReference(thermometer, "stateDisplay", display.State);
            SetReference(thermometer, "statusIndicator", led);
            SetReference(thermometer, "sampleZone", zone);

            Connect(measure, thermometer.ToggleMeasuring);
            Connect(reset, thermometer.ResetMeasurement);

            SaveAsPrefab(root, PrefabFolder, "Thermometer");
            return thermometer;
        }

        // ------------------------------------------------------------------
        // Test objects (weights for the scale, samples for the thermometer)
        // ------------------------------------------------------------------

        private static void BuildTestObjects(Palette palette, Transform parent)
        {
            CreateWeight(palette, parent, "Weight_100g", "100 g", 0.1f, 0.05f, new Vector3(-1.15f, 0f, TestObjectRowZ));
            CreateWeight(palette, parent, "Weight_250g", "250 g", 0.25f, 0.06f, new Vector3(-1.0f, 0f, TestObjectRowZ));
            CreateWeight(palette, parent, "Weight_500g", "500 g", 0.5f, 0.07f, new Vector3(-0.83f, 0f, TestObjectRowZ));

            CreateBeaker(palette, parent, "Sample_A_ColdWater", "Sample A", "Cold water", 4f, new Vector3(0.75f, 0f, TestObjectRowZ));
            CreateBeaker(palette, parent, "Sample_B_RoomWater", "Sample B", "Room temperature water", 22f, new Vector3(0.93f, 0f, TestObjectRowZ));
            CreateBeaker(palette, parent, "Sample_C_HotWater", "Sample C", "Hot water", 80f, new Vector3(1.11f, 0f, TestObjectRowZ));
        }

        private static void CreateWeight(Palette palette, Transform parent, string name, string label, float massKg, float size, Vector3 benchPosition)
        {
            Vector3 position = new Vector3(benchPosition.x, BenchTopHeight + size * 0.5f + 0.002f, benchPosition.z);
            GameObject weight = CreateGrabbable(PrimitiveType.Cube, name, parent, position, Vector3.one * size, palette.Weight, massKg);
            CreateObjectLabel(weight.transform, label, Vector3.one * size);
            SaveAsPrefab(weight, TestObjectPrefabFolder, name);
        }

        private static void CreateBeaker(Palette palette, Transform parent, string name, string label, string sampleName, float temperature, Vector3 benchPosition)
        {
            Vector3 scale = new Vector3(0.07f, 0.05f, 0.07f); // cylinder: height = 2 * y scale = 0.10 m
            Vector3 position = new Vector3(benchPosition.x, BenchTopHeight + scale.y + 0.002f, benchPosition.z);
            GameObject beaker = CreateGrabbable(PrimitiveType.Cylinder, name, parent, position, scale, palette.Beaker, 0.2f);

            var sample = beaker.AddComponent<TemperatureSample>();
            SetString(sample, "sampleName", sampleName);
            SetFloat(sample, "temperatureCelsius", temperature);

            CreateObjectLabel(beaker.transform, label, scale);
            SaveAsPrefab(beaker, TestObjectPrefabFolder, name);
        }

        private static GameObject CreateGrabbable(PrimitiveType type, string name, Transform parent, Vector3 position, Vector3 scale, Material material, float massKg)
        {
            GameObject go = CreatePrimitive(type, name, parent, position, scale, material);

            var rigidbody = go.AddComponent<Rigidbody>();
            rigidbody.mass = massKg;
            rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
            rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            var grab = go.AddComponent<XRGrabInteractable>();
            grab.useDynamicAttach = true;
            grab.throwOnDetach = true;
            return go;
        }

        // Label lying on top of a scaled primitive; the inverse scale keeps the text undistorted.
        private static void CreateObjectLabel(Transform owner, string text, Vector3 ownerScale)
        {
            TextMeshPro label = CreateText("Label", owner, new Vector3(0f, 0.51f, 0f), new Vector3(90f, 0f, 0f), text, 0.5f, LabelTextColor, new Vector2(0.9f, 0.4f), FontStyles.Bold);
            label.transform.localScale = new Vector3(1f / ownerScale.x, 1f / ownerScale.z, 1f / ownerScale.y);
            label.rectTransform.sizeDelta = new Vector2(ownerScale.x * 0.9f, ownerScale.z * 0.4f);
            label.fontSize = ownerScale.x * 0.4f * 10f;
            label.fontSizeMax = label.fontSize;
            label.fontSizeMin = label.fontSize * 0.4f;
        }

        // ------------------------------------------------------------------
        // XR rig, interaction manager, event system, debug helper
        // ------------------------------------------------------------------

        private static void BuildXRSetup()
        {
            var managerObject = new GameObject("XR Interaction Manager");
            managerObject.AddComponent<XRInteractionManager>();

            var eventSystemObject = new GameObject("EventSystem");
            eventSystemObject.AddComponent<EventSystem>();
            eventSystemObject.AddComponent<XRUIInputModule>();

            GameObject rig = null;
            GameObject rigPrefab = FindStarterAssetsRigPrefab();
            if (rigPrefab != null)
            {
                rig = PrefabUtility.InstantiatePrefab(rigPrefab) as GameObject;
                if (rig != null) rig.name = "XR Origin (XR Rig)";
            }
            else
            {
                Debug.LogWarning("[Module2SceneBuilder] Starter Assets sample not found. Creating a minimal XR Origin without controllers. Import Package Manager > XR Interaction Toolkit > Samples > Starter Assets and rebuild to get ray/grab interactors.");
                if (EditorApplication.ExecuteMenuItem("GameObject/XR/XR Origin (VR)"))
                {
                    XROrigin origin = Object.FindFirstObjectByType<XROrigin>();
                    if (origin != null) rig = origin.gameObject;
                }
            }

            // Older XRI versions auto-create an interaction manager from the GameObject menu; keep only ours.
            XRInteractionManager[] managers = Object.FindObjectsByType<XRInteractionManager>(FindObjectsSortMode.None);
            for (int i = 0; i < managers.Length; i++)
            {
                if (managers[i].gameObject != managerObject) Object.DestroyImmediate(managers[i].gameObject);
            }

            if (rig != null)
            {
                rig.transform.position = new Vector3(0f, 0f, -0.35f);
                rig.transform.rotation = Quaternion.identity;
            }
            else
            {
                Debug.LogWarning("[Module2SceneBuilder] No XR Origin could be created. Adding a plain camera so the scene still renders.");
                var cameraObject = new GameObject("Main Camera");
                cameraObject.tag = "MainCamera";
                cameraObject.transform.position = new Vector3(0f, 1.6f, -0.35f);
                cameraObject.transform.rotation = Quaternion.Euler(25f, 0f, 0f);
                cameraObject.AddComponent<Camera>();
                cameraObject.AddComponent<AudioListener>();
            }
        }

        private static GameObject FindStarterAssetsRigPrefab()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab XR Origin"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileNameWithoutExtension(path) == "XR Origin (XR Rig)" && path.Contains("Starter Assets"))
                {
                    return AssetDatabase.LoadAssetAtPath<GameObject>(path);
                }
            }

            return null;
        }

        private static GameObject FindPrefabByName(string prefabName)
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab " + prefabName))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileNameWithoutExtension(path) == prefabName)
                {
                    return AssetDatabase.LoadAssetAtPath<GameObject>(path);
                }
            }

            return null;
        }

        private static void BuildDebugInput(BuiltInstruments instruments)
        {
            var go = new GameObject("Module2 Debug Input (keyboard + mouse)");
            var debugInput = go.AddComponent<Module2DebugInput>();
            SetReference(debugInput, "digitalScale", instruments.Scale);
            SetReference(debugInput, "hotPlate", instruments.HotPlate);
            SetReference(debugInput, "stopwatch", instruments.Stopwatch);
            SetReference(debugInput, "thermometer", instruments.Thermometer);
        }

        // ------------------------------------------------------------------
        // Building blocks
        // ------------------------------------------------------------------

        private static GameObject CreateRoot(string name, Transform parent, Vector3 position)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.position = position;
            root.transform.rotation = Quaternion.identity;
            return root;
        }

        private static GameObject CreatePrimitive(PrimitiveType type, string name, Transform parent, Vector3 localPosition, Vector3 localScale, Material material, bool keepCollider = true)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = localScale;

            var renderer = go.GetComponent<Renderer>();
            if (renderer != null && material != null) renderer.sharedMaterial = material;

            Collider collider = go.GetComponent<Collider>();
            if (!keepCollider)
            {
                if (collider != null) Object.DestroyImmediate(collider);
            }
            else if (type == PrimitiveType.Cylinder && collider != null)
            {
                // A cylinder primitive gets a capsule collider; replace it with a box so objects can rest on top.
                Object.DestroyImmediate(collider);
                var box = go.AddComponent<BoxCollider>();
                box.size = new Vector3(1f, 2f, 1f);
            }

            return go;
        }

        private static TextMeshPro CreateText(string name, Transform parent, Vector3 localPosition, Vector3 localEuler, string text, float fontSize, Color color, Vector2 size, FontStyles style = FontStyles.Normal, TextAlignmentOptions alignment = TextAlignmentOptions.Center)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = Quaternion.Euler(localEuler);
            go.transform.localScale = Vector3.one;

            var tmp = go.AddComponent<TextMeshPro>();
            tmp.text = text;
            tmp.color = color;
            tmp.alignment = alignment;
            tmp.fontStyle = style;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.overflowMode = TextOverflowModes.Overflow;
            tmp.enableAutoSizing = true;
            tmp.fontSizeMax = fontSize;
            tmp.fontSizeMin = fontSize * 0.35f;
            tmp.fontSize = fontSize;
            tmp.rectTransform.sizeDelta = size;
            return tmp;
        }

        private static DisplayParts CreateDisplay(Transform parent, Vector3 localPosition, Vector2 panelSize, Palette palette, string initialValue)
        {
            var display = new GameObject("Display");
            display.transform.SetParent(parent, false);
            display.transform.localPosition = localPosition;
            display.transform.localRotation = Quaternion.Euler(DisplayTiltDegrees, 0f, 0f);

            CreatePrimitive(PrimitiveType.Cube, "Panel", display.transform, Vector3.zero, new Vector3(panelSize.x, panelSize.y, 0.01f), palette.DisplayPanel, false);

            // Text sits just in front of the panel face (local -Z) and faces the user.
            TextMeshPro value = CreateText("ValueText", display.transform, new Vector3(0f, panelSize.y * 0.14f, -0.0055f), Vector3.zero,
                initialValue, panelSize.y * 7f, DisplayTextColor, new Vector2(panelSize.x * 0.94f, panelSize.y * 0.58f), FontStyles.Bold);
            TextMeshPro state = CreateText("StateText", display.transform, new Vector3(0f, -panelSize.y * 0.33f, -0.0055f), Vector3.zero,
                "OFF", panelSize.y * 2.6f, DisplayTextColor, new Vector2(panelSize.x * 0.94f, panelSize.y * 0.3f));

            return new DisplayParts { Value = value, State = state };
        }

        private static Renderer CreateLed(Transform parent, Vector3 localPosition, Palette palette)
        {
            GameObject led = CreatePrimitive(PrimitiveType.Sphere, "StatusLed", parent, localPosition, Vector3.one * 0.014f, palette.Led, false);
            return led.GetComponent<Renderer>();
        }

        private static InstrumentButton CreateButton(string name, string label, Transform parent, Vector3 localPosition, Material material)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.localPosition = localPosition;

            // Cap: 5 cm diameter, 1.2 cm tall, sitting on the instrument surface.
            GameObject cap = CreatePrimitive(PrimitiveType.Cylinder, "Cap", root.transform, new Vector3(0f, 0.006f, 0f), new Vector3(0.05f, 0.006f, 0.05f), material);

            root.AddComponent<XRSimpleInteractable>();
            var button = root.AddComponent<InstrumentButton>();
            SetString(button, "buttonLabel", label);
            SetReference(button, "pressVisual", cap.transform);
            SetReference(button, "highlightRenderer", cap.GetComponent<Renderer>());

            // Label printed on the surface in front of the button (readable from the user's side).
            CreateText("Label", root.transform, new Vector3(0f, 0.001f, -0.036f), new Vector3(90f, 0f, 0f), label, 0.11f, ButtonLabelColor, new Vector2(0.06f, 0.014f), FontStyles.Bold);
            return button;
        }

        private static MeasurementZone CreateZone(string name, Transform parent, Vector3 localPosition, Vector3 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;

            var box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = size;

            var rigidbody = go.AddComponent<Rigidbody>();
            rigidbody.isKinematic = true;
            rigidbody.useGravity = false;

            return go.AddComponent<MeasurementZone>();
        }

        private static void CreateNameplate(Transform parent, string text, Vector3 localPosition, float width)
        {
            CreateText("Nameplate", parent, localPosition, Vector3.zero, text, 0.2f, LabelTextColor, new Vector2(width, 0.03f), FontStyles.Bold);
        }

        private static void Connect(InstrumentButton button, UnityAction action)
        {
            UnityEventTools.AddPersistentListener(button.onPressed, action);
        }

        private static void SaveAsPrefab(GameObject instance, string folder, string prefabName)
        {
            string path = $"{folder}/{prefabName}.prefab";
            PrefabUtility.SaveAsPrefabAssetAndConnect(instance, path, InteractionMode.AutomatedAction, out bool success);
            if (!success) Debug.LogError($"[Module2SceneBuilder] Could not save prefab {path}.");
        }

        // ------------------------------------------------------------------
        // Serialized field helpers (set private [SerializeField] values the same way the Inspector does)
        // ------------------------------------------------------------------

        private static SerializedProperty FindProperty(SerializedObject serializedObject, Object target, string propertyName)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property == null)
            {
                Debug.LogError($"[Module2SceneBuilder] Serialized field '{propertyName}' not found on {target.GetType().Name}.", target);
            }

            return property;
        }

        private static void SetReference(Object target, string propertyName, Object value)
        {
            var serializedObject = new SerializedObject(target);
            SerializedProperty property = FindProperty(serializedObject, target, propertyName);
            if (property == null) return;
            property.objectReferenceValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetString(Object target, string propertyName, string value)
        {
            var serializedObject = new SerializedObject(target);
            SerializedProperty property = FindProperty(serializedObject, target, propertyName);
            if (property == null) return;
            property.stringValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetFloat(Object target, string propertyName, float value)
        {
            var serializedObject = new SerializedObject(target);
            SerializedProperty property = FindProperty(serializedObject, target, propertyName);
            if (property == null) return;
            property.floatValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
