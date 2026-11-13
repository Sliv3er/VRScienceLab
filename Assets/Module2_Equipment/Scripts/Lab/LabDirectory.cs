using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

namespace VRScienceLab.Module2
{
    /// <summary>
    /// Searches the scene for the instruments and samples and exposes what it found.
    /// This is the module's "searching for and manipulating GameObjects" demonstration:
    ///   GameObject.Find(name)            - locate an instrument by its name,
    ///   Transform.Find("Display/Value")  - locate its measurement display,
    ///   GetComponent&lt;InstrumentBase&gt;() - retrieve the behaviour that drives it,
    ///   FindObjectsByType&lt;LabSample&gt;() - list every sample currently in the lab.
    /// The instrument lookups run once and are cached; nothing is searched in Update().
    /// The found object is then manipulated: a locator beacon is moved above it.
    /// </summary>
    [AddComponentMenu("VR Science Lab/Module 2/Lab Directory")]
    public class LabDirectory : MonoBehaviour
    {
        /// <summary>Everything the directory knows about one instrument.</summary>
        public class InstrumentEntry
        {
            public string ObjectName;
            public GameObject GameObject;
            public InstrumentBase Instrument;
            public TMP_Text ValueDisplay;
            public TMP_Text StateDisplay;
        }

        [SerializeField, Tooltip("GameObject names searched with GameObject.Find().")]
        private string[] instrumentObjectNames = { "DigitalScale", "HotPlate", "Stopwatch", "Thermometer" };

        [SerializeField, Tooltip("Path of the value text below the instrument root, used with Transform.Find().")]
        private string valueDisplayPath = "Display/ValueText";

        [SerializeField, Tooltip("Path of the state text below the instrument root, used with Transform.Find().")]
        private string stateDisplayPath = "Display/StateText";

        [Header("Locator beacon (manipulated when an instrument is found)")]
        [SerializeField] private Transform locatorBeacon;
        [SerializeField, Min(0f)] private float beaconHeight = 0.42f;
        [SerializeField, Min(0f), Tooltip("Seconds the beacon stays visible. 0 keeps it on.")]
        private float beaconDuration = 6f;

        [Tooltip("Raised with a readable report after every search.")]
        public UnityEvent<string> onSearchCompleted = new UnityEvent<string>();

        private readonly Dictionary<string, InstrumentEntry> cache = new Dictionary<string, InstrumentEntry>();
        private bool cacheBuilt;

        /// <summary>Report of the last search (what was found, where, and what it displays).</summary>
        public string LastReport { get; private set; } = string.Empty;

        /// <summary>Number of scene searches performed (GameObject.Find calls). Stays constant once cached.</summary>
        public int SceneSearchCount { get; private set; }

        public int InstrumentNameCount => instrumentObjectNames.Length;

        private void Start()
        {
            BuildCache();
            if (locatorBeacon != null) locatorBeacon.gameObject.SetActive(false);
        }

        /// <summary>Searches the scene once for every configured instrument name.</summary>
        public void BuildCache()
        {
            cache.Clear();
            for (int i = 0; i < instrumentObjectNames.Length; i++)
            {
                string objectName = instrumentObjectNames[i];
                if (string.IsNullOrEmpty(objectName)) continue;

                // 1) Find the GameObject by name.
                GameObject found = GameObject.Find(objectName);
                SceneSearchCount++;
                if (found == null)
                {
                    Debug.LogWarning($"[LabDirectory] GameObject '{objectName}' was not found in the scene.", this);
                    continue;
                }

                // 2) Find its displays by path and 3) get the components that matter.
                Transform value = FindDisplay(found.transform, valueDisplayPath);
                Transform state = FindDisplay(found.transform, stateDisplayPath);
                cache[objectName] = new InstrumentEntry
                {
                    ObjectName = objectName,
                    GameObject = found,
                    Instrument = found.GetComponent<InstrumentBase>(),
                    ValueDisplay = value != null ? value.GetComponent<TMP_Text>() : null,
                    StateDisplay = state != null ? state.GetComponent<TMP_Text>() : null,
                };
            }

            cacheBuilt = true;
        }

        // Transform.Find follows an exact path. When an instrument nests its display deeper
        // (the stopwatch display sits on a swivel head), fall back to a search by name.
        private static Transform FindDisplay(Transform root, string path)
        {
            Transform direct = root.Find(path);
            if (direct != null) return direct;

            string leaf = path.Substring(path.LastIndexOf('/') + 1);
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == leaf) return child;
            }

            return null;
        }

        public string GetInstrumentObjectName(int index)
        {
            return index >= 0 && index < instrumentObjectNames.Length ? instrumentObjectNames[index] : string.Empty;
        }

        /// <summary>Returns the cached entry for an instrument GameObject name.</summary>
        public bool TryGetInstrument(string objectName, out InstrumentEntry entry)
        {
            if (!cacheBuilt) BuildCache();
            return cache.TryGetValue(objectName, out entry) && entry.GameObject != null;
        }

        /// <summary>Typed access, for example TryGetInstrument("HotPlate", out HotPlate plate).</summary>
        public bool TryGetInstrument<T>(string objectName, out T instrument) where T : InstrumentBase
        {
            instrument = null;
            if (!TryGetInstrument(objectName, out InstrumentEntry entry)) return false;
            instrument = entry.Instrument as T;
            return instrument != null;
        }

        /// <summary>Finds the instrument at <paramref name="index"/>, reports it and moves the beacon above it.</summary>
        public void LocateInstrument(int index)
        {
            Locate(GetInstrumentObjectName(index));
        }

        /// <summary>Finds an instrument by name, reports it and moves the beacon above it.</summary>
        public void Locate(string objectName)
        {
            if (!TryGetInstrument(objectName, out InstrumentEntry entry))
            {
                Publish($"'{objectName}' not found.");
                return;
            }

            Vector3 position = entry.GameObject.transform.position;
            string value = entry.ValueDisplay != null ? entry.ValueDisplay.text : "no display";
            string state = entry.Instrument != null ? entry.Instrument.CurrentStateName : "no behaviour";
            string behaviour = entry.Instrument != null ? entry.Instrument.GetType().Name : "none";

            ShowBeacon(position);
            Publish($"Found '{entry.ObjectName}' at ({position.x:0.00}, {position.y:0.00}, {position.z:0.00})\n" +
                    $"Component: {behaviour}   State: {state}   Display: {value}");
        }

        /// <summary>Finds every sample in the lab and reports the heaviest one.</summary>
        public void LocateHeaviestSample()
        {
            LabSample[] samples = FindAllSamples();
            if (samples.Length == 0)
            {
                Publish("No sample found in the lab.");
                return;
            }

            LabSample heaviest = samples[0];
            for (int i = 1; i < samples.Length; i++)
            {
                if (samples[i].MassGrams > heaviest.MassGrams) heaviest = samples[i];
            }

            ShowBeacon(heaviest.transform.position - Vector3.up * 0.2f);
            Publish($"{samples.Length} samples found. Heaviest: {heaviest.DisplayName} ({heaviest.MassGrams:0} g)" +
                    (heaviest.HasTemperature ? $", {heaviest.TemperatureCelsius:0.0} °C" : string.Empty));
        }

        /// <summary>One-off scene search for every active sample (not cached: samples come and go).</summary>
        public LabSample[] FindAllSamples()
        {
            SceneSearchCount++;
            return FindObjectsByType<LabSample>(FindObjectsSortMode.None);
        }

        private void ShowBeacon(Vector3 targetPosition)
        {
            if (locatorBeacon == null) return;

            locatorBeacon.position = targetPosition + Vector3.up * beaconHeight;
            locatorBeacon.gameObject.SetActive(true);
            var animator = locatorBeacon.GetComponent<TransformAnimator>();
            if (animator != null) animator.RebaseRestPosition();

            CancelInvoke(nameof(HideBeacon));
            if (beaconDuration > 0f) Invoke(nameof(HideBeacon), beaconDuration);
        }

        private void HideBeacon()
        {
            if (locatorBeacon != null) locatorBeacon.gameObject.SetActive(false);
        }

        private void Publish(string report)
        {
            LastReport = report;
            onSearchCompleted?.Invoke(report);
        }
    }
}
