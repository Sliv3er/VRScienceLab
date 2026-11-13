using UnityEngine;
using UnityEngine.Events;

namespace VRScienceLab.Module2
{
    /// <summary>
    /// Welcome screen of the module. The session starts on the welcome view; "Begin session"
    /// swaps it for the session view (measurement summary), and "Home" brings it back.
    /// </summary>
    [AddComponentMenu("VR Science Lab/Module 2/UI/Welcome Panel")]
    public class WelcomePanel : MonoBehaviour
    {
        [SerializeField, Tooltip("Shown when the user enters the laboratory.")]
        private GameObject welcomeView;

        [SerializeField, Tooltip("Shown once the session has begun.")]
        private GameObject sessionView;

        public UnityEvent onSessionStarted = new UnityEvent();

        /// <summary>True once the user pressed "Begin session".</summary>
        public bool SessionStarted { get; private set; }

        private void Start()
        {
            ShowWelcome();
        }

        /// <summary>Hides the welcome view and shows the session view.</summary>
        public void BeginSession()
        {
            SessionStarted = true;
            Apply();
            onSessionStarted?.Invoke();
        }

        /// <summary>Returns to the welcome view.</summary>
        public void ShowWelcome()
        {
            SessionStarted = false;
            Apply();
        }

        public void Toggle()
        {
            if (SessionStarted) ShowWelcome();
            else BeginSession();
        }

        private void Apply()
        {
            if (welcomeView != null) welcomeView.SetActive(!SessionStarted);
            if (sessionView != null) sessionView.SetActive(SessionStarted);
        }
    }
}
