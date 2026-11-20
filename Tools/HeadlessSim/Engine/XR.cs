// HeadlessSim: XR Interaction Toolkit 3.x, XR Core Utils and Input System stand-ins.
// Interactables and sockets carry real selection state so interaction logic can be exercised with
// Simulation.Grab / Release / Select. Not part of the Unity project.
using System;
using System.Collections.Generic;
using System.Linq;
using HeadlessSim;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace Unity.XR.CoreUtils
{
    public class XROrigin : MonoBehaviour
    {
        public enum TrackingOriginMode { NotSpecified, Device, Floor }
        public Camera Camera;
        public GameObject Origin;
        public GameObject CameraFloorOffsetObject;
        public TrackingOriginMode RequestedTrackingOriginMode;
        public float CameraYOffset = 1.1176f;
        public Vector3 CameraInOriginSpacePos => Camera != null ? transform.InverseTransformPoint(Camera.transform.position) : Vector3.zero;
        public bool MoveCameraToWorldLocation(Vector3 desiredWorldLocation)
        {
            if (Camera == null) return false;
            transform.position += desiredWorldLocation - Camera.transform.position;
            return true;
        }
    }
}

namespace UnityEngine.XR.Interaction.Toolkit
{
    [Serializable]
    public struct InteractionLayerMask
    {
        [SerializeField] private uint m_Bits;
        public int value { get => (int)m_Bits; set => m_Bits = (uint)value; }
        public static implicit operator int(InteractionLayerMask mask) => (int)mask.m_Bits;
        public static implicit operator InteractionLayerMask(int value) => new InteractionLayerMask { m_Bits = (uint)value };
        public static int GetMask(params string[] layerNames)
        {
            int mask = 0;
            foreach (string layerName in layerNames) { int layer = NameToLayer(layerName); if (layer >= 0) mask |= 1 << layer; }
            return mask;
        }
        public static int NameToLayer(string layerName) => layerName == "Default" ? 0 : (layerName == "Teleport" ? 31 : -1);
        public static string LayerToName(int layer) => layer == 0 ? "Default" : (layer == 31 ? "Teleport" : "");
    }

    public enum InteractableSelectMode { Single, Multiple }
    public enum InteractorHandedness { None, Left, Right }

    public abstract class BaseInteractionEventArgs
    {
        public IXRInteractor interactorObject { get; set; }
        public IXRInteractable interactableObject { get; set; }
    }
    public class HoverEnterEventArgs : BaseInteractionEventArgs { public XRInteractionManager manager { get; set; } }
    public class HoverExitEventArgs : BaseInteractionEventArgs { public XRInteractionManager manager { get; set; } public bool isCanceled { get; set; } }
    public class SelectEnterEventArgs : BaseInteractionEventArgs
    {
        public new IXRSelectInteractor interactorObject { get => (IXRSelectInteractor)base.interactorObject; set => base.interactorObject = value; }
        public new IXRSelectInteractable interactableObject { get => (IXRSelectInteractable)base.interactableObject; set => base.interactableObject = value; }
        public XRInteractionManager manager { get; set; }
    }
    public class SelectExitEventArgs : BaseInteractionEventArgs
    {
        public new IXRSelectInteractor interactorObject { get => (IXRSelectInteractor)base.interactorObject; set => base.interactorObject = value; }
        public new IXRSelectInteractable interactableObject { get => (IXRSelectInteractable)base.interactableObject; set => base.interactableObject = value; }
        public XRInteractionManager manager { get; set; }
        public bool isCanceled { get; set; }
    }
    public class ActivateEventArgs : BaseInteractionEventArgs { }
    public class DeactivateEventArgs : BaseInteractionEventArgs { }

    [Serializable] public sealed class HoverEnterEvent : UnityEvent<HoverEnterEventArgs> { }
    [Serializable] public sealed class HoverExitEvent : UnityEvent<HoverExitEventArgs> { }
    [Serializable] public sealed class SelectEnterEvent : UnityEvent<SelectEnterEventArgs> { }
    [Serializable] public sealed class SelectExitEvent : UnityEvent<SelectExitEventArgs> { }
    [Serializable] public sealed class ActivateEvent : UnityEvent<ActivateEventArgs> { }
    [Serializable] public sealed class DeactivateEvent : UnityEvent<DeactivateEventArgs> { }

    [DefaultExecutionOrder(-105)]
    public class XRInteractionManager : MonoBehaviour
    {
        public void SelectEnter(IXRSelectInteractor interactor, IXRSelectInteractable interactable) { XRSim.SelectEnter(interactor, interactable); }
        public void SelectExit(IXRSelectInteractor interactor, IXRSelectInteractable interactable) { XRSim.SelectExit(interactor, interactable, false); }
        public void SelectCancel(IXRSelectInteractor interactor, IXRSelectInteractable interactable) { XRSim.SelectExit(interactor, interactable, true); }
        public void CancelInteractableSelection(IXRSelectInteractable interactable)
        {
            foreach (IXRSelectInteractor interactor in interactable.interactorsSelecting.ToArray()) XRSim.SelectExit(interactor, interactable, true);
        }
        public void CancelInteractorSelection(IXRSelectInteractor interactor)
        {
            foreach (IXRSelectInteractable interactable in interactor.interactablesSelected.ToArray()) XRSim.SelectExit(interactor, interactable, true);
        }
        public bool IsRegistered(IXRInteractable interactable) => true;
        public bool IsRegistered(IXRInteractor interactor) => true;
        public void RegisterInteractable(IXRInteractable interactable) { }
        public void UnregisterInteractable(IXRInteractable interactable) { }
        public void RegisterInteractor(IXRInteractor interactor) { }
        public void UnregisterInteractor(IXRInteractor interactor) { }
    }

    /// <summary>Simulator internals: selection bookkeeping and the emulated hand.</summary>
    public static class XRSim
    {
        private sealed class HandInteractor : IXRSelectInteractor, IXRHoverInteractor
        {
            private readonly List<IXRSelectInteractable> selected = new List<IXRSelectInteractable>();
            public Transform transform { get; set; }
            public List<IXRSelectInteractable> interactablesSelected => selected;
            public bool hasSelection => selected.Count > 0;
            public InteractionLayerMask interactionLayers => -1;
            public Transform GetAttachTransform(IXRInteractable interactable) => transform;
        }

        private static HandInteractor hand;

        internal static void Reset() { hand = null; }

        /// <summary>The emulated hand interactor (created on demand, lives on a hidden scene object).</summary>
        public static IXRSelectInteractor Hand
        {
            get
            {
                if (hand == null || hand.transform == null)
                {
                    var go = new GameObject("HeadlessSim Hand Interactor");
                    go.hideFlags = HideFlags.HideAndDontSave;
                    // The Starter Assets controllers carry a haptic player; give the hand one so haptic code paths run.
                    go.AddComponent<Inputs.Haptics.HapticImpulsePlayer>();
                    hand = new HandInteractor { transform = go.transform };
                }
                return hand;
            }
        }

        public static XRInteractionManager FindManager()
        {
            foreach (XRInteractionManager manager in Simulation.FindAll<XRInteractionManager>(false)) return manager;
            return null;
        }

        private static bool Usable(XRBaseInteractable interactable)
        {
            if (interactable == null || !interactable.isActiveAndEnabled) return false;
            if (!Application.isPlaying) { Debug.LogWarning("[HeadlessSim] XR interactions need play mode."); return false; }
            interactable.EnsureColliders();
            if (!interactable.colliders.Any(c => c != null && c.enabled && c.gameObject.activeInHierarchy))
            {
                Debug.LogWarning($"[HeadlessSim] '{interactable.name}' has no enabled collider: an XR interactor could not reach it.");
                return false;
            }
            return true;
        }

        public static bool Grab(XRGrabInteractable interactable)
        {
            if (!Usable(interactable)) return false;
            if (!interactable.IsSelectableBy(Hand)) return false;
            foreach (IXRSelectInteractor holder in interactable.interactorsSelecting.ToArray()) SelectExit(holder, interactable, false);
            ((HandInteractor)Hand).transform.position = interactable.transform.position;
            SelectEnter(Hand, interactable);
            return true;
        }

        public static bool Release(XRGrabInteractable interactable)
        {
            if (interactable == null || !interactable.interactorsSelecting.Contains(Hand)) return false;
            SelectExit(Hand, interactable, false);
            return true;
        }

        public static bool Select(XRSimpleInteractable interactable)
        {
            if (!Usable(interactable)) return false;
            if (!interactable.IsSelectableBy(Hand)) return false;
            var manager = FindManager();
            interactable.SimHoverEnter(new HoverEnterEventArgs { interactorObject = Hand, interactableObject = interactable, manager = manager });
            SelectEnter(Hand, interactable);
            SelectExit(Hand, interactable, false);
            interactable.SimHoverExit(new HoverExitEventArgs { interactorObject = Hand, interactableObject = interactable, manager = manager });
            return true;
        }

        /// <summary>Emulates a ray hovering the interactable (hover enter, then exit when <paramref name="hovering"/> is false).</summary>
        public static void Hover(XRBaseInteractable interactable, bool hovering)
        {
            if (interactable == null || !interactable.isActiveAndEnabled) return;
            var manager = FindManager();
            if (hovering) interactable.SimHoverEnter(new HoverEnterEventArgs { interactorObject = Hand, interactableObject = interactable, manager = manager });
            else interactable.SimHoverExit(new HoverExitEventArgs { interactorObject = Hand, interactableObject = interactable, manager = manager });
        }

        internal static void SelectEnter(IXRSelectInteractor interactor, IXRSelectInteractable interactable)
        {
            if (interactor == null || interactable == null) return;
            if (interactable.interactorsSelecting.Contains(interactor)) return;
            var args = new SelectEnterEventArgs { interactorObject = interactor, interactableObject = interactable, manager = FindManager() };
            interactor.interactablesSelected.Add(interactable);
            interactable.interactorsSelecting.Add(interactor);
            (interactor as XRBaseInteractor)?.SimSelectEntered(args);
            (interactable as XRBaseInteractable)?.SimSelectEntered(args);
        }

        internal static void SelectExit(IXRSelectInteractor interactor, IXRSelectInteractable interactable, bool canceled)
        {
            if (interactor == null || interactable == null) return;
            if (!interactable.interactorsSelecting.Remove(interactor)) return;
            interactor.interactablesSelected.Remove(interactable);
            var args = new SelectExitEventArgs { interactorObject = interactor, interactableObject = interactable, manager = FindManager(), isCanceled = canceled };
            (interactor as XRBaseInteractor)?.SimSelectExited(args);
            (interactable as XRBaseInteractable)?.SimSelectExited(args);
        }
    }

    namespace Interactors
    {
        public interface IXRInteractor
        {
            Transform transform { get; }
            InteractionLayerMask interactionLayers { get; }
            Transform GetAttachTransform(IXRInteractable interactable);
        }
        public interface IXRHoverInteractor : IXRInteractor { }
        public interface IXRSelectInteractor : IXRInteractor
        {
            List<IXRSelectInteractable> interactablesSelected { get; }
            bool hasSelection { get; }
        }
        public interface IXRActivateInteractor : IXRInteractor { }

        [DefaultExecutionOrder(-99)]
        public abstract class XRBaseInteractor : MonoBehaviour, IXRHoverInteractor, IXRSelectInteractor
        {
            [SerializeField] private XRInteractionManager m_InteractionManager;
            [SerializeField] private InteractionLayerMask m_InteractionLayers = -1;
            [SerializeField] private Transform m_AttachTransform;
            [SerializeField] private XRBaseInteractable m_StartingSelectedInteractable;
            [SerializeField] private SelectEnterEvent m_SelectEntered = new SelectEnterEvent();
            [SerializeField] private SelectExitEvent m_SelectExited = new SelectExitEvent();
            [SerializeField] private HoverEnterEvent m_HoverEntered = new HoverEnterEvent();
            [SerializeField] private HoverExitEvent m_HoverExited = new HoverExitEvent();
            public bool keepSelectedTargetValid = true;
            public InteractorHandedness handedness;
            [NonSerialized] private readonly List<IXRSelectInteractable> selected = new List<IXRSelectInteractable>();

            public XRInteractionManager interactionManager { get => m_InteractionManager != null ? m_InteractionManager : XRSim.FindManager(); set => m_InteractionManager = value; }
            public InteractionLayerMask interactionLayers { get => m_InteractionLayers; set => m_InteractionLayers = value; }
            public Transform attachTransform { get => m_AttachTransform; set => m_AttachTransform = value; }
            public XRBaseInteractable startingSelectedInteractable { get => m_StartingSelectedInteractable; set => m_StartingSelectedInteractable = value; }
            public SelectEnterEvent selectEntered { get => m_SelectEntered; set => m_SelectEntered = value; }
            public SelectExitEvent selectExited { get => m_SelectExited; set => m_SelectExited = value; }
            public HoverEnterEvent hoverEntered { get => m_HoverEntered; set => m_HoverEntered = value; }
            public HoverExitEvent hoverExited { get => m_HoverExited; set => m_HoverExited = value; }
            public List<IXRSelectInteractable> interactablesSelected => selected;
            public IXRSelectInteractable firstInteractableSelected => selected.Count > 0 ? selected[0] : null;
            public bool hasSelection => selected.Count > 0;
            public virtual bool isSelectActive => true;
            public virtual bool isHoverActive => true;

            public virtual Transform GetAttachTransform(IXRInteractable interactable) => m_AttachTransform != null ? m_AttachTransform : transform;
            public virtual bool CanHover(IXRHoverInteractable interactable) => true;
            public virtual bool CanSelect(IXRSelectInteractable interactable) => true;
            public bool IsSelecting(IXRSelectInteractable interactable) => selected.Contains(interactable);

            protected virtual void Awake() { }
            protected virtual void OnEnable() { }
            protected virtual void OnDisable() { }
            protected virtual void Start() { }
            protected virtual void OnDestroy() { }
            protected virtual void OnSelectEntering(SelectEnterEventArgs args) { }
            protected virtual void OnSelectEntered(SelectEnterEventArgs args) { m_SelectEntered.Invoke(args); }
            protected virtual void OnSelectExiting(SelectExitEventArgs args) { }
            protected virtual void OnSelectExited(SelectExitEventArgs args) { m_SelectExited.Invoke(args); }

            internal void SimSelectEntered(SelectEnterEventArgs args) { OnSelectEntering(args); OnSelectEntered(args); }
            internal void SimSelectExited(SelectExitEventArgs args) { OnSelectExiting(args); OnSelectExited(args); }

            internal override void SimOnDisable()
            {
                foreach (IXRSelectInteractable interactable in selected.ToArray()) XRSim.SelectExit(this, interactable, true);
            }
        }

        public abstract class XRBaseInputInteractor : XRBaseInteractor
        {
            public enum InputTriggerType { State, StateChange, Toggle, Sticky }
            public InputTriggerType selectActionTrigger = InputTriggerType.StateChange;
            public bool allowHoveredActivate;
            public bool allowActivate = true;
        }

        public class XRDirectInteractor : XRBaseInputInteractor { public bool improveAccuracyWithSphereCollider; }

        public class XRRayInteractor : XRBaseInputInteractor
        {
            public float maxRaycastDistance = 30f;
            public bool enableUIInteraction = true;
            public LayerMask raycastMask = -1;
            public QueryTriggerInteraction raycastTriggerInteraction = QueryTriggerInteraction.Ignore;
            public bool useForceGrab = true;
        }

        public class NearFarInteractor : XRBaseInputInteractor
        {
            public bool enableNearCasting = true;
            public bool enableFarCasting = true;
            public bool enableUIInteraction = true;
            public bool blockUIOnInteractableSelection = true;
        }

        /// <summary>
        /// Socket: holds one grab interactable. In the simulator a free, active socket takes the nearest
        /// unselected XRGrabInteractable whose collider bounds overlap the socket's trigger collider.
        /// </summary>
        public class XRSocketInteractor : XRBaseInteractor
        {
            public bool socketActive = true;
            public bool showInteractableHoverMeshes = true;
            public Material interactableHoverMeshMaterial;
            public Material interactableCantHoverMeshMaterial;
            public float interactableHoverScale = 1f;
            public float recycleDelayTime = 1f;
            public bool hoverSocketSnapping;
            public float socketSnappingRadius = 0.1f;
            [NonSerialized] private float lastRemoveTime = -100f;
            [NonSerialized] private bool startingSelectionDone;

            internal override bool SimTicks => true;
            public override bool isSelectActive => socketActive && isActiveAndEnabled;

            internal override void SimStart()
            {
                if (startingSelectionDone) return;
                startingSelectionDone = true;
                if (startingSelectedInteractable is XRGrabInteractable grab && grab.isActiveAndEnabled && !hasSelection) Take(grab);
            }

            protected override void OnSelectExited(SelectExitEventArgs args)
            {
                lastRemoveTime = Time.time;
                base.OnSelectExited(args);
            }

            private Bounds? TriggerBounds()
            {
                Bounds? result = null;
                foreach (Collider collider in GetComponents<Collider>())
                {
                    if (!collider.enabled || !collider.isTrigger) continue;
                    if (result == null) result = collider.bounds;
                    else { Bounds merged = result.Value; merged.Encapsulate(collider.bounds); result = merged; }
                }
                return result;
            }

            internal override void SimFixedUpdate(float fixedDeltaTime)
            {
                // Drop references to destroyed or deactivated interactables.
                foreach (IXRSelectInteractable held in interactablesSelected.ToArray())
                {
                    var heldObject = held as XRBaseInteractable;
                    if (heldObject == null || !heldObject.isActiveAndEnabled) XRSim.SelectExit(this, held, true);
                }

                if (!socketActive)
                {
                    foreach (IXRSelectInteractable held in interactablesSelected.ToArray()) XRSim.SelectExit(this, held, false);
                    return;
                }

                if (hasSelection)
                {
                    // Keep the held object on the attach point (it does not fall).
                    if (firstInteractableSelected is XRGrabInteractable holding) Snap(holding);
                    return;
                }

                if (Time.time - lastRemoveTime < recycleDelayTime) return;
                Bounds? zone = TriggerBounds();
                if (zone == null) return;

                Transform attach = GetAttachTransform(null);
                XRGrabInteractable best = null;
                float bestDistance = float.MaxValue;
                foreach (XRGrabInteractable candidate in Simulation.FindAll<XRGrabInteractable>(false))
                {
                    if (!candidate.isActiveAndEnabled || candidate.isSelected) continue;
                    candidate.EnsureColliders();
                    bool overlaps = false;
                    foreach (Collider collider in candidate.colliders)
                    {
                        if (collider != null && collider.enabled && !collider.isTrigger && zone.Value.Intersects(collider.bounds)) { overlaps = true; break; }
                    }
                    if (!overlaps) continue;
                    if (((int)interactionLayers & (int)candidate.interactionLayers) == 0) continue;
                    if (!CanHover(candidate) || !CanSelect(candidate) || !candidate.IsSelectableBy(this)) continue;
                    float distance = (candidate.transform.position - attach.position).sqrMagnitude;
                    if (distance < bestDistance) { bestDistance = distance; best = candidate; }
                }

                if (best != null) Take(best);
            }

            private void Take(XRGrabInteractable interactable)
            {
                XRSim.SelectEnter(this, interactable);
                if (IsSelecting(interactable)) Snap(interactable);
            }

            private void Snap(XRGrabInteractable interactable)
            {
                Transform socketAttach = GetAttachTransform(interactable);
                Transform root = interactable.transform;
                Transform grabAttach = interactable.attachTransform;
                if (grabAttach == null)
                {
                    root.SetPositionAndRotation(socketAttach.position, socketAttach.rotation);
                    return;
                }

                Quaternion relativeRotation = Quaternion.Inverse(root.rotation) * grabAttach.rotation;
                Vector3 relativeOffset = Quaternion.Inverse(root.rotation) * (grabAttach.position - root.position);
                Quaternion rotation = socketAttach.rotation * Quaternion.Inverse(relativeRotation);
                root.SetPositionAndRotation(socketAttach.position - rotation * relativeOffset, rotation);
            }
        }
    }

    namespace Interactables
    {
        public interface IXRInteractable
        {
            Transform transform { get; }
            List<Collider> colliders { get; }
            InteractionLayerMask interactionLayers { get; }
            Transform GetAttachTransform(IXRInteractor interactor);
        }
        public interface IXRHoverInteractable : IXRInteractable { bool isHovered { get; } bool IsHoverableBy(IXRHoverInteractor interactor); }
        public interface IXRSelectInteractable : IXRInteractable
        {
            bool isSelected { get; }
            List<IXRSelectInteractor> interactorsSelecting { get; }
            IXRSelectInteractor firstInteractorSelecting { get; }
            bool IsSelectableBy(IXRSelectInteractor interactor);
        }
        public interface IXRActivateInteractable : IXRInteractable { }

        [SelectionBase]
        [DefaultExecutionOrder(-98)]
        public abstract class XRBaseInteractable : MonoBehaviour, IXRHoverInteractable, IXRSelectInteractable, IXRActivateInteractable
        {
            public enum MovementType { VelocityTracking, Kinematic, Instantaneous }
            public enum DistanceCalculationMode { TransformPosition, ColliderPosition, ColliderVolume }

            [SerializeField] private XRInteractionManager m_InteractionManager;
            [SerializeField] private List<Collider> m_Colliders = new List<Collider>();
            [SerializeField] private InteractionLayerMask m_InteractionLayers = 1;
            [SerializeField] private InteractableSelectMode m_SelectMode = InteractableSelectMode.Single;
            [SerializeField] private HoverEnterEvent m_FirstHoverEntered = new HoverEnterEvent();
            [SerializeField] private HoverExitEvent m_LastHoverExited = new HoverExitEvent();
            [SerializeField] private HoverEnterEvent m_HoverEntered = new HoverEnterEvent();
            [SerializeField] private HoverExitEvent m_HoverExited = new HoverExitEvent();
            [SerializeField] private SelectEnterEvent m_FirstSelectEntered = new SelectEnterEvent();
            [SerializeField] private SelectExitEvent m_LastSelectExited = new SelectExitEvent();
            [SerializeField] private SelectEnterEvent m_SelectEntered = new SelectEnterEvent();
            [SerializeField] private SelectExitEvent m_SelectExited = new SelectExitEvent();
            [SerializeField] private ActivateEvent m_Activated = new ActivateEvent();
            [SerializeField] private DeactivateEvent m_Deactivated = new DeactivateEvent();
            public DistanceCalculationMode distanceCalculationMode = DistanceCalculationMode.ColliderPosition;
            public GameObject customReticle;
            public bool allowGazeInteraction;

            [NonSerialized] private readonly List<IXRSelectInteractor> selecting = new List<IXRSelectInteractor>();
            [NonSerialized] private readonly List<IXRHoverInteractor> hovering = new List<IXRHoverInteractor>();
            [NonSerialized] private bool collidersCollected;

            public XRInteractionManager interactionManager { get => m_InteractionManager != null ? m_InteractionManager : XRSim.FindManager(); set => m_InteractionManager = value; }
            public List<Collider> colliders => m_Colliders;
            public InteractionLayerMask interactionLayers { get => m_InteractionLayers; set => m_InteractionLayers = value; }
            public InteractableSelectMode selectMode { get => m_SelectMode; set => m_SelectMode = value; }
            public HoverEnterEvent firstHoverEntered { get => m_FirstHoverEntered; set => m_FirstHoverEntered = value; }
            public HoverExitEvent lastHoverExited { get => m_LastHoverExited; set => m_LastHoverExited = value; }
            public HoverEnterEvent hoverEntered { get => m_HoverEntered; set => m_HoverEntered = value; }
            public HoverExitEvent hoverExited { get => m_HoverExited; set => m_HoverExited = value; }
            public SelectEnterEvent firstSelectEntered { get => m_FirstSelectEntered; set => m_FirstSelectEntered = value; }
            public SelectExitEvent lastSelectExited { get => m_LastSelectExited; set => m_LastSelectExited = value; }
            public SelectEnterEvent selectEntered { get => m_SelectEntered; set => m_SelectEntered = value; }
            public SelectExitEvent selectExited { get => m_SelectExited; set => m_SelectExited = value; }
            public ActivateEvent activated { get => m_Activated; set => m_Activated = value; }
            public DeactivateEvent deactivated { get => m_Deactivated; set => m_Deactivated = value; }
            public bool isHovered => hovering.Count > 0;
            public bool isSelected => selecting.Count > 0;
            public List<IXRHoverInteractor> interactorsHovering => hovering;
            public List<IXRSelectInteractor> interactorsSelecting => selecting;
            public IXRSelectInteractor firstInteractorSelecting => selecting.Count > 0 ? selecting[0] : null;

            public virtual bool IsHoverableBy(IXRHoverInteractor interactor) => true;
            public virtual bool IsSelectableBy(IXRSelectInteractor interactor) => true;
            public virtual Transform GetAttachTransform(IXRInteractor interactor) => transform;
            public virtual float GetDistanceSqrToInteractor(IXRInteractor interactor) => interactor != null ? (interactor.transform.position - transform.position).sqrMagnitude : float.MaxValue;

            protected virtual void Reset() { }
            protected virtual void Awake() { EnsureColliders(); }
            protected virtual void OnEnable() { }
            protected virtual void OnDisable() { }
            protected virtual void OnDestroy() { }
            protected virtual void OnHoverEntering(HoverEnterEventArgs args) { }
            protected virtual void OnHoverEntered(HoverEnterEventArgs args) { if (hovering.Count == 1) m_FirstHoverEntered.Invoke(args); m_HoverEntered.Invoke(args); }
            protected virtual void OnHoverExiting(HoverExitEventArgs args) { }
            protected virtual void OnHoverExited(HoverExitEventArgs args) { if (hovering.Count == 0) m_LastHoverExited.Invoke(args); m_HoverExited.Invoke(args); }
            protected virtual void OnSelectEntering(SelectEnterEventArgs args) { }
            protected virtual void OnSelectEntered(SelectEnterEventArgs args) { if (selecting.Count == 1) m_FirstSelectEntered.Invoke(args); m_SelectEntered.Invoke(args); }
            protected virtual void OnSelectExiting(SelectExitEventArgs args) { }
            protected virtual void OnSelectExited(SelectExitEventArgs args) { if (selecting.Count == 0) m_LastSelectExited.Invoke(args); m_SelectExited.Invoke(args); }
            protected virtual void OnActivated(ActivateEventArgs args) { m_Activated.Invoke(args); }
            protected virtual void OnDeactivated(DeactivateEventArgs args) { m_Deactivated.Invoke(args); }

            /// <summary>Like XRI: when no collider is assigned, the colliders of the children are used.</summary>
            internal void EnsureColliders()
            {
                if (collidersCollected) return;
                collidersCollected = true;
                m_Colliders.RemoveAll(c => c == null);
                if (m_Colliders.Count == 0) m_Colliders.AddRange(GetComponentsInChildren<Collider>(true));
            }

            internal void SimHoverEnter(HoverEnterEventArgs args)
            {
                var interactor = args.interactorObject as IXRHoverInteractor;
                if (interactor == null || hovering.Contains(interactor)) return;
                hovering.Add(interactor);
                OnHoverEntering(args);
                OnHoverEntered(args);
            }

            internal void SimHoverExit(HoverExitEventArgs args)
            {
                var interactor = args.interactorObject as IXRHoverInteractor;
                if (interactor == null || !hovering.Remove(interactor)) return;
                OnHoverExiting(args);
                OnHoverExited(args);
            }

            internal virtual void SimSelectEntered(SelectEnterEventArgs args) { OnSelectEntering(args); OnSelectEntered(args); }
            internal virtual void SimSelectExited(SelectExitEventArgs args) { OnSelectExiting(args); OnSelectExited(args); }

            internal override void SimOnDisable()
            {
                foreach (IXRSelectInteractor interactor in selecting.ToArray()) XRSim.SelectExit(interactor, this, true);
                hovering.Clear();
            }
        }

        public class XRSimpleInteractable : XRBaseInteractable { }

        [RequireComponent(typeof(Rigidbody))]
        public class XRGrabInteractable : XRBaseInteractable
        {
            public Transform attachTransform;
            public Transform secondaryAttachTransform;
            public bool useDynamicAttach;
            public bool matchAttachPosition = true;
            public bool matchAttachRotation = true;
            public bool snapToColliderVolume = true;
            public bool reinitializeDynamicAttachEverySingleGrab = true;
            public float attachEaseInTime = 0.15f;
            public MovementType movementType = MovementType.Instantaneous;
            public float velocityDamping = 1f, velocityScale = 1f, angularVelocityDamping = 1f, angularVelocityScale = 1f;
            public bool trackPosition = true, trackRotation = true, trackScale = true;
            public bool smoothPosition, smoothRotation, smoothScale;
            public float smoothPositionAmount = 5f, tightenPosition = 0.5f, smoothRotationAmount = 5f, tightenRotation = 0.5f;
            public bool throwOnDetach = true;
            public float throwSmoothingDuration = 0.25f, throwVelocityScale = 1.5f, throwAngularVelocityScale = 1f;
            public bool forceGravityOnDetach;
            public bool retainTransformParent = true;

            [NonSerialized] private bool storedKinematic, storedGravity, stateStored;

            public override Transform GetAttachTransform(IXRInteractor interactor) => attachTransform != null ? attachTransform : transform;

            // While held (by a hand or a socket) the body does not fall; the previous settings come back on release.
            protected override void OnSelectEntering(SelectEnterEventArgs args)
            {
                base.OnSelectEntering(args);
                var body = GetComponent<Rigidbody>();
                if (body == null || interactorsSelecting.Count != 1) return;
                storedKinematic = body.isKinematic;
                storedGravity = body.useGravity;
                stateStored = true;
                body.useGravity = false;
                if (movementType != MovementType.VelocityTracking) body.isKinematic = true;
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }

            protected override void OnSelectExiting(SelectExitEventArgs args)
            {
                base.OnSelectExiting(args);
                var body = GetComponent<Rigidbody>();
                if (body == null || interactorsSelecting.Count != 0 || !stateStored) return;
                body.isKinematic = storedKinematic;
                body.useGravity = storedGravity || forceGravityOnDetach;
                stateStored = false;
            }
        }
    }

    namespace Inputs.Haptics
    {
        public class HapticImpulsePlayer : MonoBehaviour
        {
            public float amplitudeMultiplier = 1f;
            /// <summary>Simulator bookkeeping: number of impulses requested.</summary>
            [NonSerialized] public int SimImpulseCount;
            public bool SendHapticImpulse(float amplitude, float duration) { SimImpulseCount++; return true; }
            public bool SendHapticImpulse(float amplitude, float duration, float frequency) { SimImpulseCount++; return true; }
        }
    }

    namespace UI
    {
        using UnityEngine.EventSystems;

        public abstract class UIInputModule : BaseInputModule
        {
            public float clickSpeed = 0.3f;
            public float moveDeadzone = 0.6f;
            public float repeatDelay = 0.5f;
            public float repeatRate = 0.1f;
            public float trackedDeviceDragThresholdMultiplier = 2f;
            public float trackedScrollDeltaMultiplier = 5f;
            public Camera uiCamera;
            public override void Process() { }
        }

        public class XRUIInputModule : UIInputModule
        {
            public bool enableXRInput = true;
            public bool enableMouseInput = true;
            public bool enableTouchInput = true;
            public bool enableGamepadInput = true;
            public bool enableJoystickInput = true;
            public bool enableBuiltinActionsAsFallback = true;
        }

        public class TrackedDeviceEventData : PointerEventData
        {
            public TrackedDeviceEventData(EventSystem eventSystem) : base(eventSystem) { }
            public List<Vector3> rayPoints { get; set; } = new List<Vector3>();
            public int rayHitIndex { get; set; }
            public LayerMask layerMask { get; set; } = -1;
        }

        /// <summary>
        /// Like the real component, it only answers TrackedDeviceEventData (XR rays): ordinary mouse
        /// pointer events need a standard GraphicRaycaster on the same canvas.
        /// </summary>
        [RequireComponent(typeof(Canvas))]
        public class TrackedDeviceGraphicRaycaster : BaseRaycaster
        {
            public bool ignoreReversedGraphics;
            public bool checkFor2DOcclusion;
            public bool checkFor3DOcclusion;
            public LayerMask blockingMask = -1;
            public QueryTriggerInteraction raycastTriggerInteraction = QueryTriggerInteraction.Ignore;
            public override Camera eventCamera
            {
                get
                {
                    Canvas canvas = GetComponent<Canvas>();
                    return canvas != null && canvas.worldCamera != null ? canvas.worldCamera : Camera.main;
                }
            }
            public override void Raycast(PointerEventData eventData, List<RaycastResult> resultAppendList) { }
        }
    }
}

namespace UnityEngine.InputSystem
{
    public enum Key
    {
        None = 0, Space = 1, Enter = 2, Tab = 3, Backquote = 4, Quote = 5, Semicolon = 6, Comma = 7, Period = 8, Slash = 9, Backslash = 10,
        LeftBracket = 11, RightBracket = 12, Minus = 13, Equals = 14,
        A = 15, B = 16, C = 17, D = 18, E = 19, F = 20, G = 21, H = 22, I = 23, J = 24, K = 25, L = 26, M = 27, N = 28, O = 29, P = 30, Q = 31, R = 32,
        S = 33, T = 34, U = 35, V = 36, W = 37, X = 38, Y = 39, Z = 40,
        Digit1 = 41, Digit2 = 42, Digit3 = 43, Digit4 = 44, Digit5 = 45, Digit6 = 46, Digit7 = 47, Digit8 = 48, Digit9 = 49, Digit0 = 50,
        LeftShift = 51, RightShift = 52, LeftAlt = 53, RightAlt = 54, AltGr = 54, LeftCtrl = 55, RightCtrl = 56, LeftMeta = 57, RightMeta = 58,
        LeftWindows = 57, RightWindows = 58, LeftApple = 57, RightApple = 58, LeftCommand = 57, RightCommand = 58, ContextMenu = 59,
        Escape = 60, LeftArrow = 61, RightArrow = 62, UpArrow = 63, DownArrow = 64, Backspace = 65, PageDown = 66, PageUp = 67, Home = 68, End = 69,
        Insert = 70, Delete = 71, CapsLock = 72, NumLock = 73, PrintScreen = 74, ScrollLock = 75, Pause = 76,
        NumpadEnter = 77, NumpadDivide = 78, NumpadMultiply = 79, NumpadPlus = 80, NumpadMinus = 81, NumpadPeriod = 82, NumpadEquals = 83,
        Numpad0 = 84, Numpad1 = 85, Numpad2 = 86, Numpad3 = 87, Numpad4 = 88, Numpad5 = 89, Numpad6 = 90, Numpad7 = 91, Numpad8 = 92, Numpad9 = 93,
        F1 = 94, F2 = 95, F3 = 96, F4 = 97, F5 = 98, F6 = 99, F7 = 100, F8 = 101, F9 = 102, F10 = 103, F11 = 104, F12 = 105,
        OEM1 = 106, OEM2 = 107, OEM3 = 108, OEM4 = 109, OEM5 = 110, IMESelected = 111,
    }

    namespace Controls
    {
        public class ButtonControl
        {
            internal int pressedFrame = -1, releasedFrame = -1;
            internal bool held;
            public bool isPressed => held;
            public bool wasPressedThisFrame => pressedFrame == Time.frameCount;
            public bool wasReleasedThisFrame => releasedFrame == Time.frameCount;
            public float ReadValue() => held ? 1f : 0f;
        }
        public class KeyControl : ButtonControl { public Key keyCode { get; internal set; } }
        public class AnyKeyControl : ButtonControl { }
        public class Vector2Control
        {
            internal Vector2 value;
            public Vector2 ReadValue() => value;
            public AxisControl x => new AxisControl { value = value.x };
            public AxisControl y => new AxisControl { value = value.y };
        }
        public class AxisControl { internal float value; public float ReadValue() => value; }
    }

    public class InputDevice { public bool added => true; public string name => GetType().Name; }

    /// <summary>
    /// Keyboard.current is null until a test creates one with Keyboard.SimCreate(); keys are then
    /// pressed with SimPress(key), which reports wasPressedThisFrame during the next simulated frame.
    /// </summary>
    public class Keyboard : InputDevice
    {
        public static Keyboard current { get; private set; }
        private readonly Dictionary<Key, Controls.KeyControl> keys = new Dictionary<Key, Controls.KeyControl>();
        public Controls.AnyKeyControl anyKey { get; } = new Controls.AnyKeyControl();
        public Controls.KeyControl this[Key key]
        {
            get
            {
                if (!keys.TryGetValue(key, out Controls.KeyControl control)) { control = new Controls.KeyControl { keyCode = key }; keys[key] = control; }
                return control;
            }
        }
        public Controls.KeyControl spaceKey => this[Key.Space];
        public Controls.KeyControl enterKey => this[Key.Enter];
        public Controls.KeyControl escapeKey => this[Key.Escape];
        public Controls.KeyControl tabKey => this[Key.Tab];

        public static Keyboard SimCreate() { current = new Keyboard(); return current; }
        public static void SimRemove() { current = null; }

        /// <summary>The key reads as pressed during the next simulated frame (and held until SimRelease).</summary>
        public void SimPress(Key key)
        {
            Controls.KeyControl control = this[key];
            control.held = true; control.pressedFrame = Time.frameCount + 1;
            anyKey.held = true; anyKey.pressedFrame = Time.frameCount + 1;
        }
        public void SimRelease(Key key)
        {
            Controls.KeyControl control = this[key];
            control.held = false; control.releasedFrame = Time.frameCount + 1;
            anyKey.held = keys.Values.Any(k => k.held);
        }
        /// <summary>Press, simulate one frame, release.</summary>
        public void SimTap(Key key)
        {
            SimPress(key);
            Simulation.Step(1f / 60f);
            SimRelease(key);
        }
    }

    public class Mouse : InputDevice
    {
        public static Mouse current { get; private set; }
        public Controls.ButtonControl leftButton { get; } = new Controls.ButtonControl();
        public Controls.ButtonControl rightButton { get; } = new Controls.ButtonControl();
        public Controls.ButtonControl middleButton { get; } = new Controls.ButtonControl();
        public Controls.Vector2Control position { get; } = new Controls.Vector2Control();
        public Controls.Vector2Control delta { get; } = new Controls.Vector2Control();
        public Controls.Vector2Control scroll { get; } = new Controls.Vector2Control();
        public static Mouse SimCreate() { current = new Mouse(); return current; }
        public static void SimRemove() { current = null; }
        public void SimClick(Vector2 screenPosition)
        {
            position.value = screenPosition;
            leftButton.held = true; leftButton.pressedFrame = Time.frameCount + 1;
            Simulation.Step(1f / 60f);
            leftButton.held = false; leftButton.releasedFrame = Time.frameCount + 1;
        }
    }

    public class Gamepad : InputDevice { public static Gamepad current => null; }
}
