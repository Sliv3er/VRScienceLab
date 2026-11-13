using System.Collections.Generic;
using System.IO;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace VRScienceLab.Module2.EditorTools
{
    // The user: XR rig, scientist avatar, and the scene-level helper objects
    // (search directory, measurement log, reset controller, keyboard helpers).
    public static partial class Module2SceneBuilder
    {
        private const string CharacterFolder = ModuleRoot + "/Models/Characters/Scientist";
        private const string CharacterModelPath = CharacterFolder + "/Doctor_Male_Young.fbx";
        private const string CharacterControllerPath = CharacterFolder + "/ScientistAnimator.controller";
        private const float CharacterHeight = 1.74f;

        private static readonly Vector3 PlayerStart = new Vector3(0f, 0f, -0.35f);

        // ------------------------------------------------------------------
        // XR rig, interaction manager, event system
        // ------------------------------------------------------------------

        private static void BuildXRSetup(BuildContext context)
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
                Debug.LogWarning("[Module2SceneBuilder] Starter Assets sample not found. Creating a minimal XR Origin without controllers. Run VR Science Lab > Module 2 > Set Up Project and rebuild to get ray/grab interactors.");
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
                rig.transform.position = PlayerStart;
                rig.transform.rotation = Quaternion.identity;
            }
            else
            {
                Debug.LogWarning("[Module2SceneBuilder] No XR Origin could be created. Adding a plain camera so the scene still renders.");
                var cameraObject = new GameObject("Main Camera");
                cameraObject.tag = "MainCamera";
                cameraObject.transform.position = PlayerStart + new Vector3(0f, 1.6f, 0f);
                cameraObject.transform.rotation = Quaternion.Euler(25f, 0f, 0f);
                cameraObject.AddComponent<Camera>();
                cameraObject.AddComponent<AudioListener>();
                rig = cameraObject;
            }

            // The near clip plane must be short enough to see the avatar's own hands and chest.
            foreach (Camera camera in rig.GetComponentsInChildren<Camera>(true))
            {
                camera.nearClipPlane = 0.05f;
                camera.farClipPlane = 60f;
            }

            context.XRRig = rig;
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

        private static GameObject FindSimulatorPrefab()
        {
            GameObject simulator = FindPrefabByName("XR Interaction Simulator");
            return simulator != null ? simulator : FindPrefabByName("XR Device Simulator");
        }

        private static Transform FindDeep(Transform root, string childName)
        {
            if (root == null) return null;
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == childName) return child;
            }

            return null;
        }

        // ------------------------------------------------------------------
        // Scientist avatar
        // ------------------------------------------------------------------

        private static void BuildAvatar(BuildContext context)
        {
            Palette palette = context.Palette;
            GameObject root = CreateRoot("Scientist Avatar", null, PlayerStart);
            var avatar = root.AddComponent<ScientistAvatar>();
            context.Avatar = avatar;

            // The robot plans around the user: carved NavMeshObstacle that follows the avatar.
            var obstacle = root.AddComponent<NavMeshObstacle>();
            obstacle.shape = NavMeshObstacleShape.Capsule;
            obstacle.center = new Vector3(0f, 0.9f, 0f);
            obstacle.radius = 0.32f;
            obstacle.height = 1.8f;
            obstacle.carving = true;
            obstacle.carveOnlyStationary = false;

            Transform rig = context.XRRig != null ? context.XRRig.transform : null;
            Camera rigCamera = rig != null ? rig.GetComponentInChildren<Camera>(true) : null;
            SetReference(avatar, "headTarget", rigCamera != null ? rigCamera.transform : null);
            SetReference(avatar, "floorReference", rig != null && rig.GetComponent<XROrigin>() != null ? rig : null);
            SetReference(avatar, "leftHandTarget", FindDeep(rig, "Left Controller"));
            SetReference(avatar, "rightHandTarget", FindDeep(rig, "Right Controller"));

            // Outside view of the scientist, shown as a picture-in-picture on the desktop only.
            GameObject observer = CreateChild("Observer Camera", root.transform, new Vector3(0.95f, 1.45f, 2.3f));
            observer.transform.LookAt(root.transform.position + new Vector3(0f, 1.05f, 0f));
            var observerCamera = observer.AddComponent<Camera>();
            observerCamera.depth = 5f;
            observerCamera.rect = new Rect(0.68f, 0.04f, 0.3f, 0.44f);
            observerCamera.fieldOfView = 42f;
            observerCamera.nearClipPlane = 0.1f;
            observerCamera.farClipPlane = 40f;
            observerCamera.stereoTargetEye = StereoTargetEyeMask.None;
            observer.SetActive(false);
            SetReference(avatar, "observerCamera", observerCamera);

            GameObject modelAsset = PrepareCharacterModel();
            if (modelAsset == null)
            {
                Debug.LogWarning($"[Module2SceneBuilder] Character model not found at {CharacterModelPath}. The avatar has no body.");
                return;
            }

            var model = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset);
            model.name = "Scientist Model";
            model.transform.SetParent(root.transform, false);
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;

            // Scale the character to a realistic height, with the feet on the floor.
            Bounds bounds = CalculateRendererBounds(model);
            if (bounds.size.y > 0.01f)
            {
                float factor = CharacterHeight / bounds.size.y;
                model.transform.localScale *= factor;
                bounds = CalculateRendererBounds(model);
                model.transform.position += new Vector3(root.transform.position.x - bounds.center.x, root.transform.position.y - bounds.min.y, root.transform.position.z - bounds.center.z);
            }

            foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>())
            {
                // The body is seen from the inside in first person; keep it visible when the bounds leave the view.
                if (renderer is SkinnedMeshRenderer skinned) skinned.updateWhenOffscreen = true;
            }

            Animator animator = model.GetComponent<Animator>();
            if (animator == null) animator = model.AddComponent<Animator>();
            animator.runtimeAnimatorController = CreateCharacterController();
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            Transform head = FindDeep(model.transform, "Head");
            Transform leftHand = FindDeep(model.transform, "Fist.L");
            Transform rightHand = FindDeep(model.transform, "Fist.R");
            SetReference(avatar, "animator", animator);
            SetReference(avatar, "headBone", head);
            SetReference(avatar, "leftUpperArm", FindDeep(model.transform, "UpperArm.L"));
            SetReference(avatar, "leftLowerArm", FindDeep(model.transform, "LowerArm.L"));
            SetReference(avatar, "leftHand", leftHand);
            SetReference(avatar, "rightUpperArm", FindDeep(model.transform, "UpperArm.R"));
            SetReference(avatar, "rightLowerArm", FindDeep(model.transform, "LowerArm.R"));
            SetReference(avatar, "rightHand", rightHand);

            DressScientist(palette, root.transform, model, head, leftHand, rightHand);
        }

        private static Bounds CalculateRendererBounds(GameObject go)
        {
            Renderer[] renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            return bounds;
        }

        /// <summary>
        /// Laboratory outfit on top of the imported character: coat / scrubs / shoe colours on the
        /// model's own material slots, plus safety goggles on the head bone and nitrile gloves on
        /// the hand bones so they follow the animation and the controller-driven arms.
        /// </summary>
        private static void DressScientist(Palette palette, Transform avatarRoot, GameObject model, Transform head, Transform leftHand, Transform rightHand)
        {
            foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>())
            {
                Material[] materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    string materialName = materials[i] != null ? materials[i].name : string.Empty;
                    if (materialName.StartsWith("Main")) materials[i] = palette.Coat;
                    else if (materialName.StartsWith("Skin")) materials[i] = palette.Skin;
                    else if (materialName.StartsWith("Hair")) materials[i] = palette.Hair;
                    else if (materialName.StartsWith("Brown")) materials[i] = palette.Scrubs;
                    else if (materialName.StartsWith("Black")) materials[i] = palette.Shoe;
                }

                renderer.sharedMaterials = materials;
            }

            float scale = CharacterHeight / 1.74f;

            if (head != null)
            {
                // Built in avatar space (feet at the origin, facing +Z), then attached to the head bone.
                Transform goggles = CreateChild("Safety Goggles", avatarRoot, new Vector3(0f, CharacterHeight * 0.905f, 0.105f * scale)).transform;
                Bevel("Frame", goggles, new Vector3(0f, -0.022f, 0f), new Vector3(0.17f, 0.044f, 0.02f), 0.008f, 0.003f, palette.GoggleFrame);
                NoShadows(Box("Lens", goggles, new Vector3(0f, 0f, 0.011f), new Vector3(0.155f, 0.034f, 0.003f), palette.GoggleLens, false));
                for (int side = -1; side <= 1; side += 2)
                {
                    Box(side < 0 ? "Arm L" : "Arm R", goggles, new Vector3(side * 0.086f, 0f, -0.05f), new Vector3(0.004f, 0.012f, 0.11f), palette.GoggleFrame, false);
                }

                goggles.SetParent(head, true);
            }

            AttachGlove(palette, leftHand, "Glove L");
            AttachGlove(palette, rightHand, "Glove R");
        }

        private static void AttachGlove(Palette palette, Transform hand, string name)
        {
            if (hand == null) return;

            // Centre the glove between the wrist and the end of the fist bone.
            Vector3 center = hand.position;
            if (hand.childCount > 0) center = Vector3.Lerp(hand.position, hand.GetChild(0).position, 0.5f);

            GameObject glove = CreatePrimitive(PrimitiveType.Sphere, name, null, center, new Vector3(0.105f, 0.125f, 0.105f), palette.Glove, false);
            glove.transform.SetParent(hand, true);
            Vector3 along = hand.childCount > 0 ? hand.GetChild(0).position - hand.position : hand.up;
            if (along.sqrMagnitude > 0.000001f) glove.transform.rotation = Quaternion.FromToRotation(Vector3.up, along.normalized);

            GameObject cuff = Lathe(name + " Cuff", null, "GloveCuff", CylinderProfile(0.046f, 0.05f, 0.008f), hand.position, palette.Glove, 16);
            cuff.transform.rotation = glove.transform.rotation;
            cuff.transform.position = hand.position - glove.transform.up * 0.045f;
            cuff.transform.SetParent(hand, true);
        }

        /// <summary>
        /// Configures the FBX import (looping idle / walk clips, humanoid rig when the skeleton maps
        /// cleanly, generic otherwise) and returns the model asset.
        /// </summary>
        private static GameObject PrepareCharacterModel()
        {
            var importer = AssetImporter.GetAtPath(CharacterModelPath) as ModelImporter;
            if (importer == null) return null;

            bool changed = false;

            ModelImporterClipAnimation[] clips = importer.clipAnimations;
            if (clips == null || clips.Length == 0) clips = importer.defaultClipAnimations;
            foreach (ModelImporterClipAnimation clip in clips)
            {
                bool loop = clip.name.EndsWith("Idle") || clip.name.EndsWith("Walk");
                if (clip.loopTime != loop)
                {
                    clip.loopTime = loop;
                    changed = true;
                }
            }

            if (changed || importer.clipAnimations == null || importer.clipAnimations.Length == 0)
            {
                importer.clipAnimations = clips;
                changed = true;
            }

            if (importer.materialImportMode != ModelImporterMaterialImportMode.ImportStandard)
            {
                importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
                changed = true;
            }

            if (changed) importer.SaveAndReimport();

            if (importer.animationType != ModelImporterAnimationType.Human)
            {
                TryConfigureHumanoid(importer);
            }

            return AssetDatabase.LoadAssetAtPath<GameObject>(CharacterModelPath);
        }

        // Maps the character's bones (Quaternius naming) onto Unity's humanoid rig. If Unity
        // rejects the mapping, the importer goes back to a generic rig, which the avatar script
        // supports just as well (it drives the bones directly).
        private static void TryConfigureHumanoid(ModelImporter importer)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterModelPath);
            if (model == null) return;

            var mapping = new Dictionary<string, string>
            {
                { "Hips", "Hips" }, { "Spine", "Abdomen" }, { "Chest", "Torso" }, { "Neck", "Neck" }, { "Head", "Head" },
                { "LeftShoulder", "Shoulder.L" }, { "LeftUpperArm", "UpperArm.L" }, { "LeftLowerArm", "LowerArm.L" }, { "LeftHand", "Fist.L" },
                { "RightShoulder", "Shoulder.R" }, { "RightUpperArm", "UpperArm.R" }, { "RightLowerArm", "LowerArm.R" }, { "RightHand", "Fist.R" },
                { "LeftUpperLeg", "UpperLeg.L" }, { "LeftLowerLeg", "LowerLeg.L" }, { "LeftFoot", "Foot.L" },
                { "RightUpperLeg", "UpperLeg.R" }, { "RightLowerLeg", "LowerLeg.R" }, { "RightFoot", "Foot.R" },
            };

            var humanBones = new List<HumanBone>();
            foreach (KeyValuePair<string, string> pair in mapping)
            {
                if (FindDeep(model.transform, pair.Value) == null)
                {
                    Debug.Log($"[Module2SceneBuilder] Character bone '{pair.Value}' not found; keeping a generic rig.");
                    return;
                }

                var bone = new HumanBone { humanName = pair.Key, boneName = pair.Value };
                bone.limit.useDefaultValues = true;
                humanBones.Add(bone);
            }

            var skeleton = new List<SkeletonBone>();
            foreach (Transform transform in model.GetComponentsInChildren<Transform>(true))
            {
                skeleton.Add(new SkeletonBone
                {
                    name = transform.name,
                    position = transform.localPosition,
                    rotation = transform.localRotation,
                    scale = transform.localScale,
                });
            }

            HumanDescription description = importer.humanDescription;
            description.human = humanBones.ToArray();
            description.skeleton = skeleton.ToArray();
            description.upperArmTwist = 0.5f;
            description.lowerArmTwist = 0.5f;
            description.upperLegTwist = 0.5f;
            description.lowerLegTwist = 0.5f;
            description.armStretch = 0.05f;
            description.legStretch = 0.05f;
            description.feetSpacing = 0f;
            description.hasTranslationDoF = false;

            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.humanDescription = description;
            importer.SaveAndReimport();

            Avatar avatar = null;
            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(CharacterModelPath))
            {
                if (asset is Avatar found) avatar = found;
            }

            if (avatar == null || !avatar.isValid || !avatar.isHuman)
            {
                Debug.LogWarning("[Module2SceneBuilder] Unity rejected the humanoid mapping of the character; using a generic rig instead.");
                importer.animationType = ModelImporterAnimationType.Generic;
                importer.SaveAndReimport();
            }
        }

        /// <summary>Animator Controller with Idle and Walk states switched by the "Speed" parameter.</summary>
        private static RuntimeAnimatorController CreateCharacterController()
        {
            AnimationClip idle = null;
            AnimationClip walk = null;
            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(CharacterModelPath))
            {
                if (!(asset is AnimationClip clip) || clip.name.StartsWith("__preview__")) continue;
                if (clip.name.EndsWith("Idle")) idle = clip;
                else if (clip.name.EndsWith("Walk")) walk = clip;
            }

            if (idle == null || walk == null)
            {
                Debug.LogWarning("[Module2SceneBuilder] Idle / Walk clips not found in the character model.");
            }

            AssetDatabase.DeleteAsset(CharacterControllerPath);
            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(CharacterControllerPath);
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);

            AnimatorStateMachine machine = controller.layers[0].stateMachine;
            AnimatorState idleState = machine.AddState("Idle");
            idleState.motion = idle;
            AnimatorState walkState = machine.AddState("Walk");
            walkState.motion = walk;
            machine.defaultState = idleState;

            AnimatorStateTransition toWalk = idleState.AddTransition(walkState);
            toWalk.hasExitTime = false;
            toWalk.duration = 0.2f;
            toWalk.AddCondition(AnimatorConditionMode.Greater, 0.2f, "Speed");

            AnimatorStateTransition toIdle = walkState.AddTransition(idleState);
            toIdle.hasExitTime = false;
            toIdle.duration = 0.25f;
            toIdle.AddCondition(AnimatorConditionMode.Less, 0.12f, "Speed");

            EditorUtility.SetDirty(controller);
            return controller;
        }

        // ------------------------------------------------------------------
        // Scene-level systems
        // ------------------------------------------------------------------

        private static void BuildLabSystems(BuildContext context, Transform systems)
        {
            Palette palette = context.Palette;

            // Locator beacon: a spinning, bobbing marker moved above whatever the directory finds.
            GameObject beacon = CreateRoot("Locator Beacon", systems, new Vector3(0f, 1.5f, InstrumentRowZ));
            NoShadows(Lathe("Arrow", beacon.transform, "BeaconArrow", new[]
            {
                new Vector2(0f, 0f), new Vector2(0.035f, 0.06f), new Vector2(0.014f, 0.06f), new Vector2(0.014f, 0.13f), new Vector2(0f, 0.13f),
            }, Vector3.zero, palette.Beacon, 4));
            NoShadows(Lathe("Halo", beacon.transform, "BeaconHalo", RingProfile(0.06f, 0.052f, 0.004f), new Vector3(0f, 0.065f, 0f), palette.Beacon, 32));
            var beaconAnimator = beacon.AddComponent<TransformAnimator>();
            SetVector3(beaconAnimator, "rotationSpeed", new Vector3(0f, 120f, 0f));
            SetVector3(beaconAnimator, "bobOffset", new Vector3(0f, 0.04f, 0f));
            SetFloat(beaconAnimator, "bobFrequency", 0.8f);

            var directory = CreateChild("Lab Directory", systems, Vector3.zero).AddComponent<LabDirectory>();
            SetReference(directory, "locatorBeacon", beacon.transform);
            context.Directory = directory;

            var log = CreateChild("Measurement Log", systems, Vector3.zero).AddComponent<MeasurementLog>();
            SetReference(log, "digitalScale", context.Scale);
            SetReference(log, "hotPlate", context.HotPlate);
            SetReference(log, "stopwatch", context.Stopwatch);
            SetReference(log, "thermometer", context.Thermometer);
            SetReference(log, "sampleManager", context.SampleManager);
            context.MeasurementLog = log;

            var reset = CreateChild("Lab Reset Controller", systems, Vector3.zero).AddComponent<LabResetController>();
            SetReference(reset, "digitalScale", context.Scale);
            SetReference(reset, "hotPlate", context.HotPlate);
            SetReference(reset, "stopwatch", context.Stopwatch);
            SetReference(reset, "thermometer", context.Thermometer);
            SetReference(reset, "sampleManager", context.SampleManager);
            SetReference(reset, "robot", context.Robot);
            SetReference(reset, "measurementLog", log);
            SetReferences(reset, "mounts", context.StopwatchMount);
            context.ResetController = reset;
        }

        private static void BuildInputHelpers(BuildContext context, Transform systems)
        {
            var debugInput = CreateChild("Module2 Debug Input (keyboard + mouse)", systems, Vector3.zero).AddComponent<Module2DebugInput>();
            SetReference(debugInput, "digitalScale", context.Scale);
            SetReference(debugInput, "hotPlate", context.HotPlate);
            SetReference(debugInput, "stopwatch", context.Stopwatch);
            SetReference(debugInput, "thermometer", context.Thermometer);

            var shortcuts = CreateChild("Lab Keyboard Shortcuts", systems, Vector3.zero).AddComponent<LabKeyboardShortcuts>();
            SetReference(shortcuts, "sampleManager", context.SampleManager);
            SetReference(shortcuts, "robot", context.Robot);
            SetReference(shortcuts, "resetController", context.ResetController);
            SetReference(shortcuts, "measurementLog", context.MeasurementLog);
            SetReference(shortcuts, "directory", context.Directory);
            SetReference(shortcuts, "welcomePanel", context.WelcomePanel);
            SetReference(shortcuts, "stopwatchMount", context.StopwatchMount);
            SetReference(shortcuts, "avatar", context.Avatar);

            GameObject simulator = FindSimulatorPrefab();
            var loader = CreateChild("XR Simulator Loader", systems, Vector3.zero).AddComponent<XRSimulatorLoader>();
            SetReference(loader, "simulatorPrefab", simulator);
            if (simulator == null)
            {
                Debug.LogWarning("[Module2SceneBuilder] No XR simulator prefab found; desktop testing needs VR Science Lab > Module 2 > Set Up Project.");
            }
        }
    }
}
