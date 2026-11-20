using UnityEditor;
using UnityEngine;

namespace VRScienceLab.Module2.EditorTools
{
    // The instruments on the main workbench: the digital scale, the combined heating station
    // (hot plate + stopwatch in one housing, see Module2SceneBuilder.HeatingStation.cs) and the
    // thermometer. Each one is a self-contained prefab: housing, console with a tilted backlit
    // display, push buttons, status LED, nameplate and a small hint sign. The behaviour scripts
    // (DigitalScale, HotPlate, StopwatchTimer, Thermometer) are wired exactly as before; the
    // additions are visual (InstrumentStateVisuals, ZoneIndicator, RotatableMount).
    public static partial class Module2SceneBuilder
    {
        // Console slope: 50 degrees from horizontal, so the display faces a standing user.
        private const float ConsoleDepth = 0.07f;
        private const float ConsoleFrontHeight = 0.012f;
        private const float ConsoleBackHeight = 0.095f;

        private static float ConsoleSlopeDegrees => Mathf.Atan2(ConsoleBackHeight - ConsoleFrontHeight, ConsoleDepth) * Mathf.Rad2Deg;

        private static void BuildInstruments(BuildContext context, Transform parent)
        {
            Palette palette = context.Palette;
            context.Scale = BuildDigitalScale(context, palette, parent, new Vector3(-0.95f, BenchTopHeight, InstrumentRowZ));
            BuildHeatingStation(context, palette, parent, new Vector3(0f, BenchTopHeight, InstrumentRowZ));
            context.Thermometer = BuildThermometer(context, palette, parent, new Vector3(0.95f, BenchTopHeight, InstrumentRowZ));
        }

        // ------------------------------------------------------------------
        // Shared instrument parts
        // ------------------------------------------------------------------

        private static void CreateFeet(Transform root, Palette palette, float halfWidth, float halfDepth)
        {
            for (int x = -1; x <= 1; x += 2)
            {
                for (int z = -1; z <= 1; z += 2)
                {
                    Lathe("Foot", root, "InstrumentFoot", CylinderProfile(0.014f, 0.007f, 0.002f), new Vector3(x * halfWidth, 0f, z * halfDepth), palette.Rubber, 16);
                }
            }
        }

        /// <summary>Sloped console housing. Returns the object; its base centre is <paramref name="basePosition"/>.</summary>
        private static GameObject CreateConsole(Transform root, Palette palette, Vector3 basePosition, float width)
        {
            Mesh mesh = ProceduralMeshFactory.Wedge(width, ConsoleDepth, ConsoleFrontHeight, ConsoleBackHeight, 0.008f, 0.003f);
            GameObject console = MeshObject("Console", root, mesh, basePosition, palette.BodyDark);
            var box = console.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, ConsoleBackHeight * 0.45f, 0.008f);
            box.size = new Vector3(width, ConsoleBackHeight * 0.9f, ConsoleDepth - 0.016f);
            return console;
        }

        /// <summary>Point on the console slope at lateral offset x, lifted along the slope normal.</summary>
        private static Vector3 OnConsoleSlope(Vector3 consoleBase, float x, float lift)
        {
            float slope = ConsoleSlopeDegrees * Mathf.Deg2Rad;
            var normal = new Vector3(0f, Mathf.Cos(slope), -Mathf.Sin(slope));
            return consoleBase + new Vector3(x, (ConsoleFrontHeight + ConsoleBackHeight) * 0.5f, 0f) + normal * lift;
        }

        private static DisplayParts CreateConsoleDisplay(Transform root, Palette palette, Vector3 consoleBase, float x, Vector2 panelSize, string initialValue)
        {
            // Panel plane parallel to the slope: rotate (90 - slope) degrees around X.
            return CreateDisplay(root, OnConsoleSlope(consoleBase, x, 0.0145f), 90f - ConsoleSlopeDegrees, panelSize, palette, initialValue);
        }

        private static Renderer CreateConsoleLed(Transform root, Palette palette, Vector3 consoleBase, float x)
        {
            return CreateLed(root, OnConsoleSlope(consoleBase, x, 0.006f), palette);
        }

        /// <summary>Thin strip along the console foot that glows while the instrument is active.</summary>
        private static Renderer CreateAccentStrip(Transform root, Palette palette, Vector3 position, float width)
        {
            GameObject strip = Box("AccentStrip", root, position, new Vector3(width, 0.004f, 0.004f), palette.DisplayGlass, false);
            return strip.GetComponent<Renderer>();
        }

        /// <summary>Small sloped sign in front of the instrument that shows a hint for the current state.</summary>
        private static TMPro.TextMeshPro CreateHintSign(Transform root, Palette palette, Vector3 basePosition, float width, string initialText)
        {
            const float depth = 0.034f, front = 0.004f, back = 0.024f;
            Mesh mesh = ProceduralMeshFactory.Wedge(width, depth, front, back, 0.004f, 0.0015f);
            MeshObject("HintSign", root, mesh, basePosition, palette.BodyDark);

            float slope = Mathf.Atan2(back - front, depth);
            var normal = new Vector3(0f, Mathf.Cos(slope), -Mathf.Sin(slope));
            Vector3 position = basePosition + new Vector3(0f, (front + back) * 0.5f, 0f) + normal * 0.0012f;
            TMPro.TextMeshPro text = CreateText("HintText", root, position, new Vector3(90f - slope * Mathf.Rad2Deg, 0f, 0f), initialText,
                0.105f, LightLabelColor, new Vector2(width * 0.95f, 0.03f));
            return text;
        }

        private static void AddStateVisuals(GameObject root, InstrumentBase instrument, TMPro.TMP_Text hint, Renderer[] glow, string[] inactiveStates, params string[] stateHintPairs)
        {
            var visuals = root.AddComponent<InstrumentStateVisuals>();
            SetReference(visuals, "instrument", instrument);
            SetReference(visuals, "hintText", hint);
            SetReferences(visuals, "glowRenderers", glow);

            var serialized = new SerializedObject(visuals);
            SerializedProperty inactive = serialized.FindProperty("inactiveStates");
            inactive.arraySize = inactiveStates.Length;
            for (int i = 0; i < inactiveStates.Length; i++) inactive.GetArrayElementAtIndex(i).stringValue = inactiveStates[i];

            SerializedProperty hints = serialized.FindProperty("hints");
            hints.arraySize = stateHintPairs.Length / 2;
            for (int i = 0; i < stateHintPairs.Length / 2; i++)
            {
                SerializedProperty element = hints.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("state").stringValue = stateHintPairs[i * 2];
                element.FindPropertyRelative("hint").stringValue = stateHintPairs[i * 2 + 1];
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Renderer CreateZoneRing(Transform root, Palette palette, MeasurementZone zone, Vector3 position, float outerRadius)
        {
            GameObject ring = Lathe("ZoneRing", root, "ZoneRing_" + Mathf.RoundToInt(outerRadius * 1000f), RingProfile(outerRadius, outerRadius - 0.009f, 0.003f), position, palette.Indicator, 40);
            var indicator = ring.AddComponent<ZoneIndicator>();
            SetReference(indicator, "zone", zone);
            SetReference(indicator, "indicatorRenderer", ring.GetComponent<Renderer>());
            return ring.GetComponent<Renderer>();
        }

        // ------------------------------------------------------------------
        // Digital scale
        // ------------------------------------------------------------------

        private static DigitalScale BuildDigitalScale(BuildContext context, Palette palette, Transform parent, Vector3 position)
        {
            GameObject root = CreateRoot("DigitalScale", parent, position);
            var scale = root.AddComponent<DigitalScale>();
            SetString(scale, "instrumentName", "Digital Scale");

            const float bodyTop = 0.052f;
            const float panZ = 0.075f;
            CreateFeet(root.transform, palette, 0.125f, 0.165f);
            Bevel("Body", root.transform, new Vector3(0f, 0.007f, 0f), new Vector3(0.32f, 0.045f, 0.40f), 0.035f, 0.006f, palette.Body, true);

            // Weighing platform: support, brushed steel pan with a raised rim, occupancy ring.
            Lathe("PanSupport", root.transform, "ScalePanSupport", CylinderProfile(0.032f, 0.012f, 0.002f), new Vector3(0f, bodyTop, panZ), palette.DarkPlastic, 24);
            GameObject pan = Lathe("Pan", root.transform, "ScalePan", new[]
            {
                new Vector2(0f, 0f), new Vector2(0.097f, 0f), new Vector2(0.105f, 0.004f), new Vector2(0.105f, 0.011f),
                new Vector2(0.101f, 0.011f), new Vector2(0.098f, 0.0065f), new Vector2(0f, 0.0065f),
            }, new Vector3(0f, bodyTop + 0.012f, panZ), palette.Pan, 48);
            var panCollider = pan.AddComponent<BoxCollider>();
            panCollider.center = new Vector3(0f, 0.0032f, 0f);
            panCollider.size = new Vector3(0.2f, 0.0065f, 0.2f);

            MeasurementZone zone = CreateZone("WeighingZone", root.transform, new Vector3(0f, bodyTop + 0.012f + 0.075f, panZ), new Vector3(0.2f, 0.13f, 0.2f));
            CreateZoneRing(root.transform, palette, zone, new Vector3(0f, bodyTop, panZ), 0.12f);

            // Console with display, LED and a level bubble; push buttons on the front apron.
            var consoleBase = new Vector3(0f, bodyTop, -0.075f);
            CreateConsole(root.transform, palette, consoleBase, 0.30f);
            DisplayParts display = CreateConsoleDisplay(root.transform, palette, consoleBase, -0.035f, new Vector2(0.18f, 0.07f), "OFF");
            Renderer led = CreateConsoleLed(root.transform, palette, consoleBase, 0.115f);
            Renderer strip = CreateAccentStrip(root.transform, palette, new Vector3(0f, bodyTop + 0.003f, -0.112f), 0.27f);
            Lathe("LevelBubble", root.transform, "ScaleLevelBubble", CylinderProfile(0.011f, 0.004f, 0.001f), new Vector3(0.13f, bodyTop, 0.17f), palette.LiquidGreen, 16);
            Lathe("LevelBezel", root.transform, "ScaleLevelBezel", RingProfile(0.0145f, 0.011f, 0.005f), new Vector3(0.13f, bodyTop, 0.17f), palette.Steel, 16);

            InstrumentButton power = CreateButton("PowerButton", "POWER", root.transform, new Vector3(-0.07f, bodyTop, -0.16f), palette.ButtonRed, palette);
            InstrumentButton tare = CreateButton("TareButton", "TARE", root.transform, new Vector3(0.07f, bodyTop, -0.16f), palette.ButtonBlue, palette);
            CreateNameplate(root.transform, "DIGITAL SCALE   MAX 5000 g   d = 0.1 g", new Vector3(0f, 0.029f, -0.2012f), 0.24f, palette);
            TMPro.TextMeshPro hint = CreateHintSign(root.transform, palette, new Vector3(0f, 0f, -0.245f), 0.32f, "Press POWER to switch the scale on");

            SetReference(scale, "valueDisplay", display.Value);
            SetReference(scale, "stateDisplay", display.State);
            SetReference(scale, "statusIndicator", led);
            SetReference(scale, "weighingZone", zone);

            Connect(power, scale.TogglePower);
            Connect(tare, scale.Tare);

            AddStateVisuals(root, scale, hint, new[] { display.Backlight, strip }, new[] { "OFF" },
                "OFF", "Press POWER to switch the scale on",
                "TARE", "Tare active: the display shows the net mass",
                "READING", "Place a sample on the pan. TARE zeroes the display");

            context.ScaleAnchor = CreateChild("SampleAnchor", root.transform, new Vector3(0f, bodyTop + 0.024f, panZ)).transform;
            SaveAsPrefab(root, PrefabFolder, "DigitalScale");
            return scale;
        }

        // ------------------------------------------------------------------
        // Thermometer (meter, sample dock and probe on a stand)
        // ------------------------------------------------------------------

        private static Thermometer BuildThermometer(BuildContext context, Palette palette, Transform parent, Vector3 position)
        {
            GameObject root = CreateRoot("Thermometer", parent, position);
            var thermometer = root.AddComponent<Thermometer>();
            SetString(thermometer, "instrumentName", "Thermometer");

            const float bodyTop = 0.037f;
            var dockCenter = new Vector3(-0.08f, bodyTop, 0.06f);
            CreateFeet(root.transform, palette, 0.135f, 0.155f);
            Bevel("Body", root.transform, new Vector3(0f, 0.007f, 0f), new Vector3(0.34f, 0.03f, 0.38f), 0.03f, 0.005f, palette.Body, true);

            GameObject dock = Lathe("SampleDock", root.transform, "ThermometerDock", CylinderProfile(0.068f, 0.006f, 0.0015f), dockCenter, palette.Dock, 40);
            var dockCollider = dock.AddComponent<BoxCollider>();
            dockCollider.center = new Vector3(0f, 0.003f, 0f);
            dockCollider.size = new Vector3(0.136f, 0.006f, 0.136f);

            MeasurementZone zone = CreateZone("SampleZone", root.transform, dockCenter + new Vector3(0f, 0.075f, 0f), new Vector3(0.13f, 0.12f, 0.13f));
            CreateZoneRing(root.transform, palette, zone, dockCenter, 0.083f);

            // Probe stand, arm, probe and cable are visual only (no colliders) so a sample can be
            // placed under the probe and the needle dips into it.
            var standBase = new Vector3(0.075f, bodyTop, 0.15f);
            Lathe("StandFoot", root.transform, "ThermometerStandFoot", CylinderProfile(0.022f, 0.008f, 0.002f), standBase, palette.Steel, 20);
            Lathe("ProbeStand", root.transform, "ThermometerStand", CylinderProfile(0.0055f, 0.33f, 0.001f), standBase, palette.Probe, 14);
            MeshObject("ProbeArm", root.transform, ProceduralMeshFactory.Tube("ThermometerArm", ProceduralMeshFactory.SmoothPath(new[]
            {
                standBase + new Vector3(0f, 0.31f, 0f),
                new Vector3(0.02f, bodyTop + 0.318f, 0.115f),
                new Vector3(-0.05f, bodyTop + 0.31f, 0.07f),
                dockCenter + new Vector3(0f, 0.30f, 0f),
            }), 0.0045f), Vector3.zero, palette.Probe);
            Lathe("Clamp", root.transform, "ThermometerClamp", CylinderProfile(0.012f, 0.022f, 0.003f), standBase + new Vector3(0f, 0.299f, 0f), palette.DarkPlastic, 16);
            Lathe("ProbeHandle", root.transform, "ThermometerProbeHandle", new[]
            {
                new Vector2(0f, 0f), new Vector2(0.006f, 0f), new Vector2(0.01f, 0.008f), new Vector2(0.01f, 0.06f),
                new Vector2(0.007f, 0.066f), new Vector2(0f, 0.066f),
            }, dockCenter + new Vector3(0f, 0.235f, 0f), palette.BodyDark, 16);
            Lathe("ProbeTip", root.transform, "ThermometerProbeTip", new[]
            {
                new Vector2(0f, 0f), new Vector2(0.0018f, 0.004f), new Vector2(0.0024f, 0.16f), new Vector2(0f, 0.16f),
            }, dockCenter + new Vector3(0f, 0.078f, 0f), palette.Probe, 12);

            var consoleBase = new Vector3(0.045f, bodyTop, -0.075f);
            CreateConsole(root.transform, palette, consoleBase, 0.24f);
            DisplayParts display = CreateConsoleDisplay(root.transform, palette, consoleBase, -0.012f, new Vector2(0.17f, 0.07f), "OFF");
            Renderer led = CreateConsoleLed(root.transform, palette, consoleBase, 0.1f);
            Renderer strip = CreateAccentStrip(root.transform, palette, new Vector3(0.045f, bodyTop + 0.003f, -0.112f), 0.21f);

            MeshObject("ProbeCable", root.transform, ProceduralMeshFactory.Tube("ThermometerCable", ProceduralMeshFactory.SmoothPath(new[]
            {
                dockCenter + new Vector3(0f, 0.3f, 0f),
                dockCenter + new Vector3(0.03f, 0.36f, -0.02f),
                new Vector3(0.1f, bodyTop + 0.26f, -0.01f),
                new Vector3(0.15f, bodyTop + 0.12f, -0.025f),
                new Vector3(0.15f, bodyTop + 0.04f, -0.035f),
            }), 0.0028f, 8), Vector3.zero, palette.Cable);

            InstrumentButton measure = CreateButton("MeasureButton", "MEASURE", root.transform, new Vector3(-0.115f, bodyTop, -0.15f), palette.ButtonGreen, palette);
            InstrumentButton reset = CreateButton("ResetButton", "RESET", root.transform, new Vector3(-0.045f, bodyTop, -0.15f), palette.ButtonGray, palette);
            CreateNameplate(root.transform, "THERMOMETER   0.1 °C", new Vector3(0f, 0.022f, -0.1912f), 0.18f, palette);
            TMPro.TextMeshPro hint = CreateHintSign(root.transform, palette, new Vector3(0f, 0f, -0.235f), 0.34f, "Press MEASURE to switch the thermometer on");

            SetReference(thermometer, "valueDisplay", display.Value);
            SetReference(thermometer, "stateDisplay", display.State);
            SetReference(thermometer, "statusIndicator", led);
            SetReference(thermometer, "sampleZone", zone);

            Connect(measure, thermometer.ToggleMeasuring);
            Connect(reset, thermometer.ResetMeasurement);

            AddStateVisuals(root, thermometer, hint, new[] { display.Backlight, strip }, new[] { "OFF" },
                "OFF", "Press MEASURE to switch the thermometer on",
                "NO SAMPLE", "Place a sample on the dock under the probe",
                "READING", "Reading the sample temperature. RESET clears it");

            context.ThermometerAnchor = CreateChild("SampleAnchor", root.transform, dockCenter + new Vector3(0f, 0.018f, 0f)).transform;
            SaveAsPrefab(root, PrefabFolder, "Thermometer");
            return thermometer;
        }
    }
}
