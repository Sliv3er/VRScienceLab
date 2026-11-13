using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace VRScienceLab.Module2.EditorTools
{
    // Material palette: one consistent set of surfaces for the whole laboratory
    // (neutral whites and greys, dark epoxy, steel, glass, restrained cyan accents).
    public static partial class Module2SceneBuilder
    {
        /// <summary>Materials created by the builder.</summary>
        private class Palette
        {
            // Architecture
            public Material Floor, Wall, WallPanel, Ceiling, CeilingLight, Skirting, WindowFrame, Sky, Door;
            // Furniture
            public Material BenchTop, Cabinet, CabinetGrey, Steel, BrushedSteel, DarkPlastic, Rubber, Glass, GlassTinted, Ceramic, Fabric;
            // Accents and signals
            public Material Accent, AccentDim, Marker, Beacon, Warning, Red, Green;
            // Instruments
            public Material Body, BodyDark, DisplayGlass, Led, Pan, HeatingElement, Probe, Dock, Indicator;
            public Material ButtonRed, ButtonGreen, ButtonBlue, ButtonGray;
            // Samples and props
            public Material Brass, LiquidBlue, LiquidClear, LiquidRed, LiquidGreen, LiquidAmber, SampleBox, SampleLid, Cable;
            // Robot
            public Material RobotShell, RobotDark, RobotVisor, RobotStatus;
            // Scientist
            public Material Coat, Scrubs, Skin, Hair, Glove, Shoe, GoggleFrame, GoggleLens;
            // UI
            public Material Screen;
        }

        private static bool UsesScriptableRenderPipeline => GraphicsSettings.currentRenderPipeline != null;

        private static Shader LitShader
        {
            get
            {
                Shader shader = UsesScriptableRenderPipeline ? Shader.Find("Universal Render Pipeline/Lit") : null;
                return shader != null ? shader : Shader.Find("Standard");
            }
        }

        private static Shader UnlitShader
        {
            get
            {
                Shader shader = UsesScriptableRenderPipeline ? Shader.Find("Universal Render Pipeline/Unlit") : null;
                return shader != null ? shader : Shader.Find("Unlit/Color");
            }
        }

        private static Palette CreateMaterials()
        {
            Texture2D floorTiles = ProceduralTextureFactory.FloorTiles();
            Texture2D ceilingTiles = ProceduralTextureFactory.CeilingTiles();
            Texture2D brushed = ProceduralTextureFactory.BrushedMetal();

            var palette = new Palette
            {
                Floor = CreateMaterial("Lab_Floor", new Color(0.86f, 0.88f, 0.90f), 0.62f, 0f, texture: floorTiles, tiling: new Vector2(9f, 6.5f)),
                Wall = CreateMaterial("Lab_Wall", new Color(0.93f, 0.94f, 0.95f), 0.15f),
                WallPanel = CreateMaterial("Lab_WallPanel", new Color(0.80f, 0.83f, 0.86f), 0.35f),
                Ceiling = CreateMaterial("Lab_Ceiling", new Color(0.95f, 0.96f, 0.97f), 0.05f, 0f, texture: ceilingTiles, tiling: new Vector2(15f, 11f)),
                CeilingLight = CreateMaterial("Lab_CeilingLight", Color.white, 0.2f, 0f, emission: new Color(1.5f, 1.55f, 1.6f)),
                Skirting = CreateMaterial("Lab_Skirting", new Color(0.36f, 0.40f, 0.44f), 0.4f),
                WindowFrame = CreateMaterial("Lab_WindowFrame", new Color(0.22f, 0.24f, 0.27f), 0.55f, 0.6f),
                Sky = CreateMaterial("Lab_WindowSky", new Color(0.72f, 0.86f, 0.98f), 0f, 0f, emission: new Color(0.85f, 1.05f, 1.3f), texture: ProceduralTextureFactory.SkyGradient()),
                Door = CreateMaterial("Lab_Door", new Color(0.70f, 0.74f, 0.78f), 0.45f),

                BenchTop = CreateMaterial("Lab_BenchTop", new Color(0.13f, 0.15f, 0.17f), 0.72f),
                Cabinet = CreateMaterial("Lab_Cabinet", new Color(0.90f, 0.92f, 0.93f), 0.5f),
                CabinetGrey = CreateMaterial("Lab_CabinetGrey", new Color(0.55f, 0.60f, 0.65f), 0.5f),
                Steel = CreateMaterial("Lab_Steel", new Color(0.78f, 0.80f, 0.82f), 0.82f, 0.95f),
                BrushedSteel = CreateMaterial("Lab_BrushedSteel", new Color(0.80f, 0.82f, 0.84f), 0.6f, 0.9f, texture: brushed),
                DarkPlastic = CreateMaterial("Lab_DarkPlastic", new Color(0.08f, 0.09f, 0.10f), 0.55f),
                Rubber = CreateMaterial("Lab_Rubber", new Color(0.05f, 0.05f, 0.06f), 0.15f),
                Glass = CreateMaterial("Lab_Glass", new Color(0.82f, 0.93f, 0.97f, 0.16f), 0.96f, 0f, transparent: true),
                GlassTinted = CreateMaterial("Lab_GlassTinted", new Color(0.55f, 0.80f, 0.90f, 0.28f), 0.95f, 0f, transparent: true),
                Ceramic = CreateMaterial("Lab_Ceramic", new Color(0.94f, 0.94f, 0.92f), 0.85f),
                Fabric = CreateMaterial("Lab_Fabric", new Color(0.16f, 0.22f, 0.28f), 0.1f),

                Accent = CreateMaterial("Lab_Accent", new Color(0.10f, 0.75f, 0.90f), 0.6f, 0f, emission: new Color(0.10f, 1.1f, 1.45f)),
                AccentDim = CreateMaterial("Lab_AccentDim", new Color(0.10f, 0.45f, 0.55f), 0.6f, 0f, emission: new Color(0.03f, 0.30f, 0.40f)),
                Marker = CreateMaterial("Lab_Marker", new Color(0.10f, 0.16f, 0.20f), 0.5f),
                Beacon = CreateMaterial("Lab_Beacon", new Color(1f, 0.75f, 0.1f), 0.5f, 0f, emission: new Color(2.2f, 1.4f, 0.1f)),
                Warning = CreateMaterial("Lab_Warning", new Color(0.98f, 0.80f, 0.10f), 0.4f, 0f, texture: ProceduralTextureFactory.HotSurfaceSign()),
                Red = CreateMaterial("Lab_Red", new Color(0.80f, 0.12f, 0.10f), 0.6f),
                Green = CreateMaterial("Lab_Green", new Color(0.12f, 0.62f, 0.30f), 0.5f),

                Body = CreateMaterial("Instrument_Body", new Color(0.91f, 0.92f, 0.93f), 0.62f),
                BodyDark = CreateMaterial("Instrument_BodyDark", new Color(0.17f, 0.19f, 0.22f), 0.55f),
                DisplayGlass = CreateMaterial("Instrument_DisplayGlass", new Color(0.02f, 0.05f, 0.06f), 0.9f),
                Led = CreateMaterial("Instrument_Led", new Color(0.2f, 0.2f, 0.2f), 0f, 0f, unlit: true),
                Pan = CreateMaterial("Scale_Pan", new Color(0.80f, 0.81f, 0.83f), 0.66f, 0.95f, texture: brushed),
                HeatingElement = CreateMaterial("HotPlate_Element", new Color(0.10f, 0.10f, 0.11f), 0.55f, 0.1f, emission: Color.black),
                Probe = CreateMaterial("Thermometer_Probe", new Color(0.82f, 0.83f, 0.85f), 0.85f, 0.95f),
                Dock = CreateMaterial("Thermometer_Dock", new Color(0.24f, 0.26f, 0.29f), 0.5f),
                Indicator = CreateMaterial("Instrument_ZoneIndicator", new Color(0.10f, 0.16f, 0.20f), 0.6f),
                ButtonRed = CreateMaterial("Button_Red", new Color(0.80f, 0.16f, 0.14f), 0.6f),
                ButtonGreen = CreateMaterial("Button_Green", new Color(0.12f, 0.58f, 0.28f), 0.6f),
                ButtonBlue = CreateMaterial("Button_Blue", new Color(0.12f, 0.42f, 0.78f), 0.6f),
                ButtonGray = CreateMaterial("Button_Gray", new Color(0.36f, 0.39f, 0.43f), 0.6f),

                Brass = CreateMaterial("Prop_Brass", new Color(0.83f, 0.66f, 0.30f), 0.78f, 0.95f),
                LiquidBlue = CreateMaterial("Liquid_Blue", new Color(0.20f, 0.55f, 0.95f), 0.9f, 0f, emission: new Color(0.02f, 0.07f, 0.16f)),
                LiquidClear = CreateMaterial("Liquid_Clear", new Color(0.62f, 0.86f, 0.92f), 0.9f, 0f, emission: new Color(0.05f, 0.10f, 0.12f)),
                LiquidRed = CreateMaterial("Liquid_Red", new Color(0.92f, 0.30f, 0.22f), 0.9f, 0f, emission: new Color(0.18f, 0.03f, 0.02f)),
                LiquidGreen = CreateMaterial("Liquid_Green", new Color(0.35f, 0.85f, 0.45f), 0.9f, 0f, emission: new Color(0.03f, 0.14f, 0.05f)),
                LiquidAmber = CreateMaterial("Liquid_Amber", new Color(0.95f, 0.68f, 0.18f), 0.9f, 0f, emission: new Color(0.16f, 0.09f, 0.01f)),
                SampleBox = CreateMaterial("Sample_Box", new Color(0.93f, 0.94f, 0.95f), 0.5f),
                SampleLid = CreateMaterial("Sample_BoxLid", new Color(0.12f, 0.50f, 0.72f), 0.55f),
                Cable = CreateMaterial("Prop_Cable", new Color(0.07f, 0.07f, 0.08f), 0.35f),

                RobotShell = CreateMaterial("Robot_Shell", new Color(0.93f, 0.94f, 0.95f), 0.7f),
                RobotDark = CreateMaterial("Robot_Dark", new Color(0.14f, 0.16f, 0.19f), 0.6f, 0.3f),
                RobotVisor = CreateMaterial("Robot_Visor", new Color(0.02f, 0.03f, 0.04f), 0.95f),
                RobotStatus = CreateMaterial("Robot_Status", new Color(0.15f, 0.9f, 1f), 0.5f, 0f, emission: new Color(0.15f, 0.9f, 1.3f)),

                Coat = CreateMaterial("Scientist_Coat", new Color(0.95f, 0.96f, 0.97f), 0.25f),
                Scrubs = CreateMaterial("Scientist_Scrubs", new Color(0.33f, 0.62f, 0.80f), 0.2f),
                Skin = CreateMaterial("Scientist_Skin", new Color(0.87f, 0.67f, 0.55f), 0.3f),
                Hair = CreateMaterial("Scientist_Hair", new Color(0.16f, 0.11f, 0.08f), 0.35f),
                Glove = CreateMaterial("Scientist_Glove", new Color(0.35f, 0.55f, 0.88f), 0.55f),
                Shoe = CreateMaterial("Scientist_Shoe", new Color(0.93f, 0.94f, 0.95f), 0.4f),
                GoggleFrame = CreateMaterial("Scientist_GoggleFrame", new Color(0.10f, 0.11f, 0.12f), 0.6f),
                GoggleLens = CreateMaterial("Scientist_GoggleLens", new Color(0.75f, 0.92f, 1f, 0.30f), 0.96f, 0f, transparent: true),

                Screen = CreateMaterial("UI_ScreenBacking", new Color(0.02f, 0.035f, 0.05f), 0.85f),
            };
            return palette;
        }

        private static Material CreateMaterial(string name, Color color, float smoothness = 0.4f, float metallic = 0f, bool unlit = false,
            Color? emission = null, bool transparent = false, Texture2D texture = null, Vector2? tiling = null)
        {
            string path = $"{MaterialFolder}/{name}.mat";
            Shader shader = unlit ? UnlitShader : LitShader;
            if (shader == null)
            {
                Debug.LogError($"[Module2SceneBuilder] Could not find a shader for material '{name}'.");
                shader = Shader.Find("Standard");
            }

            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            else
            {
                material.shader = shader;
            }

            material.color = color;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);

            if (!unlit)
            {
                if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", smoothness);
                if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
                if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", metallic);

                // Emission stays enabled (black by default) so scripts can light a surface with a
                // MaterialPropertyBlock (state glow, hover highlight, zone indicator).
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", emission ?? Color.black);
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }

            if (texture != null)
            {
                material.mainTexture = texture;
                if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture);
                Vector2 scale = tiling ?? Vector2.one;
                material.mainTextureScale = scale;
                if (material.HasProperty("_BaseMap")) material.SetTextureScale("_BaseMap", scale);
            }

            if (transparent) MakeTransparent(material);

            EditorUtility.SetDirty(material);
            return material;
        }

        // Glass: premultiplied transparency so reflections stay visible on a nearly clear surface.
        private static void MakeTransparent(Material material)
        {
            if (material.HasProperty("_Surface"))
            {
                // URP Lit
                material.SetFloat("_Surface", 1f);
                material.SetFloat("_Blend", 1f);
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            }
            else
            {
                // Built-in Standard, "Transparent" rendering mode
                material.SetFloat("_Mode", 3f);
                material.DisableKeyword("_ALPHATEST_ON");
                material.DisableKeyword("_ALPHABLEND_ON");
                material.EnableKeyword("_ALPHAPREMULTIPLY_ON");
            }

            material.SetOverrideTag("RenderType", "Transparent");
            material.SetInt("_SrcBlend", (int)BlendMode.One);
            material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            material.SetInt("_ZWrite", 0);
            material.renderQueue = (int)RenderQueue.Transparent;
        }
    }
}
