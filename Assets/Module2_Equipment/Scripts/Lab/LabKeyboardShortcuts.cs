using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace VRScienceLab.Module2
{
    /// <summary>
    /// Keyboard shortcuts for testing the laboratory systems in the Editor without a headset.
    /// The instrument shortcuts (F1-F10) stay in <see cref="Module2DebugInput"/>; this component
    /// covers the samples, the robot, the search, the summary and the reset.
    /// Every shortcut calls the same public method as the matching UI button.
    /// </summary>
    [AddComponentMenu("VR Science Lab/Module 2/Lab Keyboard Shortcuts")]
    public class LabKeyboardShortcuts : MonoBehaviour
    {
        [SerializeField] private SampleManager sampleManager;
        [SerializeField] private LabAssistantRobot robot;
        [SerializeField] private LabResetController resetController;
        [SerializeField] private MeasurementLog measurementLog;
        [SerializeField] private LabDirectory directory;
        [SerializeField] private WelcomePanel welcomePanel;
        [SerializeField] private RotatableMount stopwatchMount;
        [SerializeField] private ScientistAvatar avatar;

        [SerializeField] private bool enableShortcuts = true;

#if ENABLE_INPUT_SYSTEM
        [Header("Session")]
        [SerializeField] private Key beginSessionKey = Key.Enter;
        [SerializeField] private Key resetLabKey = Key.Home;
        [SerializeField] private Key recordMeasurementKey = Key.F11;
        [SerializeField] private Key toggleAvatarViewKey = Key.F12;

        [Header("Samples")]
        [SerializeField] private Key createBeakerKey = Key.B;
        [SerializeField] private Key createTestTubeKey = Key.N;
        [SerializeField] private Key createFlaskKey = Key.M;
        [SerializeField] private Key createSampleBoxKey = Key.K;
        [SerializeField] private Key selectNextKey = Key.L;
        [SerializeField] private Key removeSelectedKey = Key.Delete;
        [SerializeField] private Key scaleUpKey = Key.PageUp;
        [SerializeField] private Key scaleDownKey = Key.PageDown;
        [SerializeField] private Key moveToScaleKey = Key.U;
        [SerializeField] private Key moveToHotPlateKey = Key.I;
        [SerializeField] private Key moveToThermometerKey = Key.O;
        [SerializeField] private Key returnToTrayKey = Key.P;

        [Header("Robot (digit keys 6-0 send it to stations 1-5)")]
        [SerializeField] private Key firstStationKey = Key.Digit6;
        [SerializeField] private Key stopResumeKey = Key.H;
        [SerializeField] private Key tourKey = Key.J;

        [Header("Search and rotation")]
        [SerializeField] private Key locateNextInstrumentKey = Key.F;
        [SerializeField] private Key locateHeaviestSampleKey = Key.V;
        [SerializeField] private Key rotateLeftKey = Key.LeftBracket;
        [SerializeField] private Key rotateRightKey = Key.RightBracket;

        private int locateIndex;
#else
        private bool warned;
#endif

        private void Update()
        {
            if (!enableShortcuts) return;

#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;
            if (!keyboard.anyKey.isPressed) return;

            if (welcomePanel != null && Pressed(keyboard, beginSessionKey)) welcomePanel.Toggle();
            if (resetController != null && Pressed(keyboard, resetLabKey)) resetController.ResetLab();
            if (measurementLog != null && Pressed(keyboard, recordMeasurementKey)) measurementLog.RecordMeasurement();
            if (avatar != null && Pressed(keyboard, toggleAvatarViewKey)) avatar.ToggleObserverView();

            if (sampleManager != null)
            {
                if (Pressed(keyboard, createBeakerKey)) sampleManager.CreateSample(1);
                if (Pressed(keyboard, createTestTubeKey)) sampleManager.CreateSample(3);
                if (Pressed(keyboard, createFlaskKey)) sampleManager.CreateSample(4);
                if (Pressed(keyboard, createSampleBoxKey)) sampleManager.CreateSample(5);
                if (Pressed(keyboard, selectNextKey)) sampleManager.SelectNext();
                if (Pressed(keyboard, removeSelectedKey)) sampleManager.RemoveSelected();
                if (Pressed(keyboard, scaleUpKey)) sampleManager.ScaleSelectedUp();
                if (Pressed(keyboard, scaleDownKey)) sampleManager.ScaleSelectedDown();
                if (Pressed(keyboard, moveToScaleKey)) sampleManager.MoveSelectedToStation(0);
                if (Pressed(keyboard, moveToHotPlateKey)) sampleManager.MoveSelectedToStation(1);
                if (Pressed(keyboard, moveToThermometerKey)) sampleManager.MoveSelectedToStation(2);
                if (Pressed(keyboard, returnToTrayKey)) sampleManager.ReturnSelectedToTray();
            }

            if (robot != null)
            {
                for (int i = 0; i < robot.StationCount && i < 5; i++)
                {
                    // Digit6..Digit9 are consecutive; Digit0 follows Digit9 in the Key enum.
                    if (Pressed(keyboard, firstStationKey + i)) robot.GoToStation(i);
                }

                if (Pressed(keyboard, stopResumeKey)) robot.ToggleStopResume();
                if (Pressed(keyboard, tourKey)) robot.ToggleTour();
            }

            if (directory != null)
            {
                if (Pressed(keyboard, locateNextInstrumentKey) && directory.InstrumentNameCount > 0)
                {
                    directory.LocateInstrument(locateIndex);
                    locateIndex = (locateIndex + 1) % directory.InstrumentNameCount;
                }

                if (Pressed(keyboard, locateHeaviestSampleKey)) directory.LocateHeaviestSample();
            }

            if (stopwatchMount != null)
            {
                if (Pressed(keyboard, rotateLeftKey)) stopwatchMount.RotateLeft();
                if (Pressed(keyboard, rotateRightKey)) stopwatchMount.RotateRight();
            }
#else
            if (!warned)
            {
                warned = true;
                Debug.LogWarning("[LabKeyboardShortcuts] The Input System backend is not active; shortcuts are disabled.", this);
            }
#endif
        }

#if ENABLE_INPUT_SYSTEM
        private static bool Pressed(Keyboard keyboard, Key key)
        {
            if (key == Key.None) return false;
            var control = keyboard[key];
            return control != null && control.wasPressedThisFrame;
        }
#endif
    }
}
