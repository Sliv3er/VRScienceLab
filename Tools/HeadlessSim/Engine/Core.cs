// HeadlessSim: functional stand-in for UnityEngine.Object / GameObject / Component / Transform.
// Not part of the Unity project.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HeadlessSim;

namespace UnityEngine
{
    public enum HideFlags { None = 0, HideInHierarchy = 1, HideInInspector = 2, DontSaveInEditor = 4, NotEditable = 8, DontSaveInBuild = 16, DontUnloadUnusedAsset = 32, DontSave = 52, HideAndDontSave = 61 }
    public enum FindObjectsSortMode { None, InstanceID }
    public enum FindObjectsInactive { Exclude, Include }
    public enum Space { World, Self }
    public enum PrimitiveType { Sphere, Capsule, Cylinder, Cube, Plane, Quad }

    public class Object
    {
        private static int nextInstanceId = 1000;
        private readonly int instanceId = nextInstanceId++;
        private string objectName = "";
        internal bool destroyed;
        public HideFlags hideFlags;

        public virtual string name { get => objectName; set => objectName = value ?? ""; }
        public int GetInstanceID() => instanceId;
        public int GetEntityId() => instanceId;

        // Unity-style null: a destroyed object compares equal to null.
        public static bool operator ==(Object a, Object b)
        {
            bool aNull = ReferenceEquals(a, null) || a.destroyed;
            bool bNull = ReferenceEquals(b, null) || b.destroyed;
            if (aNull || bNull) return aNull && bNull;
            return ReferenceEquals(a, b);
        }
        public static bool operator !=(Object a, Object b) => !(a == b);
        public static implicit operator bool(Object o) => !(o == null);
        public override bool Equals(object other) => other is Object o ? this == o : (other == null && destroyed);
        public override int GetHashCode() => instanceId;
        public override string ToString() => $"{name} ({GetType().FullName})";

        // ---------------------------------------------------------------- Instantiate
        public static T Instantiate<T>(T original) where T : Object => (T)Cloner.Instantiate(original, null, false, false, Vector3.zero, Quaternion.identity);
        public static T Instantiate<T>(T original, Transform parent) where T : Object => (T)Cloner.Instantiate(original, parent, false, false, Vector3.zero, Quaternion.identity);
        public static T Instantiate<T>(T original, Transform parent, bool worldPositionStays) where T : Object => (T)Cloner.Instantiate(original, parent, worldPositionStays, false, Vector3.zero, Quaternion.identity);
        public static T Instantiate<T>(T original, Vector3 position, Quaternion rotation) where T : Object => (T)Cloner.Instantiate(original, null, false, true, position, rotation);
        public static T Instantiate<T>(T original, Vector3 position, Quaternion rotation, Transform parent) where T : Object => (T)Cloner.Instantiate(original, parent, false, true, position, rotation);
        public static Object Instantiate(Object original) => Cloner.Instantiate(original, null, false, false, Vector3.zero, Quaternion.identity);
        public static Object Instantiate(Object original, Transform parent) => Cloner.Instantiate(original, parent, false, false, Vector3.zero, Quaternion.identity);
        public static Object Instantiate(Object original, Transform parent, bool worldPositionStays) => Cloner.Instantiate(original, parent, worldPositionStays, false, Vector3.zero, Quaternion.identity);
        public static Object Instantiate(Object original, Vector3 position, Quaternion rotation) => Cloner.Instantiate(original, null, false, true, position, rotation);
        public static Object Instantiate(Object original, Vector3 position, Quaternion rotation, Transform parent) => Cloner.Instantiate(original, parent, false, true, position, rotation);

        // ---------------------------------------------------------------- Destroy
        public static void Destroy(Object obj) { Destroy(obj, 0f); }
        public static void Destroy(Object obj, float delay)
        {
            if (ReferenceEquals(obj, null) || obj.destroyed) return;
            if (!Application.isPlaying) { Simulation.DestroyNow(obj); return; }
            Simulation.QueueDestroy(obj, delay);
        }
        public static void DestroyImmediate(Object obj) { DestroyImmediate(obj, false); }
        public static void DestroyImmediate(Object obj, bool allowDestroyingAssets)
        {
            if (ReferenceEquals(obj, null) || obj.destroyed) return;
            Simulation.DestroyNow(obj);
        }
        public static void DontDestroyOnLoad(Object target) { if (target is GameObject go) go.dontDestroyOnLoad = true; else if (target is Component c) c.gameObject.dontDestroyOnLoad = true; }

        // ---------------------------------------------------------------- Find
        public static T FindFirstObjectByType<T>() where T : Object => Simulation.FindObjects(typeof(T), false).Cast<T>().FirstOrDefault();
        public static T FindFirstObjectByType<T>(FindObjectsInactive findObjectsInactive) where T : Object => Simulation.FindObjects(typeof(T), findObjectsInactive == FindObjectsInactive.Include).Cast<T>().FirstOrDefault();
        public static T FindAnyObjectByType<T>() where T : Object => FindFirstObjectByType<T>();
        public static T FindAnyObjectByType<T>(FindObjectsInactive findObjectsInactive) where T : Object => FindFirstObjectByType<T>(findObjectsInactive);
        public static T[] FindObjectsByType<T>(FindObjectsSortMode sortMode) where T : Object => Simulation.FindObjects(typeof(T), false).Cast<T>().ToArray();
        public static T[] FindObjectsByType<T>(FindObjectsInactive findObjectsInactive, FindObjectsSortMode sortMode) where T : Object => Simulation.FindObjects(typeof(T), findObjectsInactive == FindObjectsInactive.Include).Cast<T>().ToArray();
        public static Object FindFirstObjectByType(Type type) => Simulation.FindObjects(type, false).FirstOrDefault();
        public static Object[] FindObjectsByType(Type type, FindObjectsSortMode sortMode) => Simulation.FindObjects(type, false).ToArray();
        public static Object[] FindObjectsByType(Type type, FindObjectsInactive findObjectsInactive, FindObjectsSortMode sortMode) => Simulation.FindObjects(type, findObjectsInactive == FindObjectsInactive.Include).ToArray();
        [Obsolete("Use FindFirstObjectByType")] public static T FindObjectOfType<T>() where T : Object => FindFirstObjectByType<T>();
        [Obsolete("Use FindObjectsByType")] public static T[] FindObjectsOfType<T>() where T : Object => FindObjectsByType<T>(FindObjectsSortMode.None);
    }

    public class ScriptableObject : Object
    {
        public static T CreateInstance<T>() where T : ScriptableObject => (T)Activator.CreateInstance(typeof(T), true);
        public static ScriptableObject CreateInstance(Type type) => (ScriptableObject)Activator.CreateInstance(type, true);
    }

    public sealed class GameObject : Object
    {
        internal readonly List<Component> components = new List<Component>();
        internal bool isAssetRoot;          // root of a prefab asset (not part of the scene)
        internal bool dontDestroyOnLoad;
        private bool selfActive = true;
        private Transform cachedTransform;

        public int layer;
        public string tag = "Untagged";
        public bool isStatic;

        public Transform transform => cachedTransform;
        public GameObject gameObject => this;
        public bool activeSelf => selfActive;
        public UnityEngine.SceneManagement.Scene scene => UnityEngine.SceneManagement.SceneManager.GetActiveScene();

        public bool activeInHierarchy
        {
            get
            {
                for (Transform t = cachedTransform; t != null; t = t.parent)
                {
                    if (!t.gameObject.selfActive) return false;
                }
                return true;
            }
        }

        /// <summary>True when this object belongs to a prefab asset rather than to the scene.</summary>
        internal bool IsAsset
        {
            get
            {
                Transform t = cachedTransform;
                while (t.parent != null) t = t.parent;
                return t.gameObject.isAssetRoot;
            }
        }

        public GameObject() : this("New Game Object") { }
        public GameObject(string name) : this(name, Type.EmptyTypes) { }

        public GameObject(string name, params Type[] componentTypes)
        {
            this.name = name;
            bool rect = componentTypes != null && componentTypes.Any(t => typeof(RectTransform).IsAssignableFrom(t));
            cachedTransform = rect ? new RectTransform() : new Transform();
            cachedTransform.owner = this;
            components.Add(cachedTransform);
            Simulation.RegisterRoot(this);
            if (componentTypes != null)
            {
                foreach (Type type in componentTypes)
                {
                    if (typeof(Transform).IsAssignableFrom(type)) continue;
                    AddComponent(type);
                }
            }
        }

        // Used by the cloner: no scene registration, no lifecycle.
        internal GameObject(string name, bool rectTransform, bool registerRoot)
        {
            this.name = name;
            cachedTransform = rectTransform ? new RectTransform() : new Transform();
            cachedTransform.owner = this;
            components.Add(cachedTransform);
            if (registerRoot) Simulation.RegisterRoot(this);
        }

        public void SetActive(bool value)
        {
            if (selfActive == value) return;
            bool before = activeInHierarchy;
            selfActive = value;
            bool after = activeInHierarchy;
            if (before != after) Simulation.OnActiveInHierarchyChanged(this, after);
        }

        internal void SetActiveRaw(bool value) { selfActive = value; }

        public bool CompareTag(string other) => tag == other;

        // ---------------------------------------------------------------- Components
        public T AddComponent<T>() where T : Component => (T)AddComponent(typeof(T));

        public Component AddComponent(Type type)
        {
            if (type == null) throw new ArgumentNullException(nameof(type));
            if (!typeof(Component).IsAssignableFrom(type)) throw new ArgumentException($"{type} is not a Component.");
            if (type.IsAbstract) throw new ArgumentException($"Cannot add abstract component {type}.");

            if (typeof(Transform).IsAssignableFrom(type))
            {
                if (typeof(RectTransform).IsAssignableFrom(type)) return ConvertToRectTransform();
                Debug.LogError($"Can't add 'Transform' to {name} because a 'Transform' is already added to the game object!");
                return cachedTransform;
            }

            if (type.GetCustomAttribute<DisallowMultipleComponent>(true) != null && GetComponent(type) != null)
            {
                Debug.LogError($"Can't add '{type.Name}' to {name} because a '{type.Name}' is already added to the game object!");
                return null;
            }

            foreach (RequireComponent require in type.GetCustomAttributes<RequireComponent>(true))
            {
                foreach (Type required in require.Types)
                {
                    if (required == null) continue;
                    if (typeof(RectTransform).IsAssignableFrom(required)) { ConvertToRectTransform(); continue; }
                    if (typeof(Transform).IsAssignableFrom(required)) continue;
                    if (GetComponent(required) == null && !required.IsAbstract) AddComponent(required);
                }
            }

            Component component = AddComponentRaw(type);
            Simulation.OnComponentAdded(component);
            return component;
        }

        internal Component AddComponentRaw(Type type)
        {
            var component = (Component)Activator.CreateInstance(type, true);
            component.owner = this;
            components.Add(component);
            return component;
        }

        internal RectTransform ConvertToRectTransform()
        {
            if (cachedTransform is RectTransform existing) return existing;
            Transform old = cachedTransform;
            var rect = new RectTransform();
            rect.owner = this;
            rect.AdoptStateFrom(old);
            components[0] = rect;
            cachedTransform = rect;
            old.destroyed = true;
            return rect;
        }

        public T GetComponent<T>()
        {
            for (int i = 0; i < components.Count; i++)
            {
                if (components[i] is T match && !components[i].destroyed) return match;
            }
            return default(T);
        }

        public Component GetComponent(Type type)
        {
            for (int i = 0; i < components.Count; i++)
            {
                if (type.IsInstanceOfType(components[i]) && !components[i].destroyed) return components[i];
            }
            return null;
        }

        public Component GetComponent(string typeName) => components.FirstOrDefault(c => c.GetType().Name == typeName);

        public bool TryGetComponent<T>(out T component)
        {
            component = GetComponent<T>();
            return component != null;
        }

        public bool TryGetComponent(Type type, out Component component)
        {
            component = GetComponent(type);
            return component != null;
        }

        public T[] GetComponents<T>() => components.Where(c => !c.destroyed).OfType<T>().ToArray();
        public Component[] GetComponents(Type type) => components.Where(c => !c.destroyed && type.IsInstanceOfType(c)).ToArray();
        public void GetComponents<T>(List<T> results) { results.Clear(); results.AddRange(GetComponents<T>()); }

        public T GetComponentInChildren<T>() => GetComponentInChildren<T>(false);
        public T GetComponentInChildren<T>(bool includeInactive)
        {
            foreach (T found in EnumerateInChildren<T>(includeInactive)) return found;
            return default(T);
        }
        public Component GetComponentInChildren(Type type) => GetComponentInChildren(type, false);
        public Component GetComponentInChildren(Type type, bool includeInactive) => EnumerateInChildren<Component>(includeInactive).FirstOrDefault(type.IsInstanceOfType);

        public T[] GetComponentsInChildren<T>() => GetComponentsInChildren<T>(false);
        public T[] GetComponentsInChildren<T>(bool includeInactive) => EnumerateInChildren<T>(includeInactive).ToArray();
        public void GetComponentsInChildren<T>(List<T> results) { results.Clear(); results.AddRange(EnumerateInChildren<T>(false)); }
        public void GetComponentsInChildren<T>(bool includeInactive, List<T> results) { results.Clear(); results.AddRange(EnumerateInChildren<T>(includeInactive)); }
        public Component[] GetComponentsInChildren(Type type, bool includeInactive = false) => EnumerateInChildren<Component>(includeInactive).Where(type.IsInstanceOfType).ToArray();

        internal IEnumerable<T> EnumerateInChildren<T>(bool includeInactive)
        {
            if (!includeInactive && !activeInHierarchy) yield break;
            for (int i = 0; i < components.Count; i++)
            {
                if (components[i] is T match && !components[i].destroyed) yield return match;
            }

            for (int i = 0; i < cachedTransform.childCount; i++)
            {
                GameObject child = cachedTransform.GetChild(i).gameObject;
                if (!includeInactive && !child.selfActive) continue;
                foreach (T found in child.EnumerateInChildren<T>(includeInactive)) yield return found;
            }
        }

        public T GetComponentInParent<T>() => GetComponentInParent<T>(false);
        public T GetComponentInParent<T>(bool includeInactive)
        {
            for (Transform t = cachedTransform; t != null; t = t.parent)
            {
                if (!includeInactive && !t.gameObject.activeInHierarchy) continue;
                T found = t.gameObject.GetComponent<T>();
                if (found != null) return found;
            }
            return default(T);
        }
        public Component GetComponentInParent(Type type) => GetComponentInParent(type, false);
        public Component GetComponentInParent(Type type, bool includeInactive)
        {
            for (Transform t = cachedTransform; t != null; t = t.parent)
            {
                if (!includeInactive && !t.gameObject.activeInHierarchy) continue;
                Component found = t.gameObject.GetComponent(type);
                if (found != null) return found;
            }
            return null;
        }
        public T[] GetComponentsInParent<T>() => GetComponentsInParent<T>(false);
        public T[] GetComponentsInParent<T>(bool includeInactive)
        {
            var results = new List<T>();
            for (Transform t = cachedTransform; t != null; t = t.parent)
            {
                if (!includeInactive && !t.gameObject.activeInHierarchy) continue;
                results.AddRange(t.gameObject.GetComponents<T>());
            }
            return results.ToArray();
        }

        public void SendMessage(string methodName) { SendMessage(methodName, null, SendMessageOptions.RequireReceiver); }
        public void SendMessage(string methodName, object value) { SendMessage(methodName, value, SendMessageOptions.RequireReceiver); }
        public void SendMessage(string methodName, SendMessageOptions options) { SendMessage(methodName, null, options); }
        public void SendMessage(string methodName, object value, SendMessageOptions options)
        {
            foreach (Component component in components.ToArray())
            {
                if (component is MonoBehaviour) Lifecycle.InvokeNamed(component, methodName, value);
            }
        }

        // ---------------------------------------------------------------- Static helpers
        public static GameObject Find(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            bool isPath = name.Contains("/");
            foreach (GameObject go in Simulation.AllSceneObjects(false))
            {
                if (!isPath) { if (go.name == name) return go; }
                else if (PathOf(go) == name.TrimStart('/') || PathOf(go).EndsWith("/" + name.TrimStart('/'))) return go;
            }
            return null;
        }

        private static string PathOf(GameObject go)
        {
            string path = go.name;
            for (Transform t = go.transform.parent; t != null; t = t.parent) path = t.name + "/" + path;
            return path;
        }

        public static GameObject FindWithTag(string tag) => Simulation.AllSceneObjects(false).FirstOrDefault(g => g.tag == tag);
        public static GameObject FindGameObjectWithTag(string tag) => FindWithTag(tag);
        public static GameObject[] FindGameObjectsWithTag(string tag) => Simulation.AllSceneObjects(false).Where(g => g.tag == tag).ToArray();

        public static GameObject CreatePrimitive(PrimitiveType type)
        {
            var go = new GameObject(type.ToString());
            go.AddComponent<MeshFilter>().sharedMesh = PrimitiveMeshes.Get(type);
            go.AddComponent<MeshRenderer>().sharedMaterial = PrimitiveMeshes.DefaultMaterial;
            switch (type)
            {
                case PrimitiveType.Cube: go.AddComponent<BoxCollider>(); break;
                case PrimitiveType.Sphere: go.AddComponent<SphereCollider>().radius = 0.5f; break;
                case PrimitiveType.Capsule:
                case PrimitiveType.Cylinder:
                    var capsule = go.AddComponent<CapsuleCollider>();
                    capsule.radius = 0.5f; capsule.height = 2f; capsule.direction = 1;
                    break;
                default:
                    go.AddComponent<MeshCollider>().sharedMesh = PrimitiveMeshes.Get(type);
                    break;
            }
            return go;
        }
    }

    public enum SendMessageOptions { RequireReceiver, DontRequireReceiver }

    public class Component : Object
    {
        internal GameObject owner;
        // Lifecycle bookkeeping (never cloned: these are not serialized fields).
        internal bool awakeCalled, startCalled, enableActive, startQueued;

        public GameObject gameObject => owner;
        public Transform transform => owner != null ? owner.transform : null;
        public override string name { get => owner != null ? owner.name : base.name; set { if (owner != null) owner.name = value; else base.name = value; } }
        public string tag { get => owner.tag; set => owner.tag = value; }

        public bool CompareTag(string other) => owner.CompareTag(other);
        public T GetComponent<T>() => owner.GetComponent<T>();
        public Component GetComponent(Type type) => owner.GetComponent(type);
        public bool TryGetComponent<T>(out T component) => owner.TryGetComponent(out component);
        public T[] GetComponents<T>() => owner.GetComponents<T>();
        public Component[] GetComponents(Type type) => owner.GetComponents(type);
        public void GetComponents<T>(List<T> results) => owner.GetComponents(results);
        public T GetComponentInChildren<T>() => owner.GetComponentInChildren<T>();
        public T GetComponentInChildren<T>(bool includeInactive) => owner.GetComponentInChildren<T>(includeInactive);
        public Component GetComponentInChildren(Type type) => owner.GetComponentInChildren(type);
        public T[] GetComponentsInChildren<T>() => owner.GetComponentsInChildren<T>();
        public T[] GetComponentsInChildren<T>(bool includeInactive) => owner.GetComponentsInChildren<T>(includeInactive);
        public void GetComponentsInChildren<T>(List<T> results) => owner.GetComponentsInChildren(results);
        public void GetComponentsInChildren<T>(bool includeInactive, List<T> results) => owner.GetComponentsInChildren(includeInactive, results);
        public T GetComponentInParent<T>() => owner.GetComponentInParent<T>();
        public T GetComponentInParent<T>(bool includeInactive) => owner.GetComponentInParent<T>(includeInactive);
        public Component GetComponentInParent(Type type) => owner.GetComponentInParent(type);
        public T[] GetComponentsInParent<T>() => owner.GetComponentsInParent<T>();
        public T[] GetComponentsInParent<T>(bool includeInactive) => owner.GetComponentsInParent<T>(includeInactive);
        public void SendMessage(string methodName) => owner.SendMessage(methodName);
        public void SendMessage(string methodName, object value) => owner.SendMessage(methodName, value);
        public void SendMessage(string methodName, SendMessageOptions options) => owner.SendMessage(methodName, options);

        // Hooks for the built-in components of the simulator (NavMeshAgent, sockets, ...).
        internal virtual void SimAwake() { }
        internal virtual void SimOnEnable() { }
        internal virtual void SimOnDisable() { }
        internal virtual void SimStart() { }
        internal virtual void SimUpdate(float deltaTime) { }
        internal virtual void SimFixedUpdate(float fixedDeltaTime) { }
        internal virtual void SimOnDestroy() { }
        /// <summary>False for components that have no enabled flag of their own.</summary>
        internal virtual bool SimEnabled => true;
        /// <summary>True for built-in components that need SimUpdate / SimFixedUpdate every frame.</summary>
        internal virtual bool SimTicks => false;
    }

    public class Behaviour : Component
    {
        [SerializeField] private bool m_Enabled = true;

        public bool enabled
        {
            get => m_Enabled;
            set
            {
                if (m_Enabled == value) return;
                m_Enabled = value;
                Simulation.OnEnabledChanged(this);
            }
        }

        public bool isActiveAndEnabled => m_Enabled && owner != null && owner.activeInHierarchy;
        internal override bool SimEnabled => m_Enabled;
        internal void SetEnabledRaw(bool value) { m_Enabled = value; }
    }

    public class MonoBehaviour : Behaviour
    {
        public bool useGUILayout = true;

        public void Invoke(string methodName, float time) { Simulation.AddTimer(this, methodName, time, 0f); }
        public void InvokeRepeating(string methodName, float time, float repeatRate) { Simulation.AddTimer(this, methodName, time, Mathf.Max(repeatRate, 0.00001f)); }
        public void CancelInvoke() { Simulation.CancelTimers(this, null); }
        public void CancelInvoke(string methodName) { Simulation.CancelTimers(this, methodName); }
        public bool IsInvoking() => Simulation.HasTimer(this, null);
        public bool IsInvoking(string methodName) => Simulation.HasTimer(this, methodName);

        public Coroutine StartCoroutine(IEnumerator routine) => Simulation.StartCoroutine(this, routine);
        public Coroutine StartCoroutine(string methodName)
        {
            MethodInfo method = Lifecycle.Find(GetType(), methodName);
            return method != null ? Simulation.StartCoroutine(this, (IEnumerator)method.Invoke(this, null)) : null;
        }
        public void StopCoroutine(Coroutine routine) { Simulation.StopCoroutine(routine); }
        public void StopCoroutine(IEnumerator routine) { Simulation.StopCoroutine(this, routine); }
        public void StopAllCoroutines() { Simulation.StopAllCoroutines(this); }
        public static void print(object message) { Debug.Log(message); }
    }

    public class Transform : Component, IEnumerable
    {
        private Transform parentTransform;
        private readonly List<Transform> children = new List<Transform>();
        private Vector3 localPos = Vector3.zero;
        private Quaternion localRot = Quaternion.identity;
        private Vector3 localScl = Vector3.one;

        public bool hasChanged { get; set; }

        internal void AdoptStateFrom(Transform other)
        {
            localPos = other.localPos; localRot = other.localRot; localScl = other.localScl;
            parentTransform = other.parentTransform;
            if (parentTransform != null)
            {
                int index = parentTransform.children.IndexOf(other);
                if (index >= 0) parentTransform.children[index] = this;
            }
            foreach (Transform child in other.children)
            {
                child.parentTransform = this;
                children.Add(child);
            }
            other.children.Clear();
            other.parentTransform = null;
        }

        // ---------------------------------------------------------------- Hierarchy
        public Transform parent
        {
            get => parentTransform;
            set => SetParent(value, true);
        }

        public Transform root
        {
            get
            {
                Transform t = this;
                while (t.parentTransform != null) t = t.parentTransform;
                return t;
            }
        }

        public int childCount => children.Count;
        public Transform GetChild(int index) => children[index];
        public IEnumerator GetEnumerator() => children.ToArray().GetEnumerator();
        internal List<Transform> ChildList => children;

        public void SetParent(Transform newParent) { SetParent(newParent, true); }

        public void SetParent(Transform newParent, bool worldPositionStays)
        {
            if (ReferenceEquals(newParent, parentTransform)) return;
            if (!ReferenceEquals(newParent, null))
            {
                for (Transform t = newParent; t != null; t = t.parentTransform)
                {
                    if (ReferenceEquals(t, this)) { Debug.LogError("Cannot parent a transform to one of its own descendants."); return; }
                }
            }

            bool activeBefore = owner.activeInHierarchy;
            Vector3 worldPosition = position;
            Quaternion worldRotation = rotation;
            Vector3 worldScale = lossyScale;

            if (parentTransform != null) parentTransform.children.Remove(this);
            else Simulation.UnregisterRoot(owner);

            parentTransform = ReferenceEquals(newParent, null) ? null : newParent;

            if (parentTransform != null) parentTransform.children.Add(this);
            else if (!owner.isAssetRoot) Simulation.RegisterRoot(owner);

            if (worldPositionStays)
            {
                position = worldPosition;
                rotation = worldRotation;
                Vector3 parentScale = parentTransform != null ? parentTransform.lossyScale : Vector3.one;
                localScl = new Vector3(SafeDivide(worldScale.x, parentScale.x), SafeDivide(worldScale.y, parentScale.y), SafeDivide(worldScale.z, parentScale.z));
            }

            OnHierarchyChanged();
            bool activeAfter = owner.activeInHierarchy;
            if (activeBefore != activeAfter) Simulation.OnActiveInHierarchyChanged(owner, activeAfter);
        }

        internal void SetParentRaw(Transform newParent)
        {
            if (parentTransform != null) parentTransform.children.Remove(this);
            parentTransform = newParent;
            if (newParent != null) newParent.children.Add(this);
        }

        internal void RemoveFromParentRaw()
        {
            if (parentTransform != null) parentTransform.children.Remove(this);
            parentTransform = null;
        }

        internal virtual void OnHierarchyChanged() { }

        private static float SafeDivide(float a, float b) => Mathf.Abs(b) < 1e-9f ? a : a / b;

        public void DetachChildren()
        {
            foreach (Transform child in children.ToArray()) child.SetParent(null, true);
        }

        public bool IsChildOf(Transform other)
        {
            for (Transform t = this; t != null; t = t.parentTransform)
            {
                if (ReferenceEquals(t, other)) return true;
            }
            return false;
        }

        public int GetSiblingIndex() => parentTransform != null ? parentTransform.children.IndexOf(this) : Simulation.RootIndex(owner);
        public void SetSiblingIndex(int index)
        {
            if (parentTransform == null) return;
            parentTransform.children.Remove(this);
            parentTransform.children.Insert(Mathf.Clamp(index, 0, parentTransform.children.Count), this);
        }
        public void SetAsFirstSibling() { SetSiblingIndex(0); }
        public void SetAsLastSibling() { if (parentTransform != null) SetSiblingIndex(parentTransform.children.Count); }

        /// <summary>Finds a child by name or by a '/'-separated path (direct descendants only, like Unity).</summary>
        public Transform Find(string n)
        {
            if (string.IsNullOrEmpty(n)) return null;
            Transform current = this;
            foreach (string part in n.Split('/'))
            {
                if (part.Length == 0) continue;
                if (part == "..") { current = current.parentTransform; if (current == null) return null; continue; }
                Transform next = null;
                foreach (Transform child in current.children)
                {
                    if (child.name == part) { next = child; break; }
                }
                if (next == null) return null;
                current = next;
            }
            return current;
        }

        // ---------------------------------------------------------------- Local TRS
        public Vector3 localPosition { get => localPos; set { localPos = value; hasChanged = true; } }
        public Quaternion localRotation { get => localRot; set { localRot = value.normalized; hasChanged = true; } }
        public Vector3 localScale { get => localScl; set { localScl = value; hasChanged = true; } }
        public Vector3 localEulerAngles { get => localRot.eulerAngles; set { localRot = Quaternion.Euler(value); hasChanged = true; } }

        // ---------------------------------------------------------------- World TRS
        public Vector3 position
        {
            get => parentTransform != null ? parentTransform.TransformPoint(localPos) : localPos;
            set { localPos = parentTransform != null ? parentTransform.InverseTransformPoint(value) : value; hasChanged = true; }
        }

        public Quaternion rotation
        {
            get => parentTransform != null ? (parentTransform.rotation * localRot).normalized : localRot;
            set { localRot = parentTransform != null ? (Quaternion.Inverse(parentTransform.rotation) * value).normalized : value.normalized; hasChanged = true; }
        }

        public Vector3 eulerAngles { get => rotation.eulerAngles; set => rotation = Quaternion.Euler(value); }

        public Vector3 lossyScale => parentTransform != null ? Vector3.Scale(parentTransform.lossyScale, localScl) : localScl;

        public Vector3 forward { get => rotation * Vector3.forward; set => rotation = Quaternion.LookRotation(value); }
        public Vector3 up { get => rotation * Vector3.up; set => rotation = Quaternion.FromToRotation(Vector3.up, value); }
        public Vector3 right { get => rotation * Vector3.right; set => rotation = Quaternion.FromToRotation(Vector3.right, value); }

        public void SetPositionAndRotation(Vector3 newPosition, Quaternion newRotation) { position = newPosition; rotation = newRotation; }
        public void SetLocalPositionAndRotation(Vector3 newPosition, Quaternion newRotation) { localPosition = newPosition; localRotation = newRotation; }
        public void GetPositionAndRotation(out Vector3 p, out Quaternion r) { p = position; r = rotation; }

        // ---------------------------------------------------------------- Space conversion
        public Vector3 TransformPoint(Vector3 p)
        {
            Vector3 local = localPos + localRot * Vector3.Scale(localScl, p);
            return parentTransform != null ? parentTransform.TransformPoint(local) : local;
        }
        public Vector3 TransformPoint(float x, float y, float z) => TransformPoint(new Vector3(x, y, z));

        public Vector3 InverseTransformPoint(Vector3 p)
        {
            Vector3 inParent = parentTransform != null ? parentTransform.InverseTransformPoint(p) : p;
            Vector3 unrotated = Quaternion.Inverse(localRot) * (inParent - localPos);
            return new Vector3(SafeDivide(unrotated.x, localScl.x), SafeDivide(unrotated.y, localScl.y), SafeDivide(unrotated.z, localScl.z));
        }

        public Vector3 TransformDirection(Vector3 direction) => rotation * direction;
        public Vector3 InverseTransformDirection(Vector3 direction) => Quaternion.Inverse(rotation) * direction;

        public Vector3 TransformVector(Vector3 v)
        {
            Vector3 local = localRot * Vector3.Scale(localScl, v);
            return parentTransform != null ? parentTransform.TransformVector(local) : local;
        }

        public Vector3 InverseTransformVector(Vector3 v)
        {
            Vector3 inParent = parentTransform != null ? parentTransform.InverseTransformVector(v) : v;
            Vector3 unrotated = Quaternion.Inverse(localRot) * inParent;
            return new Vector3(SafeDivide(unrotated.x, localScl.x), SafeDivide(unrotated.y, localScl.y), SafeDivide(unrotated.z, localScl.z));
        }

        public Matrix4x4 localToWorldMatrix => Matrix4x4.TRS(position, rotation, lossyScale);

        // ---------------------------------------------------------------- Motion helpers
        public void Translate(Vector3 translation) { Translate(translation, Space.Self); }
        public void Translate(Vector3 translation, Space relativeTo)
        {
            if (relativeTo == Space.World) position += translation;
            else position += TransformDirection(translation);
        }
        public void Translate(float x, float y, float z) { Translate(new Vector3(x, y, z), Space.Self); }

        public void Rotate(Vector3 eulers) { Rotate(eulers, Space.Self); }
        public void Rotate(Vector3 eulers, Space relativeTo)
        {
            Quaternion delta = Quaternion.Euler(eulers);
            if (relativeTo == Space.Self) localRotation = localRot * delta;
            else rotation = delta * rotation;
        }
        public void Rotate(float x, float y, float z) { Rotate(new Vector3(x, y, z), Space.Self); }
        public void Rotate(float x, float y, float z, Space relativeTo) { Rotate(new Vector3(x, y, z), relativeTo); }
        public void Rotate(Vector3 axis, float angle) { Rotate(axis, angle, Space.Self); }
        public void Rotate(Vector3 axis, float angle, Space relativeTo)
        {
            if (relativeTo == Space.Self) localRotation = localRot * Quaternion.AngleAxis(angle, axis);
            else rotation = Quaternion.AngleAxis(angle, axis) * rotation;
        }

        public void RotateAround(Vector3 point, Vector3 axis, float angle)
        {
            Quaternion q = Quaternion.AngleAxis(angle, axis);
            Vector3 offset = q * (position - point);
            position = point + offset;
            rotation = q * rotation;
        }

        public void LookAt(Transform target) { if (target != null) LookAt(target.position, Vector3.up); }
        public void LookAt(Transform target, Vector3 worldUp) { if (target != null) LookAt(target.position, worldUp); }
        public void LookAt(Vector3 worldPosition) { LookAt(worldPosition, Vector3.up); }
        public void LookAt(Vector3 worldPosition, Vector3 worldUp)
        {
            Vector3 direction = worldPosition - position;
            if (direction.sqrMagnitude < 1e-12f) return;
            rotation = Quaternion.LookRotation(direction, worldUp);
        }
    }

    public class RectTransform : Transform
    {
        public enum Axis { Horizontal, Vertical }
        public enum Edge { Left, Right, Top, Bottom }

        [SerializeField] private Vector2 m_AnchorMin = new Vector2(0.5f, 0.5f);
        [SerializeField] private Vector2 m_AnchorMax = new Vector2(0.5f, 0.5f);
        [SerializeField] private Vector2 m_Pivot = new Vector2(0.5f, 0.5f);
        [SerializeField] private Vector2 m_AnchoredPosition = Vector2.zero;
        [SerializeField] private Vector2 m_SizeDelta = new Vector2(100f, 100f);
        [SerializeField] private bool m_Driven; // true once a layout property has been set (the local position then follows the anchors)

        public Vector2 anchorMin { get => m_AnchorMin; set { m_AnchorMin = value; Layout(); } }
        public Vector2 anchorMax { get => m_AnchorMax; set { m_AnchorMax = value; Layout(); } }
        public Vector2 pivot { get => m_Pivot; set { m_Pivot = value; Layout(); } }
        public Vector2 anchoredPosition { get => m_AnchoredPosition; set { m_AnchoredPosition = value; Layout(); } }
        public Vector3 anchoredPosition3D { get => new Vector3(m_AnchoredPosition.x, m_AnchoredPosition.y, localPosition.z); set { m_AnchoredPosition = new Vector2(value.x, value.y); Layout(); Vector3 p = localPosition; p.z = value.z; base.localPosition = p; } }
        public Vector2 sizeDelta { get => m_SizeDelta; set { m_SizeDelta = value; Layout(); } }

        public Vector2 offsetMin
        {
            get => m_AnchoredPosition - Vector2.Scale(m_SizeDelta, m_Pivot);
            set
            {
                Vector2 offset = value - (m_AnchoredPosition - Vector2.Scale(m_SizeDelta, m_Pivot));
                m_SizeDelta -= offset;
                m_AnchoredPosition += Vector2.Scale(offset, Vector2.one - m_Pivot);
                Layout();
            }
        }

        public Vector2 offsetMax
        {
            get => m_AnchoredPosition + Vector2.Scale(m_SizeDelta, Vector2.one - m_Pivot);
            set
            {
                Vector2 offset = value - (m_AnchoredPosition + Vector2.Scale(m_SizeDelta, Vector2.one - m_Pivot));
                m_SizeDelta += offset;
                m_AnchoredPosition += Vector2.Scale(offset, m_Pivot);
                Layout();
            }
        }

        /// <summary>Rectangle in this transform's local space.</summary>
        public Rect rect
        {
            get
            {
                Vector2 size = m_SizeDelta;
                if (parent is RectTransform parentRect)
                {
                    Vector2 parentSize = parentRect.rect.size;
                    size = Vector2.Scale(parentSize, m_AnchorMax - m_AnchorMin) + m_SizeDelta;
                }
                return new Rect(-size.x * m_Pivot.x, -size.y * m_Pivot.y, size.x, size.y);
            }
        }

        public void SetSizeWithCurrentAnchors(Axis axis, float size)
        {
            Vector2 parentSize = parent is RectTransform parentRect ? parentRect.rect.size : Vector2.zero;
            Vector2 delta = m_SizeDelta;
            if (axis == Axis.Horizontal) delta.x = size - parentSize.x * (m_AnchorMax.x - m_AnchorMin.x);
            else delta.y = size - parentSize.y * (m_AnchorMax.y - m_AnchorMin.y);
            sizeDelta = delta;
        }

        public void GetLocalCorners(Vector3[] fourCornersArray)
        {
            Rect r = rect;
            fourCornersArray[0] = new Vector3(r.xMin, r.yMin, 0f);
            fourCornersArray[1] = new Vector3(r.xMin, r.yMax, 0f);
            fourCornersArray[2] = new Vector3(r.xMax, r.yMax, 0f);
            fourCornersArray[3] = new Vector3(r.xMax, r.yMin, 0f);
        }

        public void GetWorldCorners(Vector3[] fourCornersArray)
        {
            GetLocalCorners(fourCornersArray);
            for (int i = 0; i < 4; i++) fourCornersArray[i] = TransformPoint(fourCornersArray[i]);
        }

        public void ForceUpdateRectTransforms() { Layout(); }

        internal override void OnHierarchyChanged() { if (m_Driven) LayoutSelfOnly(); }

        // Positions the pivot from the anchors, like Unity's RectTransform does inside a RectTransform parent.
        private void Layout()
        {
            m_Driven = true;
            LayoutSelfOnly();
            for (int i = 0; i < childCount; i++)
            {
                if (GetChild(i) is RectTransform child && child.m_Driven) child.Layout();
            }
        }

        private void LayoutSelfOnly()
        {
            if (!(parent is RectTransform parentRect)) return;
            Rect pr = parentRect.rect;
            Vector2 anchorReference = new Vector2(
                pr.x + pr.width * (m_AnchorMin.x + (m_AnchorMax.x - m_AnchorMin.x) * m_Pivot.x),
                pr.y + pr.height * (m_AnchorMin.y + (m_AnchorMax.y - m_AnchorMin.y) * m_Pivot.y));
            Vector3 p = localPosition;
            p.x = anchorReference.x + m_AnchoredPosition.x;
            p.y = anchorReference.y + m_AnchoredPosition.y;
            base.localPosition = p;
        }
    }
}
