using System;
using TMPro;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace VRScienceLab.Module2
{
    /// <summary>Kind of laboratory sample container.</summary>
    public enum LabSampleKind
    {
        Beaker,
        TestTube,
        Flask,
        SampleBox,
    }

    /// <summary>
    /// A laboratory sample the user can create, select, move, scale, measure and remove.
    /// It only holds the sample's own data and appearance; creation and destruction are
    /// handled by <see cref="SampleManager"/>, and the instruments read the Rigidbody mass
    /// and the optional <see cref="TemperatureSample"/> exactly as they do for any object.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    [AddComponentMenu("VR Science Lab/Module 2/Lab Sample")]
    public class LabSample : MonoBehaviour
    {
        [SerializeField, Tooltip("Name shown on the label and in the interfaces.")]
        private string displayName = "Sample";

        [SerializeField] private LabSampleKind kind = LabSampleKind.Beaker;

        [SerializeField, Tooltip("Object shown while this sample is the selected one (for example a glowing ring).")]
        private GameObject selectionMarker;

        [SerializeField, Tooltip("Optional text that displays the sample name.")]
        private TMP_Text nameLabel;

        [Header("Scaling (Transform.localScale demonstration)")]
        [SerializeField, Min(0.1f)] private float minimumScaleFactor = 0.7f;
        [SerializeField, Min(0.1f)] private float maximumScaleFactor = 1.5f;

        private Rigidbody body;
        private XRGrabInteractable grabInteractable;
        private TemperatureSample temperatureSample;
        private Vector3 baseScale;
        private float baseMassKilograms;
        private bool initialized;

        /// <summary>Raised when the user grabs this sample with an XR interactor.</summary>
        public event Action<LabSample> Grabbed;

        public string DisplayName => displayName;
        public LabSampleKind Kind => kind;
        public Rigidbody Body => body;
        public bool IsSelected { get; private set; }

        /// <summary>Uniform scale relative to the size the sample was created with (1 = original).</summary>
        public float ScaleFactor { get; private set; } = 1f;

        /// <summary>Mass in grams, read from the Rigidbody (the same value the digital scale shows).</summary>
        public float MassGrams => body != null ? DigitalScale.KilogramsToGrams(body.mass) : 0f;

        public bool HasTemperature => temperatureSample != null;
        public float TemperatureCelsius => temperatureSample != null ? temperatureSample.TemperatureCelsius : 0f;

        private void Awake()
        {
            EnsureInitialized();
        }

        private void EnsureInitialized()
        {
            if (initialized) return;
            initialized = true;

            body = GetComponent<Rigidbody>();
            grabInteractable = GetComponent<XRGrabInteractable>();
            temperatureSample = GetComponent<TemperatureSample>();
            baseScale = transform.localScale;
            baseMassKilograms = body.mass;

            if (selectionMarker != null) selectionMarker.SetActive(false);
            if (nameLabel != null) nameLabel.SetText(displayName);
        }

        private void OnEnable()
        {
            EnsureInitialized();
            if (grabInteractable != null) grabInteractable.selectEntered.AddListener(OnGrabbed);
        }

        private void OnDisable()
        {
            if (grabInteractable != null) grabInteractable.selectEntered.RemoveListener(OnGrabbed);
        }

        private void OnGrabbed(SelectEnterEventArgs args)
        {
            Grabbed?.Invoke(this);
        }

        /// <summary>Changes the name shown on the label.</summary>
        public void Rename(string newName)
        {
            if (string.IsNullOrEmpty(newName)) return;
            EnsureInitialized();
            displayName = newName;
            name = newName;
            if (nameLabel != null) nameLabel.SetText(displayName);
        }

        /// <summary>Shows or hides the selection marker.</summary>
        public void SetSelected(bool selected)
        {
            EnsureInitialized();
            IsSelected = selected;
            if (selectionMarker != null) selectionMarker.SetActive(selected);
        }

        /// <summary>
        /// Scales the sample uniformly. The mass follows the volume (factor cubed) so a
        /// larger sample really reads heavier on the digital scale.
        /// </summary>
        public void SetScaleFactor(float factor)
        {
            EnsureInitialized();
            ScaleFactor = Mathf.Clamp(factor, minimumScaleFactor, maximumScaleFactor);
            transform.localScale = baseScale * ScaleFactor;
            body.mass = baseMassKilograms * ScaleFactor * ScaleFactor * ScaleFactor;
        }

        /// <summary>Moves the sample to a pose and cancels its motion so it settles where it was put.</summary>
        public void TeleportTo(Vector3 position, Quaternion rotation)
        {
            EnsureInitialized();
            if (!body.isKinematic)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }

            transform.SetPositionAndRotation(position, rotation);
            body.position = position;
            body.rotation = rotation;
            Physics.SyncTransforms();
        }
    }
}
