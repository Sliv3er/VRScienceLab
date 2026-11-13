using TMPro;
using UnityEngine;

namespace VRScienceLab.Module2
{
    /// <summary>
    /// Shows a string raised by a UnityEvent&lt;string&gt; (search report, sample feedback...)
    /// on a TextMeshPro text. Keeps the systems that raise the messages free of UI references.
    /// </summary>
    [AddComponentMenu("VR Science Lab/Module 2/UI/Status Text Display")]
    public class StatusTextDisplay : MonoBehaviour
    {
        [SerializeField] private TMP_Text target;
        [SerializeField, Tooltip("Text shown before the first message arrives.")]
        private string initialText = "";

        private void Awake()
        {
            if (target == null) target = GetComponent<TMP_Text>();
            if (target != null && !string.IsNullOrEmpty(initialText)) target.SetText(initialText);
        }

        /// <summary>Displays <paramref name="message"/>.</summary>
        public void Show(string message)
        {
            if (target != null) target.SetText(message ?? string.Empty);
        }
    }
}
