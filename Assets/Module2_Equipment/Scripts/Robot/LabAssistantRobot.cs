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
        /// <summary>The destination cannot be reached (no complete path).</summary>
        Blocked,
    }

    /// <summary>
    /// Autonomous laboratory assistant. Uses a <see cref="NavMeshAgent"/> to drive between
    /// <see cref="RobotStation"/>s on the baked NavMesh, avoiding the furniture (static obstacles
    /// baked into the NavMesh) and carved NavMeshObstacles such as the user and the equipment cart.
    /// The user can send it to a station, stop it, resume it, or start an automatic tour.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    [AddComponentMenu("VR Science Lab/Module 2/Lab Assistant Robot")]
    public class LabAssistantRobot : MonoBehaviour
    {
        [SerializeField, Tooltip("Stations the robot can visit. Index 0 is its charging dock.")]
        private RobotStation[] stations = new RobotStation[0];

        [SerializeField, Min(0f), Tooltip("Seconds the robot waits at a station during the automatic tour.")]
        private float tourWaitSeconds = 3f;

        [SerializeField, Min(10f), Tooltip("Degrees per second when aligning with the station direction after arriving.")]
        private float alignSpeed = 160f;

        [SerializeField, Tooltip("Start the automatic tour when the scene starts.")]
        private bool tourOnStart = false;

        [Tooltip("Raised when the state or the destination changes.")]
        public UnityEvent onStatusChanged = new UnityEvent();

        private NavMeshAgent agent;
        private float waitTimer;
        private bool aligning;

        public RobotState State { get; private set; } = RobotState.Idle;

        /// <summary>Index of the station the robot is heading to or parked at, or -1.</summary>
        public int DestinationIndex { get; private set; } = -1;

        /// <summary>True while the robot visits the stations one after the other on its own.</summary>
        public bool IsTouring { get; private set; }

        public int StationCount => stations.Length;
        public NavMeshAgent Agent => agent;

        /// <summary>Name of the current destination, or "none".</summary>
        public string DestinationName => DestinationIndex >= 0 && DestinationIndex < stations.Length && stations[DestinationIndex] != null
            ? stations[DestinationIndex].StationName
            : "none";

        /// <summary>Path distance left to the destination in metres (0 when not moving).</summary>
        public float RemainingDistance
        {
            get
            {
                if (agent == null || !agent.isOnNavMesh || agent.pathPending) return 0f;
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
            if (!agent.isOnNavMesh)
            {
                Debug.LogWarning("[LabAssistantRobot] The robot is not on a NavMesh. Bake the NavMesh Surface " +
                                 "(VR Science Lab > Module 2 > Bake NavMesh) so it can move.", this);
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
            }
        }

        public string GetStationName(int index)
        {
            return index >= 0 && index < stations.Length && stations[index] != null ? stations[index].StationName : string.Empty;
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

        /// <summary>Sends the robot back to station 0, its charging dock.</summary>
        public void ReturnToDock()
        {
            GoToStation(0);
        }

        /// <summary>Stops the robot where it is. The destination is kept so it can resume.</summary>
        public void Stop()
        {
            if (State != RobotState.Moving) return;
            if (agent.isOnNavMesh) agent.isStopped = true;
            SetState(RobotState.Paused);
        }

        /// <summary>Continues toward the destination after <see cref="Stop"/>.</summary>
        public void Resume()
        {
            if (State != RobotState.Paused) return;
            if (agent.isOnNavMesh) agent.isStopped = false;
            SetState(RobotState.Moving);
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
            DriveTo(NextStationIndex());
        }

        public void ToggleTour()
        {
            if (IsTouring)
            {
                IsTouring = false;
                onStatusChanged?.Invoke();
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

        private void DriveTo(int index)
        {
            if (index < 0 || index >= stations.Length || stations[index] == null)
            {
                Debug.LogWarning($"[LabAssistantRobot] No station at index {index}.", this);
                return;
            }

            if (!agent.isOnNavMesh)
            {
                Debug.LogWarning("[LabAssistantRobot] Cannot move: the robot is not on a NavMesh.", this);
                return;
            }

            if (DestinationIndex >= 0 && DestinationIndex < stations.Length && stations[DestinationIndex] != null)
            {
                stations[DestinationIndex].SetActiveTarget(false);
            }

            DestinationIndex = index;
            stations[index].SetActiveTarget(true);

            aligning = false;
            agent.isStopped = false;
            agent.updateRotation = true;
            agent.SetDestination(stations[index].StopPosition);
            SetState(RobotState.Moving);
        }

        private void UpdateMoving()
        {
            if (agent.pathPending) return;

            if (agent.pathStatus == NavMeshPathStatus.PathInvalid)
            {
                SetState(RobotState.Blocked);
                return;
            }

            if (agent.remainingDistance > agent.stoppingDistance) return;
            if (agent.hasPath && agent.velocity.sqrMagnitude > 0.0025f) return;

            // Arrived: take over the rotation to face the way the station asks for.
            agent.updateRotation = false;
            aligning = true;
            waitTimer = tourWaitSeconds;
            SetState(agent.pathStatus == NavMeshPathStatus.PathPartial ? RobotState.Blocked : RobotState.Arrived);
        }

        private void UpdateArrived()
        {
            if (aligning && DestinationIndex >= 0)
            {
                Quaternion target = stations[DestinationIndex].StopRotation;
                transform.rotation = Quaternion.RotateTowards(transform.rotation, target, alignSpeed * Time.deltaTime);
                if (Quaternion.Angle(transform.rotation, target) < 0.5f) aligning = false;
            }

            if (!IsTouring) return;

            waitTimer -= Time.deltaTime;
            if (waitTimer <= 0f) DriveTo(NextStationIndex());
        }

        private void SetState(RobotState newState)
        {
            State = newState;
            onStatusChanged?.Invoke();
        }
    }
}
