using UnityEngine;
using UnityEngine.Events;

namespace VRScienceLab.Module2
{
    /// <summary>
    /// Virtual laboratory hot plate.
    /// States: <see cref="HotPlateState.Off"/>, <see cref="HotPlateState.Heating"/> and
    /// <see cref="HotPlateState.MaxTemperatureReached"/>.
    ///
    /// Temperature model (deliberately simple, documented in the README):
    ///   switched on : T += heatingRate * dt, clamped to maximumTemperature
    ///   switched off: T -= coolingRate * dt, clamped to initialTemperature (coolingRate 0 holds T)
    /// </summary>
    [AddComponentMenu("VR Science Lab/Module 2/Hot Plate")]
    public class HotPlate : InstrumentBase
    {
        [Header("Hot Plate")]
        [SerializeField, Tooltip("Temperature in °C at start and after Reset.")]
        private float initialTemperature = 20f;

        [SerializeField, Tooltip("The temperature never rises above this value (°C).")]
        private float maximumTemperature = 300f;

        [SerializeField, Min(0f), Tooltip("Temperature increase per second while heating (°C/s).")]
        private float heatingRate = 10f;

        [SerializeField, Min(0f), Tooltip("Temperature decrease per second while off, toward the initial temperature (°C/s). 0 holds the temperature.")]
        private float coolingRate = 5f;

        [SerializeField, Range(0, 2), Tooltip("Fraction digits shown on the display.")]
        private int decimals = 1;

        [SerializeField, Tooltip("Unit text appended to the reading.")]
        private string unitSuffix = " °C";

        [SerializeField, Tooltip("Start switched on.")]
        private bool startSwitchedOn = false;

        [SerializeField] private bool logStateChanges = false;

        [Header("Heating indicator (optional)")]
        [SerializeField, Tooltip("Renderer of the heating element. Its emission color is blended from cold to hot.")]
        private Renderer heatingElementRenderer;

        [SerializeField] private Color elementColdColor = Color.black;
        [SerializeField] private Color elementHotColor = new Color(1f, 0.25f, 0.05f);

        [SerializeField, Tooltip("Optional light that brightens with the temperature.")]
        private Light heatingLight;

        [SerializeField, Min(0f)] private float heatingLightMaxIntensity = 1.5f;

        [Header("Hot plate events")]
        [Tooltip("Raised once each time the maximum temperature is reached.")]
        public UnityEvent onMaximumTemperatureReached = new UnityEvent();

        private static readonly int EmissionColorPropertyId = Shader.PropertyToID("_EmissionColor");

        private MaterialPropertyBlock elementBlock;

        /// <summary>Current state.</summary>
        public HotPlateState State { get; private set; } = HotPlateState.Off;

        /// <summary>True while the plate is switched on (Heating or MaxTemperatureReached).</summary>
        public bool IsSwitchedOn => State != HotPlateState.Off;

        /// <summary>Current temperature in °C.</summary>
        public float CurrentTemperature { get; private set; }

        public float InitialTemperature => initialTemperature;
        public float MaximumTemperature => maximumTemperature;
        public float HeatingRate => heatingRate;
        public float CoolingRate => coolingRate;

        /// <summary>0 at the initial temperature, 1 at the maximum temperature.</summary>
        public float HeatFraction
        {
            get
            {
                if (maximumTemperature <= initialTemperature) return IsSwitchedOn ? 1f : 0f;
                return Mathf.InverseLerp(initialTemperature, maximumTemperature, CurrentTemperature);
            }
        }

        public override string CurrentStateName
        {
            get
            {
                switch (State)
                {
                    case HotPlateState.Heating:
                        return "HEATING";
                    case HotPlateState.MaxTemperatureReached:
                        return "MAX TEMP";
                    default:
                        return CurrentTemperature > initialTemperature + 0.05f ? "OFF (COOLING)" : "OFF";
                }
            }
        }

        private void OnValidate()
        {
            maximumTemperature = Mathf.Max(maximumTemperature, initialTemperature);
            heatingRate = Mathf.Max(0f, heatingRate);
            coolingRate = Mathf.Max(0f, coolingRate);
            heatingLightMaxIntensity = Mathf.Max(0f, heatingLightMaxIntensity);
        }

        protected override void Awake()
        {
            base.Awake();
            if (maximumTemperature < initialTemperature)
            {
                Debug.LogWarning($"[{InstrumentName}] Maximum temperature was below the initial temperature; clamping it.", this);
                maximumTemperature = initialTemperature;
            }

            CurrentTemperature = initialTemperature;
        }

        private void Start()
        {
            if (startSwitchedOn) SwitchOn();
            SetIndicator(IsSwitchedOn);
            RefreshDisplay();
            UpdateHeatVisuals();
        }

        private void Update()
        {
            AdvanceTime(Time.deltaTime);
            RefreshDisplay();
        }

        /// <summary>
        /// Advances the temperature model by <paramref name="deltaSeconds"/>.
        /// Called from Update with Time.deltaTime; tests call it directly.
        /// </summary>
        public void AdvanceTime(float deltaSeconds)
        {
            if (deltaSeconds <= 0f) return;

            if (IsSwitchedOn)
            {
                if (CurrentTemperature < maximumTemperature)
                {
                    CurrentTemperature = Mathf.Min(CurrentTemperature + heatingRate * deltaSeconds, maximumTemperature);
                }

                if (CurrentTemperature >= maximumTemperature && State != HotPlateState.MaxTemperatureReached)
                {
                    State = HotPlateState.MaxTemperatureReached;
                    NotifyStateChanged();
                    onMaximumTemperatureReached?.Invoke();
                    if (logStateChanges) Log($"Maximum temperature {maximumTemperature:0.#} °C reached.");
                }
            }
            else if (coolingRate > 0f && CurrentTemperature > initialTemperature)
            {
                CurrentTemperature = Mathf.Max(CurrentTemperature - coolingRate * deltaSeconds, initialTemperature);
            }

            UpdateHeatVisuals();
        }

        /// <summary>Off -> Heating (or MaxTemperatureReached when already at the maximum).</summary>
        public void SwitchOn()
        {
            if (IsSwitchedOn) return;

            State = CurrentTemperature >= maximumTemperature ? HotPlateState.MaxTemperatureReached : HotPlateState.Heating;
            SetIndicator(true);
            NotifyStateChanged();
            if (logStateChanges) Log("Switched on.");
        }

        /// <summary>Heating/MaxTemperatureReached -> Off. Heating stops immediately.</summary>
        public void SwitchOff()
        {
            if (!IsSwitchedOn) return;

            State = HotPlateState.Off;
            SetIndicator(false);
            NotifyStateChanged();
            if (logStateChanges) Log("Switched off.");
        }

        /// <summary>Switches on when off, and off when on.</summary>
        public void TogglePower()
        {
            if (IsSwitchedOn) SwitchOff();
            else SwitchOn();
        }

        /// <summary>Restores the initial temperature. The on/off state is kept.</summary>
        public void ResetTemperature()
        {
            CurrentTemperature = initialTemperature;
            if (State == HotPlateState.MaxTemperatureReached && maximumTemperature > initialTemperature)
            {
                State = HotPlateState.Heating;
            }

            NotifyStateChanged();
            RefreshDisplay();
            UpdateHeatVisuals();
            if (logStateChanges) Log($"Temperature reset to {initialTemperature:0.#} °C.");
        }

        /// <summary>Writes the temperature and state to the displays.</summary>
        public void RefreshDisplay()
        {
            ShowValue(CurrentTemperature, decimals, unitSuffix);
            ShowStateText(CurrentStateName);
        }

        private void UpdateHeatVisuals()
        {
            float fraction = HeatFraction;

            if (heatingElementRenderer != null)
            {
                if (elementBlock == null) elementBlock = new MaterialPropertyBlock();
                elementBlock.SetColor(EmissionColorPropertyId, Color.Lerp(elementColdColor, elementHotColor, fraction));
                heatingElementRenderer.SetPropertyBlock(elementBlock);
            }

            if (heatingLight != null)
            {
                heatingLight.intensity = fraction * heatingLightMaxIntensity;
                heatingLight.enabled = fraction > 0.001f;
            }
        }
    }
}
