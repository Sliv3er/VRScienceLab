// ---------------------------------------------------------------------------
// Minimal stand-ins for the UnityEngine APIs used by Module 2.
// They exist only so the module's C# can be compiled and its logic executed
// outside the Unity editor (see Tools/CompileCheck/README.md). They are NOT
// part of the Unity project and are never imported by Unity.
// ---------------------------------------------------------------------------
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace UnityEngine
{
    // ----- attributes -----
    [AttributeUsage(AttributeTargets.Field)] public class SerializeField : Attribute { }
    [AttributeUsage(AttributeTargets.Field)] public class HeaderAttribute : Attribute { public HeaderAttribute(string h) { } }
    [AttributeUsage(AttributeTargets.Field)] public class TooltipAttribute : Attribute { public TooltipAttribute(string t) { } }
    [AttributeUsage(AttributeTargets.Field)] public class RangeAttribute : Attribute { public RangeAttribute(float a, float b) { } }
    [AttributeUsage(AttributeTargets.Field)] public class MinAttribute : Attribute { public MinAttribute(float a) { } }
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public class RequireComponent : Attribute
    {
        public readonly Type[] Types;
        public RequireComponent(Type a) { Types = new[] { a }; }
        public RequireComponent(Type a, Type b) { Types = new[] { a, b }; }
    }
    [AttributeUsage(AttributeTargets.Class)] public class AddComponentMenu : Attribute { public AddComponentMenu(string m) { } }
    [AttributeUsage(AttributeTargets.Class)] public class DisallowMultipleComponent : Attribute { }

    // ----- math -----
    public struct Vector2
    {
        public float x, y;
        public Vector2(float x, float y) { this.x = x; this.y = y; }
        public static Vector2 zero => new Vector2(0, 0);
        public static Vector2 operator *(Vector2 a, float d) => new Vector2(a.x * d, a.y * d);
    }

    public struct Vector3
    {
        public float x, y, z;
        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        public static Vector3 zero => new Vector3(0, 0, 0);
        public static Vector3 one => new Vector3(1, 1, 1);
        public static Vector3 operator +(Vector3 a, Vector3 b) => new Vector3(a.x + b.x, a.y + b.y, a.z + b.z);
        public static Vector3 operator -(Vector3 a, Vector3 b) => new Vector3(a.x - b.x, a.y - b.y, a.z - b.z);
        public static Vector3 operator *(Vector3 a, float d) => new Vector3(a.x * d, a.y * d, a.z * d);
        public static Vector3 operator *(float d, Vector3 a) => a * d;
        public override string ToString() => $"({x}, {y}, {z})";
    }

    public struct Quaternion
    {
        public float x, y, z, w;
        public static Quaternion identity => new Quaternion { w = 1 };
        public static Quaternion Euler(float x, float y, float z) => identity;
        public static Quaternion Euler(Vector3 e) => identity;
    }

    public struct Color
    {
        public float r, g, b, a;
        public Color(float r, float g, float b, float a = 1f) { this.r = r; this.g = g; this.b = b; this.a = a; }
        public static Color black => new Color(0, 0, 0);
        public static Color white => new Color(1, 1, 1);
        public static Color Lerp(Color a, Color b, float t) { t = Mathf.Clamp01(t); return new Color(a.r + (b.r - a.r) * t, a.g + (b.g - a.g) * t, a.b + (b.b - a.b) * t, a.a + (b.a - a.a) * t); }
    }

    public static class Mathf
    {
        public static float Min(float a, float b) => a < b ? a : b;
        public static float Max(float a, float b) => a > b ? a : b;
        public static float Abs(float a) => Math.Abs(a);
        public static float Clamp01(float v) => v < 0 ? 0 : (v > 1 ? 1 : v);
        public static float Clamp(float v, float a, float b) => v < a ? a : (v > b ? b : v);
        public static float Lerp(float a, float b, float t) => a + (b - a) * Clamp01(t);
        public static float InverseLerp(float a, float b, float v) => a != b ? Clamp01((v - a) / (b - a)) : 0f;
        public static float MoveTowards(float current, float target, float maxDelta)
        {
            if (Math.Abs(target - current) <= maxDelta) return target;
            return current + Math.Sign(target - current) * maxDelta;
        }
    }

    // ----- time / debug / app -----
    public static class Time
    {
        public static float deltaTime = 1f / 60f;
        public static float unscaledDeltaTime = 1f / 60f;
        public static float time = 0f;
    }

    public static class Debug
    {
        public static int ErrorCount;
        public static void Log(object m) => Console.WriteLine("[Log] " + m);
        public static void Log(object m, Object ctx) => Log(m);
        public static void LogWarning(object m) => Console.WriteLine("[Warning] " + m);
        public static void LogWarning(object m, Object ctx) => LogWarning(m);
        public static void LogError(object m) { ErrorCount++; Console.WriteLine("[Error] " + m); }
        public static void LogError(object m, Object ctx) => LogError(m);
    }

    public static class Application { public static bool isBatchMode = true; }
    public static class Resources { public static T Load<T>(string path) where T : Object => null; }

    // ----- object model -----
    public enum FindObjectsSortMode { None, InstanceID }

    public class Object
    {
        public string name { get; set; } = "";
        public static void Destroy(Object o) { }
        public static void DestroyImmediate(Object o) { }
        public static T FindFirstObjectByType<T>() where T : Object => null;
        public static T[] FindObjectsByType<T>(FindObjectsSortMode mode) where T : Object => new T[0];
        public override string ToString() => name;
    }

    public class ScriptableObject : Object { }

    public class GameObject : Object
    {
        internal readonly List<Component> components = new List<Component>();
        private readonly List<Component> pendingLifecycle = new List<Component>();
        public Transform transform { get; private set; }
        public string tag = "Untagged";
        public int layer;
        public bool activeSelf { get; private set; } = true;
        public bool activeInHierarchy => activeSelf;

        // Mirrors Unity: components added while inactive get Awake/OnEnable/Start when the object is activated.
        public void SetActive(bool value)
        {
            if (activeSelf == value) return;
            activeSelf = value;
            if (!value) return;
            foreach (Component c in pendingLifecycle.ToArray()) RunLifecycle(c);
            pendingLifecycle.Clear();
        }

        private static void RunLifecycle(Component component)
        {
            Lifecycle.Invoke(component, "Awake");
            Lifecycle.Invoke(component, "OnEnable");
            Lifecycle.Invoke(component, "Start");
        }

        public GameObject() : this("GameObject") { }

        public GameObject(string name, params Type[] componentTypes)
        {
            this.name = name;
            bool rect = componentTypes.Any(t => t == typeof(RectTransform));
            transform = rect ? new RectTransform() : new Transform();
            transform.gameObject = this;
            components.Add(transform);
            foreach (Type t in componentTypes)
            {
                if (t == typeof(RectTransform)) continue;
                AddComponent(t);
            }
        }

        public T AddComponent<T>() where T : Component => (T)AddComponent(typeof(T));

        public Component AddComponent(Type type)
        {
            foreach (RequireComponent req in type.GetCustomAttributes<RequireComponent>(true))
            {
                foreach (Type required in req.Types)
                {
                    if (GetComponent(required) == null) AddComponent(required);
                }
            }

            var component = (Component)Activator.CreateInstance(type);
            component.gameObject = this;
            components.Add(component);
            if (activeSelf) RunLifecycle(component);
            else pendingLifecycle.Add(component);
            return component;
        }

        public T GetComponent<T>() where T : class => components.OfType<T>().FirstOrDefault();
        public Component GetComponent(Type t) => components.FirstOrDefault(c => t.IsInstanceOfType(c));

        public T GetComponentInParent<T>() where T : class
        {
            for (GameObject go = this; go != null; go = go.transform.parent?.gameObject)
            {
                T c = go.GetComponent<T>();
                if (c != null) return c;
            }
            return null;
        }

        public T GetComponentInChildren<T>() where T : class
        {
            T own = GetComponent<T>();
            if (own != null) return own;
            foreach (Transform child in transform.children)
            {
                T c = child.gameObject.GetComponentInChildren<T>();
                if (c != null) return c;
            }
            return null;
        }

        public static GameObject CreatePrimitive(PrimitiveType type)
        {
            var go = new GameObject(type.ToString());
            go.AddComponent<MeshFilter>();
            go.AddComponent<MeshRenderer>();
            switch (type)
            {
                case PrimitiveType.Cube: go.AddComponent<BoxCollider>(); break;
                case PrimitiveType.Sphere: go.AddComponent<SphereCollider>(); break;
                case PrimitiveType.Plane: go.AddComponent<MeshCollider>(); break;
                default: go.AddComponent<CapsuleCollider>(); break;
            }
            return go;
        }
    }

    internal static class Lifecycle
    {
        public static void Invoke(object target, string methodName)
        {
            for (Type t = target.GetType(); t != null; t = t.BaseType)
            {
                MethodInfo m = t.GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly, null, Type.EmptyTypes, null);
                if (m != null)
                {
                    m.Invoke(target, null);
                    return;
                }
            }
        }
    }

    public class Component : Object
    {
        public GameObject gameObject { get; internal set; }
        public Transform transform => gameObject.transform;
        public T GetComponent<T>() where T : class => gameObject.GetComponent<T>();
        public T GetComponentInParent<T>() where T : class => gameObject.GetComponentInParent<T>();
        public T GetComponentInChildren<T>() where T : class => gameObject.GetComponentInChildren<T>();
    }

    public class Behaviour : Component
    {
        public bool enabled { get; set; } = true;
        public bool isActiveAndEnabled => enabled && gameObject.activeInHierarchy;
    }

    public class MonoBehaviour : Behaviour { }

    public class Transform : Component
    {
        internal readonly List<Transform> children = new List<Transform>();
        public Vector3 position, localPosition;
        public Vector3 localScale = Vector3.one;
        public Quaternion rotation = Quaternion.identity, localRotation = Quaternion.identity;
        public Transform parent { get; private set; }
        public int childCount => children.Count;
        public void SetParent(Transform p, bool worldPositionStays)
        {
            parent?.children.Remove(this);
            parent = p;
            p?.children.Add(this);
        }
    }

    public class RectTransform : Transform { public Vector2 sizeDelta; }

    // ----- rendering -----
    public class Shader : Object
    {
        private static readonly Dictionary<string, int> ids = new Dictionary<string, int>();
        public static int PropertyToID(string n) { if (!ids.TryGetValue(n, out int id)) { id = ids.Count + 1; ids[n] = id; } return id; }
        public static Shader Find(string n) => new Shader { name = n };
    }

    public enum MaterialGlobalIlluminationFlags { None, RealtimeEmissive, BakedEmissive, EmissiveIsBlack }

    public class Material : Object
    {
        public Shader shader;
        public Color color;
        public MaterialGlobalIlluminationFlags globalIlluminationFlags;
        public Material(Shader s) { shader = s; }
        public bool HasProperty(string n) => true;
        public void SetFloat(string n, float v) { }
        public void SetColor(string n, Color c) { }
        public void EnableKeyword(string k) { }
    }

    public class MaterialPropertyBlock
    {
        public readonly Dictionary<int, Color> Colors = new Dictionary<int, Color>();
        public void SetColor(int id, Color c) => Colors[id] = c;
        public void Clear() => Colors.Clear();
    }

    public class Renderer : Component
    {
        public Material sharedMaterial;
        public MaterialPropertyBlock LastBlock;
        public void SetPropertyBlock(MaterialPropertyBlock b) => LastBlock = b;
    }

    public class MeshRenderer : Renderer { }
    public class MeshFilter : Component { }

    public enum LightType { Spot, Directional, Point, Area }
    public enum LightShadows { None, Hard, Soft }

    public class Light : Behaviour
    {
        public LightType type;
        public Color color;
        public float intensity, range;
        public LightShadows shadows;
    }

    public static class RenderSettings
    {
        public static UnityEngine.Rendering.AmbientMode ambientMode;
        public static Color ambientLight;
    }

    public class Camera : Behaviour
    {
        public static Camera main;
        public Ray ScreenPointToRay(Vector3 p) => new Ray();
    }

    public class AudioSource : Behaviour { public void Play() { } }
    public class AudioListener : Behaviour { }

    // ----- physics -----
    public enum PrimitiveType { Sphere, Capsule, Cylinder, Cube, Plane, Quad }
    public enum RigidbodyInterpolation { None, Interpolate, Extrapolate }
    public enum CollisionDetectionMode { Discrete, Continuous, ContinuousDynamic, ContinuousSpeculative }
    public enum QueryTriggerInteraction { UseGlobal, Ignore, Collide }

    public struct LayerMask
    {
        public int value;
        public static implicit operator int(LayerMask m) => m.value;
        public static implicit operator LayerMask(int v) => new LayerMask { value = v };
    }

    public class Rigidbody : Component
    {
        public float mass = 1f;
        public bool isKinematic;
        public bool useGravity = true;
        public RigidbodyInterpolation interpolation;
        public CollisionDetectionMode collisionDetectionMode;
    }

    public class Collider : Component
    {
        public bool isTrigger;
        public bool enabled { get; set; } = true;
        public Rigidbody attachedRigidbody => GetComponentInParent<Rigidbody>();
    }

    public class BoxCollider : Collider { public Vector3 size = Vector3.one; }
    public class SphereCollider : Collider { }
    public class CapsuleCollider : Collider { }
    public class MeshCollider : Collider { }

    public struct Ray { public Vector3 origin, direction; }
    public struct RaycastHit { public Collider collider; }

    public static class Physics
    {
        public static bool Raycast(Ray ray, out RaycastHit hit, float maxDistance, int layerMask, QueryTriggerInteraction q)
        {
            hit = new RaycastHit();
            return false;
        }
    }

    // ----- coroutines -----
    public class YieldInstruction { }
    public class WaitForSeconds : YieldInstruction { public readonly float seconds; public WaitForSeconds(float s) { seconds = s; } }
    public class WaitForFixedUpdate : YieldInstruction { }

    namespace Events
    {
        public delegate void UnityAction();
        public delegate void UnityAction<T>(T arg);

        public class UnityEvent
        {
            private readonly List<UnityAction> listeners = new List<UnityAction>();
            public void AddListener(UnityAction a) => listeners.Add(a);
            public void RemoveListener(UnityAction a) => listeners.Remove(a);
            public void Invoke() { foreach (var l in listeners.ToArray()) l(); }
        }

        public class UnityEvent<T>
        {
            private readonly List<UnityAction<T>> listeners = new List<UnityAction<T>>();
            public void AddListener(UnityAction<T> a) => listeners.Add(a);
            public void RemoveListener(UnityAction<T> a) => listeners.Remove(a);
            public void Invoke(T arg) { foreach (var l in listeners.ToArray()) l(arg); }
        }
    }

    namespace XR
    {
        public static class XRSettings { public static bool isDeviceActive; }
    }

    namespace Rendering
    {
        public enum AmbientMode { Skybox, Trilight, Flat, Custom }
        public class RenderPipelineAsset : Object { }
        public static class GraphicsSettings { public static RenderPipelineAsset currentRenderPipeline; }
    }

    namespace SceneManagement
    {
        public struct Scene { public string path; }
    }

    namespace EventSystems
    {
        public class EventSystem : MonoBehaviour { }
        public class BaseInputModule : MonoBehaviour { }
    }

    namespace TestTools
    {
        [AttributeUsage(AttributeTargets.Method)] public class UnityTestAttribute : Attribute { }
    }
}
