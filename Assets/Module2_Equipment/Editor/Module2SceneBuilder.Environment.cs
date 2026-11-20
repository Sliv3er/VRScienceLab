using Unity.AI.Navigation;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;

namespace VRScienceLab.Module2.EditorTools
{
    // Laboratory room: architecture, furniture, decorative equipment, lighting and NavMesh.
    // Room interior: X from -4.5 to 4.5, Z from -5.0 (rear wall) to 1.25 (wall behind the bench),
    // height 3.2 m. The user starts at the origin facing +Z, in front of the main workbench.
    public static partial class Module2SceneBuilder
    {
        private const float RoomMinX = -4.5f, RoomMaxX = 4.5f, RoomMinZ = -5f, RoomMaxZ = 1.25f, RoomHeight = 3.2f;
        private const string NavMeshAssetPath = ModuleRoot + "/Scenes/Module2_Lab_NavMesh.asset";
        private const string ReflectionAssetPath = ModuleRoot + "/Scenes/Module2_Lab_Reflection.exr";

        private static void ConfigureLighting()
        {
            // Key light with soft shadows; the room shell does not cast shadows, so this behaves
            // like the combined light of the ceiling fixtures.
            var key = new GameObject("Key Light");
            key.transform.rotation = Quaternion.Euler(58f, -22f, 0f);
            Light light = key.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.98f, 0.95f);
            light.intensity = 0.85f;
            light.shadows = LightShadows.Soft;
            light.shadowStrength = 0.55f;
            light.shadowBias = 0.02f;
            light.shadowNormalBias = 0.25f;

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.60f, 0.64f, 0.69f);
            RenderSettings.ambientEquatorColor = new Color(0.50f, 0.53f, 0.57f);
            RenderSettings.ambientGroundColor = new Color(0.34f, 0.35f, 0.37f);
            RenderSettings.fog = false;
            RenderSettings.skybox = null;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
            RenderSettings.reflectionIntensity = 0.8f;
        }

        private static void BuildEnvironment(BuildContext context)
        {
            Palette palette = context.Palette;

            Transform architecture = CreateChild("Architecture", context.Environment, Vector3.zero).transform;
            Transform furniture = CreateChild("Furniture", context.Environment, Vector3.zero).transform;
            Transform details = CreateChild("Details", context.Environment, Vector3.zero).transform;

            // Furniture tops must not become walkable islands in the NavMesh.
            var furnitureModifier = furniture.gameObject.AddComponent<NavMeshModifier>();
            furnitureModifier.overrideArea = true;
            furnitureModifier.area = NavMesh.GetAreaFromName("Not Walkable");

            // Details are decoration only: they are neither walkable nor obstacles.
            var detailsModifier = details.gameObject.AddComponent<NavMeshModifier>();
            detailsModifier.ignoreFromBuild = true;

            BuildRoomShell(palette, architecture);
            BuildCeilingLights(palette, architecture);
            BuildMainWorkbench(palette, furniture, details);
            BuildStorageWall(context, palette, furniture, details);
            BuildFumeHood(palette, furniture);
            BuildAnalysisDesk(palette, furniture, details);
            BuildIslandTable(palette, furniture, details);
            BuildWallDetails(palette, details);

            MarkStatic(context.Environment);
            UnmarkAnimatedObjects(context.Environment);
        }

        // ------------------------------------------------------------------
        // Architecture
        // ------------------------------------------------------------------

        private static void BuildRoomShell(Palette palette, Transform parent)
        {
            float width = RoomMaxX - RoomMinX;
            float depth = RoomMaxZ - RoomMinZ;
            float centerZ = (RoomMinZ + RoomMaxZ) * 0.5f;
            const float wall = 0.12f;

            NoShadows(Box("Floor", parent, new Vector3(0f, -0.05f, centerZ), new Vector3(width + 0.3f, 0.1f, depth + 0.3f), palette.Floor));

            GameObject ceiling = Box("Ceiling", parent, new Vector3(0f, RoomHeight + 0.05f, centerZ), new Vector3(width + 0.3f, 0.1f, depth + 0.3f), palette.Ceiling);
            NoShadows(ceiling);
            ceiling.AddComponent<NavMeshModifier>().ignoreFromBuild = true;

            NoShadows(Box("Wall Back", parent, new Vector3(0f, RoomHeight * 0.5f, RoomMaxZ + wall * 0.5f), new Vector3(width + 0.3f, RoomHeight, wall), palette.Wall));
            NoShadows(Box("Wall Rear", parent, new Vector3(0f, RoomHeight * 0.5f, RoomMinZ - wall * 0.5f), new Vector3(width + 0.3f, RoomHeight, wall), palette.Wall));
            NoShadows(Box("Wall Left", parent, new Vector3(RoomMinX - wall * 0.5f, RoomHeight * 0.5f, centerZ), new Vector3(wall, RoomHeight, depth), palette.Wall));

            // Right wall with a band of three windows.
            float rightX = RoomMaxX + wall * 0.5f;
            const float sill = 1.0f, head = 2.45f, windowMinZ = -3.7f, windowMaxZ = 0.5f;
            NoShadows(Box("Wall Right Lower", parent, new Vector3(rightX, sill * 0.5f, centerZ), new Vector3(wall, sill, depth), palette.Wall));
            NoShadows(Box("Wall Right Upper", parent, new Vector3(rightX, (head + RoomHeight) * 0.5f, centerZ), new Vector3(wall, RoomHeight - head, depth), palette.Wall));
            NoShadows(Box("Wall Right End A", parent, new Vector3(rightX, (sill + head) * 0.5f, (RoomMinZ + windowMinZ) * 0.5f), new Vector3(wall, head - sill, windowMinZ - RoomMinZ), palette.Wall));
            NoShadows(Box("Wall Right End B", parent, new Vector3(rightX, (sill + head) * 0.5f, (RoomMaxZ + windowMaxZ) * 0.5f), new Vector3(wall, head - sill, RoomMaxZ - windowMaxZ), palette.Wall));

            float paneWidth = (windowMaxZ - windowMinZ) / 3f;
            for (int i = 0; i < 3; i++)
            {
                float z = windowMinZ + paneWidth * (i + 0.5f);
                NoShadows(Box($"Window Glass {i + 1}", parent, new Vector3(RoomMaxX + 0.05f, (sill + head) * 0.5f, z), new Vector3(0.012f, head - sill, paneWidth - 0.08f), palette.Glass, false));
            }

            for (int i = 0; i <= 3; i++)
            {
                float z = windowMinZ + paneWidth * i;
                NoShadows(Box($"Window Mullion {i + 1}", parent, new Vector3(RoomMaxX + 0.05f, (sill + head) * 0.5f, z), new Vector3(0.09f, head - sill, 0.08f), palette.WindowFrame, false));
            }

            NoShadows(Box("Window Sill", parent, new Vector3(RoomMaxX + 0.01f, sill - 0.02f, (windowMinZ + windowMaxZ) * 0.5f), new Vector3(0.16f, 0.04f, windowMaxZ - windowMinZ + 0.1f), palette.WindowFrame, false));
            NoShadows(Box("Window Head", parent, new Vector3(RoomMaxX + 0.05f, head + 0.02f, (windowMinZ + windowMaxZ) * 0.5f), new Vector3(0.09f, 0.04f, windowMaxZ - windowMinZ + 0.1f), palette.WindowFrame, false));
            NoShadows(Quad("Window Sky", parent, new Vector3(RoomMaxX + 0.6f, (sill + head) * 0.5f, (windowMinZ + windowMaxZ) * 0.5f), 90f, new Vector2(windowMaxZ - windowMinZ + 1.4f, head - sill + 0.9f), palette.Sky));

            // Skirting boards and a thin accent line at dado height.
            const float skirt = 0.1f;
            NoShadows(Box("Skirting Back", parent, new Vector3(0f, skirt * 0.5f, RoomMaxZ - 0.008f), new Vector3(width, skirt, 0.016f), palette.Skirting, false));
            NoShadows(Box("Skirting Rear", parent, new Vector3(0f, skirt * 0.5f, RoomMinZ + 0.008f), new Vector3(width, skirt, 0.016f), palette.Skirting, false));
            NoShadows(Box("Skirting Left", parent, new Vector3(RoomMinX + 0.008f, skirt * 0.5f, centerZ), new Vector3(0.016f, skirt, depth), palette.Skirting, false));
            NoShadows(Box("Skirting Right", parent, new Vector3(RoomMaxX - 0.008f, skirt * 0.5f, centerZ), new Vector3(0.016f, skirt, depth), palette.Skirting, false));
            NoShadows(Box("Wall Band Rear", parent, new Vector3(0f, 1.0f, RoomMinZ + 0.006f), new Vector3(width, 0.02f, 0.012f), palette.AccentDim, false));
            NoShadows(Box("Wall Band Left", parent, new Vector3(RoomMinX + 0.006f, 1.0f, centerZ), new Vector3(0.012f, 0.02f, depth), palette.AccentDim, false));

            // Door in the rear wall.
            Transform door = CreateChild("Door", parent, new Vector3(-1.9f, 0f, RoomMinZ + 0.02f)).transform;
            NoShadows(Box("Frame", door, new Vector3(0f, 1.06f, 0f), new Vector3(1.08f, 2.12f, 0.05f), palette.WindowFrame, false));
            NoShadows(Box("Leaf", door, new Vector3(0f, 1.03f, 0.012f), new Vector3(0.96f, 2.04f, 0.045f), palette.Door, false));
            NoShadows(Box("Window", door, new Vector3(0.18f, 1.55f, 0.036f), new Vector3(0.22f, 0.62f, 0.006f), palette.GlassTinted, false));
            NoShadows(Box("Handle", door, new Vector3(-0.36f, 1.02f, 0.05f), new Vector3(0.14f, 0.022f, 0.03f), palette.Steel, false));
            NoShadows(Box("Kick Plate", door, new Vector3(0f, 0.14f, 0.036f), new Vector3(0.9f, 0.22f, 0.004f), palette.BrushedSteel, false));
            NoShadows(Box("Exit Light", door, new Vector3(0f, 2.24f, 0.03f), new Vector3(0.34f, 0.12f, 0.04f), palette.Green, false));
        }

        private static void BuildCeilingLights(Palette palette, Transform parent)
        {
            Transform lights = CreateChild("Ceiling Lights", parent, Vector3.zero).transform;
            float[] xs = { -2.9f, 0f, 2.9f };
            float[] zs = { -0.35f, -3.1f };
            int index = 0;
            foreach (float z in zs)
            {
                foreach (float x in xs)
                {
                    index++;
                    Transform fixture = CreateChild($"Fixture {index}", lights, new Vector3(x, RoomHeight, z)).transform;
                    NoShadows(Box("Housing", fixture, new Vector3(0f, -0.02f, 0f), new Vector3(1.36f, 0.04f, 0.72f), palette.WindowFrame, false));
                    NoShadows(Box("Diffuser", fixture, new Vector3(0f, -0.043f, 0f), new Vector3(1.26f, 0.008f, 0.62f), palette.CeilingLight, false));
                }
            }

            // Three fill lights without shadows (the key light is the fourth per-pixel light).
            CreateFillLight(lights, new Vector3(-2.6f, 2.85f, -1.4f));
            CreateFillLight(lights, new Vector3(2.6f, 2.85f, -1.4f));
            CreateFillLight(lights, new Vector3(0f, 2.85f, -3.6f));
        }

        private static void CreateFillLight(Transform parent, Vector3 position)
        {
            GameObject go = CreateChild("Fill Light", parent, position);
            Light light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(0.94f, 0.97f, 1f);
            light.range = 9f;
            light.intensity = 0.9f;
            light.shadows = LightShadows.None;
            light.renderMode = LightRenderMode.ForcePixel;
        }

        // ------------------------------------------------------------------
        // Main workbench (the four instruments stand on it)
        // ------------------------------------------------------------------

        private static void BuildMainWorkbench(Palette palette, Transform furniture, Transform details)
        {
            Transform bench = CreateChild("Workbench", furniture, Vector3.zero).transform;
            const float width = 3.8f, frontZ = 0.2f, backZ = 1.15f;
            float depth = backZ - frontZ;
            float centerZ = (frontZ + backZ) * 0.5f;

            GameObject top = Bevel("Workbench Top", bench, new Vector3(0f, BenchTopHeight - 0.04f, centerZ), new Vector3(width, 0.04f, depth), 0.012f, 0.004f, palette.BenchTop, true);
            top.name = "Workbench";

            // Base cabinets, set back from the front edge to leave knee room.
            const int modules = 5;
            float moduleWidth = (width - 0.1f) / modules;
            float cabinetFront = frontZ + 0.12f;
            float cabinetDepth = backZ - cabinetFront;
            for (int i = 0; i < modules; i++)
            {
                float x = -width * 0.5f + 0.05f + moduleWidth * (i + 0.5f);
                Transform module = CreateChild($"Base Cabinet {i + 1}", bench, new Vector3(x, 0f, cabinetFront + cabinetDepth * 0.5f)).transform;
                Box("Carcass", module, new Vector3(0f, 0.49f, 0f), new Vector3(moduleWidth - 0.004f, 0.74f, cabinetDepth), palette.Cabinet);
                Box("Toe Kick", module, new Vector3(0f, 0.06f, 0.04f), new Vector3(moduleWidth, 0.12f, cabinetDepth - 0.08f), palette.Rubber, false);

                bool drawers = i == 2;
                if (drawers)
                {
                    for (int d = 0; d < 3; d++)
                    {
                        float y = 0.24f + d * 0.245f;
                        Bevel($"Drawer {d + 1}", module, new Vector3(0f, y - 0.11f, -cabinetDepth * 0.5f - 0.008f), new Vector3(moduleWidth - 0.03f, 0.22f, 0.018f), 0.006f, 0.003f, palette.CabinetGrey);
                        Box($"Drawer Handle {d + 1}", module, new Vector3(0f, y + 0.06f, -cabinetDepth * 0.5f - 0.03f), new Vector3(0.22f, 0.012f, 0.014f), palette.Steel, false);
                    }
                }
                else
                {
                    for (int side = -1; side <= 1; side += 2)
                    {
                        float doorWidth = (moduleWidth - 0.03f) * 0.5f - 0.004f;
                        Bevel(side < 0 ? "Door L" : "Door R", module, new Vector3(side * (doorWidth * 0.5f + 0.002f), 0.14f, -cabinetDepth * 0.5f - 0.008f), new Vector3(doorWidth, 0.70f, 0.018f), 0.006f, 0.003f, palette.Cabinet);
                        Box(side < 0 ? "Handle L" : "Handle R", module, new Vector3(side * 0.035f, 0.66f, -cabinetDepth * 0.5f - 0.03f), new Vector3(0.012f, 0.16f, 0.014f), palette.Steel, false);
                    }
                }
            }

            // Service riser behind the bench with outlets, taps and a soft light line.
            Box("Service Riser", bench, new Vector3(0f, 1.16f, (backZ + RoomMaxZ) * 0.5f), new Vector3(width, 0.52f, RoomMaxZ - backZ), palette.WallPanel);
            Box("Riser Cap", bench, new Vector3(0f, 1.43f, (backZ + RoomMaxZ) * 0.5f - 0.01f), new Vector3(width + 0.02f, 0.02f, RoomMaxZ - backZ + 0.02f), palette.BrushedSteel, false);
            NoShadows(Box("Riser Light Line", details, new Vector3(0f, 1.405f, backZ - 0.004f), new Vector3(width - 0.1f, 0.01f, 0.008f), palette.AccentDim, false));

            float[] outletXs = { -1.6f, -0.64f, 0.0f, 0.64f, 1.6f };
            foreach (float x in outletXs)
            {
                Transform outlet = CreateChild("Power Outlet", details, new Vector3(x, 1.08f, backZ - 0.004f)).transform;
                Bevel("Plate", outlet, new Vector3(0f, -0.045f, 0f), new Vector3(0.15f, 0.09f, 0.008f), 0.004f, 0.002f, palette.Cabinet);
                for (int socket = -1; socket <= 1; socket += 2)
                {
                    Box("Socket", outlet, new Vector3(socket * 0.036f, 0f, -0.004f), new Vector3(0.05f, 0.05f, 0.004f), palette.DarkPlastic, false);
                    Box("Switch", outlet, new Vector3(socket * 0.036f, 0.034f, -0.005f), new Vector3(0.016f, 0.008f, 0.004f), palette.AccentDim, false);
                }
            }

            // Gas / water taps between the outlets.
            float[] tapXs = { -1.12f, 1.12f };
            foreach (float x in tapXs)
            {
                Transform tap = CreateChild("Service Tap", details, new Vector3(x, 1.06f, backZ)).transform;
                Lathe("Rosette", tap, "TapRosette", CylinderProfile(0.03f, 0.012f, 0.003f), Vector3.zero, palette.Steel, 24).transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
                MeshObject("Spout", tap, ProceduralMeshFactory.Tube("TapSpout", ProceduralMeshFactory.SmoothPath(new[]
                {
                    new Vector3(0f, 0f, 0f), new Vector3(0f, 0.01f, -0.07f), new Vector3(0f, -0.02f, -0.11f), new Vector3(0f, -0.06f, -0.115f),
                }), 0.009f), Vector3.zero, palette.Steel);
                Lathe("Valve", tap, "TapValve", CylinderProfile(0.018f, 0.022f, 0.004f), new Vector3(0f, 0.012f, -0.07f), x < 0f ? palette.ButtonBlue : palette.Warning, 16);
            }

            // Trays that organise the bench: calibration weights on the left, samples on the right.
            Bevel("Weight Tray", bench, new Vector3(-1.56f, BenchTopHeight, 0.55f), new Vector3(0.52f, 0.008f, 0.34f), 0.02f, 0.002f, palette.Fabric);
            Bevel("Sample Tray", bench, new Vector3(1.52f, BenchTopHeight, 0.535f), new Vector3(0.64f, 0.008f, 0.38f), 0.02f, 0.002f, palette.BrushedSteel);
        }

        // ------------------------------------------------------------------
        // Storage wall (left): tall cabinets with glass-fronted upper sections.
        // Units 1 and 3 are closed display units; unit 2 is the interactive laboratory cabinet
        // (see Module2SceneBuilder.Cabinet.cs).
        // ------------------------------------------------------------------

        private const int InteractiveStorageUnit = 1;

        private static void BuildStorageWall(BuildContext context, Palette palette, Transform furniture, Transform details)
        {
            Transform storage = CreateChild("Storage Cabinets", furniture, Vector3.zero).transform;
            const float depth = 0.55f, unitWidth = 0.9f, lowerHeight = 0.95f, totalHeight = 2.25f;
            float centerX = RoomMinX + depth * 0.5f;
            float frontX = RoomMinX + depth;
            float[] centersZ = { 0.6f, -0.3f, -1.2f };
            Material[] liquids = { palette.LiquidBlue, palette.LiquidAmber, palette.LiquidGreen, palette.LiquidRed, palette.LiquidClear };

            for (int i = 0; i < centersZ.Length; i++)
            {
                float z = centersZ[i];
                if (i == InteractiveStorageUnit)
                {
                    // The middle unit is the functional storage cabinet (hinged doors, shelves with
                    // colliders, grabbable equipment). Its local front (-Z) is turned toward the room (+X).
                    BuildLaboratoryCabinet(context, palette, storage, new Vector3(centerX, 0f, z), -90f);
                    continue;
                }

                Transform unit = CreateChild($"Storage Unit {i + 1}", storage, new Vector3(centerX, 0f, z)).transform;

                // One collider for the whole unit; the visible parts carry none.
                var collider = unit.gameObject.AddComponent<BoxCollider>();
                collider.center = new Vector3(0f, totalHeight * 0.5f, 0f);
                collider.size = new Vector3(depth, totalHeight, unitWidth);

                Box("Lower Carcass", unit, new Vector3(0f, lowerHeight * 0.5f + 0.05f, 0f), new Vector3(depth, lowerHeight - 0.1f, unitWidth - 0.004f), palette.Cabinet, false);
                Box("Plinth", unit, new Vector3(-0.02f, 0.05f, 0f), new Vector3(depth - 0.04f, 0.1f, unitWidth), palette.Rubber, false);
                Box("Counter Strip", unit, new Vector3(0.005f, lowerHeight + 0.01f, 0f), new Vector3(depth + 0.01f, 0.02f, unitWidth), palette.BenchTop, false);

                for (int side = -1; side <= 1; side += 2)
                {
                    float doorWidth = unitWidth * 0.5f - 0.012f;
                    GameObject door = Bevel(side < 0 ? "Lower Door A" : "Lower Door B", unit, new Vector3(depth * 0.5f + 0.009f, 0.12f, side * (doorWidth * 0.5f + 0.004f)), new Vector3(0.018f, lowerHeight - 0.14f, doorWidth), 0.006f, 0.003f, palette.Cabinet);
                    door.name = side < 0 ? "Lower Door A" : "Lower Door B";
                    Box("Handle", unit, new Vector3(depth * 0.5f + 0.03f, 0.72f, side * 0.04f), new Vector3(0.014f, 0.16f, 0.012f), palette.Steel, false);
                }

                // Upper display section: back, sides, top, shelves and glass doors.
                float upperHeight = totalHeight - lowerHeight - 0.02f;
                float upperCenterY = lowerHeight + 0.02f + upperHeight * 0.5f;
                Box("Upper Back", unit, new Vector3(-depth * 0.5f + 0.01f, upperCenterY, 0f), new Vector3(0.02f, upperHeight, unitWidth), palette.WallPanel, false);
                Box("Upper Side A", unit, new Vector3(0f, upperCenterY, -unitWidth * 0.5f + 0.01f), new Vector3(depth, upperHeight, 0.02f), palette.Cabinet, false);
                Box("Upper Side B", unit, new Vector3(0f, upperCenterY, unitWidth * 0.5f - 0.01f), new Vector3(depth, upperHeight, 0.02f), palette.Cabinet, false);
                Box("Upper Top", unit, new Vector3(0f, totalHeight - 0.01f, 0f), new Vector3(depth, 0.02f, unitWidth), palette.Cabinet, false);
                NoShadows(Box("Glass Doors", unit, new Vector3(depth * 0.5f - 0.004f, upperCenterY, 0f), new Vector3(0.008f, upperHeight - 0.03f, unitWidth - 0.05f), palette.Glass, false));
                Box("Door Stile", unit, new Vector3(depth * 0.5f - 0.002f, upperCenterY, 0f), new Vector3(0.014f, upperHeight - 0.03f, 0.02f), palette.Steel, false);
                NoShadows(Box("Cabinet Light", unit, new Vector3(depth * 0.5f - 0.06f, totalHeight - 0.03f, 0f), new Vector3(0.02f, 0.008f, unitWidth - 0.1f), palette.AccentDim, false));

                for (int shelf = 0; shelf < 3; shelf++)
                {
                    float y = lowerHeight + 0.03f + shelf * 0.40f;
                    if (shelf > 0) NoShadows(Box($"Shelf {shelf}", unit, new Vector3(0f, y, 0f), new Vector3(depth - 0.06f, 0.012f, unitWidth - 0.04f), palette.GlassTinted, false));

                    // Glassware on the shelf (decoration only).
                    for (int slot = 0; slot < 4; slot++)
                    {
                        int variant = (i * 7 + shelf * 3 + slot) % 5;
                        var position = new Vector3(centerX + 0.03f + (slot % 2) * 0.1f - 0.05f, y + 0.008f, z - 0.3f + slot * 0.2f);
                        Transform holder = CreateChild("Stored Glassware", details, position).transform;
                        Material liquid = liquids[(i + shelf + slot) % liquids.Length];
                        if (variant == 0 || variant == 3) BuildBeakerVisual(holder, palette, liquid, 0.45f + 0.1f * slot);
                        else if (variant == 1 || variant == 4) BuildFlaskVisual(holder, palette, liquid, 0.5f);
                        else BuildBottleVisual(holder, palette, liquid);
                    }
                }
            }
        }

        // ------------------------------------------------------------------
        // Fume hood (rear left)
        // ------------------------------------------------------------------

        private static void BuildFumeHood(Palette palette, Transform furniture)
        {
            const float width = 1.5f, depth = 0.85f, height = 2.35f;
            Transform hood = CreateChild("Fume Hood", furniture, new Vector3(RoomMinX + depth * 0.5f, 0f, -3.35f)).transform;

            var collider = hood.gameObject.AddComponent<BoxCollider>();
            collider.center = new Vector3(0f, height * 0.5f, 0f);
            collider.size = new Vector3(depth, height, width);

            Box("Base", hood, new Vector3(0f, 0.45f, 0f), new Vector3(depth, 0.9f, width), palette.Cabinet, false);
            Box("Plinth", hood, new Vector3(0.02f, 0.05f, 0f), new Vector3(depth - 0.02f, 0.1f, width + 0.002f), palette.Rubber, false);
            Box("Worktop", hood, new Vector3(0.01f, 0.915f, 0f), new Vector3(depth + 0.02f, 0.03f, width), palette.BenchTop, false);
            Box("Back", hood, new Vector3(-depth * 0.5f + 0.015f, 1.63f, 0f), new Vector3(0.03f, 1.4f, width), palette.WallPanel, false);
            Box("Side A", hood, new Vector3(0f, 1.63f, -width * 0.5f + 0.02f), new Vector3(depth, 1.4f, 0.04f), palette.Cabinet, false);
            Box("Side B", hood, new Vector3(0f, 1.63f, width * 0.5f - 0.02f), new Vector3(depth, 1.4f, 0.04f), palette.Cabinet, false);
            Box("Canopy", hood, new Vector3(0f, 2.24f, 0f), new Vector3(depth, 0.22f, width), palette.Cabinet, false);
            Box("Duct", hood, new Vector3(-0.1f, 2.78f, 0f), new Vector3(0.36f, 0.86f, 0.36f), palette.BrushedSteel, false);
            NoShadows(Box("Sash Glass", hood, new Vector3(depth * 0.5f - 0.02f, 1.78f, 0f), new Vector3(0.01f, 0.7f, width - 0.1f), palette.GlassTinted, false));
            Box("Sash Handle", hood, new Vector3(depth * 0.5f - 0.005f, 1.43f, 0f), new Vector3(0.03f, 0.03f, width - 0.12f), palette.Steel, false);
            NoShadows(Box("Hood Light", hood, new Vector3(0.05f, 2.12f, 0f), new Vector3(0.3f, 0.01f, width - 0.2f), palette.CeilingLight, false));
            Box("Control Strip", hood, new Vector3(depth * 0.5f + 0.004f, 2.24f, 0f), new Vector3(0.008f, 0.1f, 0.5f), palette.DarkPlastic, false);
            NoShadows(Box("Airflow Indicator", hood, new Vector3(depth * 0.5f + 0.009f, 2.24f, 0.16f), new Vector3(0.004f, 0.03f, 0.1f), palette.Accent, false));

            for (int side = -1; side <= 1; side += 2)
            {
                Bevel(side < 0 ? "Base Door A" : "Base Door B", hood, new Vector3(depth * 0.5f + 0.009f, 0.13f, side * 0.37f), new Vector3(0.018f, 0.72f, 0.7f), 0.006f, 0.003f, palette.CabinetGrey);
                Box("Handle", hood, new Vector3(depth * 0.5f + 0.03f, 0.7f, side * 0.06f), new Vector3(0.014f, 0.16f, 0.012f), palette.Steel, false);
            }

            Transform inside = CreateChild("Hood Glassware", hood, new Vector3(0.02f, 0.93f, 0f)).transform;
            BuildFlaskVisual(CreateChild("Flask", inside, new Vector3(-0.1f, 0f, -0.3f)).transform, palette, palette.LiquidGreen, 0.55f);
            BuildBeakerVisual(CreateChild("Beaker", inside, new Vector3(0.05f, 0f, 0.1f)).transform, palette, palette.LiquidAmber, 0.6f);
            BuildBottleVisual(CreateChild("Bottle", inside, new Vector3(-0.15f, 0f, 0.4f)).transform, palette, palette.LiquidBlue);
        }

        // ------------------------------------------------------------------
        // Analysis desk under the windows (right)
        // ------------------------------------------------------------------

        private static void BuildAnalysisDesk(Palette palette, Transform furniture, Transform details)
        {
            const float depth = 0.72f, minZ = -3.5f, maxZ = 0.3f, topHeight = 0.78f;
            float centerX = RoomMaxX - depth * 0.5f;
            float length = maxZ - minZ;
            Transform desk = CreateChild("Analysis Desk", furniture, new Vector3(centerX, 0f, (minZ + maxZ) * 0.5f)).transform;

            GameObject top = Bevel("Desk Top", desk, new Vector3(0f, topHeight - 0.035f, 0f), new Vector3(depth, 0.035f, length), 0.012f, 0.004f, palette.BenchTop, true);
            top.name = "Analysis Desk";
            float[] legZs = { -length * 0.5f + 0.06f, 0f, length * 0.5f - 0.06f };
            foreach (float z in legZs)
            {
                Box("Leg Frame", desk, new Vector3(0f, (topHeight - 0.035f) * 0.5f, z), new Vector3(depth - 0.08f, topHeight - 0.035f, 0.04f), palette.BrushedSteel);
            }

            Box("Drawer Unit", desk, new Vector3(0.02f, 0.4f, length * 0.5f - 0.36f), new Vector3(depth - 0.1f, 0.62f, 0.5f), palette.Cabinet);
            for (int d = 0; d < 3; d++)
            {
                Box("Drawer Handle", desk, new Vector3(-depth * 0.5f + 0.055f, 0.2f + d * 0.2f, length * 0.5f - 0.36f), new Vector3(0.012f, 0.012f, 0.2f), palette.Steel, false);
            }

            Transform props = CreateChild("Desk Equipment", details, new Vector3(centerX, topHeight, 0f)).transform;
            BuildMicroscope(palette, CreateChild("Microscope", props, new Vector3(0.02f, 0f, -1.5f)).transform, -90f);
            BuildWorkstation(palette, CreateChild("Workstation", props, new Vector3(0.1f, 0f, -0.55f)).transform, -90f);
            BuildTubeRack(palette, CreateChild("Tube Rack", props, new Vector3(-0.05f, 0f, -2.5f)).transform, 90f);
            BuildFlaskVisual(CreateChild("Flask", props, new Vector3(0.12f, 0f, -2.95f)).transform, palette, palette.LiquidAmber, 0.5f);

            BuildStool(palette, furniture, new Vector3(RoomMaxX - depth - 0.3f, 0f, -1.4f));
            BuildStool(palette, furniture, new Vector3(RoomMaxX - depth - 0.3f, 0f, -2.6f));
        }

        // ------------------------------------------------------------------
        // Island table (centre rear) — the obstacle the robot drives around
        // ------------------------------------------------------------------

        private static void BuildIslandTable(Palette palette, Transform furniture, Transform details)
        {
            const float width = 1.8f, depth = 0.9f;
            var center = new Vector3(0f, 0f, -2.6f);
            Transform island = CreateChild("Island Table", furniture, center).transform;

            GameObject top = Bevel("Island Top", island, new Vector3(0f, BenchTopHeight - 0.04f, 0f), new Vector3(width, 0.04f, depth), 0.012f, 0.004f, palette.BenchTop, true);
            top.name = "Island Table";
            for (int side = -1; side <= 1; side += 2)
            {
                Box("Pedestal", island, new Vector3(side * (width * 0.5f - 0.28f), 0.43f, 0f), new Vector3(0.5f, 0.86f, depth - 0.14f), palette.Cabinet);
                Box("Pedestal Handle", island, new Vector3(side * (width * 0.5f - 0.28f), 0.7f, -depth * 0.5f + 0.06f), new Vector3(0.18f, 0.012f, 0.014f), palette.Steel, false);
            }

            Box("Lower Shelf", island, new Vector3(0f, 0.22f, 0f), new Vector3(width - 1.0f, 0.02f, depth - 0.2f), palette.BrushedSteel, false);
            NoShadows(Box("Island Light Line", island, new Vector3(0f, BenchTopHeight - 0.05f, -depth * 0.5f + 0.01f), new Vector3(width - 0.1f, 0.008f, 0.006f), palette.AccentDim, false));

            Transform props = CreateChild("Island Equipment", details, center + new Vector3(0f, BenchTopHeight, 0f)).transform;
            BuildTubeRack(palette, CreateChild("Tube Rack", props, new Vector3(-0.5f, 0f, 0.05f)).transform, 0f);
            BuildMicroscope(palette, CreateChild("Microscope", props, new Vector3(0.45f, 0f, 0.05f)).transform, 180f);
            BuildBeakerVisual(CreateChild("Beaker", props, new Vector3(-0.05f, 0f, -0.15f)).transform, palette, palette.LiquidGreen, 0.5f);
            BuildFlaskVisual(CreateChild("Flask", props, new Vector3(0.1f, 0f, 0.2f)).transform, palette, palette.LiquidBlue, 0.6f);

            BuildStool(palette, furniture, center + new Vector3(-0.45f, 0f, -0.78f));
            BuildStool(palette, furniture, center + new Vector3(0.45f, 0f, -0.78f));
        }

        // ------------------------------------------------------------------
        // Reusable props
        // ------------------------------------------------------------------

        private static void BuildStool(Palette palette, Transform parent, Vector3 position)
        {
            Transform stool = CreateChild("Lab Stool", parent, position).transform;
            var collider = stool.gameObject.AddComponent<CapsuleCollider>();
            collider.center = new Vector3(0f, 0.32f, 0f);
            collider.radius = 0.19f;
            collider.height = 0.64f;

            Lathe("Base", stool, "StoolBase", new[]
            {
                new Vector2(0f, 0f), new Vector2(0.21f, 0f), new Vector2(0.22f, 0.012f), new Vector2(0.2f, 0.028f),
                new Vector2(0.05f, 0.05f), new Vector2(0.03f, 0.07f), new Vector2(0f, 0.07f),
            }, Vector3.zero, palette.Steel);
            Lathe("Column", stool, "StoolColumn", CylinderProfile(0.025f, 0.5f, 0.002f), new Vector3(0f, 0.06f, 0f), palette.Steel, 16);
            Lathe("Foot Ring", stool, "StoolFootRing", RingProfile(0.17f, 0.15f, 0.018f), new Vector3(0f, 0.24f, 0f), palette.Steel);
            Lathe("Seat", stool, "StoolSeat", new[]
            {
                new Vector2(0f, 0f), new Vector2(0.15f, 0f), new Vector2(0.175f, 0.02f), new Vector2(0.175f, 0.05f),
                new Vector2(0.15f, 0.07f), new Vector2(0f, 0.075f),
            }, new Vector3(0f, 0.56f, 0f), palette.Fabric);
        }

        private static void BuildMicroscope(Palette palette, Transform root, float yaw)
        {
            root.localRotation = Quaternion.Euler(0f, yaw, 0f);
            Bevel("Base", root, Vector3.zero, new Vector3(0.16f, 0.025f, 0.22f), 0.03f, 0.005f, palette.Body);
            GameObject arm = Bevel("Arm", root, new Vector3(0f, 0.02f, 0.075f), new Vector3(0.05f, 0.26f, 0.05f), 0.012f, 0.004f, palette.Body);
            arm.transform.localRotation = Quaternion.Euler(-8f, 0f, 0f);
            Bevel("Stage", root, new Vector3(0f, 0.12f, -0.01f), new Vector3(0.12f, 0.012f, 0.12f), 0.01f, 0.002f, palette.DarkPlastic);
            Bevel("Head", root, new Vector3(0f, 0.25f, 0.02f), new Vector3(0.07f, 0.06f, 0.12f), 0.015f, 0.004f, palette.Body);
            GameObject tube = Lathe("Eyepiece", root, "MicroscopeEyepiece", CylinderProfile(0.014f, 0.1f, 0.002f), new Vector3(0f, 0.29f, -0.03f), palette.DarkPlastic, 16);
            tube.transform.localRotation = Quaternion.Euler(-35f, 0f, 0f);
            Lathe("Objective", root, "MicroscopeObjective", new[]
            {
                new Vector2(0f, 0f), new Vector2(0.008f, 0f), new Vector2(0.014f, 0.02f), new Vector2(0.014f, 0.06f), new Vector2(0f, 0.06f),
            }, new Vector3(0f, 0.19f, -0.01f), palette.Steel, 16);
            Lathe("Focus Knob", root, "MicroscopeKnob", CylinderProfile(0.022f, 0.09f, 0.004f), new Vector3(-0.045f, 0.09f, 0.085f), palette.DarkPlastic, 16).transform.localRotation = Quaternion.Euler(0f, 0f, -90f);
            NoShadows(Box("Illuminator", root, new Vector3(0f, 0.027f, -0.01f), new Vector3(0.03f, 0.004f, 0.03f), palette.CeilingLight, false));
        }

        private static void BuildWorkstation(Palette palette, Transform root, float yaw)
        {
            root.localRotation = Quaternion.Euler(0f, yaw, 0f);
            Lathe("Stand Base", root, "MonitorStandBase", CylinderProfile(0.11f, 0.012f, 0.004f), Vector3.zero, palette.DarkPlastic);
            Box("Stand Neck", root, new Vector3(0f, 0.12f, 0.02f), new Vector3(0.04f, 0.24f, 0.02f), palette.DarkPlastic, false);
            GameObject screen = Bevel("Monitor", root, new Vector3(0f, 0.16f, 0f), new Vector3(0.56f, 0.34f, 0.022f), 0.01f, 0.003f, palette.DarkPlastic);
            screen.name = "Monitor";
            NoShadows(Box("Screen", root, new Vector3(0f, 0.335f, -0.012f), new Vector3(0.53f, 0.3f, 0.002f), palette.AccentDim, false));
            NoShadows(Box("Screen Graph A", root, new Vector3(-0.1f, 0.36f, -0.0135f), new Vector3(0.26f, 0.12f, 0.001f), palette.DisplayGlass, false));
            NoShadows(Box("Screen Graph B", root, new Vector3(0.14f, 0.3f, -0.0135f), new Vector3(0.16f, 0.2f, 0.001f), palette.DisplayGlass, false));
            Bevel("Keyboard", root, new Vector3(0f, 0f, -0.22f), new Vector3(0.36f, 0.012f, 0.12f), 0.008f, 0.003f, palette.DarkPlastic);
            Bevel("Mouse", root, new Vector3(0.27f, 0f, -0.22f), new Vector3(0.055f, 0.022f, 0.095f), 0.02f, 0.008f, palette.DarkPlastic);
        }

        private static void BuildTubeRack(Palette palette, Transform root, float yaw)
        {
            root.localRotation = Quaternion.Euler(0f, yaw, 0f);
            Bevel("Rack Base", root, Vector3.zero, new Vector3(0.24f, 0.012f, 0.07f), 0.008f, 0.002f, palette.SampleBox);
            Bevel("Rack Top", root, new Vector3(0f, 0.06f, 0f), new Vector3(0.24f, 0.008f, 0.07f), 0.008f, 0.002f, palette.SampleBox);
            for (int side = -1; side <= 1; side += 2)
            {
                Box("Rack Post", root, new Vector3(side * 0.112f, 0.036f, 0f), new Vector3(0.008f, 0.05f, 0.06f), palette.SampleBox, false);
            }

            Material[] liquids = { palette.LiquidBlue, palette.LiquidRed, palette.LiquidGreen, palette.LiquidAmber, palette.LiquidClear };
            for (int i = 0; i < 5; i++)
            {
                Transform tube = CreateChild("Tube", root, new Vector3(-0.088f + i * 0.044f, 0.014f, 0f)).transform;
                BuildTestTubeVisual(tube, palette, liquids[i], 0.35f + 0.1f * (i % 3));
            }
        }

        private static GameObject Quad(string name, Transform parent, Vector3 position, float yaw, Vector2 size, Material material)
        {
            GameObject quad = CreatePrimitive(PrimitiveType.Quad, name, parent, position, new Vector3(size.x, size.y, 1f), material, false);
            quad.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            return quad;
        }

        // ------------------------------------------------------------------
        // Wall details: framed diagrams, safety pictograms, clock, ventilation, extinguisher
        // ------------------------------------------------------------------

        private static void BuildWallDetails(Palette palette, Transform details)
        {
            Material periodic = CreateMaterial("Diagram_Periodic", Color.white, 0.2f, texture: ProceduralTextureFactory.PeriodicGrid());
            Material molecule = CreateMaterial("Diagram_Molecule", Color.white, 0.2f, texture: ProceduralTextureFactory.Molecule());
            Material curve = CreateMaterial("Diagram_HeatingCurve", Color.white, 0.2f, texture: ProceduralTextureFactory.HeatingCurve());
            Material goggles = CreateMaterial("Sign_Goggles", Color.white, 0.3f, texture: ProceduralTextureFactory.GogglesSign());
            Material coat = CreateMaterial("Sign_LabCoat", Color.white, 0.3f, texture: ProceduralTextureFactory.LabCoatSign());

            // Rear wall (faces +Z).
            FramedPicture(palette, details, "Diagram Periodic Table", new Vector3(1.05f, 1.85f, RoomMinZ), 180f, new Vector2(1.6f, 1.0f), periodic);
            FramedPicture(palette, details, "Diagram Molecule", new Vector3(2.55f, 1.85f, RoomMinZ), 180f, new Vector2(0.8f, 1.0f), molecule);
            FramedPicture(palette, details, "Sign Goggles", new Vector3(-0.95f, 1.75f, RoomMinZ), 180f, new Vector2(0.3f, 0.3f), goggles, 0.008f);
            FramedPicture(palette, details, "Sign Lab Coat", new Vector3(-0.58f, 1.75f, RoomMinZ), 180f, new Vector2(0.3f, 0.3f), coat, 0.008f);

            // Left wall (faces +X), between the storage cabinets and the fume hood.
            FramedPicture(palette, details, "Diagram Heating Curve", new Vector3(RoomMinX, 1.8f, -2.15f), -90f, new Vector2(0.8f, 0.6f), curve);

            BuildWallClock(palette, details, new Vector3(2.95f, 2.3f, RoomMaxZ));
            BuildVentilation(palette, details, new Vector3(-2.95f, 2.5f, RoomMaxZ));

            // Fire extinguisher beside the door.
            Transform extinguisher = CreateChild("Fire Extinguisher", details, new Vector3(-1.1f, 0.12f, RoomMinZ + 0.11f)).transform;
            Lathe("Cylinder", extinguisher, "Extinguisher", new[]
            {
                new Vector2(0f, 0f), new Vector2(0.065f, 0f), new Vector2(0.07f, 0.01f), new Vector2(0.07f, 0.36f),
                new Vector2(0.05f, 0.42f), new Vector2(0.022f, 0.44f), new Vector2(0.022f, 0.47f), new Vector2(0f, 0.47f),
            }, Vector3.zero, palette.Red, 24);
            Box("Valve", extinguisher, new Vector3(0f, 0.49f, 0.01f), new Vector3(0.03f, 0.04f, 0.09f), palette.DarkPlastic, false);
            Box("Bracket", extinguisher, new Vector3(0f, 0.25f, -0.085f), new Vector3(0.1f, 0.3f, 0.02f), palette.BrushedSteel, false);
        }

        private static void FramedPicture(Palette palette, Transform parent, string name, Vector3 wallPosition, float yaw, Vector2 size, Material picture, float frame = 0.025f)
        {
            Transform root = CreateChild(name, parent, wallPosition).transform;
            root.localRotation = Quaternion.Euler(0f, yaw, 0f);
            // In local space the wall is at z = 0 and the room is toward -Z.
            NoShadows(Box("Frame", root, new Vector3(0f, 0f, -0.012f), new Vector3(size.x + frame * 2f, size.y + frame * 2f, 0.024f), palette.WindowFrame, false));
            NoShadows(Quad("Picture", root, new Vector3(0f, 0f, -0.0245f), 0f, size, picture));
        }

        private static void BuildWallClock(Palette palette, Transform parent, Vector3 wallPosition)
        {
            Transform clock = CreateChild("Wall Clock", parent, wallPosition).transform;
            GameObject face = Lathe("Face", clock, "ClockFace", CylinderProfile(0.17f, 0.012f, 0.002f), new Vector3(0f, 0f, -0.012f), palette.Ceramic);
            face.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            GameObject rim = Lathe("Rim", clock, "ClockRim", RingProfile(0.185f, 0.168f, 0.03f), new Vector3(0f, 0f, -0.03f), palette.WindowFrame);
            rim.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            for (int i = 0; i < 12; i++)
            {
                Transform mark = CreateChild("Hour Mark", clock, new Vector3(0f, 0f, -0.0135f)).transform;
                mark.localRotation = Quaternion.Euler(0f, 0f, i * 30f);
                Box("Tick", mark, new Vector3(0f, 0.145f, 0f), new Vector3(i % 3 == 0 ? 0.012f : 0.006f, 0.026f, 0.002f), palette.DarkPlastic, false);
            }

            Transform hour = CreateChild("Hour Hand", clock, new Vector3(0f, 0f, -0.016f)).transform;
            hour.localRotation = Quaternion.Euler(0f, 0f, -305f);
            Box("Hand", hour, new Vector3(0f, 0.04f, 0f), new Vector3(0.012f, 0.1f, 0.002f), palette.DarkPlastic, false);
            Transform minute = CreateChild("Minute Hand", clock, new Vector3(0f, 0f, -0.018f)).transform;
            minute.localRotation = Quaternion.Euler(0f, 0f, -60f);
            Box("Hand", minute, new Vector3(0f, 0.06f, 0f), new Vector3(0.008f, 0.14f, 0.002f), palette.DarkPlastic, false);

            // Second hand: continuous Transform rotation, 6 degrees per second (clockwise seen from the room).
            Transform second = CreateChild("Second Hand", clock, new Vector3(0f, 0f, -0.02f)).transform;
            Box("Hand", second, new Vector3(0f, 0.055f, 0f), new Vector3(0.004f, 0.15f, 0.002f), palette.Red, false);
            var animator = second.gameObject.AddComponent<TransformAnimator>();
            SetVector3(animator, "rotationSpeed", new Vector3(0f, 0f, 6f));
            Lathe("Hub", clock, "ClockHub", CylinderProfile(0.01f, 0.006f, 0.001f), new Vector3(0f, 0f, -0.022f), palette.DarkPlastic, 12).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        }

        private static void BuildVentilation(Palette palette, Transform parent, Vector3 wallPosition)
        {
            Transform vent = CreateChild("Ventilation", parent, wallPosition).transform;
            NoShadows(Box("Housing", vent, new Vector3(0f, 0f, -0.03f), new Vector3(0.62f, 0.62f, 0.06f), palette.BrushedSteel, false));
            GameObject well = Lathe("Well", vent, "VentWell", CylinderProfile(0.26f, 0.01f, 0.002f), new Vector3(0f, 0f, -0.062f), palette.DarkPlastic);
            well.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            // Fan: continuous Transform rotation around the wall normal.
            Transform fan = CreateChild("Fan", vent, new Vector3(0f, 0f, -0.075f)).transform;
            for (int i = 0; i < 5; i++)
            {
                Transform blade = CreateChild("Blade", fan, Vector3.zero).transform;
                blade.localRotation = Quaternion.Euler(0f, 0f, i * 72f);
                GameObject plate = Box("Plate", blade, new Vector3(0f, 0.125f, 0f), new Vector3(0.085f, 0.2f, 0.004f), palette.CabinetGrey, false);
                plate.transform.localRotation = Quaternion.Euler(0f, 24f, 0f);
                NoShadows(plate);
            }

            Lathe("Fan Hub", fan, "VentHub", CylinderProfile(0.04f, 0.03f, 0.006f), new Vector3(0f, 0f, 0.012f), palette.DarkPlastic, 20).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var animator = fan.gameObject.AddComponent<TransformAnimator>();
            SetVector3(animator, "rotationSpeed", new Vector3(0f, 0f, 110f));

            for (int i = -3; i <= 3; i++)
            {
                NoShadows(Box("Grille Bar", vent, new Vector3(0f, i * 0.075f, -0.095f), new Vector3(0.56f, 0.008f, 0.006f), palette.Steel, false));
            }

            NoShadows(Lathe("Grille Ring", vent, "VentRing", RingProfile(0.285f, 0.262f, 0.012f), new Vector3(0f, 0f, -0.09f), palette.Steel));
            vent.Find("Grille Ring").localRotation = Quaternion.Euler(90f, 0f, 0f);
        }

        // Objects animated at runtime must not be static (static batching would freeze them).
        private static void UnmarkAnimatedObjects(Transform root)
        {
            foreach (CabinetDoor door in root.GetComponentsInChildren<CabinetDoor>(true))
            {
                foreach (Transform child in door.GetComponentsInChildren<Transform>(true))
                {
                    child.gameObject.isStatic = false;
                }
            }

            foreach (MeasurementZone zone in root.GetComponentsInChildren<MeasurementZone>(true))
            {
                zone.gameObject.isStatic = false;
            }

            foreach (TransformAnimator animator in root.GetComponentsInChildren<TransformAnimator>(true))
            {
                foreach (Transform child in animator.GetComponentsInChildren<Transform>(true))
                {
                    child.gameObject.isStatic = false;
                }
            }
        }

        // ------------------------------------------------------------------
        // NavMesh and reflections
        // ------------------------------------------------------------------

        /// <summary>
        /// Adds a NavMeshSurface to the environment and bakes it from the physics colliders of the
        /// room and furniture. Called before any movable object exists, so only static geometry is baked.
        /// </summary>
        private static void BakeNavMesh(BuildContext context)
        {
            var surface = context.Environment.gameObject.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.layerMask = ~0;
            surface.overrideVoxelSize = true;
            surface.voxelSize = 0.05f;
            surface.minRegionArea = 1f;

            Physics.SyncTransforms();
            surface.BuildNavMesh();

            if (surface.navMeshData == null)
            {
                Debug.LogError("[Module2SceneBuilder] NavMesh bake produced no data.");
                return;
            }

            AssetDatabase.DeleteAsset(NavMeshAssetPath);
            AssetDatabase.CreateAsset(surface.navMeshData, NavMeshAssetPath);
            EditorUtility.SetDirty(surface);
        }

        [MenuItem("VR Science Lab/Module 2/Bake NavMesh", false, 2)]
        public static void RebakeNavMesh()
        {
            NavMeshSurface surface = Object.FindFirstObjectByType<NavMeshSurface>();
            if (surface == null)
            {
                Debug.LogWarning("[Module2SceneBuilder] No NavMeshSurface in the open scene. Open or build the lab scene first.");
                return;
            }

            Physics.SyncTransforms();
            surface.BuildNavMesh();
            string path = AssetDatabase.GetAssetPath(surface.navMeshData);
            if (string.IsNullOrEmpty(path))
            {
                AssetDatabase.DeleteAsset(NavMeshAssetPath);
                AssetDatabase.CreateAsset(surface.navMeshData, NavMeshAssetPath);
            }

            EditorUtility.SetDirty(surface);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(surface.gameObject.scene);
            Debug.Log("[Module2SceneBuilder] NavMesh baked. Save the scene to keep it.");
        }

        /// <summary>One baked reflection probe so steel, glass and the polished floor reflect the room.</summary>
        private static void BakeReflections()
        {
            var go = new GameObject("Reflection Probe");
            go.transform.position = new Vector3(0f, 1.6f, -1.6f);
            var probe = go.AddComponent<ReflectionProbe>();
            probe.mode = ReflectionProbeMode.Baked;
            probe.size = new Vector3(RoomMaxX - RoomMinX + 0.4f, RoomHeight + 0.4f, RoomMaxZ - RoomMinZ + 0.4f);
            probe.center = new Vector3(0f, 0f, (RoomMinZ + RoomMaxZ) * 0.5f + 1.6f);
            probe.boxProjection = true;
            probe.resolution = 128;
            probe.hdr = true;
            probe.intensity = 0.9f;

            try
            {
                if (!Lightmapping.BakeReflectionProbe(probe, ReflectionAssetPath))
                {
                    Debug.LogWarning("[Module2SceneBuilder] Reflection probe bake failed; the scene uses ambient reflections only.");
                    return;
                }

                AssetDatabase.ImportAsset(ReflectionAssetPath);
                var cubemap = AssetDatabase.LoadAssetAtPath<Cubemap>(ReflectionAssetPath);
                if (cubemap != null)
                {
                    probe.bakedTexture = cubemap;
                    RenderSettings.customReflectionTexture = cubemap;
                }
            }
            catch (System.Exception exception)
            {
                Debug.LogWarning("[Module2SceneBuilder] Reflection probe bake skipped: " + exception.Message);
            }
        }
    }
}
