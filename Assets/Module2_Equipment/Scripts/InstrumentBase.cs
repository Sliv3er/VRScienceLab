using TMPro;
using UnityEngine;
using UnityEngine.Events;

namespace VRScienceLab.Module2
{
    /// <summary>
    /// Common presentation layer for all Module 2 instruments.
    /// Behavior (state machines, measurements) lives in the derived classes; this base
    /// class only knows how to push values, state labels and an indicator color to the
    /// instrument's displays without allocating every frame.
    /// </summary>
    public abstract class InstrumentBase : MonoBehaviour
    {
        [Header("Instrument")]
        [SerializeField, Tooltip("Name used in log messages.")]
        private string instrumentName = "Instrument";

        [Header("Presentation")]
        [SerializeField, Tooltip("TextMeshPro text that shows the measured value (mass, temperature, time).")]
        private TMP_Text valueDisplay;

        [SerializeField, Tooltip("TextMeshPro text that shows the current state (OFF, READING, HEATING...).")]
        private TMP_Text stateDisplay;

        [SerializeField, Tooltip("Optional renderer (for example a small LED sphere) tinted to show whether the instrument is active.")]
        private Renderer statusIndicator;

        [SerializeField] private Color indicatorOffColor = new Color(0.2f, 0.2f, 0.2f);
        [SerializeField] private Color indicatorActiveColor = new Color(0.1f, 1f, 0.2f);

        [Header("Events")]
        [Tooltip("Raised whenever the instrument state changes. The argument is the new state name.")]
        public UnityEvent<string> onStateChanged = new UnityEvent<string>();

        // Both property names are written so the indicator works with the Built-in pipeline
        // (_Color) and with URP (_BaseColor) materials.
        private static readonly int ColorPropertyId = Shader.PropertyToID("_Color");
        private static readonly int BaseColorPropertyId = Shader.PropertyToID("_BaseColor");

        private readonly char[] displayBuffer = new char[DisplayFormatter.BufferSize];
        private MaterialPropertyBlock propertyBlock;
        private long lastValueKey = long.MinValue;
        private string lastValueSuffix;
        private string lastValueText;
        private string lastStateText;

        /// <summary>Name used in log messages; falls back to the type name.</summary>
        public string InstrumentName => string.IsNullOrEmpty(instrumentName) ? GetType().Name : instrumentName;

        /// <summary>Human readable name of the current state, shown on the state display.</summary>
        public abstract string CurrentStateName { get; }

        /// <summary>True when a value display is assigned.</summary>
        public bool HasValueDisplay => valueDisplay != null;

        /// <summary>The text currently shown on the value display, or null when there is none.</summary>
        public string ValueDisplayText => valueDisplay != null ? valueDisplay.text : null;

        /// <summary>The text currently shown on the state display, or null when there is none.</summary>
        public string StateDisplayText => stateDisplay != null ? stateDisplay.text : null;

        protected virtual void Awake()
        {
            if (valueDisplay == null)
            {
                Debug.LogWarning($"[{InstrumentName}] No value display assigned. Readings will not be visible.", this);
            }
        }

        /// <summary>
        /// Shows a numeric value with a fixed number of decimals and a unit suffix.
        /// The display is only rewritten when the rounded value changes.
        /// </summary>
        protected void ShowValue(float value, int decimals, string suffix)
        {
            long key = DisplayFormatter.ScaledValue(value, decimals);
            if (lastValueText == null && key == lastValueKey && string.Equals(lastValueSuffix, suffix))
            {
                return;
            }

            lastValueKey = key;
            lastValueSuffix = suffix;
            lastValueText = null;

            if (valueDisplay == null) return;
            int length = DisplayFormatter.FormatFixed(displayBuffer, value, decimals, suffix);
            valueDisplay.SetCharArray(displayBuffer, 0, length);
        }

        /// <summary>Shows a stopwatch time (mm:ss.ff). Only rewritten when the hundredths change.</summary>
        protected void ShowTime(double totalSeconds)
        {
            long key = (long)System.Math.Floor(totalSeconds * 100.0);
            if (lastValueText == null && key == lastValueKey)
            {
                return;
            }

            lastValueKey = key;
            lastValueSuffix = null;
            lastValueText = null;

            if (valueDisplay == null) return;
            int length = DisplayFormatter.FormatTime(displayBuffer, totalSeconds);
            valueDisplay.SetCharArray(displayBuffer, 0, length);
        }

        /// <summary>Shows a literal text on the value display (for example "OFF").</summary>
        protected void ShowValueText(string text)
        {
            if (lastValueText != null && lastValueText == text)
            {
                return;
            }

            lastValueText = text;
            lastValueKey = long.MinValue;
            lastValueSuffix = null;

            if (valueDisplay != null) valueDisplay.SetText(text);
        }

        /// <summary>Shows a literal text on the state display. Only rewritten when it changes.</summary>
        protected void ShowStateText(string text)
        {
            if (lastStateText != null && lastStateText == text)
            {
                return;
            }

            lastStateText = text;
            if (stateDisplay != null) stateDisplay.SetText(text);
        }

        /// <summary>Tints the optional status indicator renderer.</summary>
        protected void SetIndicator(bool active)
        {
            if (statusIndicator == null) return;

            if (propertyBlock == null) propertyBlock = new MaterialPropertyBlock();
            Color color = active ? indicatorActiveColor : indicatorOffColor;
            propertyBlock.SetColor(ColorPropertyId, color);
            propertyBlock.SetColor(BaseColorPropertyId, color);
            statusIndicator.SetPropertyBlock(propertyBlock);
        }

        /// <summary>Updates the state display and raises <see cref="onStateChanged"/>.</summary>
        protected void NotifyStateChanged()
        {
            string stateName = CurrentStateName;
            ShowStateText(stateName);
            onStateChanged?.Invoke(stateName);
        }

        /// <summary>Forces the next Show* call to rewrite the displays.</summary>
        protected void InvalidateDisplayCache()
        {
            lastValueKey = long.MinValue;
            lastValueSuffix = null;
            lastValueText = null;
            lastStateText = null;
        }

        /// <summary>Logs a message prefixed with the instrument name.</summary>
        protected void Log(string message)
        {
            Debug.Log($"[{InstrumentName}] {message}", this);
        }
    }
}
