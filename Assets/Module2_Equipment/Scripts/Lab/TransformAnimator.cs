using UnityEngine;

namespace VRScienceLab.Module2
{
    /// <summary>
    /// Small reusable Transform animation: continuous rotation, a vertical bob and a scale pulse.
    /// Used for the ventilation fan, the locator beacon and the selection ring, so the three
    /// Transform operations (position, rotation, scale) are visibly animated in the lab.
    /// </summary>
    [AddComponentMenu("VR Science Lab/Module 2/Transform Animator")]
    public class TransformAnimator : MonoBehaviour
    {
        [Header("Rotation")]
        [SerializeField, Tooltip("Degrees per second around each local axis.")]
        private Vector3 rotationSpeed = Vector3.zero;

        [Header("Position")]
        [SerializeField, Tooltip("Local offset reached at the top of the bob.")]
        private Vector3 bobOffset = Vector3.zero;
        [SerializeField, Min(0f)] private float bobFrequency = 0.5f;

        [Header("Scale")]
        [SerializeField, Min(0f), Tooltip("0.1 = the object grows and shrinks by 10 %.")]
        private float pulseAmount = 0f;
        [SerializeField, Min(0f)] private float pulseFrequency = 1f;

        private Vector3 restPosition;
        private Vector3 restScale;
        private float time;

        private void Awake()
        {
            restPosition = transform.localPosition;
            restScale = transform.localScale;
        }

        private void OnEnable()
        {
            time = 0f;
        }

        private void Update()
        {
            float delta = Time.deltaTime;
            time += delta;

            if (rotationSpeed != Vector3.zero)
            {
                transform.Rotate(rotationSpeed * delta, Space.Self);
            }

            if (bobOffset != Vector3.zero)
            {
                float wave = 0.5f - 0.5f * Mathf.Cos(time * bobFrequency * 2f * Mathf.PI);
                transform.localPosition = restPosition + bobOffset * wave;
            }

            if (pulseAmount > 0f)
            {
                float wave = Mathf.Sin(time * pulseFrequency * 2f * Mathf.PI);
                transform.localScale = restScale * (1f + pulseAmount * wave);
            }
        }

        /// <summary>Call after moving the object from code so the bob continues around the new position.</summary>
        public void RebaseRestPosition()
        {
            restPosition = transform.localPosition;
        }
    }
}
