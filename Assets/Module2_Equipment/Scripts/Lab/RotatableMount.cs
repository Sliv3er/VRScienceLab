using UnityEngine;

namespace VRScienceLab.Module2
{
    /// <summary>
    /// Swivel mount: rotates a part of an instrument (for example the stopwatch head or a
    /// monitor) around its vertical axis in fixed steps, so the user can turn the display
    /// toward themselves. Transform.localRotation demonstration; the instrument logic is untouched.
    /// </summary>
    [AddComponentMenu("VR Science Lab/Module 2/Rotatable Mount")]
    public class RotatableMount : MonoBehaviour
    {
        [SerializeField, Tooltip("Transform that turns. Defaults to this transform.")]
        private Transform pivot;

        [SerializeField, Tooltip("Local axis the pivot turns around.")]
        private Vector3 axis = Vector3.up;

        [SerializeField, Min(1f)] private float stepDegrees = 15f;
        [SerializeField, Min(0f)] private float maximumAngle = 60f;
        [SerializeField, Min(1f), Tooltip("Degrees per second while turning toward the target angle.")]
        private float turnSpeed = 120f;

        private Quaternion restRotation;
        private float currentAngle;

        /// <summary>Angle the mount is turning to, in degrees (0 = original orientation).</summary>
        public float TargetAngle { get; private set; }

        /// <summary>Angle currently applied, in degrees.</summary>
        public float CurrentAngle => currentAngle;

        private void Awake()
        {
            if (pivot == null) pivot = transform;
            restRotation = pivot.localRotation;
            enabled = false; // Update only runs while the mount is turning.
        }

        public void RotateLeft()
        {
            SetTargetAngle(TargetAngle - stepDegrees);
        }

        public void RotateRight()
        {
            SetTargetAngle(TargetAngle + stepDegrees);
        }

        public void ResetRotation()
        {
            SetTargetAngle(0f);
        }

        /// <summary>Sets the angle to reach, clamped to the allowed range.</summary>
        public void SetTargetAngle(float degrees)
        {
            TargetAngle = Mathf.Clamp(degrees, -maximumAngle, maximumAngle);
            enabled = true;
        }

        private void Update()
        {
            currentAngle = Mathf.MoveTowards(currentAngle, TargetAngle, turnSpeed * Time.deltaTime);
            pivot.localRotation = restRotation * Quaternion.AngleAxis(currentAngle, axis);
            if (Mathf.Approximately(currentAngle, TargetAngle)) enabled = false;
        }
    }
}
