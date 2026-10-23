// ---------------------------------------------------------------------------
// Minimal stand-ins for the TextMeshPro, XR Interaction Toolkit, XR Core Utils
// and Input System APIs used by Module 2. Compile-check only; never imported by Unity.
// ---------------------------------------------------------------------------
using UnityEngine;
using UnityEngine.Events;

namespace TMPro
{
    public enum TextAlignmentOptions { Left, Center, Right, TopLeft, Top }
    public enum FontStyles { Normal = 0, Bold = 1 }
    public enum TextWrappingModes { NoWrap, Normal, PreserveWhitespace, PreserveWhitespaceNoWrap }
    public enum TextOverflowModes { Overflow, Ellipsis, Masking, Truncate }

    public class TMP_Settings : ScriptableObject { }

    public class TMP_Text : MonoBehaviour
    {
        public string text { get; set; } = "";
        public Color color;
        public TextAlignmentOptions alignment;
        public FontStyles fontStyle;
        public TextWrappingModes textWrappingMode;
        public TextOverflowModes overflowMode;
        public bool enableAutoSizing;
        public float fontSize, fontSizeMin, fontSizeMax;
        public RectTransform rectTransform => transform as RectTransform ?? new RectTransform();
        public void SetText(string sourceText) => text = sourceText;
        public void SetCharArray(char[] sourceText, int start, int length) => text = new string(sourceText, start, length);
    }

    public class TextMeshPro : TMP_Text { }
}

namespace Unity.XR.CoreUtils
{
    public class XROrigin : MonoBehaviour { }
}

namespace UnityEngine.XR.Interaction.Toolkit
{
    using UnityEngine.XR.Interaction.Toolkit.Interactors;

    public class XRInteractionManager : MonoBehaviour { }

    public class SelectEnterEventArgs
    {
        public IXRSelectInteractor interactorObject;
    }

    public class SelectEnterEvent : UnityEvent<SelectEnterEventArgs> { }

    namespace Interactors
    {
        public interface IXRInteractor { Transform transform { get; } }
        public interface IXRSelectInteractor : IXRInteractor { }
    }

    namespace Interactables
    {
        public class XRBaseInteractable : MonoBehaviour
        {
            public enum MovementType { VelocityTracking, Kinematic, Instantaneous }
            public SelectEnterEvent selectEntered = new SelectEnterEvent();
        }

        public class XRSimpleInteractable : XRBaseInteractable { }

        [RequireComponent(typeof(Rigidbody))]
        public class XRGrabInteractable : XRBaseInteractable
        {
            public bool useDynamicAttach;
            public bool throwOnDetach;
            public MovementType movementType;
        }
    }

    namespace Inputs.Haptics
    {
        public class HapticImpulsePlayer : MonoBehaviour
        {
            public bool SendHapticImpulse(float amplitude, float duration) => true;
            public bool SendHapticImpulse(float amplitude, float duration, float frequency) => true;
        }
    }

    namespace UI
    {
        public class XRUIInputModule : UnityEngine.EventSystems.BaseInputModule { }
    }
}

namespace UnityEngine.InputSystem
{
    public enum Key { None, Space, Digit1, Digit2, F1, F2, F3, F4, F5, F6, F7, F8, F9, F10, F11, F12 }

    namespace Controls
    {
        public class ButtonControl { public bool wasPressedThisFrame; }
        public class KeyControl : ButtonControl { }
        public class Vector2Control { public Vector2 ReadValue() => Vector2.zero; }
    }

    public class Keyboard
    {
        public static Keyboard current;
        public Controls.KeyControl this[Key key] => new Controls.KeyControl();
    }

    public class Mouse
    {
        public static Mouse current;
        public Controls.ButtonControl leftButton = new Controls.ButtonControl();
        public Controls.Vector2Control position = new Controls.Vector2Control();
    }
}
