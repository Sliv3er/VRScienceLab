using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;

namespace VRScienceLab.Module2
{
    /// <summary>What the laboratory assistant robot is doing.</summary>
    public enum RobotState
    {
        /// <summary>Standing still with no destination.</summary>
        Idle,
        /// <summary>Driving to a station along a NavMesh path.</summary>
        Moving,
        /// <summary>Stopped by the user on the way; the destination is kept.</summary>
        Paused,
        /// <summary>Parked at the requested station.</summary>
        Arrived,
        /// <summary>The destination cannot be reached (no path, or the path ends too far from it).</summary>
        Blocked,
    }

    /// <summary>
    /// Autonomous laboratory assistant. Uses a <see cref="NavMeshAgent"/> to drive between
    /// <see cref="RobotStation"/>s on the baked NavMesh, avoiding the furniture (static obstacles
    /// baked into the NavMesh) and carved NavMeshObstacles such as the user and the equipment cart.
    /// The user can send it to a station, stop it, resume it, send it back to its starting
    /// position, or start an automatic tour.
    ///
    /// Every command reports what happened through <see cref="LastMessage"/> and
    /// <see cref="onStatusChanged"/>, including the reason when a command cannot be carried out
    /// (no NavMesh under the robot, no path...), so a control screen never fails silently.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    [AddComponentMenu("VR Science Lab/Module 2/Lab Assistant Robot")]
    public class LabAssistantRobot : MonoBehaviour
    {
        /// <summary>Index of the station the robot starts from and returns to.</summary>
        public const int StartStationIndex = 0;

        [SerializeField, Tooltip("Stations the robot can visit. Index 0 is its starting position (charging dock).")]
        private RobotStation[] stations = new RobotStation[0];

        [SerializeField, Min(0f), Tooltip("Seconds the robot waits at a station during the automatic tour.")]
        private float tourWaitSeconds = 3f;

        [SerializeField, Min(10f), Tooltip("Degrees per second when aligning with the station direction after arriving.")]
        private float alignSpeed = 160f;

        [SerializeField, Tooltip("Start the automatic tour when the scene starts.")]
        private bool tourOnStart = false;

        [Header("Navigation robustness")]
        [SerializeField, Min(0.1f), Tooltip("If the robot is not on the NavMesh it is moved to the nearest NavMesh point within this distance (metres).")]
        private float navMeshSnapDistance = 2f;

        [SerializeField, Min(0f), Tooltip("A station that is not on the NavMesh (for example because the user stands on it) is replaced by the nearest reachable point within this distance (metres).")]
        private float destinationSnapDistance = 1.5f;

        [SerializeField, Min(0.05f), Tooltip("When the path ends before the station, the robot still counts as arrived if it is closer than this (metres); otherwise it reports Blocked.")]
        private float arrivalTolerance = 1.2f;

        [SerializeField, Min(0.5f), Tooltip("Seconds without movement before the robot re-plans its path, then reports Blocked.")]
        private float stuckTimeout = 4f;

        [SerializeField, Tooltip("Write every command and its result to the Console.")]
        private bool logCommands = true;

        [Tooltip("Raised when the state, the destination or the last message changes.")]
        public UnityEvent onStatusChanged = new UnityEvent();

        private NavMeshAgent agent;
        private Vector3 currentTarget;
        private float waitTimer;
        private float stuckTimer;
        private bool repathAttempted;
        private bool aligning;
        private bool destinationAdjusted;

        public RobotState State { get; private set; } = RobotState.Idle;

        /// <summary>Index of the station the robot is heading to or parked at, or -1.</summary>
        public int DestinationIndex { get; private set; } = -1;

        /// <summary>True while the robot visits the stations one after the other on its own.</summary>
        public bool IsTouring { get; private set; }

        public int StationCount => stations.Length;

        public NavMeshAgent Agent
        {
            get
            {
                if (agent == null) agent = GetComponent<NavMeshAgent>();
                return agent;
            }
        }

        /// <summary>True when the agent exists, is enabled and stands on a NavMesh.</summary>
        public bool IsOnNavMesh => Agent != null && agent.enabled && agent.isOnNavMesh;

        /// <summary>Result of the last command or state change, in plain words.</summary>
        public string LastMessage { get; private set; } = "Ready";

        /// <summary>False when the last command could not be carried out.</summary>
        public bool LastCommandAccepted { get; private set; } = true;

        /// <summary>Name of the current destination, or "none".</summary>
        public string DestinationName => DestinationIndex >= 0 && DestinationIndex < stations.Length && stations[DestinationIndex] != null
            ? stations[DestinationIndex].StationName
            : "none";

        /// <summary>Path distance left to the destination in metres (0 when not moving).</summary>
        public float RemainingDistance
        {
            get
            {
                if (!IsOnNavMesh || agent.pathPending) return 0f;
                if (State != RobotState.Moving && State != RobotState.Paused) return 0f;
                float distance = agent.remainingDistance;
                return float.IsInfinity(distance) ? 0f : distance;
            }
        }

        /// <summary>Current speed in metres per second.</summary>
        public float Speed => agent != null ? agent.velocity.magnitude : 0f;

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
        }

        private void Start()
        {
            if (!EnsureAgentReady(out string problem))
            {
                Report(false, "Navigation unavailable: " + problem);
            }

            if (tourOnStart) StartTour();
        }

        private void Update()
        {
            switch (State)
            {
                case RobotState.Moving:
                    UpdateMoving();
                    break;
                case RobotState.Arrived:
                    UpdateArrived();
                    break;
                case RobotState.Blocked:
                    UpdateBlocked();
                    break;
            }
        }

        public string GetStationName(int index)
        {
            return index >= 0 && index < stations.Length && stations[index] != null ? stations[index].StationName : string.Empty;
        }

        public RobotStation GetStation(int index)
        {
            return index >= 0 && index < stations.Length ? stations[index] : null;
        }

        /// <summary>Index of the station called <paramref name="stationName"/> (case-insensitive), or -1.</summary>
        public int FindStationIndex(string stationName)
        {
            for (int i = 0; i < stations.Length; i++)
            {
                if (stations[i] != null && string.Equals(stations[i].StationName, stationName, System.StringComparison.OrdinalIgnoreCase)) return i;
            }

            return -1;
        }

        // ------------------------------------------------------------------
        // Commands
        // ------------------------------------------------------------------

        /// <summary>Sends the robot to the station at <paramref name="index"/>. Cancels the automatic tour.</summary>
        public void GoToStation(int index)
        {
            IsTouring = false;
            DriveTo(index);
        }

        /// <summary>Sends the robot to the station after the current one (wraps around).</summary>
        public void GoToNextStation()
        {
            IsTouring = false;
            DriveTo(NextStationIndex());
        }

        /// <summary>Sends the robot back to its starting position (station 0, the charging dock).</summary>
        public void ReturnToStart()
        {
            GoToStation(StartStationIndex);
        }

        /// <summary>Same as <see cref="ReturnToStart"/> (kept for existing callers).</summary>
        public void ReturnToDock()
        {
            ReturnToStart();
        }

        /// <summary>
        /// Stops the robot where it is and cancels the automatic tour. The destination is kept so
        /// <see cref="Resume"/> can continue the trip.
        /// </summary>
        public void Stop()
        {
            bool wasTouring = IsTouring;
            IsTouring = false;

            if (State == RobotState.Moving)
            {
                if (IsOnNavMesh)
                {
                    agent.isStopped = true;
                    agent.velocity = Vector3.zero;
                }

                SetState(RobotState.Paused, true, "Stopped on the way to " + DestinationName);
                return;
            }

            Report(true, wasTouring ? "Tour cancelled" : "The robot is already stopped");
        }

        /// <summary>Continues toward the destination after <see cref="Stop"/>.</summary>
        public void Resume()
        {
            if (State != RobotState.Paused)
            {
                Report(true, "Nothing to resume");
                return;
            }

            if (!EnsureAgentReady(out string problem))
            {
                Report(false, "Cannot resume: " + problem);
                return;
            }

            agent.isStopped = false;
            stuckTimer = 0f;
            SetState(RobotState.Moving, true, "Driving to " + DestinationName);
        }

        /// <summary>Stop when moving, resume when paused.</summary>
        public void ToggleStopResume()
        {
            if (State == RobotState.Moving) Stop();
            else if (State == RobotState.Paused) Resume();
        }

        /// <summary>The robot visits every station in order, waiting a moment at each one.</summary>
        public void StartTour()
        {
            IsTouring = true;
            if (!DriveTo(NextStationIndex())) IsTouring = false;
        }

        public void ToggleTour()
        {
            if (IsTouring)
            {
                IsTouring = false;
                Report(true, "Tour cancelled");
            }
            else
            {
                StartTour();
            }
        }

        // ------------------------------------------------------------------
        // Navigation
        // ------------------------------------------------------------------

        private int NextStationIndex()
        {
            if (stations.Length == 0) return -1;
            return (DestinationIndex + 1 + stations.Length) % stations.Length;
        }

        /// <summary>
        /// Makes sure the NavMeshAgent can be used: present, enabled and standing on a NavMesh.
        /// An agent that was created away from the NavMesh is moved onto the nearest NavMesh point.
        /// </summary>
        private bool EnsureAgentReady(out string problem)
        {
            if (agent == null) agent = GetComponent<NavMeshAgent>();
            if (agent == null)
            {
                problem = "the robot has no NavMeshAgent";
                return false;
            }

            if (!agent.enabled) agent.enabled = true;

            if (!agent.isOnNavMesh && NavMesh.SamplePosition(transform.position, out NavMeshHit hit, navMeshSnapDistance, NavMesh.AllAreas))
            {
                // An agent only attaches to the NavMesh after a Warp or after being re-enabled on it.
                if (!agent.Warp(hit.position) || !agent.isOnNavMesh)
                {
                    agent.enabled = false;
                    transform.position = hit.position;
                    agent.enabled = true;
                }
            }

            if (agent.isOnNavMesh)
            {
                problem = null;
                return true;
            }

            problem = "the robot is not on a NavMesh (bake the NavMesh Surface: VR Science Lab > Module 2 > Bake NavMesh)";
            return false;
        }

        private bool DriveTo(int index)
        {
            if (index < 0 || index >= stations.Length || stations[index] == null)
            {
                Report(false, $"No station at index {index}");
                return false;
            }

            string stationName = stations[index].StationName;
            if (!EnsureAgentReady(out string problem))
            {
                Report(false, $"Cannot drive to {stationName}: {problem}");
                return false;
            }

            // The requested stop point may be off the NavMesh at this moment (the user or the
            // equipment cart stands on it): aim for the nearest point the robot can stand on.
            Vector3 target = stations[index].StopPosition;
            destinationAdjusted = false;
            if (destinationSnapDistance > 0f && NavMesh.SamplePosition(target, out NavMeshHit hit, destinationSnapDistance, agent.areaMask))
            {
                destinationAdjusted = HorizontalDistance(hit.position, target) > 0.1f;
                target = hit.position;
            }

            agent.isStopped = false;
            agent.updateRotation = true;
            if (!agent.SetDestination(target))
            {
                SetActiveMarker(index);
                DestinationIndex = index;
                SetState(RobotState.Blocked, false, $"No path to {stationName}");
                return false;
            }

            SetActiveMarker(index);
            DestinationIndex = index;
            currentTarget = target;
            aligning = false;
            stuckTimer = 0f;
            repathAttempted = false;
            SetState(RobotState.Moving, true, "Driving to " + stationName);
            return true;
        }

        private void SetActiveMarker(int index)
        {
            if (DestinationIndex >= 0 && DestinationIndex < stations.Length && stations[DestinationIndex] != null)
            {
                stations[DestinationIndex].SetActiveTarget(false);
            }

            if (index >= 0 && index < stations.Length && stations[index] != null)
            {
                stations[index].SetActiveTarget(true);
            }
        }

        private void UpdateMoving()
        {
            if (!IsOnNavMesh)
            {
                SetState(RobotState.Blocked, false, "The robot left the NavMesh");
                return;
            }

            if (agent.pathPending) return;

            if (agent.pathStatus == NavMeshPathStatus.PathInvalid)
            {
                waitTimer = tourWaitSeconds;
                SetState(RobotState.Blocked, false, "No path to " + DestinationName);
                return;
            }

            if (agent.remainingDistance > agent.stoppingDistance)
            {
                // Still travelling. Watch for a robot that does not make progress any more.
                if (agent.velocity.sqrMagnitude < 0.0004f) stuckTimer += Time.deltaTime;
                else stuckTimer = 0f;

                if (stuckTimer > stuckTimeout)
                {
                    stuckTimer = 0f;
                    if (!repathAttempted)
                    {
                        repathAttempted = true;
                        agent.ResetPath();
                        agent.SetDestination(currentTarget);
                    }
                    else
                    {
                        agent.isStopped = true;
                        waitTimer = tourWaitSeconds;
                        SetState(RobotState.Blocked, false, "Blocked on the way to " + DestinationName);
                    }
                }

                return;
            }

            if (agent.hasPath && agent.velocity.sqrMagnitude > 0.0025f) return;

            // End of the path. A partial path that ends far from the station is a blocked route.
            float miss = HorizontalDistance(transform.position, stations[DestinationIndex].StopPosition);
            waitTimer = tourWaitSeconds;
            if (agent.pathStatus == NavMeshPathStatus.PathPartial && miss > arrivalTolerance)
            {
                SetState(RobotState.Blocked, false, $"Route blocked {miss:0.0} m before {DestinationName}");
                return;
            }

            // Arrived: take over the rotation to face the way the station asks for.
            agent.updateRotation = false;
            aligning = true;
            bool nearby = destinationAdjusted || miss > 0.3f;
            SetState(RobotState.Arrived, true, (nearby ? "Arrived next to " : "Arrived at ") + DestinationName);
        }

        private void UpdateArrived()
        {
            if (aligning && DestinationIndex >= 0 && stations[DestinationIndex] != null)
            {
                Quaternion target = stations[DestinationIndex].StopRotation;
                transform.rotation = Quaternion.RotateTowards(transform.rotation, target, alignSpeed * Time.deltaTime);
                if (Quaternion.Angle(transform.rotation, target) < 0.5f) aligning = false;
            }

            if (!IsTouring) return;

            waitTimer -= Time.deltaTime;
            if (waitTimer <= 0f && !DriveTo(NextStationIndex())) IsTouring = false;
        }

        private void UpdateBlocked()
        {
            // During a tour a blocked station is skipped after the usual wait.
            if (!IsTouring) return;

            waitTimer -= Time.deltaTime;
            if (waitTimer <= 0f && !DriveTo(NextStationIndex())) IsTouring = false;
        }

        private static float HorizontalDistance(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x;
            float dz = a.z - b.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        private void SetState(RobotState newState, bool accepted, string message)
        {
            State = newState;
            Report(accepted, message);
        }

        private void Report(bool accepted, string message)
        {
            LastCommandAccepted = accepted;
            LastMessage = message;

            if (!accepted) Debug.LogWarning("[LabAssistantRobot] " + message, this);
            else if (logCommands) Debug.Log("[LabAssistantRobot] " + message, this);

            onStatusChanged?.Invoke();
        }
    }
}
