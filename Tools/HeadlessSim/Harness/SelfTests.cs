// HeadlessSim harness: self-tests of the simulated engine itself. They run before the project is
// touched, so a failure here means the simulator is wrong, not the project. Not part of the Unity project.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace HeadlessSim.SelfTest
{
    [Serializable]
    public struct ProbeEntry
    {
        public string label;
        public Transform anchor;
    }

    /// <summary>Records every Unity message it receives.</summary>
    public class LifecycleProbe : MonoBehaviour
    {
        public readonly List<string> Calls = new List<string>();
        [SerializeField] private Transform target;
        [SerializeField] private float speed = 2f;
        [SerializeField] private ProbeEntry[] entries = new ProbeEntry[0];
        public string publicText = "initial";
        public UnityEvent onPing = new UnityEvent();
        private int runtimeCounter = 5;           // not serialized: must not be cloned
        public int Updates, FixedUpdates, Pings, TriggerEnters, TriggerExits, IntArgument;
        public int RuntimeCounter { get => runtimeCounter; set => runtimeCounter = value; }
        public Transform Target => target;
        public float Speed => speed;
        public ProbeEntry[] Entries => entries;

        private void Awake() { Calls.Add("Awake"); }
        private void OnEnable() { Calls.Add("OnEnable"); }
        private void Start() { Calls.Add("Start"); }
        private void Update() { Updates++; if (Updates == 1) Calls.Add("Update"); }
        private void FixedUpdate() { FixedUpdates++; }
        private void OnDisable() { Calls.Add("OnDisable"); }
        private void OnDestroy() { Calls.Add("OnDestroy"); }
        private void OnTriggerEnter(Collider other) { TriggerEnters++; }
        private void OnTriggerExit(Collider other) { TriggerExits++; }
        public int Collisions;
        public float LastImpactSpeed;
        private void OnCollisionEnter(Collision collision) { Collisions++; LastImpactSpeed = collision.relativeVelocity.magnitude; }
        public void Ping() { Pings++; }
        public void PingWith(int value) { IntArgument = value; }
        private void Tick() { Calls.Add("Tick"); }
        public void StartTick(float delay) { Invoke(nameof(Tick), delay); }
        public IEnumerator Routine()
        {
            Calls.Add("Routine0");
            yield return null;
            Calls.Add("Routine1");
            yield return new WaitForSeconds(0.5f);
            Calls.Add("Routine2");
        }
    }

    public static class EngineSelfTests
    {
        private static int passed, failed;
        private static readonly List<string> Failures = new List<string>();

        private static void Check(bool condition, string what)
        {
            if (condition) { passed++; return; }
            failed++;
            Failures.Add(what);
            Console.WriteLine("    SELF-TEST FAIL  " + what);
        }

        private static bool Near(Vector3 a, Vector3 b, float tolerance = 1e-3f) => (a - b).magnitude <= tolerance;
        private static bool Near(float a, float b, float tolerance = 1e-3f) => Mathf.Abs(a - b) <= tolerance;

        /// <summary>Runs every self-test; returns the failure messages (empty when the engine behaves).</summary>
        public static List<string> Run(out int passedCount)
        {
            passed = 0; failed = 0; Failures.Clear();
            bool echo = LogCapture.Echo;
            LogCapture.Echo = false;
            int exceptionsBefore = Simulation.Exceptions.Count;
            Action[] tests = { MathTests, TransformTests, LifecycleTests, CloneTests, SerializedObjectTests, EventTests, TriggerTests, GravityTests, NavigationTests, SocketTests, UiTests, EditorTests };
            foreach (Action test in tests)
            {
                Simulation.ResetWorld();
                try { test(); }
                catch (Exception exception) { Check(false, $"{test.Method.Name} threw {exception.GetType().Name}: {exception.Message}\n{exception.StackTrace}"); }
            }

            Check(Simulation.Exceptions.Count == exceptionsBefore, "no exception inside a simulated Unity message" +
                  (Simulation.Exceptions.Count > exceptionsBefore ? ": " + Simulation.Exceptions[exceptionsBefore] : ""));
            Simulation.ResetWorld();
            LogCapture.Echo = echo;
            passedCount = passed;
            return new List<string>(Failures);
        }

        private static void MathTests()
        {
            Check(Near(Quaternion.Euler(0f, 90f, 0f) * Vector3.forward, Vector3.right), "Euler(0,90,0) turns forward to right");
            Check(Near(Quaternion.Euler(90f, 0f, 0f) * Vector3.forward, Vector3.down), "Euler(90,0,0) turns forward to down");
            Check(Near(Quaternion.Euler(0f, 0f, 90f) * Vector3.right, Vector3.up), "Euler(0,0,90) turns right to up");
            Quaternion composed = Quaternion.AngleAxis(40f, Vector3.up) * Quaternion.AngleAxis(30f, Vector3.right) * Quaternion.AngleAxis(50f, Vector3.forward);
            Check(Quaternion.Angle(Quaternion.Euler(30f, 40f, 50f), composed) < 0.01f, "Euler order is Z, then X, then Y");
            Vector3 euler = Quaternion.Euler(30f, 40f, 50f).eulerAngles;
            Check(Near(euler, new Vector3(30f, 40f, 50f), 0.01f), "eulerAngles round trip (30,40,50) got " + euler);
            euler = Quaternion.Euler(-20f, 200f, 0f).eulerAngles;
            Check(Near(euler, new Vector3(340f, 200f, 0f), 0.01f), "eulerAngles are reported in 0..360, got " + euler);
            Check(Near(Quaternion.LookRotation(Vector3.right) * Vector3.forward, Vector3.right), "LookRotation points forward at the target");
            Check(Near(Quaternion.LookRotation(Vector3.right) * Vector3.up, Vector3.up), "LookRotation keeps up");
            Check(Near(Quaternion.FromToRotation(Vector3.up, Vector3.right) * Vector3.up, Vector3.right), "FromToRotation");
            Check(Near(Quaternion.Angle(Quaternion.identity, Quaternion.Euler(0f, 37f, 0f)), 37f, 0.01f), "Quaternion.Angle");
            Check(Near(Quaternion.Angle(Quaternion.identity, Quaternion.RotateTowards(Quaternion.identity, Quaternion.Euler(0f, 90f, 0f), 30f)), 30f, 0.01f), "RotateTowards limits the step");
            Check(Near(Quaternion.Inverse(Quaternion.Euler(10f, 20f, 30f)) * (Quaternion.Euler(10f, 20f, 30f) * Vector3.one), Vector3.one), "Inverse undoes a rotation");
            Check(Near(Vector3.SignedAngle(Vector3.forward, Vector3.right, Vector3.up), 90f), "SignedAngle sign convention");
            Check(Near(Vector3.ProjectOnPlane(new Vector3(1f, 2f, 3f), Vector3.up), new Vector3(1f, 0f, 3f)), "ProjectOnPlane");
            Check(Near(Mathf.SmoothStep(0f, 10f, 0.5f), 5f) && Near(Mathf.DeltaAngle(350f, 10f), 20f) && Near(Mathf.Repeat(7.5f, 5f), 2.5f) && Near(Mathf.PingPong(7f, 5f), 3f), "Mathf helpers");
            Check(Near(Mathf.MoveTowards(1f, 5f, 1.5f), 2.5f) && Near(Mathf.InverseLerp(10f, 20f, 15f), 0.5f), "Mathf.MoveTowards / InverseLerp");
            var bounds = new Bounds(Vector3.zero, Vector3.one * 2f);
            Check(bounds.Contains(new Vector3(0.9f, 0f, 0f)) && !bounds.Contains(new Vector3(1.1f, 0f, 0f)) && bounds.Intersects(new Bounds(new Vector3(1.5f, 0f, 0f), Vector3.one * 2f)), "Bounds contains / intersects");
        }

        private static void TransformTests()
        {
            var parent = new GameObject("Parent");
            parent.transform.position = new Vector3(1f, 2f, 3f);
            parent.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
            parent.transform.localScale = Vector3.one * 2f;
            var child = new GameObject("Child");
            child.transform.SetParent(parent.transform, false);
            child.transform.localPosition = new Vector3(1f, 0f, 0f);
            Check(Near(child.transform.position, new Vector3(1f, 2f, 1f)), "child world position through rotated, scaled parent: " + child.transform.position);
            Check(Near(child.transform.lossyScale, Vector3.one * 2f), "lossyScale");
            Check(Near(parent.transform.InverseTransformPoint(child.transform.position), new Vector3(1f, 0f, 0f)), "InverseTransformPoint round trip");
            Check(Near(child.transform.forward, Vector3.right), "child forward follows the parent rotation");

            child.transform.position = new Vector3(5f, 5f, 5f);
            Check(Near(child.transform.position, new Vector3(5f, 5f, 5f)), "setting a world position");
            var other = new GameObject("Other");
            other.transform.position = new Vector3(-3f, 0f, 0f);
            child.transform.SetParent(other.transform, true);
            Check(Near(child.transform.position, new Vector3(5f, 5f, 5f)) && Near(child.transform.lossyScale, Vector3.one * 2f), "SetParent(worldPositionStays) keeps the world pose");
            child.transform.SetParent(null);
            Check(Simulation.RootObjects.Contains(child) && child.transform.parent == null, "un-parenting makes a root object");

            Check(ReferenceEquals(parent.transform.Find("Missing"), null), "Transform.Find returns null for a missing child");
            var a = new GameObject("A"); a.transform.SetParent(parent.transform, false);
            var b = new GameObject("B"); b.transform.SetParent(a.transform, false);
            Check(ReferenceEquals(parent.transform.Find("A/B"), b.transform), "Transform.Find follows a path");
            Check(ReferenceEquals(parent.transform.Find("B"), null), "Transform.Find does not search grandchildren");
            Check(ReferenceEquals(GameObject.Find("B"), b), "GameObject.Find searches the whole scene");
            a.SetActive(false);
            Check(ReferenceEquals(GameObject.Find("B"), null), "GameObject.Find ignores inactive objects");
            Check(!b.activeInHierarchy && b.activeSelf, "activeInHierarchy follows the parents");
            Check(parent.GetComponentsInChildren<Transform>().Length == 1 && parent.GetComponentsInChildren<Transform>(true).Length == 3, "GetComponentsInChildren honours includeInactive");

            var boxObject = new GameObject("Box");
            boxObject.transform.position = new Vector3(10f, 0f, 0f);
            boxObject.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
            var box = boxObject.AddComponent<BoxCollider>();
            box.size = new Vector3(2f, 1f, 4f);
            Check(Near(box.bounds.size, new Vector3(4f, 1f, 2f)), "BoxCollider.bounds of a rotated box: " + box.bounds.size);
            Check(Near(box.bounds.center, new Vector3(10f, 0f, 0f)), "BoxCollider.bounds centre");

            var looker = new GameObject("Looker");
            looker.transform.LookAt(new Vector3(0f, 0f, -5f));
            Check(Near(looker.transform.forward, Vector3.back), "LookAt");

            var canvas = new GameObject("Canvas", typeof(RectTransform));
            var canvasRect = (RectTransform)canvas.transform;
            canvasRect.sizeDelta = new Vector2(1000f, 500f);
            var item = new GameObject("Item", typeof(RectTransform));
            item.transform.SetParent(canvas.transform, false);
            var itemRect = (RectTransform)item.transform;
            itemRect.anchorMin = new Vector2(0f, 1f); itemRect.anchorMax = new Vector2(0f, 1f); itemRect.pivot = new Vector2(0f, 1f);
            itemRect.anchoredPosition = new Vector2(100f, -50f); itemRect.sizeDelta = new Vector2(200f, 80f);
            Check(Near(itemRect.localPosition, new Vector3(-400f, 200f, 0f)), "RectTransform anchored to the top-left corner: " + itemRect.localPosition);
            Check(Near(itemRect.rect.width, 200f) && Near(itemRect.rect.height, 80f), "RectTransform.rect");
        }

        private static void LifecycleTests()
        {
            var editObject = new GameObject("EditTime");
            var editProbe = editObject.AddComponent<LifecycleProbe>();
            Check(editProbe.Calls.Count == 0, "no lifecycle message in edit mode");

            Simulation.EnterPlayMode();
            Check(string.Join(",", editProbe.Calls) == "Awake,OnEnable,Start", "EnterPlayMode runs Awake, OnEnable, Start: " + string.Join(",", editProbe.Calls));
            Simulation.Step(1f / 60f);
            Check(editProbe.Updates == 1, "Update runs once per frame");
            Simulation.RunFor(1f);
            Check(editProbe.FixedUpdates >= 49 && editProbe.FixedUpdates <= 52, "FixedUpdate runs 50 times per second: " + editProbe.FixedUpdates);

            var inactive = new GameObject("Inactive");
            inactive.SetActive(false);
            var late = inactive.AddComponent<LifecycleProbe>();
            Check(late.Calls.Count == 0, "components on an inactive object are not awoken");
            inactive.SetActive(true);
            Check(string.Join(",", late.Calls) == "Awake,OnEnable", "activation runs Awake and OnEnable immediately: " + string.Join(",", late.Calls));
            Simulation.Step(1f / 60f);
            Check(string.Join(",", late.Calls) == "Awake,OnEnable,Start,Update", "Start runs before the first Update");
            late.enabled = false;
            Check(late.Calls.Last() == "OnDisable", "disabling calls OnDisable");
            int updates = late.Updates;
            Simulation.Step(1f / 60f);
            Check(late.Updates == updates, "a disabled behaviour is not updated");
            late.enabled = true;
            Check(late.Calls.Last() == "OnEnable", "enabling calls OnEnable again");

            late.StartTick(0.5f);
            Simulation.RunFor(0.4f);
            Check(!late.Calls.Contains("Tick"), "Invoke waits for its delay");
            Simulation.RunFor(0.2f);
            Check(late.Calls.Contains("Tick"), "Invoke fires after its delay");

            late.StartCoroutine(late.Routine());
            Check(late.Calls.Contains("Routine0") && !late.Calls.Contains("Routine1"), "a coroutine runs to its first yield at once");
            Simulation.Step(1f / 60f);
            Check(late.Calls.Contains("Routine1"), "yield return null resumes next frame");
            Simulation.RunFor(0.6f);
            Check(late.Calls.Contains("Routine2"), "WaitForSeconds resumes after the delay");

            UnityEngine.Object.Destroy(inactive);
            Check(!(inactive == null), "Destroy is deferred to the end of the frame");
            Simulation.Step(1f / 60f);
            Check(inactive == null && late == null, "destroyed objects compare equal to null");
            Check(late.Calls.Contains("OnDestroy") && late.Calls.IndexOf("OnDestroy") > late.Calls.LastIndexOf("OnDisable"), "Destroy calls OnDisable then OnDestroy");
            Check(UnityEngine.Object.FindFirstObjectByType<LifecycleProbe>() == editProbe, "FindFirstObjectByType skips destroyed objects");

            Time.timeScale = 4f;
            float before = Time.time;
            Simulation.Step(0.1f);
            Check(Near(Time.time - before, 0.4f) && Near(Time.deltaTime, 0.4f) && Near(Time.unscaledDeltaTime, 0.1f), "Time.timeScale scales deltaTime");
            Time.timeScale = 1f;
        }

        private static void CloneTests()
        {
            var root = new GameObject("Template");
            var child = new GameObject("Child");
            child.transform.SetParent(root.transform, false);
            child.transform.localPosition = new Vector3(0f, 1f, 0f);
            var probe = root.AddComponent<LifecycleProbe>();
            var so = new SerializedObject(probe);
            so.FindProperty("target").objectReferenceValue = child.transform;
            probe.publicText = "changed";
            probe.RuntimeCounter = 99;
            UnityEventTools.AddPersistentListener(probe.onPing, probe.Ping);
            probe.onPing.AddListener(() => probe.Pings += 100); // runtime listener: must not be cloned
            var outside = new GameObject("Outside");
            var entries = so.FindProperty("entries");
            entries.arraySize = 2;
            entries.GetArrayElementAtIndex(0).FindPropertyRelative("label").stringValue = "inside";
            entries.GetArrayElementAtIndex(0).FindPropertyRelative("anchor").objectReferenceValue = child.transform;
            entries.GetArrayElementAtIndex(1).FindPropertyRelative("label").stringValue = "outside";
            entries.GetArrayElementAtIndex(1).FindPropertyRelative("anchor").objectReferenceValue = outside.transform;

            GameObject clone = UnityEngine.Object.Instantiate(root, new Vector3(3f, 0f, 0f), Quaternion.identity);
            var cloneProbe = clone.GetComponent<LifecycleProbe>();
            Check(clone.name == "Template(Clone)" && clone.transform.childCount == 1, "Instantiate clones the hierarchy");
            Check(Near(clone.transform.position, new Vector3(3f, 0f, 0f)) && Near(clone.transform.GetChild(0).position, new Vector3(3f, 1f, 0f)), "Instantiate applies the pose");
            Check(ReferenceEquals(cloneProbe.Target, clone.transform.GetChild(0)), "references inside the hierarchy are remapped to the clone");
            Check(cloneProbe.publicText == "changed", "public fields are copied");
            Check(cloneProbe.RuntimeCounter == 5, "non-serialized private fields are not copied: " + cloneProbe.RuntimeCounter);
            Check(cloneProbe.Entries.Length == 2 && cloneProbe.Entries[0].label == "inside" && ReferenceEquals(cloneProbe.Entries[0].anchor, clone.transform.GetChild(0)), "struct arrays are deep-copied and remapped");
            Check(ReferenceEquals(cloneProbe.Entries[1].anchor, outside.transform), "references outside the hierarchy are kept");
            Check(cloneProbe.onPing.GetPersistentEventCount() == 1 && ReferenceEquals(cloneProbe.onPing.GetPersistentTarget(0), cloneProbe), "persistent listeners are cloned with their target remapped");

            Simulation.EnterPlayMode();
            cloneProbe.onPing.Invoke();
            Check(cloneProbe.Pings == 1 && probe.Pings == 0, "the cloned listener calls the clone, runtime listeners are not cloned");

            GameObject runtimeClone = UnityEngine.Object.Instantiate(root);
            Check(runtimeClone.GetComponent<LifecycleProbe>().Calls.Contains("Awake"), "Instantiate in play mode awakes the clone");
            var parented = UnityEngine.Object.Instantiate(probe, outside.transform);
            Check(parented != null && ReferenceEquals(parented.transform.parent, outside.transform), "Instantiate(component, parent) returns the cloned component under the parent");
        }

        private static void SerializedObjectTests()
        {
            var go = new GameObject("Serialized");
            var probe = go.AddComponent<LifecycleProbe>();
            var so = new SerializedObject(probe);
            Check(so.FindProperty("doesNotExist") == null, "FindProperty returns null for an unknown field");
            Check(so.FindProperty("runtimeCounter") == null, "FindProperty returns null for a non-serialized private field");
            Check(so.FindProperty("speed") != null, "FindProperty finds a private [SerializeField] field");
            so.FindProperty("speed").floatValue = 7.5f;
            Check(Near(probe.Speed, 7.5f), "floatValue writes the field");
            so.FindProperty("publicText").stringValue = "written";
            Check(probe.publicText == "written", "stringValue writes the field");

            int errorsBefore = LogCapture.Count(LogKind.Error);
            so.FindProperty("target").objectReferenceValue = go.AddComponent<BoxCollider>(); // wrong type
            Check(LogCapture.Count(LogKind.Error) == errorsBefore + 1 && probe.Target == null, "assigning the wrong object type logs an error and is refused");
            so.FindProperty("speed").stringValue = "nope";
            Check(LogCapture.Count(LogKind.Error) == errorsBefore + 2, "assigning the wrong value type logs an error");

            var entries = so.FindProperty("entries");
            entries.arraySize = 3;
            entries.GetArrayElementAtIndex(2).FindPropertyRelative("label").stringValue = "third";
            Check(probe.Entries.Length == 3 && probe.Entries[2].label == "third" && string.IsNullOrEmpty(probe.Entries[0].label), "arrays of structs can be resized and written");

            var box = go.GetComponent<BoxCollider>();
            var boxObject = new SerializedObject(box);
            boxObject.FindProperty("size").vector3Value = new Vector3(1f, 2f, 3f);
            Check(Near(box.size, new Vector3(1f, 2f, 3f)), "vector3Value on an engine component");
        }

        private static void EventTests()
        {
            var go = new GameObject("Events");
            var probe = go.AddComponent<LifecycleProbe>();
            var button = new GameObject("Button", typeof(RectTransform)).AddComponent<Button>();
            UnityEventTools.AddPersistentListener(button.onClick, probe.Ping);
            UnityEventTools.AddIntPersistentListener(button.onClick, probe.PingWith, 42);
            Check(button.onClick.GetPersistentEventCount() == 2 && button.onClick.GetPersistentMethodName(1) == "PingWith", "persistent listeners are recorded");

            button.onClick.Invoke();
            Check(probe.Pings == 0, "runtime-only persistent listeners do not fire in edit mode");
            Simulation.EnterPlayMode();
            button.onClick.Invoke();
            Check(probe.Pings == 1 && probe.IntArgument == 42, "persistent listeners fire in play mode, with their stored argument");

            int errorsBefore = LogCapture.Count(LogKind.Error);
            UnityEventTools.AddPersistentListener(button.onClick, () => { });
            Check(LogCapture.Count(LogKind.Error) == errorsBefore + 1, "a lambda cannot be a persistent listener");

            var typed = new UnityEvent<string>();
            string received = null;
            typed.AddListener(value => received = value);
            typed.Invoke("hello");
            Check(received == "hello", "UnityEvent<T> runtime listener");
        }

        private static void TriggerTests()
        {
            Simulation.EnterPlayMode();
            var zone = new GameObject("Zone");
            var trigger = zone.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            zone.AddComponent<Rigidbody>().isKinematic = true;
            var probe = zone.AddComponent<LifecycleProbe>();

            var item = GameObject.CreatePrimitive(PrimitiveType.Cube);
            item.transform.localScale = Vector3.one * 0.2f;
            item.transform.position = new Vector3(5f, 0f, 0f);
            item.AddComponent<Rigidbody>();
            var staticItem = GameObject.CreatePrimitive(PrimitiveType.Cube);
            staticItem.transform.position = new Vector3(0f, 0.6f, 0f);

            Simulation.RunFor(0.1f);
            Check(probe.TriggerEnters == 1, "a static collider overlapping a trigger that has a Rigidbody raises OnTriggerEnter once: " + probe.TriggerEnters);
            item.transform.position = Vector3.zero;
            Simulation.RunFor(0.1f);
            Check(probe.TriggerEnters == 2, "moving a body into the trigger raises OnTriggerEnter: " + probe.TriggerEnters);
            item.transform.position = new Vector3(5f, 0f, 0f);
            Simulation.RunFor(0.1f);
            Check(probe.TriggerExits == 1, "moving it out raises OnTriggerExit: " + probe.TriggerExits);
        }

        private static void GravityTests()
        {
            Simulation.EnterPlayMode();
            GameObject shelf = GameObject.CreatePrimitive(PrimitiveType.Cube);
            shelf.name = "Shelf";
            shelf.transform.position = new Vector3(0f, 1f, 0f);
            shelf.transform.localScale = new Vector3(1f, 0.02f, 0.4f);

            GameObject jar = GameObject.CreatePrimitive(PrimitiveType.Cube);
            jar.name = "Jar";
            jar.transform.localScale = Vector3.one * 0.1f;
            jar.transform.position = new Vector3(0f, 1.5f, 0f);
            jar.AddComponent<Rigidbody>();
            var probe = jar.AddComponent<LifecycleProbe>();

            GameObject stray = GameObject.CreatePrimitive(PrimitiveType.Cube);
            stray.name = "Stray";
            stray.transform.localScale = Vector3.one * 0.1f;
            stray.transform.position = new Vector3(3f, 1.5f, 0f);
            stray.AddComponent<Rigidbody>();

            GameObject pinned = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pinned.transform.localScale = Vector3.one * 0.1f;
            pinned.transform.position = new Vector3(-3f, 1.5f, 0f);
            pinned.AddComponent<Rigidbody>().isKinematic = true;

            Simulation.RunFor(1f);
            Check(Near(jar.transform.position.y, 1.06f, 0.002f), "a body falls onto the collider below it and rests there: y=" + jar.transform.position.y);
            Check(probe.Collisions == 1 && probe.LastImpactSpeed > 2f, $"landing sends OnCollisionEnter once with the impact speed ({probe.Collisions}, {probe.LastImpactSpeed:0.0} m/s)");
            Check(stray.transform.position.y < -2f, "a body with nothing below keeps falling: y=" + stray.transform.position.y);
            Check(Near(pinned.transform.position.y, 1.5f), "kinematic bodies do not fall");
            Check(Simulation.BodyStates().First(b => b.Body.gameObject == jar).Rest == Simulation.BodyRest.Resting, "BodyStates reports the resting body");
            Check(Simulation.BodyStates().First(b => b.Body.gameObject == stray).Rest == Simulation.BodyRest.Falling, "BodyStates reports the falling body");

            shelf.GetComponent<Collider>().enabled = false;
            Simulation.RunFor(0.5f);
            Check(jar.transform.position.y < 0.5f, "removing the support makes the body fall again");

            jar.GetComponent<Rigidbody>().linearVelocity = Vector3.zero;
            jar.transform.position = new Vector3(0f, 1.0f, 0f);
            shelf.GetComponent<Collider>().enabled = true;
            Check(Simulation.FindPenetrations().Any(p => p.Body.gameObject == jar && p.Other.gameObject == shelf), "FindPenetrations reports a body placed inside a solid collider");
            Simulation.GravityEnabled = false;
            stray.transform.position = new Vector3(3f, 5f, 0f);
            stray.GetComponent<Rigidbody>().linearVelocity = Vector3.zero;
            Simulation.RunFor(0.5f);
            Check(Near(stray.transform.position.y, 5f), "gravity can be switched off");
            Simulation.GravityEnabled = true;
        }

        private static void NavigationTests()
        {
            // 10 x 10 m floor split by a wall with a 2 m gap at the north end.
            var world = new GameObject("NavWorld");
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Floor";
            floor.transform.SetParent(world.transform, false);
            floor.transform.position = new Vector3(0f, -0.5f, 0f);
            floor.transform.localScale = new Vector3(10f, 1f, 10f);
            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "Wall";
            wall.transform.SetParent(world.transform, false);
            wall.transform.position = new Vector3(0f, 1f, -1f);
            wall.transform.localScale = new Vector3(0.2f, 2f, 8f);
            var decoration = GameObject.CreatePrimitive(PrimitiveType.Cube);
            decoration.name = "Ignored";
            decoration.transform.SetParent(world.transform, false);
            decoration.transform.position = new Vector3(-3f, 1f, 0f);
            decoration.AddComponent<NavMeshModifier>().ignoreFromBuild = true;

            var surface = world.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.BuildNavMesh();
            Check(surface.navMeshData != null, "BuildNavMesh produces data");

            Check(NavMesh.SamplePosition(new Vector3(-3f, 0f, 0f), out NavMeshHit hit, 0.5f, NavMesh.AllAreas) && Near(hit.position, new Vector3(-3f, 0f, 0f)), "SamplePosition on the floor (ignored object does not block)");
            Check(!NavMesh.SamplePosition(new Vector3(0f, 0f, -1f), out hit, 0.3f, NavMesh.AllAreas), "SamplePosition fails inside the wall's clearance");
            Check(NavMesh.SamplePosition(new Vector3(0f, 0f, -1f), out hit, 1.5f, NavMesh.AllAreas) && Mathf.Abs(hit.position.x) >= 0.55f, "SamplePosition finds the nearest point outside the clearance: " + hit.position);
            Check(!NavMesh.SamplePosition(new Vector3(30f, 0f, 0f), out hit, 2f, NavMesh.AllAreas), "SamplePosition fails off the mesh");

            var path = new NavMeshPath();
            Check(NavMesh.CalculatePath(new Vector3(-3f, 0f, -3f), new Vector3(3f, 0f, -3f), NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete, "CalculatePath finds a way around the wall");
            float length = 0f;
            for (int i = 1; i < path.corners.Length; i++) length += Vector3.Distance(path.corners[i - 1], path.corners[i]);
            Check(length > 12f && length < 16f && path.corners.Length >= 3, $"the path goes through the gap (length {length:0.0} m, {path.corners.Length} corners)");
            Check(path.corners.Any(c => c.z > 3f), "the path passes north of the wall");

            Simulation.EnterPlayMode();
            var robot = new GameObject("Agent");
            robot.SetActive(false);
            robot.transform.position = new Vector3(-3f, 0f, -3f);
            var agent = robot.AddComponent<NavMeshAgent>();
            agent.speed = 4f; agent.acceleration = 20f; agent.stoppingDistance = 0.05f;
            robot.SetActive(true);
            Check(agent.isOnNavMesh, "an agent placed on the floor is on the NavMesh");
            Check(agent.SetDestination(new Vector3(3f, 0f, -3f)) && agent.pathPending, "SetDestination accepts a reachable target");
            Simulation.Step(1f / 60f);
            Check(!agent.pathPending && agent.hasPath && agent.remainingDistance > 10f, "the agent has a path after one frame");
            Simulation.RunFor(1f);
            Check(agent.velocity.magnitude > 3f, "the agent moves at its speed: " + agent.velocity.magnitude);
            agent.isStopped = true;
            Vector3 stoppedAt = robot.transform.position;
            Simulation.RunFor(0.5f);
            Check(Near(robot.transform.position, stoppedAt) && agent.velocity.magnitude < 0.01f, "isStopped freezes the agent");
            agent.isStopped = false;
            Simulation.RunFor(8f);
            Check(Vector3.Distance(robot.transform.position, new Vector3(3f, 0f, -3f)) < 0.15f, "the agent arrives: " + robot.transform.position);
            Check(agent.remainingDistance <= agent.stoppingDistance + 0.01f && agent.velocity.sqrMagnitude < 0.001f, "arrived: no remaining distance, no velocity");

            // Carving obstacle closes the gap: the far side becomes unreachable (partial path).
            var blocker = new GameObject("Blocker");
            blocker.transform.position = new Vector3(0f, 0f, 4f);
            var obstacle = blocker.AddComponent<NavMeshObstacle>();
            obstacle.shape = NavMeshObstacleShape.Box;
            obstacle.size = new Vector3(0.4f, 2f, 2f);
            obstacle.center = new Vector3(0f, 1f, 0f);
            obstacle.carving = true;
            Check(agent.SetDestination(new Vector3(-3f, 0f, -3f)) && agent.pathStatus == NavMeshPathStatus.PathPartial, "a carving obstacle in the gap makes the path partial: " + agent.pathStatus);
            blocker.SetActive(false);
            Check(agent.SetDestination(new Vector3(-3f, 0f, -3f)) && agent.pathStatus == NavMeshPathStatus.PathComplete, "removing the obstacle restores the complete path");

            var lost = new GameObject("Lost Agent");
            lost.transform.position = new Vector3(40f, 0f, 40f);
            int warnings = LogCapture.Count(LogKind.Warning);
            var lostAgent = lost.AddComponent<NavMeshAgent>();
            Check(!lostAgent.isOnNavMesh && LogCapture.Count(LogKind.Warning) == warnings + 1, "an agent far from the NavMesh is not placed (warning logged)");
            int errors = LogCapture.Count(LogKind.Error);
            Check(!lostAgent.SetDestination(Vector3.zero) && LogCapture.Count(LogKind.Error) == errors + 1, "SetDestination off the NavMesh logs Unity's error and returns false");
            Check(lostAgent.Warp(new Vector3(-2f, 0f, 2f)) && lostAgent.isOnNavMesh, "Warp onto the floor places the agent");
        }

        private static void SocketTests()
        {
            Simulation.EnterPlayMode();
            new GameObject("XR Interaction Manager").AddComponent<XRInteractionManager>();

            var socketObject = new GameObject("Socket");
            socketObject.transform.position = new Vector3(0f, 1f, 0f);
            var zone = socketObject.AddComponent<SphereCollider>();
            zone.isTrigger = true; zone.radius = 0.1f;
            var socket = socketObject.AddComponent<XRSocketInteractor>();
            socket.recycleDelayTime = 0.2f;
            int socketEnters = 0, socketExits = 0;
            socket.selectEntered.AddListener(_ => socketEnters++);
            socket.selectExited.AddListener(_ => socketExits++);

            GameObject tube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tube.name = "Tube";
            tube.transform.localScale = Vector3.one * 0.05f;
            tube.transform.position = new Vector3(2f, 1f, 0f);
            var grab = tube.AddComponent<XRGrabInteractable>();
            var body = tube.GetComponent<Rigidbody>();
            Check(body != null, "XRGrabInteractable requires (adds) a Rigidbody");
            int grabEnters = 0, grabExits = 0;
            grab.selectEntered.AddListener(_ => grabEnters++);
            grab.selectExited.AddListener(_ => grabExits++);

            Simulation.RunFor(0.2f);
            Check(!socket.hasSelection, "a socket ignores objects outside its trigger");

            Check(Simulation.Grab(grab) && grab.isSelected && grabEnters == 1 && !body.useGravity, "Grab selects the object and suspends gravity");
            tube.transform.position = new Vector3(0.02f, 1.02f, 0f);
            Simulation.RunFor(0.2f);
            Check(!socket.hasSelection, "a socket does not take an object that is still held");
            Simulation.Release(grab);
            Check(!grab.isSelected && grabExits == 1 && body.useGravity, "Release deselects and restores gravity");
            Simulation.RunFor(0.2f);
            Check(socket.hasSelection && ReferenceEquals(socket.firstInteractableSelected, grab) && socketEnters == 1 && grabEnters == 2, "the socket takes the released object");
            Check(Near(tube.transform.position, socketObject.transform.position) && !body.useGravity, "the object snaps to the socket and stays there");

            Simulation.Grab(grab);
            Check(!socket.hasSelection && socketExits == 1 && grab.isSelected, "grabbing takes the object out of the socket");
            tube.transform.position = new Vector3(2f, 1f, 0f);
            Simulation.Release(grab);
            Simulation.RunFor(0.5f);
            Check(!socket.hasSelection, "released elsewhere, the socket stays empty");

            var buttonObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var simple = buttonObject.AddComponent<XRSimpleInteractable>();
            int selects = 0, hovers = 0;
            simple.selectEntered.AddListener(args => { selects++; if (args.interactorObject == null || args.interactorObject.transform == null) selects += 100; });
            simple.hoverEntered.AddListener(_ => hovers++);
            Check(Simulation.Select(simple) && selects == 1 && hovers == 1 && !simple.isSelected, "Select emulates a ray press with an interactor that has a transform");
            buttonObject.GetComponent<Collider>().enabled = false;
            Check(!Simulation.Select(simple) && selects == 1, "an interactable without an enabled collider cannot be selected");
        }

        private static void UiTests()
        {
            var canvasObject = new GameObject("Canvas", typeof(RectTransform));
            canvasObject.AddComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            var buttonObject = new GameObject("Button", typeof(RectTransform));
            buttonObject.transform.SetParent(canvasObject.transform, false);
            var image = buttonObject.AddComponent<Image>();
            var button = buttonObject.AddComponent<Button>();
            button.targetGraphic = image;
            int clicks = 0;
            button.onClick.AddListener(() => clicks++);

            Simulation.EnterPlayMode();
            Check(Simulation.Click(button) && clicks == 1, "a UI click invokes Button.onClick");
            button.interactable = false;
            Simulation.Click(button);
            Check(clicks == 1, "a non-interactable button ignores clicks");
            button.interactable = true;
            canvasObject.SetActive(false);
            Check(!Simulation.Click(button) && clicks == 1, "a button on an inactive canvas ignores clicks");
            canvasObject.SetActive(true);
            canvasObject.AddComponent<CanvasGroup>().interactable = false;
            Simulation.Click(button);
            Check(clicks == 1, "a CanvasGroup can disable its buttons");

            var text = new GameObject("Text").AddComponent<TMPro.TextMeshPro>();
            Check(text.transform is RectTransform && text.GetComponent<MeshRenderer>() != null, "TextMeshPro brings a RectTransform and a MeshRenderer");
            text.SetText("{0:1} m", 3.14159f);
            Check(text.text == "3.1 m", "TMP SetText numeric format: " + text.text);
            text.SetText("{0:0} of {1}", 2.6f, 8f);
            Check(text.text == "3 of 8", "TMP SetText with two arguments: " + text.text);
            char[] buffer = "250.0 g____".ToCharArray();
            text.SetCharArray(buffer, 0, 7);
            Check(text.text == "250.0 g", "TMP SetCharArray");
        }

        private static void EditorTests()
        {
            Check(AssetDatabase.IsValidFolder("Assets") && !AssetDatabase.IsValidFolder("Assets/SelfTest"), "AssetDatabase knows the Assets folder");
            AssetDatabase.CreateFolder("Assets", "SelfTest");
            var material = new Material(Shader.Find("Standard"));
            AssetDatabase.CreateAsset(material, "Assets/SelfTest/Test.mat");
            Check(ReferenceEquals(AssetDatabase.LoadAssetAtPath<Material>("Assets/SelfTest/Test.mat"), material) && AssetDatabase.GetAssetPath(material) == "Assets/SelfTest/Test.mat", "CreateAsset / LoadAssetAtPath");
            Check(AssetDatabase.LoadAssetAtPath<Mesh>("Assets/SelfTest/Test.mat") == null, "LoadAssetAtPath checks the type");

            var root = new GameObject("PrefabSource");
            var probe = root.AddComponent<LifecycleProbe>();
            probe.publicText = "from prefab";
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, "Assets/SelfTest/Thing.prefab", out bool success);
            Check(success && prefab != null && prefab.name == "Thing", "SaveAsPrefabAsset");
            Check(UnityEngine.Object.FindObjectsByType<LifecycleProbe>(FindObjectsSortMode.None).Length == 1, "prefab assets are not part of the scene");
            string[] found = AssetDatabase.FindAssets("t:Prefab Thing");
            Check(found.Length == 1 && AssetDatabase.GUIDToAssetPath(found[0]) == "Assets/SelfTest/Thing.prefab", "FindAssets by type and name");

            Simulation.EnterPlayMode();
            int awakesOnAsset = prefab.GetComponent<LifecycleProbe>().Calls.Count;
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            Check(awakesOnAsset == 0 && instance.name == "Thing" && instance.GetComponent<LifecycleProbe>().publicText == "from prefab", "InstantiatePrefab clones the asset into the scene");
            Check(instance.GetComponent<LifecycleProbe>().Calls.Contains("Awake"), "a prefab instance created in play mode is awoken");
            Simulation.ExitPlayMode();

            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), "Assets/SelfTest/Scene.unity");
            int objectsBefore = Simulation.AllSceneObjects(true).Count();
            UnityEditor.SceneManagement.EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Check(Simulation.AllSceneObjects(true).Count() == 0, "NewScene empties the scene");
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/SelfTest/Scene.unity");
            Check(Simulation.AllSceneObjects(true).Count() == objectsBefore, "OpenScene restores the saved objects");
            AssetDatabase.DeleteAsset("Assets/SelfTest/Test.mat");
            AssetDatabase.DeleteAsset("Assets/SelfTest/Thing.prefab");
            AssetDatabase.DeleteAsset("Assets/SelfTest");
        }
    }
}
