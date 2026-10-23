using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace VRScienceLab.Module2
{
    /// <summary>
    /// Trigger volume that keeps track of the colliders currently inside it.
    /// Used by the digital scale (objects on the pan) and the thermometer (sample in the dock).
    /// Put it on a GameObject with a trigger collider and a kinematic Rigidbody.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    [AddComponentMenu("VR Science Lab/Module 2/Measurement Zone")]
    public class MeasurementZone : MonoBehaviour
    {
        [SerializeField, Tooltip("Only colliders whose GameObject layer is in this mask are tracked.")]
        private LayerMask detectedLayers = ~0;

        [SerializeField, Tooltip("Only colliders attached to a Rigidbody are tracked. Keep enabled so controller colliders are ignored.")]
        private bool requireRigidbody = true;

        [Tooltip("Raised when an object enters or leaves the zone.")]
        public UnityEvent onOccupantsChanged = new UnityEvent();

        private readonly List<Collider> occupants = new List<Collider>(8);
        private readonly HashSet<Rigidbody> rigidbodyScratch = new HashSet<Rigidbody>();

        /// <summary>Number of colliders currently inside the zone.</summary>
        public int OccupantCount
        {
            get
            {
                Prune();
                return occupants.Count;
            }
        }

        /// <summary>True when nothing is inside the zone.</summary>
        public bool IsEmpty => OccupantCount == 0;

        private void Reset()
        {
            // Editor-time default when the component is added: make the collider a trigger.
            var collider = GetComponent<Collider>();
            if (collider != null) collider.isTrigger = true;
        }

        private void Awake()
        {
            var collider = GetComponent<Collider>();
            if (collider == null)
            {
                Debug.LogError($"[MeasurementZone] '{name}' has no Collider. The zone is disabled.", this);
                enabled = false;
                return;
            }

            if (!collider.isTrigger)
            {
                Debug.LogWarning($"[MeasurementZone] Collider on '{name}' was not a trigger; enabling isTrigger.", this);
                collider.isTrigger = true;
            }

            if (GetComponent<Rigidbody>() == null)
            {
                Debug.LogWarning($"[MeasurementZone] '{name}' has no Rigidbody. Add a kinematic Rigidbody so trigger events are reliable.", this);
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            AddOccupant(other);
        }

        private void OnTriggerExit(Collider other)
        {
            RemoveOccupant(other);
        }

        private void OnDisable()
        {
            if (occupants.Count == 0) return;
            occupants.Clear();
            onOccupantsChanged?.Invoke();
        }

        /// <summary>Registers a collider as being inside the zone. Returns true when it was added.</summary>
        public bool AddOccupant(Collider other)
        {
            if (other == null || !IsAccepted(other) || occupants.Contains(other)) return false;
            occupants.Add(other);
            onOccupantsChanged?.Invoke();
            return true;
        }

        /// <summary>Unregisters a collider. Returns true when it was inside the zone.</summary>
        public bool RemoveOccupant(Collider other)
        {
            if (other == null) return false;
            bool removed = occupants.Remove(other);
            if (removed) onOccupantsChanged?.Invoke();
            return removed;
        }

        /// <summary>
        /// Fills <paramref name="results"/> with the distinct Rigidbodies currently inside the zone
        /// (an object with several colliders is counted once). Allocation-free after warm-up.
        /// </summary>
        public int CollectRigidbodies(List<Rigidbody> results)
        {
            results.Clear();
            Prune();
            rigidbodyScratch.Clear();

            for (int i = 0; i < occupants.Count; i++)
            {
                Rigidbody rigidbody = occupants[i].attachedRigidbody;
                if (rigidbody != null && rigidbodyScratch.Add(rigidbody))
                {
                    results.Add(rigidbody);
                }
            }

            return results.Count;
        }

        /// <summary>Finds the first component of type T on an occupant or one of its parents.</summary>
        public bool TryGetOccupantComponent<T>(out T component) where T : Component
        {
            Prune();
            for (int i = 0; i < occupants.Count; i++)
            {
                T found = occupants[i].GetComponentInParent<T>();
                if (found != null)
                {
                    component = found;
                    return true;
                }
            }

            component = null;
            return false;
        }

        private bool IsAccepted(Collider other)
        {
            if (((1 << other.gameObject.layer) & detectedLayers.value) == 0) return false;
            if (requireRigidbody && other.attachedRigidbody == null) return false;
            return true;
        }

        // Removes colliders that were destroyed or deactivated while inside the zone
        // (Unity does not send OnTriggerExit in that case).
        private void Prune()
        {
            bool changed = false;
            for (int i = occupants.Count - 1; i >= 0; i--)
            {
                Collider collider = occupants[i];
                if (collider == null || !collider.enabled || !collider.gameObject.activeInHierarchy)
                {
                    occupants.RemoveAt(i);
                    changed = true;
                }
            }

            if (changed) onOccupantsChanged?.Invoke();
        }
    }
}
