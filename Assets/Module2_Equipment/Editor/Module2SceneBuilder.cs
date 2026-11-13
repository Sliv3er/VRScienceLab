using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace VRScienceLab.Module2.EditorTools
{
    /// <summary>
    /// Builds the complete Module 2 laboratory scene from code: materials, generated meshes and
    /// textures, the laboratory architecture and furniture, the four instruments, the sample
    /// system, the assistant robot with its NavMesh, the scientist avatar, the XR rig and the
    /// world-space interfaces.
    ///
    /// The class is split in partial files, one per area:
    ///   Module2SceneBuilder.cs              entry points, shared building blocks (this file)
    ///   Module2SceneBuilder.Materials.cs    material palette
    ///   Module2SceneBuilder.Environment.cs  room, furniture, lighting, NavMesh
    ///   Module2SceneBuilder.Instruments.cs  digital scale, hot plate, stopwatch, thermometer
    ///   Module2SceneBuilder.Samples.cs      sample prefabs, tray, weights, SampleManager
    ///   Module2SceneBuilder.Robot.cs        laboratory assistant robot and its stations
    ///   Module2SceneBuilder.Interface.cs    monitors and UI panels
    ///   Module2SceneBuilder.Player.cs       XR rig, scientist avatar, input helpers
    ///
    /// Menu: VR Science Lab > Module 2 > Build Lab Scene.
    /// Command line: Unity.exe -batchmode -projectPath &lt;path&gt;
    ///               -executeMethod VRScienceLab.Module2.EditorTools.Module2SceneBuilder.BuildFromCommandLine -quit
    /// </summary>
    public static partial class Module2SceneBuilder
    {
        public const string ModuleRoot = "Assets/Module2_Equipment";
        public const string ScenePath = ModuleRoot + "/Scenes/Module2_Lab.unity";
        private const string PrefabFolder = ModuleRoot + "/Prefabs";
        private const string TestObjectPrefabFolder = PrefabFolder + "/TestObjects";
        private const string SamplePrefabFolder = PrefabFolder + "/Samples";
        private const string MaterialFolder = ModuleRoot + "/Materials";

        private const float BenchTopHeight = 0.9f;
        private const float InstrumentRowZ = 0.55f;

        private static readonly Color DisplayTextColor = new Color(0.55f, 1f, 0.95f);
        private static readonly Color LabelTextColor = new Color(0.14f, 0.17f, 0.20f);
        private static readonly Color LightLabelColor = new Color(0.90f, 0.95f, 0.98f);

        /// <summary>Everything later build steps need to reference.</summary>
        private class BuildContext
        {
            public Palette Palette;
            public Transform Environment;
            public DigitalScale Scale;
            public HotPlate HotPlate;
            public StopwatchTimer Stopwatch;
            public Thermometer Thermometer;
            public RotatableMount StopwatchMount;
            public Transform ScaleAnchor, HotPlateAnchor, ThermometerAnchor;
            public SampleManager SampleManager;
            public LabAssistantRobot Robot;
            public LabDirectory Directory;
            public LabResetController ResetController;
            public MeasurementLog MeasurementLog;
            public WelcomePanel WelcomePanel;
            public ScientistAvatar Avatar;
            public GameObject XRRig;
        }

        private struct DisplayParts
        {
            public TextMeshPro Value;
            public TextMeshPro State;
            public Renderer Backlight;
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
            ProceduralMeshFactory.BeginBuild();

            var context = new BuildContext { Palette = CreateMaterials() };

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            ConfigureLighting();

            context.Environment = new GameObject("Environment").transform;
            BuildEnvironment(context);
            BakeNavMesh(context);

            Transform instrumentsParent = new GameObject("Instruments").transform;
            BuildInstruments(context, instrumentsParent);

            Transform systems = new GameObject("Lab Systems").transform;
            BuildSamples(context, systems);
            BuildRobot(context);
            BuildLabSystems(context, systems);

            BuildXRSetup(context);
            BuildAvatar(context);
            BuildInterface(context);
            BuildInputHelpers(context, systems);
            BakeReflections();

            if (!EditorSceneManager.SaveScene(scene, ScenePath))
            {
                Debug.LogError($"[Module2SceneBuilder] Could not save the scene to {ScenePath}.");
                return;
            }

            AddSceneToBuildSettings();
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
                Debug.LogWarning("[Module 2 validation] TextMeshPro essential resources are missing. Run VR Science Lab > Module 2 > Set Up Project, or Window > TextMeshPro > Import TMP Essential Resources.");
                ok = false;
            }

            if (FindStarterAssetsRigPrefab() == null)
            {
                Debug.LogWarning("[Module 2 validation] Starter Assets sample not found. Run VR Science Lab > Module 2 > Set Up Project, or Package Manager > XR Interaction Toolkit > Samples > Starter Assets > Import. Without it the scene gets a camera-only rig with no controllers.");
                ok = false;
            }

            if (FindSimulatorPrefab() == null)
            {
                Debug.LogWarning("[Module 2 validation] No simulator sample found. Run VR Science Lab > Module 2 > Set Up Project, or Package Manager > XR Interaction Toolkit > Samples > XR Device Simulator > Import.");
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

            // Try to import them automatically, then check again.
            ProjectBootstrap.ImportTextMeshProEssentials();
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
            EnsureFolder(SamplePrefabFolder);
            EnsureFolder(MaterialFolder);
        }

        internal static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            string folderName = Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, folderName);
        }

        private static void AddSceneToBuildSettings()
        {
            EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
            for (int i = 0; i < scenes.Length; i++)
            {
                if (scenes[i].path == ScenePath) return;
            }

            var updated = new EditorBuildSettingsScene[scenes.Length + 1];
            scenes.CopyTo(updated, 0);
            updated[scenes.Length] = new EditorBuildSettingsScene(ScenePath, true);
            EditorBuildSettings.scenes = updated;
        }

        // ------------------------------------------------------------------
        // Building blocks: objects
        // ------------------------------------------------------------------

        private static GameObject CreateRoot(string name, Transform parent, Vector3 position)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.position = position;
            root.transform.rotation = Quaternion.identity;
            return root;
        }

        private static GameObject CreateChild(string name, Transform parent, Vector3 localPosition)
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent, false);
            child.transform.localPosition = localPosition;
            return child;
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

        /// <summary>Axis-aligned architectural box given by its centre and size (plain cube).</summary>
        private static GameObject Box(string name, Transform parent, Vector3 center, Vector3 size, Material material, bool collider = true)
        {
            return CreatePrimitive(PrimitiveType.Cube, name, parent, center, size, material, collider);
        }

        /// <summary>Object that renders a generated mesh. <paramref name="localPosition"/> is the mesh origin (usually its base centre).</summary>
        private static GameObject MeshObject(string name, Transform parent, Mesh mesh, Vector3 localPosition, Material material)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
            return go;
        }

        /// <summary>
        /// Bevelled box with its base centre at <paramref name="basePosition"/>; optionally gets a
        /// BoxCollider that matches its size.
        /// </summary>
        private static GameObject Bevel(string name, Transform parent, Vector3 basePosition, Vector3 size, float cornerRadius, float bevel, Material material, bool collider = false)
        {
            Mesh mesh = ProceduralMeshFactory.BevelBox(size.x, size.y, size.z, cornerRadius, bevel);
            GameObject go = MeshObject(name, parent, mesh, basePosition, material);
            if (collider)
            {
                var box = go.AddComponent<BoxCollider>();
                box.center = new Vector3(0f, size.y * 0.5f, 0f);
                box.size = size;
            }

            return go;
        }

        /// <summary>Lathed object (axis = local Y, origin at the profile's y = 0).</summary>
        private static GameObject Lathe(string name, Transform parent, string meshName, Vector2[] profile, Vector3 localPosition, Material material, int segments = 32)
        {
            return MeshObject(name, parent, ProceduralMeshFactory.Lathe(meshName, profile, segments), localPosition, material);
        }

        /// <summary>Simple solid cylinder profile for <see cref="Lathe"/> with a small edge bevel.</summary>
        private static Vector2[] CylinderProfile(float radius, float height, float bevel = 0.001f)
        {
            bevel = Mathf.Min(bevel, Mathf.Min(radius, height * 0.5f) * 0.9f);
            return new[]
            {
                new Vector2(0f, 0f),
                new Vector2(radius - bevel, 0f),
                new Vector2(radius, bevel),
                new Vector2(radius, height - bevel),
                new Vector2(radius - bevel, height),
                new Vector2(0f, height),
            };
        }

        /// <summary>Flat ring profile (outer and inner radius) for markers and bezels.</summary>
        private static Vector2[] RingProfile(float outerRadius, float innerRadius, float height)
        {
            return new[]
            {
                new Vector2(innerRadius, 0f),
                new Vector2(outerRadius, 0f),
                new Vector2(outerRadius, height),
                new Vector2(innerRadius, height),
                new Vector2(innerRadius, 0f),
            };
        }

        private static void NoShadows(GameObject go)
        {
            foreach (Renderer renderer in go.GetComponentsInChildren<Renderer>())
            {
                renderer.shadowCastingMode = ShadowCastingMode.Off;
            }
        }

        private static void MarkStatic(Transform root)
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                child.gameObject.isStatic = true;
            }
        }

        // ------------------------------------------------------------------
        // Building blocks: 3D text and instrument parts
        // ------------------------------------------------------------------

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

        /// <summary>
        /// Instrument display: dark bezel, backlit glass panel, value text and state text.
        /// The child names (Display / Panel / ValueText / StateText) are part of the module's
        /// contract: LabDirectory finds the texts with Transform.Find("Display/ValueText").
        /// </summary>
        private static DisplayParts CreateDisplay(Transform parent, Vector3 localPosition, float tiltDegrees, Vector2 panelSize, Palette palette, string initialValue)
        {
            var display = new GameObject("Display");
            display.transform.SetParent(parent, false);
            display.transform.localPosition = localPosition;
            display.transform.localRotation = Quaternion.Euler(tiltDegrees, 0f, 0f);

            // Bezel: bevelled frame slightly larger than the glass, lying behind it.
            GameObject bezel = MeshObject("Bezel", display.transform,
                ProceduralMeshFactory.BevelBox(panelSize.x + 0.016f, 0.012f, panelSize.y + 0.016f, 0.006f, 0.002f),
                new Vector3(0f, 0f, 0.001f), palette.DarkPlastic);
            bezel.transform.localRotation = Quaternion.Euler(90f, 0f, 0f); // thickness goes behind the glass (+Z)

            GameObject panel = CreatePrimitive(PrimitiveType.Cube, "Panel", display.transform, new Vector3(0f, 0f, 0.0005f), new Vector3(panelSize.x, panelSize.y, 0.002f), palette.DisplayGlass, false);

            // Text sits just in front of the panel face (local -Z) and faces the user.
            TextMeshPro value = CreateText("ValueText", display.transform, new Vector3(0f, panelSize.y * 0.13f, -0.0015f), Vector3.zero,
                initialValue, panelSize.y * 6.6f, DisplayTextColor, new Vector2(panelSize.x * 0.92f, panelSize.y * 0.58f), FontStyles.Bold);
            TextMeshPro state = CreateText("StateText", display.transform, new Vector3(0f, -panelSize.y * 0.33f, -0.0015f), Vector3.zero,
                "OFF", panelSize.y * 2.4f, DisplayTextColor, new Vector2(panelSize.x * 0.92f, panelSize.y * 0.28f));

            return new DisplayParts { Value = value, State = state, Backlight = panel.GetComponent<Renderer>() };
        }

        private static Renderer CreateLed(Transform parent, Vector3 localPosition, Palette palette)
        {
            Lathe("LedBezel", parent, "LedBezel", RingProfile(0.009f, 0.0055f, 0.003f), localPosition - new Vector3(0f, 0.004f, 0f), palette.Steel, 20);
            GameObject led = CreatePrimitive(PrimitiveType.Sphere, "StatusLed", parent, localPosition, Vector3.one * 0.012f, palette.Led, false);
            return led.GetComponent<Renderer>();
        }

        /// <summary>
        /// Push button: steel collar, coloured cap that moves when pressed, label on the cap.
        /// The XRSimpleInteractable makes it selectable by ray and direct interactors.
        /// </summary>
        private static InstrumentButton CreateButton(string name, string label, Transform parent, Vector3 localPosition, Material material, Palette palette, float diameter = 0.044f)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.localPosition = localPosition;

            float radius = diameter * 0.5f;
            Lathe("Collar", root.transform, "ButtonCollar_" + Mathf.RoundToInt(diameter * 1000f), RingProfile(radius + 0.004f, radius - 0.001f, 0.004f), Vector3.zero, palette.Steel, 28);

            GameObject cap = Lathe("Cap", root.transform, "ButtonCap_" + Mathf.RoundToInt(diameter * 1000f), CylinderProfile(radius, 0.011f, 0.0025f), new Vector3(0f, 0.001f, 0f), material, 28);
            var box = cap.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 0.006f, 0f);
            box.size = new Vector3(diameter, 0.014f, diameter);

            root.AddComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable>();
            var button = root.AddComponent<InstrumentButton>();
            SetString(button, "buttonLabel", label);
            SetReference(button, "pressVisual", cap.transform);
            SetReference(button, "highlightRenderer", cap.GetComponent<Renderer>());
            root.AddComponent<HoverHighlight>();

            // Label printed on the cap, readable from the user's side.
            CreateText("Label", cap.transform, new Vector3(0f, 0.0122f, 0f), new Vector3(90f, 0f, 0f), label, 0.085f, Color.white, new Vector2(diameter * 0.86f, diameter * 0.4f), FontStyles.Bold);
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

        private static void CreateNameplate(Transform parent, string text, Vector3 localPosition, float width, Palette palette)
        {
            GameObject plate = Box("NameplateBacking", parent, localPosition + new Vector3(0f, 0f, 0.0006f), new Vector3(width, 0.018f, 0.001f), palette.Steel, false);
            plate.name = "NameplateBacking";
            CreateText("Nameplate", parent, localPosition, Vector3.zero, text, 0.12f, LabelTextColor, new Vector2(width * 0.94f, 0.016f), FontStyles.Bold);
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

        private static void SetReferences(Object target, string propertyName, params Object[] values)
        {
            var serializedObject = new SerializedObject(target);
            SerializedProperty property = FindProperty(serializedObject, target, propertyName);
            if (property == null) return;
            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
            {
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            }

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

        private static void SetInt(Object target, string propertyName, int value)
        {
            var serializedObject = new SerializedObject(target);
            SerializedProperty property = FindProperty(serializedObject, target, propertyName);
            if (property == null) return;
            property.intValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetBool(Object target, string propertyName, bool value)
        {
            var serializedObject = new SerializedObject(target);
            SerializedProperty property = FindProperty(serializedObject, target, propertyName);
            if (property == null) return;
            property.boolValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetVector3(Object target, string propertyName, Vector3 value)
        {
            var serializedObject = new SerializedObject(target);
            SerializedProperty property = FindProperty(serializedObject, target, propertyName);
            if (property == null) return;
            property.vector3Value = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetIntArray(Object target, string propertyName, params int[] values)
        {
            var serializedObject = new SerializedObject(target);
            SerializedProperty property = FindProperty(serializedObject, target, propertyName);
            if (property == null) return;
            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
            {
                property.GetArrayElementAtIndex(i).intValue = values[i];
            }

            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
