using UnityEngine;

namespace VRScienceLab.Module2
{
    /// <summary>
    /// A place the laboratory assistant robot can be sent to.
    /// The transform position is where the robot stops; its forward axis is where it faces.
    /// </summary>
    [AddComponentMenu("VR Science Lab/Module 2/Robot Station")]
    public class RobotStation : MonoBehaviour
    {
        [SerializeField] private string stationName = "Station";

        [SerializeField, Tooltip("Optional floor marker lit while the robot is heading to or parked at this station.")]
        private Renderer marker;

        [SerializeField] private Color idleColor = new Color(0.10f, 0.16f, 0.20f);
        [SerializeField, ColorUsage(false, true)] private Color activeColor = new Color(0.1f, 1.4f, 1.8f);

        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

        private MaterialPropertyBlock block;

        public string StationName => stationName;
        public Vector3 StopPosition => transform.position;
        public Quaternion StopRotation => transform.rotation;

        /// <summary>Lights or dims the floor marker.</summary>
        public void SetActiveTarget(bool active)
        {
            if (marker == null) return;
            if (block == null) block = new MaterialPropertyBlock();
            Color albedo = active ? new Color(0.2f, 0.9f, 1f) : idleColor;
            block.SetColor(ColorId, albedo);
            block.SetColor(BaseColorId, albedo);
            block.SetColor(EmissionId, active ? activeColor : Color.black);
            marker.SetPropertyBlock(block);
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.1f, 0.8f, 1f, 0.8f);
            Gizmos.DrawWireSphere(transform.position, 0.2f);
            Gizmos.DrawLine(transform.position, transform.position + transform.forward * 0.4f);
        }
    }
}
