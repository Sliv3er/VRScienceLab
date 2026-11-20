using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace VRScienceLab.Module2
{
    /// <summary>
    /// Robot control screen. Every button of the screen calls one method of this panel, which
    /// 1) records and displays the command that was pressed (proof that the UI received the click),
    /// 2) forwards it to the <see cref="LabAssistantRobot"/>,
    /// 3) displays the robot's answer (driving, arrived, stopped, or the reason it cannot move).
    /// It also shows the state, the destination, the remaining distance, whether the robot stands
    /// on the NavMesh, and highlights the button of the current destination.
    /// </summary>
    [AddComponentMenu("VR Science Lab/Module 2/UI/Robot Control Panel")]
    public class RobotControlPanel : MonoBehaviour
    {
        [SerializeField] private LabAssistantRobot robot;

        [Header("Status texts")]
        [SerializeField] private TMP_Text stateText;
        [SerializeField] private TMP_Text destinationText;
        [SerializeField] private TMP_Text distanceText;

        [SerializeField, Tooltip("Shows the last command pressed on the screen and the robot's answer.")]
        private TMP_Text commandText;

        [SerializeField, Tooltip("Shows whether the robot stands on the NavMesh.")]
        private TMP_Text navigationText;

        [Header("Destination highlight")]
        [SerializeField, Tooltip("Button backgrounds, one per station index (entries may be empty). The current destination is tinted.")]
        private Graphic[] stationButtonGraphics = new Graphic[0];

        [SerializeField] private Color activeDestinationColor = new Color(0.16f, 0.72f, 0.42f, 1f);
        [SerializeField] private Color blockedDestinationColor = new Color(0.80f, 0.28f, 0.22f, 1f);

        [SerializeField, Min(0.05f), Tooltip("Seconds between distance refreshes while the robot moves.")]
        private float refreshInterval = 0.25f;

        [SerializeField, Tooltip("Write every button press to the Console.")]
        private bool logPresses = true;

        private Color[] normalColors;
        private float timer;

        /// <summary>Label of the last command pressed on this screen.</summary>
        public string LastCommand { get; private set; } = string.Empty;

        /// <summary>Number of commands received from the buttons of this screen.</summary>
        public int CommandCount { get; private set; }

        private void Awake()
        {
            CaptureNormalColors();
            if (robot == null) Debug.LogError("[RobotControlPanel] No robot assigned: the buttons of this screen cannot do anything.", this);
        }

        private void OnEnable()
        {
            if (robot != null) robot.onStatusChanged.AddListener(Refresh);
            Refresh();
        }

        private void OnDisable()
        {
            if (robot != null) robot.onStatusChanged.RemoveListener(Refresh);
        }

        private void Update()
        {
            // The distance changes continuously, but four refreshes per second are plenty.
            if (robot == null || robot.State != RobotState.Moving) return;

            timer -= Time.deltaTime;
            if (timer > 0f) return;
            timer = refreshInterval;
            RefreshDistance();
        }

        // ------------------------------------------------------------------
        // Button handlers
        // ------------------------------------------------------------------

        /// <summary>"Go to ..." buttons: sends the robot to the station at <paramref name="stationIndex"/>.</summary>
        public void GoToStation(int stationIndex)
        {
            if (!Register("GO TO " + StationLabel(stationIndex))) return;
            robot.GoToStation(stationIndex);
        }

        /// <summary>"Stop Robot" button.</summary>
        public void StopRobot()
        {
            if (!Register("STOP ROBOT")) return;
            robot.Stop();
        }

        /// <summary>"Resume" button.</summary>
        public void ResumeRobot()
        {
            if (!Register("RESUME")) return;
            robot.Resume();
        }

        /// <summary>"Return to Starting Position" button.</summary>
        public void ReturnToStart()
        {
            if (!Register("RETURN TO STARTING POSITION")) return;
            robot.ReturnToStart();
        }

        /// <summary>"Next Station" button.</summary>
        public void GoToNextStation()
        {
            if (!Register("NEXT STATION")) return;
            robot.GoToNextStation();
        }

        /// <summary>"Auto Tour" button.</summary>
        public void ToggleTour()
        {
            if (!Register("AUTO TOUR")) return;
            robot.ToggleTour();
        }

        private string StationLabel(int stationIndex)
        {
            string stationName = robot != null ? robot.GetStationName(stationIndex) : string.Empty;
            return string.IsNullOrEmpty(stationName) ? "STATION " + stationIndex : stationName.ToUpperInvariant();
        }

        // Records the command; returns false when there is no robot to forward it to.
        private bool Register(string command)
        {
            CommandCount++;
            LastCommand = command;
            if (logPresses) Debug.Log("[RobotControlPanel] Button pressed: " + command, this);

            if (robot != null) return true;

            Debug.LogError("[RobotControlPanel] '" + command + "' ignored: no robot assigned.", this);
            if (commandText != null) commandText.SetText(command + "   >   NO ROBOT ASSIGNED");
            return false;
        }

        // ------------------------------------------------------------------
        // Display
        // ------------------------------------------------------------------

        /// <summary>Rewrites every text of the panel.</summary>
        public void Refresh()
        {
            if (robot == null) return;

            if (stateText != null)
            {
                // "Paused" is shown as STOPPED: that is what the Stop Robot button produces.
                string state = robot.State == RobotState.Paused ? "STOPPED" : robot.State.ToString().ToUpperInvariant();
                stateText.SetText(robot.IsTouring ? state + "  (TOUR)" : state);
            }

            if (destinationText != null) destinationText.SetText(robot.DestinationName);

            if (commandText != null)
            {
                string answer = robot.LastCommandAccepted ? robot.LastMessage : "FAILED: " + robot.LastMessage;
                commandText.SetText(string.IsNullOrEmpty(LastCommand) ? answer : LastCommand + "   >   " + answer);
            }

            if (navigationText != null)
            {
                navigationText.SetText(robot.IsOnNavMesh ? "NAVMESH OK" : "NOT ON NAVMESH");
            }

            RefreshDistance();
            RefreshHighlight();
        }

        private void RefreshDistance()
        {
            if (distanceText != null) distanceText.SetText("{0:1} m", robot.RemainingDistance);
        }

        private void CaptureNormalColors()
        {
            normalColors = new Color[stationButtonGraphics.Length];
            for (int i = 0; i < stationButtonGraphics.Length; i++)
            {
                normalColors[i] = stationButtonGraphics[i] != null ? stationButtonGraphics[i].color : Color.white;
            }
        }

        private void RefreshHighlight()
        {
            if (normalColors == null || normalColors.Length != stationButtonGraphics.Length) CaptureNormalColors();

            bool hasDestination = robot.State == RobotState.Moving || robot.State == RobotState.Paused ||
                                  robot.State == RobotState.Arrived || robot.State == RobotState.Blocked;
            for (int i = 0; i < stationButtonGraphics.Length; i++)
            {
                if (stationButtonGraphics[i] == null) continue;

                Color color = normalColors[i];
                if (hasDestination && i == robot.DestinationIndex)
                {
                    color = robot.State == RobotState.Blocked ? blockedDestinationColor : activeDestinationColor;
                }

                stationButtonGraphics[i].color = color;
            }
        }
    }
}
