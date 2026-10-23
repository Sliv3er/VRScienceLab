// ---------------------------------------------------------------------------
// Minimal stand-ins for the UnityEditor APIs used by Module2SceneBuilder.
// Compile-check only; never imported by Unity.
// ---------------------------------------------------------------------------
using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

namespace UnityEditor
{
    [AttributeUsage(AttributeTargets.Method)]
    public class MenuItem : Attribute
    {
        public MenuItem(string path) { }
        public MenuItem(string path, bool isValidateFunction, int priority) { }
    }

    public enum InteractionMode { UserAction, AutomatedAction }
    public enum NewSceneSetup { EmptyScene, DefaultGameObjects }
    public enum NewSceneMode { Single, Additive }
    public enum OpenSceneMode { Single, Additive, AdditiveWithoutLoading }

    public static class EditorUtility
    {
        public static bool DisplayDialog(string title, string message, string ok) => true;
        public static void SetDirty(UnityEngine.Object o) { }
    }

    public static class EditorApplication
    {
        public static bool ExecuteMenuItem(string path) => false;
    }

    public static class AssetDatabase
    {
        public static bool IsValidFolder(string path) => true;
        public static string CreateFolder(string parent, string name) => "";
        public static T LoadAssetAtPath<T>(string path) where T : UnityEngine.Object => null;
        public static void CreateAsset(UnityEngine.Object asset, string path) { }
        public static string[] FindAssets(string filter) => new string[0];
        public static string GUIDToAssetPath(string guid) => "";
        public static void SaveAssets() { }
        public static void Refresh() { }
    }

    public static class PrefabUtility
    {
        public static UnityEngine.Object InstantiatePrefab(UnityEngine.Object prefab) => prefab;
        public static GameObject SaveAsPrefabAssetAndConnect(GameObject instance, string path, InteractionMode mode, out bool success) { success = true; return instance; }
    }

    public class SerializedProperty
    {
        public UnityEngine.Object objectReferenceValue;
        public string stringValue;
        public float floatValue;
    }

    public class SerializedObject
    {
        public SerializedObject(UnityEngine.Object target) { }
        public SerializedProperty FindProperty(string name) => new SerializedProperty();
        public bool ApplyModifiedPropertiesWithoutUndo() => true;
    }

    namespace SceneManagement
    {
        public static class EditorSceneManager
        {
            public static bool SaveCurrentModifiedScenesIfUserWantsTo() => true;
            public static Scene NewScene(NewSceneSetup setup, NewSceneMode mode) => new Scene();
            public static bool SaveScene(Scene scene, string path) => true;
            public static Scene OpenScene(string path, OpenSceneMode mode) => new Scene();
        }
    }

    namespace Events
    {
        public static class UnityEventTools
        {
            public static void AddPersistentListener(UnityEvent unityEvent, UnityAction call) => unityEvent.AddListener(call);
        }
    }
}
