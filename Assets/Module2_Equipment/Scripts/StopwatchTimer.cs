using UnityEngine;

namespace VRScienceLab.Module2
{
    /// <summary>
    /// Laboratory stopwatch.
    /// States: <see cref="StopwatchState.Idle"/>, <see cref="StopwatchState.Running"/>, <see cref="StopwatchState.Paused"/>.
    /// Elapsed time accumulates Time.deltaTime (or unscaled delta time) only while Running, so it
    /// never advances after Pause or Reset. The display shows mm:ss.ff.
    /// </summary>
    [AddComponentMenu("VR Science Lab/Module 2/Stopwatch Timer")]
    public class StopwatchTimer : InstrumentBase
    {
        [Header("Stopwatch")]
        [SerializeField, Tooltip("Use Time.unscaledDeltaTime so the stopwatch ignores Time.timeScale.")]
        private bool useUnscaledTime = false;

        [SerializeField] private bool logStateChanges = false;

        /// <summary>Current state.</summary>
        public StopwatchState State { get; private set; } = StopwatchState.Idle;

        /// <summary>True while time is accumulating.</summary>
        public bool IsRunning => State == StopwatchState.Running;

        /// <summary>Accumulated time in seconds.</summary>
        public double ElapsedSeconds { get; private set; }

        public override string CurrentStateName
        {
            get
            {
                switch (State)
                {
                    case StopwatchState.Running:
                        return "RUNNING";
                    case StopwatchState.Paused:
                        return "PAUSED";
                    default:
                        return "IDLE";
                }
            }
        }

        private void Start()
        {
            SetIndicator(false);
            RefreshDisplay();
            ShowStateText(CurrentStateName);
        }

        private void Update()
        {
            if (!IsRunning) return;

            AdvanceTime(useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime);
            RefreshDisplay();
        }

        /// <summary>Adds <paramref name="deltaSeconds"/> to the elapsed time, only while Running.</summary>
        public void AdvanceTime(float deltaSeconds)
        {
            if (!IsRunning || deltaSeconds <= 0f) return;
            ElapsedSeconds += deltaSeconds;
        }

        /// <summary>Idle/Paused -> Running. Ignored when already running.</summary>
        public void StartTimer()
        {
            if (IsRunning) return;

            State = StopwatchState.Running;
            SetIndicator(true);
            NotifyStateChanged();
            if (logStateChanges) Log("Started.");
        }

        /// <summary>Running -> Paused, keeping the elapsed time. Ignored when not running.</summary>
        public void PauseTimer()
        {
            if (!IsRunning) return;

            State = StopwatchState.Paused;
            SetIndicator(false);
            RefreshDisplay();
            NotifyStateChanged();
            if (logStateChanges) Log($"Paused at {ElapsedSeconds:0.00} s.");
        }

        /// <summary>Starts when idle or paused, pauses when running (single-button operation).</summary>
        public void ToggleStartPause()
        {
            if (IsRunning) PauseTimer();
            else StartTimer();
        }

        /// <summary>Any state -> Idle with zero elapsed time.</summary>
        public void ResetTimer()
        {
            bool stateChanged = State != StopwatchState.Idle;
            ElapsedSeconds = 0;
            State = StopwatchState.Idle;
            SetIndicator(false);
            RefreshDisplay();

            if (stateChanged) NotifyStateChanged();
            else ShowStateText(CurrentStateName);

            if (logStateChanges) Log("Reset.");
        }

        /// <summary>Writes the elapsed time to the value display.</summary>
        public void RefreshDisplay()
        {
            ShowTime(ElapsedSeconds);
        }
    }
}
