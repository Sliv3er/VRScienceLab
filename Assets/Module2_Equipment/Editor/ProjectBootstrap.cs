using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace VRScienceLab.Module2.EditorTools
{
    /// <summary>
    /// One-click project preparation, so a fresh clone needs no manual import steps:
    ///  - TextMeshPro essential resources,
    ///  - XR Interaction Toolkit samples (Starter Assets rig, XR simulators),
    ///  - player settings the module relies on (linear colour space, Input System backend).
    /// Menu: VR Science Lab > Module 2 > Set Up Project.
    /// Command line: -executeMethod VRScienceLab.Module2.EditorTools.ProjectBootstrap.SetUpFromCommandLine
    /// </summary>
    public static class ProjectBootstrap
    {
        private const string XriPackage = "Packages/com.unity.xr.interaction.toolkit";
        private static readonly string[] XriSamples = { "Starter Assets", "XR Device Simulator", "XR Interaction Simulator" };

        [MenuItem("VR Science Lab/Module 2/Set Up Project", false, 19)]
        public static void SetUpProject()
        {
            ConfigurePlayerSettings();
            ImportTextMeshProEssentials();
            ImportXriSamples();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[ProjectBootstrap] Project set up. If the Input System backend was just enabled, restart the editor once, then run VR Science Lab > Module 2 > Build Lab Scene.");
        }

        /// <summary>Entry point for -executeMethod (batch mode).</summary>
        public static void SetUpFromCommandLine()
        {
            SetUpProject();
        }

        /// <summary>Imports "TMP Essential Resources" when they are missing.</summary>
        public static void ImportTextMeshProEssentials()
        {
            if (Resources.Load<TMP_Settings>("TMP Settings") != null) return;

            string[] candidates =
            {
                "Packages/com.unity.ugui/Package Resources/TMP Essential Resources.unitypackage",
                "Packages/com.unity.textmeshpro/Package Resources/TMP Essential Resources.unitypackage",
            };

            foreach (string candidate in candidates)
            {
                string fullPath = Path.GetFullPath(candidate);
                if (!File.Exists(fullPath)) continue;

                AssetDatabase.ImportPackage(fullPath, false);
                AssetDatabase.Refresh();
                Debug.Log("[ProjectBootstrap] Imported TextMeshPro essential resources.");
                return;
            }

            Debug.LogWarning("[ProjectBootstrap] TMP Essential Resources package not found. Use Window > TextMeshPro > Import TMP Essential Resources.");
        }

        /// <summary>
        /// Copies the XR Interaction Toolkit samples the scene uses into Assets/Samples, exactly
        /// where the Package Manager "Import" button puts them.
        /// </summary>
        public static void ImportXriSamples()
        {
            UnityEditor.PackageManager.PackageInfo package = UnityEditor.PackageManager.PackageInfo.FindForAssetPath(XriPackage);
            if (package == null)
            {
                Debug.LogWarning("[ProjectBootstrap] XR Interaction Toolkit package not found; samples were not imported.");
                return;
            }

            string destinationRoot = $"Assets/Samples/XR Interaction Toolkit/{package.version}";
            bool imported = false;
            foreach (string sample in XriSamples)
            {
                string source = Path.Combine(package.resolvedPath, "Samples~", sample);
                string destination = $"{destinationRoot}/{sample}";
                if (!Directory.Exists(source) || Directory.Exists(destination)) continue;

                Directory.CreateDirectory(destinationRoot);
                FileUtil.CopyFileOrDirectory(source, destination);
                imported = true;
                Debug.Log($"[ProjectBootstrap] Imported XR Interaction Toolkit sample '{sample}'.");
            }

            if (imported) AssetDatabase.Refresh();
        }

        private static void ConfigurePlayerSettings()
        {
            if (PlayerSettings.colorSpace != ColorSpace.Linear) PlayerSettings.colorSpace = ColorSpace.Linear;

            // Active Input Handling = Both (0 = old Input Manager, 1 = Input System, 2 = both).
            // There is no public API for it, so the serialized project setting is edited directly.
            Object[] assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
            if (assets == null || assets.Length == 0) return;

            var settings = new SerializedObject(assets[0]);
            SerializedProperty handler = settings.FindProperty("activeInputHandler");
            if (handler != null && handler.intValue == 0)
            {
                handler.intValue = 2;
                settings.ApplyModifiedPropertiesWithoutUndo();
                Debug.Log("[ProjectBootstrap] Active Input Handling set to Both. Restart the editor for it to take effect.");
            }
        }
    }
}
