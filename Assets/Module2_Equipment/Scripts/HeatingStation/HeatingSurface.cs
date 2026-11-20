using System.Collections.Generic;
using UnityEngine;

namespace VRScienceLab.Module2
{
    /// <summary>
    /// Links the heating surface of a <see cref="HotPlate"/> to the containers standing on it.
    /// A container that carries a <see cref="TemperatureSample"/> warms up while the plate is
    /// hotter than its content, so the thermometer reads a higher value after heating.
    ///
    /// Simplified, deterministic model (documented in the README):
    ///   while on the plate and plate temperature > sample temperature:
    ///       sample += heatTransferRate * dt, limited by the plate temperature and by maximumSampleTemperature
    /// Samples never cool down and nothing happens to containers without a TemperatureSample.
    /// The hot plate itself is not modified; this component only reads its temperature.
    /// </summary>
    [AddComponentMenu("VR Science Lab/Module 2/Heating Surface")]
    public class HeatingSurface : MonoBehaviour
    {
        [SerializeField, Tooltip("Hot plate whose temperature heats the containers.")]
        private HotPlate hotPlate;

        [SerializeField, Tooltip("Trigger zone just above the heating surface.")]
        private MeasurementZone zone;

        [SerializeField, Min(0f), Tooltip("Temperature increase of a sample standing on the plate, in °C per second.")]
        private float heatTransferRate = 2f;

        [SerializeField, Tooltip("Samples are never heated above this temperature (°C). 100 keeps water-based samples realistic.")]
        private float maximumSampleTemperature = 100f;

        private readonly List<Rigidbody> bodies = new List<Rigidbody>(4);

        /// <summary>Number of objects (Rigidbodies) currently standing on the heating surface.</summary>
        public int ContainerCount { get; private set; }

        /// <summary>Number of those objects whose content is currently being heated.</summary>
        public int HeatedSampleCount { get; private set; }

        /// <summary>True when at least one object stands on the heating surface.</summary>
        public bool HasContainer => ContainerCount > 0;

        public float HeatTransferRate => heatTransferRate;
        public float MaximumSampleTemperature => maximumSampleTemperature;

        private void Awake()
        {
            if (hotPlate == null) Debug.LogWarning("[HeatingSurface] No HotPlate assigned; containers will not be heated.", this);
            if (zone == null) Debug.LogWarning("[HeatingSurface] No zone assigned; containers will not be detected.", this);
        }

        private void Update()
        {
            AdvanceTime(Time.deltaTime);
        }

        /// <summary>
        /// Advances the heat transfer by <paramref name="deltaSeconds"/>.
        /// Called from Update with Time.deltaTime; tests call it directly.
        /// </summary>
        public void AdvanceTime(float deltaSeconds)
        {
            ContainerCount = 0;
            HeatedSampleCount = 0;
            if (zone == null) return;

            ContainerCount = zone.CollectRigidbodies(bodies);
            if (hotPlate == null || deltaSeconds <= 0f) return;

            float plateTemperature = hotPlate.CurrentTemperature;
            float limit = Mathf.Min(plateTemperature, maximumSampleTemperature);
            for (int i = 0; i < bodies.Count; i++)
            {
                TemperatureSample sample = bodies[i].GetComponent<TemperatureSample>();
                if (sample == null) continue;
                if (sample.TemperatureCelsius >= limit) continue;

                sample.TemperatureCelsius = Mathf.MoveTowards(sample.TemperatureCelsius, limit, heatTransferRate * deltaSeconds);
                HeatedSampleCount++;
            }
        }
    }
}
