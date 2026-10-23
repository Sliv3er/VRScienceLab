using UnityEngine;

namespace VRScienceLab.Module2
{
    /// <summary>
    /// Marks an object (beaker, flask, block) as something the <see cref="Thermometer"/> can measure.
    /// The temperature is a configured value, not a physical simulation: the thermometer reads
    /// exactly what is set here. Other scripts may change it at runtime through <see cref="TemperatureCelsius"/>.
    /// </summary>
    [AddComponentMenu("VR Science Lab/Module 2/Temperature Sample")]
    public class TemperatureSample : MonoBehaviour
    {
        [SerializeField, Tooltip("Name shown in logs (for example 'Water, room temperature').")]
        private string sampleName = "Sample";

        [SerializeField, Tooltip("Temperature reported to a thermometer, in degrees Celsius.")]
        private float temperatureCelsius = 20f;

        /// <summary>Display name of the sample.</summary>
        public string SampleName => string.IsNullOrEmpty(sampleName) ? name : sampleName;

        /// <summary>Configured temperature in degrees Celsius.</summary>
        public float TemperatureCelsius
        {
            get => temperatureCelsius;
            set => temperatureCelsius = value;
        }

        /// <summary>Sets the configured temperature (usable from UnityEvents).</summary>
        public void SetTemperature(float celsius)
        {
            temperatureCelsius = celsius;
        }
    }
}
