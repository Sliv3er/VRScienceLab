using UnityEngine;
using UnityEngine.Events;

namespace VRScienceLab.Module2
{
    /// <summary>
    /// One physical piece of equipment that houses two independent instruments: the
    /// <see cref="HotPlate"/> (heating surface and temperature display) and the
    /// <see cref="StopwatchTimer"/> (timer display and controls).
    ///
    /// This component deliberately contains no heating or timing logic. Each instrument keeps
    /// its own script, state machine, display and buttons; the station only
    /// - gives other systems one place to find both instruments,
    /// - drives the shared "station active" light,
    /// - offers station-level conveniences (switch everything off, reset both).
    /// </summary>
    [AddComponentMenu("VR Science Lab/Module 2/Combined Heating Station")]
    public class CombinedHeatingStation : MonoBehaviour
    {
        [Header("Instruments housed in this station")]
        [SerializeField] private HotPlate hotPlate;
        [SerializeField] private StopwatchTimer stopwatch;

        [Header("Shared status light (optional)")]
        [SerializeField, Tooltip("Renderers lit while the heater is on or the timer is running (for example the strip along the front edge).")]
        private Renderer[] activityRenderers = new Renderer[0];

        [SerializeField] private Color idleColor = new Color(0.02f, 0.05f, 0.06f);
        [SerializeField] private Color activeColor = new Color(0.03f, 0.16f, 0.20f);
        [SerializeField, ColorUsage(false, true)] private Color activeEmission = new Color(0.05f, 0.55f, 0.70f);
        [SerializeField, ColorUsage(false, true), Tooltip("Emission used while the heater is switched on.")]
        private Color heatingEmission = new Color(0.9f, 0.35f, 0.05f);

        [Header("Events")]
        [Tooltip("Raised when the heater or the timer changes state. The argument is the combined status text.")]
        public UnityEvent<string> onStatusChanged = new UnityEvent<string>();

        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

        private MaterialPropertyBlock block;

        /// <summary>The heating instrument of the station.</summary>
        public HotPlate HotPlate => hotPlate;

        /// <summary>The timing instrument of the station.</summary>
        public StopwatchTimer Stopwatch => stopwatch;

        /// <summary>True while the heater is switched on.</summary>
        public bool IsHeating => hotPlate != null && hotPlate.IsSwitchedOn;

        /// <summary>True while the timer is running.</summary>
        public bool IsTiming => stopwatch != null && stopwatch.IsRunning;

        /// <summary>True while either instrument is active.</summary>
        public bool IsActive => IsHeating || IsTiming;

        /// <summary>Combined status, for example "HEATER HEATING  |  TIMER RUNNING".</summary>
        public string StatusSummary
        {
            get
            {
                string heater = hotPlate != null ? hotPlate.CurrentStateName : "MISSING";
                string timer = stopwatch != null ? stopwatch.CurrentStateName : "MISSING";
                return "HEATER " + heater + "  |  TIMER " + timer;
            }
        }

        private void Awake()
        {
            if (hotPlate == null) Debug.LogWarning("[CombinedHeatingStation] No HotPlate assigned.", this);
            if (stopwatch == null) Debug.LogWarning("[CombinedHeatingStation] No StopwatchTimer assigned.", this);
        }

        private void OnEnable()
        {
            if (hotPlate != null) hotPlate.onStateChanged.AddListener(OnInstrumentStateChanged);
            if (stopwatch != null) stopwatch.onStateChanged.AddListener(OnInstrumentStateChanged);
        }

        private void Start()
        {
            ApplyActivityLight();
        }

        private void OnDisable()
        {
            if (hotPlate != null) hotPlate.onStateChanged.RemoveListener(OnInstrumentStateChanged);
            if (stopwatch != null) stopwatch.onStateChanged.RemoveListener(OnInstrumentStateChanged);
        }

        private void OnInstrumentStateChanged(string stateName)
        {
            ApplyActivityLight();
            onStatusChanged?.Invoke(StatusSummary);
        }

        /// <summary>Safety control: stops heating and pauses the timer. Readings are kept.</summary>
        public void SwitchEverythingOff()
        {
            if (hotPlate != null) hotPlate.SwitchOff();
            if (stopwatch != null) stopwatch.PauseTimer();
        }

        /// <summary>Switches the heater off, restores its initial temperature and clears the timer.</summary>
        public void ResetStation()
        {
            if (hotPlate != null)
            {
                hotPlate.SwitchOff();
                hotPlate.ResetTemperature();
            }

            if (stopwatch != null) stopwatch.ResetTimer();
        }

        private void ApplyActivityLight()
        {
            if (activityRenderers == null || activityRenderers.Length == 0) return;

            if (block == null) block = new MaterialPropertyBlock();
            bool active = IsActive;
            Color color = active ? activeColor : idleColor;
            block.SetColor(ColorId, color);
            block.SetColor(BaseColorId, color);
            block.SetColor(EmissionId, !active ? Color.black : (IsHeating ? heatingEmission : activeEmission));
            for (int i = 0; i < activityRenderers.Length; i++)
            {
                if (activityRenderers[i] != null) activityRenderers[i].SetPropertyBlock(block);
            }
        }
    }
}
