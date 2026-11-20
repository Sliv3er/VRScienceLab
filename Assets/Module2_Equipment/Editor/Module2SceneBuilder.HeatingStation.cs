using UnityEditor;
using UnityEngine;

namespace VRScienceLab.Module2.EditorTools
{
    // Combined heating station: ONE piece of equipment that houses the hot plate and the
    // stopwatch. The housing, feet, vents and status strip are shared; each instrument keeps its
    // own module (display, buttons, LED, hint) and its own behaviour script:
    //
    //   HeatingStation                 CombinedHeatingStation (facade, no logic)
    //   ├── Body, feet, vents, strip   shared housing
    //   ├── HotPlate                   HotPlate + HeatingSurface + InstrumentStateVisuals
    //   │     ceramic top, heating element, console with the temperature display,
    //   │     ON / OFF / RESET buttons, heating zone
    //   └── Stopwatch                  StopwatchTimer + RotatableMount + InstrumentStateVisuals
    //         timer deck, swivel display head, START / PAUSE / RESET and swivel buttons
    //
    // The child objects keep the names "HotPlate" and "Stopwatch" on purpose: LabDirectory finds
    // the instruments by those names, exactly as before.
    public static partial class Module2SceneBuilder
    {
        private static void BuildHeatingStation(BuildContext context, Palette palette, Transform parent, Vector3 position)
        {
            GameObject root = CreateRoot("HeatingStation", parent, position);
            var station = root.AddComponent<CombinedHeatingStation>();

            const float width = 0.60f, depth = 0.42f;
            const float bodyTop = 0.077f;
            const float heaterX = -0.135f, timerX = 0.165f;
            float frontFace = -depth * 0.5f;

            // ---------------- Shared housing ----------------
            CreateFeet(root.transform, palette, width * 0.5f - 0.035f, depth * 0.5f - 0.035f);
            Bevel("Body", root.transform, new Vector3(0f, 0.007f, 0f), new Vector3(width, 0.07f, depth), 0.03f, 0.006f, palette.Body, true);

            for (int side = -1; side <= 1; side += 2)
            {
                for (int slot = 0; slot < 5; slot++)
                {
                    Box("Vent", root.transform, new Vector3(side * (width * 0.5f + 0.0003f), 0.042f, 0.02f + slot * 0.03f), new Vector3(0.002f, 0.03f, 0.012f), palette.DarkPlastic, false);
                }
            }

            // Thin groove between the heating module and the timer module, and the shared status strip.
            Box("ModuleDivider", root.transform, new Vector3(0.018f, bodyTop + 0.0004f, -0.02f), new Vector3(0.003f, 0.001f, depth - 0.07f), palette.DarkPlastic, false);
            Renderer stationStrip = CreateAccentStrip(root.transform, palette, new Vector3(0f, 0.066f, frontFace - 0.0005f), width - 0.09f);
            stationStrip.gameObject.name = "StationStatusStrip";

            HotPlate hotPlate = BuildHeaterModule(context, palette, root.transform, heaterX, bodyTop, frontFace);
            StopwatchTimer stopwatch = BuildTimerModule(context, palette, root.transform, timerX, bodyTop, frontFace);

            SetReference(station, "hotPlate", hotPlate);
            SetReference(station, "stopwatch", stopwatch);
            SetReferences(station, "activityRenderers", stationStrip);

            context.HotPlate = hotPlate;
            context.Stopwatch = stopwatch;
            context.HeatingStation = station;

            // The hot plate and the stopwatch no longer exist as separate pieces of equipment.
            AssetDatabase.DeleteAsset(PrefabFolder + "/HotPlate.prefab");
            AssetDatabase.DeleteAsset(PrefabFolder + "/Stopwatch.prefab");
            SaveAsPrefab(root, PrefabFolder, "HeatingStation");
        }

        // ------------------------------------------------------------------
        // Heating module (left): heating surface, temperature display, ON / OFF / RESET
        // ------------------------------------------------------------------

        private static HotPlate BuildHeaterModule(BuildContext context, Palette palette, Transform station, float x, float bodyTop, float frontFace)
        {
            Transform module = CreateChild("HotPlate", station, new Vector3(x, 0f, 0f)).transform;
            var hotPlate = module.gameObject.AddComponent<HotPlate>();
            SetString(hotPlate, "instrumentName", "Hot Plate");

            const float plateZ = 0.075f;
            const float plateThickness = 0.012f;

            // Ceramic top with the heating zone (its emission follows the temperature).
            Bevel("CeramicTop", module, new Vector3(0f, bodyTop, plateZ), new Vector3(0.26f, plateThickness, 0.26f), 0.012f, 0.003f, palette.Ceramic, true);
            GameObject element = Lathe("HeatingElement", module, "HotPlateElement", CylinderProfile(0.1f, 0.0016f, 0.0005f), new Vector3(0f, bodyTop + plateThickness, plateZ), palette.HeatingElement, 48);
            for (int i = 1; i <= 3; i++)
            {
                Lathe("HeatingRing", module, "HotPlateRing" + i, RingProfile(0.028f * i + 0.002f, 0.028f * i, 0.0004f), new Vector3(0f, bodyTop + plateThickness + 0.0018f, plateZ), palette.DarkPlastic, 40);
            }

            // Containers standing on the surface are detected by this zone (ring lights when occupied).
            MeasurementZone zone = CreateZone("HeatingZone", module, new Vector3(0f, bodyTop + plateThickness + 0.07f, plateZ), new Vector3(0.22f, 0.13f, 0.22f));
            CreateZoneRing(module, palette, zone, new Vector3(0f, bodyTop + plateThickness, plateZ), 0.121f);

            GameObject sign = Box("HotSurfaceSign", module, new Vector3(-0.14f, 0.04f, frontFace - 0.0006f), new Vector3(0.034f, 0.034f, 0.001f), palette.Warning, false);
            sign.name = "HotSurfaceSign";

            var lightObject = new GameObject("HeatLight");
            lightObject.transform.SetParent(module, false);
            lightObject.transform.localPosition = new Vector3(0f, 0.2f, plateZ);
            Light heatLight = lightObject.AddComponent<Light>();
            heatLight.type = LightType.Point;
            heatLight.color = new Color(1f, 0.45f, 0.15f);
            heatLight.range = 0.7f;
            heatLight.intensity = 0f;
            heatLight.shadows = LightShadows.None;
            heatLight.enabled = false;

            // Console with the temperature display, the heater LED and a (decorative) power knob.
            var consoleBase = new Vector3(0f, bodyTop, -0.075f);
            CreateConsole(module, palette, consoleBase, 0.28f);
            DisplayParts display = CreateConsoleDisplay(module, palette, consoleBase, -0.045f, new Vector2(0.17f, 0.07f), "20.0 °C");
            Renderer led = CreateConsoleLed(module, palette, consoleBase, 0.07f);
            Renderer strip = CreateAccentStrip(module, palette, new Vector3(0f, bodyTop + 0.003f, -0.112f), 0.25f);

            GameObject knob = Lathe("Knob", module, "HotPlateKnob", new[]
            {
                new Vector2(0f, 0f), new Vector2(0.02f, 0f), new Vector2(0.022f, 0.003f), new Vector2(0.0185f, 0.018f),
                new Vector2(0.016f, 0.021f), new Vector2(0f, 0.021f),
            }, OnConsoleSlope(consoleBase, 0.112f, 0.0005f), palette.DarkPlastic, 28);
            knob.transform.localRotation = Quaternion.Euler(-ConsoleSlopeDegrees, 0f, 0f);
            Box("KnobMark", knob.transform, new Vector3(0f, 0.0215f, 0.009f), new Vector3(0.003f, 0.001f, 0.012f), palette.Accent, false);

            InstrumentButton on = CreateButton("OnButton", "ON", module, new Vector3(-0.09f, bodyTop, -0.16f), palette.ButtonGreen, palette);
            InstrumentButton off = CreateButton("OffButton", "OFF", module, new Vector3(0f, bodyTop, -0.16f), palette.ButtonRed, palette);
            InstrumentButton reset = CreateButton("ResetButton", "RESET", module, new Vector3(0.09f, bodyTop, -0.16f), palette.ButtonGray, palette);
            CreateNameplate(module, "HEATING   20 - 300 °C", new Vector3(0.02f, 0.04f, frontFace - 0.0012f), 0.2f, palette);
            TMPro.TextMeshPro hint = CreateHintSign(module, palette, new Vector3(0f, 0f, frontFace - 0.045f), 0.28f, "Press ON to start heating");

            SetReference(hotPlate, "valueDisplay", display.Value);
            SetReference(hotPlate, "stateDisplay", display.State);
            SetReference(hotPlate, "statusIndicator", led);
            SetReference(hotPlate, "heatingElementRenderer", element.GetComponent<Renderer>());
            SetReference(hotPlate, "heatingLight", heatLight);

            Connect(on, hotPlate.SwitchOn);
            Connect(off, hotPlate.SwitchOff);
            Connect(reset, hotPlate.ResetTemperature);

            var surface = module.gameObject.AddComponent<HeatingSurface>();
            SetReference(surface, "hotPlate", hotPlate);
            SetReference(surface, "zone", zone);

            AddStateVisuals(module.gameObject, hotPlate, hint, new[] { display.Backlight, strip }, new[] { "OFF" },
                "COOLING", "Cooling down. The surface may still be hot",
                "OFF", "Press ON to start heating",
                "HEATING", "Heating at 10 °C per second. OFF stops, RESET returns to 20 °C",
                "MAX", "Maximum temperature reached. Press OFF to cool down");

            context.HotPlateAnchor = CreateChild("SampleAnchor", module, new Vector3(0f, bodyTop + plateThickness + 0.008f, plateZ)).transform;
            return hotPlate;
        }

        // ------------------------------------------------------------------
        // Timer module (right): swivel display head, START / PAUSE / RESET
        // ------------------------------------------------------------------

        private static StopwatchTimer BuildTimerModule(BuildContext context, Palette palette, Transform station, float x, float bodyTop, float frontFace)
        {
            Transform module = CreateChild("Stopwatch", station, new Vector3(x, 0f, 0f)).transform;
            var stopwatch = module.gameObject.AddComponent<StopwatchTimer>();
            SetString(stopwatch, "instrumentName", "Stopwatch");

            const float deckHeight = 0.012f;
            float deckTop = bodyTop + deckHeight;

            // Raised deck that carries the swivel: turntable + neck + display head.
            // RotatableMount turns the head in 15 degree steps so it can be read from the side.
            Bevel("TimerDeck", module, new Vector3(0f, bodyTop, 0.095f), new Vector3(0.24f, deckHeight, 0.16f), 0.02f, 0.003f, palette.BodyDark, true);
            Transform swivel = CreateChild("Swivel", module, new Vector3(0f, deckTop, 0.1f)).transform;
            Lathe("Turntable", swivel, "TimerTurntable", CylinderProfile(0.052f, 0.007f, 0.002f), Vector3.zero, palette.Steel, 32);
            Lathe("Neck", swivel, "TimerNeck", CylinderProfile(0.014f, 0.03f, 0.002f), new Vector3(0f, 0.006f, 0f), palette.DarkPlastic, 16);

            Transform head = CreateChild("Head", swivel, new Vector3(0f, 0.03f, 0f)).transform;
            head.localRotation = Quaternion.Euler(22f, 0f, 0f);
            GameObject housing = Bevel("Housing", head, Vector3.zero, new Vector3(0.23f, 0.105f, 0.04f), 0.012f, 0.004f, palette.BodyDark, true);
            housing.name = "Housing";
            GameObject trim = Bevel("Trim", head, new Vector3(0f, 0.105f, 0f), new Vector3(0.23f, 0.004f, 0.04f), 0.012f, 0.0015f, palette.Steel);
            trim.name = "Trim";

            DisplayParts display = CreateDisplay(head, new Vector3(-0.012f, 0.054f, -0.0222f), 0f, new Vector2(0.185f, 0.075f), palette, "00:00.00");
            GameObject ledObject = CreatePrimitive(PrimitiveType.Sphere, "StatusLed", head, new Vector3(0.099f, 0.085f, -0.021f), Vector3.one * 0.012f, palette.Led, false);
            Renderer led = ledObject.GetComponent<Renderer>();
            Renderer strip = CreateAccentStrip(head, palette, new Vector3(0f, 0.008f, -0.0205f), 0.2f);

            var mount = module.gameObject.AddComponent<RotatableMount>();
            SetReference(mount, "pivot", swivel);
            context.StopwatchMount = mount;

            InstrumentButton start = CreateButton("StartButton", "START", module, new Vector3(-0.09f, bodyTop, -0.16f), palette.ButtonGreen, palette);
            InstrumentButton pause = CreateButton("PauseButton", "PAUSE", module, new Vector3(0f, bodyTop, -0.16f), palette.ButtonBlue, palette);
            InstrumentButton reset = CreateButton("ResetButton", "RESET", module, new Vector3(0.09f, bodyTop, -0.16f), palette.ButtonGray, palette);
            InstrumentButton turnLeft = CreateButton("TurnLeftButton", "<", module, new Vector3(-0.09f, bodyTop, -0.05f), palette.ButtonGray, palette, 0.032f);
            InstrumentButton turnRight = CreateButton("TurnRightButton", ">", module, new Vector3(0.09f, bodyTop, -0.05f), palette.ButtonGray, palette, 0.032f);
            CreateNameplate(module, "TIMER   0.01 s", new Vector3(0f, 0.04f, frontFace - 0.0012f), 0.14f, palette);
            TMPro.TextMeshPro hint = CreateHintSign(module, palette, new Vector3(0f, 0f, frontFace - 0.045f), 0.28f, "Press START to begin timing");

            SetReference(stopwatch, "valueDisplay", display.Value);
            SetReference(stopwatch, "stateDisplay", display.State);
            SetReference(stopwatch, "statusIndicator", led);

            Connect(start, stopwatch.StartTimer);
            Connect(pause, stopwatch.PauseTimer);
            Connect(reset, stopwatch.ResetTimer);
            // The arrows are seen from the user's side: "<" turns the display toward the user's left.
            Connect(turnLeft, mount.RotateRight);
            Connect(turnRight, mount.RotateLeft);

            AddStateVisuals(module.gameObject, stopwatch, hint, new[] { display.Backlight, strip }, new[] { "IDLE" },
                "IDLE", "Press START to begin timing",
                "RUNNING", "Timing. PAUSE freezes the time, RESET clears it",
                "PAUSED", "Paused. START resumes, RESET clears the time");

            return stopwatch;
        }
    }
}
