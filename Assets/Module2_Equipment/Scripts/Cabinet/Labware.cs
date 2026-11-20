using UnityEngine;

namespace VRScienceLab.Module2
{
    /// <summary>Kinds of laboratory equipment kept in the storage cabinet.</summary>
    public enum LabwareKind
    {
        Beaker,
        ErlenmeyerFlask,
        TestTube,
        TestTubeRack,
        LaboratoryTape,
        SampleVial,
        ReagentBottle,
        SpecimenJar,
    }

    /// <summary>
    /// Identifies a movable piece of laboratory equipment (glassware, rack, tape...).
    /// It carries no behavior: grabbing is done by the XR Grab Interactable, the mass shown by
    /// the digital scale is the Rigidbody mass, and a temperature is only reported when the
    /// object also has a <see cref="TemperatureSample"/>. The storage cabinet and the test tube
    /// rack use this component to recognise what they hold.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    [AddComponentMenu("VR Science Lab/Module 2/Labware")]
    public class Labware : MonoBehaviour
    {
        [SerializeField, Tooltip("Name shown by the cabinet and in logs.")]
        private string displayName = "Labware";

        [SerializeField] private LabwareKind kind = LabwareKind.Beaker;

        [SerializeField, Min(0f), Tooltip("Nominal capacity in millilitres (0 when it does not apply).")]
        private float capacityMillilitres;

        private Rigidbody body;

        public string DisplayName => string.IsNullOrEmpty(displayName) ? name : displayName;
        public LabwareKind Kind => kind;
        public float CapacityMillilitres => capacityMillilitres;

        /// <summary>The Rigidbody of this object.</summary>
        public Rigidbody Body
        {
            get
            {
                if (body == null) body = GetComponent<Rigidbody>();
                return body;
            }
        }

        /// <summary>Mass in grams, read from the Rigidbody (the value the digital scale shows).</summary>
        public float MassGrams => Body != null ? DigitalScale.KilogramsToGrams(Body.mass) : 0f;
    }
}
