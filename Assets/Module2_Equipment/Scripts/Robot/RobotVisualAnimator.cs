using TMPro;
using UnityEngine;

namespace VRScienceLab.Module2
{
    /// <summary>
    /// Procedural animation of the laboratory assistant robot. It only reads the robot's
    /// state and speed: wheels turn with the distance travelled, the body leans and hovers
    /// slightly, the head looks toward the destination, and the status ring changes color.
    /// Navigation itself is entirely in <see cref="LabAssistantRobot"/>.
    /// </summary>
    [AddComponentMenu("VR Science Lab/Module 2/Robot Visual Animator")]
    public class RobotVisualAnimator : MonoBehaviour
    {
        [SerializeField] private LabAssistantRobot robot;

        [Header("Parts")]
        [SerializeField] private Transform[] wheels = new Transform[0];
        [SerializeField, Min(0.01f)] private float wheelRadius = 0.07f;
        [SerializeField, Tooltip("Part that bobs and leans (everything above the chassis).")]
        private Transform body;
        [SerializeField] private Transform head;
        [SerializeField, Tooltip("Renderers tinted with the state color (status ring, eyes).")]
        private Renderer[] statusRenderers = new Renderer[0];
        [SerializeField] private TMP_Text statusText;

        [Header("Motion")]
        [SerializeField, Min(0f)] private float bobHeight = 0.006f;
        [SerializeField, Min(0f)] private float bobFrequency = 1.1f;
        [SerializeField, Min(0f)] private float leanDegreesPerSpeed = 4f;
        [SerializeField, Min(0f)] private float headTurnSpeed = 140f;
        [SerializeField, Range(0f, 80f)] private float headMaximumYaw = 55f;

        [Header("State colors")]
        [SerializeField, ColorUsage(false, true)] private Color idleColor = new Color(0.15f, 0.9f, 1.3f);
        [SerializeField, ColorUsage(false, true)] private Color movingColor = new Color(0.2f, 1.6f, 0.5f);
        [SerializeField, ColorUsage(false, true)] private Color pausedColor = new Color(1.8f, 1.1f, 0.1f);
        [SerializeField, ColorUsage(false, true)] private Color blockedColor = new Color(2.0f, 0.2f, 0.1f);

        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

        private MaterialPropertyBlock block;
        private Vector3 bodyRestPosition;
        private Quaternion bodyRestRotation;
        private Quaternion headRestRotation;
        private float headYaw;
        private float lean;
        private float time;

        private void Awake()
        {
            if (body != null)
            {
                bodyRestPosition = body.localPosition;
                bodyRestRotation = body.localRotation;
            }

            if (head != null) headRestRotation = head.localRotation;
        }

        private void OnEnable()
        {
            if (robot != null) robot.onStatusChanged.AddListener(RefreshStatus);
            RefreshStatus();
        }

        private void OnDisable()
        {
            if (robot != null) robot.onStatusChanged.RemoveListener(RefreshStatus);
        }

        private void Update()
        {
            if (robot == null) return;

            float delta = Time.deltaTime;
            float speed = robot.Speed;
            time += delta;

            // Wheels: angle = distance / radius.
            if (speed > 0.001f)
            {
                float degrees = speed * delta / wheelRadius * Mathf.Rad2Deg;
                for (int i = 0; i < wheels.Length; i++)
                {
                    if (wheels[i] != null) wheels[i].Rotate(degrees, 0f, 0f, Space.Self);
                }
            }

            if (body != null)
            {
                lean = Mathf.Lerp(lean, speed * leanDegreesPerSpeed, 1f - Mathf.Exp(-6f * delta));
                float bob = Mathf.Sin(time * bobFrequency * 2f * Mathf.PI) * bobHeight;
                body.localPosition = bodyRestPosition + new Vector3(0f, bob, 0f);
                body.localRotation = bodyRestRotation * Quaternion.Euler(lean, 0f, 0f);
            }

            if (head != null)
            {
                float targetYaw = 0f;
                if (robot.State == RobotState.Moving && robot.Agent != null && robot.Agent.hasPath)
                {
                    Vector3 toTarget = robot.Agent.steeringTarget - transform.position;
                    toTarget.y = 0f;
                    if (toTarget.sqrMagnitude > 0.01f)
                    {
                        targetYaw = Mathf.Clamp(Vector3.SignedAngle(transform.forward, toTarget, Vector3.up), -headMaximumYaw, headMaximumYaw);
                    }
                }
                else if (robot.State == RobotState.Arrived || robot.State == RobotState.Idle)
                {
                    // Slow scan left and right while parked.
                    targetYaw = Mathf.Sin(time * 0.6f) * 25f;
                }

                headYaw = Mathf.MoveTowards(headYaw, targetYaw, headTurnSpeed * delta);
                head.localRotation = headRestRotation * Quaternion.Euler(0f, headYaw, 0f);
            }
        }

        /// <summary>Updates the status ring color and the chest text.</summary>
        public void RefreshStatus()
        {
            if (robot == null) return;

            Color color;
            switch (robot.State)
            {
                case RobotState.Moving: color = movingColor; break;
                case RobotState.Paused: color = pausedColor; break;
                case RobotState.Blocked: color = blockedColor; break;
                default: color = idleColor; break;
            }

            if (block == null) block = new MaterialPropertyBlock();
            Color albedo = new Color(Mathf.Clamp01(color.r), Mathf.Clamp01(color.g), Mathf.Clamp01(color.b));
            block.SetColor(ColorId, albedo);
            block.SetColor(BaseColorId, albedo);
            block.SetColor(EmissionId, color);
            for (int i = 0; i < statusRenderers.Length; i++)
            {
                if (statusRenderers[i] != null) statusRenderers[i].SetPropertyBlock(block);
            }

            if (statusText != null)
            {
                switch (robot.State)
                {
                    case RobotState.Moving: statusText.SetText(robot.DestinationName); break;
                    case RobotState.Paused: statusText.SetText("PAUSED"); break;
                    case RobotState.Blocked: statusText.SetText("BLOCKED"); break;
                    case RobotState.Arrived: statusText.SetText(robot.DestinationName); break;
                    default: statusText.SetText("READY"); break;
                }
            }
        }
    }
}
