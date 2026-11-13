using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace VRScienceLab.Module2
{
    /// <summary>
    /// Creates, selects, moves, scales and removes laboratory samples.
    /// This is the module's Instantiate()/Destroy() demonstration: every sample in the lab,
    /// including the three that are present when the session starts, is an instance of one of
    /// the sample prefabs created by <see cref="CreateSample(int)"/> and removed by
    /// <see cref="RemoveSelected"/> or <see cref="ResetSamples"/>.
    /// </summary>
    [AddComponentMenu("VR Science Lab/Module 2/Sample Manager")]
    public class SampleManager : MonoBehaviour
    {
        [Serializable]
        public struct StationAnchor
        {
            [Tooltip("Name shown in the interface (for example 'Scale pan').")]
            public string label;
            [Tooltip("Where a sample is placed when it is sent to this station.")]
            public Transform anchor;
        }

        [Header("Prefabs and placement")]
        [SerializeField, Tooltip("Sample prefabs that can be instantiated. The index is used by CreateSample(int).")]
        private LabSample[] samplePrefabs = new LabSample[0];

        [SerializeField, Tooltip("Free slots of the sample tray. A new sample appears on the first slot that is not occupied.")]
        private Transform[] spawnPoints = new Transform[0];

        [SerializeField, Tooltip("Parent of the instantiated samples. Defaults to this transform.")]
        private Transform sampleParent;

        [SerializeField, Tooltip("Prefab indices instantiated when the session starts and after a reset.")]
        private int[] initialSamples = new int[0];

        [SerializeField, Min(1), Tooltip("Upper limit so the user cannot flood the lab with objects.")]
        private int maximumSamples = 8;

        [SerializeField, Min(0.01f), Tooltip("A spawn point counts as occupied when a sample is closer than this (metres).")]
        private float spawnClearance = 0.07f;

        [Header("Stations a sample can be sent to (Transform.position demonstration)")]
        [SerializeField] private StationAnchor[] stationAnchors = new StationAnchor[0];

        [Header("Scaling")]
        [SerializeField, Min(0.01f)] private float scaleStep = 0.1f;

        [Header("Events")]
        [Tooltip("Raised when a sample is created, removed, selected, moved or scaled.")]
        public UnityEvent onSamplesChanged = new UnityEvent();

        [Tooltip("Short feedback message for the interface (for example 'Tray is full').")]
        public UnityEvent<string> onMessage = new UnityEvent<string>();

        private readonly List<LabSample> samples = new List<LabSample>(8);
        private readonly int[] createdPerPrefab = new int[16];

        /// <summary>The sample the interface buttons act on, or null.</summary>
        public LabSample Selected { get; private set; }

        /// <summary>Number of samples currently alive.</summary>
        public int SampleCount
        {
            get
            {
                Prune();
                return samples.Count;
            }
        }

        public int MaximumSamples => maximumSamples;
        public int PrefabCount => samplePrefabs.Length;
        public int StationCount => stationAnchors.Length;

        /// <summary>Total number of Instantiate() calls since the scene started.</summary>
        public int TotalCreated { get; private set; }

        /// <summary>Total number of Destroy() calls since the scene started.</summary>
        public int TotalDestroyed { get; private set; }

        /// <summary>Last feedback message.</summary>
        public string LastMessage { get; private set; } = string.Empty;

        private void Awake()
        {
            if (sampleParent == null) sampleParent = transform;
        }

        private void Start()
        {
            SpawnInitialSamples();
        }

        /// <summary>Read-only access to a live sample.</summary>
        public LabSample GetSample(int index)
        {
            Prune();
            return index >= 0 && index < samples.Count ? samples[index] : null;
        }

        public string GetStationLabel(int index)
        {
            return index >= 0 && index < stationAnchors.Length ? stationAnchors[index].label : string.Empty;
        }

        // ------------------------------------------------------------------
        // Instantiation
        // ------------------------------------------------------------------

        /// <summary>Instantiates the prefab at <paramref name="prefabIndex"/> on the first free tray slot.</summary>
        public LabSample CreateSample(int prefabIndex)
        {
            if (prefabIndex < 0 || prefabIndex >= samplePrefabs.Length || samplePrefabs[prefabIndex] == null)
            {
                Report($"No sample prefab at index {prefabIndex}.");
                return null;
            }

            Prune();
            if (samples.Count >= maximumSamples)
            {
                Report($"Limit reached ({maximumSamples} samples). Remove one first.");
                return null;
            }

            Transform slot = FindFreeSpawnPoint();
            if (slot == null)
            {
                Report("The sample tray is full. Move or remove a sample first.");
                return null;
            }

            LabSample prefab = samplePrefabs[prefabIndex];
            LabSample sample = Instantiate(prefab, slot.position, slot.rotation, sampleParent);
            TotalCreated++;

            int serial = ++createdPerPrefab[prefabIndex % createdPerPrefab.Length];
            sample.Rename($"{prefab.DisplayName} {serial}");
            sample.Grabbed += Select;
            samples.Add(sample);

            Select(sample);
            Report($"Created {sample.DisplayName}.");
            return sample;
        }

        /// <summary>Inspector/UI friendly overload (UnityEvent listeners cannot return a value).</summary>
        public void CreateSampleFromButton(int prefabIndex)
        {
            CreateSample(prefabIndex);
        }

        private void SpawnInitialSamples()
        {
            for (int i = 0; i < initialSamples.Length; i++)
            {
                CreateSample(initialSamples[i]);
            }

            if (samples.Count > 0) Select(samples[0]);
            Report(samples.Count > 0 ? $"{samples.Count} samples ready on the tray." : "No samples.");
        }

        private Transform FindFreeSpawnPoint()
        {
            float clearanceSquared = spawnClearance * spawnClearance;
            for (int i = 0; i < spawnPoints.Length; i++)
            {
                Transform point = spawnPoints[i];
                if (point == null) continue;

                bool occupied = false;
                for (int s = 0; s < samples.Count; s++)
                {
                    Vector3 offset = samples[s].transform.position - point.position;
                    offset.y = 0f;
                    if (offset.sqrMagnitude < clearanceSquared && Mathf.Abs(samples[s].transform.position.y - point.position.y) < 0.3f)
                    {
                        occupied = true;
                        break;
                    }
                }

                if (!occupied) return point;
            }

            return null;
        }

        // ------------------------------------------------------------------
        // Selection
        // ------------------------------------------------------------------

        /// <summary>Makes <paramref name="sample"/> the selected one (null clears the selection).</summary>
        public void Select(LabSample sample)
        {
            if (sample != null && !samples.Contains(sample)) return;
            if (Selected == sample) return;

            if (Selected != null) Selected.SetSelected(false);
            Selected = sample;
            if (Selected != null) Selected.SetSelected(true);
            onSamplesChanged?.Invoke();
        }

        /// <summary>Selects the next sample in creation order (wraps around).</summary>
        public void SelectNext()
        {
            Prune();
            if (samples.Count == 0)
            {
                Report("There is no sample to select.");
                return;
            }

            int index = Selected != null ? samples.IndexOf(Selected) : -1;
            Select(samples[(index + 1) % samples.Count]);
            Report($"Selected {Selected.DisplayName}.");
        }

        // ------------------------------------------------------------------
        // Destruction
        // ------------------------------------------------------------------

        /// <summary>Destroys the selected sample and selects the next one.</summary>
        public void RemoveSelected()
        {
            Prune();
            if (Selected == null)
            {
                Report("Select a sample first.");
                return;
            }

            LabSample removed = Selected;
            int index = samples.IndexOf(removed);
            string removedName = removed.DisplayName;

            DestroySample(removed);

            LabSample next = samples.Count > 0 ? samples[Mathf.Clamp(index, 0, samples.Count - 1)] : null;
            Selected = null;
            Select(next);
            Report($"Removed {removedName}.");
            onSamplesChanged?.Invoke();
        }

        /// <summary>Destroys every sample and instantiates the initial set again.</summary>
        public void ResetSamples()
        {
            Prune();
            for (int i = samples.Count - 1; i >= 0; i--)
            {
                DestroySample(samples[i]);
            }

            Selected = null;
            Array.Clear(createdPerPrefab, 0, createdPerPrefab.Length);
            SpawnInitialSamples();
            Report("Samples reset to the initial set.");
            onSamplesChanged?.Invoke();
        }

        private void DestroySample(LabSample sample)
        {
            samples.Remove(sample);
            if (sample == null) return;

            sample.Grabbed -= Select;
            // Deactivate first: Destroy() is deferred to the end of the frame, and an inactive
            // object immediately leaves the measurement zones and frees its tray slot.
            sample.gameObject.SetActive(false);
            Destroy(sample.gameObject);
            TotalDestroyed++;
        }

        // ------------------------------------------------------------------
        // Transform manipulation
        // ------------------------------------------------------------------

        /// <summary>Places the selected sample on a station anchor (scale pan, thermometer dock, tray...).</summary>
        public void MoveSelectedToStation(int stationIndex)
        {
            if (Selected == null)
            {
                Report("Select a sample first.");
                return;
            }

            if (stationIndex < 0 || stationIndex >= stationAnchors.Length || stationAnchors[stationIndex].anchor == null)
            {
                Report($"No station at index {stationIndex}.");
                return;
            }

            StationAnchor station = stationAnchors[stationIndex];
            Selected.TeleportTo(station.anchor.position, station.anchor.rotation);
            Report($"{Selected.DisplayName} moved to {station.label}.");
            onSamplesChanged?.Invoke();
        }

        /// <summary>Puts the selected sample back on the first free tray slot.</summary>
        public void ReturnSelectedToTray()
        {
            if (Selected == null)
            {
                Report("Select a sample first.");
                return;
            }

            // Move it away first so its own position does not count as an occupied slot.
            Vector3 original = Selected.transform.position;
            Selected.transform.position = original + Vector3.up * 100f;
            Transform slot = FindFreeSpawnPoint();
            Selected.transform.position = original;

            if (slot == null)
            {
                Report("The sample tray is full.");
                return;
            }

            Selected.TeleportTo(slot.position, slot.rotation);
            Report($"{Selected.DisplayName} returned to the tray.");
            onSamplesChanged?.Invoke();
        }

        public void ScaleSelectedUp()
        {
            ScaleSelected(scaleStep);
        }

        public void ScaleSelectedDown()
        {
            ScaleSelected(-scaleStep);
        }

        /// <summary>Changes the uniform scale of the selected sample by <paramref name="delta"/>.</summary>
        public void ScaleSelected(float delta)
        {
            if (Selected == null)
            {
                Report("Select a sample first.");
                return;
            }

            Selected.SetScaleFactor(Selected.ScaleFactor + delta);
            Report($"{Selected.DisplayName} scale x{Selected.ScaleFactor:0.0} ({Selected.MassGrams:0} g).");
            onSamplesChanged?.Invoke();
        }

        // ------------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------------

        // Drops entries destroyed by something else than this manager.
        private void Prune()
        {
            for (int i = samples.Count - 1; i >= 0; i--)
            {
                if (samples[i] == null) samples.RemoveAt(i);
            }

            if (Selected == null && !ReferenceEquals(Selected, null)) Selected = null;
        }

        private void Report(string message)
        {
            LastMessage = message;
            onMessage?.Invoke(message);
        }
    }
}
