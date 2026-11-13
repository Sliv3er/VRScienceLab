// Stand-ins for packages whose sources are not bundled with the editor (XRI, Input System, XR Core Utils, AI Navigation).
using UnityEngine;
using UnityEngine.Events;

namespace Unity.XR.CoreUtils { public class XROrigin : MonoBehaviour { } }

namespace Unity.AI.Navigation
{
    public enum CollectObjects { All, Volume, Children, MarkedWithModifier }
    public class NavMeshSurface : MonoBehaviour
    {
        public CollectObjects collectObjects; public UnityEngine.AI.NavMeshCollectGeometry useGeometry; public LayerMask layerMask;
        public bool overrideVoxelSize; public float voxelSize; public float minRegionArea; public UnityEngine.AI.NavMeshData navMeshData { get; set; }
        public void BuildNavMesh() { }
    }
    public class NavMeshModifier : MonoBehaviour { public bool overrideArea; public int area; public bool ignoreFromBuild; }
}

namespace UnityEngine.XR.Interaction.Toolkit
{
    using UnityEngine.XR.Interaction.Toolkit.Interactors;
    public class XRInteractionManager : MonoBehaviour { }
    public class SelectEnterEventArgs { public IXRSelectInteractor interactorObject; }
    public class SelectEnterEvent : UnityEvent<SelectEnterEventArgs> { }
    public class HoverEnterEventArgs { }
    public class HoverExitEventArgs { }
    public class HoverEnterEvent : UnityEvent<HoverEnterEventArgs> { }
    public class HoverExitEvent : UnityEvent<HoverExitEventArgs> { }
    namespace Interactors { public interface IXRInteractor { Transform transform { get; } } public interface IXRSelectInteractor : IXRInteractor { } }
    namespace Interactables
    {
        public class XRBaseInteractable : MonoBehaviour
        {
            public enum MovementType { VelocityTracking, Kinematic, Instantaneous }
            public SelectEnterEvent selectEntered = new SelectEnterEvent();
            public HoverEnterEvent hoverEntered = new HoverEnterEvent();
            public HoverExitEvent hoverExited = new HoverExitEvent();
        }
        public class XRSimpleInteractable : XRBaseInteractable { }
        [RequireComponent(typeof(Rigidbody))]
        public class XRGrabInteractable : XRBaseInteractable { public bool useDynamicAttach; public bool throwOnDetach; public MovementType movementType; }
    }
    namespace Inputs.Haptics { public class HapticImpulsePlayer : MonoBehaviour { public bool SendHapticImpulse(float amplitude, float duration) => true; } }
    namespace UI
    {
        public class XRUIInputModule : UnityEngine.EventSystems.BaseInputModule { public override void Process() { } }
        public class TrackedDeviceGraphicRaycaster : UnityEngine.EventSystems.BaseRaycaster
        {
            public override Camera eventCamera => null;
            public override void Raycast(UnityEngine.EventSystems.PointerEventData eventData, System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult> resultAppendList) { }
        }
    }
}

namespace UnityEngine.InputSystem
{
    public enum Key { None, Space, Enter, Tab, LeftBracket = 13, RightBracket = 14, A = 15, B, C, D, E, F, G, H, I, J, K, L, M, N, O, P, Q, R, S, T, U, V, W, X, Y, Z,
        Digit1, Digit2, Digit3, Digit4, Digit5, Digit6, Digit7, Digit8, Digit9, Digit0, Delete = 71, Home = 68, PageDown = 66, PageUp = 67, F1 = 94, F2, F3, F4, F5, F6, F7, F8, F9, F10, F11, F12 }
    namespace Controls
    {
        public class ButtonControl { public bool wasPressedThisFrame; public bool isPressed; }
        public class KeyControl : ButtonControl { }
        public class AnyKeyControl : ButtonControl { }
        public class Vector2Control { public Vector2 ReadValue() => Vector2.zero; }
    }
    public class Keyboard { public static Keyboard current; public Controls.KeyControl this[Key key] => new Controls.KeyControl(); public Controls.AnyKeyControl anyKey = new Controls.AnyKeyControl(); }
    public class Mouse { public static Mouse current; public Controls.ButtonControl leftButton = new Controls.ButtonControl(); public Controls.Vector2Control position = new Controls.Vector2Control(); }
}
