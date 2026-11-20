// HeadlessSim: attributes, Debug, Time, Application, yield instructions and small engine types.
// Not part of the Unity project.
using System;
using System.Collections;
using System.Collections.Generic;
using HeadlessSim;

namespace UnityEngine
{
    // ---------------------------------------------------------------- Attributes
    [AttributeUsage(AttributeTargets.Field, Inherited = true)] public sealed class SerializeField : Attribute { }
    [AttributeUsage(AttributeTargets.Field, Inherited = true)] public sealed class SerializeReference : Attribute { }
    [AttributeUsage(AttributeTargets.Field, Inherited = true)] public sealed class HideInInspector : Attribute { }
    public abstract class PropertyAttribute : Attribute { public int order { get; set; } }
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = true)] public class HeaderAttribute : PropertyAttribute { public readonly string header; public HeaderAttribute(string header) { this.header = header; } }
    [AttributeUsage(AttributeTargets.Field)] public class TooltipAttribute : PropertyAttribute { public readonly string tooltip; public TooltipAttribute(string tooltip) { this.tooltip = tooltip; } }
    [AttributeUsage(AttributeTargets.Field)] public sealed class RangeAttribute : PropertyAttribute { public readonly float min, max; public RangeAttribute(float min, float max) { this.min = min; this.max = max; } }
    [AttributeUsage(AttributeTargets.Field)] public sealed class MinAttribute : PropertyAttribute { public readonly float min; public MinAttribute(float min) { this.min = min; } }
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = true)] public class SpaceAttribute : PropertyAttribute { public readonly float height; public SpaceAttribute() { height = 8f; } public SpaceAttribute(float height) { this.height = height; } }
    [AttributeUsage(AttributeTargets.Field)] public sealed class TextAreaAttribute : PropertyAttribute { public TextAreaAttribute() { } public TextAreaAttribute(int minLines, int maxLines) { } }
    [AttributeUsage(AttributeTargets.Field)] public sealed class MultilineAttribute : PropertyAttribute { public MultilineAttribute() { } public MultilineAttribute(int lines) { } }
    [AttributeUsage(AttributeTargets.Field)] public sealed class ColorUsageAttribute : PropertyAttribute
    {
        public readonly bool showAlpha, hdr;
        public ColorUsageAttribute(bool showAlpha) { this.showAlpha = showAlpha; }
        public ColorUsageAttribute(bool showAlpha, bool hdr) { this.showAlpha = showAlpha; this.hdr = hdr; }
    }
    [AttributeUsage(AttributeTargets.Field)] public sealed class DelayedAttribute : PropertyAttribute { }
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public sealed class RequireComponent : Attribute
    {
        public readonly Type[] Types;
        public Type m_Type0, m_Type1, m_Type2;
        public RequireComponent(Type requiredComponent) { Types = new[] { requiredComponent }; m_Type0 = requiredComponent; }
        public RequireComponent(Type a, Type b) { Types = new[] { a, b }; m_Type0 = a; m_Type1 = b; }
        public RequireComponent(Type a, Type b, Type c) { Types = new[] { a, b, c }; m_Type0 = a; m_Type1 = b; m_Type2 = c; }
    }
    [AttributeUsage(AttributeTargets.Class)] public sealed class AddComponentMenu : Attribute { public readonly string componentMenu; public AddComponentMenu(string menuName) { componentMenu = menuName; } public AddComponentMenu(string menuName, int order) { componentMenu = menuName; } }
    [AttributeUsage(AttributeTargets.Class)] public sealed class DisallowMultipleComponent : Attribute { }
    [AttributeUsage(AttributeTargets.Class)] public sealed class ExecuteAlways : Attribute { }
    [AttributeUsage(AttributeTargets.Class)] public sealed class ExecuteInEditMode : Attribute { }
    [AttributeUsage(AttributeTargets.Class)] public sealed class SelectionBaseAttribute : Attribute { }
    [AttributeUsage(AttributeTargets.Class)] public sealed class DefaultExecutionOrder : Attribute { public int order { get; private set; } public DefaultExecutionOrder(int order) { this.order = order; } }
    [AttributeUsage(AttributeTargets.Class)] public sealed class HelpURLAttribute : Attribute { public HelpURLAttribute(string url) { } }
    [AttributeUsage(AttributeTargets.Class)] public sealed class CreateAssetMenuAttribute : Attribute { public string menuName { get; set; } public string fileName { get; set; } public int order { get; set; } }
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)] public sealed class ContextMenu : Attribute { public ContextMenu(string itemName) { } }
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Enum | AttributeTargets.Field)] public sealed class InspectorNameAttribute : Attribute { public InspectorNameAttribute(string displayName) { } }
    public enum RuntimeInitializeLoadType { AfterSceneLoad, BeforeSceneLoad, AfterAssembliesLoaded, BeforeSplashScreen, SubsystemRegistration }
    [AttributeUsage(AttributeTargets.Method)]
    public class RuntimeInitializeOnLoadMethodAttribute : Attribute
    {
        public RuntimeInitializeLoadType loadType { get; private set; }
        public RuntimeInitializeOnLoadMethodAttribute() { loadType = RuntimeInitializeLoadType.AfterSceneLoad; }
        public RuntimeInitializeOnLoadMethodAttribute(RuntimeInitializeLoadType loadType) { this.loadType = loadType; }
    }

    namespace Serialization
    {
        [AttributeUsage(AttributeTargets.Field, AllowMultiple = true)]
        public class FormerlySerializedAsAttribute : Attribute { public string oldName { get; } public FormerlySerializedAsAttribute(string oldName) { this.oldName = oldName; } }
    }

    // ---------------------------------------------------------------- Debug / Application / Time
    public enum LogType { Error, Assert, Warning, Log, Exception }

    public static class Debug
    {
        public static bool isDebugBuild => true;
        private static string Text(object message) => message == null ? "Null" : message.ToString();
        public static void Log(object message) { LogCapture.Add(LogKind.Log, Text(message)); }
        public static void Log(object message, Object context) { LogCapture.Add(LogKind.Log, Text(message)); }
        public static void LogFormat(string format, params object[] args) { LogCapture.Add(LogKind.Log, string.Format(format, args)); }
        public static void LogFormat(Object context, string format, params object[] args) { LogCapture.Add(LogKind.Log, string.Format(format, args)); }
        public static void LogWarning(object message) { LogCapture.Add(LogKind.Warning, Text(message)); }
        public static void LogWarning(object message, Object context) { LogCapture.Add(LogKind.Warning, Text(message)); }
        public static void LogWarningFormat(string format, params object[] args) { LogCapture.Add(LogKind.Warning, string.Format(format, args)); }
        public static void LogWarningFormat(Object context, string format, params object[] args) { LogCapture.Add(LogKind.Warning, string.Format(format, args)); }
        public static void LogError(object message) { LogCapture.Add(LogKind.Error, Text(message)); }
        public static void LogError(object message, Object context) { LogCapture.Add(LogKind.Error, Text(message)); }
        public static void LogErrorFormat(string format, params object[] args) { LogCapture.Add(LogKind.Error, string.Format(format, args)); }
        public static void LogErrorFormat(Object context, string format, params object[] args) { LogCapture.Add(LogKind.Error, string.Format(format, args)); }
        public static void LogException(Exception exception) { LogCapture.Add(LogKind.Exception, exception.ToString()); Simulation.Exceptions.Add(exception.ToString()); }
        public static void LogException(Exception exception, Object context) { LogException(exception); }
        public static void LogAssertion(object message) { LogCapture.Add(LogKind.Error, Text(message)); }
        public static void Assert(bool condition) { if (!condition) LogCapture.Add(LogKind.Error, "Assertion failed"); }
        public static void Assert(bool condition, object message) { if (!condition) LogCapture.Add(LogKind.Error, "Assertion failed: " + Text(message)); }
        public static void Assert(bool condition, string message) { if (!condition) LogCapture.Add(LogKind.Error, "Assertion failed: " + message); }
        public static void Assert(bool condition, object message, Object context) { Assert(condition, message); }
        public static void DrawLine(Vector3 start, Vector3 end) { }
        public static void DrawLine(Vector3 start, Vector3 end, Color color) { }
        public static void DrawLine(Vector3 start, Vector3 end, Color color, float duration) { }
        public static void DrawRay(Vector3 start, Vector3 dir) { }
        public static void DrawRay(Vector3 start, Vector3 dir, Color color) { }
        public static void DrawRay(Vector3 start, Vector3 dir, Color color, float duration) { }
        public static void Break() { }
    }

    public enum RuntimePlatform { OSXEditor = 0, OSXPlayer = 1, WindowsPlayer = 2, WindowsEditor = 7, IPhonePlayer = 8, Android = 11, LinuxPlayer = 13, LinuxEditor = 16, WebGLPlayer = 17 }

    public static class Application
    {
        public static bool isPlaying { get; internal set; }
        public static bool isBatchMode = true;
        public static bool isEditor => true;
        public static bool isFocused => true;
        public static bool isMobilePlatform => false;
        public static RuntimePlatform platform => RuntimePlatform.WindowsEditor;
        public static string dataPath => System.IO.Path.Combine(Environment.CurrentDirectory, "Assets").Replace('\\', '/');
        public static string persistentDataPath => Environment.CurrentDirectory.Replace('\\', '/');
        public static string streamingAssetsPath => dataPath + "/StreamingAssets";
        public static string temporaryCachePath => System.IO.Path.GetTempPath();
        public static string unityVersion => "6000.3.0f1-headless-sim";
        public static string version => "0.0";
        public static string productName => "HeadlessSim";
        public static int targetFrameRate { get; set; } = -1;
        public static bool runInBackground { get; set; } = true;
        public static void Quit() { }
        public static void Quit(int exitCode) { }
        public static void OpenURL(string url) { }
    }

    public static class Time
    {
        public static float time { get; internal set; }
        public static double timeAsDouble => time;
        public static float deltaTime { get; internal set; } = 1f / 60f;
        public static float unscaledDeltaTime { get; internal set; } = 1f / 60f;
        public static float unscaledTime { get; internal set; }
        public static float fixedDeltaTime { get; set; } = Simulation.FixedDeltaTime;
        public static float fixedUnscaledDeltaTime => fixedDeltaTime;
        public static float fixedTime { get; internal set; }
        public static float smoothDeltaTime => deltaTime;
        public static float timeSinceLevelLoad => time;
        public static float realtimeSinceStartup => unscaledTime;
        public static double realtimeSinceStartupAsDouble => unscaledTime;
        public static int frameCount { get; internal set; }
        public static float timeScale { get; set; } = 1f;
        public static bool inFixedTimeStep { get; internal set; }
        public static float maximumDeltaTime { get; set; } = 0.3333f;

        internal static void Advance(float unscaled, float scaled)
        {
            unscaledDeltaTime = unscaled;
            deltaTime = scaled;
            time += scaled;
            unscaledTime += unscaled;
            frameCount++;
        }

        internal static void Reset()
        {
            time = 0f; unscaledTime = 0f; fixedTime = 0f; frameCount = 0; timeScale = 1f;
            deltaTime = 1f / 60f; unscaledDeltaTime = 1f / 60f; inFixedTimeStep = false;
        }
    }

    public static class Resources
    {
        private static readonly Dictionary<string, Object> Registered = new Dictionary<string, Object>();
        /// <summary>Simulator hook: makes Resources.Load return <paramref name="asset"/> for <paramref name="path"/>.</summary>
        public static void Register(string path, Object asset) { Registered[path] = asset; }

        public static T Load<T>(string path) where T : Object => Load(path, typeof(T)) as T;
        public static Object Load(string path) => Load(path, typeof(Object));
        public static Object Load(string path, Type type)
        {
            if (Registered.TryGetValue(path, out Object asset) && type.IsInstanceOfType(asset)) return asset;
            if (type == typeof(TMPro.TMP_Settings) && path == "TMP Settings")
            {
                // The simulator behaves like a project with the TextMeshPro essentials imported.
                var settings = ScriptableObject.CreateInstance<TMPro.TMP_Settings>();
                settings.name = "TMP Settings";
                Registered[path] = settings;
                return settings;
            }
            return null;
        }
        public static T[] LoadAll<T>(string path) where T : Object => new T[0];
        public static AsyncOperation UnloadUnusedAssets() => new AsyncOperation();
        public static void UnloadAsset(Object asset) { }
        public static T GetBuiltinResource<T>(string path) where T : Object => null;
    }

    public class AsyncOperation : YieldInstruction
    {
        public bool isDone => true;
        public float progress => 1f;
        public bool allowSceneActivation { get; set; } = true;
        public event Action<AsyncOperation> completed { add { value?.Invoke(this); } remove { } }
    }

    // ---------------------------------------------------------------- Yield instructions
    public class YieldInstruction { }
    public sealed class WaitForSeconds : YieldInstruction { internal readonly float seconds; public WaitForSeconds(float seconds) { this.seconds = seconds; } }
    public sealed class WaitForFixedUpdate : YieldInstruction { }
    public sealed class WaitForEndOfFrame : YieldInstruction { }
    public sealed class Coroutine : YieldInstruction { internal bool done; }
    public abstract class CustomYieldInstruction : IEnumerator
    {
        public abstract bool keepWaiting { get; }
        public object Current => null;
        public bool MoveNext() => keepWaiting;
        public virtual void Reset() { }
    }
    public class WaitForSecondsRealtime : CustomYieldInstruction
    {
        public float waitTime { get; set; }
        private float start = -1f;
        public WaitForSecondsRealtime(float time) { waitTime = time; }
        public override bool keepWaiting { get { if (start < 0f) start = Time.unscaledTime; return Time.unscaledTime - start < waitTime; } }
    }
    public sealed class WaitUntil : CustomYieldInstruction { private readonly Func<bool> predicate; public WaitUntil(Func<bool> predicate) { this.predicate = predicate; } public override bool keepWaiting => !predicate(); }
    public sealed class WaitWhile : CustomYieldInstruction { private readonly Func<bool> predicate; public WaitWhile(Func<bool> predicate) { this.predicate = predicate; } public override bool keepWaiting => predicate(); }

    // ---------------------------------------------------------------- Small types
    [Serializable]
    public struct LayerMask
    {
        [SerializeField] private int m_Bits;
        public int value { get => m_Bits; set => m_Bits = value; }
        public static implicit operator int(LayerMask mask) => mask.m_Bits;
        public static implicit operator LayerMask(int value) => new LayerMask { m_Bits = value };
        private static readonly string[] Names = { "Default", "TransparentFX", "Ignore Raycast", "", "Water", "UI" };
        public static int NameToLayer(string layerName) { int i = Array.IndexOf(Names, layerName); return i; }
        public static string LayerToName(int layer) => layer >= 0 && layer < Names.Length ? Names[layer] : "";
        public static int GetMask(params string[] layerNames)
        {
            int mask = 0;
            foreach (string layerName in layerNames) { int layer = NameToLayer(layerName); if (layer >= 0) mask |= 1 << layer; }
            return mask;
        }
    }

    public static class Gizmos
    {
        public static Color color { get; set; }
        public static Matrix4x4 matrix { get; set; }
        public static void DrawLine(Vector3 from, Vector3 to) { }
        public static void DrawRay(Vector3 from, Vector3 direction) { }
        public static void DrawSphere(Vector3 center, float radius) { }
        public static void DrawWireSphere(Vector3 center, float radius) { }
        public static void DrawCube(Vector3 center, Vector3 size) { }
        public static void DrawWireCube(Vector3 center, Vector3 size) { }
        public static void DrawMesh(Mesh mesh) { }
        public static void DrawIcon(Vector3 center, string name) { }
    }

    public static class Screen
    {
        public static int width => 1920;
        public static int height => 1080;
        public static float dpi => 96f;
        public static bool fullScreen { get; set; }
    }

    public static class SystemInfo
    {
        public static string deviceName => "HeadlessSim";
        public static string operatingSystem => Environment.OSVersion.ToString();
        public static int processorCount => Environment.ProcessorCount;
        public static bool supportsInstancing => true;
    }

    public static class PlayerPrefs
    {
        private static readonly Dictionary<string, object> Values = new Dictionary<string, object>();
        public static void SetInt(string key, int value) { Values[key] = value; }
        public static int GetInt(string key, int defaultValue = 0) => Values.TryGetValue(key, out object v) && v is int i ? i : defaultValue;
        public static void SetFloat(string key, float value) { Values[key] = value; }
        public static float GetFloat(string key, float defaultValue = 0f) => Values.TryGetValue(key, out object v) && v is float f ? f : defaultValue;
        public static void SetString(string key, string value) { Values[key] = value; }
        public static string GetString(string key, string defaultValue = "") => Values.TryGetValue(key, out object v) && v is string s ? s : defaultValue;
        public static bool HasKey(string key) => Values.ContainsKey(key);
        public static void DeleteKey(string key) { Values.Remove(key); }
        public static void DeleteAll() { Values.Clear(); }
        public static void Save() { }
    }

    public static class JsonUtility
    {
        public static string ToJson(object obj) => "{}";
        public static string ToJson(object obj, bool prettyPrint) => "{}";
        public static T FromJson<T>(string json) => default(T);
    }

    namespace XR
    {
        public static class XRSettings
        {
            public static bool isDeviceActive { get; set; }
            public static bool enabled { get; set; }
            public static string loadedDeviceName => "";
        }
    }

    namespace SceneManagement
    {
        public enum LoadSceneMode { Single, Additive }

        public struct Scene
        {
            internal string scenePath;
            internal bool valid;
            public string path => scenePath ?? "";
            public string name => string.IsNullOrEmpty(scenePath) ? "Untitled" : System.IO.Path.GetFileNameWithoutExtension(scenePath);
            public bool isLoaded => valid;
            public bool isDirty => false;
            public int buildIndex => SceneUtility.GetBuildIndexByScenePath(path);
            public int rootCount => Simulation.RootObjects.Count;
            public bool IsValid() => valid;
            public GameObject[] GetRootGameObjects() { var list = new List<GameObject>(Simulation.RootObjects); return list.ToArray(); }
            public static bool operator ==(Scene a, Scene b) => a.path == b.path;
            public static bool operator !=(Scene a, Scene b) => a.path != b.path;
            public override bool Equals(object obj) => obj is Scene s && s.path == path;
            public override int GetHashCode() => path.GetHashCode();
        }

        public static class SceneManager
        {
            internal static string activeScenePath = "";
            /// <summary>Scenes saved by the editor stand-in: path -> detached copy of the root objects.</summary>
            internal static readonly Dictionary<string, List<GameObject>> SavedScenes = new Dictionary<string, List<GameObject>>();
            internal static readonly List<string> BuildScenes = new List<string>();

            public static event Action<Scene, LoadSceneMode> sceneLoaded;
            public static int sceneCount => 1;
            public static int sceneCountInBuildSettings => BuildScenes.Count;

            public static Scene GetActiveScene() => new Scene { scenePath = activeScenePath, valid = true };
            public static Scene GetSceneAt(int index) => GetActiveScene();
            public static Scene GetSceneByPath(string path) => new Scene { scenePath = path, valid = path == activeScenePath };
            public static Scene GetSceneByName(string name) => new Scene { scenePath = activeScenePath, valid = GetActiveScene().name == name };
            public static bool SetActiveScene(Scene scene) => true;

            public static void LoadScene(string sceneNameOrPath) { LoadScene(sceneNameOrPath, LoadSceneMode.Single); }
            public static void LoadScene(int buildIndex) { LoadScene(BuildScenes[buildIndex], LoadSceneMode.Single); }
            public static void LoadScene(int buildIndex, LoadSceneMode mode) { LoadScene(BuildScenes[buildIndex], mode); }

            public static void LoadScene(string sceneNameOrPath, LoadSceneMode mode)
            {
                string path = ResolvePath(sceneNameOrPath);
                if (path == null)
                {
                    Debug.LogError($"Scene '{sceneNameOrPath}' couldn't be loaded because it has not been saved by the simulator (build it first).");
                    return;
                }

                if (mode == LoadSceneMode.Single) Simulation.ClearScene(true);
                InstantiateSavedScene(path);
                activeScenePath = path;
                if (Application.isPlaying) Simulation.ActivateScene();
                sceneLoaded?.Invoke(GetActiveScene(), mode);
            }

            public static AsyncOperation LoadSceneAsync(string sceneNameOrPath) { LoadScene(sceneNameOrPath); return new AsyncOperation(); }
            public static AsyncOperation LoadSceneAsync(string sceneNameOrPath, LoadSceneMode mode) { LoadScene(sceneNameOrPath, mode); return new AsyncOperation(); }
            public static AsyncOperation UnloadSceneAsync(string sceneName) => new AsyncOperation();

            internal static string ResolvePath(string nameOrPath)
            {
                if (SavedScenes.ContainsKey(nameOrPath)) return nameOrPath;
                foreach (string path in SavedScenes.Keys)
                {
                    if (System.IO.Path.GetFileNameWithoutExtension(path) == nameOrPath) return path;
                }
                return null;
            }

            internal static void SaveCurrentScene(string path)
            {
                var map = new Dictionary<Object, Object>();
                var roots = new List<GameObject>(Simulation.RootObjects);
                List<GameObject> copies = Cloner.CloneHierarchies(roots, map);
                foreach (GameObject copy in copies) copy.isAssetRoot = true;
                SavedScenes[path] = copies;
                activeScenePath = path;
            }

            private static void InstantiateSavedScene(string path)
            {
                var map = new Dictionary<Object, Object>();
                List<GameObject> copies = Cloner.CloneHierarchies(SavedScenes[path], map);
                foreach (GameObject copy in copies)
                {
                    copy.isAssetRoot = false;
                    Simulation.RegisterRoot(copy);
                }
            }
        }

        public static class SceneUtility
        {
            public static int GetBuildIndexByScenePath(string scenePath) => SceneManager.BuildScenes.IndexOf(scenePath);
            public static string GetScenePathByBuildIndex(int buildIndex) => buildIndex >= 0 && buildIndex < SceneManager.BuildScenes.Count ? SceneManager.BuildScenes[buildIndex] : "";
        }
    }
}
