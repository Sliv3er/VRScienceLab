using UnityEngine;

namespace VRScienceLab.Module2
{
    /// <summary>
    /// Visual feedback for a <see cref="MeasurementZone"/>: a ring around the scale pan or the
    /// thermometer dock lights up while an object is inside the trigger volume.
    /// Driven by the zone's onOccupantsChanged event (OnTriggerEnter / OnTriggerExit), never by Update.
    /// </summary>
    [AddComponentMenu("VR Science Lab/Module 2/Zone Indicator")]
    public class ZoneIndicator : MonoBehaviour
    {
        [SerializeField] private MeasurementZone zone;
        [SerializeField, Tooltip("Renderer tinted when the zone is occupied (for example a thin ring).")]
        private Renderer indicatorRenderer;

        [SerializeField] private Color emptyColor = new Color(0.10f, 0.16f, 0.20f);
        [SerializeField, ColorUsage(false, true)] private Color occupiedColor = new Color(0.1f, 1.6f, 2.0f);

        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

        private MaterialPropertyBlock block;

        /// <summary>True while the indicator shows the occupied look.</summary>
        public bool ShowsOccupied { get; private set; }

        private void OnEnable()
        {
            if (zone != null) zone.onOccupantsChanged.AddListener(Refresh);
            Refresh();
        }

        private void OnDisable()
        {
            if (zone != null) zone.onOccupantsChanged.RemoveListener(Refresh);
        }

        /// <summary>Applies the color that matches the current zone content.</summary>
        public void Refresh()
        {
            ShowsOccupied = zone != null && !zone.IsEmpty;
            if (indicatorRenderer == null) return;

            if (block == null) block = new MaterialPropertyBlock();
            Color color = ShowsOccupied ? occupiedColor : emptyColor;
            Color albedo = ShowsOccupied ? new Color(0.2f, 0.9f, 1f) : emptyColor;
            block.SetColor(ColorId, albedo);
            block.SetColor(BaseColorId, albedo);
            block.SetColor(EmissionId, ShowsOccupied ? color : Color.black);
            indicatorRenderer.SetPropertyBlock(block);
        }
    }
}
