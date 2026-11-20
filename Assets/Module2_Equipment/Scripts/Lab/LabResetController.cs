using UnityEngine;
using UnityEngine.Events;

namespace VRScienceLab.Module2
{
    /// <summary>
    /// Puts the laboratory back in its initial state: instruments off and zeroed, samples
    /// back to the initial set, loose objects and cabinet equipment back in place, cabinet
    /// doors closed, robot back at its starting position.
    /// Each instrument is reset through its own public API; they stay independent of each other.
    /// </summary>
    [AddComponentMenu("VR Science Lab/Module 2/Lab Reset Controller")]
    public class LabResetController : MonoBehaviour
    {
        [Header("Instruments")]
        [SerializeField] private DigitalScale digitalScale;
        [SerializeField] private HotPlate hotPlate;
        [SerializeField] private StopwatchTimer stopwatch;
        [SerializeField] private Thermometer thermometer;

        [Header("Other systems (optional)")]
        [SerializeField] private SampleManager sampleManager;
        [SerializeField] private LabAssistantRobot robot;
        [SerializeField] private MeasurementLog measurementLog;
        [SerializeField] private RotatableMount[] mounts = new RotatableMount[0];
        [SerializeField, Tooltip("Storage cabinet whose doors are closed by a full reset.")]
        private LaboratoryCabinet cabinet;

        [Tooltip("Raised after a reset so interfaces can refresh.")]
        public UnityEvent onLabReset = new UnityEvent();

        /// <summary>Number of full resets performed.</summary>
        public int ResetCount { get; private set; }

        /// <summary>Switches every instrument off and clears its reading.</summary>
        public void ResetInstruments()
        {
            if (digitalScale != null)
            {
                digitalScale.ClearTare();
                digitalScale.PowerOff();
            }

            if (hotPlate != null)
            {
                hotPlate.SwitchOff();
                hotPlate.ResetTemperature();
            }

            if (stopwatch != null) stopwatch.ResetTimer();

            if (thermometer != null)
            {
                thermometer.StopMeasuring();
                thermometer.ResetMeasurement();
            }

            for (int i = 0; i < mounts.Length; i++)
            {
                if (mounts[i] != null) mounts[i].ResetRotation();
            }
        }

        /// <summary>Returns the weights and loose props to their start poses.</summary>
        public void ResetLooseObjects()
        {
            // One-off search at reset time; samples are handled by the SampleManager.
            LabPhysicsObject[] objects = FindObjectsByType<LabPhysicsObject>(FindObjectsSortMode.None);
            for (int i = 0; i < objects.Length; i++)
            {
                if (objects[i].GetComponent<LabSample>() == null) objects[i].ResetPose();
            }
        }

        /// <summary>Full reset: instruments, samples, loose objects, robot and measurement log.</summary>
        public void ResetLab()
        {
            ResetInstruments();
            ResetLooseObjects();
            if (sampleManager != null) sampleManager.ResetSamples();
            if (cabinet != null) cabinet.CloseDoors();
            if (robot != null) robot.ReturnToStart();
            if (measurementLog != null) measurementLog.Clear();

            ResetCount++;
            onLabReset?.Invoke();
        }
    }
}
