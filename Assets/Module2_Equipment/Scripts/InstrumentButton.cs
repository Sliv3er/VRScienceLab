using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Haptics;

namespace VRScienceLab.Module2
{
    /// <summary>
    /// A physical push button operated with XR interactors (ray, near-far, direct).
    /// Wraps an <see cref="XRSimpleInteractable"/>: a select on the interactable triggers a press,
    /// which plays visual/audio/haptic feedback and raises <see cref="onPressed"/>.
    /// <see cref="Press"/> can also be called directly (mouse/keyboard debug helper, tests).
    /// </summary>
    [RequireComponent(typeof(XRSimpleInteractable))]
    [AddComponentMenu("VR Science Lab/Module 2/Instrument Button")]
    public class InstrumentButton : MonoBehaviour
    {
        [SerializeField, Tooltip("Label used in logs and shown on the button by the scene builder.")]
        private string buttonLabel = "Button";

        [Tooltip("Invoked each time the button is pressed.")]
        public UnityEvent onPressed = new UnityEvent();

        [Header("Visual feedback")]
        [SerializeField, Tooltip("Transform moved down while pressed. Defaults to this transform.")]
        private Transform pressVisual;

        [SerializeField, Min(0f), Tooltip("Distance (local units) the button moves down while pressed.")]
        private float pressDepth = 0.006f;

        [SerializeField, Min(0.01f), Tooltip("How long the pressed look lasts, in seconds.")]
        private float pressDuration = 0.15f;

        [SerializeField, Tooltip("Renderer tinted while pressed. Defaults to the renderer on this GameObject.")]
        private Renderer highlightRenderer;

        [SerializeField] private Color pressedColor = new Color(1f, 0.85f, 0.2f);

        [Header("Audio / haptics (optional)")]
        [SerializeField] private AudioSource clickAudio;
        [SerializeField] private bool sendHaptics = true;
        [SerializeField, Range(0f, 1f)] private float hapticAmplitude = 0.4f;
        [SerializeField, Min(0f)] private float hapticDuration = 0.08f;

        [Header("Robustness")]
        [SerializeField, Min(0f), Tooltip("Presses arriving within this many seconds of the previous one are ignored.")]
        private float repeatPressCooldown = 0.1f;

        private static readonly int ColorPropertyId = Shader.PropertyToID("_Color");
        private static readonly int BaseColorPropertyId = Shader.PropertyToID("_BaseColor");

        private XRSimpleInteractable interactable;
        private MaterialPropertyBlock propertyBlock;
        private Vector3 restLocalPosition;
        private float pressTimer;
        private float lastPressTime = float.NegativeInfinity;
        private bool visualPressed;

        /// <summary>Label of this button.</summary>
        public string Label => buttonLabel;

        /// <summary>Number of accepted presses since the scene started.</summary>
        public int PressCount { get; private set; }

        private void Awake()
        {
            interactable = GetComponent<XRSimpleInteractable>();
            if (pressVisual == null) pressVisual = transform;
            if (highlightRenderer == null) highlightRenderer = GetComponent<Renderer>();
            restLocalPosition = pressVisual.localPosition;

            if (GetComponentInChildren<Collider>() == null)
            {
                Debug.LogError($"[InstrumentButton] '{buttonLabel}' on '{name}' has no Collider, so XR interactors cannot hit it.", this);
            }
        }

        private void OnEnable()
        {
            if (interactable != null) interactable.selectEntered.AddListener(OnSelectEntered);
        }

        private void OnDisable()
        {
            if (interactable != null) interactable.selectEntered.RemoveListener(OnSelectEntered);
            RestoreVisual();
        }

        private void Update()
        {
            if (!visualPressed) return;

            pressTimer -= Time.deltaTime;
            if (pressTimer <= 0f) RestoreVisual();
        }

        private void OnSelectEntered(SelectEnterEventArgs args)
        {
            if (!Press()) return;

            if (sendHaptics && args != null && args.interactorObject != null)
            {
                // The Starter Assets controllers carry a HapticImpulsePlayer on a parent of each interactor.
                var hapticPlayer = args.interactorObject.transform.GetComponentInParent<HapticImpulsePlayer>();
                if (hapticPlayer != null) hapticPlayer.SendHapticImpulse(hapticAmplitude, hapticDuration);
            }
        }

        /// <summary>
        /// Presses the button. Returns false when the press was ignored (component disabled or
        /// pressed again within the cooldown).
        /// </summary>
        public bool Press()
        {
            if (!isActiveAndEnabled) return false;
            if (Time.time - lastPressTime < repeatPressCooldown) return false;

            lastPressTime = Time.time;
            PressCount++;

            BeginVisual();
            if (clickAudio != null) clickAudio.Play();
            onPressed?.Invoke();
            return true;
        }

        private void BeginVisual()
        {
            visualPressed = true;
            pressTimer = pressDuration;
            if (pressVisual != null) pressVisual.localPosition = restLocalPosition - new Vector3(0f, pressDepth, 0f);

            if (highlightRenderer != null)
            {
                if (propertyBlock == null) propertyBlock = new MaterialPropertyBlock();
                propertyBlock.SetColor(ColorPropertyId, pressedColor);
                propertyBlock.SetColor(BaseColorPropertyId, pressedColor);
                highlightRenderer.SetPropertyBlock(propertyBlock);
            }
        }

        private void RestoreVisual()
        {
            if (!visualPressed) return;
            visualPressed = false;
            if (pressVisual != null) pressVisual.localPosition = restLocalPosition;

            if (highlightRenderer != null && propertyBlock != null)
            {
                propertyBlock.Clear();
                highlightRenderer.SetPropertyBlock(propertyBlock);
            }
        }
    }
}
