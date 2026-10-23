namespace VRScienceLab.Module2
{
    // Shared state definitions for the Module 2 instruments.
    //
    // Every instrument follows the same convention:
    //   * a public read-only `State` property typed with one of the enums below,
    //   * a `CurrentStateName` string shown on the instrument's state display,
    //   * an `onStateChanged` UnityEvent raised with that name (see InstrumentBase).
    // Transitions are only performed through the instrument's public methods, which
    // ignore requests that are not valid in the current state (for example pausing a
    // stopwatch that is not running).

    /// <summary>States of the <see cref="DigitalScale"/>.</summary>
    public enum DigitalScaleState
    {
        /// <summary>Switched off; the display shows OFF and no mass is measured.</summary>
        Off,
        /// <summary>Switched on; the display shows the mass currently on the pan (minus tare).</summary>
        Reading
    }

    /// <summary>States of the <see cref="HotPlate"/>.</summary>
    public enum HotPlateState
    {
        /// <summary>Switched off; the temperature is held or cools toward the initial temperature.</summary>
        Off,
        /// <summary>Switched on and rising toward the maximum temperature.</summary>
        Heating,
        /// <summary>Switched on and clamped at the configured maximum temperature.</summary>
        MaxTemperatureReached
    }

    /// <summary>States of the <see cref="StopwatchTimer"/>.</summary>
    public enum StopwatchState
    {
        /// <summary>Not started, elapsed time is zero.</summary>
        Idle,
        /// <summary>Accumulating elapsed time.</summary>
        Running,
        /// <summary>Stopped with the elapsed time preserved.</summary>
        Paused
    }

    /// <summary>States of the <see cref="Thermometer"/>.</summary>
    public enum ThermometerState
    {
        /// <summary>Switched off; no reading is shown.</summary>
        Off,
        /// <summary>Measuring; shows the sample temperature, or an unavailable marker when no sample is present.</summary>
        Reading
    }
}
