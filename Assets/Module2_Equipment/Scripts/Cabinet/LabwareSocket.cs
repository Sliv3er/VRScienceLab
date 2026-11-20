using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace VRScienceLab.Module2
{
    /// <summary>
    /// A holding position for one piece of laboratory equipment, for example one hole of the
    /// test tube rack. It is an XR Socket Interactor that only accepts <see cref="Labware"/> of one
    /// kind: when the user releases a matching object inside the socket's trigger collider, the
    /// socket takes it and holds it at its attach transform; grabbing the object again takes it out.
    /// Requires a trigger Collider on the same GameObject (XR Socket Interactor rule).
    /// </summary>
    [AddComponentMenu("VR Science Lab/Module 2/Labware Socket")]
    public class LabwareSocket : XRSocketInteractor
    {
        [SerializeField, Tooltip("Kind of labware this position holds.")]
        private LabwareKind acceptedKind = LabwareKind.TestTube;

        /// <summary>Kind of labware this position holds.</summary>
        public LabwareKind AcceptedKind => acceptedKind;

        /// <summary>True while an object is held in this position.</summary>
        public bool IsOccupied => hasSelection;

        /// <summary>The labware currently held, or null.</summary>
        public Labware HeldLabware
        {
            get
            {
                if (!hasSelection || interactablesSelected.Count == 0) return null;
                Transform held = interactablesSelected[0].transform;
                return held != null ? held.GetComponent<Labware>() : null;
            }
        }

        /// <summary>True when <paramref name="labware"/> is of the kind this position holds.</summary>
        public bool Accepts(Labware labware)
        {
            return labware != null && labware.Kind == acceptedKind;
        }

        public override bool CanHover(IXRHoverInteractable interactable)
        {
            return base.CanHover(interactable) && AcceptsInteractable(interactable);
        }

        public override bool CanSelect(IXRSelectInteractable interactable)
        {
            return base.CanSelect(interactable) && AcceptsInteractable(interactable);
        }

        private bool AcceptsInteractable(IXRInteractable interactable)
        {
            if (interactable == null) return false;
            Transform target = interactable.transform;
            return target != null && Accepts(target.GetComponent<Labware>());
        }
    }
}
