using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit;

namespace VRScienceLab.Module2
{
    /// <summary>
    /// Test tube rack: a row of <see cref="LabwareSocket"/> positions, one per hole. The rack only
    /// keeps count of the occupied positions and shows it on an optional label; holding and
    /// releasing the tubes is done by the sockets themselves.
    /// </summary>
    [AddComponentMenu("VR Science Lab/Module 2/Test Tube Rack")]
    public class TestTubeRack : MonoBehaviour
    {
        [SerializeField, Tooltip("One socket per hole, in order.")]
        private LabwareSocket[] slots = new LabwareSocket[0];

        [SerializeField, Tooltip("Optional text showing 'occupied / capacity'.")]
        private TMP_Text countText;

        [Tooltip("Raised when a tube is placed in or taken from the rack.")]
        public UnityEvent onContentsChanged = new UnityEvent();

        /// <summary>Number of holes.</summary>
        public int Capacity => slots.Length;

        /// <summary>Number of holes that currently hold a tube.</summary>
        public int OccupiedCount { get; private set; }

        /// <summary>True when every hole holds a tube.</summary>
        public bool IsFull => Capacity > 0 && OccupiedCount >= Capacity;

        private void OnEnable()
        {
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] == null) continue;
                slots[i].selectEntered.AddListener(OnTubePlaced);
                slots[i].selectExited.AddListener(OnTubeTaken);
            }
        }

        private void Start()
        {
            Refresh();
        }

        private void OnDisable()
        {
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] == null) continue;
                slots[i].selectEntered.RemoveListener(OnTubePlaced);
                slots[i].selectExited.RemoveListener(OnTubeTaken);
            }
        }

        public LabwareSocket GetSlot(int index)
        {
            return index >= 0 && index < slots.Length ? slots[index] : null;
        }

        /// <summary>Index of the first hole without a tube, or -1 when the rack is full.</summary>
        public int FirstFreeSlot()
        {
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] != null && !slots[i].IsOccupied) return i;
            }

            return -1;
        }

        private void OnTubePlaced(SelectEnterEventArgs args)
        {
            Refresh();
        }

        private void OnTubeTaken(SelectExitEventArgs args)
        {
            Refresh();
        }

        /// <summary>Recounts the occupied holes and updates the label.</summary>
        public void Refresh()
        {
            int occupied = 0;
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] != null && slots[i].IsOccupied) occupied++;
            }

            bool changed = occupied != OccupiedCount;
            OccupiedCount = occupied;
            if (countText != null) countText.SetText("{0:0} / {1:0}", occupied, slots.Length);
            if (changed) onContentsChanged?.Invoke();
        }
    }
}
