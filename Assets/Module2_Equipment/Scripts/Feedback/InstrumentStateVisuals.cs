using System;
using TMPro;
using UnityEngine;

namespace VRScienceLab.Module2
{
    /// <summary>
    /// Activation feedback for one instrument. Listens to <see cref="InstrumentBase.onStateChanged"/>
    /// and, without touching the instrument's own logic:
    /// - lights the display backlight and accent strips while the instrument is active,
    /// - shows a short contextual hint that matches the current state.
    /// </summary>
    [AddComponentMenu("VR Science Lab/Module 2/Instrument State Visuals")]
    public class InstrumentStateVisuals : MonoBehaviour
    {
        [Serializable]
        public struct StateHint
        {
            [Tooltip("State name as reported by the instrument (for example 'OFF', 'HEATING'). The first entry whose name is contained in the state is used.")]
            public string state;
            [Tooltip("Hint shown while the instrument is in that state.")]
            public string hint;
        }

        [SerializeField] private InstrumentBase instrument;

        [SerializeField, Tooltip("State names that count as inactive (backlight off).")]
        private string[] inactiveStates = { "OFF", "READY" };

        [SerializeField, Tooltip("Renderers that glow while the instrument is active (display backlight, accent strip).")]
        private Renderer[] glowRenderers = new Renderer[0];

        [SerializeField] private Color idleColor = new Color(0.02f, 0.05f, 0.06f);
        [SerializeField] private Color activeColor = new Color(0.03f, 0.16f, 0.20f);
        [SerializeField, ColorUsage(false, true)] private Color activeEmission = new Color(0.05f, 0.55f, 0.70f);

        [Header("Contextual hint")]
        [SerializeField] private TMP_Text hintText;
        [SerializeField] private StateHint[] hints = new StateHint[0];

        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

        private MaterialPropertyBlock block;

        /// <summary>True while the active look is applied.</summary>
        public bool IsShowingActive { get; private set; }

        private void OnEnable()
        {
            if (instrument != null) instrument.onStateChanged.AddListener(Apply);
        }

        private void Start()
        {
            if (instrument != null) Apply(instrument.CurrentStateName);
        }

        private void OnDisable()
        {
            if (instrument != null) instrument.onStateChanged.RemoveListener(Apply);
        }

        /// <summary>Applies the look and hint for <paramref name="stateName"/>.</summary>
        public void Apply(string stateName)
        {
            IsShowingActive = !IsInactive(stateName);

            if (block == null) block = new MaterialPropertyBlock();
            Color color = IsShowingActive ? activeColor : idleColor;
            block.SetColor(ColorId, color);
            block.SetColor(BaseColorId, color);
            block.SetColor(EmissionId, IsShowingActive ? activeEmission : Color.black);
            for (int i = 0; i < glowRenderers.Length; i++)
            {
                if (glowRenderers[i] != null) glowRenderers[i].SetPropertyBlock(block);
            }

            if (hintText == null) return;
            for (int i = 0; i < hints.Length; i++)
            {
                if (!string.IsNullOrEmpty(hints[i].state) && stateName != null &&
                    stateName.IndexOf(hints[i].state, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    hintText.SetText(hints[i].hint);
                    return;
                }
            }
        }

        private bool IsInactive(string stateName)
        {
            if (string.IsNullOrEmpty(stateName)) return true;
            for (int i = 0; i < inactiveStates.Length; i++)
            {
                if (string.Equals(inactiveStates[i], stateName, StringComparison.OrdinalIgnoreCase)) return true;
            }

            return false;
        }
    }
}
