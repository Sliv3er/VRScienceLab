using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace VRScienceLab.Module2
{
    /// <summary>
    /// Interaction feedback: brightens an object while an XR interactor (ray or direct) hovers it,
    /// so the user sees what a press or a grab will act on.
    /// Works with any XR interactable on the same GameObject (button, grabbable sample...).
    /// </summary>
    [AddComponentMenu("VR Science Lab/Module 2/Hover Highlight")]
    public class HoverHighlight : MonoBehaviour
    {
        [SerializeField, Tooltip("Renderers that glow while hovered. Defaults to the renderers in the children.")]
        private Renderer[] renderers = new Renderer[0];

        [SerializeField, ColorUsage(false, true)] private Color hoverEmission = new Color(0.10f, 0.45f, 0.55f);

        private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

        private XRBaseInteractable interactable;
        private MaterialPropertyBlock block;
        private int hoverCount;

        /// <summary>True while at least one interactor hovers the object.</summary>
        public bool IsHovered => hoverCount > 0;

        private void Awake()
        {
            interactable = GetComponent<XRBaseInteractable>();
            if (renderers.Length == 0) renderers = GetComponentsInChildren<Renderer>();
        }

        private void OnEnable()
        {
            if (interactable == null) return;
            interactable.hoverEntered.AddListener(OnHoverEntered);
            interactable.hoverExited.AddListener(OnHoverExited);
        }

        private void OnDisable()
        {
            if (interactable != null)
            {
                interactable.hoverEntered.RemoveListener(OnHoverEntered);
                interactable.hoverExited.RemoveListener(OnHoverExited);
            }

            hoverCount = 0;
            Apply();
        }

        private void OnHoverEntered(HoverEnterEventArgs args)
        {
            hoverCount++;
            Apply();
        }

        private void OnHoverExited(HoverExitEventArgs args)
        {
            hoverCount = Mathf.Max(0, hoverCount - 1);
            Apply();
        }

        private void Apply()
        {
            if (block == null) block = new MaterialPropertyBlock();
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer target = renderers[i];
                if (target == null) continue;

                // Read the block first so other scripts' overrides (press color, state color) are kept.
                target.GetPropertyBlock(block);
                block.SetColor(EmissionId, IsHovered ? hoverEmission : Color.black);
                target.SetPropertyBlock(block);
            }
        }
    }
}
