using TMPro;
using UnityEngine;

namespace VRScienceLab.Module2
{
    /// <summary>
    /// Robot navigation interface: shows the assistant's state, destination and remaining
    /// distance. The station / stop / resume / tour buttons call <see cref="LabAssistantRobot"/>
    /// directly (wired in the Inspector).
    /// </summary>
    [AddComponentMenu("VR Science Lab/Module 2/UI/Robot Control Panel")]
    public class RobotControlPanel : MonoBehaviour
    {
        [SerializeField] private LabAssistantRobot robot;
        [SerializeField] private TMP_Text stateText;
        [SerializeField] private TMP_Text destinationText;
        [SerializeField] private TMP_Text distanceText;

        [SerializeField, Min(0.05f), Tooltip("Seconds between distance refreshes while the robot moves.")]
        private float refreshInterval = 0.25f;

        private float timer;

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

        /// <summary>Rewrites every text of the panel.</summary>
        public void Refresh()
        {
            if (robot == null) return;

            if (stateText != null)
            {
                string state = robot.State.ToString().ToUpperInvariant();
                stateText.SetText(robot.IsTouring ? state + "  (TOUR)" : state);
            }

            if (destinationText != null) destinationText.SetText(robot.DestinationName);
            RefreshDistance();
        }

        private void RefreshDistance()
        {
            if (distanceText != null) distanceText.SetText("{0:1} m", robot.RemainingDistance);
        }
    }
}
