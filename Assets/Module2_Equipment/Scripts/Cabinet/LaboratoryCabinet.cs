using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

namespace VRScienceLab.Module2
{
    /// <summary>
    /// Laboratory storage cabinet: a set of hinged doors and the equipment kept behind them.
    /// - Doors: each <see cref="CabinetDoor"/> is operated on its own with a controller; the
    ///   cabinet also offers Open / Close / Toggle for both at once (keyboard, reset, tests).
    /// - Contents: a trigger zone covering the interior counts the <see cref="Labware"/> that is
    ///   currently stored, so taking an object out and putting it back is visible on the
    ///   cabinet's status line.
    /// - Feedback: status text and an interior light that brightens while a door is open.
    /// </summary>
    [AddComponentMenu("VR Science Lab/Module 2/Laboratory Cabinet")]
    public class LaboratoryCabinet : MonoBehaviour
    {
        [SerializeField, Tooltip("Name used in logs.")]
        private string cabinetName = "Storage Cabinet";

        [SerializeField, Tooltip("Hinged doors of the cabinet.")]
        private CabinetDoor[] doors = new CabinetDoor[0];

        [SerializeField, Tooltip("Trigger zone covering the interior. Labware inside it counts as stored.")]
        private MeasurementZone interiorZone;

        [SerializeField, Tooltip("Equipment that belongs in this cabinet (used for the 'x of y' status).")]
        private Labware[] contents = new Labware[0];

        [Header("Feedback (optional)")]
        [SerializeField, Tooltip("Text showing the door state and the number of stored objects.")]
        private TMP_Text statusText;

        [SerializeField, Tooltip("Renderers of the interior light, brighter while a door is open.")]
        private Renderer[] interiorLights = new Renderer[0];

        [SerializeField] private Color lightClosedColor = new Color(0.10f, 0.45f, 0.55f);
        [SerializeField, ColorUsage(false, true)] private Color lightClosedEmission = new Color(0.03f, 0.30f, 0.40f);
        [SerializeField] private Color lightOpenColor = new Color(0.85f, 0.95f, 1f);
        [SerializeField, ColorUsage(false, true)] private Color lightOpenEmission = new Color(1.3f, 1.5f, 1.6f);

        [Header("Events")]
        [Tooltip("Raised when a door changes state or an object enters or leaves the cabinet.")]
        public UnityEvent onStatusChanged = new UnityEvent();

        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

        private readonly List<Rigidbody> bodies = new List<Rigidbody>(32);
        private MaterialPropertyBlock block;
        private string lastStatus;

        public string CabinetName => cabinetName;
        public int DoorCount => doors.Length;

        /// <summary>Number of objects that belong in the cabinet.</summary>
        public int TotalCount => contents.Length;

        /// <summary>True when at least one door is not completely closed.</summary>
        public bool IsOpen
        {
            get
            {
                for (int i = 0; i < doors.Length; i++)
                {
                    if (doors[i] != null && !doors[i].IsClosed) return true;
                }

                return false;
            }
        }

        /// <summary>True when every door is fully open.</summary>
        public bool IsFullyOpen
        {
            get
            {
                if (doors.Length == 0) return false;
                for (int i = 0; i < doors.Length; i++)
                {
                    if (doors[i] == null || doors[i].State != CabinetDoorState.Open) return false;
                }

                return true;
            }
        }

        /// <summary>Number of labware objects currently inside the cabinet.</summary>
        public int StoredCount { get; private set; }

        /// <summary>Text shown on the status line, for example "DOORS OPEN   |   21 / 23 STORED".</summary>
        public string StatusLine { get; private set; } = string.Empty;

        private void Awake()
        {
            if (doors.Length == 0) Debug.LogWarning($"[LaboratoryCabinet] '{cabinetName}' has no doors assigned.", this);
            if (interiorZone == null) Debug.LogWarning($"[LaboratoryCabinet] '{cabinetName}' has no interior zone; stored objects are not counted.", this);
        }

        private void OnEnable()
        {
            for (int i = 0; i < doors.Length; i++)
            {
                if (doors[i] != null) doors[i].onStateChanged.AddListener(Refresh);
            }

            if (interiorZone != null) interiorZone.onOccupantsChanged.AddListener(Refresh);
        }

        private void Start()
        {
            Refresh();
        }

        private void OnDisable()
        {
            for (int i = 0; i < doors.Length; i++)
            {
                if (doors[i] != null) doors[i].onStateChanged.RemoveListener(Refresh);
            }

            if (interiorZone != null) interiorZone.onOccupantsChanged.RemoveListener(Refresh);
        }

        public CabinetDoor GetDoor(int index)
        {
            return index >= 0 && index < doors.Length ? doors[index] : null;
        }

        public Labware GetContent(int index)
        {
            return index >= 0 && index < contents.Length ? contents[index] : null;
        }

        /// <summary>Opens every door.</summary>
        public void OpenDoors()
        {
            for (int i = 0; i < doors.Length; i++)
            {
                if (doors[i] != null) doors[i].Open();
            }
        }

        /// <summary>Closes every door.</summary>
        public void CloseDoors()
        {
            for (int i = 0; i < doors.Length; i++)
            {
                if (doors[i] != null) doors[i].Close();
            }
        }

        /// <summary>Closes the doors when one of them is open, opens them otherwise.</summary>
        public void ToggleDoors()
        {
            if (IsOpen) CloseDoors();
            else OpenDoors();
        }

        /// <summary>Closes every door immediately (used by the laboratory reset).</summary>
        public void CloseDoorsImmediate()
        {
            for (int i = 0; i < doors.Length; i++)
            {
                if (doors[i] != null) doors[i].SetOpenImmediate(false);
            }
        }

        /// <summary>True when <paramref name="item"/> is currently inside the cabinet.</summary>
        public bool IsStored(Labware item)
        {
            if (item == null || interiorZone == null) return false;
            interiorZone.CollectRigidbodies(bodies);
            return bodies.Contains(item.Body);
        }

        /// <summary>Recounts the stored objects and updates the status text and the interior light.</summary>
        public void Refresh()
        {
            StoredCount = CountStored();

            string doorText = !IsOpen ? "DOORS CLOSED" : (IsFullyOpen ? "DOORS OPEN" : "DOOR OPEN");
            StatusLine = TotalCount > 0
                ? $"{doorText}   |   {StoredCount} / {TotalCount} STORED"
                : $"{doorText}   |   {StoredCount} STORED";

            if (StatusLine != lastStatus)
            {
                lastStatus = StatusLine;
                if (statusText != null) statusText.SetText(StatusLine);
                ApplyLight();
                onStatusChanged?.Invoke();
            }
        }

        private int CountStored()
        {
            if (interiorZone == null) return 0;

            interiorZone.CollectRigidbodies(bodies);
            int count = 0;
            for (int i = 0; i < bodies.Count; i++)
            {
                if (bodies[i].GetComponent<Labware>() != null) count++;
            }

            return count;
        }

        private void ApplyLight()
        {
            if (interiorLights == null || interiorLights.Length == 0) return;

            if (block == null) block = new MaterialPropertyBlock();
            bool open = IsOpen;
            Color color = open ? lightOpenColor : lightClosedColor;
            block.SetColor(ColorId, color);
            block.SetColor(BaseColorId, color);
            block.SetColor(EmissionId, open ? lightOpenEmission : lightClosedEmission);
            for (int i = 0; i < interiorLights.Length; i++)
            {
                if (interiorLights[i] != null) interiorLights[i].SetPropertyBlock(block);
            }
        }
    }
}
