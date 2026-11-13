using System.Text;
using TMPro;
using UnityEngine;

namespace VRScienceLab.Module2
{
    /// <summary>
    /// Results interface: live readings of the four instruments, the table of recorded
    /// measurements and their statistics, plus the last detected collision.
    /// The live line is refreshed a few times per second; the table only when the log changes.
    /// </summary>
    [AddComponentMenu("VR Science Lab/Module 2/UI/Measurement Summary Panel")]
    public class MeasurementSummaryPanel : MonoBehaviour
    {
        [SerializeField] private MeasurementLog log;

        [Header("Live readings")]
        [SerializeField] private DigitalScale digitalScale;
        [SerializeField] private HotPlate hotPlate;
        [SerializeField] private StopwatchTimer stopwatch;
        [SerializeField] private Thermometer thermometer;
        [SerializeField] private TMP_Text scaleText;
        [SerializeField] private TMP_Text hotPlateText;
        [SerializeField] private TMP_Text stopwatchText;
        [SerializeField] private TMP_Text thermometerText;
        [SerializeField, Min(0.05f)] private float liveRefreshInterval = 0.25f;

        [Header("Recorded measurements")]
        [SerializeField] private TMP_Text tableText;
        [SerializeField] private TMP_Text statisticsText;

        [Header("Collision monitor")]
        [SerializeField] private TMP_Text collisionText;

        private readonly StringBuilder builder = new StringBuilder(512);
        private float timer;
        private int collisionCount;

        private void OnEnable()
        {
            if (log != null) log.onLogChanged.AddListener(RefreshTable);
            LabPhysicsObject.AnyImpact += OnImpact;
            RefreshTable();
            RefreshLive();
        }

        private void OnDisable()
        {
            if (log != null) log.onLogChanged.RemoveListener(RefreshTable);
            LabPhysicsObject.AnyImpact -= OnImpact;
        }

        private void Update()
        {
            timer -= Time.deltaTime;
            if (timer > 0f) return;
            timer = liveRefreshInterval;
            RefreshLive();
        }

        private void RefreshLive()
        {
            // The instruments already format their own displays; the summary mirrors them.
            Mirror(scaleText, digitalScale);
            Mirror(hotPlateText, hotPlate);
            Mirror(stopwatchText, stopwatch);
            Mirror(thermometerText, thermometer);
        }

        private static void Mirror(TMP_Text target, InstrumentBase instrument)
        {
            if (target == null || instrument == null) return;
            string value = instrument.ValueDisplayText ?? "-";
            if (target.text != value) target.SetText(value);
        }

        /// <summary>Rewrites the table of recorded measurements and the statistics line.</summary>
        public void RefreshTable()
        {
            if (log == null) return;

            if (tableText != null)
            {
                builder.Length = 0;
                if (log.Count == 0)
                {
                    builder.Append("<color=#7FA6B8>No measurement recorded yet. Press RECORD to store the current readings.</color>");
                }

                for (int i = 0; i < log.Count; i++)
                {
                    MeasurementLog.Entry entry = log.GetEntry(i);
                    builder.Append(entry.Number.ToString("00"));
                    builder.Append("<pos=8%>").Append(entry.SampleName);
                    builder.Append("<pos=40%>").Append(entry.HasMass ? entry.MassGrams.ToString("0.0") + " g" : "-");
                    builder.Append("<pos=58%>").Append(entry.HasTemperature ? entry.TemperatureCelsius.ToString("0.0") + " °C" : "-");
                    builder.Append("<pos=74%>").Append(entry.PlateTemperatureCelsius.ToString("0.0")).Append(" °C");
                    builder.Append("<pos=89%>").Append(FormatTime(entry.ElapsedSeconds));
                    if (i < log.Count - 1) builder.Append('\n');
                }

                tableText.SetText(builder);
            }

            if (statisticsText != null)
            {
                builder.Length = 0;
                builder.Append("Entries ").Append(log.Count).Append(" / ").Append(log.MaximumEntries);
                builder.Append("     Mean mass ").Append(log.AverageMassGrams().ToString("0.0")).Append(" g");
                if (log.TryGetMaximumTemperature(out float maximum))
                {
                    builder.Append("     Max sample temp ").Append(maximum.ToString("0.0")).Append(" °C");
                }

                statisticsText.SetText(builder);
            }
        }

        private void OnImpact(LabPhysicsObject.Impact impact)
        {
            collisionCount++;
            if (collisionText == null || impact.Source == null) return;

            builder.Length = 0;
            builder.Append("Collisions ").Append(collisionCount).Append("     Last: ")
                .Append(impact.Source.name).Append(" hit ").Append(impact.OtherName)
                .Append(" at ").Append(impact.Speed.ToString("0.0")).Append(" m/s");
            collisionText.SetText(builder);
        }

        private static string FormatTime(double seconds)
        {
            int total = (int)seconds;
            return (total / 60).ToString("00") + ":" + (total % 60).ToString("00");
        }
    }
}
