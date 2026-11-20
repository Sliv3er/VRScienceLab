using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace VRScienceLab.Module2.EditorTools
{
    // World-space interfaces: three monitors around the main workbench.
    //   centre : welcome screen, then the measurement summary and the reset controls
    //   left   : sample management (create / select / remove / move / scale)
    //   right  : laboratory assistant robot and "find in lab"
    // One canvas unit is one millimetre on the 1000-px-per-metre monitors, so font sizes read as
    // millimetres: body text is about 28 mm tall, comfortable from two metres in a headset.
    public static partial class Module2SceneBuilder
    {
        private static readonly Color UiBackground = new Color(0.035f, 0.055f, 0.075f, 1f);
        private static readonly Color UiCard = new Color(0.075f, 0.115f, 0.15f, 1f);
        private static readonly Color UiAccent = new Color(0.20f, 0.82f, 0.95f, 1f);
        private static readonly Color UiTextColor = new Color(0.92f, 0.96f, 0.98f, 1f);
        private static readonly Color UiMuted = new Color(0.55f, 0.68f, 0.76f, 1f);
        private static readonly Color UiButtonColor = new Color(0.13f, 0.22f, 0.29f, 1f);
        private static readonly Color UiButtonPrimary = new Color(0.10f, 0.58f, 0.72f, 1f);
        private static readonly Color UiButtonDanger = new Color(0.62f, 0.20f, 0.18f, 1f);
        private static readonly Color UiButtonGo = new Color(0.14f, 0.50f, 0.30f, 1f);

        private static Sprite uiSprite;
        private static Camera uiEventCamera;

        private static void BuildInterface(BuildContext context)
        {
            uiSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

            // Event camera of the world-space canvases: the headset camera of the XR rig.
            uiEventCamera = null;
            if (context.XRRig != null)
            {
                foreach (Camera camera in context.XRRig.GetComponentsInChildren<Camera>(true))
                {
                    if (camera.CompareTag("MainCamera") || uiEventCamera == null) uiEventCamera = camera;
                }
            }

            if (uiEventCamera == null)
            {
                Debug.LogWarning("[Module2SceneBuilder] No camera found for the interface canvases; they fall back to Camera.main at runtime.");
            }

            Transform root = new GameObject("Interface").transform;

            RectTransform center = CreateMonitor(context.Palette, root, "Main Display", new Vector3(0f, 1.9f, 1.12f), 0f, -6f, new Vector2(1500f, 840f), 0.9f, false);
            BuildMainDisplay(context, center);

            RectTransform left = CreateMonitor(context.Palette, root, "Sample Console", new Vector3(-2.35f, 1.5f, 0.3f), -66f, 0f, new Vector2(1000f, 750f), 1f, true);
            BuildSampleConsole(context, left);

            RectTransform right = CreateMonitor(context.Palette, root, "Robot Console", new Vector3(2.35f, 1.5f, 0.3f), 66f, 0f, new Vector2(1000f, 750f), 1f, true);
            BuildRobotConsole(context, right);
        }

        // ------------------------------------------------------------------
        // Monitor (3D housing + world-space canvas)
        // ------------------------------------------------------------------

        private static RectTransform CreateMonitor(Palette palette, Transform parent, string name, Vector3 screenCenter, float yaw, float pitch, Vector2 pixels, float metresPerThousandPixels, bool floorStand)
        {
            GameObject root = CreateRoot(name, parent, screenCenter);
            root.transform.rotation = Quaternion.Euler(pitch, yaw, 0f);

            float scale = 0.001f * metresPerThousandPixels;
            float width = pixels.x * scale;
            float height = pixels.y * scale;

            // Housing behind the screen (thickness along +Z, away from the viewer).
            GameObject housing = MeshObject("Housing", root.transform, ProceduralMeshFactory.BevelBox(width + 0.05f, 0.035f, height + 0.05f, 0.02f, 0.006f), new Vector3(0f, 0f, 0.004f), palette.DarkPlastic);
            housing.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            NoShadows(Box("Screen Backing", root.transform, new Vector3(0f, 0f, 0.003f), new Vector3(width + 0.004f, height + 0.004f, 0.002f), palette.Screen, false));
            NoShadows(Box("Status Light", root.transform, new Vector3(0f, -height * 0.5f - 0.014f, 0.001f), new Vector3(0.08f, 0.006f, 0.004f), palette.Accent, false));

            if (floorStand)
            {
                // Pole and weighted base, built upright in world space.
                Transform stand = CreateChild("Stand", parent, Vector3.zero).transform;
                Vector3 back = root.transform.forward;
                var foot = new Vector3(screenCenter.x + back.x * 0.07f, 0f, screenCenter.z + back.z * 0.07f);
                stand.position = foot;
                Lathe("Base", stand, "MonitorFloorBase", new[]
                {
                    new Vector2(0f, 0f), new Vector2(0.24f, 0f), new Vector2(0.25f, 0.01f), new Vector2(0.23f, 0.025f), new Vector2(0.04f, 0.04f), new Vector2(0f, 0.04f),
                }, Vector3.zero, palette.DarkPlastic, 40);
                Lathe("Pole", stand, "MonitorPole_" + Mathf.RoundToInt(screenCenter.y * 100f), CylinderProfile(0.024f, screenCenter.y - 0.03f, 0.003f), new Vector3(0f, 0.03f, 0f), palette.BrushedSteel, 20);
                GameObject bracket = Box("Bracket", stand, new Vector3(0f, screenCenter.y, 0f), new Vector3(0.16f, 0.16f, 0.07f), palette.DarkPlastic, false);
                bracket.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
                var collider = stand.gameObject.AddComponent<CapsuleCollider>();
                collider.center = new Vector3(0f, 0.6f, 0f);
                collider.radius = 0.06f;
                collider.height = 1.2f;

                // Added after the NavMesh bake, so it carves its own hole for the robot.
                var obstacle = stand.gameObject.AddComponent<UnityEngine.AI.NavMeshObstacle>();
                obstacle.shape = UnityEngine.AI.NavMeshObstacleShape.Capsule;
                obstacle.center = new Vector3(0f, 0.5f, 0f);
                obstacle.radius = 0.27f;
                obstacle.height = 1f;
                obstacle.carving = true;
            }
            else
            {
                // Wall arm.
                Box("Wall Arm", root.transform, new Vector3(0f, 0f, 0.085f), new Vector3(0.12f, 0.12f, 0.1f), palette.DarkPlastic, false);
                Box("Wall Plate", root.transform, new Vector3(0f, 0f, 0.128f), new Vector3(0.3f, 0.3f, 0.012f), palette.BrushedSteel, false);
            }

            var canvasObject = new GameObject("Canvas", typeof(RectTransform));
            canvasObject.transform.SetParent(root.transform, false);
            var rect = (RectTransform)canvasObject.transform;
            rect.sizeDelta = pixels;
            rect.localScale = Vector3.one * scale;
            rect.localPosition = Vector3.zero;

            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.additionalShaderChannels = AdditionalCanvasShaderChannels.TexCoord1 | AdditionalCanvasShaderChannels.Normal | AdditionalCanvasShaderChannels.Tangent;
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.dynamicPixelsPerUnit = 2f;

            // Two raycasters, exactly like XRI's own "GameObject > XR > UI Canvas":
            // - GraphicRaycaster answers ordinary pointer events. The XR Interaction Simulator uses
            //   it to recognise a mouse click on a screen as a UI press; without it a click on a
            //   button is sent as a grip press and the button never fires. It also makes a plain
            //   mouse click work when no simulator is running.
            // - TrackedDeviceGraphicRaycaster answers the controller rays.
            // Both need an event camera on a world-space canvas.
            canvasObject.AddComponent<GraphicRaycaster>();
            canvasObject.AddComponent<TrackedDeviceGraphicRaycaster>();
            canvas.worldCamera = uiEventCamera;

            Image background = canvasObject.AddComponent<Image>();
            background.color = UiBackground;
            background.raycastTarget = true;

            UiImage("Top Accent", rect, 0f, 0f, pixels.x, 8f, UiAccent, false);
            return rect;
        }

        // ------------------------------------------------------------------
        // UI building blocks (coordinates in pixels from the top-left corner)
        // ------------------------------------------------------------------

        private static RectTransform UiRect(string name, Transform parent, float x, float y, float width, float height)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
            return rect;
        }

        private static Image UiImage(string name, Transform parent, float x, float y, float width, float height, Color color, bool rounded = true)
        {
            RectTransform rect = UiRect(name, parent, x, y, width, height);
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            if (rounded && uiSprite != null)
            {
                image.sprite = uiSprite;
                image.type = Image.Type.Sliced;
                image.pixelsPerUnitMultiplier = 0.5f;
            }

            return image;
        }

        private static TextMeshProUGUI UiText(string name, Transform parent, float x, float y, float width, float height, string text, float size, Color color,
            FontStyles style = FontStyles.Normal, TextAlignmentOptions alignment = TextAlignmentOptions.TopLeft)
        {
            RectTransform rect = UiRect(name, parent, x, y, width, height);
            var tmp = rect.gameObject.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.color = color;
            tmp.fontStyle = style;
            tmp.alignment = alignment;
            tmp.textWrappingMode = TextWrappingModes.Normal;
            tmp.overflowMode = TextOverflowModes.Truncate;
            tmp.raycastTarget = false;
            tmp.richText = true;
            return tmp;
        }

        private static TextMeshProUGUI UiSectionLabel(Transform parent, float x, float y, string text)
        {
            TextMeshProUGUI label = UiText("Section " + text, parent, x, y, 900f, 28f, text, 21f, UiMuted, FontStyles.Bold);
            label.characterSpacing = 6f;
            return label;
        }

        private static Button UiButton(string name, Transform parent, float x, float y, float width, float height, string label, Color color, float fontSize = 27f)
        {
            Image image = UiImage(name, parent, x, y, width, height, color);
            image.raycastTarget = true;

            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.35f, 1.35f, 1.35f, 1f);
            colors.pressedColor = new Color(0.7f, 1.6f, 1.8f, 1f);
            colors.selectedColor = Color.white;
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.08f;
            button.colors = colors;
            var navigation = button.navigation;
            navigation.mode = Navigation.Mode.None;
            button.navigation = navigation;

            TextMeshProUGUI text = UiText("Label", image.transform, 0f, 0f, width, height, label, fontSize, UiTextColor, FontStyles.Bold, TextAlignmentOptions.Center);
            var textRect = (RectTransform)text.transform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.pivot = new Vector2(0.5f, 0.5f);
            textRect.offsetMin = new Vector2(8f, 4f);
            textRect.offsetMax = new Vector2(-8f, -4f);
            text.enableAutoSizing = true;
            text.fontSizeMax = fontSize;
            text.fontSizeMin = fontSize * 0.6f;
            return button;
        }

        /// <summary>Card with a small caption and a large value; returns the value text.</summary>
        private static TextMeshProUGUI UiTile(Transform parent, float x, float y, float width, float height, string caption, string value, float valueSize = 50f)
        {
            Image card = UiImage("Tile " + caption, parent, x, y, width, height, UiCard);
            UiText("Caption", card.transform, 20f, 14f, width - 40f, 26f, caption, 20f, UiMuted, FontStyles.Bold).characterSpacing = 6f;
            TextMeshProUGUI valueText = UiText("Value", card.transform, 20f, 42f, width - 40f, height - 50f, value, valueSize, UiAccent, FontStyles.Bold, TextAlignmentOptions.Left);
            valueText.enableAutoSizing = true;
            valueText.fontSizeMax = valueSize;
            valueText.fontSizeMin = valueSize * 0.45f;
            valueText.textWrappingMode = TextWrappingModes.NoWrap;
            return valueText;
        }

        private static void OnClick(Button button, UnityAction action)
        {
            UnityEventTools.AddPersistentListener(button.onClick, action);
        }

        private static void OnClick(Button button, UnityAction<int> action, int argument)
        {
            UnityEventTools.AddIntPersistentListener(button.onClick, action, argument);
        }

        // ------------------------------------------------------------------
        // Centre monitor: welcome screen and measurement summary
        // ------------------------------------------------------------------

        private static void BuildMainDisplay(BuildContext context, RectTransform canvas)
        {
            var welcomePanel = canvas.gameObject.AddComponent<WelcomePanel>();
            context.WelcomePanel = welcomePanel;

            // ---- Welcome view ----
            RectTransform welcome = UiRect("Welcome View", canvas, 0f, 0f, 1500f, 840f);
            UiText("Kicker", welcome, 80f, 62f, 900f, 40f, "VR SCIENCE LAB", 30f, UiAccent, FontStyles.Bold).characterSpacing = 14f;
            UiText("Title", welcome, 80f, 108f, 1340f, 100f, "Scientific Equipment and Its Behavior", 76f, UiTextColor, FontStyles.Bold);
            UiText("Subtitle", welcome, 80f, 218f, 1340f, 44f, "Module 2   |   Measurement training session", 32f, UiMuted);

            string[] names = { "Digital scale", "Heating station", "Thermometer", "Storage cabinet" };
            string[] details = { "Mass of a sample, with tare", "Hot plate up to 300 °C with a built-in timer", "Sample temperature by probe", "Glassware to take out and put back" };
            for (int i = 0; i < 4; i++)
            {
                Image card = UiImage("Instrument Card " + (i + 1), welcome, 80f + i * 340f, 300f, 320f, 190f, UiCard);
                UiText("Number", card.transform, 24f, 20f, 100f, 30f, "0" + (i + 1), 24f, UiAccent, FontStyles.Bold);
                UiText("Name", card.transform, 24f, 58f, 280f, 48f, names[i], 37f, UiTextColor, FontStyles.Bold);
                UiText("Detail", card.transform, 24f, 112f, 280f, 70f, details[i], 24f, UiMuted);
            }

            UiText("How A", welcome, 80f, 528f, 1340f, 38f, "Ray + Grip presses the instrument buttons, opens the cabinet doors and grabs objects.  Ray + Trigger operates the screens.", 27f, UiTextColor);
            UiText("How B", welcome, 80f, 572f, 1340f, 38f, "The side screens create samples and send the laboratory assistant robot to a station.", 27f, UiMuted);

            Button begin = UiButton("Begin Button", welcome, 80f, 660f, 440f, 110f, "BEGIN SESSION", UiButtonPrimary, 40f);
            OnClick(begin, welcomePanel.BeginSession);
            UiText("Keyboard Note", welcome, 560f, 690f, 860f, 60f, "Without a headset: Enter begins, F1 - F10 operate the instruments, Home resets the laboratory.", 22f, UiMuted);

            // ---- Session view ----
            RectTransform session = UiRect("Session View", canvas, 0f, 0f, 1500f, 840f);
            UiText("Header", session, 60f, 36f, 900f, 56f, "MEASUREMENT SUMMARY", 44f, UiTextColor, FontStyles.Bold);
            Button home = UiButton("Home Button", session, 1280f, 34f, 160f, 62f, "HOME", UiButtonColor, 26f);
            OnClick(home, welcomePanel.ShowWelcome);

            TextMeshProUGUI scaleValue = UiTile(session, 60f, 116f, 330f, 132f, "DIGITAL SCALE", "OFF");
            TextMeshProUGUI plateValue = UiTile(session, 410f, 116f, 330f, 132f, "HEATING STATION", "20.0 °C");
            TextMeshProUGUI timeValue = UiTile(session, 760f, 116f, 330f, 132f, "STATION TIMER", "00:00.00");
            TextMeshProUGUI thermoValue = UiTile(session, 1110f, 116f, 330f, 132f, "THERMOMETER", "OFF");

            UiText("Table Header", session, 60f, 276f, 1380f, 32f,
                "#<pos=8%>SAMPLE<pos=40%>MASS<pos=58%>SAMPLE TEMP<pos=74%>PLATE TEMP<pos=89%>TIME", 22f, UiMuted, FontStyles.Bold);
            UiImage("Table Rule", session, 60f, 312f, 1380f, 2f, UiCard, false);
            TextMeshProUGUI table = UiText("Table", session, 60f, 324f, 1380f, 300f, "", 28f, UiTextColor);
            table.lineSpacing = 12f;
            table.textWrappingMode = TextWrappingModes.NoWrap;

            TextMeshProUGUI statistics = UiText("Statistics", session, 60f, 636f, 1380f, 36f, "Entries 0 / 8", 26f, UiAccent, FontStyles.Bold);
            TextMeshProUGUI collisions = UiText("Collisions", session, 60f, 676f, 1380f, 34f, "Collisions 0     Drop or place an object to see the last impact", 23f, UiMuted);

            Button record = UiButton("Record Button", session, 60f, 730f, 340f, 80f, "RECORD MEASUREMENT", UiButtonPrimary);
            Button clear = UiButton("Clear Button", session, 420f, 730f, 240f, 80f, "CLEAR LOG", UiButtonColor);
            Button resetInstruments = UiButton("Reset Instruments Button", session, 680f, 730f, 340f, 80f, "RESET INSTRUMENTS", UiButtonColor);
            Button resetLab = UiButton("Reset Lab Button", session, 1040f, 730f, 400f, 80f, "RESET LABORATORY", UiButtonDanger);
            OnClick(record, context.MeasurementLog.RecordMeasurement);
            OnClick(clear, context.MeasurementLog.Clear);
            OnClick(resetInstruments, context.ResetController.ResetInstruments);
            OnClick(resetLab, context.ResetController.ResetLab);

            var summary = session.gameObject.AddComponent<MeasurementSummaryPanel>();
            SetReference(summary, "log", context.MeasurementLog);
            SetReference(summary, "digitalScale", context.Scale);
            SetReference(summary, "hotPlate", context.HotPlate);
            SetReference(summary, "stopwatch", context.Stopwatch);
            SetReference(summary, "thermometer", context.Thermometer);
            SetReference(summary, "scaleText", scaleValue);
            SetReference(summary, "hotPlateText", plateValue);
            SetReference(summary, "stopwatchText", timeValue);
            SetReference(summary, "thermometerText", thermoValue);
            SetReference(summary, "tableText", table);
            SetReference(summary, "statisticsText", statistics);
            SetReference(summary, "collisionText", collisions);

            SetReference(welcomePanel, "welcomeView", welcome.gameObject);
            SetReference(welcomePanel, "sessionView", session.gameObject);
            session.gameObject.SetActive(false);
        }

        // ------------------------------------------------------------------
        // Left monitor: sample management
        // ------------------------------------------------------------------

        private static void BuildSampleConsole(BuildContext context, RectTransform canvas)
        {
            SampleManager manager = context.SampleManager;

            UiText("Header", canvas, 40f, 30f, 640f, 52f, "SAMPLE MANAGEMENT", 40f, UiTextColor, FontStyles.Bold);
            UiText("Count Caption", canvas, 700f, 30f, 140f, 52f, "IN LAB", 20f, UiMuted, FontStyles.Bold, TextAlignmentOptions.Right);
            TextMeshProUGUI count = UiText("Count", canvas, 850f, 24f, 110f, 56f, "0 / 8", 40f, UiAccent, FontStyles.Bold, TextAlignmentOptions.Right);

            UiSectionLabel(canvas, 40f, 100f, "CREATE A SAMPLE");
            string[] createLabels = { "BEAKER", "TEST TUBE", "FLASK", "SAMPLE BOX" };
            int[] createIndices = { 1, 3, 4, 5 };
            for (int i = 0; i < createLabels.Length; i++)
            {
                Button create = UiButton("Create " + createLabels[i], canvas, 40f + i * 235f, 130f, 215f, 80f, createLabels[i], UiButtonPrimary);
                OnClick(create, manager.CreateSampleFromButton, createIndices[i]);
            }

            UiSectionLabel(canvas, 40f, 230f, "SELECT AND REMOVE");
            Button selectNext = UiButton("Select Next", canvas, 40f, 260f, 290f, 80f, "SELECT NEXT", UiButtonColor);
            Button remove = UiButton("Remove Selected", canvas, 350f, 260f, 310f, 80f, "REMOVE SELECTED", UiButtonDanger);
            Button resetSamples = UiButton("Reset Samples", canvas, 680f, 260f, 280f, 80f, "RESET SAMPLES", UiButtonColor);
            OnClick(selectNext, manager.SelectNext);
            OnClick(remove, manager.RemoveSelected);
            OnClick(resetSamples, manager.ResetSamples);

            UiImage("Selected Card", canvas, 40f, 358f, 920f, 132f, UiCard);
            TextMeshProUGUI selected = UiText("Selected", canvas, 64f, 370f, 880f, 112f, "No sample selected", 27f, UiTextColor);
            selected.lineSpacing = 8f;

            UiSectionLabel(canvas, 40f, 506f, "MOVE THE SELECTED SAMPLE TO");
            string[] moveLabels = { "SCALE PAN", "HEATING PLATE", "THERMOMETER" };
            for (int i = 0; i < moveLabels.Length; i++)
            {
                Button move = UiButton("Move To " + moveLabels[i], canvas, 40f + i * 235f, 536f, 215f, 74f, moveLabels[i], UiButtonColor, 24f);
                OnClick(move, manager.MoveSelectedToStation, i);
            }

            Button tray = UiButton("Return To Tray", canvas, 745f, 536f, 215f, 74f, "TRAY", UiButtonColor, 24f);
            OnClick(tray, manager.ReturnSelectedToTray);

            UiSectionLabel(canvas, 40f, 626f, "SIZE OF THE SELECTED SAMPLE");
            Button smaller = UiButton("Scale Down", canvas, 40f, 654f, 215f, 70f, "SMALLER  -", UiButtonColor, 24f);
            Button larger = UiButton("Scale Up", canvas, 275f, 654f, 215f, 70f, "LARGER  +", UiButtonColor, 24f);
            OnClick(smaller, manager.ScaleSelectedDown);
            OnClick(larger, manager.ScaleSelectedUp);

            TextMeshProUGUI message = UiText("Message", canvas, 515f, 654f, 445f, 70f, "", 23f, UiAccent, FontStyles.Normal, TextAlignmentOptions.Left);

            var panel = canvas.gameObject.AddComponent<SampleManagerPanel>();
            SetReference(panel, "manager", manager);
            SetReference(panel, "countText", count);
            SetReference(panel, "selectedText", selected);
            SetReference(panel, "messageText", message);
        }

        // ------------------------------------------------------------------
        // Right monitor: assistant robot and scene search
        // ------------------------------------------------------------------

        private static void BuildRobotConsole(BuildContext context, RectTransform canvas)
        {
            LabAssistantRobot robot = context.Robot;
            LabDirectory directory = context.Directory;

            // Every button calls the RobotControlPanel, which shows the command it received and the
            // robot's answer, then forwards it to the robot.
            var robotPanel = canvas.gameObject.AddComponent<RobotControlPanel>();
            SetReference(robotPanel, "robot", robot);

            UiText("Header", canvas, 40f, 26f, 700f, 52f, "LABORATORY ASSISTANT", 40f, UiTextColor, FontStyles.Bold);
            TextMeshProUGUI navigation = UiText("Navigation", canvas, 700f, 36f, 260f, 34f, "NAVMESH OK", 21f, UiMuted, FontStyles.Bold, TextAlignmentOptions.Right);

            TextMeshProUGUI state = UiTile(canvas, 40f, 84f, 300f, 104f, "STATE", "IDLE", 38f);
            TextMeshProUGUI destination = UiTile(canvas, 355f, 84f, 385f, 104f, "DESTINATION", "none", 38f);
            TextMeshProUGUI distance = UiTile(canvas, 755f, 84f, 205f, 104f, "DISTANCE", "0.0 m", 38f);

            // ---- Main destinations (two rows of two large buttons).
            UiSectionLabel(canvas, 40f, 198f, "SEND THE ROBOT TO");
            int stationCount = new SerializedObject(robot).FindProperty("stations").arraySize;
            var stationGraphics = new Object[stationCount];
            int[] mainStations = { RobotStationScale, RobotStationHeating, RobotStationCabinet, RobotStationWorkbench };
            for (int i = 0; i < mainStations.Length; i++)
            {
                int station = mainStations[i];
                string label = "GO TO " + RobotStationSpecs[station].Name.ToUpperInvariant();
                float x = 40f + (i % 2) * 465f;
                float y = 226f + (i / 2) * 78f;
                Button go = UiButton(label, canvas, x, y, 455f, 68f, label, UiButtonPrimary, 25f);
                OnClick(go, robotPanel.GoToStation, station);
                stationGraphics[station] = go.targetGraphic;
            }

            // ---- Stop and return.
            Button stop = UiButton("STOP ROBOT", canvas, 40f, 382f, 300f, 72f, "STOP ROBOT", UiButtonDanger, 27f);
            Button home = UiButton("RETURN TO STARTING POSITION", canvas, 350f, 382f, 610f, 72f, "RETURN TO STARTING POSITION", UiButtonGo, 25f);
            OnClick(stop, robotPanel.StopRobot);
            OnClick(home, robotPanel.ReturnToStart);
            stationGraphics[RobotStationStart] = home.targetGraphic;

            // ---- Secondary controls: resume, the two extra stations, automatic tour.
            Button resume = UiButton("RESUME", canvas, 40f, 464f, 222f, 54f, "RESUME", UiButtonColor, 21f);
            OnClick(resume, robotPanel.ResumeRobot);
            float extraX = 272f;
            for (int station = RobotStationWorkbench + 1; station < stationCount && station < RobotStationSpecs.Length; station++)
            {
                string label = RobotStationSpecs[station].Name.ToUpperInvariant();
                Button extra = UiButton("GO TO " + label, canvas, extraX, 464f, 222f, 54f, label, UiButtonColor, 21f);
                OnClick(extra, robotPanel.GoToStation, station);
                stationGraphics[station] = extra.targetGraphic;
                extraX += 232f;
            }

            Button tour = UiButton("AUTO TOUR", canvas, 736f, 464f, 224f, 54f, "AUTO TOUR", UiButtonColor, 21f);
            OnClick(tour, robotPanel.ToggleTour);

            // ---- Last command and the robot's answer.
            UiImage("Command Card", canvas, 40f, 528f, 920f, 46f, UiCard);
            TextMeshProUGUI command = UiText("Command", canvas, 58f, 534f, 884f, 34f, "Press a button to send the robot to a station.", 21f, UiAccent, FontStyles.Bold, TextAlignmentOptions.Left);
            command.textWrappingMode = TextWrappingModes.NoWrap;
            command.enableAutoSizing = true;
            command.fontSizeMax = 21f;
            command.fontSizeMin = 13f;

            SetReference(robotPanel, "stateText", state);
            SetReference(robotPanel, "destinationText", destination);
            SetReference(robotPanel, "distanceText", distance);
            SetReference(robotPanel, "commandText", command);
            SetReference(robotPanel, "navigationText", navigation);
            SetReferences(robotPanel, "stationButtonGraphics", stationGraphics);

            // ---- Find in the laboratory (scene search).
            UiSectionLabel(canvas, 40f, 586f, "FIND IN THE LABORATORY");
            string[] findLabels = { "SCALE", "HEATER", "TIMER", "THERMOMETER" };
            for (int i = 0; i < findLabels.Length; i++)
            {
                Button find = UiButton("Find " + findLabels[i], canvas, 40f + i * 187f, 612f, 172f, 50f, findLabels[i], UiButtonColor, 20f);
                OnClick(find, directory.LocateInstrument, i);
            }

            Button heaviest = UiButton("Find Heaviest", canvas, 788f, 612f, 172f, 50f, "HEAVIEST", UiButtonColor, 20f);
            OnClick(heaviest, directory.LocateHeaviestSample);

            UiImage("Report Card", canvas, 40f, 670f, 920f, 68f, UiCard);
            TextMeshProUGUI report = UiText("Report", canvas, 58f, 676f, 884f, 58f, "Press a button to search the scene for an instrument or a sample.", 20f, UiTextColor);
            var reportDisplay = report.gameObject.AddComponent<StatusTextDisplay>();
            SetReference(reportDisplay, "target", report);
            UnityEventTools.AddPersistentListener(directory.onSearchCompleted, reportDisplay.Show);
        }
    }
}
