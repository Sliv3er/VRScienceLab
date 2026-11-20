// HeadlessSim: UGUI, EventSystem and TextMeshPro as data (no rendering, no layout beyond RectTransform anchors).
// Not part of the Unity project.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using HeadlessSim;
using UnityEngine.Events;
using UnityEngine.EventSystems;

namespace UnityEngine
{
    public enum RenderMode { ScreenSpaceOverlay, ScreenSpaceCamera, WorldSpace }
    [Flags] public enum AdditionalCanvasShaderChannels { None = 0, TexCoord1 = 1, TexCoord2 = 2, TexCoord3 = 4, Normal = 8, Tangent = 16 }

    [RequireComponent(typeof(RectTransform))]
    public class Canvas : Behaviour
    {
        public RenderMode renderMode;
        public Camera worldCamera;
        public AdditionalCanvasShaderChannels additionalShaderChannels;
        public int sortingOrder;
        public string sortingLayerName = "Default";
        public float scaleFactor = 1f;
        public float referencePixelsPerUnit = 100f;
        public bool pixelPerfect;
        public bool overrideSorting;
        public float planeDistance = 100f;
        public bool isRootCanvas => transform.parent == null || transform.parent.GetComponentInParent<Canvas>(true) == null;
        public Canvas rootCanvas { get { Canvas root = this; for (Transform t = transform.parent; t != null; t = t.parent) { Canvas c = t.GetComponent<Canvas>(); if (c != null) root = c; } return root; } }
        public static void ForceUpdateCanvases() { }
    }

    public class CanvasRenderer : Component
    {
        public bool cullTransparentMesh = true;
        public void SetAlpha(float alpha) { }
        public void SetColor(Color color) { }
    }

    public class CanvasGroup : Behaviour
    {
        public float alpha = 1f;
        public bool interactable = true;
        public bool blocksRaycasts = true;
        public bool ignoreParentGroups;
    }
}

namespace UnityEngine.EventSystems
{
    public interface IEventSystemHandler { }
    public interface IPointerClickHandler : IEventSystemHandler { void OnPointerClick(PointerEventData eventData); }
    public interface IPointerDownHandler : IEventSystemHandler { void OnPointerDown(PointerEventData eventData); }
    public interface IPointerUpHandler : IEventSystemHandler { void OnPointerUp(PointerEventData eventData); }
    public interface IPointerEnterHandler : IEventSystemHandler { void OnPointerEnter(PointerEventData eventData); }
    public interface IPointerExitHandler : IEventSystemHandler { void OnPointerExit(PointerEventData eventData); }
    public interface ISubmitHandler : IEventSystemHandler { void OnSubmit(BaseEventData eventData); }
    public interface ISelectHandler : IEventSystemHandler { void OnSelect(BaseEventData eventData); }
    public interface IDeselectHandler : IEventSystemHandler { void OnDeselect(BaseEventData eventData); }

    public abstract class UIBehaviour : MonoBehaviour
    {
        protected virtual void Awake() { }
        protected virtual void OnEnable() { }
        protected virtual void Start() { }
        protected virtual void OnDisable() { }
        protected virtual void OnDestroy() { }
        public virtual bool IsActive() => isActiveAndEnabled;
        public bool IsDestroyed() => this == null;
    }

    public class BaseEventData
    {
        private bool used;
        public EventSystem currentInputModuleEventSystem { get; }
        public GameObject selectedObject { get; set; }
        public BaseEventData(EventSystem eventSystem) { currentInputModuleEventSystem = eventSystem; }
        public bool used_ => used;
        public void Use() { used = true; }
        public void Reset() { used = false; }
    }

    public struct RaycastResult
    {
        public GameObject gameObject { get; set; }
        public BaseRaycaster module;
        public float distance;
        public float index;
        public int depth;
        public int sortingLayer;
        public int sortingOrder;
        public Vector3 worldPosition;
        public Vector3 worldNormal;
        public Vector2 screenPosition;
        public int displayIndex;
        public bool isValid => module != null && gameObject != null;
        public void Clear() { gameObject = null; module = null; distance = 0f; index = 0f; depth = 0; }
    }

    public class PointerEventData : BaseEventData
    {
        public enum InputButton { Left, Right, Middle }
        public enum FramePressState { Pressed, Released, PressedAndReleased, NotChanged }
        public PointerEventData(EventSystem eventSystem) : base(eventSystem) { }
        public InputButton button { get; set; } = InputButton.Left;
        public Vector2 position { get; set; }
        public Vector2 delta { get; set; }
        public Vector2 pressPosition { get; set; }
        public Vector2 scrollDelta { get; set; }
        public int pointerId { get; set; } = -1;
        public int clickCount { get; set; } = 1;
        public float clickTime { get; set; }
        public bool dragging { get; set; }
        public bool eligibleForClick { get; set; } = true;
        public GameObject pointerEnter { get; set; }
        public GameObject pointerPress { get; set; }
        public GameObject pointerClick { get; set; }
        public GameObject pointerDrag { get; set; }
        public GameObject rawPointerPress { get; set; }
        public RaycastResult pointerCurrentRaycast { get; set; }
        public RaycastResult pointerPressRaycast { get; set; }
        public Camera enterEventCamera => Camera.main;
        public Camera pressEventCamera => Camera.main;
    }

    public class EventSystem : UIBehaviour
    {
        public static EventSystem current
        {
            get
            {
                foreach (EventSystem system in Simulation.FindAll<EventSystem>(false))
                {
                    if (system.enabled) return system;
                }
                return null;
            }
            set { }
        }
        public bool sendNavigationEvents = true;
        public int pixelDragThreshold = 10;
        public GameObject firstSelectedGameObject;
        public GameObject currentSelectedGameObject { get; private set; }
        public BaseInputModule currentInputModule => GetComponent<BaseInputModule>();
        public bool alreadySelecting => false;
        public bool isFocused => true;
        public void SetSelectedGameObject(GameObject selected) { currentSelectedGameObject = selected; }
        public void SetSelectedGameObject(GameObject selected, BaseEventData pointer) { currentSelectedGameObject = selected; }
        public bool IsPointerOverGameObject() => false;
        public bool IsPointerOverGameObject(int pointerId) => false;
        public void RaycastAll(PointerEventData eventData, List<RaycastResult> raycastResults) { raycastResults.Clear(); }
        public void UpdateModules() { }
    }

    [RequireComponent(typeof(EventSystem))]
    public abstract class BaseInputModule : UIBehaviour
    {
        public EventSystem eventSystem => GetComponent<EventSystem>();
        public abstract void Process();
        public virtual void UpdateModule() { }
        public virtual bool IsModuleSupported() => true;
        public virtual bool ShouldActivateModule() => enabled && gameObject.activeInHierarchy;
        public virtual void ActivateModule() { }
        public virtual void DeactivateModule() { }
        public virtual bool IsPointerOverGameObject(int pointerId) => false;
    }

    public abstract class PointerInputModule : BaseInputModule { }
    public class StandaloneInputModule : PointerInputModule { public override void Process() { } }

    public abstract class BaseRaycaster : UIBehaviour
    {
        public abstract Camera eventCamera { get; }
        public abstract void Raycast(PointerEventData eventData, List<RaycastResult> resultAppendList);
        public virtual int sortOrderPriority => int.MinValue;
        public virtual int renderOrderPriority => int.MinValue;
        public BaseRaycaster rootRaycaster => this;
    }

    public class PhysicsRaycaster : BaseRaycaster
    {
        public override Camera eventCamera => GetComponent<Camera>();
        public override void Raycast(PointerEventData eventData, List<RaycastResult> resultAppendList) { }
    }

    public static class ExecuteEvents
    {
        public delegate void EventFunction<T1>(T1 handler, BaseEventData eventData);

        public static EventFunction<IPointerClickHandler> pointerClickHandler { get; } = (handler, data) => handler.OnPointerClick(ValidateEventData<PointerEventData>(data));
        public static EventFunction<IPointerDownHandler> pointerDownHandler { get; } = (handler, data) => handler.OnPointerDown(ValidateEventData<PointerEventData>(data));
        public static EventFunction<IPointerUpHandler> pointerUpHandler { get; } = (handler, data) => handler.OnPointerUp(ValidateEventData<PointerEventData>(data));
        public static EventFunction<IPointerEnterHandler> pointerEnterHandler { get; } = (handler, data) => handler.OnPointerEnter(ValidateEventData<PointerEventData>(data));
        public static EventFunction<IPointerExitHandler> pointerExitHandler { get; } = (handler, data) => handler.OnPointerExit(ValidateEventData<PointerEventData>(data));
        public static EventFunction<ISubmitHandler> submitHandler { get; } = (handler, data) => handler.OnSubmit(data);
        public static EventFunction<ISelectHandler> selectHandler { get; } = (handler, data) => handler.OnSelect(data);
        public static EventFunction<IDeselectHandler> deselectHandler { get; } = (handler, data) => handler.OnDeselect(data);

        public static T ValidateEventData<T>(BaseEventData data) where T : class
        {
            if (!(data is T)) throw new ArgumentException($"Invalid type: {data?.GetType()} passed to event expecting {typeof(T)}");
            return data as T;
        }

        /// <summary>Sends the event to every enabled handler of type T on <paramref name="target"/> (active objects only).</summary>
        public static bool Execute<T>(GameObject target, BaseEventData eventData, EventFunction<T> functor) where T : IEventSystemHandler
        {
            if (target == null || !target.activeInHierarchy) return false;
            int handled = 0;
            foreach (Component component in target.components.ToArray())
            {
                if (!(component is T handler)) continue;
                if (component is Behaviour behaviour && !behaviour.isActiveAndEnabled) continue;
                try { functor(handler, eventData); }
                catch (Exception exception) { Simulation.ReportException(exception, component, typeof(T).Name); }
                handled++;
            }
            return handled > 0;
        }

        public static GameObject ExecuteHierarchy<T>(GameObject root, BaseEventData eventData, EventFunction<T> callbackFunction) where T : IEventSystemHandler
        {
            for (Transform t = root != null ? root.transform : null; t != null; t = t.parent)
            {
                if (Execute(t.gameObject, eventData, callbackFunction)) return t.gameObject;
            }
            return null;
        }

        public static bool CanHandleEvent<T>(GameObject go) where T : IEventSystemHandler => go != null && go.GetComponent<T>() != null;
        public static GameObject GetEventHandler<T>(GameObject root) where T : IEventSystemHandler
        {
            for (Transform t = root != null ? root.transform : null; t != null; t = t.parent)
            {
                if (CanHandleEvent<T>(t.gameObject)) return t.gameObject;
            }
            return null;
        }
    }
}

namespace UnityEngine.UI
{
    [RequireComponent(typeof(RectTransform))]
    [RequireComponent(typeof(CanvasRenderer))]
    public abstract class Graphic : UIBehaviour
    {
        [SerializeField] private Color m_Color = Color.white;
        [SerializeField] private bool m_RaycastTarget = true;
        [SerializeField] private Material m_Material;
        public Vector4 raycastPadding;

        public virtual Color color { get => m_Color; set => m_Color = value; }
        public virtual bool raycastTarget { get => m_RaycastTarget; set => m_RaycastTarget = value; }
        public virtual Material material { get => m_Material; set => m_Material = value; }
        public virtual Material defaultMaterial => null;
        public virtual Material materialForRendering => m_Material;
        public virtual Texture mainTexture => null;
        public RectTransform rectTransform => transform as RectTransform;
        public Canvas canvas => GetComponentInParent<Canvas>(true);
        public CanvasRenderer canvasRenderer => GetComponent<CanvasRenderer>();
        public int depth => 0;
        public virtual void SetAllDirty() { }
        public virtual void SetLayoutDirty() { }
        public virtual void SetVerticesDirty() { }
        public virtual void SetMaterialDirty() { }
        public virtual void SetNativeSize() { }
        public virtual void CrossFadeColor(Color targetColor, float duration, bool ignoreTimeScale, bool useAlpha) { }
        public virtual void CrossFadeAlpha(float alpha, float duration, bool ignoreTimeScale) { }
        public virtual bool Raycast(Vector2 sp, Camera eventCamera) => raycastTarget && isActiveAndEnabled;
    }

    public abstract class MaskableGraphic : Graphic
    {
        public bool maskable = true;
        public bool isMaskingGraphic;
    }

    public class Image : MaskableGraphic
    {
        public enum Type { Simple, Sliced, Tiled, Filled }
        public enum FillMethod { Horizontal, Vertical, Radial90, Radial180, Radial360 }
        public Sprite sprite;
        public Sprite overrideSprite { get => sprite; set => sprite = value; }
        public Type type;
        public FillMethod fillMethod;
        public float fillAmount = 1f;
        public bool fillCenter = true;
        public bool fillClockwise = true;
        public int fillOrigin;
        public bool preserveAspect;
        public float pixelsPerUnitMultiplier = 1f;
        public bool useSpriteMesh;
        public float alphaHitTestMinimumThreshold;
        public float pixelsPerUnit => 100f;
    }

    public class RawImage : MaskableGraphic
    {
        public Texture texture;
        public Rect uvRect = new Rect(0f, 0f, 1f, 1f);
    }

    public class Text : MaskableGraphic
    {
        public string text = "";
        public int fontSize = 14;
        public TextAnchor alignment;
        public FontStyle fontStyle;
        public bool supportRichText = true;
    }

    [Serializable]
    public struct ColorBlock
    {
        public Color normalColor, highlightedColor, pressedColor, selectedColor, disabledColor;
        public float colorMultiplier, fadeDuration;
        public static ColorBlock defaultColorBlock => new ColorBlock
        {
            normalColor = Color.white, highlightedColor = new Color(0.96f, 0.96f, 0.96f, 1f), pressedColor = new Color(0.78f, 0.78f, 0.78f, 1f),
            selectedColor = new Color(0.96f, 0.96f, 0.96f, 1f), disabledColor = new Color(0.78f, 0.78f, 0.78f, 0.5f), colorMultiplier = 1f, fadeDuration = 0.1f,
        };
    }

    [Serializable]
    public struct Navigation
    {
        public enum Mode { None = 0, Horizontal = 1, Vertical = 2, Automatic = 3, Explicit = 4 }
        public Mode mode;
        public bool wrapAround;
        public Selectable selectOnUp, selectOnDown, selectOnLeft, selectOnRight;
        public static Navigation defaultNavigation => new Navigation { mode = Mode.Automatic };
    }

    public class Selectable : UIBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
    {
        public enum Transition { None, ColorTint, SpriteSwap, Animation }
        [SerializeField] private bool m_Interactable = true;
        [SerializeField] private ColorBlock m_Colors = ColorBlock.defaultColorBlock;
        [SerializeField] private Navigation m_Navigation = Navigation.defaultNavigation;
        public Graphic targetGraphic;
        public Transition transition = Transition.ColorTint;

        public bool interactable { get => m_Interactable; set => m_Interactable = value; }
        public ColorBlock colors { get => m_Colors; set => m_Colors = value; }
        public Navigation navigation { get => m_Navigation; set => m_Navigation = value; }
        public Image image { get => targetGraphic as Image; set => targetGraphic = value; }

        /// <summary>Interactable, and not disabled by a parent CanvasGroup.</summary>
        public virtual bool IsInteractable()
        {
            if (!m_Interactable) return false;
            for (Transform t = transform; t != null; t = t.parent)
            {
                CanvasGroup group = t.GetComponent<CanvasGroup>();
                if (group == null || !group.enabled) continue;
                if (!group.interactable) return false;
                if (group.ignoreParentGroups) break;
            }
            return true;
        }

        public virtual void OnPointerDown(PointerEventData eventData) { }
        public virtual void OnPointerUp(PointerEventData eventData) { }
        public virtual void OnPointerEnter(PointerEventData eventData) { }
        public virtual void OnPointerExit(PointerEventData eventData) { }
        public virtual void OnSelect(BaseEventData eventData) { }
        public virtual void OnDeselect(BaseEventData eventData) { }
        public virtual void Select() { EventSystem system = EventSystem.current; if (system != null) system.SetSelectedGameObject(gameObject); }
    }

    public class Button : Selectable, IPointerClickHandler, ISubmitHandler
    {
        [Serializable] public class ButtonClickedEvent : UnityEvent { }
        [SerializeField] private ButtonClickedEvent m_OnClick = new ButtonClickedEvent();
        public ButtonClickedEvent onClick { get => m_OnClick; set => m_OnClick = value; }

        private void Press()
        {
            if (!IsActive() || !IsInteractable()) return;
            m_OnClick.Invoke();
        }

        public virtual void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            Press();
        }

        public virtual void OnSubmit(BaseEventData eventData) { Press(); }
    }

    public class Toggle : Selectable, IPointerClickHandler
    {
        [Serializable] public class ToggleEvent : UnityEvent<bool> { }
        public ToggleEvent onValueChanged = new ToggleEvent();
        [SerializeField] private bool m_IsOn;
        public Graphic graphic;
        public bool isOn { get => m_IsOn; set { if (m_IsOn == value) return; m_IsOn = value; onValueChanged.Invoke(value); } }
        public void SetIsOnWithoutNotify(bool value) { m_IsOn = value; }
        public virtual void OnPointerClick(PointerEventData eventData) { if (IsActive() && IsInteractable()) isOn = !isOn; }
    }

    public class Slider : Selectable
    {
        [Serializable] public class SliderEvent : UnityEvent<float> { }
        public SliderEvent onValueChanged = new SliderEvent();
        public float minValue, maxValue = 1f;
        public bool wholeNumbers;
        [SerializeField] private float m_Value;
        public RectTransform fillRect, handleRect;
        public float value { get => m_Value; set { float clamped = Mathf.Clamp(value, minValue, maxValue); if (clamped == m_Value) return; m_Value = clamped; onValueChanged.Invoke(clamped); } }
        public float normalizedValue { get => Mathf.InverseLerp(minValue, maxValue, m_Value); set => this.value = Mathf.Lerp(minValue, maxValue, value); }
        public void SetValueWithoutNotify(float input) { m_Value = Mathf.Clamp(input, minValue, maxValue); }
    }

    [RequireComponent(typeof(Canvas))]
    public class CanvasScaler : UIBehaviour
    {
        public enum ScaleMode { ConstantPixelSize, ScaleWithScreenSize, ConstantPhysicalSize }
        public enum ScreenMatchMode { MatchWidthOrHeight, Expand, Shrink }
        public ScaleMode uiScaleMode;
        public float referencePixelsPerUnit = 100f;
        public float scaleFactor = 1f;
        public Vector2 referenceResolution = new Vector2(800f, 600f);
        public ScreenMatchMode screenMatchMode;
        public float matchWidthOrHeight;
        public float dynamicPixelsPerUnit = 1f;
    }

    [RequireComponent(typeof(Canvas))]
    public class GraphicRaycaster : BaseRaycaster
    {
        public enum BlockingObjects { None, TwoD, ThreeD, All }
        public bool ignoreReversedGraphics = true;
        public BlockingObjects blockingObjects;
        public LayerMask blockingMask = -1;
        public override Camera eventCamera
        {
            get
            {
                Canvas canvas = GetComponent<Canvas>();
                if (canvas == null) return null;
                if (canvas.renderMode == RenderMode.ScreenSpaceOverlay) return null;
                return canvas.worldCamera != null ? canvas.worldCamera : Camera.main;
            }
        }
        public override void Raycast(PointerEventData eventData, List<RaycastResult> resultAppendList) { }
    }

    public abstract class LayoutGroup : UIBehaviour { public RectOffset padding; public TextAnchor childAlignment; }
    public class LayoutElement : UIBehaviour { public float minWidth = -1, minHeight = -1, preferredWidth = -1, preferredHeight = -1, flexibleWidth = -1, flexibleHeight = -1; public bool ignoreLayout; }
    public static class LayoutRebuilder { public static void ForceRebuildLayoutImmediate(RectTransform layoutRoot) { } public static void MarkLayoutForRebuild(RectTransform rect) { } }
}

namespace UnityEngine
{
    public enum TextAnchor { UpperLeft, UpperCenter, UpperRight, MiddleLeft, MiddleCenter, MiddleRight, LowerLeft, LowerCenter, LowerRight }
    public enum FontStyle { Normal, Bold, Italic, BoldAndItalic }
}

namespace TMPro
{
    using UnityEngine;
    using UnityEngine.UI;

    public enum TextAlignmentOptions
    {
        TopLeft = 257, Top = 258, TopRight = 260, TopJustified = 264, TopFlush = 272, TopGeoAligned = 288,
        Left = 513, Center = 514, Right = 516, Justified = 520, Flush = 528, CenterGeoAligned = 544,
        BottomLeft = 1025, Bottom = 1026, BottomRight = 1028, BottomJustified = 1032, BottomFlush = 1040, BottomGeoAligned = 1056,
        BaselineLeft = 2049, Baseline = 2050, BaselineRight = 2052, BaselineJustified = 2056, BaselineFlush = 2064, BaselineGeoAligned = 2080,
        MidlineLeft = 4097, Midline = 4098, MidlineRight = 4100, MidlineJustified = 4104, MidlineFlush = 4112, MidlineGeoAligned = 4128,
        CaplineLeft = 8193, Capline = 8194, CaplineRight = 8196, CaplineJustified = 8200, CaplineFlush = 8208, CaplineGeoAligned = 8224,
        Converted = 65535,
    }
    public enum HorizontalAlignmentOptions { Left = 1, Center = 2, Right = 4, Justified = 8, Flush = 16, Geometry = 32 }
    public enum VerticalAlignmentOptions { Top = 256, Middle = 512, Bottom = 1024, Baseline = 2048, Geometry = 4096, Capline = 8192 }
    [Flags] public enum FontStyles { Normal = 0, Bold = 1, Italic = 2, Underline = 4, LowerCase = 8, UpperCase = 16, SmallCaps = 32, Strikethrough = 64, Superscript = 128, Subscript = 256, Highlight = 512 }
    public enum TextWrappingModes { NoWrap = 0, Normal = 1, PreserveWhitespace = 2, PreserveWhitespaceNoWrap = 3 }
    public enum TextOverflowModes { Overflow = 0, Ellipsis = 1, Masking = 2, Truncate = 3, ScrollRect = 4, Page = 5, Linked = 6 }
    public enum FontWeight { Thin = 100, ExtraLight = 200, Light = 300, Regular = 400, Medium = 500, SemiBold = 600, Bold = 700, Heavy = 800, Black = 900 }

    public class TMP_Asset : ScriptableObject { public Material material; }
    public class TMP_FontAsset : TMP_Asset { public bool HasCharacter(char character) => true; public bool HasCharacter(int character) => true; }
    public class TMP_SpriteAsset : TMP_Asset { }

    public class TMP_Settings : ScriptableObject
    {
        internal static TMP_Settings loaded;
        public static TMP_Settings instance => loaded ?? (loaded = Resources.Load<TMP_Settings>("TMP Settings"));
        public static TMP_FontAsset defaultFontAsset { get; set; } = CreateDefaultFont();
        private static TMP_FontAsset CreateDefaultFont() { var font = CreateInstance<TMP_FontAsset>(); font.name = "LiberationSans SDF"; return font; }
    }

    public class TMP_TextInfo
    {
        public int characterCount, wordCount, lineCount, pageCount;
    }

    public abstract class TMP_Text : MaskableGraphic
    {
        [SerializeField] private string m_text = "";
        public TMP_FontAsset font = TMP_Settings.defaultFontAsset;
        public Material fontSharedMaterial;
        public float fontSize = 36f;
        public float fontSizeMin = 18f, fontSizeMax = 72f;
        public bool enableAutoSizing;
        public TextAlignmentOptions alignment = TextAlignmentOptions.TopLeft;
        public FontStyles fontStyle;
        public FontWeight fontWeight = FontWeight.Regular;
        public TextWrappingModes textWrappingMode = TextWrappingModes.Normal;
        public TextOverflowModes overflowMode;
        public bool richText = true;
        public float characterSpacing, wordSpacing, lineSpacing, paragraphSpacing;
        public Vector4 margin;
        public float outlineWidth;
        public Color32 outlineColor;
        public Color faceColor = Color.white;
        public bool enableVertexGradient;
        public bool extraPadding;
        public bool isTextObjectScaleStatic;
        public int maxVisibleCharacters = 99999;
        public int maxVisibleLines = 99999;
        public bool parseCtrlCharacters = true;
        public bool isOrthographic;
        public bool enableCulling;
        public float alpha { get => color.a; set { Color c = color; c.a = value; color = c; } }

        public virtual string text { get => m_text; set => m_text = value ?? ""; }
        public HorizontalAlignmentOptions horizontalAlignment { get => (HorizontalAlignmentOptions)((int)alignment & 0xFF); set => alignment = (TextAlignmentOptions)(((int)alignment & 0xFF00) | (int)value); }
        public VerticalAlignmentOptions verticalAlignment { get => (VerticalAlignmentOptions)((int)alignment & 0xFF00); set => alignment = (TextAlignmentOptions)(((int)alignment & 0xFF) | (int)value); }
        [Obsolete("Use textWrappingMode")] public bool enableWordWrapping { get => textWrappingMode == TextWrappingModes.Normal; set => textWrappingMode = value ? TextWrappingModes.Normal : TextWrappingModes.NoWrap; }
        public new RectTransform rectTransform
        {
            get
            {
                if (transform is RectTransform rect) return rect;
                return gameObject.ConvertToRectTransform();
            }
        }
        public TMP_TextInfo textInfo => new TMP_TextInfo { characterCount = m_text.Length, lineCount = m_text.Split('\n').Length };
        public float preferredWidth => m_text.Length * fontSize * 0.5f;
        public float preferredHeight => fontSize * 1.2f * m_text.Split('\n').Length;
        public Bounds bounds => new Bounds(Vector3.zero, new Vector3(preferredWidth, preferredHeight, 0f));
        public bool havePropertiesChanged { get; set; }

        public void SetText(string sourceText) { text = sourceText; }
        [Obsolete] public void SetText(string sourceText, bool syncTextInputBox) { text = sourceText; }
        public void SetText(string sourceText, float arg0) { text = Format(sourceText, arg0); }
        public void SetText(string sourceText, float arg0, float arg1) { text = Format(sourceText, arg0, arg1); }
        public void SetText(string sourceText, float arg0, float arg1, float arg2) { text = Format(sourceText, arg0, arg1, arg2); }
        public void SetText(string sourceText, float arg0, float arg1, float arg2, float arg3) { text = Format(sourceText, arg0, arg1, arg2, arg3); }
        public void SetText(string sourceText, float arg0, float arg1, float arg2, float arg3, float arg4) { text = Format(sourceText, arg0, arg1, arg2, arg3, arg4); }
        public void SetText(string sourceText, float arg0, float arg1, float arg2, float arg3, float arg4, float arg5) { text = Format(sourceText, arg0, arg1, arg2, arg3, arg4, arg5); }
        public void SetText(StringBuilder sourceText) { text = sourceText != null ? sourceText.ToString() : ""; }
        public void SetText(char[] sourceText) { SetCharArray(sourceText); }
        public void SetText(char[] sourceText, int start, int length) { SetCharArray(sourceText, start, length); }
        public void SetCharArray(char[] sourceText) { text = sourceText != null ? new string(sourceText).TrimEnd('\0') : ""; }
        public void SetCharArray(char[] sourceText, int start, int length)
        {
            if (sourceText == null) { text = ""; return; }
            if (start < 0 || length < 0 || start + length > sourceText.Length) throw new ArgumentOutOfRangeException(nameof(length), "SetCharArray range is outside the array.");
            text = new string(sourceText, start, length);
        }
        public string GetParsedText() => m_text;
        public virtual void ForceMeshUpdate(bool ignoreActiveState = false, bool forceTextReparsing = false) { }
        public Vector2 GetPreferredValues() => new Vector2(preferredWidth, preferredHeight);
        public Vector2 GetPreferredValues(string value) => new Vector2((value ?? "").Length * fontSize * 0.5f, fontSize * 1.2f);

        // TextMeshPro numeric format: {index} or {index:decimals}.
        internal static string Format(string pattern, params float[] args)
        {
            if (string.IsNullOrEmpty(pattern)) return "";
            var builder = new StringBuilder();
            for (int i = 0; i < pattern.Length; i++)
            {
                char c = pattern[i];
                int close = c == '{' ? pattern.IndexOf('}', i) : -1;
                if (close > i)
                {
                    string body = pattern.Substring(i + 1, close - i - 1);
                    string[] parts = body.Split(':');
                    if (int.TryParse(parts[0], out int index) && index >= 0 && index < args.Length)
                    {
                        int decimals = -1;
                        if (parts.Length > 1) int.TryParse(parts[1], out decimals);
                        builder.Append(decimals >= 0
                            ? Math.Round(args[index], decimals, MidpointRounding.AwayFromZero).ToString("F" + decimals, CultureInfo.InvariantCulture)
                            : args[index].ToString("0.##", CultureInfo.InvariantCulture));
                        i = close;
                        continue;
                    }
                }
                builder.Append(c);
            }
            return builder.ToString();
        }
    }

    [RequireComponent(typeof(MeshRenderer))]
    public class TextMeshPro : TMP_Text
    {
        public int sortingOrder;
        public int sortingLayerID;
        public MeshRenderer renderer => GetComponent<MeshRenderer>();
        public MeshFilter meshFilter => GetComponent<MeshFilter>();
    }

    public class TextMeshProUGUI : TMP_Text
    {
        public bool autoSizeTextContainer;
    }

    public class TMP_InputField : Selectable
    {
        public TMP_Text textComponent;
        [SerializeField] private string m_Text = "";
        public string text { get => m_Text; set => m_Text = value ?? ""; }
    }
}
