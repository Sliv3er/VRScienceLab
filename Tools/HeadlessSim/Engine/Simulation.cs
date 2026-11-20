// HeadlessSim: the world (scene roots), play-mode lifecycle, timers, coroutines, cloning and
// serialization rules. Not part of the Unity project.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.Events;
using Object = UnityEngine.Object;

namespace HeadlessSim
{
    public enum LogKind { Log, Warning, Error, Exception }

    public struct LogEntry
    {
        public LogKind Kind;
        public string Message;
        public string Phase;
        public override string ToString() => $"[{Kind}] {Message}";
    }

    /// <summary>Everything written through UnityEngine.Debug.</summary>
    public static class LogCapture
    {
        public static readonly List<LogEntry> Entries = new List<LogEntry>();
        public static string Phase = "startup";
        /// <summary>Print plain Debug.Log messages too (warnings and errors are always printed).</summary>
        public static bool Verbose = false;
        /// <summary>Set to false to capture without printing.</summary>
        public static bool Echo = true;

        public static void Add(LogKind kind, string message)
        {
            Entries.Add(new LogEntry { Kind = kind, Message = message, Phase = Phase });
            if (!Echo) return;
            if (kind == LogKind.Log && !Verbose) return;
            Console.WriteLine($"    [{kind}] {message}");
        }

        public static int Count(LogKind kind, int fromIndex = 0)
        {
            int count = 0;
            for (int i = fromIndex; i < Entries.Count; i++) if (Entries[i].Kind == kind) count++;
            return count;
        }

        public static IEnumerable<LogEntry> Since(int fromIndex, params LogKind[] kinds)
        {
            for (int i = fromIndex; i < Entries.Count; i++)
            {
                if (kinds.Length == 0 || Array.IndexOf(kinds, Entries[i].Kind) >= 0) yield return Entries[i];
            }
        }
    }

    internal static class Lifecycle
    {
        private static readonly Dictionary<Type, Dictionary<string, MethodInfo>> Cache = new Dictionary<Type, Dictionary<string, MethodInfo>>();
        private static readonly Dictionary<Type, int> Orders = new Dictionary<Type, int>();
        private static readonly object[] NoArguments = new object[0];
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        /// <summary>Most-derived method called <paramref name="name"/> (any accessibility), like Unity's message lookup.</summary>
        public static MethodInfo Find(Type type, string name)
        {
            if (!Cache.TryGetValue(type, out Dictionary<string, MethodInfo> byName))
            {
                byName = new Dictionary<string, MethodInfo>();
                Cache[type] = byName;
            }

            if (byName.TryGetValue(name, out MethodInfo cached)) return cached;

            MethodInfo found = null;
            for (Type t = type; t != null && t != typeof(Component) && t != typeof(object); t = t.BaseType)
            {
                foreach (MethodInfo candidate in t.GetMethods(Flags))
                {
                    if (candidate.Name != name || candidate.IsGenericMethod) continue;
                    if (found == null || candidate.GetParameters().Length < found.GetParameters().Length) found = candidate;
                }

                if (found != null) break;
            }

            byName[name] = found;
            return found;
        }

        public static bool Has(Type type, string name) => Find(type, name) != null;

        public static int ExecutionOrder(Type type)
        {
            if (Orders.TryGetValue(type, out int order)) return order;
            var attribute = type.GetCustomAttribute<DefaultExecutionOrder>(true);
            order = attribute != null ? attribute.order : 0;
            Orders[type] = order;
            return order;
        }

        /// <summary>Calls a Unity message on a MonoBehaviour. Exceptions are logged (like Unity) and reported.</summary>
        public static void Call(Component component, string name, object argument = null)
        {
            if (!(component is MonoBehaviour)) return;
            MethodInfo method = Find(component.GetType(), name);
            if (method == null) return;
            ParameterInfo[] parameters = method.GetParameters();
            try
            {
                if (parameters.Length == 0) method.Invoke(component, NoArguments);
                else if (parameters.Length == 1 && (argument == null || parameters[0].ParameterType.IsInstanceOfType(argument))) method.Invoke(component, new[] { argument });
            }
            catch (TargetInvocationException exception)
            {
                Simulation.ReportException(exception.InnerException ?? exception, component, name);
            }
        }

        public static void InvokeNamed(Component component, string name, object argument)
        {
            Call(component, name, argument);
        }
    }

    /// <summary>Unity's serialization rules, shared by Instantiate and SerializedObject.</summary>
    public static class Serialization
    {
        private static readonly Dictionary<Type, FieldInfo[]> FieldCache = new Dictionary<Type, FieldInfo[]>();
        private static readonly Dictionary<Type, bool> SerializableCache = new Dictionary<Type, bool>();
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        public static bool IsEngineType(Type type)
        {
            string ns = type.Namespace ?? "";
            return ns == "UnityEngine" || ns.StartsWith("UnityEngine.") || ns.StartsWith("Unity.") || ns == "TMPro" || ns.StartsWith("UnityEditor") || ns.StartsWith("HeadlessSim");
        }

        public static bool IsSerializableType(Type type)
        {
            if (SerializableCache.TryGetValue(type, out bool cached)) return cached;
            bool result = Compute(type);
            SerializableCache[type] = result;
            return result;
        }

        private static bool Compute(Type type)
        {
            if (type.IsPrimitive || type == typeof(string) || type.IsEnum) return true;
            if (typeof(Object).IsAssignableFrom(type)) return true;
            if (typeof(UnityEventBase).IsAssignableFrom(type)) return true;
            if (type.IsArray) return type.GetArrayRank() == 1 && IsSerializableType(type.GetElementType());
            if (type.IsGenericType)
            {
                if (type.GetGenericTypeDefinition() == typeof(List<>)) return IsSerializableType(type.GetGenericArguments()[0]);
                return false;
            }
            if (type.IsInterface || type.IsAbstract || typeof(Delegate).IsAssignableFrom(type)) return false;
            if (type.IsValueType) return IsEngineType(type) || type.GetCustomAttribute<SerializableAttribute>() != null;
            return type.GetCustomAttribute<SerializableAttribute>() != null;
        }

        /// <summary>Fields Unity would serialize: public (not readonly) or [SerializeField], of a serializable type.</summary>
        public static FieldInfo[] GetSerializedFields(Type type)
        {
            if (FieldCache.TryGetValue(type, out FieldInfo[] cached)) return cached;

            var chain = new List<Type>();
            for (Type t = type; t != null && t != typeof(object) && t != typeof(Object); t = t.BaseType) chain.Add(t);
            chain.Reverse();

            var fields = new List<FieldInfo>();
            foreach (Type t in chain)
            {
                foreach (FieldInfo field in t.GetFields(Flags))
                {
                    if (field.IsStatic || field.IsLiteral || field.IsInitOnly) continue;
                    if (field.GetCustomAttribute<NonSerializedAttribute>() != null) continue;
                    if (!field.IsPublic && field.GetCustomAttribute<SerializeField>() == null) continue;
                    if (!IsSerializableType(field.FieldType)) continue;
                    fields.Add(field);
                }
            }

            cached = fields.ToArray();
            FieldCache[type] = cached;
            return cached;
        }

        public static FieldInfo FindSerializedField(Type type, string name)
        {
            FieldInfo[] fields = GetSerializedFields(type);
            for (int i = fields.Length - 1; i >= 0; i--)
            {
                if (fields[i].Name == name) return fields[i];
            }
            return null;
        }

        /// <summary>Deep copy of a serialized value; object references inside the cloned hierarchy are remapped.</summary>
        public static object CloneValue(object value, Dictionary<Object, Object> map)
        {
            if (ReferenceEquals(value, null)) return null;
            Type type = value.GetType();
            if (type.IsPrimitive || type == typeof(string) || type.IsEnum) return value;

            if (value is Object unityObject)
            {
                return map != null && map.TryGetValue(unityObject, out Object mapped) ? mapped : unityObject;
            }

            if (value is UnityEventBase unityEvent) return unityEvent.CloneForInstantiate(map);

            if (type.IsArray)
            {
                var source = (Array)value;
                Array copy = Array.CreateInstance(type.GetElementType(), source.Length);
                for (int i = 0; i < source.Length; i++) copy.SetValue(CloneValue(source.GetValue(i), map), i);
                return copy;
            }

            if (value is IList list && type.IsGenericType)
            {
                var copy = (IList)Activator.CreateInstance(type);
                foreach (object item in list) copy.Add(CloneValue(item, map));
                return copy;
            }

            if (type.IsValueType && IsPlainStruct(type)) return value;

            // [Serializable] class or struct: copy field by field.
            object clone = type.IsValueType ? value : Activator.CreateInstance(type, true);
            foreach (FieldInfo field in GetSerializedFields(type))
            {
                field.SetValue(clone, CloneValue(field.GetValue(value), map));
            }
            return clone;
        }

        private static readonly Dictionary<Type, bool> PlainCache = new Dictionary<Type, bool>();

        // A struct whose serialized fields hold no references can be copied by value.
        private static bool IsPlainStruct(Type type)
        {
            if (PlainCache.TryGetValue(type, out bool cached)) return cached;
            bool plain = true;
            foreach (FieldInfo field in GetSerializedFields(type))
            {
                Type fieldType = field.FieldType;
                if (fieldType.IsPrimitive || fieldType.IsEnum) continue;
                if (fieldType.IsValueType && fieldType != type && IsPlainStruct(fieldType)) continue;
                plain = false;
                break;
            }
            PlainCache[type] = plain;
            return plain;
        }

        public static void CopySerializedFields(object source, object destination, Dictionary<Object, Object> map)
        {
            foreach (FieldInfo field in GetSerializedFields(source.GetType()))
            {
                field.SetValue(destination, CloneValue(field.GetValue(source), map));
            }
        }
    }

    internal static class Cloner
    {
        public static Object Instantiate(Object original, Transform parent, bool worldPositionStays, bool hasPose, Vector3 position, Quaternion rotation)
        {
            if (ReferenceEquals(original, null) || original.destroyed) throw new ArgumentException("The Object you want to instantiate is null.");

            GameObject sourceObject = original as GameObject ?? (original as Component)?.gameObject;
            if (sourceObject == null) return CloneLooseObject(original);

            var map = new Dictionary<Object, Object>();
            GameObject clone = CloneHierarchy(sourceObject, map);
            clone.name = sourceObject.name + "(Clone)";
            clone.isAssetRoot = false;

            Vector3 sourceWorldPosition = sourceObject.transform.position;
            Quaternion sourceWorldRotation = sourceObject.transform.rotation;

            if (parent != null)
            {
                clone.transform.SetParentRaw(parent);
                if (worldPositionStays && !hasPose)
                {
                    clone.transform.position = sourceWorldPosition;
                    clone.transform.rotation = sourceWorldRotation;
                }
            }
            else
            {
                Simulation.RegisterRoot(clone);
                if (!hasPose)
                {
                    clone.transform.position = sourceWorldPosition;
                    clone.transform.rotation = sourceWorldRotation;
                }
            }

            if (hasPose)
            {
                clone.transform.position = position;
                clone.transform.rotation = rotation;
            }

            Simulation.Touch();
            if (Application.isPlaying && !clone.IsAsset && clone.activeInHierarchy) Simulation.ActivateHierarchy(clone);

            if (original is GameObject) return clone;
            return map[original];
        }

        private static Object CloneLooseObject(Object original)
        {
            if (original is Material material) return new Material(material);
            MethodInfo memberwise = typeof(object).GetMethod("MemberwiseClone", BindingFlags.Instance | BindingFlags.NonPublic);
            var copy = (Object)memberwise.Invoke(original, null);
            copy.name = original.name + "(Clone)";
            return copy;
        }

        /// <summary>Clones a hierarchy without registering it as a scene root and without running any lifecycle.</summary>
        public static GameObject CloneHierarchy(GameObject source, Dictionary<Object, Object> map)
        {
            GameObject clone = CreateShells(source, map);
            CopyData(source, map);
            return clone;
        }

        /// <summary>Clones several roots at once so references between them are remapped too.</summary>
        public static List<GameObject> CloneHierarchies(IEnumerable<GameObject> sources, Dictionary<Object, Object> map)
        {
            var sourceList = sources.ToList();
            var clones = new List<GameObject>();
            foreach (GameObject source in sourceList) clones.Add(CreateShells(source, map));
            foreach (GameObject source in sourceList) CopyData(source, map);
            return clones;
        }

        private static GameObject CreateShells(GameObject source, Dictionary<Object, Object> map)
        {
            var clone = new GameObject(source.name, source.transform is RectTransform, false);
            clone.layer = source.layer;
            clone.tag = source.tag;
            clone.isStatic = source.isStatic;
            clone.hideFlags = source.hideFlags;
            clone.SetActiveRaw(source.activeSelf);
            map[source] = clone;
            map[source.transform] = clone.transform;

            for (int i = 1; i < source.components.Count; i++)
            {
                Component component = source.components[i];
                if (component.destroyed) continue;
                map[component] = clone.AddComponentRaw(component.GetType());
            }

            for (int i = 0; i < source.transform.childCount; i++)
            {
                GameObject child = CreateShells(source.transform.GetChild(i).gameObject, map);
                child.transform.SetParentRaw(clone.transform);
            }

            return clone;
        }

        private static void CopyData(GameObject source, Dictionary<Object, Object> map)
        {
            var clone = (GameObject)map[source];
            clone.transform.localPosition = source.transform.localPosition;
            clone.transform.localRotation = source.transform.localRotation;
            clone.transform.localScale = source.transform.localScale;

            foreach (Component component in source.components)
            {
                if (component.destroyed || !map.TryGetValue(component, out Object target)) continue;
                Serialization.CopySerializedFields(component, target, map);
            }

            // The RectTransform copy above may have re-run the anchor layout; the stored local position wins.
            clone.transform.localPosition = source.transform.localPosition;

            for (int i = 0; i < source.transform.childCount; i++) CopyData(source.transform.GetChild(i).gameObject, map);
        }
    }

    /// <summary>The simulated world: scene roots, edit/play mode, frame stepping.</summary>
    public static class Simulation
    {
        private class Timer
        {
            public MonoBehaviour Target;
            public string Method;
            public float NextTime;
            public float Repeat;
            public bool Cancelled;
        }

        private class CoroutineState
        {
            public MonoBehaviour Owner;
            public Coroutine Handle;
            public readonly Stack<IEnumerator> Stack = new Stack<IEnumerator>();
            public float WaitUntilTime = -1f;
            public Coroutine WaitFor;
            public CustomYieldInstruction WaitCustom;
        }

        private struct PendingDestroy
        {
            public Object Target;
            public float Time;
        }

        private static readonly List<GameObject> Roots = new List<GameObject>();
        private static readonly List<Component> PendingStarts = new List<Component>();
        private static readonly List<PendingDestroy> DestroyQueue = new List<PendingDestroy>();
        private static readonly List<Timer> Timers = new List<Timer>();
        private static readonly List<CoroutineState> Coroutines = new List<CoroutineState>();
        private static readonly HashSet<long> TriggerPairs = new HashSet<long>();
        private static readonly Dictionary<long, KeyValuePair<Collider, Collider>> TriggerPairObjects = new Dictionary<long, KeyValuePair<Collider, Collider>>();
        private static readonly List<Component> TickCache = new List<Component>();
        private static int tickCacheVersion = -1;
        private static int version;
        private static float fixedAccumulator;

        public const float FixedDeltaTime = 0.02f;

        /// <summary>Exceptions thrown inside Unity messages (Awake, Update, event handlers...).</summary>
        public static readonly List<string> Exceptions = new List<string>();

        /// <summary>Raised by Step after every FixedUpdate pass (hook for extra simulated physics).</summary>
        public static event Action<float> AfterFixedUpdate;

        public static int SceneVersion => version;
        internal static void Touch() { version++; }

        // ---------------------------------------------------------------- Roots and queries
        internal static void RegisterRoot(GameObject go)
        {
            if (go.isAssetRoot) return;
            if (!Roots.Contains(go)) Roots.Add(go);
            version++;
        }

        internal static void UnregisterRoot(GameObject go)
        {
            Roots.Remove(go);
            version++;
        }

        internal static int RootIndex(GameObject go) => Roots.IndexOf(go);

        public static IReadOnlyList<GameObject> RootObjects => Roots;

        public static IEnumerable<GameObject> AllSceneObjects(bool includeInactive)
        {
            foreach (GameObject root in Roots.ToArray())
            {
                if (root.destroyed) continue;
                foreach (GameObject go in Walk(root, includeInactive)) yield return go;
            }
        }

        private static IEnumerable<GameObject> Walk(GameObject go, bool includeInactive)
        {
            if (!includeInactive && !go.activeSelf) yield break;
            yield return go;
            Transform t = go.transform;
            for (int i = 0; i < t.childCount; i++)
            {
                foreach (GameObject child in Walk(t.GetChild(i).gameObject, includeInactive)) yield return child;
            }
        }

        internal static IEnumerable<Object> FindObjects(Type type, bool includeInactive)
        {
            if (type == typeof(GameObject))
            {
                foreach (GameObject go in AllSceneObjects(includeInactive)) yield return go;
                yield break;
            }

            foreach (GameObject go in AllSceneObjects(includeInactive))
            {
                List<Component> components = go.components;
                for (int i = 0; i < components.Count; i++)
                {
                    if (!components[i].destroyed && type.IsInstanceOfType(components[i])) yield return components[i];
                }
            }
        }

        /// <summary>Convenience for harness code: every component of type T in the scene.</summary>
        public static List<T> FindAll<T>(bool includeInactive = true) where T : class
        {
            var results = new List<T>();
            foreach (GameObject go in AllSceneObjects(includeInactive))
            {
                foreach (Component component in go.components)
                {
                    if (!component.destroyed && component is T match) results.Add(match);
                }
            }
            return results;
        }

        // ---------------------------------------------------------------- Mode switches
        /// <summary>Removes every scene object, timers, coroutines; resets Time. Assets are kept.</summary>
        public static void ResetWorld()
        {
            foreach (GameObject root in Roots.ToArray()) MarkDestroyed(root);
            Roots.Clear();
            PendingStarts.Clear();
            DestroyQueue.Clear();
            Timers.Clear();
            Coroutines.Clear();
            TriggerPairs.Clear();
            TriggerPairObjects.Clear();
            fixedAccumulator = 0f;
            GravityEnabled = true;
            Application.isPlaying = false;
            Time.Reset();
            UnityEngine.AI.NavMeshWorld.Reset();
            UnityEngine.XR.Interaction.Toolkit.XRSim.Reset();
            version++;
        }

        /// <summary>Removes the scene objects only (used by SceneManager when a scene is loaded or created).</summary>
        internal static void ClearScene(bool keepDontDestroyOnLoad)
        {
            foreach (GameObject root in Roots.ToArray())
            {
                if (keepDontDestroyOnLoad && root.dontDestroyOnLoad) continue;
                if (Application.isPlaying) DestroyNow(root);
                else { MarkDestroyed(root); Roots.Remove(root); }
            }
            TriggerPairs.Clear();
            TriggerPairObjects.Clear();
            version++;
        }

        public static void EnterPlayMode()
        {
            if (Application.isPlaying) return;
            Application.isPlaying = true;
            RunRuntimeInitializers();
            ActivateScene();
        }

        /// <summary>Awake + OnEnable for every active object of the scene, then Start.</summary>
        internal static void ActivateScene()
        {
            var components = new List<Component>();
            foreach (GameObject go in AllSceneObjects(false)) components.AddRange(go.components);
            ActivateBatch(components);
            RunPendingStarts();
        }

        public static void ExitPlayMode()
        {
            if (!Application.isPlaying) return;
            foreach (GameObject go in AllSceneObjects(true).ToArray())
            {
                foreach (Component component in go.components.ToArray()) Deactivate(component);
            }
            Application.isPlaying = false;
            Timers.Clear();
            Coroutines.Clear();
            PendingStarts.Clear();
        }

        private static void RunRuntimeInitializers()
        {
            foreach (Type type in typeof(Simulation).Assembly.GetTypes())
            {
                foreach (MethodInfo method in type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    if (method.GetCustomAttribute<RuntimeInitializeOnLoadMethodAttribute>() == null) continue;
                    try { method.Invoke(null, null); }
                    catch (TargetInvocationException exception) { ReportException(exception.InnerException ?? exception, null, method.Name); }
                }
            }
        }

        // ---------------------------------------------------------------- Lifecycle
        internal static void ReportException(Exception exception, Component context, string message)
        {
            if (exception is NUnit.Framework.AssertionException || exception is NUnit.Framework.IgnoreException) throw exception;
            string text = $"{exception.GetType().Name} in {(context != null ? context.GetType().Name + "." : "")}{message}: {exception.Message}\n{exception.StackTrace}";
            Exceptions.Add(text);
            LogCapture.Add(LogKind.Exception, text);
        }

        internal static void OnComponentAdded(Component component)
        {
            version++;
            if (!Application.isPlaying)
            {
                // Editor behaviour: adding a component outside play mode calls its Reset() message.
                Lifecycle.Call(component, "Reset");
                return;
            }
            if (component.gameObject.IsAsset) return;
            if (component.gameObject.activeInHierarchy) Activate(component);
        }

        internal static void ActivateHierarchy(GameObject root)
        {
            var components = new List<Component>();
            foreach (GameObject go in Walk(root, false)) components.AddRange(go.components);
            ActivateBatch(components);
        }

        private static void ActivateBatch(List<Component> components)
        {
            // Stable sort by [DefaultExecutionOrder]; Awake + OnEnable per component, object by object.
            List<Component> ordered = components.OrderBy(c => Lifecycle.ExecutionOrder(c.GetType())).ToList();
            foreach (Component component in ordered)
            {
                if (component.destroyed || component.awakeCalled) continue;
                if (!component.gameObject.activeInHierarchy) continue;
                component.awakeCalled = true;
                component.SimAwake();
                Lifecycle.Call(component, "Awake");
            }

            foreach (Component component in ordered)
            {
                if (component.destroyed || !component.gameObject.activeInHierarchy) continue;
                EnableIfNeeded(component);
            }
        }

        private static void Activate(Component component)
        {
            if (component.destroyed) return;
            if (!component.awakeCalled)
            {
                component.awakeCalled = true;
                component.SimAwake();
                Lifecycle.Call(component, "Awake");
            }

            if (component.destroyed) return;
            EnableIfNeeded(component);
        }

        private static void EnableIfNeeded(Component component)
        {
            if (!component.awakeCalled || component.enableActive || !component.SimEnabled) return;
            if (!component.gameObject.activeInHierarchy) return;
            component.enableActive = true;
            component.SimOnEnable();
            Lifecycle.Call(component, "OnEnable");
            if (!component.startCalled && !component.startQueued)
            {
                component.startQueued = true;
                PendingStarts.Add(component);
            }
        }

        private static void Deactivate(Component component)
        {
            if (!component.enableActive) return;
            component.enableActive = false;
            Lifecycle.Call(component, "OnDisable");
            component.SimOnDisable();
        }

        internal static void OnActiveInHierarchyChanged(GameObject go, bool nowActive)
        {
            version++;
            if (!Application.isPlaying || go.IsAsset) return;
            if (nowActive)
            {
                ActivateHierarchy(go);
            }
            else
            {
                foreach (GameObject child in Walk(go, true).ToArray())
                {
                    foreach (Component component in child.components.ToArray()) Deactivate(component);
                }
            }
        }

        internal static void OnEnabledChanged(Behaviour behaviour)
        {
            if (!Application.isPlaying || behaviour.gameObject == null || behaviour.gameObject.IsAsset) return;
            if (behaviour.enabled)
            {
                if (behaviour.gameObject.activeInHierarchy && behaviour.awakeCalled) EnableIfNeeded(behaviour);
                else if (behaviour.gameObject.activeInHierarchy) Activate(behaviour);
            }
            else
            {
                Deactivate(behaviour);
            }
        }

        private static void RunPendingStarts()
        {
            int guard = 0;
            while (PendingStarts.Count > 0 && guard++ < 64)
            {
                Component[] batch = PendingStarts.OrderBy(c => Lifecycle.ExecutionOrder(c.GetType())).ToArray();
                PendingStarts.Clear();
                foreach (Component component in batch)
                {
                    component.startQueued = false;
                    if (component.destroyed || component.startCalled) continue;
                    if (!component.enableActive)
                    {
                        // Disabled before its first frame: Start runs when it is enabled again.
                        continue;
                    }

                    component.startCalled = true;
                    component.SimStart();
                    Lifecycle.Call(component, "Start");
                }
            }
        }

        // ---------------------------------------------------------------- Destroy
        internal static void QueueDestroy(Object target, float delay)
        {
            DestroyQueue.Add(new PendingDestroy { Target = target, Time = Time.time + Mathf.Max(delay, 0f) });
        }

        internal static void DestroyNow(Object target)
        {
            if (ReferenceEquals(target, null) || target.destroyed) return;

            if (target is GameObject go)
            {
                bool notify = Application.isPlaying && !go.IsAsset;
                GameObject[] subtree = Walk(go, true).ToArray();
                if (notify)
                {
                    foreach (GameObject child in subtree)
                    {
                        foreach (Component component in child.components.ToArray()) Deactivate(component);
                    }

                    foreach (GameObject child in subtree)
                    {
                        foreach (Component component in child.components.ToArray())
                        {
                            if (component.awakeCalled) Lifecycle.Call(component, "OnDestroy");
                            component.SimOnDestroy();
                        }
                    }
                }

                if (go.transform.parent != null) go.transform.RemoveFromParentRaw();
                else Roots.Remove(go);
                MarkDestroyed(go);
                version++;
                return;
            }

            if (target is Component c)
            {
                if (c is Transform)
                {
                    Debug.LogError("Can't destroy Transform component. If you want to destroy the game object, please call 'Destroy' on the game object instead.");
                    return;
                }

                if (Application.isPlaying && c.gameObject != null && !c.gameObject.IsAsset)
                {
                    Deactivate(c);
                    if (c.awakeCalled) Lifecycle.Call(c, "OnDestroy");
                    c.SimOnDestroy();
                }

                c.gameObject?.components.Remove(c);
                c.destroyed = true;
                version++;
                return;
            }

            target.destroyed = true;
        }

        private static void MarkDestroyed(GameObject go)
        {
            foreach (GameObject child in Walk(go, true).ToArray())
            {
                child.destroyed = true;
                foreach (Component component in child.components) component.destroyed = true;
            }
        }

        private static void ProcessDestroyQueue()
        {
            if (DestroyQueue.Count == 0) return;
            PendingDestroy[] pending = DestroyQueue.ToArray();
            DestroyQueue.Clear();
            foreach (PendingDestroy item in pending)
            {
                if (item.Time <= Time.time + 1e-6f) DestroyNow(item.Target);
                else DestroyQueue.Add(item);
            }
        }

        // ---------------------------------------------------------------- Timers and coroutines
        internal static void AddTimer(MonoBehaviour target, string method, float delay, float repeat)
        {
            if (!Application.isPlaying) return;
            Timers.Add(new Timer { Target = target, Method = method, NextTime = Time.time + Mathf.Max(delay, 0f), Repeat = repeat });
        }

        internal static void CancelTimers(MonoBehaviour target, string method)
        {
            foreach (Timer timer in Timers)
            {
                if (ReferenceEquals(timer.Target, target) && (method == null || timer.Method == method)) timer.Cancelled = true;
            }
        }

        internal static bool HasTimer(MonoBehaviour target, string method)
        {
            return Timers.Any(t => !t.Cancelled && ReferenceEquals(t.Target, target) && (method == null || t.Method == method));
        }

        private static void ProcessTimers()
        {
            if (Timers.Count == 0) return;
            foreach (Timer timer in Timers.ToArray())
            {
                if (timer.Cancelled || timer.Target == null) { timer.Cancelled = true; continue; }
                int guard = 0;
                while (!timer.Cancelled && Time.time + 1e-6f >= timer.NextTime && guard++ < 8)
                {
                    Lifecycle.Call(timer.Target, timer.Method);
                    if (timer.Repeat > 0f) timer.NextTime += timer.Repeat;
                    else timer.Cancelled = true;
                }
            }
            Timers.RemoveAll(t => t.Cancelled);
        }

        internal static Coroutine StartCoroutine(MonoBehaviour owner, IEnumerator routine)
        {
            if (routine == null) return null;
            var state = new CoroutineState { Owner = owner, Handle = new Coroutine() };
            state.Stack.Push(routine);
            Coroutines.Add(state);
            AdvanceCoroutine(state); // Unity runs a coroutine up to its first yield immediately.
            return state.Handle;
        }

        internal static void StopCoroutine(Coroutine handle)
        {
            foreach (CoroutineState state in Coroutines) if (ReferenceEquals(state.Handle, handle)) state.Handle.done = true;
        }

        internal static void StopCoroutine(MonoBehaviour owner, IEnumerator routine)
        {
            foreach (CoroutineState state in Coroutines)
            {
                if (ReferenceEquals(state.Owner, owner) && state.Stack.Contains(routine)) state.Handle.done = true;
            }
        }

        internal static void StopAllCoroutines(MonoBehaviour owner)
        {
            foreach (CoroutineState state in Coroutines) if (ReferenceEquals(state.Owner, owner)) state.Handle.done = true;
        }

        private static void ProcessCoroutines()
        {
            if (Coroutines.Count == 0) return;
            foreach (CoroutineState state in Coroutines.ToArray())
            {
                if (state.Handle.done) continue;
                if (state.Owner == null || !state.Owner.gameObject.activeInHierarchy) { state.Handle.done = true; continue; }
                if (state.WaitUntilTime >= 0f) { if (Time.time < state.WaitUntilTime) continue; state.WaitUntilTime = -1f; }
                if (state.WaitFor != null) { if (!state.WaitFor.done) continue; state.WaitFor = null; }
                if (state.WaitCustom != null) { if (state.WaitCustom.keepWaiting) continue; state.WaitCustom = null; }
                AdvanceCoroutine(state);
            }
            Coroutines.RemoveAll(s => s.Handle.done);
        }

        private static void AdvanceCoroutine(CoroutineState state)
        {
            try
            {
                while (state.Stack.Count > 0)
                {
                    IEnumerator top = state.Stack.Peek();
                    if (!top.MoveNext()) { state.Stack.Pop(); continue; }
                    object current = top.Current;
                    if (current is IEnumerator nested) { state.Stack.Push(nested); continue; }
                    if (current is WaitForSeconds wait) state.WaitUntilTime = Time.time + wait.seconds;
                    else if (current is WaitForSecondsRealtime realtime) state.WaitUntilTime = Time.time + realtime.waitTime;
                    else if (current is Coroutine other) state.WaitFor = other;
                    else if (current is CustomYieldInstruction custom) state.WaitCustom = custom;
                    return;
                }
                state.Handle.done = true;
            }
            catch (Exception exception)
            {
                state.Handle.done = true;
                ReportException(exception, state.Owner, "coroutine");
            }
        }

        // ---------------------------------------------------------------- Frame stepping
        private static List<Component> Tickables()
        {
            if (tickCacheVersion == version) return TickCache;
            TickCache.Clear();
            foreach (GameObject go in AllSceneObjects(true))
            {
                foreach (Component component in go.components)
                {
                    if (component.destroyed) continue;
                    if (component.SimTicks) { TickCache.Add(component); continue; }
                    if (!(component is MonoBehaviour)) continue;
                    Type type = component.GetType();
                    if (Lifecycle.Has(type, "Update") || Lifecycle.Has(type, "FixedUpdate") || Lifecycle.Has(type, "LateUpdate")) TickCache.Add(component);
                }
            }
            tickCacheVersion = version;
            return TickCache;
        }

        private static bool Ticking(Component component)
        {
            return !component.destroyed && component.enableActive && component.SimEnabled && component.gameObject.activeInHierarchy;
        }

        /// <summary>Advances the simulation by one rendered frame of <paramref name="deltaTime"/> seconds (unscaled).</summary>
        public static void Step(float deltaTime)
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Simulation.Step requires play mode (call EnterPlayMode first).");

            float scaled = deltaTime * Time.timeScale;
            Time.Advance(deltaTime, scaled);
            RunPendingStarts();

            fixedAccumulator += scaled;
            int fixedSteps = 0;
            while (fixedAccumulator >= FixedDeltaTime - 1e-6f && fixedSteps++ < 50)
            {
                fixedAccumulator -= FixedDeltaTime;
                Time.fixedTime += FixedDeltaTime;
                Time.inFixedTimeStep = true;
                foreach (Component component in Tickables().ToArray())
                {
                    if (!Ticking(component)) continue;
                    component.SimFixedUpdate(FixedDeltaTime);
                    Lifecycle.Call(component, "FixedUpdate");
                }

                if (GravityEnabled) SimulateGravity(FixedDeltaTime);
                UpdateTriggers();
                AfterFixedUpdate?.Invoke(FixedDeltaTime);
                Time.inFixedTimeStep = false;
                RunPendingStarts();
            }

            ProcessTimers();

            foreach (Component component in Tickables().ToArray())
            {
                if (!Ticking(component)) continue;
                component.SimUpdate(scaled);
                Lifecycle.Call(component, "Update");
            }

            ProcessCoroutines();

            foreach (Component component in Tickables().ToArray())
            {
                if (!Ticking(component)) continue;
                Lifecycle.Call(component, "LateUpdate");
            }

            ProcessDestroyQueue();
        }

        public static void RunFor(float seconds, float deltaTime = 1f / 60f)
        {
            // "seconds" is game time: with a time scale above 1 fewer frames are needed.
            float target = Time.time + seconds;
            int guard = 0;
            while (Time.time < target - 1e-5f && guard++ < 2000000)
            {
                Step(deltaTime);
                if (Time.timeScale <= 0f) break;
            }
        }

        // ---------------------------------------------------------------- Gravity (vertical only)
        /// <summary>
        /// Minimal stand-in for rigid-body dynamics: a non-kinematic Rigidbody with gravity falls straight
        /// down until the bounding box of its colliders rests on the bounding box of a solid collider
        /// below it (OnCollisionEnter is sent on landing). No lateral motion, no rotation, no pushing.
        /// </summary>
        public static bool GravityEnabled = true;

        /// <summary>A dynamic body whose collider box overlaps another solid collider box (PhysX would push it out).</summary>
        public struct Penetration
        {
            public Rigidbody Body;
            public Collider Other;
            public float Depth;
            public override string ToString() => $"{Body.name} overlaps {Other.name} by {Depth * 1000f:0.0} mm";
        }

        private static bool BodyBounds(Rigidbody body, List<Collider> all, out Bounds bounds)
        {
            bounds = new Bounds();
            bool any = false;
            foreach (Collider collider in all)
            {
                if (collider.isTrigger || !ReferenceEquals(collider.attachedRigidbody, body)) continue;
                if (!any) { bounds = collider.bounds; any = true; }
                else bounds.Encapsulate(collider.bounds);
            }
            return any;
        }

        private static List<Collider> ActiveColliders()
        {
            var colliders = new List<Collider>();
            foreach (GameObject go in AllSceneObjects(false))
            {
                foreach (Component component in go.components)
                {
                    if (component is Collider collider && collider.enabled && !collider.destroyed) colliders.Add(collider);
                }
            }
            return colliders;
        }

        private static float Overlap(float minA, float maxA, float minB, float maxB) => Mathf.Min(maxA, maxB) - Mathf.Max(minA, minB);

        /// <summary>Dynamic bodies whose collider boxes interpenetrate another solid collider by more than <paramref name="tolerance"/> metres on every axis.</summary>
        public static List<Penetration> FindPenetrations(float tolerance = 0.002f)
        {
            var result = new List<Penetration>();
            List<Collider> colliders = ActiveColliders();
            foreach (Rigidbody body in FindAll<Rigidbody>(false))
            {
                if (body.isKinematic || !BodyBounds(body, colliders, out Bounds box)) continue;
                foreach (Collider other in colliders)
                {
                    if (other.isTrigger || ReferenceEquals(other.attachedRigidbody, body)) continue;
                    Bounds b = other.bounds;
                    float depth = Mathf.Min(Overlap(box.min.x, box.max.x, b.min.x, b.max.x), Overlap(box.min.y, box.max.y, b.min.y, b.max.y), Overlap(box.min.z, box.max.z, b.min.z, b.max.z));
                    if (depth > tolerance) result.Add(new Penetration { Body = body, Other = other, Depth = depth });
                }
            }
            return result;
        }

        public enum BodyRest { Resting, Falling, Embedded, Kinematic, Held, NoCollider }

        /// <summary>Where a Rigidbody stands right now (what it rests on, or that it is falling).</summary>
        public struct BodyState
        {
            public Rigidbody Body;
            public BodyRest Rest;
            public Collider Support;
            public override string ToString()
            {
                string where = Rest == BodyRest.Resting && Support != null ? $" on {DescribeOwner(Support)}" : (Rest == BodyRest.Embedded && Support != null ? $" in {DescribeOwner(Support)}" : "");
                return $"{Body.name}: {Rest}{where} at y={Body.transform.position.y:0.000}";
            }

            private static string DescribeOwner(Collider collider)
            {
                Transform t = collider.transform;
                string path = t.name;
                for (int i = 0; i < 2 && t.parent != null; i++) { t = t.parent; path = t.name + "/" + path; }
                return path;
            }
        }

        /// <summary>State of every Rigidbody of the scene, using the same box test as the gravity step.</summary>
        public static List<BodyState> BodyStates()
        {
            var result = new List<BodyState>();
            List<Collider> colliders = ActiveColliders();
            foreach (Rigidbody body in FindAll<Rigidbody>(false))
            {
                var state = new BodyState { Body = body };
                if (body.isKinematic) { state.Rest = BodyRest.Kinematic; result.Add(state); continue; }
                if (!body.useGravity) { state.Rest = BodyRest.Held; result.Add(state); continue; }
                if (!BodyBounds(body, colliders, out Bounds box)) { state.Rest = BodyRest.NoCollider; result.Add(state); continue; }

                float supportTop = float.NegativeInfinity;
                state.Rest = BodyRest.Falling;
                foreach (Collider other in colliders)
                {
                    if (other.isTrigger || ReferenceEquals(other.attachedRigidbody, body)) continue;
                    Bounds b = other.bounds;
                    if (Overlap(box.min.x, box.max.x, b.min.x, b.max.x) < 0.001f || Overlap(box.min.z, box.max.z, b.min.z, b.max.z) < 0.001f) continue;
                    if (b.max.y <= box.min.y + 0.002f)
                    {
                        if (b.max.y > supportTop) { supportTop = b.max.y; if (box.min.y - supportTop <= 0.001f) { state.Rest = BodyRest.Resting; state.Support = other; } }
                    }
                    else if (b.min.y < box.max.y - 0.002f)
                    {
                        state.Rest = BodyRest.Embedded; state.Support = other;
                        break;
                    }
                }
                result.Add(state);
            }
            return result;
        }

        private static void SimulateGravity(float deltaTime)
        {
            List<Rigidbody> bodies = FindAll<Rigidbody>(false);
            if (bodies.Count == 0) return;
            List<Collider> colliders = null;
            Dictionary<Collider, Bounds> boundsOf = null;

            foreach (Rigidbody body in bodies.OrderBy(b => b.transform.position.y))
            {
                if (body.destroyed || body.isKinematic || !body.useGravity) continue;
                if ((body.constraints & RigidbodyConstraints.FreezePositionY) != 0) continue;

                if (colliders == null)
                {
                    colliders = ActiveColliders();
                    boundsOf = new Dictionary<Collider, Bounds>();
                    foreach (Collider collider in colliders) if (!collider.isTrigger) boundsOf[collider] = collider.bounds;
                }

                Vector3 velocity = body.linearVelocity;
                if (!BodyBounds(body, colliders, out Bounds box))
                {
                    // No collider: nothing can stop it.
                    velocity.y += Physics.gravity.y * deltaTime;
                    body.linearVelocity = new Vector3(0f, velocity.y, 0f);
                    body.transform.position += new Vector3(0f, velocity.y * deltaTime, 0f);
                    continue;
                }

                // Highest surface below the body (boxes that overlap it in X and Z).
                float supportTop = float.NegativeInfinity;
                Collider support = null;
                bool embedded = false;
                foreach (KeyValuePair<Collider, Bounds> pair in boundsOf)
                {
                    if (ReferenceEquals(pair.Key.attachedRigidbody, body)) continue;
                    Bounds b = pair.Value;
                    if (Overlap(box.min.x, box.max.x, b.min.x, b.max.x) < 0.001f || Overlap(box.min.z, box.max.z, b.min.z, b.max.z) < 0.001f) continue;
                    if (b.max.y <= box.min.y + 0.002f)
                    {
                        if (b.max.y > supportTop) { supportTop = b.max.y; support = pair.Key; }
                    }
                    else if (b.min.y < box.max.y - 0.002f)
                    {
                        embedded = true; // already interpenetrating: PhysX would resolve it, this simulator leaves it in place
                        break;
                    }
                }

                if (embedded)
                {
                    body.linearVelocity = Vector3.zero;
                    continue;
                }

                float gap = box.min.y - supportTop;
                if (support != null && gap <= 0.0005f)
                {
                    body.linearVelocity = Vector3.zero; // resting
                    continue;
                }

                velocity.y += Physics.gravity.y * deltaTime;
                float drop = velocity.y * deltaTime;
                bool landed = support != null && box.min.y + drop <= supportTop;
                if (landed)
                {
                    float impactSpeed = Mathf.Abs(velocity.y);
                    body.transform.position += new Vector3(0f, -gap, 0f);
                    body.linearVelocity = Vector3.zero;
                    SendCollision(body, support, impactSpeed);
                }
                else
                {
                    body.transform.position += new Vector3(0f, drop, 0f);
                    body.linearVelocity = new Vector3(0f, velocity.y, 0f);
                }

                // The body moved: refresh its collider boxes, it may support other bodies.
                foreach (Collider collider in colliders)
                {
                    if (!collider.isTrigger && !collider.destroyed && ReferenceEquals(collider.attachedRigidbody, body)) boundsOf[collider] = collider.bounds;
                }
            }
        }

        private static void SendCollision(Rigidbody body, Collider support, float impactSpeed)
        {
            Collider own = null;
            foreach (Collider collider in body.GetComponentsInChildren<Collider>())
            {
                if (!collider.isTrigger && collider.enabled) { own = collider; break; }
            }

            var forBody = new Collision { collider = support, relativeVelocity = new Vector3(0f, impactSpeed, 0f) };
            foreach (Component component in body.gameObject.components.ToArray())
            {
                if (component is MonoBehaviour behaviour && behaviour.awakeCalled && !behaviour.destroyed) Lifecycle.Call(behaviour, "OnCollisionEnter", forBody);
            }

            if (own == null) return;
            var forSupport = new Collision { collider = own, relativeVelocity = new Vector3(0f, -impactSpeed, 0f) };
            var targets = new List<GameObject> { support.gameObject };
            Rigidbody supportBody = support.attachedRigidbody;
            if (supportBody != null && !ReferenceEquals(supportBody.gameObject, support.gameObject)) targets.Add(supportBody.gameObject);
            foreach (GameObject target in targets)
            {
                foreach (Component component in target.components.ToArray())
                {
                    if (component is MonoBehaviour behaviour && behaviour.awakeCalled && !behaviour.destroyed) Lifecycle.Call(behaviour, "OnCollisionEnter", forSupport);
                }
            }
        }

        // ---------------------------------------------------------------- Trigger events
        /// <summary>
        /// Computes bounding-box overlaps between trigger colliders and other colliders (at least one
        /// side with a Rigidbody) and sends OnTriggerEnter / OnTriggerExit to both sides.
        /// Called once per FixedUpdate step; there is no rigid-body dynamics in this simulator.
        /// </summary>
        public static void UpdateTriggers()
        {
            var colliders = new List<Collider>();
            var triggers = new List<Collider>();
            foreach (GameObject go in AllSceneObjects(false))
            {
                foreach (Component component in go.components)
                {
                    if (component is Collider collider && collider.enabled && !collider.destroyed)
                    {
                        colliders.Add(collider);
                        if (collider.isTrigger) triggers.Add(collider);
                    }
                }
            }

            var current = new HashSet<long>();
            if (triggers.Count > 0)
            {
                var bounds = new Dictionary<Collider, Bounds>();
                foreach (Collider collider in colliders) bounds[collider] = collider.bounds;

                foreach (Collider trigger in triggers)
                {
                    Bounds triggerBounds = bounds[trigger];
                    Rigidbody triggerBody = trigger.attachedRigidbody;
                    foreach (Collider other in colliders)
                    {
                        if (ReferenceEquals(other, trigger)) continue;
                        Rigidbody otherBody = other.attachedRigidbody;
                        if (triggerBody == null && otherBody == null) continue;
                        if (triggerBody != null && ReferenceEquals(triggerBody, otherBody)) continue;
                        if (other.isTrigger && other.GetInstanceID() < trigger.GetInstanceID()) continue; // trigger/trigger pair handled once
                        if (!triggerBounds.Intersects(bounds[other])) continue;

                        long key = ((long)trigger.GetInstanceID() << 32) | (uint)other.GetInstanceID();
                        current.Add(key);
                        if (TriggerPairs.Add(key))
                        {
                            TriggerPairObjects[key] = new KeyValuePair<Collider, Collider>(trigger, other);
                            SendTrigger("OnTriggerEnter", trigger, other);
                            SendTrigger("OnTriggerEnter", other, trigger);
                        }
                        else
                        {
                            SendTrigger("OnTriggerStay", trigger, other);
                            SendTrigger("OnTriggerStay", other, trigger);
                        }
                    }
                }
            }

            if (TriggerPairs.Count == current.Count) return;
            foreach (long key in TriggerPairs.Where(k => !current.Contains(k)).ToArray())
            {
                KeyValuePair<Collider, Collider> pair = TriggerPairObjects[key];
                TriggerPairs.Remove(key);
                TriggerPairObjects.Remove(key);
                bool alive = pair.Key != null && pair.Value != null && pair.Key.gameObject.activeInHierarchy && pair.Value.gameObject.activeInHierarchy;
                if (!alive) continue; // Unity sends no exit for destroyed or deactivated colliders
                SendTrigger("OnTriggerExit", pair.Key, pair.Value);
                SendTrigger("OnTriggerExit", pair.Value, pair.Key);
            }
        }

        private static void SendTrigger(string message, Collider receiver, Collider other)
        {
            var targets = new List<GameObject> { receiver.gameObject };
            Rigidbody body = receiver.attachedRigidbody;
            if (body != null && !ReferenceEquals(body.gameObject, receiver.gameObject)) targets.Add(body.gameObject);
            foreach (GameObject target in targets)
            {
                foreach (Component component in target.components.ToArray())
                {
                    if (component is MonoBehaviour behaviour && behaviour.awakeCalled && !behaviour.destroyed) Lifecycle.Call(behaviour, message, other);
                }
            }
        }

        // ---------------------------------------------------------------- XR helpers (see XR.cs)
        /// <summary>Emulates a hand interactor grabbing the object (selectEntered); releases it from a socket first.</summary>
        public static bool Grab(UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable interactable)
        {
            return UnityEngine.XR.Interaction.Toolkit.XRSim.Grab(interactable);
        }

        /// <summary>Emulates the hand letting go (selectExited).</summary>
        public static bool Release(UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable interactable)
        {
            return UnityEngine.XR.Interaction.Toolkit.XRSim.Release(interactable);
        }

        /// <summary>Emulates a ray select press and release on a simple interactable (a push button).</summary>
        public static bool Select(UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable interactable)
        {
            return UnityEngine.XR.Interaction.Toolkit.XRSim.Select(interactable);
        }

        /// <summary>Emulates a UI click (pointer click through the EventSystem) on a button.</summary>
        public static bool Click(UnityEngine.UI.Button button)
        {
            if (button == null) return false;
            return UnityEngine.EventSystems.ExecuteEvents.Execute(button.gameObject, new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current), UnityEngine.EventSystems.ExecuteEvents.pointerClickHandler);
        }
    }
}
