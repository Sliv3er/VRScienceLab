using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace VRScienceLab.Module2
{
    /// <summary>
    /// Virtual laboratory scale.
    /// States: <see cref="DigitalScaleState.Off"/> and <see cref="DigitalScaleState.Reading"/>.
    /// While reading, the mass shown is the sum of the Rigidbody masses (kilograms) of every
    /// object inside the weighing zone, converted to grams, minus the tare offset.
    /// No mass is invented: an empty pan reads 0 g and an object without a Rigidbody is ignored.
    /// </summary>
    [AddComponentMenu("VR Science Lab/Module 2/Digital Scale")]
    public class DigitalScale : InstrumentBase
    {
        /// <summary>Unity Rigidbody.mass is expressed in kilograms; the display uses grams.</summary>
        public const float GramsPerKilogram = 1000f;

        [Header("Digital Scale")]
        [SerializeField, Tooltip("Trigger zone above the pan. Objects (with a Rigidbody) inside it are weighed.")]
        private MeasurementZone weighingZone;

        [SerializeField, Range(0, 3), Tooltip("Fraction digits shown on the display.")]
        private int decimals = 1;

        [SerializeField, Tooltip("Start in the Reading state instead of Off.")]
        private bool startSwitchedOn = false;

        [SerializeField, Tooltip("Forget the tare offset when the scale is switched off (like a real scale).")]
        private bool clearTareOnPowerOff = true;

        [SerializeField] private bool logStateChanges = false;

        [Header("Scale events")]
        [Tooltip("Raised when the displayed mass (grams) changes.")]
        public UnityEvent<float> onMeasurementChanged = new UnityEvent<float>();

        private readonly List<Rigidbody> rigidbodyScratch = new List<Rigidbody>(8);
        private long lastReportedKey = long.MinValue;

        /// <summary>Current state.</summary>
        public DigitalScaleState State { get; private set; } = DigitalScaleState.Off;

        /// <summary>True while the scale is switched on.</summary>
        public bool IsOn => State == DigitalScaleState.Reading;

        /// <summary>Total mass on the pan in grams, before tare. Zero while off.</summary>
        public float RawMassGrams { get; private set; }

        /// <summary>Mass subtracted from the raw reading, in grams.</summary>
        public float TareOffsetGrams { get; private set; }

        /// <summary>True when a tare offset is applied.</summary>
        public bool IsTareActive => Mathf.Abs(TareOffsetGrams) > 0.0001f;

        /// <summary>The value shown on the display in grams (raw minus tare). Zero while off.</summary>
        public float DisplayedMassGrams => IsOn ? RawMassGrams - TareOffsetGrams : 0f;

        /// <summary>True when at least one measurable object is inside the weighing zone.</summary>
        public bool HasObjectOnPan => weighingZone != null && !weighingZone.IsEmpty;

        /// <summary>Fraction digits used by the display.</summary>
        public int Decimals => decimals;

        public override string CurrentStateName
        {
            get
            {
                if (State == DigitalScaleState.Off) return "OFF";
                return IsTareActive ? "READING (TARE)" : "READING";
            }
        }

        /// <summary>Converts a Rigidbody mass (kilograms) to grams.</summary>
        public static float KilogramsToGrams(float kilograms)
        {
            return kilograms * GramsPerKilogram;
        }

        protected override void Awake()
        {
            base.Awake();
            if (weighingZone == null)
            {
                Debug.LogWarning($"[{InstrumentName}] No weighing zone assigned. The scale will always read 0 g.", this);
            }
        }

        private void Start()
        {
            if (startSwitchedOn)
            {
                PowerOn();
            }
            else
            {
                ApplyOffState();
            }
        }

        private void Update()
        {
            if (!IsOn) return;
            RefreshMeasurement();
        }

        /// <summary>Switches the scale on when off, and off when on.</summary>
        public void TogglePower()
        {
            if (IsOn) PowerOff();
            else PowerOn();
        }

        /// <summary>Off -> Reading.</summary>
        public void PowerOn()
        {
            if (IsOn) return;

            State = DigitalScaleState.Reading;
            SetIndicator(true);
            RefreshMeasurement();
            NotifyStateChanged();
            if (logStateChanges) Log("Powered on.");
        }

        /// <summary>Reading -> Off. Optionally clears the tare offset.</summary>
        public void PowerOff()
        {
            if (!IsOn) return;

            State = DigitalScaleState.Off;
            if (clearTareOnPowerOff) TareOffsetGrams = 0f;
            ApplyOffState();
            NotifyStateChanged();
            if (logStateChanges) Log("Powered off.");
        }

        /// <summary>Stores the current raw mass as the tare offset so the display reads 0 g. Ignored while off.</summary>
        public void Tare()
        {
            if (!IsOn)
            {
                if (logStateChanges) Log("Tare ignored: the scale is off.");
                return;
            }

            RecomputeRawMass();
            TareOffsetGrams = RawMassGrams;
            RefreshMeasurement();
            NotifyStateChanged();
            if (logStateChanges) Log($"Tare set to {TareOffsetGrams:0.###} g.");
        }

        /// <summary>Removes the tare offset so the display shows the raw mass again.</summary>
        public void ClearTare()
        {
            if (!IsTareActive) return;

            TareOffsetGrams = 0f;
            if (IsOn)
            {
                RefreshMeasurement();
                NotifyStateChanged();
            }
        }

        /// <summary>Recomputes the mass on the pan and updates the display. Does nothing while off.</summary>
        public void RefreshMeasurement()
        {
            if (!IsOn) return;

            RecomputeRawMass();
            float grams = DisplayedMassGrams;
            ShowValue(grams, decimals, " g");

            long key = DisplayFormatter.ScaledValue(grams, decimals);
            if (key != lastReportedKey)
            {
                lastReportedKey = key;
                onMeasurementChanged?.Invoke(grams);
            }
        }

        private void RecomputeRawMass()
        {
            float kilograms = 0f;
            if (weighingZone != null)
            {
                weighingZone.CollectRigidbodies(rigidbodyScratch);
                for (int i = 0; i < rigidbodyScratch.Count; i++)
                {
                    kilograms += rigidbodyScratch[i].mass;
                }
            }

            RawMassGrams = KilogramsToGrams(kilograms);
        }

        private void ApplyOffState()
        {
            RawMassGrams = 0f;
            lastReportedKey = long.MinValue;
            ShowValueText("OFF");
            ShowStateText(CurrentStateName);
            SetIndicator(false);
        }
    }
}
