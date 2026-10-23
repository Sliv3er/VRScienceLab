using UnityEngine;
using UnityEngine.Events;

namespace VRScienceLab.Module2
{
    /// <summary>
    /// Virtual laboratory thermometer.
    /// States: <see cref="ThermometerState.Off"/> and <see cref="ThermometerState.Reading"/>.
    /// While reading, the thermometer looks for a <see cref="TemperatureSample"/>: either the one
    /// assigned manually with <see cref="SetSample"/> or the first one inside the sample zone.
    /// The reading is taken from that sample's configured temperature, so the value shown is
    /// always traceable to a sample. Without a sample the display shows an unavailable marker.
    /// </summary>
    [AddComponentMenu("VR Science Lab/Module 2/Thermometer")]
    public class Thermometer : InstrumentBase
    {
        [Header("Thermometer")]
        [SerializeField, Tooltip("Trigger zone (dock or probe tip). The first TemperatureSample inside it is measured.")]
        private MeasurementZone sampleZone;

        [SerializeField, Tooltip("Optional: a sample assigned here overrides whatever is in the sample zone.")]
        private TemperatureSample manualSample;

        [SerializeField, Range(0, 2), Tooltip("Fraction digits shown on the display.")]
        private int decimals = 1;

        [SerializeField, Min(0f), Tooltip("°C per second the reading moves toward the sample temperature. 0 = instant reading.")]
        private float responseRate = 0f;

        [SerializeField, Tooltip("Reading used as the starting point when a response rate is set.")]
        private float ambientTemperature = 20f;

        [SerializeField, Tooltip("Unit text appended to the reading.")]
        private string unitSuffix = " °C";

        [SerializeField, Tooltip("Shown in place of the number when no sample is available.")]
        private string noSampleText = "--";

        [SerializeField, Tooltip("Start in the Reading state.")]
        private bool startSwitchedOn = false;

        [SerializeField] private bool logStateChanges = false;

        [Header("Thermometer events")]
        [Tooltip("Raised when the displayed reading (°C) changes.")]
        public UnityEvent<float> onReadingChanged = new UnityEvent<float>();

        private string noSampleDisplay;
        private long lastReportedKey = long.MinValue;

        /// <summary>Current state.</summary>
        public ThermometerState State { get; private set; } = ThermometerState.Off;

        /// <summary>True while measuring.</summary>
        public bool IsOn => State == ThermometerState.Reading;

        /// <summary>The sample currently being measured, or null.</summary>
        public TemperatureSample CurrentSample { get; private set; }

        /// <summary>True when a sample is available.</summary>
        public bool HasSample => CurrentSample != null;

        /// <summary>True when <see cref="CurrentReading"/> holds a valid measurement.</summary>
        public bool HasReading { get; private set; }

        /// <summary>Latest reading in °C. Only meaningful when <see cref="HasReading"/> is true.</summary>
        public float CurrentReading { get; private set; }

        public override string CurrentStateName
        {
            get
            {
                if (State == ThermometerState.Off) return "OFF";
                return HasSample ? "READING" : "NO SAMPLE";
            }
        }

        private void OnValidate()
        {
            responseRate = Mathf.Max(0f, responseRate);
            noSampleDisplay = null;
        }

        protected override void Awake()
        {
            base.Awake();
            if (sampleZone == null && manualSample == null)
            {
                Debug.LogWarning($"[{InstrumentName}] No sample zone or manual sample assigned. The thermometer will report NO SAMPLE until SetSample() is called.", this);
            }
        }

        private void Start()
        {
            if (startSwitchedOn) StartMeasuring();
            else ApplyOffState();
        }

        private void Update()
        {
            if (!IsOn) return;

            AdvanceTime(Time.deltaTime);
            RefreshDisplay();
        }

        /// <summary>
        /// Resolves the current sample and moves the reading toward its temperature.
        /// Called from Update with Time.deltaTime; tests call it directly.
        /// </summary>
        public void AdvanceTime(float deltaSeconds)
        {
            if (!IsOn) return;

            ResolveSample(true);
            if (!HasSample)
            {
                HasReading = false;
                return;
            }

            float target = CurrentSample.TemperatureCelsius;
            if (responseRate <= 0f)
            {
                CurrentReading = target;
            }
            else
            {
                if (!HasReading) CurrentReading = ambientTemperature;
                if (deltaSeconds > 0f) CurrentReading = Mathf.MoveTowards(CurrentReading, target, responseRate * deltaSeconds);
            }

            HasReading = true;
        }

        /// <summary>Off -> Reading.</summary>
        public void StartMeasuring()
        {
            if (IsOn) return;

            State = ThermometerState.Reading;
            SetIndicator(true);
            ResolveSample(false);
            AdvanceTime(0f);
            RefreshDisplay();
            NotifyStateChanged();
            if (logStateChanges) Log(HasSample ? $"Measuring '{CurrentSample.SampleName}'." : "Measuring, no sample available.");
        }

        /// <summary>Reading -> Off. The reading is discarded.</summary>
        public void StopMeasuring()
        {
            if (!IsOn) return;

            State = ThermometerState.Off;
            ApplyOffState();
            NotifyStateChanged();
            if (logStateChanges) Log("Stopped.");
        }

        /// <summary>Starts when off, stops when reading.</summary>
        public void ToggleMeasuring()
        {
            if (IsOn) StopMeasuring();
            else StartMeasuring();
        }

        /// <summary>Stops measuring and clears the reading. Works from any state.</summary>
        public void ResetMeasurement()
        {
            if (IsOn)
            {
                StopMeasuring();
            }
            else
            {
                ApplyOffState();
                ShowStateText(CurrentStateName);
            }

            if (logStateChanges) Log("Reset.");
        }

        /// <summary>
        /// Selects a sample manually. It overrides the sample zone until <see cref="ClearManualSample"/>
        /// is called. Pass null to go back to zone detection.
        /// </summary>
        public void SetSample(TemperatureSample sample)
        {
            manualSample = sample;
            if (!IsOn) return;

            ResolveSample(true);
            AdvanceTime(0f);
            RefreshDisplay();
        }

        /// <summary>Removes the manual sample override.</summary>
        public void ClearManualSample()
        {
            SetSample(null);
        }

        /// <summary>Writes the reading and state to the displays.</summary>
        public void RefreshDisplay()
        {
            if (!IsOn)
            {
                ShowValueText("OFF");
                ShowStateText(CurrentStateName);
                return;
            }

            if (HasReading)
            {
                ShowValue(CurrentReading, decimals, unitSuffix);
                long key = DisplayFormatter.ScaledValue(CurrentReading, decimals);
                if (key != lastReportedKey)
                {
                    lastReportedKey = key;
                    onReadingChanged?.Invoke(CurrentReading);
                }
            }
            else
            {
                if (noSampleDisplay == null) noSampleDisplay = noSampleText + unitSuffix;
                ShowValueText(noSampleDisplay);
                lastReportedKey = long.MinValue;
            }

            ShowStateText(CurrentStateName);
        }

        // Picks the manual sample first, then the first sample inside the zone.
        private void ResolveSample(bool notifyOnChange)
        {
            TemperatureSample sample = manualSample;
            if (sample == null && sampleZone != null)
            {
                sampleZone.TryGetOccupantComponent(out sample);
            }

            if (sample == CurrentSample) return;

            CurrentSample = sample;
            HasReading = false;
            if (notifyOnChange) NotifyStateChanged();
            if (logStateChanges) Log(HasSample ? $"Sample '{sample.SampleName}' selected." : "Sample removed.");
        }

        private void ApplyOffState()
        {
            CurrentSample = null;
            HasReading = false;
            CurrentReading = 0f;
            lastReportedKey = long.MinValue;
            ShowValueText("OFF");
            SetIndicator(false);
        }
    }
}
