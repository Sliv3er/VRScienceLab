using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Haptics;

namespace VRScienceLab.Module2
{
    /// <summary>States of a <see cref="CabinetDoor"/>.</summary>
    public enum CabinetDoorState
    {
        Closed,
        Opening,
        Open,
        Closing,
    }

    /// <summary>
    /// A hinged cabinet door operated with XR interactors.
    /// The door is an <see cref="XRSimpleInteractable"/>: pointing a controller ray at the door or
    /// its handle and pressing select (Grip) toggles it. The door then swings around its hinge
    /// with a Transform rotation (smooth ease in / ease out). Put this component on the hinge
    /// pivot; the leaf, glass, handle and their colliders are children of the pivot.
    /// A kinematic Rigidbody on the pivot keeps the moving colliders cheap for the physics engine.
    /// </summary>
    [RequireComponent(typeof(XRSimpleInteractable))]
    [AddComponentMenu("VR Science Lab/Module 2/Cabinet Door")]
    public class CabinetDoor : MonoBehaviour
    {
        [SerializeField, Tooltip("Name used in logs and status texts.")]
        private string doorName = "Door";

        [SerializeField, Tooltip("Transform that rotates. Defaults to this transform (the hinge pivot).")]
        private Transform hinge;

        [SerializeField, Tooltip("Local axis of the hinge.")]
        private Vector3 hingeAxis = Vector3.up;

        [SerializeField, Tooltip("Angle of the fully open door in degrees. The sign selects the swing direction.")]
        private float openAngle = 105f;

        [SerializeField, Min(0.05f), Tooltip("Seconds the door takes to open or close completely.")]
        private float swingDuration = 0.9f;

        [SerializeField, Tooltip("Start the scene with the door open.")]
        private bool startOpen = false;

        [SerializeField, Min(0f), Tooltip("Select presses arriving within this many seconds of the previous one are ignored.")]
        private float repeatPressCooldown = 0.25f;

        [Header("Haptics (optional)")]
        [SerializeField] private bool sendHaptics = true;
        [SerializeField, Range(0f, 1f)] private float hapticAmplitude = 0.35f;
        [SerializeField, Min(0f)] private float hapticDuration = 0.08f;

        [SerializeField] private bool logStateChanges = false;

        [Header("Events")]
        [Tooltip("Raised whenever the door state changes.")]
        public UnityEvent onStateChanged = new UnityEvent();
        [Tooltip("Raised when the door reaches the fully open position.")]
        public UnityEvent onOpened = new UnityEvent();
        [Tooltip("Raised when the door reaches the fully closed position.")]
        public UnityEvent onClosed = new UnityEvent();

        private XRSimpleInteractable interactable;
        private Quaternion closedRotation;
        private bool restCaptured;
        private float lastPressTime = float.NegativeInfinity;

        /// <summary>Current state.</summary>
        public CabinetDoorState State { get; private set; } = CabinetDoorState.Closed;

        /// <summary>0 when fully closed, 1 when fully open.</summary>
        public float OpenFraction { get; private set; }

        /// <summary>True when the door is open or opening.</summary>
        public bool IsOpenOrOpening => State == CabinetDoorState.Open || State == CabinetDoorState.Opening;

        /// <summary>True only when the door is completely closed.</summary>
        public bool IsClosed => State == CabinetDoorState.Closed;

        /// <summary>True while the door is swinging.</summary>
        public bool IsMoving => State == CabinetDoorState.Opening || State == CabinetDoorState.Closing;

        /// <summary>Angle currently applied around the hinge, in degrees.</summary>
        public float CurrentAngle => openAngle * EasedFraction;

        public float OpenAngle => openAngle;
        public string DoorName => doorName;

        /// <summary>Number of accepted select presses.</summary>
        public int PressCount { get; private set; }

        private float EasedFraction => Mathf.SmoothStep(0f, 1f, OpenFraction);

        private void Awake()
        {
            interactable = GetComponent<XRSimpleInteractable>();
            CaptureRest();

            if (GetComponentInChildren<Collider>() == null)
            {
                Debug.LogError($"[CabinetDoor] '{doorName}' on '{name}' has no Collider, so XR interactors cannot hit it.", this);
            }

            if (startOpen) SetOpenImmediate(true);
        }

        private void OnEnable()
        {
            if (interactable != null) interactable.selectEntered.AddListener(OnSelectEntered);
        }

        private void OnDisable()
        {
            if (interactable != null) interactable.selectEntered.RemoveListener(OnSelectEntered);
        }

        private void Update()
        {
            if (!IsMoving) return;
            AdvanceTime(Time.deltaTime);
        }

        private void CaptureRest()
        {
            if (restCaptured) return;
            if (hinge == null) hinge = transform;
            closedRotation = hinge.localRotation;
            restCaptured = true;
        }

        private void OnSelectEntered(SelectEnterEventArgs args)
        {
            if (!Press()) return;

            if (sendHaptics && args != null && args.interactorObject != null)
            {
                var hapticPlayer = args.interactorObject.transform.GetComponentInParent<HapticImpulsePlayer>();
                if (hapticPlayer != null) hapticPlayer.SendHapticImpulse(hapticAmplitude, hapticDuration);
            }
        }

        /// <summary>
        /// Same effect as a select press with a controller: toggles the door. Returns false when
        /// the press was ignored (component disabled or pressed again within the cooldown).
        /// </summary>
        public bool Press()
        {
            if (!isActiveAndEnabled) return false;
            if (Time.time - lastPressTime < repeatPressCooldown) return false;

            lastPressTime = Time.time;
            PressCount++;
            Toggle();
            return true;
        }

        /// <summary>Closed/Closing -> Opening.</summary>
        public void Open()
        {
            if (State == CabinetDoorState.Open || State == CabinetDoorState.Opening) return;
            SetState(CabinetDoorState.Opening);
        }

        /// <summary>Open/Opening -> Closing.</summary>
        public void Close()
        {
            if (State == CabinetDoorState.Closed || State == CabinetDoorState.Closing) return;
            SetState(CabinetDoorState.Closing);
        }

        /// <summary>Opens a closed (or closing) door, closes an open (or opening) one.</summary>
        public void Toggle()
        {
            if (IsOpenOrOpening) Close();
            else Open();
        }

        /// <summary>Puts the door in the open or closed position without animation.</summary>
        public void SetOpenImmediate(bool open)
        {
            CaptureRest();
            OpenFraction = open ? 1f : 0f;
            ApplyRotation();

            CabinetDoorState target = open ? CabinetDoorState.Open : CabinetDoorState.Closed;
            if (State != target) SetState(target);
        }

        /// <summary>
        /// Advances the swing by <paramref name="deltaSeconds"/>.
        /// Called from Update with Time.deltaTime; tests call it directly.
        /// </summary>
        public void AdvanceTime(float deltaSeconds)
        {
            if (!IsMoving || deltaSeconds <= 0f) return;
            CaptureRest();

            float step = deltaSeconds / swingDuration;
            if (State == CabinetDoorState.Opening)
            {
                OpenFraction = Mathf.Min(1f, OpenFraction + step);
                ApplyRotation();
                if (OpenFraction >= 1f)
                {
                    SetState(CabinetDoorState.Open);
                    onOpened?.Invoke();
                }
            }
            else
            {
                OpenFraction = Mathf.Max(0f, OpenFraction - step);
                ApplyRotation();
                if (OpenFraction <= 0f)
                {
                    SetState(CabinetDoorState.Closed);
                    onClosed?.Invoke();
                }
            }
        }

        private void ApplyRotation()
        {
            hinge.localRotation = closedRotation * Quaternion.AngleAxis(openAngle * EasedFraction, hingeAxis);
        }

        private void SetState(CabinetDoorState newState)
        {
            State = newState;
            if (logStateChanges) Debug.Log($"[CabinetDoor] {doorName}: {newState}.", this);
            onStateChanged?.Invoke();
        }
    }
}
