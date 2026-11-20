using UnityEngine;
using UnityEngine.AI;

namespace VRScienceLab.Module2
{
    /// <summary>
    /// Draws the route of the laboratory assistant robot on the floor while it is driving:
    /// a line from the robot along the corners of its NavMesh path to the destination.
    /// Purely visual; it only reads the robot and its NavMeshAgent.
    /// </summary>
    [RequireComponent(typeof(LineRenderer))]
    [AddComponentMenu("VR Science Lab/Module 2/Robot Path Indicator")]
    public class RobotPathIndicator : MonoBehaviour
    {
        [SerializeField] private LabAssistantRobot robot;

        [SerializeField, Min(0f), Tooltip("Height of the line above the floor (metres).")]
        private float heightAboveFloor = 0.02f;

        [SerializeField, Min(0.05f), Tooltip("Seconds between two path refreshes.")]
        private float refreshInterval = 0.2f;

        [SerializeField, Tooltip("Keep showing the route while the robot is stopped on the way.")]
        private bool showWhilePaused = true;

        private const int MaximumCorners = 32;

        private readonly Vector3[] corners = new Vector3[MaximumCorners];
        private LineRenderer line;
        private NavMeshPath path;
        private float timer;

        /// <summary>Number of points currently drawn (0 when the line is hidden).</summary>
        public int PointCount { get; private set; }

        private void Awake()
        {
            line = GetComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = 0;
            line.enabled = false;
        }

        private void OnEnable()
        {
            if (robot != null) robot.onStatusChanged.AddListener(RefreshNow);
        }

        private void OnDisable()
        {
            if (robot != null) robot.onStatusChanged.RemoveListener(RefreshNow);
        }

        private void LateUpdate()
        {
            if (robot == null) return;

            timer -= Time.deltaTime;
            if (timer > 0f) return;
            RefreshNow();
        }

        /// <summary>Recomputes the line from the robot's current path.</summary>
        public void RefreshNow()
        {
            timer = refreshInterval;
            if (line == null) line = GetComponent<LineRenderer>();

            bool show = robot != null && robot.IsOnNavMesh &&
                        (robot.State == RobotState.Moving || (showWhilePaused && robot.State == RobotState.Paused));
            if (!show)
            {
                Hide();
                return;
            }

            NavMeshAgent agent = robot.Agent;
            if (agent.pathPending)
            {
                return; // keep the previous line until the new path is ready
            }

            if (path == null) path = new NavMeshPath();
            if (!agent.CalculatePath(agent.destination, path))
            {
                Hide();
                return;
            }

            int count = path.GetCornersNonAlloc(corners);
            if (count < 2)
            {
                Hide();
                return;
            }

            line.positionCount = count;
            for (int i = 0; i < count; i++)
            {
                Vector3 point = corners[i];
                point.y += heightAboveFloor;
                line.SetPosition(i, point);
            }

            line.enabled = true;
            PointCount = count;
        }

        private void Hide()
        {
            if (line != null)
            {
                line.positionCount = 0;
                line.enabled = false;
            }

            PointCount = 0;
        }
    }
}
