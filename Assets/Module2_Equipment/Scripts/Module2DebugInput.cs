using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace VRScienceLab.Module2
{
    /// <summary>
    /// Editor/desktop debugging helper. It is not needed in VR.
    /// - Keyboard shortcuts call the instrument methods directly.
    /// - A left mouse click casts a ray from the camera and presses the <see cref="InstrumentButton"/> it hits.
    /// Requires the Input System package with the Input System backend active (ENABLE_INPUT_SYSTEM).
    /// </summary>
    [AddComponentMenu("VR Science Lab/Module 2/Debug Input (keyboard + mouse)")]
    public class Module2DebugInput : MonoBehaviour
    {
        [Header("Instruments")]
        [SerializeField] private DigitalScale digitalScale;
        [SerializeField] private HotPlate hotPlate;
        [SerializeField] private StopwatchTimer stopwatch;
        [SerializeField] private Thermometer thermometer;

        [Header("Keyboard")]
        [SerializeField] private bool enableKeyboardShortcuts = true;
#if ENABLE_INPUT_SYSTEM
        [SerializeField] private Key scalePowerKey = Key.F1;
        [SerializeField] private Key scaleTareKey = Key.F2;
        [SerializeField] private Key hotPlateOnKey = Key.F3;
        [SerializeField] private Key hotPlateOffKey = Key.F4;
        [SerializeField] private Key hotPlateResetKey = Key.F5;
        [SerializeField] private Key stopwatchStartKey = Key.F6;
        [SerializeField] private Key stopwatchPauseKey = Key.F7;
        [SerializeField] private Key stopwatchResetKey = Key.F8;
        [SerializeField] private Key thermometerToggleKey = Key.F9;
        [SerializeField] private Key thermometerResetKey = Key.F10;
#endif

        [Header("Mouse")]
        [SerializeField, Tooltip("Left click presses the InstrumentButton under the cursor.")]
        private bool enableMouseClicks = true;

        [SerializeField, Tooltip("Ignore mouse clicks while a real XR headset is active.")]
        private bool disableMouseWhenXRDeviceActive = true;

        [SerializeField, Tooltip("Camera used for the click ray. Defaults to Camera.main.")]
        private Camera rayCamera;

        [SerializeField, Min(0.1f)] private float maxClickDistance = 10f;
        [SerializeField] private LayerMask clickLayers = ~0;

#if !ENABLE_INPUT_SYSTEM
        private bool warnedMissingInputSystem;
#endif

        private void Update()
        {
#if ENABLE_INPUT_SYSTEM
            if (enableKeyboardShortcuts) HandleKeyboard();
            if (enableMouseClicks) HandleMouse();
#else
            if (!warnedMissingInputSystem)
            {
                warnedMissingInputSystem = true;
                Debug.LogWarning("[Module2DebugInput] The Input System backend is not active (Project Settings > Player > Active Input Handling). Keyboard/mouse debugging is disabled.", this);
            }
#endif
        }

#if ENABLE_INPUT_SYSTEM
        private void HandleKeyboard()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (digitalScale != null)
            {
                if (WasPressed(keyboard, scalePowerKey)) digitalScale.TogglePower();
                if (WasPressed(keyboard, scaleTareKey)) digitalScale.Tare();
            }

            if (hotPlate != null)
            {
                if (WasPressed(keyboard, hotPlateOnKey)) hotPlate.SwitchOn();
                if (WasPressed(keyboard, hotPlateOffKey)) hotPlate.SwitchOff();
                if (WasPressed(keyboard, hotPlateResetKey)) hotPlate.ResetTemperature();
            }

            if (stopwatch != null)
            {
                if (WasPressed(keyboard, stopwatchStartKey)) stopwatch.StartTimer();
                if (WasPressed(keyboard, stopwatchPauseKey)) stopwatch.PauseTimer();
                if (WasPressed(keyboard, stopwatchResetKey)) stopwatch.ResetTimer();
            }

            if (thermometer != null)
            {
                if (WasPressed(keyboard, thermometerToggleKey)) thermometer.ToggleMeasuring();
                if (WasPressed(keyboard, thermometerResetKey)) thermometer.ResetMeasurement();
            }
        }

        private static bool WasPressed(Keyboard keyboard, Key key)
        {
            if (key == Key.None) return false;
            var control = keyboard[key];
            return control != null && control.wasPressedThisFrame;
        }

        private void HandleMouse()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null || !mouse.leftButton.wasPressedThisFrame) return;
            if (disableMouseWhenXRDeviceActive && UnityEngine.XR.XRSettings.isDeviceActive) return;

            Camera camera = rayCamera != null ? rayCamera : Camera.main;
            if (camera == null) return;

            Vector2 position = mouse.position.ReadValue();
            Ray ray = camera.ScreenPointToRay(new Vector3(position.x, position.y, 0f));
            if (!Physics.Raycast(ray, out RaycastHit hit, maxClickDistance, clickLayers, QueryTriggerInteraction.Ignore)) return;

            InstrumentButton button = hit.collider.GetComponentInParent<InstrumentButton>();
            if (button != null) button.Press();
        }
#endif
    }
}
