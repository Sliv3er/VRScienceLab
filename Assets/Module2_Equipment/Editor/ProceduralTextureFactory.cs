using System.IO;
using UnityEditor;
using UnityEngine;

namespace VRScienceLab.Module2.EditorTools
{
    /// <summary>
    /// Draws the few textures the laboratory needs (floor and ceiling tiles, brushed metal,
    /// window sky, safety signs and the scientific wall diagrams) and saves them as PNG assets,
    /// so the project does not depend on any downloaded texture.
    /// </summary>
    public static class ProceduralTextureFactory
    {
        private const string TextureFolder = Module2SceneBuilder.ModuleRoot + "/Textures/Generated";

        // ------------------------------------------------------------------
        // Surfaces
        // ------------------------------------------------------------------

        public static Texture2D FloorTiles()
        {
            const int size = 256;
            var canvas = new Canvas(size, size, new Color(0.93f, 0.94f, 0.95f));
            var random = new System.Random(11);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float noise = (float)random.NextDouble() * 0.035f;
                    canvas.Multiply(x, y, 0.965f + noise);
                }
            }

            // One tile per texture repeat: grout line along two edges.
            Color grout = new Color(0.70f, 0.73f, 0.76f);
            canvas.FillRect(0, 0, size, 3, grout);
            canvas.FillRect(0, 0, 3, size, grout);
            return Save("Floor_Tiles", canvas, true);
        }

        public static Texture2D CeilingTiles()
        {
            const int size = 128;
            var canvas = new Canvas(size, size, new Color(0.97f, 0.97f, 0.98f));
            Color joint = new Color(0.82f, 0.84f, 0.86f);
            canvas.FillRect(0, 0, size, 2, joint);
            canvas.FillRect(0, 0, 2, size, joint);
            return Save("Ceiling_Tiles", canvas, true);
        }

        public static Texture2D BrushedMetal()
        {
            const int size = 128;
            var canvas = new Canvas(size, size, Color.white);
            var random = new System.Random(5);
            for (int y = 0; y < size; y++)
            {
                float line = 0.90f + (float)random.NextDouble() * 0.10f;
                for (int x = 0; x < size; x++)
                {
                    canvas.Set(x, y, new Color(line, line, line));
                }
            }

            return Save("Brushed_Metal", canvas, true);
        }

        public static Texture2D SkyGradient()
        {
            const int width = 8;
            const int height = 128;
            var canvas = new Canvas(width, height, Color.white);
            for (int y = 0; y < height; y++)
            {
                float t = y / (float)(height - 1);
                Color color = Color.Lerp(new Color(0.86f, 0.93f, 0.98f), new Color(0.42f, 0.68f, 0.95f), t);
                for (int x = 0; x < width; x++) canvas.Set(x, y, color);
            }

            return Save("Window_Sky", canvas, false);
        }

        // ------------------------------------------------------------------
        // Signs (pictograms only, no text)
        // ------------------------------------------------------------------

        /// <summary>Yellow warning triangle with three heat waves: hot surface.</summary>
        public static Texture2D HotSurfaceSign()
        {
            const int size = 128;
            var canvas = new Canvas(size, size, new Color(0.98f, 0.80f, 0.10f));
            Color black = new Color(0.08f, 0.08f, 0.08f);
            canvas.Triangle(new Vector2(64, 118), new Vector2(8, 14), new Vector2(120, 14), black);
            canvas.Triangle(new Vector2(64, 100), new Vector2(24, 23), new Vector2(104, 23), new Color(0.98f, 0.80f, 0.10f));
            for (int i = -1; i <= 1; i++)
            {
                float x = 64 + i * 14;
                canvas.Line(new Vector2(x - 3, 34), new Vector2(x + 3, 46), 2.2f, black);
                canvas.Line(new Vector2(x + 3, 46), new Vector2(x - 3, 58), 2.2f, black);
                canvas.Line(new Vector2(x - 3, 58), new Vector2(x + 3, 70), 2.2f, black);
            }

            canvas.FillRect(34, 28, 60, 4, black);
            return Save("Sign_HotSurface", canvas, false);
        }

        /// <summary>Blue mandatory-action disc with safety goggles.</summary>
        public static Texture2D GogglesSign()
        {
            const int size = 128;
            var canvas = new Canvas(size, size, Color.white);
            Color blue = new Color(0.05f, 0.33f, 0.68f);
            canvas.Disc(new Vector2(64, 64), 60, blue);
            canvas.Disc(new Vector2(44, 62), 17, Color.white);
            canvas.Disc(new Vector2(84, 62), 17, Color.white);
            canvas.Disc(new Vector2(44, 62), 11, blue);
            canvas.Disc(new Vector2(84, 62), 11, blue);
            canvas.FillRect(58, 60, 12, 6, Color.white);
            canvas.Line(new Vector2(27, 66), new Vector2(12, 74), 4f, Color.white);
            canvas.Line(new Vector2(101, 66), new Vector2(116, 74), 4f, Color.white);
            return Save("Sign_Goggles", canvas, false);
        }

        /// <summary>Blue mandatory-action disc with a lab coat.</summary>
        public static Texture2D LabCoatSign()
        {
            const int size = 128;
            var canvas = new Canvas(size, size, Color.white);
            Color blue = new Color(0.05f, 0.33f, 0.68f);
            canvas.Disc(new Vector2(64, 64), 60, blue);
            canvas.FillRect(42, 22, 44, 66, Color.white);
            canvas.Triangle(new Vector2(42, 88), new Vector2(22, 60), new Vector2(42, 62), Color.white);
            canvas.Triangle(new Vector2(86, 88), new Vector2(106, 60), new Vector2(86, 62), Color.white);
            canvas.Triangle(new Vector2(64, 62), new Vector2(54, 88), new Vector2(74, 88), blue);
            canvas.Line(new Vector2(64, 22), new Vector2(64, 62), 2f, blue);
            canvas.Disc(new Vector2(64, 98), 9, Color.white);
            return Save("Sign_LabCoat", canvas, false);
        }

        // ------------------------------------------------------------------
        // Scientific diagrams for the wall frames (graphics only)
        // ------------------------------------------------------------------

        /// <summary>Periodic-table style grid of element blocks.</summary>
        public static Texture2D PeriodicGrid()
        {
            const int width = 512;
            const int height = 320;
            var canvas = new Canvas(width, height, new Color(0.96f, 0.97f, 0.98f));
            Color[] groups =
            {
                new Color(0.93f, 0.42f, 0.38f), new Color(0.96f, 0.67f, 0.32f), new Color(0.98f, 0.85f, 0.40f),
                new Color(0.47f, 0.76f, 0.55f), new Color(0.36f, 0.68f, 0.86f), new Color(0.50f, 0.52f, 0.82f),
                new Color(0.74f, 0.52f, 0.80f),
            };
            // Rows and the columns they occupy, like the real table.
            int[][] rows =
            {
                new[] { 0, 17 },
                new[] { 0, 1, 12, 13, 14, 15, 16, 17 },
                new[] { 0, 1, 12, 13, 14, 15, 16, 17 },
                null, null, null, null,
            };
            const int cell = 24;
            const int originX = 28;
            for (int r = 0; r < 7; r++)
            {
                int y = height - 46 - r * (cell + 3);
                for (int c = 0; c < 18; c++)
                {
                    if (rows[r] != null && System.Array.IndexOf(rows[r], c) < 0) continue;
                    Color color = c == 0 ? groups[0] : c == 1 ? groups[1] : c < 12 ? groups[4] : c < 15 ? groups[3] : c < 17 ? groups[2] : groups[6];
                    canvas.FillRect(originX + c * (cell + 2), y, cell, cell, color);
                    canvas.FillRect(originX + c * (cell + 2) + 3, y + cell - 7, 8, 3, new Color(1f, 1f, 1f, 0.75f));
                }
            }

            for (int r = 0; r < 2; r++)
            {
                int y = 30 - r * (cell + 3) + 20;
                for (int c = 3; c < 17; c++)
                {
                    canvas.FillRect(originX + c * (cell + 2), y - 14, cell, cell, r == 0 ? groups[5] : groups[1]);
                }
            }

            canvas.FillRect(0, height - 14, width, 14, new Color(0.10f, 0.55f, 0.70f));
            return Save("Diagram_PeriodicGrid", canvas, false);
        }

        /// <summary>Ball-and-stick molecule built from hexagonal rings.</summary>
        public static Texture2D Molecule()
        {
            const int width = 320;
            const int height = 400;
            var canvas = new Canvas(width, height, new Color(0.07f, 0.11f, 0.15f));
            Color bond = new Color(0.72f, 0.80f, 0.86f);
            Color carbon = new Color(0.20f, 0.78f, 0.92f);
            Color oxygen = new Color(0.95f, 0.38f, 0.32f);
            Color nitrogen = new Color(0.98f, 0.80f, 0.30f);

            Vector2[] centers = { new Vector2(118, 250), new Vector2(202, 250), new Vector2(160, 177) };
            const float radius = 48f;
            foreach (Vector2 center in centers)
            {
                for (int i = 0; i < 6; i++)
                {
                    Vector2 a = center + Polar(radius, 30 + i * 60);
                    Vector2 b = center + Polar(radius, 30 + (i + 1) * 60);
                    canvas.Line(a, b, 3.5f, bond);
                }
            }

            canvas.Line(centers[2] + Polar(radius, 270), new Vector2(160, 84), 3.5f, bond);
            canvas.Line(new Vector2(160, 84), new Vector2(118, 52), 3.5f, bond);
            canvas.Line(new Vector2(160, 84), new Vector2(204, 52), 3.5f, bond);
            canvas.Line(centers[0] + Polar(radius, 150), new Vector2(42, 300), 3.5f, bond);
            canvas.Line(centers[1] + Polar(radius, 30), new Vector2(280, 300), 3.5f, bond);

            foreach (Vector2 center in centers)
            {
                for (int i = 0; i < 6; i++) canvas.Disc(center + Polar(radius, 30 + i * 60), 9, carbon);
            }

            canvas.Disc(new Vector2(160, 84), 11, nitrogen);
            canvas.Disc(new Vector2(118, 52), 12, oxygen);
            canvas.Disc(new Vector2(204, 52), 12, oxygen);
            canvas.Disc(new Vector2(42, 300), 12, oxygen);
            canvas.Disc(new Vector2(280, 300), 11, nitrogen);
            canvas.FillRect(0, height - 12, width, 12, new Color(0.10f, 0.75f, 0.90f));
            return Save("Diagram_Molecule", canvas, false);
        }

        /// <summary>Heating curve: temperature against time with two plateaus.</summary>
        public static Texture2D HeatingCurve()
        {
            const int width = 400;
            const int height = 300;
            var canvas = new Canvas(width, height, new Color(0.96f, 0.97f, 0.98f));
            Color grid = new Color(0.84f, 0.87f, 0.90f);
            Color axis = new Color(0.20f, 0.24f, 0.28f);
            for (int i = 1; i < 8; i++) canvas.FillRect(50 + i * 40, 40, 1, 220, grid);
            for (int i = 1; i < 6; i++) canvas.FillRect(50, 40 + i * 40, 320, 1, grid);
            canvas.FillRect(50, 40, 2, 222, axis);
            canvas.FillRect(50, 40, 322, 2, axis);

            Vector2[] curve =
            {
                new Vector2(50, 60), new Vector2(110, 110), new Vector2(170, 110),
                new Vector2(250, 205), new Vector2(320, 205), new Vector2(368, 245),
            };
            for (int i = 0; i < curve.Length - 1; i++)
            {
                bool plateau = Mathf.Approximately(curve[i].y, curve[i + 1].y);
                canvas.Line(curve[i], curve[i + 1], 4f, plateau ? new Color(0.10f, 0.62f, 0.80f) : new Color(0.92f, 0.36f, 0.26f));
            }

            foreach (Vector2 point in curve) canvas.Disc(point, 5, axis);
            canvas.FillRect(0, height - 12, width, 12, new Color(0.92f, 0.36f, 0.26f));
            return Save("Diagram_HeatingCurve", canvas, false);
        }

        private static Vector2 Polar(float radius, float degrees)
        {
            float radians = degrees * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(radians) * radius, Mathf.Sin(radians) * radius);
        }

        // ------------------------------------------------------------------
        // Saving
        // ------------------------------------------------------------------

        private static Texture2D Save(string name, Canvas canvas, bool repeat)
        {
            Module2SceneBuilder.EnsureFolder(TextureFolder);
            string path = $"{TextureFolder}/{name}.png";

            var texture = new Texture2D(canvas.Width, canvas.Height, TextureFormat.RGBA32, false);
            texture.SetPixels(canvas.Pixels);
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null)
            {
                importer.wrapMode = repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
                importer.mipmapEnabled = true;
                importer.anisoLevel = 8;
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        /// <summary>Minimal software rasteriser (origin bottom-left, like Texture2D).</summary>
        private class Canvas
        {
            public readonly int Width;
            public readonly int Height;
            public readonly Color[] Pixels;

            public Canvas(int width, int height, Color background)
            {
                Width = width;
                Height = height;
                Pixels = new Color[width * height];
                for (int i = 0; i < Pixels.Length; i++) Pixels[i] = background;
            }

            public void Set(int x, int y, Color color)
            {
                if (x < 0 || y < 0 || x >= Width || y >= Height) return;
                int index = y * Width + x;
                Color existing = Pixels[index];
                Pixels[index] = new Color(
                    Mathf.Lerp(existing.r, color.r, color.a),
                    Mathf.Lerp(existing.g, color.g, color.a),
                    Mathf.Lerp(existing.b, color.b, color.a), 1f);
            }

            public void Multiply(int x, int y, float factor)
            {
                int index = y * Width + x;
                Color c = Pixels[index];
                Pixels[index] = new Color(c.r * factor, c.g * factor, c.b * factor, 1f);
            }

            public void FillRect(int x, int y, int width, int height, Color color)
            {
                for (int j = y; j < y + height; j++)
                {
                    for (int i = x; i < x + width; i++) Set(i, j, color);
                }
            }

            public void Disc(Vector2 center, float radius, Color color)
            {
                int minX = Mathf.FloorToInt(center.x - radius - 1);
                int maxX = Mathf.CeilToInt(center.x + radius + 1);
                int minY = Mathf.FloorToInt(center.y - radius - 1);
                int maxY = Mathf.CeilToInt(center.y + radius + 1);
                for (int y = minY; y <= maxY; y++)
                {
                    for (int x = minX; x <= maxX; x++)
                    {
                        float distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center);
                        float coverage = Mathf.Clamp01(radius - distance + 0.5f);
                        if (coverage > 0f) Set(x, y, new Color(color.r, color.g, color.b, color.a * coverage));
                    }
                }
            }

            public void Line(Vector2 a, Vector2 b, float halfWidth, Color color)
            {
                int minX = Mathf.FloorToInt(Mathf.Min(a.x, b.x) - halfWidth - 1);
                int maxX = Mathf.CeilToInt(Mathf.Max(a.x, b.x) + halfWidth + 1);
                int minY = Mathf.FloorToInt(Mathf.Min(a.y, b.y) - halfWidth - 1);
                int maxY = Mathf.CeilToInt(Mathf.Max(a.y, b.y) + halfWidth + 1);
                Vector2 ab = b - a;
                float lengthSquared = Mathf.Max(ab.sqrMagnitude, 0.0001f);
                for (int y = minY; y <= maxY; y++)
                {
                    for (int x = minX; x <= maxX; x++)
                    {
                        var p = new Vector2(x + 0.5f, y + 0.5f);
                        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / lengthSquared);
                        float distance = Vector2.Distance(p, a + ab * t);
                        float coverage = Mathf.Clamp01(halfWidth - distance + 0.5f);
                        if (coverage > 0f) Set(x, y, new Color(color.r, color.g, color.b, color.a * coverage));
                    }
                }
            }

            public void Triangle(Vector2 a, Vector2 b, Vector2 c, Color color)
            {
                int minX = Mathf.FloorToInt(Mathf.Min(a.x, Mathf.Min(b.x, c.x)));
                int maxX = Mathf.CeilToInt(Mathf.Max(a.x, Mathf.Max(b.x, c.x)));
                int minY = Mathf.FloorToInt(Mathf.Min(a.y, Mathf.Min(b.y, c.y)));
                int maxY = Mathf.CeilToInt(Mathf.Max(a.y, Mathf.Max(b.y, c.y)));
                for (int y = minY; y <= maxY; y++)
                {
                    for (int x = minX; x <= maxX; x++)
                    {
                        var p = new Vector2(x + 0.5f, y + 0.5f);
                        float d1 = Cross(p, a, b);
                        float d2 = Cross(p, b, c);
                        float d3 = Cross(p, c, a);
                        bool negative = d1 < 0 || d2 < 0 || d3 < 0;
                        bool positive = d1 > 0 || d2 > 0 || d3 > 0;
                        if (!(negative && positive)) Set(x, y, color);
                    }
                }
            }

            private static float Cross(Vector2 p, Vector2 a, Vector2 b)
            {
                return (p.x - b.x) * (a.y - b.y) - (a.x - b.x) * (p.y - b.y);
            }
        }
    }
}
