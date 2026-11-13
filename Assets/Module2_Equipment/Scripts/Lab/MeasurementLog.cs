using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace VRScienceLab.Module2
{
    /// <summary>
    /// Stores the measurements the user records during the session.
    /// It reads the four instruments through their public properties only, so the
    /// instruments know nothing about the log (no dependency in that direction).
    /// </summary>
    [AddComponentMenu("VR Science Lab/Module 2/Measurement Log")]
    public class MeasurementLog : MonoBehaviour
    {
        /// <summary>One recorded line of the summary.</summary>
        public struct Entry
        {
            public int Number;
            public string SampleName;
            public bool HasMass;
            public float MassGrams;
            public bool HasTemperature;
            public float TemperatureCelsius;
            public float PlateTemperatureCelsius;
            public double ElapsedSeconds;
        }

        [Header("Instruments read when a measurement is recorded")]
        [SerializeField] private DigitalScale digitalScale;
        [SerializeField] private HotPlate hotPlate;
        [SerializeField] private StopwatchTimer stopwatch;
        [SerializeField] private Thermometer thermometer;

        [SerializeField, Tooltip("Optional: gives the name of the selected sample.")]
        private SampleManager sampleManager;

        [SerializeField, Min(1)] private int maximumEntries = 8;

        [Tooltip("Raised when an entry is added or the log is cleared.")]
        public UnityEvent onLogChanged = new UnityEvent();

        private readonly List<Entry> entries = new List<Entry>(8);
        private int nextNumber = 1;

        public int Count => entries.Count;
        public int MaximumEntries => maximumEntries;

        public Entry GetEntry(int index)
        {
            return entries[index];
        }

        /// <summary>Captures what the instruments show right now as one summary line.</summary>
        public void RecordMeasurement()
        {
            var entry = new Entry
            {
                Number = nextNumber++,
                SampleName = sampleManager != null && sampleManager.Selected != null ? sampleManager.Selected.DisplayName : "-",
                HasMass = digitalScale != null && digitalScale.IsOn,
                MassGrams = digitalScale != null ? digitalScale.DisplayedMassGrams : 0f,
                HasTemperature = thermometer != null && thermometer.IsOn && thermometer.HasReading,
                TemperatureCelsius = thermometer != null ? thermometer.CurrentReading : 0f,
                PlateTemperatureCelsius = hotPlate != null ? hotPlate.CurrentTemperature : 0f,
                ElapsedSeconds = stopwatch != null ? stopwatch.ElapsedSeconds : 0.0,
            };

            if (entries.Count >= maximumEntries) entries.RemoveAt(0);
            entries.Add(entry);
            onLogChanged?.Invoke();
        }

        /// <summary>Removes every recorded line.</summary>
        public void Clear()
        {
            entries.Clear();
            nextNumber = 1;
            onLogChanged?.Invoke();
        }

        /// <summary>Average of the recorded masses in grams (0 when none).</summary>
        public float AverageMassGrams()
        {
            float sum = 0f;
            int count = 0;
            for (int i = 0; i < entries.Count; i++)
            {
                if (!entries[i].HasMass) continue;
                sum += entries[i].MassGrams;
                count++;
            }

            return count > 0 ? sum / count : 0f;
        }

        /// <summary>Highest recorded sample temperature in °C (false when none).</summary>
        public bool TryGetMaximumTemperature(out float maximum)
        {
            maximum = float.MinValue;
            bool any = false;
            for (int i = 0; i < entries.Count; i++)
            {
                if (!entries[i].HasTemperature) continue;
                any = true;
                if (entries[i].TemperatureCelsius > maximum) maximum = entries[i].TemperatureCelsius;
            }

            return any;
        }
    }
}
