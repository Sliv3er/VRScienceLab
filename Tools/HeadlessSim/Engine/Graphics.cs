// HeadlessSim: rendering, audio and animation types as plain data. Not part of the Unity project.
using System;
using System.Collections.Generic;
using System.Linq;
using HeadlessSim;

namespace UnityEngine
{
    namespace Rendering
    {
        public enum AmbientMode { Skybox = 0, Trilight = 1, Flat = 3, Custom = 4 }
        public enum DefaultReflectionMode { Skybox, Custom }
        public enum ShadowCastingMode { Off, On, TwoSided, ShadowsOnly }
        public enum BlendMode { Zero = 0, One = 1, DstColor = 2, SrcColor = 3, OneMinusDstColor = 4, SrcAlpha = 5, OneMinusSrcColor = 6, DstAlpha = 7, OneMinusDstAlpha = 8, SrcAlphaSaturate = 9, OneMinusSrcAlpha = 10 }
        public enum RenderQueue { Background = 1000, Geometry = 2000, AlphaTest = 2450, GeometryLast = 2500, Transparent = 3000, Overlay = 4000 }
        public enum ReflectionProbeMode { Baked, Realtime, Custom }
        public enum LightProbeUsage { Off, BlendProbes, UseProxyVolume, CustomProvided }
        public enum ReflectionProbeUsage { Off, BlendProbes, BlendProbesAndSkybox, Simple }
        public enum CullMode { Off, Front, Back }
        public enum CompareFunction { Disabled, Never, Less, Equal, LessEqual, Greater, NotEqual, GreaterEqual, Always }
        public enum IndexFormat { UInt16, UInt32 }
        public class RenderPipelineAsset : ScriptableObject { }
        public static class GraphicsSettings
        {
            public static RenderPipelineAsset currentRenderPipeline { get; set; }
            public static RenderPipelineAsset defaultRenderPipeline { get; set; }
            public static RenderPipelineAsset renderPipelineAsset { get; set; }
        }
    }

    public enum MaterialGlobalIlluminationFlags { None = 0, RealtimeEmissive = 1, BakedEmissive = 2, EmissiveIsBlack = 4, AnyEmissive = 3 }
    public enum TextureFormat { Alpha8 = 1, RGB24 = 3, RGBA32 = 4, ARGB32 = 5, RGBAHalf = 17, RGBAFloat = 20 }
    public enum TextureWrapMode { Repeat, Clamp, Mirror, MirrorOnce }
    public enum FilterMode { Point, Bilinear, Trilinear }
    public enum ColorSpace { Uninitialized = -1, Gamma = 0, Linear = 1 }
    public enum LightType { Spot, Directional, Point, Area, Rectangle = 3, Disc = 4 }
    public enum LightShadows { None, Hard, Soft }
    public enum LightRenderMode { Auto, ForcePixel, ForceVertex }
    public enum LightmapBakeType { Realtime = 4, Baked = 2, Mixed = 1 }
    public enum CameraClearFlags { Skybox = 1, Color = 2, SolidColor = 2, Depth = 3, Nothing = 4 }
    public enum StereoTargetEyeMask { None, Left, Right, Both }
    public enum LineAlignment { View, TransformZ }
    public enum LineTextureMode { Stretch, Tile, DistributePerSegment, RepeatPerSegment }
    public enum AnimatorCullingMode { AlwaysAnimate, CullUpdateTransforms, CullCompletely }
    public enum AnimatorUpdateMode { Normal, Fixed, UnscaledTime }
    public enum AnimatorControllerParameterType { Float = 1, Int = 3, Bool = 4, Trigger = 9 }
    public enum AudioRolloffMode { Logarithmic, Linear, Custom }

    public class Shader : Object
    {
        private static readonly Dictionary<string, int> Ids = new Dictionary<string, int>();
        private static readonly Dictionary<string, Shader> Shaders = new Dictionary<string, Shader>();

        public static int PropertyToID(string name)
        {
            if (!Ids.TryGetValue(name, out int id)) { id = Ids.Count + 1; Ids[name] = id; }
            return id;
        }

        internal static string NameOf(int id) => Ids.FirstOrDefault(pair => pair.Value == id).Key ?? ("#" + id);

        /// <summary>Returns a named shader; unknown render-pipeline shaders return null like in a Built-in pipeline project.</summary>
        public static Shader Find(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            if (name.StartsWith("Universal Render Pipeline/") || name.StartsWith("HDRP/")) return null;
            if (!Shaders.TryGetValue(name, out Shader shader)) { shader = new Shader { name = name }; Shaders[name] = shader; }
            return shader;
        }

        public bool isSupported => true;
        public int renderQueue => 2000;
        public static void EnableKeyword(string keyword) { }
        public static void DisableKeyword(string keyword) { }
        public static void SetGlobalFloat(string name, float value) { }
        public static void SetGlobalColor(string name, Color value) { }
    }

    public class Texture : Object
    {
        public virtual int width { get; set; }
        public virtual int height { get; set; }
        public TextureWrapMode wrapMode;
        public FilterMode filterMode = FilterMode.Bilinear;
        public int anisoLevel = 1;
        public float mipMapBias;
        public Vector2 texelSize => new Vector2(width > 0 ? 1f / width : 0f, height > 0 ? 1f / height : 0f);
    }

    public class Texture2D : Texture
    {
        private Color[] pixels;
        public TextureFormat format { get; private set; } = TextureFormat.RGBA32;
        public bool isReadable => true;
        public int mipmapCount => 1;
        public static Texture2D whiteTexture { get; } = new Texture2D(4, 4) { name = "white" };
        public static Texture2D blackTexture { get; } = new Texture2D(4, 4) { name = "black" };

        public Texture2D(int width, int height) : this(width, height, TextureFormat.RGBA32, true) { }
        public Texture2D(int width, int height, TextureFormat textureFormat, bool mipChain) : this(width, height, textureFormat, mipChain, false) { }
        public Texture2D(int width, int height, TextureFormat textureFormat, bool mipChain, bool linear)
        {
            this.width = width; this.height = height; format = textureFormat;
            pixels = new Color[Math.Max(width * height, 0)];
        }

        public void SetPixel(int x, int y, Color color) { if (x >= 0 && y >= 0 && x < width && y < height) pixels[y * width + x] = color; }
        public Color GetPixel(int x, int y) => x >= 0 && y >= 0 && x < width && y < height ? pixels[y * width + x] : Color.clear;
        public void SetPixels(Color[] colors)
        {
            if (colors.Length != pixels.Length) throw new ArgumentException($"SetPixels: array size {colors.Length} does not match the texture ({pixels.Length} pixels).");
            Array.Copy(colors, pixels, pixels.Length);
        }
        public void SetPixels(Color[] colors, int miplevel) { SetPixels(colors); }
        public void SetPixels32(Color32[] colors) { for (int i = 0; i < pixels.Length && i < colors.Length; i++) pixels[i] = colors[i]; }
        public Color[] GetPixels() => (Color[])pixels.Clone();
        public Color32[] GetPixels32() => pixels.Select(p => (Color32)p).ToArray();
        public void Apply() { }
        public void Apply(bool updateMipmaps) { }
        public void Apply(bool updateMipmaps, bool makeNoLongerReadable) { }
        // In Unity this is the ImageConversion extension method; the simulator returns a small marker payload.
        public byte[] EncodeToPNG() => new byte[] { 0x89, 0x50, 0x4E, 0x47 };
        public byte[] EncodeToJPG() => new byte[] { 0xFF, 0xD8 };
        public bool LoadImage(byte[] data) => true;
        public bool Reinitialize(int w, int h) { width = w; height = h; pixels = new Color[w * h]; return true; }
    }

    public class Cubemap : Texture { public Cubemap() { } public Cubemap(int width, TextureFormat format, bool mipChain) { this.width = width; height = width; } }
    public class RenderTexture : Texture { public RenderTexture(int width, int height, int depth) { this.width = width; this.height = height; } public static RenderTexture active { get; set; } public void Release() { } public bool Create() => true; }

    public class Sprite : Object
    {
        public Texture2D texture { get; internal set; }
        public Rect rect { get; internal set; }
        public Vector4 border { get; internal set; }
        public float pixelsPerUnit { get; internal set; } = 100f;
        public Vector2 pivot { get; internal set; }
        public Bounds bounds => new Bounds(Vector3.zero, new Vector3(rect.width / pixelsPerUnit, rect.height / pixelsPerUnit, 0f));
        public static Sprite Create(Texture2D texture, Rect rect, Vector2 pivot) => new Sprite { texture = texture, rect = rect, pivot = pivot, name = texture != null ? texture.name : "Sprite" };
        public static Sprite Create(Texture2D texture, Rect rect, Vector2 pivot, float pixelsPerUnit) => new Sprite { texture = texture, rect = rect, pivot = pivot, pixelsPerUnit = pixelsPerUnit };
    }

    public class Material : Object
    {
        internal readonly Dictionary<string, object> properties = new Dictionary<string, object>();
        internal readonly HashSet<string> keywords = new HashSet<string>();
        internal readonly Dictionary<string, string> tags = new Dictionary<string, string>();

        public Shader shader;
        public int renderQueue = 2000;
        public MaterialGlobalIlluminationFlags globalIlluminationFlags;
        public bool enableInstancing;
        public bool doubleSidedGI;

        public Material(Shader shader)
        {
            this.shader = shader;
            name = shader != null ? shader.name : "Material";
        }

        public Material(Material source)
        {
            shader = source.shader;
            name = source.name;
            renderQueue = source.renderQueue;
            globalIlluminationFlags = source.globalIlluminationFlags;
            foreach (KeyValuePair<string, object> pair in source.properties) properties[pair.Key] = pair.Value;
            foreach (string keyword in source.keywords) keywords.Add(keyword);
        }

        // Which properties a shader exposes: Standard / Unlit use _Color and _MainTex; the URP names are not present.
        public bool HasProperty(string name)
        {
            if (properties.ContainsKey(name)) return true;
            string shaderName = shader != null ? shader.name : "";
            if (shaderName == "Standard")
            {
                return name == "_Color" || name == "_MainTex" || name == "_Glossiness" || name == "_Metallic" || name == "_EmissionColor" ||
                       name == "_Mode" || name == "_SrcBlend" || name == "_DstBlend" || name == "_ZWrite" || name == "_BumpMap" || name == "_Cutoff";
            }
            if (shaderName.StartsWith("Unlit/")) return name == "_Color" || name == "_MainTex";
            return false;
        }
        public bool HasProperty(int id) => HasProperty(Shader.NameOf(id));
        public bool HasColor(string name) => HasProperty(name);
        public bool HasFloat(string name) => HasProperty(name);
        public bool HasTexture(string name) => HasProperty(name);

        public Color color { get => GetColor("_Color"); set => SetColor("_Color", value); }
        public Texture mainTexture { get => GetTexture("_MainTex"); set => SetTexture("_MainTex", value); }
        public Vector2 mainTextureScale { get => GetTextureScale("_MainTex"); set => SetTextureScale("_MainTex", value); }
        public Vector2 mainTextureOffset { get => GetTextureOffset("_MainTex"); set => SetTextureOffset("_MainTex", value); }

        public void SetColor(string name, Color value) { properties[name] = value; }
        public void SetColor(int id, Color value) { properties[Shader.NameOf(id)] = value; }
        public Color GetColor(string name) => properties.TryGetValue(name, out object v) && v is Color c ? c : Color.white;
        public Color GetColor(int id) => GetColor(Shader.NameOf(id));
        public void SetFloat(string name, float value) { properties[name] = value; }
        public void SetFloat(int id, float value) { properties[Shader.NameOf(id)] = value; }
        public float GetFloat(string name) => properties.TryGetValue(name, out object v) && v is float f ? f : 0f;
        public float GetFloat(int id) => GetFloat(Shader.NameOf(id));
        public void SetInt(string name, int value) { properties[name] = (float)value; }
        public void SetInt(int id, int value) { properties[Shader.NameOf(id)] = (float)value; }
        public void SetInteger(string name, int value) { properties[name] = (float)value; }
        public int GetInt(string name) => (int)GetFloat(name);
        public void SetVector(string name, Vector4 value) { properties[name] = value; }
        public Vector4 GetVector(string name) => properties.TryGetValue(name, out object v) && v is Vector4 vec ? vec : Vector4.zero;
        public void SetTexture(string name, Texture value) { properties[name] = value; }
        public void SetTexture(int id, Texture value) { properties[Shader.NameOf(id)] = value; }
        public Texture GetTexture(string name) => properties.TryGetValue(name, out object v) ? v as Texture : null;
        public void SetTextureScale(string name, Vector2 value) { properties[name + "_ST_scale"] = value; }
        public Vector2 GetTextureScale(string name) => properties.TryGetValue(name + "_ST_scale", out object v) && v is Vector2 s ? s : Vector2.one;
        public void SetTextureOffset(string name, Vector2 value) { properties[name + "_ST_offset"] = value; }
        public Vector2 GetTextureOffset(string name) => properties.TryGetValue(name + "_ST_offset", out object v) && v is Vector2 s ? s : Vector2.zero;
        public void EnableKeyword(string keyword) { keywords.Add(keyword); }
        public void DisableKeyword(string keyword) { keywords.Remove(keyword); }
        public bool IsKeywordEnabled(string keyword) => keywords.Contains(keyword);
        public void SetOverrideTag(string tag, string value) { tags[tag] = value; }
        public string GetTag(string tag, bool searchFallbacks) => tags.TryGetValue(tag, out string v) ? v : "";
        public string GetTag(string tag, bool searchFallbacks, string defaultValue) => tags.TryGetValue(tag, out string v) ? v : defaultValue;
        public void CopyPropertiesFromMaterial(Material other) { foreach (KeyValuePair<string, object> pair in other.properties) properties[pair.Key] = pair.Value; }
        public int passCount => 1;
        public void SetPass(int pass) { }
    }

    public sealed class MaterialPropertyBlock
    {
        private readonly Dictionary<string, object> values = new Dictionary<string, object>();
        public bool isEmpty => values.Count == 0;
        public void Clear() { values.Clear(); }
        public void SetColor(string name, Color value) { values[name] = value; }
        public void SetColor(int id, Color value) { values[Shader.NameOf(id)] = value; }
        public Color GetColor(string name) => values.TryGetValue(name, out object v) && v is Color c ? c : Color.clear;
        public Color GetColor(int id) => GetColor(Shader.NameOf(id));
        public void SetFloat(string name, float value) { values[name] = value; }
        public void SetFloat(int id, float value) { values[Shader.NameOf(id)] = value; }
        public float GetFloat(string name) => values.TryGetValue(name, out object v) && v is float f ? f : 0f;
        public float GetFloat(int id) => GetFloat(Shader.NameOf(id));
        public void SetVector(string name, Vector4 value) { values[name] = value; }
        public void SetVector(int id, Vector4 value) { values[Shader.NameOf(id)] = value; }
        public void SetTexture(string name, Texture value) { values[name] = value; }
        public void SetTexture(int id, Texture value) { values[Shader.NameOf(id)] = value; }
        public bool HasColor(string name) => values.ContainsKey(name);
        public bool HasProperty(string name) => values.ContainsKey(name);
        internal MaterialPropertyBlock Copy() { var copy = new MaterialPropertyBlock(); foreach (KeyValuePair<string, object> pair in values) copy.values[pair.Key] = pair.Value; return copy; }
        internal void CopyTo(MaterialPropertyBlock target) { target.values.Clear(); foreach (KeyValuePair<string, object> pair in values) target.values[pair.Key] = pair.Value; }
    }

    public class Mesh : Object
    {
        private Vector3[] meshVertices = new Vector3[0];
        private Vector3[] meshNormals = new Vector3[0];
        private Vector4[] meshTangents = new Vector4[0];
        private Vector2[] meshUv = new Vector2[0];
        private Color[] meshColors = new Color[0];
        private int[] meshTriangles = new int[0];
        private Bounds meshBounds;

        public UnityEngine.Rendering.IndexFormat indexFormat;
        public Vector3[] vertices { get => (Vector3[])meshVertices.Clone(); set { meshVertices = value ?? new Vector3[0]; RecalculateBounds(); } }
        public Vector3[] normals { get => (Vector3[])meshNormals.Clone(); set => meshNormals = value ?? new Vector3[0]; }
        public Vector4[] tangents { get => (Vector4[])meshTangents.Clone(); set => meshTangents = value ?? new Vector4[0]; }
        public Vector2[] uv { get => (Vector2[])meshUv.Clone(); set => meshUv = value ?? new Vector2[0]; }
        public Color[] colors { get => (Color[])meshColors.Clone(); set => meshColors = value ?? new Color[0]; }
        public int[] triangles { get => (int[])meshTriangles.Clone(); set => meshTriangles = value ?? new int[0]; }
        public int vertexCount => meshVertices.Length;
        public int subMeshCount { get; set; } = 1;
        public bool isReadable => true;
        public Bounds bounds { get => meshBounds; set => meshBounds = value; }

        public void SetVertices(List<Vector3> inVertices) { meshVertices = inVertices.ToArray(); RecalculateBounds(); }
        public void SetVertices(Vector3[] inVertices) { meshVertices = (Vector3[])inVertices.Clone(); RecalculateBounds(); }
        public void GetVertices(List<Vector3> result) { result.Clear(); result.AddRange(meshVertices); }
        public void SetNormals(List<Vector3> inNormals) { meshNormals = inNormals.ToArray(); }
        public void SetNormals(Vector3[] inNormals) { meshNormals = (Vector3[])inNormals.Clone(); }
        public void SetUVs(int channel, List<Vector2> uvs) { if (channel == 0) meshUv = uvs.ToArray(); }
        public void SetUVs(int channel, Vector2[] uvs) { if (channel == 0) meshUv = (Vector2[])uvs.Clone(); }
        public void SetColors(List<Color> inColors) { meshColors = inColors.ToArray(); }
        public void SetTriangles(List<int> inTriangles, int submesh) { meshTriangles = inTriangles.ToArray(); }
        public void SetTriangles(int[] inTriangles, int submesh) { meshTriangles = (int[])inTriangles.Clone(); }
        public int[] GetTriangles(int submesh) => triangles;
        public void SetIndices(int[] indices, MeshTopology topology, int submesh) { meshTriangles = (int[])indices.Clone(); }

        public void RecalculateBounds()
        {
            if (meshVertices.Length == 0) { meshBounds = new Bounds(Vector3.zero, Vector3.zero); return; }
            Vector3 min = meshVertices[0], max = meshVertices[0];
            for (int i = 1; i < meshVertices.Length; i++) { min = Vector3.Min(min, meshVertices[i]); max = Vector3.Max(max, meshVertices[i]); }
            var bounds = new Bounds();
            bounds.SetMinMax(min, max);
            meshBounds = bounds;
        }

        public void RecalculateNormals()
        {
            var result = new Vector3[meshVertices.Length];
            for (int t = 0; t + 2 < meshTriangles.Length; t += 3)
            {
                Vector3 a = meshVertices[meshTriangles[t]], b = meshVertices[meshTriangles[t + 1]], c = meshVertices[meshTriangles[t + 2]];
                Vector3 n = Vector3.Cross(b - a, c - a);
                result[meshTriangles[t]] += n; result[meshTriangles[t + 1]] += n; result[meshTriangles[t + 2]] += n;
            }
            for (int i = 0; i < result.Length; i++) result[i] = result[i].sqrMagnitude > 0f ? result[i].normalized : Vector3.up;
            meshNormals = result;
        }

        public void RecalculateTangents() { meshTangents = new Vector4[meshVertices.Length]; }
        public void Optimize() { }
        public void UploadMeshData(bool markNoLongerReadable) { }
        public void MarkDynamic() { }

        public void Clear()
        {
            meshVertices = new Vector3[0]; meshNormals = new Vector3[0]; meshTangents = new Vector4[0];
            meshUv = new Vector2[0]; meshColors = new Color[0]; meshTriangles = new int[0];
            meshBounds = new Bounds(Vector3.zero, Vector3.zero);
        }
        public void Clear(bool keepVertexLayout) { Clear(); }

        internal void CopyFrom(Mesh other)
        {
            meshVertices = (Vector3[])other.meshVertices.Clone(); meshNormals = (Vector3[])other.meshNormals.Clone();
            meshTangents = (Vector4[])other.meshTangents.Clone(); meshUv = (Vector2[])other.meshUv.Clone();
            meshColors = (Color[])other.meshColors.Clone(); meshTriangles = (int[])other.meshTriangles.Clone();
            meshBounds = other.meshBounds; subMeshCount = other.subMeshCount;
        }
    }

    public enum MeshTopology { Triangles = 0, Quads = 2, Lines = 3, LineStrip = 4, Points = 5 }

    internal static class PrimitiveMeshes
    {
        private static readonly Dictionary<PrimitiveType, Mesh> Meshes = new Dictionary<PrimitiveType, Mesh>();
        private static Material defaultMaterial;
        public static Material DefaultMaterial => defaultMaterial ?? (defaultMaterial = new Material(Shader.Find("Standard")) { name = "Default-Material" });

        public static Mesh Get(PrimitiveType type)
        {
            if (Meshes.TryGetValue(type, out Mesh mesh)) return mesh;
            Vector3 half;
            switch (type)
            {
                case PrimitiveType.Plane: half = new Vector3(5f, 0f, 5f); break;
                case PrimitiveType.Quad: half = new Vector3(0.5f, 0.5f, 0f); break;
                case PrimitiveType.Cylinder:
                case PrimitiveType.Capsule: half = new Vector3(0.5f, 1f, 0.5f); break;
                default: half = new Vector3(0.5f, 0.5f, 0.5f); break;
            }

            mesh = new Mesh { name = type.ToString() };
            var vertices = new List<Vector3>();
            for (int x = -1; x <= 1; x += 2) for (int y = -1; y <= 1; y += 2) for (int z = -1; z <= 1; z += 2) vertices.Add(new Vector3(x * half.x, y * half.y, z * half.z));
            mesh.SetVertices(vertices);
            Meshes[type] = mesh;
            return mesh;
        }
    }

    public class MeshFilter : Component
    {
        public Mesh sharedMesh;
        public Mesh mesh { get => sharedMesh; set => sharedMesh = value; }
    }

    public class Renderer : Component
    {
        [SerializeField] private bool m_RendererEnabled = true;
        [SerializeField] private Material[] m_Materials = new Material[1];
        internal MaterialPropertyBlock propertyBlock;

        public UnityEngine.Rendering.ShadowCastingMode shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        public bool receiveShadows = true;
        public UnityEngine.Rendering.LightProbeUsage lightProbeUsage;
        public UnityEngine.Rendering.ReflectionProbeUsage reflectionProbeUsage;
        public int sortingOrder;
        public string sortingLayerName = "Default";
        public bool allowOcclusionWhenDynamic = true;

        public bool enabled { get => m_RendererEnabled; set => m_RendererEnabled = value; }
        internal override bool SimEnabled => m_RendererEnabled;
        public bool isVisible => m_RendererEnabled && owner.activeInHierarchy;

        public Material sharedMaterial
        {
            get => m_Materials.Length > 0 ? m_Materials[0] : null;
            set { if (m_Materials.Length == 0) m_Materials = new Material[1]; m_Materials[0] = value; }
        }
        public Material[] sharedMaterials { get => (Material[])m_Materials.Clone(); set => m_Materials = value != null ? (Material[])value.Clone() : new Material[0]; }
        // Accessing .material in Unity instantiates a private copy; the simulator does the same.
        public Material material
        {
            get
            {
                if (sharedMaterial != null && !sharedMaterial.name.EndsWith(" (Instance)")) sharedMaterial = new Material(sharedMaterial) { name = sharedMaterial.name + " (Instance)" };
                return sharedMaterial;
            }
            set => sharedMaterial = value;
        }
        public Material[] materials { get => sharedMaterials; set => sharedMaterials = value; }

        public void SetPropertyBlock(MaterialPropertyBlock properties) { propertyBlock = properties == null || properties.isEmpty ? null : properties.Copy(); }
        public void SetPropertyBlock(MaterialPropertyBlock properties, int materialIndex) { SetPropertyBlock(properties); }
        public void GetPropertyBlock(MaterialPropertyBlock properties) { if (propertyBlock != null) propertyBlock.CopyTo(properties); else properties.Clear(); }
        public bool HasPropertyBlock() => propertyBlock != null;
        /// <summary>Simulator helper: the last property block applied to this renderer (null when cleared).</summary>
        public MaterialPropertyBlock SimPropertyBlock => propertyBlock;

        public virtual Bounds bounds
        {
            get
            {
                Mesh mesh = owner.GetComponent<MeshFilter>()?.sharedMesh;
                return mesh != null ? PhysicsMath.TransformBounds(transform, mesh.bounds) : new Bounds(transform.position, Vector3.zero);
            }
        }
        public Bounds localBounds
        {
            get { Mesh mesh = owner.GetComponent<MeshFilter>()?.sharedMesh; return mesh != null ? mesh.bounds : new Bounds(); }
        }
    }

    public class MeshRenderer : Renderer { }

    public class SkinnedMeshRenderer : Renderer
    {
        public Mesh sharedMesh;
        public Transform[] bones = new Transform[0];
        public Transform rootBone;
        public bool updateWhenOffscreen;
        public override Bounds bounds => sharedMesh != null ? PhysicsMath.TransformBounds(transform, sharedMesh.bounds) : new Bounds(transform.position, Vector3.zero);
    }

    public class LineRenderer : Renderer
    {
        [SerializeField] private Vector3[] m_Positions = new Vector3[0];
        public bool useWorldSpace = true;
        public bool loop;
        public float widthMultiplier = 1f;
        public float startWidth = 1f, endWidth = 1f;
        public Color startColor = Color.white, endColor = Color.white;
        public int numCapVertices, numCornerVertices;
        public LineAlignment alignment;
        public LineTextureMode textureMode;
        public Gradient colorGradient;

        public int positionCount
        {
            get => m_Positions.Length;
            set { if (value != m_Positions.Length) Array.Resize(ref m_Positions, Math.Max(value, 0)); }
        }
        public void SetPosition(int index, Vector3 position)
        {
            if (index < 0 || index >= m_Positions.Length) throw new IndexOutOfRangeException($"LineRenderer.SetPosition index {index} is out of bounds (positionCount {m_Positions.Length}).");
            m_Positions[index] = position;
        }
        public Vector3 GetPosition(int index) => m_Positions[index];
        public void SetPositions(Vector3[] positions) { for (int i = 0; i < m_Positions.Length && i < positions.Length; i++) m_Positions[i] = positions[i]; }
        public int GetPositions(Vector3[] positions) { int n = Math.Min(positions.Length, m_Positions.Length); Array.Copy(m_Positions, positions, n); return n; }
    }

    [Serializable] public class Gradient { public GradientColorKey[] colorKeys = new GradientColorKey[0]; public GradientAlphaKey[] alphaKeys = new GradientAlphaKey[0]; public void SetKeys(GradientColorKey[] c, GradientAlphaKey[] a) { colorKeys = c; alphaKeys = a; } public Color Evaluate(float time) => colorKeys.Length > 0 ? colorKeys[0].color : Color.white; }
    [Serializable] public struct GradientColorKey { public Color color; public float time; public GradientColorKey(Color color, float time) { this.color = color; this.time = time; } }
    [Serializable] public struct GradientAlphaKey { public float alpha; public float time; public GradientAlphaKey(float alpha, float time) { this.alpha = alpha; this.time = time; } }
    [Serializable] public struct Keyframe { public float time, value; public Keyframe(float time, float value) { this.time = time; this.value = value; } }
    [Serializable]
    public class AnimationCurve
    {
        public Keyframe[] keys = new Keyframe[0];
        public AnimationCurve() { }
        public AnimationCurve(params Keyframe[] keys) { this.keys = keys; }
        public static AnimationCurve Linear(float t0, float v0, float t1, float v1) => new AnimationCurve(new Keyframe(t0, v0), new Keyframe(t1, v1));
        public static AnimationCurve EaseInOut(float t0, float v0, float t1, float v1) => Linear(t0, v0, t1, v1);
        public float Evaluate(float time)
        {
            if (keys.Length == 0) return 0f;
            if (time <= keys[0].time) return keys[0].value;
            for (int i = 1; i < keys.Length; i++)
            {
                if (time <= keys[i].time) return Mathf.Lerp(keys[i - 1].value, keys[i].value, Mathf.InverseLerp(keys[i - 1].time, keys[i].time, time));
            }
            return keys[keys.Length - 1].value;
        }
    }

    public class Light : Behaviour
    {
        public LightType type = LightType.Point;
        public Color color = Color.white;
        public float intensity = 1f;
        public float bounceIntensity = 1f;
        public float range = 10f;
        public float spotAngle = 30f;
        public float innerSpotAngle = 21.8f;
        public LightShadows shadows;
        public float shadowStrength = 1f;
        public float shadowBias = 0.05f;
        public float shadowNormalBias = 0.4f;
        public float shadowNearPlane = 0.2f;
        public LightRenderMode renderMode;
        public LightmapBakeType lightmapBakeType = LightmapBakeType.Realtime;
        public int cullingMask = -1;
        public float colorTemperature = 6570f;
        public bool useColorTemperature;
    }

    public class ReflectionProbe : Behaviour
    {
        public UnityEngine.Rendering.ReflectionProbeMode mode;
        public Vector3 size = new Vector3(10f, 10f, 10f);
        public Vector3 center;
        public bool boxProjection;
        public int resolution = 128;
        public bool hdr = true;
        public float intensity = 1f;
        public float nearClipPlane = 0.3f, farClipPlane = 1000f;
        public Texture bakedTexture;
        public Texture customBakedTexture;
        public int importance = 1;
        public float blendDistance = 1f;
        public int RenderProbe() => 0;
    }

    public static class RenderSettings
    {
        public static UnityEngine.Rendering.AmbientMode ambientMode;
        public static Color ambientLight, ambientSkyColor, ambientEquatorColor, ambientGroundColor;
        public static float ambientIntensity = 1f;
        public static bool fog;
        public static Color fogColor;
        public static float fogDensity;
        public static Material skybox;
        public static Light sun;
        public static UnityEngine.Rendering.DefaultReflectionMode defaultReflectionMode;
        public static float reflectionIntensity = 1f;
        public static int reflectionBounces = 1;
        public static int defaultReflectionResolution = 128;
        public static Texture customReflectionTexture;
        [Obsolete] public static Cubemap customReflection;
    }

    public static class QualitySettings
    {
        public static int vSyncCount { get; set; }
        public static int antiAliasing { get; set; }
        public static float shadowDistance { get; set; } = 40f;
        public static ColorSpace activeColorSpace => ColorSpace.Linear;
        public static int pixelLightCount { get; set; } = 4;
    }

    public struct Ray
    {
        public Vector3 origin;
        private Vector3 dir;
        public Vector3 direction { get => dir; set => dir = value.normalized; }
        public Ray(Vector3 origin, Vector3 direction) { this.origin = origin; dir = direction.normalized; }
        public Vector3 GetPoint(float distance) => origin + dir * distance;
    }

    public class Camera : Behaviour
    {
        public float depth;
        public Rect rect = new Rect(0f, 0f, 1f, 1f);
        public float fieldOfView = 60f;
        public float nearClipPlane = 0.3f;
        public float farClipPlane = 1000f;
        public float aspect = 16f / 9f;
        public bool orthographic;
        public float orthographicSize = 5f;
        public StereoTargetEyeMask stereoTargetEye = StereoTargetEyeMask.Both;
        public CameraClearFlags clearFlags = CameraClearFlags.Skybox;
        public Color backgroundColor = new Color(0.19f, 0.3f, 0.47f);
        public int cullingMask = -1;
        public RenderTexture targetTexture;
        public bool allowHDR = true, allowMSAA = true;
        public bool stereoEnabled => false;
        public int pixelWidth => Screen.width;
        public int pixelHeight => Screen.height;

        public static Camera main
        {
            get
            {
                foreach (GameObject go in Simulation.AllSceneObjects(false))
                {
                    if (go.tag != "MainCamera") continue;
                    Camera camera = go.GetComponent<Camera>();
                    if (camera != null && camera.enabled) return camera;
                }
                return null;
            }
        }
        public static Camera current => main;
        public static Camera[] allCameras => Simulation.FindAll<Camera>(false).ToArray();
        public static int allCamerasCount => allCameras.Length;

        public Ray ScreenPointToRay(Vector3 position)
        {
            float halfHeight = Mathf.Tan(fieldOfView * 0.5f * Mathf.Deg2Rad);
            float nx = (position.x / Screen.width) * 2f - 1f, ny = (position.y / Screen.height) * 2f - 1f;
            Vector3 local = new Vector3(nx * halfHeight * aspect, ny * halfHeight, 1f);
            return new Ray(transform.position, transform.TransformDirection(local));
        }
        public Ray ViewportPointToRay(Vector3 position) => ScreenPointToRay(new Vector3(position.x * Screen.width, position.y * Screen.height, 0f));
        public Vector3 WorldToScreenPoint(Vector3 position)
        {
            Vector3 local = transform.InverseTransformPoint(position);
            float halfHeight = Mathf.Tan(fieldOfView * 0.5f * Mathf.Deg2Rad);
            if (Mathf.Abs(local.z) < 1e-6f) return new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f);
            return new Vector3((local.x / (local.z * halfHeight * aspect) + 1f) * 0.5f * Screen.width, (local.y / (local.z * halfHeight) + 1f) * 0.5f * Screen.height, local.z);
        }
        public Vector3 WorldToViewportPoint(Vector3 position) { Vector3 s = WorldToScreenPoint(position); return new Vector3(s.x / Screen.width, s.y / Screen.height, s.z); }
        public Vector3 ScreenToWorldPoint(Vector3 position) => ScreenPointToRay(position).GetPoint(position.z);
        public void Render() { }
        public void ResetAspect() { }
    }

    public class AudioClip : Object
    {
        public float length { get; internal set; }
        public int samples { get; internal set; }
        public int channels { get; internal set; }
        public int frequency { get; internal set; }
        internal float[] data;
        public static AudioClip Create(string name, int lengthSamples, int channels, int frequency, bool stream)
        {
            return new AudioClip { name = name, samples = lengthSamples, channels = channels, frequency = frequency, length = frequency > 0 ? lengthSamples / (float)frequency : 0f };
        }
        public bool SetData(float[] samples, int offsetSamples) { data = (float[])samples.Clone(); return true; }
        public bool GetData(float[] samples, int offsetSamples) { if (data != null) Array.Copy(data, samples, Math.Min(data.Length, samples.Length)); return data != null; }
    }

    public class AudioSource : Behaviour
    {
        public AudioClip clip;
        public bool playOnAwake = true;
        public bool loop;
        public bool mute;
        public float volume = 1f;
        public float pitch = 1f;
        public float spatialBlend;
        public float minDistance = 1f;
        public float maxDistance = 500f;
        public float dopplerLevel = 1f;
        public AudioRolloffMode rolloffMode;
        public int priority = 128;
        /// <summary>Simulator bookkeeping: number of Play / PlayOneShot calls.</summary>
        [NonSerialized] public int SimPlayCount;
        public bool isPlaying { get; private set; }
        public float time { get; set; }
        public void Play() { SimPlayCount++; isPlaying = clip != null; }
        public void Play(ulong delay) { Play(); }
        public void PlayDelayed(float delay) { Play(); }
        public void PlayOneShot(AudioClip oneShot) { SimPlayCount++; }
        public void PlayOneShot(AudioClip oneShot, float volumeScale) { SimPlayCount++; }
        public void Stop() { isPlaying = false; }
        public void Pause() { isPlaying = false; }
        public void UnPause() { isPlaying = clip != null; }
        public static void PlayClipAtPoint(AudioClip clip, Vector3 position) { }
        public static void PlayClipAtPoint(AudioClip clip, Vector3 position, float volume) { }
    }

    public class AudioListener : Behaviour { public static float volume { get; set; } = 1f; public static bool pause { get; set; } }

    // ---------------------------------------------------------------- Animation (data only)
    public class Motion : Object { }
    public class AnimationClip : Motion
    {
        public float length = 1f;
        public float frameRate = 30f;
        public bool legacy;
        public WrapMode wrapMode;
        public bool isLooping => wrapMode == WrapMode.Loop;
    }
    public enum WrapMode { Default = 0, Once = 1, Clamp = 1, Loop = 2, PingPong = 4, ClampForever = 8 }
    public class RuntimeAnimatorController : Object { public AnimationClip[] animationClips = new AnimationClip[0]; }
    public class Avatar : Object { public bool isValid { get; internal set; } public bool isHuman { get; internal set; } }

    public class AnimatorControllerParameter
    {
        public string name { get; set; } = "";
        public AnimatorControllerParameterType type { get; set; }
        public float defaultFloat { get; set; }
        public int defaultInt { get; set; }
        public bool defaultBool { get; set; }
        public int nameHash => Animator.StringToHash(name);
    }

    public class Animator : Behaviour
    {
        private readonly Dictionary<int, float> floats = new Dictionary<int, float>();
        private readonly Dictionary<int, int> ints = new Dictionary<int, int>();
        private readonly Dictionary<int, bool> bools = new Dictionary<int, bool>();
        public RuntimeAnimatorController runtimeAnimatorController;
        public Avatar avatar;
        public bool applyRootMotion;
        public AnimatorCullingMode cullingMode;
        public AnimatorUpdateMode updateMode;
        public float speed = 1f;
        public bool isHuman => avatar != null && avatar.isHuman;
        public bool isInitialized => true;
        public Vector3 velocity => Vector3.zero;
        public Vector3 deltaPosition => Vector3.zero;

        public AnimatorControllerParameter[] parameters
        {
            get
            {
                if (runtimeAnimatorController is UnityEditor.Animations.AnimatorController controller) return controller.parameters;
                return new AnimatorControllerParameter[0];
            }
        }
        public int parameterCount => parameters.Length;

        public static int StringToHash(string name) => name == null ? 0 : name.GetHashCode();
        public void SetFloat(string name, float value) { floats[StringToHash(name)] = value; }
        public void SetFloat(int id, float value) { floats[id] = value; }
        public void SetFloat(string name, float value, float dampTime, float deltaTime) { floats[StringToHash(name)] = value; }
        public void SetFloat(int id, float value, float dampTime, float deltaTime) { floats[id] = value; }
        public float GetFloat(string name) => GetFloat(StringToHash(name));
        public float GetFloat(int id) => floats.TryGetValue(id, out float v) ? v : 0f;
        public void SetInteger(string name, int value) { ints[StringToHash(name)] = value; }
        public void SetInteger(int id, int value) { ints[id] = value; }
        public int GetInteger(string name) => ints.TryGetValue(StringToHash(name), out int v) ? v : 0;
        public void SetBool(string name, bool value) { bools[StringToHash(name)] = value; }
        public void SetBool(int id, bool value) { bools[id] = value; }
        public bool GetBool(string name) => bools.TryGetValue(StringToHash(name), out bool v) && v;
        public bool GetBool(int id) => bools.TryGetValue(id, out bool v) && v;
        public void SetTrigger(string name) { bools[StringToHash(name)] = true; }
        public void SetTrigger(int id) { bools[id] = true; }
        public void ResetTrigger(string name) { bools[StringToHash(name)] = false; }
        public void Play(string stateName) { }
        public void Play(string stateName, int layer) { }
        public void Play(string stateName, int layer, float normalizedTime) { }
        public void CrossFade(string stateName, float duration) { }
        public void Rebind() { }
        public void Update(float deltaTime) { }
        public Transform GetBoneTransform(HumanBodyBones bone) => null;
    }

    public enum HumanBodyBones { Hips = 0, LeftUpperLeg, RightUpperLeg, LeftLowerLeg, RightLowerLeg, LeftFoot, RightFoot, Spine, Chest, Neck, Head, LeftShoulder, RightShoulder, LeftUpperArm, RightUpperArm, LeftLowerArm, RightLowerArm, LeftHand, RightHand, LastBone = 55 }

    [Serializable] public struct HumanLimit { public bool useDefaultValues; public Vector3 min, max, center; public float axisLength; }
    [Serializable] public struct HumanBone { public string boneName; public string humanName; public HumanLimit limit; }
    [Serializable] public struct SkeletonBone { public string name; public Vector3 position; public Quaternion rotation; public Vector3 scale; }
    [Serializable]
    public struct HumanDescription
    {
        public HumanBone[] human;
        public SkeletonBone[] skeleton;
        public float upperArmTwist, lowerArmTwist, upperLegTwist, lowerLegTwist, armStretch, legStretch, feetSpacing;
        public bool hasTranslationDoF;
    }
}
