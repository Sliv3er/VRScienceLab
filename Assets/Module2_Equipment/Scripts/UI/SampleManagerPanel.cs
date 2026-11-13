using System.Text;
using TMPro;
using UnityEngine;

namespace VRScienceLab.Module2
{
    /// <summary>
    /// Sample management interface: shows how many samples exist, which one is selected and
    /// its data. The buttons of the panel call the <see cref="SampleManager"/> directly
    /// (wired in the Inspector); this component only refreshes the texts when the manager
    /// reports a change, never in Update.
    /// </summary>
    [AddComponentMenu("VR Science Lab/Module 2/UI/Sample Manager Panel")]
    public class SampleManagerPanel : MonoBehaviour
    {
        [SerializeField] private SampleManager manager;
        [SerializeField] private TMP_Text countText;
        [SerializeField] private TMP_Text selectedText;
        [SerializeField] private TMP_Text messageText;

        private readonly StringBuilder builder = new StringBuilder(160);

        private void OnEnable()
        {
            if (manager == null) return;
            manager.onSamplesChanged.AddListener(Refresh);
            manager.onMessage.AddListener(ShowMessage);
            Refresh();
            ShowMessage(manager.LastMessage);
        }

        private void OnDisable()
        {
            if (manager == null) return;
            manager.onSamplesChanged.RemoveListener(Refresh);
            manager.onMessage.RemoveListener(ShowMessage);
        }

        /// <summary>Rewrites the counter and the selected sample card.</summary>
        public void Refresh()
        {
            if (manager == null) return;

            if (countText != null)
            {
                countText.SetText("{0} / {1}", manager.SampleCount, manager.MaximumSamples);
            }

            if (selectedText == null) return;

            LabSample sample = manager.Selected;
            if (sample == null)
            {
                selectedText.SetText("No sample selected");
                return;
            }

            builder.Length = 0;
            builder.Append("<b>").Append(sample.DisplayName).Append("</b>\n");
            builder.Append("Mass  ").Append(sample.MassGrams.ToString("0")).Append(" g");
            if (sample.HasTemperature)
            {
                builder.Append("     Temp  ").Append(sample.TemperatureCelsius.ToString("0.0")).Append(" °C");
            }

            builder.Append("\nScale  x").Append(sample.ScaleFactor.ToString("0.0"));
            Vector3 position = sample.transform.position;
            builder.Append("     Pos  (").Append(position.x.ToString("0.00")).Append(", ")
                .Append(position.y.ToString("0.00")).Append(", ").Append(position.z.ToString("0.00")).Append(')');
            selectedText.SetText(builder);
        }

        private void ShowMessage(string message)
        {
            if (messageText != null) messageText.SetText(message ?? string.Empty);
        }
    }
}
