using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace VRScienceLab.Module2.EditorTools
{
    // Laboratory storage cabinet (the middle unit of the storage wall) and the equipment kept in it.
    //
    //   Laboratory Cabinet                LaboratoryCabinet
    //   ├── Carcass                       closed base cabinet, back, sides, top, three shelf levels
    //   │                                 (every shelf has its own BoxCollider and a front lip)
    //   ├── Door Left / Door Right        CabinetDoor + XRSimpleInteractable + kinematic Rigidbody:
    //   │                                 steel frame, glass panel, pull handle, hinges
    //   └── InteriorZone                  MeasurementZone (trigger) that counts the stored labware
    //
    //   Cabinet Labware                   23 grabbable objects placed on the shelves:
    //       beakers, Erlenmeyer flasks, a test tube rack with six sockets and five test tubes,
    //       laboratory tape, sample vials, specimen jars and reagent bottles.
    //
    // The cabinet is built in a local frame (X = width, Y = height, Z = depth, front at -Z) and
    // then turned so that its front faces the room.
    public static partial class Module2SceneBuilder
    {
        private const float CabinetWidth = 0.9f, CabinetDepth = 0.55f, CabinetHeight = 2.25f;
        private const float CabinetCounterTop = 0.965f;

        // Height of the three storage levels (top face of the counter and of the two glass shelves).
        private static readonly float[] CabinetShelfTops = { CabinetCounterTop, 1.385f, 1.805f };
        private static readonly string[] CabinetShelfLabels = { "BEAKERS   |   JARS   |   VIALS   |   TAPE", "TEST TUBES   |   ERLENMEYER FLASKS", "REAGENT BOTTLES" };

        private static readonly HashSet<string> savedLabwarePrefabs = new HashSet<string>();

        // ------------------------------------------------------------------
        // Cabinet: carcass, shelves, doors
        // ------------------------------------------------------------------

        private static void BuildLaboratoryCabinet(BuildContext context, Palette palette, Transform parent, Vector3 position, float yaw)
        {
            GameObject root = CreateRoot("Laboratory Cabinet", parent, position);
            root.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            var cabinet = root.AddComponent<LaboratoryCabinet>();
            SetString(cabinet, "cabinetName", "Storage Cabinet");

            float halfWidth = CabinetWidth * 0.5f;
            float halfDepth = CabinetDepth * 0.5f;
            float front = -halfDepth;
            float upperHeight = CabinetHeight - CabinetCounterTop;
            float upperCenterY = CabinetCounterTop + upperHeight * 0.5f;

            Transform carcass = CreateChild("Carcass", root.transform, Vector3.zero).transform;

            // ---- Lower section: closed base cabinet (one collider), fascia with the status line.
            var lowerCollider = carcass.gameObject.AddComponent<BoxCollider>();
            lowerCollider.center = new Vector3(0f, CabinetCounterTop * 0.5f, 0f);
            lowerCollider.size = new Vector3(CabinetWidth, CabinetCounterTop, CabinetDepth);

            Box("Plinth", carcass, new Vector3(0f, 0.05f, 0.02f), new Vector3(CabinetWidth, 0.1f, CabinetDepth - 0.04f), palette.Rubber, false);
            Box("Lower Carcass", carcass, new Vector3(0f, 0.5225f, 0f), new Vector3(CabinetWidth - 0.004f, 0.845f, CabinetDepth), palette.Cabinet, false);
            Box("Counter Strip", carcass, new Vector3(0f, CabinetCounterTop - 0.01f, -0.005f), new Vector3(CabinetWidth, 0.02f, CabinetDepth + 0.01f), palette.BenchTop, false);

            float lowerDoorWidth = halfWidth - 0.012f;
            for (int side = -1; side <= 1; side += 2)
            {
                GameObject lowerDoor = Bevel(side < 0 ? "Lower Door L" : "Lower Door R", carcass, new Vector3(side * (lowerDoorWidth * 0.5f + 0.004f), 0.12f, front - 0.009f), new Vector3(lowerDoorWidth, 0.70f, 0.018f), 0.006f, 0.003f, palette.Cabinet);
                lowerDoor.name = side < 0 ? "Lower Door L" : "Lower Door R";
                Box("Lower Handle", carcass, new Vector3(side * 0.04f, 0.66f, front - 0.03f), new Vector3(0.012f, 0.16f, 0.014f), palette.Steel, false);
            }

            Box("Fascia", carcass, new Vector3(0f, 0.885f, front - 0.008f), new Vector3(CabinetWidth - 0.02f, 0.09f, 0.016f), palette.BodyDark, false);
            CreateText("Cabinet Title", carcass, new Vector3(0f, 0.906f, front - 0.0168f), Vector3.zero, "LABORATORY STORAGE   |   GLASSWARE AND CONSUMABLES", 0.2f, LightLabelColor, new Vector2(CabinetWidth - 0.08f, 0.028f), TMPro.FontStyles.Bold);
            TMPro.TextMeshPro status = CreateText("Cabinet Status", carcass, new Vector3(0f, 0.87f, front - 0.0168f), Vector3.zero, "DOORS CLOSED", 0.2f, DisplayTextColor, new Vector2(CabinetWidth - 0.08f, 0.028f));
            NoShadows(Box("Fascia Light", carcass, new Vector3(0f, 0.846f, front - 0.0165f), new Vector3(CabinetWidth - 0.08f, 0.004f, 0.002f), palette.AccentDim, false));

            // ---- Upper section: back, sides and top each carry a collider, so objects stay inside.
            Box("Back Panel", carcass, new Vector3(0f, upperCenterY, halfDepth - 0.01f), new Vector3(CabinetWidth, upperHeight, 0.02f), palette.WallPanel);
            Box("Side L", carcass, new Vector3(-halfWidth + 0.01f, upperCenterY, 0f), new Vector3(0.02f, upperHeight, CabinetDepth), palette.Cabinet);
            Box("Side R", carcass, new Vector3(halfWidth - 0.01f, upperCenterY, 0f), new Vector3(0.02f, upperHeight, CabinetDepth), palette.Cabinet);
            Box("Top", carcass, new Vector3(0f, CabinetHeight - 0.01f, 0f), new Vector3(CabinetWidth, 0.02f, CabinetDepth), palette.Cabinet);

            // Brushed-steel face frame around the door opening.
            for (int side = -1; side <= 1; side += 2)
            {
                Box(side < 0 ? "Frame Post L" : "Frame Post R", carcass, new Vector3(side * (halfWidth - 0.011f), upperCenterY, front + 0.011f), new Vector3(0.022f, upperHeight, 0.022f), palette.BrushedSteel, false);
            }

            Box("Frame Rail Top", carcass, new Vector3(0f, CabinetHeight - 0.011f, front + 0.011f), new Vector3(CabinetWidth, 0.022f, 0.022f), palette.BrushedSteel, false);

            // ---- Storage levels. Level 0 is the counter; levels 1 and 2 are glass shelves.
            float shelfWidth = CabinetWidth - 0.044f;
            float shelfDepth = CabinetDepth - 0.07f;
            const float shelfCenterZ = 0.02f;
            float shelfFront = shelfCenterZ - shelfDepth * 0.5f;
            for (int level = 0; level < CabinetShelfTops.Length; level++)
            {
                float top = CabinetShelfTops[level];
                if (level > 0)
                {
                    NoShadows(Box($"Shelf {level} Glass", carcass, new Vector3(0f, top - 0.006f, shelfCenterZ), new Vector3(shelfWidth, 0.012f, shelfDepth), palette.GlassTinted, false));

                    // The collider is thicker than the visible glass so a dropped object cannot pass through it.
                    GameObject shelfCollider = CreateChild($"Shelf {level} Collider", carcass, new Vector3(0f, top - 0.0125f, shelfCenterZ));
                    shelfCollider.AddComponent<BoxCollider>().size = new Vector3(shelfWidth, 0.025f, shelfDepth);

                    for (int side = -1; side <= 1; side += 2)
                    {
                        Box("Shelf Bracket", carcass, new Vector3(side * (halfWidth - 0.027f), top - 0.018f, shelfCenterZ), new Vector3(0.014f, 0.012f, shelfDepth - 0.06f), palette.BrushedSteel, false);
                    }
                }

                // Low front lip (with a collider): round objects cannot roll out of the cabinet.
                Box($"Shelf {level} Lip", carcass, new Vector3(0f, top + 0.004f, shelfFront + 0.004f), new Vector3(shelfWidth, 0.008f, 0.008f), palette.BrushedSteel);

                // Label holder on the shelf edge.
                float labelY = level == 0 ? top - 0.01f : top - 0.022f;
                float labelZ = level == 0 ? front - 0.0105f : shelfFront - 0.0015f;
                NoShadows(Box($"Shelf {level} Label Plate", carcass, new Vector3(0f, labelY, labelZ + 0.0008f), new Vector3(0.46f, 0.018f, 0.001f), palette.Ceramic, false));
                CreateText($"Shelf {level} Label", carcass, new Vector3(0f, labelY, labelZ), Vector3.zero, CabinetShelfLabels[level], 0.13f, LabelTextColor, new Vector2(0.44f, 0.016f), TMPro.FontStyles.Bold);
            }

            GameObject interiorLight = Box("Cabinet Light", carcass, new Vector3(0f, CabinetHeight - 0.026f, front + 0.07f), new Vector3(CabinetWidth - 0.1f, 0.008f, 0.02f), palette.AccentDim, false);
            NoShadows(interiorLight);

            // ---- Interior zone: everything inside it counts as stored.
            // It starts just above the counter so that even the flat tape rolls reach into it.
            float zoneBottom = CabinetCounterTop + 0.004f, zoneTop = CabinetHeight - 0.03f;
            MeasurementZone zone = CreateZone("InteriorZone", root.transform, new Vector3(0f, (zoneBottom + zoneTop) * 0.5f, 0.005f), new Vector3(CabinetWidth - 0.05f, zoneTop - zoneBottom, CabinetDepth - 0.05f));

            // ---- Hinged glass doors.
            var doors = new Object[2];
            float doorBottom = CabinetCounterTop + 0.01f;
            float doorHeight = upperHeight - 0.02f;
            float leafWidth = halfWidth - 0.004f;
            for (int side = -1; side <= 1; side += 2)
            {
                doors[side < 0 ? 0 : 1] = BuildCabinetDoor(palette, root.transform, side, new Vector3(side * (halfWidth - 0.002f), doorBottom, front - 0.012f), leafWidth, doorHeight);
            }

            SetReferences(cabinet, "doors", doors);
            SetReference(cabinet, "interiorZone", zone);
            SetReference(cabinet, "statusText", status);
            SetReferences(cabinet, "interiorLights", interiorLight.GetComponent<Renderer>());

            context.Cabinet = cabinet;
            context.CabinetRoot = root.transform;
            SaveAsPrefab(root, PrefabFolder, "LaboratoryCabinet");
        }

        /// <summary>
        /// One door leaf. <paramref name="side"/> is -1 for the left door (hinge on the left edge)
        /// and +1 for the right door. The pivot object sits on the hinge line; the leaf extends
        /// from it toward the middle of the cabinet.
        /// </summary>
        private static CabinetDoor BuildCabinetDoor(Palette palette, Transform cabinet, int side, Vector3 hingePosition, float leafWidth, float doorHeight)
        {
            string label = side < 0 ? "Left" : "Right";
            GameObject pivot = CreateChild("Door " + label, cabinet, hingePosition);
            float direction = -side; // local X direction from the hinge toward the meeting edge

            const float thickness = 0.02f, frame = 0.026f;
            Transform leaf = CreateChild("Leaf", pivot.transform, Vector3.zero).transform;
            Box("Stile Hinge", leaf, new Vector3(direction * frame * 0.5f, doorHeight * 0.5f, 0f), new Vector3(frame, doorHeight, thickness), palette.BrushedSteel, false);
            Box("Stile Meeting", leaf, new Vector3(direction * (leafWidth - frame * 0.5f), doorHeight * 0.5f, 0f), new Vector3(frame, doorHeight, thickness), palette.BrushedSteel, false);
            Box("Rail Bottom", leaf, new Vector3(direction * leafWidth * 0.5f, frame * 0.5f, 0f), new Vector3(leafWidth - frame * 2f, frame, thickness), palette.BrushedSteel, false);
            Box("Rail Top", leaf, new Vector3(direction * leafWidth * 0.5f, doorHeight - frame * 0.5f, 0f), new Vector3(leafWidth - frame * 2f, frame, thickness), palette.BrushedSteel, false);
            NoShadows(Box("Glass", leaf, new Vector3(direction * leafWidth * 0.5f, doorHeight * 0.5f, 0f), new Vector3(leafWidth - frame * 2f + 0.004f, doorHeight - frame * 2f + 0.004f, 0.006f), palette.Glass, false));

            float[] hingeHeights = { 0.12f, doorHeight * 0.5f, doorHeight - 0.12f };
            foreach (float y in hingeHeights)
            {
                Lathe("Hinge", leaf, "CabinetHinge", CylinderProfile(0.007f, 0.06f, 0.0015f), new Vector3(0f, y - 0.03f, -0.004f), palette.Steel, 12);
            }

            // Pull handle on the meeting stile, at chest height (about 1.4 m above the floor).
            const float handleCenter = 0.44f, handleHalf = 0.09f, handleStandOff = 0.038f;
            float handleX = direction * (leafWidth - frame * 0.5f);
            MeshObject("Handle", leaf, ProceduralMeshFactory.Tube("CabinetHandle" + label, ProceduralMeshFactory.SmoothPath(new[]
            {
                new Vector3(handleX, handleCenter - handleHalf, -thickness * 0.5f),
                new Vector3(handleX, handleCenter - handleHalf + 0.02f, -handleStandOff),
                new Vector3(handleX, handleCenter + handleHalf - 0.02f, -handleStandOff),
                new Vector3(handleX, handleCenter + handleHalf, -thickness * 0.5f),
            }), 0.007f), Vector3.zero, palette.Steel);

            // Colliders on the pivot itself: the whole leaf and a generous volume around the handle.
            var leafCollider = pivot.AddComponent<BoxCollider>();
            leafCollider.center = new Vector3(direction * leafWidth * 0.5f, doorHeight * 0.5f, 0f);
            leafCollider.size = new Vector3(leafWidth, doorHeight, thickness);
            var handleCollider = pivot.AddComponent<BoxCollider>();
            handleCollider.center = new Vector3(handleX, handleCenter, -handleStandOff * 0.6f);
            handleCollider.size = new Vector3(0.05f, handleHalf * 2f + 0.04f, handleStandOff + 0.02f);

            // Moving colliders belong to a kinematic Rigidbody.
            var rigidbody = pivot.AddComponent<Rigidbody>();
            rigidbody.isKinematic = true;
            rigidbody.useGravity = false;

            // The doors move: they must not be baked into the NavMesh.
            pivot.AddComponent<NavMeshModifier>().ignoreFromBuild = true;

            pivot.AddComponent<XRSimpleInteractable>();
            var door = pivot.AddComponent<CabinetDoor>();
            SetString(door, "doorName", label + " door");
            // Rotating around +Y by a positive angle swings +X toward -Z (out of the cabinet):
            // the left leaf (extending toward +X) opens with +105 degrees, the right one with -105.
            SetFloat(door, "openAngle", side < 0 ? 105f : -105f);
            pivot.AddComponent<HoverHighlight>();
            return door;
        }

        // ------------------------------------------------------------------
        // Cabinet contents
        // ------------------------------------------------------------------

        private static void BuildCabinetContents(BuildContext context)
        {
            if (context.CabinetRoot == null || context.Cabinet == null)
            {
                Debug.LogError("[Module2SceneBuilder] The laboratory cabinet was not built; its contents are skipped.");
                return;
            }

            Palette palette = context.Palette;
            PhysicsMaterial glass = CreatePhysicsMaterial("Glass", 0.35f, 0.45f, 0.05f);
            PhysicsMaterial plastic = CreatePhysicsMaterial("Plastic", 0.5f, 0.6f, 0.05f);
            savedLabwarePrefabs.Clear();

            Transform cabinet = context.CabinetRoot;
            Quaternion rotation = cabinet.rotation;
            Transform parent = new GameObject("Cabinet Labware").transform;
            var contents = new List<Object>();

            // A small gap above the shelf: the objects settle onto it in the first physics steps.
            const float gap = 0.002f;
            float level0 = CabinetShelfTops[0] + gap, level1 = CabinetShelfTops[1] + gap, level2 = CabinetShelfTops[2] + gap;
            Vector3 At(float x, float y, float z) => cabinet.TransformPoint(new Vector3(x, y, z));

            // ---- Level 0: beakers, specimen jars, sample vials, laboratory tape.
            contents.Add(CreateBeaker(palette, glass, parent, "Beaker_600mL", "Beaker 600 mL", 1.3f, 600f, 0.19f, null, 0f, null, 0f, At(-0.33f, level0, 0.10f), rotation));
            contents.Add(CreateBeaker(palette, glass, parent, "Beaker_250mL_Water", "Beaker 250 mL (distilled water)", 1f, 250f, 0.25f, palette.LiquidClear, 0.6f, "Distilled water", 22f, At(-0.2f, level0, 0.10f), rotation));
            contents.Add(CreateBeaker(palette, glass, parent, "Beaker_250mL_Saline", "Beaker 250 mL (saline solution)", 1f, 250f, 0.27f, palette.LiquidBlue, 0.7f, "Saline solution", 18f, At(-0.09f, level0, 0.10f), rotation));
            contents.Add(CreateBeaker(palette, glass, parent, "Beaker_100mL_Indicator", "Beaker 100 mL (indicator)", 0.72f, 100f, 0.11f, palette.LiquidGreen, 0.55f, "Indicator solution", 20f, At(0.01f, level0, 0.10f), rotation));

            contents.Add(CreateSpecimenJar(palette, glass, parent, "SpecimenJar_1", At(0.13f, level0, 0.10f), rotation));
            contents.Add(CreateSpecimenJar(palette, glass, parent, "SpecimenJar_2", At(0.22f, level0, 0.10f), rotation));

            contents.Add(CreateVial(palette, glass, parent, "SampleVial_A", "Sample vial A", palette.LiquidAmber, "Sample A", 20f, At(0.32f, level0, 0.12f), rotation));
            contents.Add(CreateVial(palette, glass, parent, "SampleVial_B", "Sample vial B", palette.LiquidRed, "Sample B", 20f, At(0.375f, level0, 0.12f), rotation));
            contents.Add(CreateVial(palette, glass, parent, "SampleVial_C", "Sample vial C", palette.LiquidBlue, "Sample C", 20f, At(0.3475f, level0, 0.06f), rotation));

            contents.Add(CreateTapeRoll(palette, plastic, parent, "LabTape_Blue", "Laboratory tape (blue)", palette.TapeBlue, At(-0.31f, level0, -0.1f), rotation));
            contents.Add(CreateTapeRoll(palette, plastic, parent, "LabTape_Yellow", "Laboratory tape (yellow)", palette.TapeYellow, At(-0.2f, level0, -0.1f), rotation));

            // ---- Level 1: test tube rack with its tubes, Erlenmeyer flasks.
            TestTubeRack rack = CreateTestTubeRack(palette, plastic, parent, At(-0.2f, level1, 0.03f), rotation, out Labware rackLabware);
            contents.Add(rackLabware);

            Material[] tubeLiquids = { palette.LiquidBlue, palette.LiquidRed, palette.LiquidGreen, palette.LiquidAmber, palette.LiquidClear };
            float[] tubeTemperatures = { 12f, 25f, 37f, 45f, 60f };
            for (int i = 0; i < tubeLiquids.Length; i++)
            {
                // The tubes start in the first five holes; the sixth hole is free.
                LabwareSocket socket = rack.GetSlot(i);
                Transform attach = socket.attachTransform != null ? socket.attachTransform : socket.transform;
                Labware tube = CreateTestTube(palette, glass, parent, "TestTube_" + (i + 1), "Test tube " + (i + 1), tubeLiquids[i], 0.4f + 0.1f * (i % 3), tubeTemperatures[i], attach.position, attach.rotation);
                socket.startingSelectedInteractable = tube.GetComponent<XRGrabInteractable>();
                EditorUtility.SetDirty(socket);
                contents.Add(tube);
            }

            contents.Add(CreateFlask(palette, glass, parent, "Erlenmeyer_500mL", "Erlenmeyer flask 500 mL", 1.25f, 500f, 0.21f, null, 0f, null, 0f, At(0.09f, level1, 0.06f), rotation));
            contents.Add(CreateFlask(palette, glass, parent, "Erlenmeyer_250mL_Buffer", "Erlenmeyer flask 250 mL (buffer)", 1f, 250f, 0.27f, palette.LiquidAmber, 0.6f, "Buffer solution", 30f, At(0.225f, level1, 0.06f), rotation));
            contents.Add(CreateFlask(palette, glass, parent, "Erlenmeyer_125mL_Culture", "Erlenmeyer flask 125 mL (culture medium)", 0.8f, 125f, 0.15f, palette.LiquidGreen, 0.55f, "Culture medium", 37f, At(0.34f, level1, 0.06f), rotation));

            // ---- Level 2: reagent bottles.
            contents.Add(CreateReagentBottle(palette, glass, parent, "ReagentBottle_Ethanol", "Reagent bottle (ethanol 70 %)", palette.LiquidClear, "Ethanol 70 %", 21f, At(-0.3f, level2, 0.1f), rotation));
            contents.Add(CreateReagentBottle(palette, glass, parent, "ReagentBottle_CopperSulfate", "Reagent bottle (copper sulfate)", palette.LiquidBlue, "Copper sulfate solution", 21f, At(-0.19f, level2, 0.1f), rotation));
            contents.Add(CreateReagentBottle(palette, glass, parent, "ReagentBottle_Iodine", "Reagent bottle (iodine solution)", palette.LiquidAmber, "Iodine solution", 21f, At(-0.08f, level2, 0.1f), rotation));

            SetReferences(context.Cabinet, "contents", contents.ToArray());
        }

        // ------------------------------------------------------------------
        // Labware building blocks
        // ------------------------------------------------------------------

        /// <summary>
        /// Makes <paramref name="root"/> a grabbable piece of labware (Rigidbody, XR Grab Interactable,
        /// collision feedback, hover highlight, Labware). Colliders must already be on the object.
        /// </summary>
        private static Labware FinishLabware(GameObject root, string displayName, LabwareKind kind, float capacityMillilitres, float massKilograms, PhysicsMaterial physicsMaterial, string prefabName)
        {
            Labware labware = FinishLabwareWithoutPrefab(root, displayName, kind, capacityMillilitres, massKilograms, physicsMaterial);

            // One prefab per type of object; further copies in the cabinet stay plain scene objects.
            if (savedLabwarePrefabs.Add(prefabName)) SaveAsPrefab(root, LabwarePrefabFolder, prefabName);
            return labware;
        }

        private static void AddTemperature(GameObject root, string content, float temperatureCelsius)
        {
            var sample = root.AddComponent<TemperatureSample>();
            SetString(sample, "sampleName", content);
            SetFloat(sample, "temperatureCelsius", temperatureCelsius);
        }

        /// <summary>Volume mark printed on the glass, facing the front of the object.</summary>
        private static void CreateGlassPrint(Transform parent, string text, float radius, float height)
        {
            CreateText("Print", parent, new Vector3(0f, height, -radius - 0.0008f), Vector3.zero, text, 0.07f, new Color(0.96f, 0.98f, 1f), new Vector2(radius * 1.7f, 0.011f), TMPro.FontStyles.Bold);
        }

        private static GameObject CreateLabwareRoot(string name, Transform parent, Vector3 position, Quaternion rotation)
        {
            GameObject root = CreateRoot(name, parent, position);
            root.transform.rotation = rotation;
            return root;
        }

        private static Transform CreateScaledVisual(Transform root, float scale)
        {
            Transform visual = CreateChild("Visual", root, Vector3.zero).transform;
            visual.localScale = Vector3.one * scale;
            return visual;
        }

        /// <summary>Griffin beaker. The reference model (scale 1) is a 250 mL beaker, 68 mm x 95 mm.</summary>
        private static Labware CreateBeaker(Palette palette, PhysicsMaterial glass, Transform parent, string name, string displayName, float scale, float capacity, float massKilograms,
            Material liquid, float fill, string content, float temperature, Vector3 position, Quaternion rotation)
        {
            GameObject root = CreateLabwareRoot(name, parent, position, rotation);
            BuildBeakerVisual(CreateScaledVisual(root.transform, scale), palette, liquid, fill);

            var box = root.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 0.0475f * scale, 0f);
            box.size = new Vector3(0.066f * scale, 0.095f * scale, 0.066f * scale);

            CreateGlassPrint(root.transform, Mathf.RoundToInt(capacity) + " mL", 0.034f * scale, 0.072f * scale);
            if (liquid != null) AddTemperature(root, content, temperature);
            return FinishLabware(root, displayName, LabwareKind.Beaker, capacity, massKilograms, glass, name);
        }

        /// <summary>Erlenmeyer flask. The reference model (scale 1) is a 250 mL flask, 80 mm x 120 mm.</summary>
        private static Labware CreateFlask(Palette palette, PhysicsMaterial glass, Transform parent, string name, string displayName, float scale, float capacity, float massKilograms,
            Material liquid, float fill, string content, float temperature, Vector3 position, Quaternion rotation)
        {
            GameObject root = CreateLabwareRoot(name, parent, position, rotation);
            BuildFlaskVisual(CreateScaledVisual(root.transform, scale), palette, liquid, fill);

            // Conical body as a box (stable base), neck as a capsule.
            var box = root.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 0.03f * scale, 0f);
            box.size = new Vector3(0.07f * scale, 0.06f * scale, 0.07f * scale);
            var capsule = root.AddComponent<CapsuleCollider>();
            capsule.direction = 1;
            capsule.center = new Vector3(0f, 0.085f * scale, 0f);
            capsule.radius = 0.015f * scale;
            capsule.height = 0.075f * scale;

            CreateGlassPrint(root.transform, Mathf.RoundToInt(capacity) + " mL", 0.027f * scale, 0.045f * scale);
            if (liquid != null) AddTemperature(root, content, temperature);
            return FinishLabware(root, displayName, LabwareKind.ErlenmeyerFlask, capacity, massKilograms, glass, name);
        }

        /// <summary>Test tube, 18 mm x 110 mm, round bottom. Its origin is the lowest point, so it stands in a rack socket.</summary>
        private static Labware CreateTestTube(Palette palette, PhysicsMaterial glass, Transform parent, string name, string displayName, Material liquid, float fill, float temperature, Vector3 position, Quaternion rotation)
        {
            GameObject root = CreateLabwareRoot(name, parent, position, rotation);
            BuildTestTubeVisual(CreateChild("Visual", root.transform, Vector3.zero).transform, palette, liquid, fill);

            var capsule = root.AddComponent<CapsuleCollider>();
            capsule.direction = 1;
            capsule.center = new Vector3(0f, 0.055f, 0f);
            capsule.radius = 0.0095f;
            capsule.height = 0.11f;

            if (liquid != null) AddTemperature(root, displayName, temperature);
            return FinishLabware(root, displayName, LabwareKind.TestTube, 15f, 0.03f, glass, "TestTube");
        }

        /// <summary>Glass reagent bottle with a stopper and a label (about 250 mL).</summary>
        private static Labware CreateReagentBottle(Palette palette, PhysicsMaterial glass, Transform parent, string name, string displayName, Material liquid, string content, float temperature, Vector3 position, Quaternion rotation)
        {
            GameObject root = CreateLabwareRoot(name, parent, position, rotation);
            BuildBottleVisual(CreateChild("Visual", root.transform, Vector3.zero).transform, palette, liquid);

            var box = root.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 0.071f, 0f);
            box.size = new Vector3(0.064f, 0.142f, 0.064f);

            AddTemperature(root, content, temperature);
            return FinishLabware(root, displayName, LabwareKind.ReagentBottle, 250f, 0.35f, glass, "ReagentBottle");
        }

        /// <summary>Small screw-cap sample vial (about 10 mL) with a liquid sample.</summary>
        private static Labware CreateVial(Palette palette, PhysicsMaterial glass, Transform parent, string name, string displayName, Material liquid, string content, float temperature, Vector3 position, Quaternion rotation)
        {
            GameObject root = CreateLabwareRoot(name, parent, position, rotation);
            Transform visual = CreateChild("Visual", root.transform, Vector3.zero).transform;
            Lathe("Glass", visual, "VialGlass", new[]
            {
                new Vector2(0f, 0f), new Vector2(0.0105f, 0f), new Vector2(0.012f, 0.0015f), new Vector2(0.012f, 0.04f),
                new Vector2(0.0085f, 0.045f), new Vector2(0.0085f, 0.05f), new Vector2(0.0072f, 0.05f), new Vector2(0.0072f, 0.044f),
                new Vector2(0.0106f, 0.039f), new Vector2(0.0106f, 0.003f), new Vector2(0f, 0.003f),
            }, Vector3.zero, palette.Glass, 20);
            Lathe("Liquid", visual, "VialLiquid", new[]
            {
                new Vector2(0f, 0.0034f), new Vector2(0.0102f, 0.0034f), new Vector2(0.0102f, 0.028f), new Vector2(0f, 0.028f),
            }, Vector3.zero, liquid, 16);
            Lathe("Cap", visual, "VialCap", CylinderProfile(0.0105f, 0.011f, 0.002f), new Vector3(0f, 0.046f, 0f), palette.DarkPlastic, 16);
            NoShadows(Box("Label", visual, new Vector3(0f, 0.022f, -0.0121f), new Vector3(0.014f, 0.016f, 0.0005f), palette.Ceramic, false));

            var box = root.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 0.0285f, 0f);
            box.size = new Vector3(0.024f, 0.057f, 0.024f);

            AddTemperature(root, content, temperature);
            return FinishLabware(root, displayName, LabwareKind.SampleVial, 10f, 0.02f, glass, "SampleVial");
        }

        /// <summary>Wide specimen jar with a screw lid (about 120 mL), empty.</summary>
        private static Labware CreateSpecimenJar(Palette palette, PhysicsMaterial glass, Transform parent, string name, Vector3 position, Quaternion rotation)
        {
            GameObject root = CreateLabwareRoot(name, parent, position, rotation);
            Transform visual = CreateChild("Visual", root.transform, Vector3.zero).transform;
            Lathe("Glass", visual, "JarGlass", new[]
            {
                new Vector2(0f, 0f), new Vector2(0.027f, 0f), new Vector2(0.03f, 0.003f), new Vector2(0.03f, 0.062f),
                new Vector2(0.027f, 0.068f), new Vector2(0.0255f, 0.068f), new Vector2(0.0282f, 0.061f),
                new Vector2(0.0282f, 0.004f), new Vector2(0f, 0.004f),
            }, Vector3.zero, palette.Glass, 28);
            Lathe("Lid", visual, "JarLid", CylinderProfile(0.0315f, 0.013f, 0.003f), new Vector3(0f, 0.062f, 0f), palette.SampleLid, 28);

            var box = root.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 0.0375f, 0f);
            box.size = new Vector3(0.06f, 0.075f, 0.06f);

            CreateGlassPrint(root.transform, "120 mL", 0.03f, 0.035f);
            return FinishLabware(root, "Specimen jar 120 mL", LabwareKind.SpecimenJar, 120f, 0.12f, glass, "SpecimenJar");
        }

        /// <summary>Roll of laboratory labelling tape lying flat: coloured tape on a cardboard core.</summary>
        private static Labware CreateTapeRoll(Palette palette, PhysicsMaterial plastic, Transform parent, string name, string displayName, Material tape, Vector3 position, Quaternion rotation)
        {
            GameObject root = CreateLabwareRoot(name, parent, position, rotation);
            Transform visual = CreateChild("Visual", root.transform, Vector3.zero).transform;
            const float width = 0.019f;
            Lathe("Tape", visual, "TapeRoll", new[]
            {
                new Vector2(0.0245f, 0f), new Vector2(0.0385f, 0f), new Vector2(0.04f, 0.0015f), new Vector2(0.04f, width - 0.0015f),
                new Vector2(0.0385f, width), new Vector2(0.0245f, width), new Vector2(0.0245f, 0f),
            }, Vector3.zero, tape, 36);
            Lathe("Core", visual, "TapeCore", RingProfile(0.0246f, 0.022f, width), Vector3.zero, palette.Cardboard, 28);
            // Loose end of the tape, so the roll reads as tape and not as a plain ring.
            Box("Tape End", visual, new Vector3(0.0405f, width * 0.5f, 0.004f), new Vector3(0.0008f, width - 0.002f, 0.014f), tape, false);

            var box = root.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, width * 0.5f, 0f);
            box.size = new Vector3(0.078f, width, 0.078f);

            return FinishLabware(root, displayName, LabwareKind.LaboratoryTape, 0f, 0.06f, plastic, name);
        }

        /// <summary>
        /// Test tube rack with six holes. Each hole is a LabwareSocket (XR Socket Interactor that
        /// only accepts test tubes) with a trigger collider and an attach transform at the bottom
        /// of the hole, so a tube released over a free hole is taken and held upright.
        /// </summary>
        private static TestTubeRack CreateTestTubeRack(Palette palette, PhysicsMaterial plastic, Transform parent, Vector3 position, Quaternion rotation, out Labware labware)
        {
            const int holes = 6;
            const float length = 0.3f, depth = 0.07f, baseHeight = 0.012f, topHeight = 0.075f, pitch = 0.045f;
            const float tubeRest = 0.013f; // height of a tube's lowest point above the rack origin

            GameObject root = CreateLabwareRoot("TestTubeRack", parent, position, rotation);
            Transform visual = CreateChild("Visual", root.transform, Vector3.zero).transform;

            Bevel("Base", visual, Vector3.zero, new Vector3(length, baseHeight, depth), 0.006f, 0.002f, palette.RackPlastic);
            for (int side = -1; side <= 1; side += 2)
            {
                Box("End Plate", visual, new Vector3(side * (length * 0.5f - 0.004f), (baseHeight + topHeight + 0.004f) * 0.5f, 0f), new Vector3(0.008f, topHeight + 0.004f - baseHeight, depth - 0.006f), palette.RackPlastic, false);

                // Two guide levels, each made of a front and a back rail.
                Box("Top Rail", visual, new Vector3(0f, topHeight, side * 0.0165f), new Vector3(length - 0.016f, 0.006f, 0.009f), palette.RackPlastic, false);
                Box("Middle Rail", visual, new Vector3(0f, 0.042f, side * 0.0165f), new Vector3(length - 0.016f, 0.005f, 0.009f), palette.RackPlastic, false);
            }

            float firstHole = -pitch * (holes - 1) * 0.5f;
            for (int i = 0; i <= holes; i++)
            {
                // Cross bars between the holes turn the rails into a row of square openings.
                float x = firstHole + (i - 0.5f) * pitch;
                Box("Top Divider", visual, new Vector3(x, topHeight, 0f), new Vector3(0.006f, 0.006f, 0.024f), palette.RackPlastic, false);
                Box("Middle Divider", visual, new Vector3(x, 0.042f, 0f), new Vector3(0.005f, 0.005f, 0.024f), palette.RackPlastic, false);
            }

            for (int i = 0; i < holes; i++)
            {
                // Shallow seat under every hole.
                NoShadows(Lathe("Seat", visual, "RackSeat", RingProfile(0.0115f, 0.008f, 0.0012f), new Vector3(firstHole + i * pitch, baseHeight, 0f), palette.DarkPlastic, 16));
            }

            CreateText("Rack Label", visual, new Vector3(-0.05f, baseHeight * 0.5f, -depth * 0.5f - 0.0006f), Vector3.zero, "TEST TUBE RACK", 0.07f, Color.white, new Vector2(0.16f, 0.009f), TMPro.FontStyles.Bold);
            TMPro.TextMeshPro count = CreateText("Rack Count", visual, new Vector3(0.105f, baseHeight * 0.5f, -depth * 0.5f - 0.0006f), Vector3.zero, "0 / 6", 0.07f, Color.white, new Vector2(0.06f, 0.009f), TMPro.FontStyles.Bold);

            // Solid colliders: the base and the two end plates.
            var baseCollider = root.AddComponent<BoxCollider>();
            baseCollider.center = new Vector3(0f, baseHeight * 0.5f, 0f);
            baseCollider.size = new Vector3(length, baseHeight, depth);
            for (int side = -1; side <= 1; side += 2)
            {
                var endCollider = root.AddComponent<BoxCollider>();
                endCollider.center = new Vector3(side * (length * 0.5f - 0.004f), (baseHeight + topHeight + 0.004f) * 0.5f, 0f);
                endCollider.size = new Vector3(0.008f, topHeight + 0.004f - baseHeight, depth - 0.006f);
            }

            labware = FinishLabwareWithoutPrefab(root, "Test tube rack", LabwareKind.TestTubeRack, 0f, 0.25f, plastic);

            // Sockets: created after the grabbable so the rack's Rigidbody already exists.
            var slots = new Object[holes];
            for (int i = 0; i < holes; i++)
            {
                GameObject slot = CreateChild("Slot " + (i + 1), root.transform, new Vector3(firstHole + i * pitch, 0.05f, 0f));
                var trigger = slot.AddComponent<SphereCollider>();
                trigger.isTrigger = true;
                trigger.radius = 0.02f;

                Transform attach = CreateChild("Attach", slot.transform, new Vector3(0f, tubeRest - 0.05f, 0f)).transform;
                var socket = slot.AddComponent<LabwareSocket>();
                socket.attachTransform = attach;
                socket.recycleDelayTime = 0.5f;
                slots[i] = socket;
            }

            var rack = root.AddComponent<TestTubeRack>();
            SetReferences(rack, "slots", slots);
            SetReference(rack, "countText", count);

            savedLabwarePrefabs.Add("TestTubeRack");
            SaveAsPrefab(root, LabwarePrefabFolder, "TestTubeRack");
            return rack;
        }

        // Same as FinishLabware, for objects that still get more components before being saved as a prefab.
        private static Labware FinishLabwareWithoutPrefab(GameObject root, string displayName, LabwareKind kind, float capacityMillilitres, float massKilograms, PhysicsMaterial physicsMaterial)
        {
            MakeGrabbable(root, massKilograms, physicsMaterial);

            var labware = root.AddComponent<Labware>();
            SetString(labware, "displayName", displayName);
            SetFloat(labware, "capacityMillilitres", capacityMillilitres);
            var serialized = new SerializedObject(labware);
            serialized.FindProperty("kind").enumValueIndex = (int)kind;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return labware;
        }
    }
}
