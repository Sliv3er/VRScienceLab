// HeadlessSim: UnityEditor API with an in-memory AssetDatabase, reflection-based SerializedObject,
// prefab save / instantiate and scene save / open. Not part of the Unity project.
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using HeadlessSim;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace UnityEditor
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public sealed class MenuItem : Attribute
    {
        public string menuItem;
        public bool validate;
        public int priority;
        public MenuItem(string itemName) { menuItem = itemName; }
        public MenuItem(string itemName, bool isValidateFunction) { menuItem = itemName; validate = isValidateFunction; }
        public MenuItem(string itemName, bool isValidateFunction, int priority) { menuItem = itemName; validate = isValidateFunction; this.priority = priority; }
    }

    [AttributeUsage(AttributeTargets.Class)] public sealed class CustomEditor : Attribute { public CustomEditor(Type inspectedType) { } public CustomEditor(Type inspectedType, bool editorForChildClasses) { } }
    [AttributeUsage(AttributeTargets.Class)] public sealed class InitializeOnLoadAttribute : Attribute { }
    [AttributeUsage(AttributeTargets.Method)] public sealed class InitializeOnLoadMethodAttribute : Attribute { }

    public enum InteractionMode { UserAction, AutomatedAction }
    public enum NewSceneSetup { EmptyScene, DefaultGameObjects }
    public enum NewSceneMode { Single, Additive }
    public enum OpenSceneMode { Single, Additive, AdditiveWithoutLoading }
    [Flags] public enum ImportAssetOptions { Default = 0, ForceUpdate = 1, ForceSynchronousImport = 8, ImportRecursive = 256, DontDownloadFromCacheServer = 8192, ForceUncompressedImport = 16384 }
    public enum ModelImporterMaterialImportMode { None, ImportStandard, ImportViaMaterialDescription }
    public enum ModelImporterAnimationType { None, Legacy, Generic, Human }
    public enum ModelImporterAvatarSetup { NoAvatar, CreateFromThisModel, CopyFromOther }
    public enum TextureImporterCompression { Uncompressed, Compressed, CompressedHQ, CompressedLQ }
    public enum TextureImporterType { Default, NormalMap, GUI, Sprite, Cursor, Cookie, Lightmap }
    public enum PrefabUnpackMode { OutermostRoot, Completely }
    public enum PrefabAssetType { NotAPrefab, Regular, Model, Variant, MissingAsset }
    public enum BuildTarget { StandaloneWindows64 = 19, Android = 13 }

    /// <summary>Simulator configuration for the editor stand-in.</summary>
    public static class EditorSim
    {
        /// <summary>Menu items the simulator can execute (path -> action returning success).</summary>
        public static readonly Dictionary<string, Func<bool>> MenuItems = new Dictionary<string, Func<bool>>
        {
            { "GameObject/XR/XR Origin (VR)", CreateMinimalXROrigin },
        };

        /// <summary>Like XRI's menu item: a minimal rig with a tracked main camera (no interactors).</summary>
        private static bool CreateMinimalXROrigin()
        {
            var origin = new GameObject("XR Origin (VR)");
            var xrOrigin = origin.AddComponent<Unity.XR.CoreUtils.XROrigin>();
            var offset = new GameObject("Camera Offset");
            offset.transform.SetParent(origin.transform, false);
            offset.transform.localPosition = new Vector3(0f, xrOrigin.CameraYOffset, 0f);
            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetParent(offset.transform, false);
            xrOrigin.Camera = cameraObject.AddComponent<Camera>();
            cameraObject.AddComponent<AudioListener>();
            xrOrigin.Origin = origin;
            xrOrigin.CameraFloorOffsetObject = offset;
            return true;
        }

        /// <summary>
        /// Registers stand-ins for the XR Interaction Toolkit "Starter Assets" rig prefab and the simulator
        /// prefab, so builder code that searches for them takes the same path as in a set-up project.
        /// Off by default: a fresh simulator behaves like a project without the samples imported.
        /// </summary>
        public static void RegisterStarterAssetsStandIn(string version = "3.6.1")
        {
            string folder = $"Assets/Samples/XR Interaction Toolkit/{version}/Starter Assets/Prefabs";
            string path = folder + "/XR Origin (XR Rig).prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) return;

            var rig = new GameObject("XR Origin (XR Rig)");
            var xrOrigin = rig.AddComponent<Unity.XR.CoreUtils.XROrigin>();
            var offset = new GameObject("Camera Offset");
            offset.transform.SetParent(rig.transform, false);
            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetParent(offset.transform, false);
            cameraObject.transform.localPosition = new Vector3(0f, 1.6f, 0f);
            xrOrigin.Camera = cameraObject.AddComponent<Camera>();
            cameraObject.AddComponent<AudioListener>();
            xrOrigin.Origin = rig;
            xrOrigin.CameraFloorOffsetObject = offset;
            foreach (string hand in new[] { "Left Controller", "Right Controller" })
            {
                var controller = new GameObject(hand);
                controller.transform.SetParent(offset.transform, false);
                controller.transform.localPosition = new Vector3(hand.StartsWith("Left") ? -0.2f : 0.2f, 1.2f, 0.3f);
                controller.AddComponent<UnityEngine.XR.Interaction.Toolkit.Inputs.Haptics.HapticImpulsePlayer>();
                var interactor = new GameObject("Near-Far Interactor");
                interactor.transform.SetParent(controller.transform, false);
                interactor.AddComponent<UnityEngine.XR.Interaction.Toolkit.Interactors.NearFarInteractor>();
            }

            AssetDatabase.EnsureFolderChain(folder);
            PrefabUtility.SaveAsPrefabAsset(rig, path);
            Object.DestroyImmediate(rig);

            string simulatorFolder = $"Assets/Samples/XR Interaction Toolkit/{version}/XR Device Simulator/XR Interaction Simulator";
            var simulator = new GameObject("XR Interaction Simulator");
            AssetDatabase.EnsureFolderChain(simulatorFolder);
            PrefabUtility.SaveAsPrefabAsset(simulator, simulatorFolder + "/XR Interaction Simulator.prefab");
            Object.DestroyImmediate(simulator);
        }
    }

    public static class AssetDatabase
    {
        private static readonly Dictionary<string, List<Object>> Assets = new Dictionary<string, List<Object>>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<Object, string> Paths = new Dictionary<Object, string>();
        private static readonly HashSet<string> Folders = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Assets", "Packages", "ProjectSettings" };

        /// <summary>Simulator: every asset path currently registered.</summary>
        public static IEnumerable<string> SimAllAssetPaths => Assets.Keys;
        public static int SimAssetCount => Assets.Count;

        private static string Normalize(string path) => (path ?? "").Replace('\\', '/').TrimEnd('/');

        // ---------------------------------------------------------------- Folders
        public static bool IsValidFolder(string path) => Folders.Contains(Normalize(path));

        public static string CreateFolder(string parentFolder, string newFolderName)
        {
            parentFolder = Normalize(parentFolder);
            if (!Folders.Contains(parentFolder))
            {
                Debug.LogError($"AssetDatabase.CreateFolder: parent folder '{parentFolder}' does not exist.");
                return "";
            }
            string path = parentFolder + "/" + newFolderName;
            Folders.Add(path);
            // The project code also writes real files (PNG textures) below the folders it creates.
            try { Directory.CreateDirectory(path); } catch (Exception) { }
            return GuidOf(path);
        }

        internal static void EnsureFolderChain(string path)
        {
            path = Normalize(path);
            if (Folders.Contains(path) || string.IsNullOrEmpty(path)) return;
            int slash = path.LastIndexOf('/');
            if (slash > 0)
            {
                EnsureFolderChain(path.Substring(0, slash));
                CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
            }
            else Folders.Add(path);
        }

        // ---------------------------------------------------------------- Assets
        public static void CreateAsset(Object asset, string path)
        {
            path = Normalize(path);
            if (asset == null) { Debug.LogError("AssetDatabase.CreateAsset: the asset is null."); return; }
            if (Paths.TryGetValue(asset, out string existing) && existing != path)
            {
                Debug.LogError($"Couldn't add object to asset file because the {asset.GetType().Name} '{asset.name}' is already an asset at '{existing}'.");
                return;
            }
            string folder = path.Contains("/") ? path.Substring(0, path.LastIndexOf('/')) : "";
            if (!Folders.Contains(folder))
            {
                Debug.LogError($"AssetDatabase.CreateAsset: parent directory '{folder}' is not a valid folder (create it with AssetDatabase.CreateFolder first).");
                return;
            }
            Register(asset, path);
        }

        internal static void Register(Object asset, string path)
        {
            path = Normalize(path);
            if (Assets.TryGetValue(path, out List<Object> old))
            {
                foreach (Object o in old) Paths.Remove(o);
            }
            Assets[path] = new List<Object> { asset };
            Paths[asset] = path;
            if (string.IsNullOrEmpty(asset.name)) asset.name = Path.GetFileNameWithoutExtension(path);
        }

        public static void AddObjectToAsset(Object objectToAdd, string path)
        {
            path = Normalize(path);
            if (!Assets.TryGetValue(path, out List<Object> list)) { Debug.LogError($"AddObjectToAsset: no asset at '{path}'."); return; }
            list.Add(objectToAdd);
            Paths[objectToAdd] = path;
        }
        public static void AddObjectToAsset(Object objectToAdd, Object assetObject) { AddObjectToAsset(objectToAdd, GetAssetPath(assetObject)); }

        public static T LoadAssetAtPath<T>(string assetPath) where T : Object => LoadAssetAtPath(assetPath, typeof(T)) as T;
        public static Object LoadAssetAtPath(string assetPath, Type type)
        {
            if (!Assets.TryGetValue(Normalize(assetPath), out List<Object> list)) return null;
            foreach (Object asset in list)
            {
                if (asset != null && type.IsInstanceOfType(asset)) return asset;
            }
            return null;
        }
        public static Object LoadMainAssetAtPath(string assetPath) => Assets.TryGetValue(Normalize(assetPath), out List<Object> list) && list.Count > 0 ? list[0] : null;
        public static Object[] LoadAllAssetsAtPath(string assetPath) => Assets.TryGetValue(Normalize(assetPath), out List<Object> list) ? list.Where(o => o != null).ToArray() : new Object[0];
        public static Object[] LoadAllAssetRepresentationsAtPath(string assetPath) => LoadAllAssetsAtPath(assetPath).Skip(1).ToArray();

        public static bool DeleteAsset(string path)
        {
            path = Normalize(path);
            if (Assets.TryGetValue(path, out List<Object> list))
            {
                foreach (Object asset in list) Paths.Remove(asset);
                Assets.Remove(path);
                return true;
            }
            return Folders.Remove(path);
        }

        public static string GetAssetPath(Object assetObject)
        {
            if (assetObject == null) return "";
            if (Paths.TryGetValue(assetObject, out string path)) return path;
            GameObject go = assetObject as GameObject ?? (assetObject as Component)?.gameObject;
            if (go != null && go.IsAsset && Paths.TryGetValue(go.transform.root.gameObject, out string prefabPath)) return prefabPath;
            return "";
        }
        public static string GetAssetPath(int instanceId) => Paths.FirstOrDefault(pair => pair.Key.GetInstanceID() == instanceId).Value ?? "";
        public static bool Contains(Object obj) => !string.IsNullOrEmpty(GetAssetPath(obj));
        public static bool IsMainAsset(Object obj) => Paths.TryGetValue(obj, out string path) && ReferenceEquals(Assets[path][0], obj);

        public static string AssetPathToGUID(string path) => GuidOf(Normalize(path));
        public static string GUIDToAssetPath(string guid)
        {
            foreach (string path in Assets.Keys) if (GuidOf(path) == guid) return path;
            foreach (string folder in Folders) if (GuidOf(folder) == guid) return folder;
            return "";
        }
        private static string GuidOf(string path) => "sim" + ((uint)path.ToLowerInvariant().GetHashCode()).ToString("x8") + path.Length.ToString("x4");

        /// <summary>Supports "t:Type" tokens (t:Prefab, t:Material, t:Mesh, t:Texture2D, t:Scene...) and name words.</summary>
        public static string[] FindAssets(string filter) => FindAssets(filter, null);
        public static string[] FindAssets(string filter, string[] searchInFolders)
        {
            var typeNames = new List<string>();
            var words = new List<string>();
            foreach (string token in (filter ?? "").Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (token.StartsWith("t:", StringComparison.OrdinalIgnoreCase)) typeNames.Add(token.Substring(2));
                else if (!token.Contains(":")) words.Add(token);
            }

            var results = new List<string>();
            foreach (KeyValuePair<string, List<Object>> pair in Assets)
            {
                if (searchInFolders != null && searchInFolders.Length > 0 && !searchInFolders.Any(f => pair.Key.StartsWith(Normalize(f) + "/", StringComparison.OrdinalIgnoreCase))) continue;
                Object main = pair.Value.Count > 0 ? pair.Value[0] : null;
                if (main == null) continue;
                string fileName = Path.GetFileNameWithoutExtension(pair.Key);
                if (words.Any(w => fileName.IndexOf(w, StringComparison.OrdinalIgnoreCase) < 0)) continue;
                if (typeNames.Count > 0 && !typeNames.Any(t => MatchesType(main, pair.Key, t))) continue;
                results.Add(GuidOf(pair.Key));
            }
            return results.ToArray();
        }

        private static bool MatchesType(Object asset, string path, string typeName)
        {
            if (string.Equals(typeName, "Prefab", StringComparison.OrdinalIgnoreCase)) return asset is GameObject && path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase);
            if (string.Equals(typeName, "Scene", StringComparison.OrdinalIgnoreCase)) return path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase);
            if (string.Equals(typeName, "Model", StringComparison.OrdinalIgnoreCase)) return path.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase);
            for (Type t = asset.GetType(); t != null; t = t.BaseType)
            {
                if (string.Equals(t.Name, typeName, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        public static void ImportAsset(string path) { ImportAsset(path, ImportAssetOptions.Default); }
        public static void ImportAsset(string path, ImportAssetOptions options)
        {
            path = Normalize(path);
            string extension = Path.GetExtension(path).ToLowerInvariant();
            if ((extension == ".png" || extension == ".jpg" || extension == ".tga") && !Assets.ContainsKey(path))
            {
                Register(new Texture2D(4, 4) { name = Path.GetFileNameWithoutExtension(path) }, path);
            }
            else if (extension == ".exr" && !Assets.ContainsKey(path))
            {
                Register(new Cubemap { name = Path.GetFileNameWithoutExtension(path) }, path);
            }
        }
        public static void ImportPackage(string packagePath, bool interactive) { }
        public static void SaveAssets() { }
        public static void SaveAssetIfDirty(Object obj) { }
        public static void Refresh() { }
        public static void Refresh(ImportAssetOptions options) { }
        public static void StartAssetEditing() { }
        public static void StopAssetEditing() { }
        public static bool CopyAsset(string path, string newPath)
        {
            Object source = LoadMainAssetAtPath(path);
            if (source == null) return false;
            Register(Object.Instantiate(source), newPath);
            return true;
        }
        public static string MoveAsset(string oldPath, string newPath)
        {
            oldPath = Normalize(oldPath); newPath = Normalize(newPath);
            if (!Assets.TryGetValue(oldPath, out List<Object> list)) return "No asset at " + oldPath;
            Assets.Remove(oldPath);
            Assets[newPath] = list;
            foreach (Object asset in list) Paths[asset] = newPath;
            return "";
        }
        public static string GenerateUniqueAssetPath(string path)
        {
            path = Normalize(path);
            if (!Assets.ContainsKey(path)) return path;
            string directory = Path.GetDirectoryName(path).Replace('\\', '/'), stem = Path.GetFileNameWithoutExtension(path), extension = Path.GetExtension(path);
            for (int i = 1; ; i++)
            {
                string candidate = $"{directory}/{stem} {i}{extension}";
                if (!Assets.ContainsKey(candidate)) return candidate;
            }
        }

        public static T GetBuiltinExtraResource<T>(string path) where T : Object
        {
            if (typeof(T) == typeof(Sprite)) return Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f)) as T;
            if (typeof(T) == typeof(Material)) return new Material(Shader.Find("Standard")) { name = Path.GetFileNameWithoutExtension(path) } as T;
            return null;
        }
        public static Object GetBuiltinExtraResource(Type type, string path) => null;
    }

    public static class PrefabUtility
    {
        public static GameObject SaveAsPrefabAsset(GameObject instanceRoot, string assetPath) => SaveAsPrefabAsset(instanceRoot, assetPath, out bool _);

        public static GameObject SaveAsPrefabAsset(GameObject instanceRoot, string assetPath, out bool success)
        {
            success = false;
            if (instanceRoot == null) { Debug.LogError("PrefabUtility.SaveAsPrefabAsset: the instance root is null."); return null; }
            if (!assetPath.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase)) { Debug.LogError($"PrefabUtility.SaveAsPrefabAsset: '{assetPath}' is not a prefab path."); return null; }
            string folder = assetPath.Replace('\\', '/');
            folder = folder.Substring(0, folder.LastIndexOf('/'));
            if (!AssetDatabase.IsValidFolder(folder)) { Debug.LogError($"PrefabUtility.SaveAsPrefabAsset: folder '{folder}' does not exist."); return null; }

            var map = new Dictionary<Object, Object>();
            GameObject asset = Cloner.CloneHierarchy(instanceRoot, map);
            asset.isAssetRoot = true;
            asset.name = Path.GetFileNameWithoutExtension(assetPath);
            asset.transform.localPosition = instanceRoot.transform.localPosition;
            AssetDatabase.Register(asset, assetPath);
            success = true;
            return asset;
        }

        public static GameObject SaveAsPrefabAssetAndConnect(GameObject instanceRoot, string assetPath, InteractionMode action) => SaveAsPrefabAssetAndConnect(instanceRoot, assetPath, action, out bool _);
        public static GameObject SaveAsPrefabAssetAndConnect(GameObject instanceRoot, string assetPath, InteractionMode action, out bool success)
        {
            GameObject asset = SaveAsPrefabAsset(instanceRoot, assetPath, out success);
            if (success) Sources[instanceRoot] = asset;
            return asset;
        }

        private static readonly Dictionary<GameObject, GameObject> Sources = new Dictionary<GameObject, GameObject>();

        public static Object InstantiatePrefab(Object assetComponentOrGameObject) => InstantiatePrefab(assetComponentOrGameObject, (Transform)null);
        public static Object InstantiatePrefab(Object assetComponentOrGameObject, Scene destinationScene) => InstantiatePrefab(assetComponentOrGameObject, (Transform)null);
        public static Object InstantiatePrefab(Object assetComponentOrGameObject, Transform parent)
        {
            GameObject source = assetComponentOrGameObject as GameObject ?? (assetComponentOrGameObject as Component)?.gameObject;
            if (source == null) return null;
            Object clone = parent != null ? Object.Instantiate(assetComponentOrGameObject, parent) : Object.Instantiate(assetComponentOrGameObject);
            GameObject cloneObject = clone as GameObject ?? ((Component)clone).gameObject;
            cloneObject.name = source.name;
            Sources[cloneObject] = source;
            return clone;
        }

        public static bool IsPartOfPrefabAsset(Object componentOrGameObject)
        {
            GameObject go = componentOrGameObject as GameObject ?? (componentOrGameObject as Component)?.gameObject;
            return go != null && go.IsAsset;
        }
        public static bool IsPartOfPrefabInstance(Object componentOrGameObject)
        {
            GameObject go = componentOrGameObject as GameObject ?? (componentOrGameObject as Component)?.gameObject;
            for (Transform t = go != null ? go.transform : null; t != null; t = t.parent) if (Sources.ContainsKey(t.gameObject)) return true;
            return false;
        }
        public static bool IsPartOfAnyPrefab(Object o) => IsPartOfPrefabAsset(o) || IsPartOfPrefabInstance(o);
        public static T GetCorrespondingObjectFromSource<T>(T componentOrGameObject) where T : Object
        {
            var go = componentOrGameObject as GameObject;
            return go != null && Sources.TryGetValue(go, out GameObject source) ? source as T : null;
        }
        public static GameObject GetOutermostPrefabInstanceRoot(Object o)
        {
            GameObject go = o as GameObject ?? (o as Component)?.gameObject;
            GameObject result = null;
            for (Transform t = go != null ? go.transform : null; t != null; t = t.parent) if (Sources.ContainsKey(t.gameObject)) result = t.gameObject;
            return result;
        }
        public static string GetPrefabAssetPathOfNearestInstanceRoot(Object o)
        {
            GameObject root = GetOutermostPrefabInstanceRoot(o);
            return root != null ? AssetDatabase.GetAssetPath(Sources[root]) : "";
        }
        public static PrefabAssetType GetPrefabAssetType(Object o) => IsPartOfAnyPrefab(o) ? PrefabAssetType.Regular : PrefabAssetType.NotAPrefab;
        public static void UnpackPrefabInstance(GameObject instanceRoot, PrefabUnpackMode unpackMode, InteractionMode action) { Sources.Remove(instanceRoot); }
        public static void RecordPrefabInstancePropertyModifications(Object targetObject) { }
        public static void ApplyPrefabInstance(GameObject instanceRoot, InteractionMode action) { }
    }

    public static class EditorUtility
    {
        public static bool DisplayDialog(string title, string message, string ok)
        {
            Debug.Log($"[Dialog] {title}: {message}");
            return true;
        }
        public static bool DisplayDialog(string title, string message, string ok, string cancel) => DisplayDialog(title, message, ok);
        public static void SetDirty(Object target) { }
        public static void ClearDirty(Object target) { }
        public static bool IsDirty(Object target) => false;
        public static bool IsPersistent(Object target) => AssetDatabase.Contains(target);
        public static void DisplayProgressBar(string title, string info, float progress) { }
        public static bool DisplayCancelableProgressBar(string title, string info, float progress) => false;
        public static void ClearProgressBar() { }
        public static void FocusProjectWindow() { }
        public static void RevealInFinder(string path) { }
        public static string OpenFilePanel(string title, string directory, string extension) => "";
        public static string SaveFilePanel(string title, string directory, string defaultName, string extension) => "";

        public static void CopySerialized(Object source, Object dest)
        {
            if (source is Mesh sourceMesh && dest is Mesh destMesh) { destMesh.CopyFrom(sourceMesh); return; }
            if (source == null || dest == null || source.GetType() != dest.GetType())
            {
                Debug.LogError("EditorUtility.CopySerialized: source and destination must be of the same type.");
                return;
            }
            Serialization.CopySerializedFields(source, dest, null);
        }
    }

    public static class EditorApplication
    {
        public static bool isPlaying { get => Application.isPlaying; set { if (value) Simulation.EnterPlayMode(); else Simulation.ExitPlayMode(); } }
        public static bool isPlayingOrWillChangePlaymode => Application.isPlaying;
        public static bool isCompiling => false;
        public static bool isUpdating => false;
        public static double timeSinceStartup => Time.unscaledTime;
        public static event Action delayCall { add { value?.Invoke(); } remove { } }
        public static event Action update { add { } remove { } }

        public static bool ExecuteMenuItem(string menuItemPath)
        {
            if (EditorSim.MenuItems.TryGetValue(menuItemPath, out Func<bool> action)) return action();
            // Project menu items declared with [MenuItem].
            foreach (Type type in typeof(EditorApplication).Assembly.GetTypes())
            {
                foreach (MethodInfo method in type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    foreach (MenuItem item in method.GetCustomAttributes<MenuItem>())
                    {
                        if (item.menuItem != menuItemPath || item.validate || method.GetParameters().Length != 0) continue;
                        method.Invoke(null, null);
                        return true;
                    }
                }
            }
            Debug.LogWarning($"[HeadlessSim] ExecuteMenuItem: '{menuItemPath}' is not available in the simulator.");
            return false;
        }
        public static void Exit(int returnValue) { }
        public static void Beep() { }
    }

    public class EditorBuildSettingsScene
    {
        public string path;
        public bool enabled;
        public EditorBuildSettingsScene() { }
        public EditorBuildSettingsScene(string path, bool enabled) { this.path = path; this.enabled = enabled; }
    }

    public static class EditorBuildSettings
    {
        private static EditorBuildSettingsScene[] sceneList = new EditorBuildSettingsScene[0];
        public static EditorBuildSettingsScene[] scenes
        {
            get => (EditorBuildSettingsScene[])sceneList.Clone();
            set
            {
                sceneList = value ?? new EditorBuildSettingsScene[0];
                SceneManager.BuildScenes.Clear();
                foreach (EditorBuildSettingsScene scene in sceneList) if (scene != null && scene.enabled) SceneManager.BuildScenes.Add(scene.path);
            }
        }
    }

    public static class PlayerSettings
    {
        public static ColorSpace colorSpace { get; set; } = ColorSpace.Linear;
        public static string productName { get; set; } = "VRScienceLab";
        public static string companyName { get; set; } = "";
        public static bool runInBackground { get; set; } = true;
    }

    public static class FileUtil
    {
        public static void CopyFileOrDirectory(string source, string dest) { }
        public static bool DeleteFileOrDirectory(string path) => false;
        public static void ReplaceFile(string src, string dst) { }
    }

    public static class Lightmapping
    {
        public static bool isRunning => false;
        /// <summary>No renderer in the simulator: the bake reports failure, which project code must handle.</summary>
        public static bool BakeReflectionProbe(ReflectionProbe probe, string path) => false;
        public static bool Bake() => false;
        public static bool BakeAsync() => false;
        public static void Clear() { }
    }

    [Flags] public enum StaticEditorFlags { ContributeGI = 1, OccluderStatic = 2, BatchingStatic = 4, NavigationStatic = 8, OccludeeStatic = 16, OffMeshLinkGeneration = 32, ReflectionProbeStatic = 64 }

    public static class GameObjectUtility
    {
        private static readonly Dictionary<GameObject, StaticEditorFlags> Flags = new Dictionary<GameObject, StaticEditorFlags>();
        public static void SetStaticEditorFlags(GameObject go, StaticEditorFlags flags) { Flags[go] = flags; go.isStatic = flags != 0; }
        public static StaticEditorFlags GetStaticEditorFlags(GameObject go) => Flags.TryGetValue(go, out StaticEditorFlags flags) ? flags : 0;
        public static bool AreStaticEditorFlagsSet(GameObject go, StaticEditorFlags flags) => (GetStaticEditorFlags(go) & flags) == flags;
        public static void SetParentAndAlign(GameObject child, GameObject parent)
        {
            if (parent == null) return;
            child.transform.SetParent(parent.transform, false);
            child.layer = parent.layer;
        }
        public static string GetUniqueNameForSibling(Transform parent, string name) => name;
        public static int RemoveMonoBehavioursWithMissingScript(GameObject go) => 0;
    }

    public static class Selection
    {
        public static Object activeObject { get; set; }
        public static GameObject activeGameObject { get; set; }
        public static Transform activeTransform { get; set; }
        public static Object[] objects { get; set; } = new Object[0];
        public static GameObject[] gameObjects => objects.OfType<GameObject>().ToArray();
    }

    public static class Undo
    {
        public static void RecordObject(Object objectToUndo, string name) { }
        public static void RegisterCreatedObjectUndo(Object objectToUndo, string name) { }
        public static void DestroyObjectImmediate(Object objectToUndo) { Object.DestroyImmediate(objectToUndo); }
        public static T AddComponent<T>(GameObject gameObject) where T : Component => gameObject.AddComponent<T>();
        public static void SetTransformParent(Transform transform, Transform newParent, string name) { transform.SetParent(newParent, true); }
        public static void RegisterCompleteObjectUndo(Object objectToUndo, string name) { }
    }

    public static class ObjectFactory
    {
        public static GameObject CreateGameObject(string name, params Type[] types) => new GameObject(name, types);
        public static T AddComponent<T>(GameObject gameObject) where T : Component => gameObject.AddComponent<T>();
        public static Component AddComponent(GameObject gameObject, Type type) => gameObject.AddComponent(type);
        public static GameObject CreatePrimitive(PrimitiveType type) => GameObject.CreatePrimitive(type);
    }

    public class AssetImporter : Object
    {
        public string assetPath { get; internal set; } = "";
        public string userData { get; set; } = "";
        /// <summary>The simulator has no importers: callers must handle null (missing / unsupported asset).</summary>
        public static AssetImporter GetAtPath(string path) => null;
        public void SaveAndReimport() { }
    }

    public class ModelImporterClipAnimation
    {
        public string name = "";
        public string takeName = "";
        public float firstFrame, lastFrame;
        public bool loopTime, loop, loopPose;
        public WrapMode wrapMode;
    }

    public class ModelImporter : AssetImporter
    {
        public ModelImporterClipAnimation[] clipAnimations = new ModelImporterClipAnimation[0];
        public ModelImporterClipAnimation[] defaultClipAnimations => new ModelImporterClipAnimation[0];
        public ModelImporterMaterialImportMode materialImportMode;
        public ModelImporterAnimationType animationType = ModelImporterAnimationType.Generic;
        public ModelImporterAvatarSetup avatarSetup;
        public HumanDescription humanDescription;
        public Avatar sourceAvatar;
        public float globalScale = 1f;
        public bool importAnimation = true, importBlendShapes = true, isReadable;
        public bool useFileScale = true;
    }

    public class TextureImporter : AssetImporter
    {
        public TextureWrapMode wrapMode;
        public FilterMode filterMode;
        public bool mipmapEnabled = true;
        public int anisoLevel = 1;
        public int maxTextureSize = 2048;
        public bool sRGBTexture = true;
        public bool alphaIsTransparency;
        public bool isReadable;
        public TextureImporterCompression textureCompression;
        public TextureImporterType textureType;
    }

    namespace PackageManager
    {
        public class PackageInfo
        {
            public string name = "", version = "", resolvedPath = "", displayName = "", assetPath = "";
            public static PackageInfo FindForAssetPath(string assetPath) => null;
            public static PackageInfo FindForAssembly(Assembly assembly) => null;
        }
    }

    // ---------------------------------------------------------------- SerializedObject
    public enum SerializedPropertyType { Generic = -1, Integer = 0, Boolean = 1, Float = 2, String = 3, Color = 4, ObjectReference = 5, LayerMask = 6, Enum = 7, Vector2 = 8, Vector3 = 9, Vector4 = 10, Rect = 11, ArraySize = 12, Quaternion = 17 }

    public sealed class SerializedObject : IDisposable
    {
        public Object targetObject { get; }
        public Object[] targetObjects => new[] { targetObject };
        public bool hasModifiedProperties => false;

        public SerializedObject(Object obj)
        {
            if (obj == null) throw new ArgumentException("Object at index 0 is null");
            targetObject = obj;
        }

        /// <summary>Null when no serialized field has that name, exactly like Unity.</summary>
        public SerializedProperty FindProperty(string propertyPath)
        {
            if (string.IsNullOrEmpty(propertyPath)) return null;
            SerializedProperty current = null;
            foreach (string part in propertyPath.Split('.'))
            {
                if (part == "Array") continue;
                if (part.StartsWith("data[") && current != null)
                {
                    int index = int.Parse(part.Substring(5, part.Length - 6));
                    current = current.GetArrayElementAtIndex(index);
                    continue;
                }
                current = current == null ? SerializedProperty.ForField(targetObject, part, () => targetObject, _ => { }, part) : current.FindPropertyRelative(part);
                if (current == null) return null;
            }
            return current;
        }

        public SerializedProperty GetIterator() => null;
        public void Update() { }
        public void UpdateIfRequiredOrScript() { }
        public bool ApplyModifiedProperties() => true;
        public bool ApplyModifiedPropertiesWithoutUndo() => true;
        public void Dispose() { }
    }

    public sealed class SerializedProperty
    {
        private readonly Type type;
        private readonly Func<object> getter;
        private readonly Action<object> setter;
        public string name { get; }
        public string propertyPath { get; }
        public string displayName => name;

        private SerializedProperty(Type type, Func<object> getter, Action<object> setter, string name, string path)
        {
            this.type = type; this.getter = getter; this.setter = setter; this.name = name; propertyPath = path;
        }

        internal static SerializedProperty ForField(object probe, string fieldName, Func<object> container, Action<object> writeBack, string path)
        {
            if (probe == null) return null;
            FieldInfo field = Serialization.FindSerializedField(probe.GetType(), fieldName);
            if (field == null) return null;
            return new SerializedProperty(field.FieldType,
                () => field.GetValue(container()),
                value =>
                {
                    object owner = container();
                    field.SetValue(owner, value);
                    writeBack(owner); // propagates struct copies up to the owning object
                },
                fieldName, path);
        }

        public SerializedProperty FindPropertyRelative(string relativePropertyPath)
        {
            object value = getter();
            if (value == null)
            {
                if (type.IsValueType || type.IsArray || typeof(Object).IsAssignableFrom(type) || type == typeof(string)) return null;
                value = Activator.CreateInstance(type, true);
                setter(value);
            }
            if (value is Object) return null; // object references have no relative properties
            return ForField(value, relativePropertyPath, getter, setter, propertyPath + "." + relativePropertyPath);
        }

        public SerializedProperty Copy() => new SerializedProperty(type, getter, setter, name, propertyPath);

        // ---------------------------------------------------------------- Arrays
        public bool isArray => type.IsArray || (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>));
        private Type ElementType => type.IsArray ? type.GetElementType() : type.GetGenericArguments()[0];

        public int arraySize
        {
            get
            {
                if (!isArray) throw new InvalidOperationException($"Property '{propertyPath}' is not an array.");
                return getter() is IList list ? list.Count : 0;
            }
            set
            {
                if (!isArray) throw new InvalidOperationException($"Property '{propertyPath}' is not an array.");
                Type elementType = ElementType;
                var old = getter() as IList;
                int oldCount = old != null ? old.Count : 0;
                IList resized = type.IsArray ? Array.CreateInstance(elementType, Math.Max(value, 0)) : (IList)Activator.CreateInstance(type);
                for (int i = 0; i < value; i++)
                {
                    object element;
                    if (i < oldCount) element = old[i];
                    else if (oldCount > 0) element = Serialization.CloneValue(old[oldCount - 1], null); // Unity duplicates the last element
                    else element = DefaultElement(elementType);
                    if (type.IsArray) resized[i] = element; else resized.Add(element);
                }
                setter(resized);
            }
        }

        private static object DefaultElement(Type elementType)
        {
            if (elementType.IsValueType) return Activator.CreateInstance(elementType);
            if (elementType == typeof(string)) return "";
            if (typeof(Object).IsAssignableFrom(elementType)) return null;
            return Activator.CreateInstance(elementType, true);
        }

        public SerializedProperty GetArrayElementAtIndex(int index)
        {
            if (!isArray) throw new InvalidOperationException($"Property '{propertyPath}' is not an array.");
            if (!(getter() is IList list) || index < 0 || index >= list.Count) throw new IndexOutOfRangeException($"Index {index} is out of range for '{propertyPath}'.");
            return new SerializedProperty(ElementType,
                () => ((IList)getter())[index],
                value =>
                {
                    var owner = (IList)getter();
                    owner[index] = value;
                    setter(owner);
                },
                "data", $"{propertyPath}.Array.data[{index}]");
        }

        public void InsertArrayElementAtIndex(int index) { arraySize = arraySize + 1; }
        public void DeleteArrayElementAtIndex(int index)
        {
            var list = new ArrayList((IList)getter());
            list.RemoveAt(index);
            IList resized = type.IsArray ? Array.CreateInstance(ElementType, list.Count) : (IList)Activator.CreateInstance(type);
            for (int i = 0; i < list.Count; i++) { if (type.IsArray) resized[i] = list[i]; else resized.Add(list[i]); }
            setter(resized);
        }
        public void ClearArray() { arraySize = 0; }

        // ---------------------------------------------------------------- Typed values
        private T Read<T>(string accessor)
        {
            object value = getter();
            if (value is T typed) return typed;
            if (value == null && !typeof(T).IsValueType) return default(T);
            throw new InvalidOperationException($"SerializedProperty '{propertyPath}' is a {type.Name}; .{accessor} cannot read it.");
        }

        private void Write(object value, Type expected, string accessor)
        {
            if (!type.IsAssignableFrom(expected))
            {
                Debug.LogError($"SerializedProperty '{propertyPath}' is a {type.Name}; .{accessor} cannot be assigned to it.");
                return;
            }
            setter(value);
        }

        public Object objectReferenceValue
        {
            get
            {
                if (!typeof(Object).IsAssignableFrom(type)) throw new InvalidOperationException($"SerializedProperty '{propertyPath}' is a {type.Name}, not an object reference.");
                return getter() as Object;
            }
            set
            {
                if (!typeof(Object).IsAssignableFrom(type))
                {
                    Debug.LogError($"SerializedProperty '{propertyPath}' is a {type.Name}, not an object reference.");
                    return;
                }
                if (!ReferenceEquals(value, null) && !type.IsInstanceOfType(value))
                {
                    Debug.LogError($"Type mismatch: cannot assign a {value.GetType().Name} ('{value.name}') to '{propertyPath}', which expects a {type.Name}.");
                    return;
                }
                setter(ReferenceEquals(value, null) ? null : value);
            }
        }

        public string stringValue { get => Read<string>(nameof(stringValue)) ?? ""; set => Write(value, typeof(string), nameof(stringValue)); }
        public bool boolValue { get => Read<bool>(nameof(boolValue)); set => Write(value, typeof(bool), nameof(boolValue)); }
        public Vector2 vector2Value { get => Read<Vector2>(nameof(vector2Value)); set => Write(value, typeof(Vector2), nameof(vector2Value)); }
        public Vector3 vector3Value { get => Read<Vector3>(nameof(vector3Value)); set => Write(value, typeof(Vector3), nameof(vector3Value)); }
        public Vector4 vector4Value { get => Read<Vector4>(nameof(vector4Value)); set => Write(value, typeof(Vector4), nameof(vector4Value)); }
        public Quaternion quaternionValue { get => Read<Quaternion>(nameof(quaternionValue)); set => Write(value, typeof(Quaternion), nameof(quaternionValue)); }
        public Color colorValue { get => Read<Color>(nameof(colorValue)); set => Write(value, typeof(Color), nameof(colorValue)); }
        public Rect rectValue { get => Read<Rect>(nameof(rectValue)); set => Write(value, typeof(Rect), nameof(rectValue)); }
        public Bounds boundsValue { get => Read<Bounds>(nameof(boundsValue)); set => Write(value, typeof(Bounds), nameof(boundsValue)); }

        public float floatValue
        {
            get { object value = getter(); if (value is float f) return f; if (value is double d) return (float)d; throw new InvalidOperationException($"SerializedProperty '{propertyPath}' is a {type.Name}, not a float."); }
            set
            {
                if (type == typeof(float)) setter(value);
                else if (type == typeof(double)) setter((double)value);
                else Debug.LogError($"SerializedProperty '{propertyPath}' is a {type.Name}; .floatValue cannot be assigned to it.");
            }
        }
        public double doubleValue { get => floatValue; set => floatValue = (float)value; }

        public int intValue
        {
            get
            {
                object value = getter();
                if (value is int i) return i;
                if (value is uint u) return (int)u;
                if (value is long l) return (int)l;
                if (value is short s) return s;
                if (value is byte b) return b;
                if (value is LayerMask mask) return mask.value;
                if (value != null && type.IsEnum) return Convert.ToInt32(value);
                throw new InvalidOperationException($"SerializedProperty '{propertyPath}' is a {type.Name}, not an integer.");
            }
            set
            {
                if (type == typeof(int)) setter(value);
                else if (type == typeof(uint)) setter((uint)value);
                else if (type == typeof(long)) setter((long)value);
                else if (type == typeof(short)) setter((short)value);
                else if (type == typeof(byte)) setter((byte)value);
                else if (type == typeof(LayerMask)) setter((LayerMask)value);
                else if (type.IsEnum) setter(Enum.ToObject(type, value));
                else Debug.LogError($"SerializedProperty '{propertyPath}' is a {type.Name}; .intValue cannot be assigned to it.");
            }
        }
        public long longValue { get => intValue; set => intValue = (int)value; }

        /// <summary>Index into the enum's declared names (not the numeric value), like Unity.</summary>
        public int enumValueIndex
        {
            get
            {
                if (!type.IsEnum) throw new InvalidOperationException($"SerializedProperty '{propertyPath}' is a {type.Name}, not an enum.");
                return Array.IndexOf(Enum.GetNames(type), Enum.GetName(type, getter()));
            }
            set
            {
                if (!type.IsEnum) { Debug.LogError($"SerializedProperty '{propertyPath}' is a {type.Name}, not an enum."); return; }
                string[] names = Enum.GetNames(type);
                if (value < 0 || value >= names.Length) { Debug.LogError($"enumValueIndex {value} is out of range for {type.Name} ('{propertyPath}')."); return; }
                setter(Enum.Parse(type, names[value]));
            }
        }
        public int enumValueFlag { get => intValue; set => intValue = value; }
        public string[] enumNames => type.IsEnum ? Enum.GetNames(type) : new string[0];
        public string[] enumDisplayNames => enumNames;

        public SerializedPropertyType propertyType
        {
            get
            {
                if (type == typeof(int) || type == typeof(uint) || type == typeof(long) || type == typeof(short) || type == typeof(byte)) return SerializedPropertyType.Integer;
                if (type == typeof(bool)) return SerializedPropertyType.Boolean;
                if (type == typeof(float) || type == typeof(double)) return SerializedPropertyType.Float;
                if (type == typeof(string)) return SerializedPropertyType.String;
                if (type == typeof(Color)) return SerializedPropertyType.Color;
                if (typeof(Object).IsAssignableFrom(type)) return SerializedPropertyType.ObjectReference;
                if (type.IsEnum) return SerializedPropertyType.Enum;
                if (type == typeof(Vector2)) return SerializedPropertyType.Vector2;
                if (type == typeof(Vector3)) return SerializedPropertyType.Vector3;
                return SerializedPropertyType.Generic;
            }
        }
    }

    namespace SceneManagement
    {
        public static class EditorSceneManager
        {
            public static bool SaveCurrentModifiedScenesIfUserWantsTo() => true;
            public static bool SaveOpenScenes() => true;
            public static Scene GetActiveScene() => SceneManager.GetActiveScene();

            public static Scene NewScene(NewSceneSetup setup, NewSceneMode mode)
            {
                if (mode == NewSceneMode.Single) Simulation.ClearScene(false);
                SceneManager.activeScenePath = "";
                if (setup == NewSceneSetup.DefaultGameObjects)
                {
                    var camera = new GameObject("Main Camera") { tag = "MainCamera" };
                    camera.AddComponent<Camera>();
                    camera.AddComponent<AudioListener>();
                    camera.transform.position = new Vector3(0f, 1f, -10f);
                    var light = new GameObject("Directional Light");
                    light.AddComponent<Light>().type = LightType.Directional;
                    light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
                }
                return SceneManager.GetActiveScene();
            }

            public static bool SaveScene(Scene scene) => SaveScene(scene, scene.path);
            public static bool SaveScene(Scene scene, string dstScenePath) => SaveScene(scene, dstScenePath, false);
            public static bool SaveScene(Scene scene, string dstScenePath, bool saveAsCopy)
            {
                if (string.IsNullOrEmpty(dstScenePath)) { Debug.LogError("EditorSceneManager.SaveScene: the scene has no path."); return false; }
                string folder = dstScenePath.Replace('\\', '/');
                folder = folder.Contains("/") ? folder.Substring(0, folder.LastIndexOf('/')) : "";
                if (!AssetDatabase.IsValidFolder(folder)) { Debug.LogError($"EditorSceneManager.SaveScene: folder '{folder}' does not exist."); return false; }
                SceneManager.SaveCurrentScene(dstScenePath.Replace('\\', '/'));
                return true;
            }

            public static Scene OpenScene(string scenePath) => OpenScene(scenePath, OpenSceneMode.Single);
            public static Scene OpenScene(string scenePath, OpenSceneMode mode)
            {
                SceneManager.LoadScene(scenePath, mode == OpenSceneMode.Single ? LoadSceneMode.Single : LoadSceneMode.Additive);
                return SceneManager.GetActiveScene();
            }

            public static bool MarkSceneDirty(Scene scene) => true;
            public static void MarkAllScenesDirty() { }
        }
    }

    namespace Events
    {
        public static class UnityEventTools
        {
            public static void AddPersistentListener(UnityEvent unityEvent, UnityAction call) { unityEvent.AddPersistent(call, PersistentListenerMode.Void, null); }
            public static void AddPersistentListener<T0>(UnityEvent<T0> unityEvent, UnityAction<T0> call) { unityEvent.AddPersistent(call, PersistentListenerMode.EventDefined, null); }
            public static void AddPersistentListener<T0, T1>(UnityEvent<T0, T1> unityEvent, UnityAction<T0, T1> call) { unityEvent.AddPersistent(call, PersistentListenerMode.EventDefined, null); }
            public static void AddVoidPersistentListener(UnityEventBase unityEvent, UnityAction call) { unityEvent.AddPersistent(call, PersistentListenerMode.Void, null); }
            public static void AddIntPersistentListener(UnityEventBase unityEvent, UnityAction<int> call, int argument) { unityEvent.AddPersistent(call, PersistentListenerMode.Int, argument); }
            public static void AddFloatPersistentListener(UnityEventBase unityEvent, UnityAction<float> call, float argument) { unityEvent.AddPersistent(call, PersistentListenerMode.Float, argument); }
            public static void AddStringPersistentListener(UnityEventBase unityEvent, UnityAction<string> call, string argument) { unityEvent.AddPersistent(call, PersistentListenerMode.String, argument); }
            public static void AddBoolPersistentListener(UnityEventBase unityEvent, UnityAction<bool> call, bool argument) { unityEvent.AddPersistent(call, PersistentListenerMode.Bool, argument); }
            public static void AddObjectPersistentListener<T>(UnityEventBase unityEvent, UnityAction<T> call, T argument) where T : Object { unityEvent.AddPersistent(call, PersistentListenerMode.Object, argument); }
            public static void RemovePersistentListener(UnityEventBase unityEvent, int index) { unityEvent.RemovePersistent(index); }
            public static void RemovePersistentListener(UnityEventBase unityEvent, UnityAction call)
            {
                for (int i = unityEvent.GetPersistentEventCount() - 1; i >= 0; i--)
                {
                    if (ReferenceEquals(unityEvent.GetPersistentTarget(i), call.Target) && unityEvent.GetPersistentMethodName(i) == call.Method.Name) unityEvent.RemovePersistent(i);
                }
            }
        }
    }

    namespace Animations
    {
        public enum AnimatorConditionMode { If = 1, IfNot = 2, Greater = 3, Less = 4, Equals = 6, NotEqual = 7 }

        public class AnimatorCondition { public AnimatorConditionMode mode; public float threshold; public string parameter; }

        public class AnimatorTransitionBase : Object
        {
            public readonly List<AnimatorCondition> conditions = new List<AnimatorCondition>();
            public AnimatorState destinationState;
            public void AddCondition(AnimatorConditionMode mode, float threshold, string parameter) { conditions.Add(new AnimatorCondition { mode = mode, threshold = threshold, parameter = parameter }); }
        }

        public class AnimatorStateTransition : AnimatorTransitionBase
        {
            public bool hasExitTime = true;
            public float exitTime = 0.75f;
            public float duration = 0.25f;
            public float offset;
            public bool hasFixedDuration = true;
            public bool canTransitionToSelf;
        }

        public class AnimatorState : Object
        {
            public Motion motion;
            public float speed = 1f;
            public bool writeDefaultValues = true;
            public readonly List<AnimatorStateTransition> transitions = new List<AnimatorStateTransition>();
            public AnimatorStateTransition AddTransition(AnimatorState destinationState)
            {
                var transition = new AnimatorStateTransition { destinationState = destinationState };
                transitions.Add(transition);
                return transition;
            }
            public AnimatorStateTransition AddTransition(AnimatorState destinationState, bool defaultExitTime) => AddTransition(destinationState);
        }

        public class AnimatorStateMachine : Object
        {
            public readonly List<AnimatorState> stateList = new List<AnimatorState>();
            public AnimatorState defaultState;
            public AnimatorState AddState(string name) { var state = new AnimatorState { name = name }; stateList.Add(state); if (defaultState == null) defaultState = state; return state; }
            public AnimatorState AddState(string name, Vector3 position) => AddState(name);
            public AnimatorStateTransition AddAnyStateTransition(AnimatorState destinationState) => new AnimatorStateTransition { destinationState = destinationState };
        }

        public class AnimatorControllerLayer
        {
            public string name = "Base Layer";
            public AnimatorStateMachine stateMachine = new AnimatorStateMachine();
            public float defaultWeight = 1f;
        }

        public class AnimatorController : RuntimeAnimatorController
        {
            private readonly List<AnimatorControllerParameter> parameterList = new List<AnimatorControllerParameter>();
            private readonly List<AnimatorControllerLayer> layerList = new List<AnimatorControllerLayer> { new AnimatorControllerLayer() };
            public AnimatorControllerParameter[] parameters { get => parameterList.ToArray(); set { parameterList.Clear(); parameterList.AddRange(value); } }
            public AnimatorControllerLayer[] layers { get => layerList.ToArray(); set { layerList.Clear(); layerList.AddRange(value); } }

            public static AnimatorController CreateAnimatorControllerAtPath(string path)
            {
                var controller = new AnimatorController { name = Path.GetFileNameWithoutExtension(path) };
                AssetDatabase.Register(controller, path);
                return controller;
            }
            public void AddParameter(string name, AnimatorControllerParameterType type) { parameterList.Add(new AnimatorControllerParameter { name = name, type = type }); }
            public void AddParameter(AnimatorControllerParameter parameter) { parameterList.Add(parameter); }
            public void AddLayer(string name) { layerList.Add(new AnimatorControllerLayer { name = name }); }
        }
    }
}
