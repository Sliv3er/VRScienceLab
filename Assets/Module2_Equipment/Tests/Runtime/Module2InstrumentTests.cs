using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace VRScienceLab.Module2.Tests
{
    /// <summary>
    /// PlayMode tests for the Module 2 instruments. Run them from Window > General > Test Runner > PlayMode.
    /// They exercise the state machines and the measurement logic directly (no displays, no XR input).
    /// Each test advances time explicitly through the public AdvanceTime methods, so the results are deterministic.
    /// </summary>
    public class Module2InstrumentTests
    {
        private readonly List<GameObject> created = new List<GameObject>();

        private T Create<T>(string name) where T : Component
        {
            var go = new GameObject(name);
            created.Add(go);
            return go.AddComponent<T>();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in created)
            {
                if (go != null) Object.Destroy(go);
            }

            created.Clear();
        }

        // ---------------------------------------------------------------- DisplayFormatter

        [Test]
        public void DisplayFormatter_FormatsFixedDecimals()
        {
            Assert.AreEqual("250.0 g", DisplayFormatter.FormatFixedToString(250f, 1, " g"));
            Assert.AreEqual("0.0 g", DisplayFormatter.FormatFixedToString(0f, 1, " g"));
            Assert.AreEqual("-12.35 g", DisplayFormatter.FormatFixedToString(-12.345f, 2, " g"));
            Assert.AreEqual("300 °C", DisplayFormatter.FormatFixedToString(299.96f, 0, " °C"));
            Assert.AreEqual("-- °C", DisplayFormatter.FormatFixedToString(float.NaN, 1, " °C"));
        }

        [Test]
        public void DisplayFormatter_FormatsStopwatchTime()
        {
            Assert.AreEqual("00:00.00", DisplayFormatter.FormatTimeToString(0));
            Assert.AreEqual("00:01.50", DisplayFormatter.FormatTimeToString(1.5));
            Assert.AreEqual("01:01.23", DisplayFormatter.FormatTimeToString(61.23));
            Assert.AreEqual("125:00.00", DisplayFormatter.FormatTimeToString(7500));
        }

        // ---------------------------------------------------------------- Hot plate

        [UnityTest]
        public IEnumerator HotPlate_StartsOffAtInitialTemperature()
        {
            HotPlate plate = Create<HotPlate>("HotPlate");
            yield return null;

            Assert.AreEqual(HotPlateState.Off, plate.State);
            Assert.AreEqual(plate.InitialTemperature, plate.CurrentTemperature, 0.0001f);
            Assert.AreEqual("OFF", plate.CurrentStateName);
        }

        [UnityTest]
        public IEnumerator HotPlate_HeatsAtConfiguredRateAndClampsAtMaximum()
        {
            HotPlate plate = Create<HotPlate>("HotPlate");
            yield return null;

            plate.SwitchOn();
            Assert.AreEqual(HotPlateState.Heating, plate.State);

            plate.AdvanceTime(1f);
            Assert.AreEqual(plate.InitialTemperature + plate.HeatingRate, plate.CurrentTemperature, 0.0001f);

            plate.AdvanceTime(10000f);
            Assert.AreEqual(plate.MaximumTemperature, plate.CurrentTemperature, 0.0001f);
            Assert.AreEqual(HotPlateState.MaxTemperatureReached, plate.State);

            plate.AdvanceTime(5f);
            Assert.LessOrEqual(plate.CurrentTemperature, plate.MaximumTemperature);
        }

        [UnityTest]
        public IEnumerator HotPlate_StopsHeatingWhenSwitchedOffAndResets()
        {
            HotPlate plate = Create<HotPlate>("HotPlate");
            yield return null;

            plate.SwitchOn();
            plate.AdvanceTime(2f);
            float heated = plate.CurrentTemperature;
            Assert.Greater(heated, plate.InitialTemperature);

            plate.SwitchOff();
            Assert.AreEqual(HotPlateState.Off, plate.State);
            plate.AdvanceTime(1f);
            Assert.LessOrEqual(plate.CurrentTemperature, heated, "Temperature must not rise while off.");

            plate.ResetTemperature();
            Assert.AreEqual(plate.InitialTemperature, plate.CurrentTemperature, 0.0001f);

            // Repeated presses are harmless.
            plate.SwitchOff();
            plate.SwitchOn();
            plate.SwitchOn();
            Assert.AreEqual(HotPlateState.Heating, plate.State);
        }

        // ---------------------------------------------------------------- Stopwatch

        [UnityTest]
        public IEnumerator Stopwatch_StartPauseResumeReset()
        {
            StopwatchTimer stopwatch = Create<StopwatchTimer>("Stopwatch");
            yield return null;

            Assert.AreEqual(StopwatchState.Idle, stopwatch.State);
            Assert.AreEqual(0, stopwatch.ElapsedSeconds, 0.0001);

            stopwatch.AdvanceTime(1f);
            Assert.AreEqual(0, stopwatch.ElapsedSeconds, 0.0001, "Idle stopwatch must not accumulate time.");

            stopwatch.StartTimer();
            Assert.AreEqual(StopwatchState.Running, stopwatch.State);
            stopwatch.AdvanceTime(1.5f);
            Assert.AreEqual(1.5, stopwatch.ElapsedSeconds, 0.0001);

            stopwatch.PauseTimer();
            Assert.AreEqual(StopwatchState.Paused, stopwatch.State);
            stopwatch.AdvanceTime(3f);
            Assert.AreEqual(1.5, stopwatch.ElapsedSeconds, 0.0001, "Paused stopwatch must keep its elapsed time.");

            stopwatch.StartTimer();
            stopwatch.AdvanceTime(0.5f);
            Assert.AreEqual(2.0, stopwatch.ElapsedSeconds, 0.0001);

            stopwatch.ResetTimer();
            Assert.AreEqual(StopwatchState.Idle, stopwatch.State);
            Assert.AreEqual(0, stopwatch.ElapsedSeconds, 0.0001);
            stopwatch.AdvanceTime(1f);
            Assert.AreEqual(0, stopwatch.ElapsedSeconds, 0.0001, "Reset stopwatch must not run.");

            // Pausing when not running is ignored.
            stopwatch.PauseTimer();
            Assert.AreEqual(StopwatchState.Idle, stopwatch.State);
        }

        [UnityTest]
        public IEnumerator Stopwatch_AccumulatesFrameTimeWhileRunning()
        {
            StopwatchTimer stopwatch = Create<StopwatchTimer>("Stopwatch");
            yield return null;

            stopwatch.StartTimer();
            float start = Time.time;
            yield return new WaitForSeconds(0.25f);
            float expected = Time.time - start;

            Assert.AreEqual(expected, stopwatch.ElapsedSeconds, 0.05, "Elapsed time should follow Unity's time system.");
        }

        // ---------------------------------------------------------------- Digital scale

        [UnityTest]
        public IEnumerator DigitalScale_ConvertsRigidbodyMassToGramsAndTares()
        {
            Assert.AreEqual(250f, DigitalScale.KilogramsToGrams(0.25f), 0.0001f);

            MeasurementZone zone = CreateZone("WeighingZone");
            DigitalScale scale = CreateWithField<DigitalScale>("DigitalScale", "weighingZone", zone);
            yield return null;

            Assert.AreEqual(DigitalScaleState.Off, scale.State);
            Assert.AreEqual(0f, scale.DisplayedMassGrams, 0.0001f);

            scale.PowerOn();
            Assert.AreEqual(DigitalScaleState.Reading, scale.State);
            scale.RefreshMeasurement();
            Assert.AreEqual(0f, scale.DisplayedMassGrams, 0.0001f, "Empty pan must read zero.");

            GameObject weight = CreateWeight("Weight", 0.25f);
            zone.AddOccupant(weight.GetComponent<Collider>());
            scale.RefreshMeasurement();
            Assert.AreEqual(250f, scale.DisplayedMassGrams, 0.0001f);

            scale.Tare();
            Assert.AreEqual(0f, scale.DisplayedMassGrams, 0.0001f);
            Assert.IsTrue(scale.IsTareActive);

            GameObject second = CreateWeight("Weight2", 0.1f);
            zone.AddOccupant(second.GetComponent<Collider>());
            scale.RefreshMeasurement();
            Assert.AreEqual(100f, scale.DisplayedMassGrams, 0.0001f);

            scale.ClearTare();
            Assert.AreEqual(350f, scale.DisplayedMassGrams, 0.0001f);

            scale.PowerOff();
            Assert.AreEqual(DigitalScaleState.Off, scale.State);
            Assert.AreEqual(0f, scale.DisplayedMassGrams, 0.0001f);
        }

        [UnityTest]
        public IEnumerator DigitalScale_DetectsObjectThroughPhysicsTrigger()
        {
            MeasurementZone zone = CreateZone("WeighingZone");
            zone.transform.position = new Vector3(100f, 100f, 100f);
            DigitalScale scale = CreateWithField<DigitalScale>("DigitalScale", "weighingZone", zone);

            GameObject weight = CreateWeight("Weight", 0.5f);
            weight.transform.position = zone.transform.position;
            weight.GetComponent<Rigidbody>().useGravity = false;

            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();

            scale.PowerOn();
            scale.RefreshMeasurement();
            Assert.IsTrue(scale.HasObjectOnPan);
            Assert.AreEqual(500f, scale.DisplayedMassGrams, 0.0001f);
        }

        // ---------------------------------------------------------------- Thermometer

        [UnityTest]
        public IEnumerator Thermometer_ReadsSelectedSampleAndHandlesMissingSample()
        {
            Thermometer thermometer = Create<Thermometer>("Thermometer");
            yield return null;

            Assert.AreEqual(ThermometerState.Off, thermometer.State);

            thermometer.StartMeasuring();
            Assert.AreEqual(ThermometerState.Reading, thermometer.State);
            Assert.IsFalse(thermometer.HasSample);
            Assert.IsFalse(thermometer.HasReading);
            Assert.AreEqual("NO SAMPLE", thermometer.CurrentStateName);

            TemperatureSample sample = Create<TemperatureSample>("Sample");
            sample.SetTemperature(37f);
            thermometer.SetSample(sample);
            thermometer.AdvanceTime(0.1f);
            Assert.IsTrue(thermometer.HasReading);
            Assert.AreEqual(37f, thermometer.CurrentReading, 0.0001f);
            Assert.AreEqual("READING", thermometer.CurrentStateName);

            sample.SetTemperature(80f);
            thermometer.AdvanceTime(0.1f);
            Assert.AreEqual(80f, thermometer.CurrentReading, 0.0001f, "Reading must follow the sample's configured temperature.");

            thermometer.ClearManualSample();
            thermometer.AdvanceTime(0.1f);
            Assert.IsFalse(thermometer.HasReading);

            thermometer.ResetMeasurement();
            Assert.AreEqual(ThermometerState.Off, thermometer.State);
            Assert.IsFalse(thermometer.HasReading);
        }

        [UnityTest]
        public IEnumerator Thermometer_DetectsSampleInZone()
        {
            MeasurementZone zone = CreateZone("SampleZone");
            Thermometer thermometer = CreateWithField<Thermometer>("Thermometer", "sampleZone", zone);
            yield return null;

            GameObject beaker = CreateWeight("Beaker", 0.2f);
            beaker.AddComponent<TemperatureSample>().SetTemperature(4f);
            zone.AddOccupant(beaker.GetComponent<Collider>());

            thermometer.StartMeasuring();
            Assert.IsTrue(thermometer.HasSample);
            Assert.AreEqual(4f, thermometer.CurrentReading, 0.0001f);

            zone.RemoveOccupant(beaker.GetComponent<Collider>());
            thermometer.AdvanceTime(0.1f);
            Assert.IsFalse(thermometer.HasSample);
            Assert.AreEqual("NO SAMPLE", thermometer.CurrentStateName);
        }

        // ---------------------------------------------------------------- Button

        [UnityTest]
        public IEnumerator InstrumentButton_PressInvokesEventAndDebouncesRepeats()
        {
            var go = new GameObject("Button");
            created.Add(go);
            go.AddComponent<BoxCollider>();
            InstrumentButton button = go.AddComponent<InstrumentButton>();
            int presses = 0;
            button.onPressed.AddListener(() => presses++);
            yield return null;

            Assert.IsTrue(button.Press());
            Assert.IsFalse(button.Press(), "A second press within the cooldown is ignored.");
            Assert.AreEqual(1, presses);
            Assert.AreEqual(1, button.PressCount);

            yield return new WaitForSeconds(0.2f);
            Assert.IsTrue(button.Press());
            Assert.AreEqual(2, presses);
        }

        // ---------------------------------------------------------------- helpers

        // Adds the component on an inactive GameObject so the serialized reference is in place before Awake runs.
        private T CreateWithField<T>(string name, string fieldName, object value) where T : Component
        {
            var go = new GameObject(name);
            created.Add(go);
            go.SetActive(false);
            T component = go.AddComponent<T>();
            SetPrivateField(component, fieldName, value);
            go.SetActive(true);
            return component;
        }

        private MeasurementZone CreateZone(string name)
        {
            var go = new GameObject(name);
            created.Add(go);
            var box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = Vector3.one * 0.3f;
            var rigidbody = go.AddComponent<Rigidbody>();
            rigidbody.isKinematic = true;
            rigidbody.useGravity = false;
            return go.AddComponent<MeasurementZone>();
        }

        private GameObject CreateWeight(string name, float massKg)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.localScale = Vector3.one * 0.05f;
            created.Add(go);
            var rigidbody = go.AddComponent<Rigidbody>();
            rigidbody.mass = massKg;
            return go;
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"Field '{fieldName}' not found on {target.GetType().Name}.");
            field.SetValue(target, value);
        }
    }
}
