using UnityEditor;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace VRScienceLab.Module2.EditorTools
{
    // Samples and loose objects: glassware visuals, the sample prefabs instantiated at runtime by
    // SampleManager, the calibration weights, the drop-test ball and the equipment cart.
    public static partial class Module2SceneBuilder
    {
        private const string PhysicsMaterialFolder = ModuleRoot + "/Materials/Physics";

        // ------------------------------------------------------------------
        // Glassware visuals (shared by the sample prefabs and the decoration)
        // ------------------------------------------------------------------

        /// <summary>Beaker: 68 mm wide, 95 mm tall, thin glass wall with a flared lip. Origin at the base.</summary>
        private static void BuildBeakerVisual(Transform parent, Palette palette, Material liquid, float fill)
        {
            Lathe("Glass", parent, "BeakerGlass", new[]
            {
                new Vector2(0f, 0f), new Vector2(0.031f, 0f), new Vector2(0.034f, 0.003f), new Vector2(0.034f, 0.088f),
                new Vector2(0.0365f, 0.095f), new Vector2(0.035f, 0.095f), new Vector2(0.0322f, 0.088f),
                new Vector2(0.0322f, 0.004f), new Vector2(0f, 0.004f),
            }, Vector3.zero, palette.Glass, 36);

            float level = Mathf.Lerp(0.012f, 0.082f, Mathf.Clamp01(fill));
            Lathe("Liquid", parent, "BeakerLiquid_" + Mathf.RoundToInt(level * 1000f), new[]
            {
                new Vector2(0f, 0.0045f), new Vector2(0.0316f, 0.0045f), new Vector2(0.0316f, level), new Vector2(0f, level),
            }, Vector3.zero, liquid, 28);

            for (int i = 1; i <= 3; i++)
            {
                Lathe("Graduation", parent, "BeakerGraduation", RingProfile(0.0343f, 0.0339f, 0.0006f), new Vector3(0f, 0.02f * i + 0.01f, 0f), palette.Ceramic, 28);
            }
        }

        /// <summary>Erlenmeyer flask: 80 mm base, 120 mm tall. Origin at the base.</summary>
        private static void BuildFlaskVisual(Transform parent, Palette palette, Material liquid, float fill)
        {
            Lathe("Glass", parent, "FlaskGlass", new[]
            {
                new Vector2(0f, 0f), new Vector2(0.037f, 0f), new Vector2(0.04f, 0.004f), new Vector2(0.0135f, 0.085f),
                new Vector2(0.0135f, 0.115f), new Vector2(0.016f, 0.12f), new Vector2(0.0145f, 0.12f), new Vector2(0.012f, 0.115f),
                new Vector2(0.012f, 0.086f), new Vector2(0.038f, 0.005f), new Vector2(0f, 0.005f),
            }, Vector3.zero, palette.Glass, 36);

            float level = Mathf.Lerp(0.015f, 0.07f, Mathf.Clamp01(fill));
            float radiusAtLevel = Mathf.Lerp(0.0375f, 0.012f, (level - 0.005f) / 0.081f);
            Lathe("Liquid", parent, "FlaskLiquid_" + Mathf.RoundToInt(level * 1000f), new[]
            {
                new Vector2(0f, 0.0055f), new Vector2(0.0372f, 0.0055f), new Vector2(radiusAtLevel, level), new Vector2(0f, level),
            }, Vector3.zero, liquid, 28);
        }

        /// <summary>Test tube: 18 mm wide, 110 mm tall, round bottom. Origin at the lowest point.</summary>
        private static void BuildTestTubeVisual(Transform parent, Palette palette, Material liquid, float fill)
        {
            Lathe("Glass", parent, "TestTubeGlass", new[]
            {
                new Vector2(0f, 0f), new Vector2(0.005f, 0.0015f), new Vector2(0.008f, 0.005f), new Vector2(0.009f, 0.01f),
                new Vector2(0.009f, 0.106f), new Vector2(0.0102f, 0.11f), new Vector2(0.0092f, 0.11f), new Vector2(0.0078f, 0.106f),
                new Vector2(0.0078f, 0.011f), new Vector2(0.0068f, 0.006f), new Vector2(0f, 0.002f),
            }, Vector3.zero, palette.Glass, 20);

            float level = Mathf.Lerp(0.02f, 0.09f, Mathf.Clamp01(fill));
            Lathe("Liquid", parent, "TestTubeLiquid_" + Mathf.RoundToInt(level * 1000f), new[]
            {
                new Vector2(0f, 0.0028f), new Vector2(0.0064f, 0.0065f), new Vector2(0.0074f, 0.012f), new Vector2(0.0074f, level), new Vector2(0f, level),
            }, Vector3.zero, liquid, 16);
        }

        /// <summary>Reagent bottle with a stopper (decoration). Origin at the base.</summary>
        private static void BuildBottleVisual(Transform parent, Palette palette, Material liquid)
        {
            Lathe("Glass", parent, "BottleGlass", new[]
            {
                new Vector2(0f, 0f), new Vector2(0.03f, 0f), new Vector2(0.033f, 0.004f), new Vector2(0.033f, 0.085f),
                new Vector2(0.026f, 0.1f), new Vector2(0.013f, 0.108f), new Vector2(0.013f, 0.124f), new Vector2(0f, 0.124f),
            }, Vector3.zero, palette.GlassTinted, 28);
            Lathe("Liquid", parent, "BottleLiquid", new[]
            {
                new Vector2(0f, 0.004f), new Vector2(0.0305f, 0.004f), new Vector2(0.0305f, 0.07f), new Vector2(0f, 0.07f),
            }, Vector3.zero, liquid, 24);
            Lathe("Stopper", parent, "BottleStopper", CylinderProfile(0.016f, 0.02f, 0.004f), new Vector3(0f, 0.122f, 0f), palette.DarkPlastic, 16);
            NoShadows(Box("Label", parent, new Vector3(0f, 0.045f, -0.0331f), new Vector3(0.036f, 0.03f, 0.0006f), palette.Ceramic, false));
        }

        // ------------------------------------------------------------------
        // Grabbable building blocks
        // ------------------------------------------------------------------

        /// <summary>
        /// Adds what every movable lab object needs: Rigidbody with gravity, XR grab, collision
        /// feedback and hover highlight. Colliders are added by the caller.
        /// </summary>
        private static Rigidbody MakeGrabbable(GameObject go, float massKilograms, PhysicsMaterial physicsMaterial = null)
        {
            var rigidbody = go.AddComponent<Rigidbody>();
            rigidbody.mass = massKilograms;
            rigidbody.useGravity = true;
            rigidbody.linearDamping = 0.05f;
            rigidbody.angularDamping = 0.6f;
            rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
            rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            if (physicsMaterial != null)
            {
                foreach (Collider collider in go.GetComponentsInChildren<Collider>())
                {
                    collider.sharedMaterial = physicsMaterial;
                }
            }

            var grab = go.AddComponent<XRGrabInteractable>();
            grab.useDynamicAttach = true;
            grab.throwOnDetach = true;
            grab.movementType = XRBaseInteractable.MovementType.VelocityTracking;

            var audio = go.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            audio.spatialBlend = 1f;
            audio.minDistance = 0.3f;
            audio.maxDistance = 6f;

            var physicsObject = go.AddComponent<LabPhysicsObject>();
            SetReference(physicsObject, "impactAudio", audio);

            go.AddComponent<HoverHighlight>();
            return rigidbody;
        }

        private static PhysicsMaterial CreatePhysicsMaterial(string name, float dynamicFriction, float staticFriction, float bounciness)
        {
            EnsureFolder(PhysicsMaterialFolder);
            string path = $"{PhysicsMaterialFolder}/{name}.physicsMaterial";
            PhysicsMaterial material = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(path);
            if (material == null)
            {
                material = new PhysicsMaterial(name);
                AssetDatabase.CreateAsset(material, path);
            }

            material.dynamicFriction = dynamicFriction;
            material.staticFriction = staticFriction;
            material.bounciness = bounciness;
            material.bounceCombine = PhysicsMaterialCombine.Maximum;
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>Glowing ring at the base of a sample, shown while it is the selected one.</summary>
        private static GameObject CreateSelectionMarker(Transform parent, Palette palette, float radius)
        {
            GameObject marker = Lathe("SelectionMarker", parent, "SelectionRing_" + Mathf.RoundToInt(radius * 1000f), RingProfile(radius, radius - 0.006f, 0.003f), new Vector3(0f, 0.001f, 0f), palette.Accent, 36);
            NoShadows(marker);
            var animator = marker.AddComponent<TransformAnimator>();
            SetFloat(animator, "pulseAmount", 0.06f);
            SetFloat(animator, "pulseFrequency", 1.2f);
            return marker;
        }

        private static TMPro.TextMeshPro CreateSampleLabel(Transform parent, string text, float height)
        {
            return CreateText("Label", parent, new Vector3(0f, height, 0f), Vector3.zero, text, 0.11f, LabelTextColor, new Vector2(0.14f, 0.02f), TMPro.FontStyles.Bold);
        }

        private static LabSample FinishSample(GameObject root, string displayName, LabSampleKind kind, GameObject marker, TMPro.TMP_Text label, string prefabName)
        {
            var sample = root.AddComponent<LabSample>();
            SetString(sample, "displayName", displayName);
            var serialized = new SerializedObject(sample);
            serialized.FindProperty("kind").enumValueIndex = (int)kind;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            SetReference(sample, "selectionMarker", marker);
            SetReference(sample, "nameLabel", label);

            string path = $"{SamplePrefabFolder}/{prefabName}.prefab";
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path, out bool success);
            Object.DestroyImmediate(root);
            if (!success || prefab == null)
            {
                Debug.LogError($"[Module2SceneBuilder] Could not save prefab {path}.");
                return null;
            }

            return prefab.GetComponent<LabSample>();
        }

        // ------------------------------------------------------------------
        // Sample prefabs
        // ------------------------------------------------------------------

        private static LabSample CreateBeakerPrefab(Palette palette, PhysicsMaterial glass, string prefabName, string displayName, string content, float temperature, Material liquid)
        {
            var root = new GameObject(prefabName);
            BuildBeakerVisual(CreateChild("Visual", root.transform, Vector3.zero).transform, palette, liquid, 0.7f);

            var box = root.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 0.0475f, 0f);
            box.size = new Vector3(0.066f, 0.095f, 0.066f);

            MakeGrabbable(root, 0.2f, glass);
            var temperatureSample = root.AddComponent<TemperatureSample>();
            SetString(temperatureSample, "sampleName", content);
            SetFloat(temperatureSample, "temperatureCelsius", temperature);

            GameObject marker = CreateSelectionMarker(root.transform, palette, 0.052f);
            TMPro.TextMeshPro label = CreateSampleLabel(root.transform, displayName, 0.115f);
            return FinishSample(root, displayName, LabSampleKind.Beaker, marker, label, prefabName);
        }

        private static LabSample CreateTestTubePrefab(Palette palette, PhysicsMaterial glass)
        {
            var root = new GameObject("Sample_TestTube");
            // Weighted holder so the tube stands upright on the bench and on the instruments.
            GameObject holder = Bevel("Holder", root.transform, Vector3.zero, new Vector3(0.05f, 0.03f, 0.05f), 0.01f, 0.003f, palette.SampleLid, true);
            holder.name = "Holder";
            BuildTestTubeVisual(CreateChild("Visual", root.transform, new Vector3(0f, 0.012f, 0f)).transform, palette, palette.LiquidGreen, 0.6f);

            // CapsuleCollider for the tube itself.
            var capsule = root.AddComponent<CapsuleCollider>();
            capsule.direction = 1;
            capsule.center = new Vector3(0f, 0.075f, 0f);
            capsule.radius = 0.0095f;
            capsule.height = 0.1f;

            MakeGrabbable(root, 0.06f, glass);
            var temperatureSample = root.AddComponent<TemperatureSample>();
            SetString(temperatureSample, "sampleName", "Culture medium");
            SetFloat(temperatureSample, "temperatureCelsius", 37f);

            GameObject marker = CreateSelectionMarker(root.transform, palette, 0.045f);
            TMPro.TextMeshPro label = CreateSampleLabel(root.transform, "Test tube", 0.14f);
            return FinishSample(root, "Test tube", LabSampleKind.TestTube, marker, label, "Sample_TestTube");
        }

        private static LabSample CreateFlaskPrefab(Palette palette, PhysicsMaterial glass)
        {
            var root = new GameObject("Sample_Flask");
            BuildFlaskVisual(CreateChild("Visual", root.transform, Vector3.zero).transform, palette, palette.LiquidAmber, 0.6f);

            var box = root.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 0.03f, 0f);
            box.size = new Vector3(0.07f, 0.06f, 0.07f);
            var capsule = root.AddComponent<CapsuleCollider>();
            capsule.direction = 1;
            capsule.center = new Vector3(0f, 0.085f, 0f);
            capsule.radius = 0.015f;
            capsule.height = 0.075f;

            MakeGrabbable(root, 0.25f, glass);
            var temperatureSample = root.AddComponent<TemperatureSample>();
            SetString(temperatureSample, "sampleName", "Warm solution");
            SetFloat(temperatureSample, "temperatureCelsius", 55f);

            GameObject marker = CreateSelectionMarker(root.transform, palette, 0.058f);
            TMPro.TextMeshPro label = CreateSampleLabel(root.transform, "Flask", 0.14f);
            return FinishSample(root, "Flask", LabSampleKind.Flask, marker, label, "Sample_Flask");
        }

        private static LabSample CreateSampleBoxPrefab(Palette palette)
        {
            var root = new GameObject("Sample_Box");
            GameObject body = Bevel("Body", root.transform, Vector3.zero, new Vector3(0.075f, 0.04f, 0.055f), 0.008f, 0.003f, palette.SampleBox);
            body.name = "Body";
            GameObject lid = Bevel("Lid", root.transform, new Vector3(0f, 0.04f, 0f), new Vector3(0.079f, 0.012f, 0.059f), 0.009f, 0.003f, palette.SampleLid);
            lid.name = "Lid";
            Box("Latch", root.transform, new Vector3(0f, 0.036f, -0.0285f), new Vector3(0.018f, 0.014f, 0.004f), palette.Steel, false);
            Box("Sticker", root.transform, new Vector3(0f, 0.02f, -0.0277f), new Vector3(0.04f, 0.016f, 0.0006f), palette.Warning, false);

            var box = root.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 0.026f, 0f);
            box.size = new Vector3(0.078f, 0.052f, 0.058f);

            MakeGrabbable(root, 0.15f);
            GameObject marker = CreateSelectionMarker(root.transform, palette, 0.058f);
            TMPro.TextMeshPro label = CreateSampleLabel(root.transform, "Sample box", 0.075f);
            return FinishSample(root, "Sample box", LabSampleKind.SampleBox, marker, label, "Sample_Box");
        }

        // ------------------------------------------------------------------
        // Scene objects: weights, drop-test ball, equipment cart, sample manager
        // ------------------------------------------------------------------

        private static void BuildSamples(BuildContext context, Transform systems)
        {
            Palette palette = context.Palette;
            PhysicsMaterial glass = CreatePhysicsMaterial("Glass", 0.35f, 0.45f, 0.05f);
            PhysicsMaterial metal = CreatePhysicsMaterial("Metal", 0.4f, 0.5f, 0.1f);
            PhysicsMaterial rubber = CreatePhysicsMaterial("RubberBall", 0.6f, 0.7f, 0.72f);

            // Loose objects that exist from the start.
            Transform testObjects = new GameObject("Test Objects").transform;
            CreateWeight(palette, metal, testObjects, "Weight_100g", "100 g", 0.1f, 0.019f, new Vector3(-1.72f, BenchTopHeight + 0.01f, 0.5f));
            CreateWeight(palette, metal, testObjects, "Weight_250g", "250 g", 0.25f, 0.025f, new Vector3(-1.6f, BenchTopHeight + 0.01f, 0.5f));
            CreateWeight(palette, metal, testObjects, "Weight_500g", "500 g", 0.5f, 0.031f, new Vector3(-1.46f, BenchTopHeight + 0.01f, 0.5f));
            CreateDropBall(palette, rubber, testObjects, new Vector3(-1.6f, BenchTopHeight + 0.01f, 0.64f));
            BuildEquipmentCart(palette, metal, testObjects, new Vector3(1.95f, 0f, -1.35f));

            // Sample prefabs (index order is used by the interface buttons and keyboard shortcuts).
            LabSample[] prefabs =
            {
                CreateBeakerPrefab(palette, glass, "Sample_Beaker_Cold", "Cold water", "Cold water", 4f, palette.LiquidBlue),
                CreateBeakerPrefab(palette, glass, "Sample_Beaker_Room", "Beaker", "Room temperature water", 22f, palette.LiquidClear),
                CreateBeakerPrefab(palette, glass, "Sample_Beaker_Hot", "Hot water", "Hot water", 80f, palette.LiquidRed),
                CreateTestTubePrefab(palette, glass),
                CreateFlaskPrefab(palette, glass),
                CreateSampleBoxPrefab(palette),
            };

            // Tray slots: two rows of four on the sample tray.
            Transform manager = CreateChild("Sample Manager", systems, Vector3.zero).transform;
            Transform slots = CreateChild("Sample Tray Slots", manager, new Vector3(1.52f, BenchTopHeight + 0.008f, 0.535f)).transform;
            var spawnPoints = new Object[8];
            for (int i = 0; i < 8; i++)
            {
                int column = i % 4;
                int row = i / 4;
                // Front row first so the initial samples are the easiest to reach.
                var local = new Vector3(-0.225f + column * 0.15f, 0f, row == 0 ? -0.09f : 0.09f);
                Transform slot = CreateChild($"Slot {i + 1}", slots, local).transform;
                NoShadows(Lathe("SlotMark", slot, "TraySlotMark", RingProfile(0.045f, 0.041f, 0.0006f), Vector3.zero, palette.Marker, 32));
                Transform spawn = CreateChild("SpawnPoint", slot, new Vector3(0f, 0.004f, 0f)).transform;
                spawnPoints[i] = spawn;
            }

            Transform sampleParent = CreateChild("Samples", manager, Vector3.zero).transform;
            var sampleManager = manager.gameObject.AddComponent<SampleManager>();
            SetReferences(sampleManager, "samplePrefabs", prefabs);
            SetReferences(sampleManager, "spawnPoints", spawnPoints);
            SetReference(sampleManager, "sampleParent", sampleParent);
            SetIntArray(sampleManager, "initialSamples", 0, 1, 2);
            SetInt(sampleManager, "maximumSamples", 8);

            var serialized = new SerializedObject(sampleManager);
            SerializedProperty anchors = serialized.FindProperty("stationAnchors");
            string[] labels = { "Scale pan", "Hot plate", "Thermometer dock" };
            Transform[] transforms = { context.ScaleAnchor, context.HotPlateAnchor, context.ThermometerAnchor };
            anchors.arraySize = labels.Length;
            for (int i = 0; i < labels.Length; i++)
            {
                SerializedProperty element = anchors.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("label").stringValue = labels[i];
                element.FindPropertyRelative("anchor").objectReferenceValue = transforms[i];
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            context.SampleManager = sampleManager;
        }

        /// <summary>Brass calibration weight (classic knob shape). The Rigidbody mass is what the scale reads.</summary>
        private static void CreateWeight(Palette palette, PhysicsMaterial metal, Transform parent, string name, string label, float massKilograms, float radius, Vector3 position)
        {
            GameObject weight = CreateRoot(name, parent, position);
            float height = radius * 1.5f;
            Lathe("Body", weight.transform, "Weight_" + Mathf.RoundToInt(radius * 1000f), new[]
            {
                new Vector2(0f, 0f), new Vector2(radius - 0.002f, 0f), new Vector2(radius, 0.002f), new Vector2(radius, height - 0.003f),
                new Vector2(radius - 0.003f, height), new Vector2(radius * 0.42f, height), new Vector2(radius * 0.3f, height + radius * 0.25f),
                new Vector2(radius * 0.48f, height + radius * 0.5f), new Vector2(radius * 0.48f, height + radius * 0.7f),
                new Vector2(radius * 0.3f, height + radius * 0.8f), new Vector2(0f, height + radius * 0.8f),
            }, Vector3.zero, palette.Brass, 32);

            float totalHeight = height + radius * 0.8f;
            var box = weight.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, totalHeight * 0.5f, 0f);
            box.size = new Vector3(radius * 1.9f, totalHeight, radius * 1.9f);

            MakeGrabbable(weight, massKilograms, metal);
            TMPro.TextMeshPro text = CreateText("Label", weight.transform, new Vector3(0f, height * 0.5f, -radius - 0.0004f), Vector3.zero, label, radius * 3.2f, LabelTextColor, new Vector2(radius * 1.7f, height * 0.6f), TMPro.FontStyles.Bold);
            text.name = "Label";
            SaveAsPrefab(weight, TestObjectPrefabFolder, name);
        }

        /// <summary>Rubber ball with a SphereCollider and a bouncy physics material, resting in a dish: grab it and drop it.</summary>
        private static void CreateDropBall(Palette palette, PhysicsMaterial rubber, Transform parent, Vector3 position)
        {
            NoShadows(Lathe("Ball Dish", parent, "BallDish", new[]
            {
                new Vector2(0f, 0f), new Vector2(0.03f, 0f), new Vector2(0.04f, 0.006f), new Vector2(0.037f, 0.006f), new Vector2(0.028f, 0.002f), new Vector2(0f, 0.002f),
            }, position - new Vector3(0f, 0.002f, 0f), palette.Steel, 28));

            GameObject ball = CreatePrimitive(PrimitiveType.Sphere, "Drop_Test_Ball", parent, position + new Vector3(0f, 0.024f, 0f), Vector3.one * 0.044f, palette.Red);
            MakeGrabbable(ball, 0.05f, rubber);
            SaveAsPrefab(ball, TestObjectPrefabFolder, "Drop_Test_Ball");
        }

        /// <summary>
        /// Equipment cart: a heavy Rigidbody the user can push around. Its NavMeshObstacle carves
        /// the NavMesh, so the robot plans its path around wherever the cart currently stands.
        /// </summary>
        private static void BuildEquipmentCart(Palette palette, PhysicsMaterial metal, Transform parent, Vector3 position)
        {
            GameObject cart = CreateRoot("Equipment Cart", parent, position);
            const float width = 0.7f, depth = 0.45f, height = 0.82f;

            for (int x = -1; x <= 1; x += 2)
            {
                for (int z = -1; z <= 1; z += 2)
                {
                    Box("Post", cart.transform, new Vector3(x * (width * 0.5f - 0.02f), height * 0.5f + 0.04f, z * (depth * 0.5f - 0.02f)), new Vector3(0.025f, height - 0.08f, 0.025f), palette.BrushedSteel, false);
                    GameObject caster = Lathe("Caster", cart.transform, "CartCaster", CylinderProfile(0.035f, 0.022f, 0.004f), new Vector3(x * (width * 0.5f - 0.02f) - 0.011f, 0.035f, z * (depth * 0.5f - 0.02f)), palette.Rubber, 20);
                    caster.transform.localRotation = Quaternion.Euler(0f, 0f, -90f);
                }
            }

            float[] shelfHeights = { 0.16f, 0.48f, height };
            foreach (float y in shelfHeights)
            {
                Bevel("Shelf", cart.transform, new Vector3(0f, y - 0.02f, 0f), new Vector3(width, 0.02f, depth), 0.015f, 0.003f, palette.BrushedSteel);
            }

            MeshObject("Handle", cart.transform, ProceduralMeshFactory.Tube("CartHandle", ProceduralMeshFactory.SmoothPath(new[]
            {
                new Vector3(width * 0.5f, height - 0.01f, -depth * 0.5f + 0.03f), new Vector3(width * 0.5f + 0.07f, height + 0.08f, -depth * 0.5f + 0.05f),
                new Vector3(width * 0.5f + 0.07f, height + 0.08f, depth * 0.5f - 0.05f), new Vector3(width * 0.5f, height - 0.01f, depth * 0.5f - 0.03f),
            }), 0.011f), Vector3.zero, palette.Steel);

            BuildTubeRack(palette, CreateChild("Tube Rack", cart.transform, new Vector3(-0.12f, height, 0f)).transform, 0f);
            BuildBottleVisual(CreateChild("Bottle", cart.transform, new Vector3(0.2f, height, 0.05f)).transform, palette, palette.LiquidAmber);
            BuildBottleVisual(CreateChild("Bottle", cart.transform, new Vector3(-0.15f, 0.48f, 0f)).transform, palette, palette.LiquidBlue);
            BuildBeakerVisual(CreateChild("Beaker", cart.transform, new Vector3(0.1f, 0.48f, -0.05f)).transform, palette, palette.LiquidGreen, 0.4f);

            var box = cart.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, height * 0.5f + 0.01f, 0f);
            box.size = new Vector3(width, height, depth);
            box.sharedMaterial = metal;

            var rigidbody = cart.AddComponent<Rigidbody>();
            rigidbody.mass = 18f;
            rigidbody.linearDamping = 2.5f;
            rigidbody.angularDamping = 4f;
            rigidbody.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            rigidbody.interpolation = RigidbodyInterpolation.Interpolate;

            var obstacle = cart.AddComponent<UnityEngine.AI.NavMeshObstacle>();
            obstacle.shape = UnityEngine.AI.NavMeshObstacleShape.Box;
            obstacle.center = new Vector3(0f, height * 0.5f, 0f);
            obstacle.size = new Vector3(width + 0.1f, height, depth + 0.1f);
            obstacle.carving = true;
            obstacle.carveOnlyStationary = false;

            var physicsObject = cart.AddComponent<LabPhysicsObject>();
            SetFloat(physicsObject, "minimumImpactSpeed", 0.6f);
        }
    }
}
