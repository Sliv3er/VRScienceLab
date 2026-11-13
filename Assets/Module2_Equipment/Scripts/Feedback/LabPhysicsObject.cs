using System;
using UnityEngine;

namespace VRScienceLab.Module2
{
    /// <summary>
    /// Put on every movable Rigidbody of the lab (samples, weights, loose props).
    /// - Remembers the pose the object started with so the lab can be reset.
    /// - Reports collisions (OnCollisionEnter) with a short click sound and a static event
    ///   the interfaces use to show the last impact.
    /// - Brings the object back when it leaves the room (dropped out of reach).
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    [AddComponentMenu("VR Science Lab/Module 2/Lab Physics Object")]
    public class LabPhysicsObject : MonoBehaviour
    {
        /// <summary>Information about one collision.</summary>
        public struct Impact
        {
            public LabPhysicsObject Source;
            public string OtherName;
            public float Speed;
        }

        [SerializeField, Min(0f), Tooltip("Collisions slower than this (m/s) are ignored.")]
        private float minimumImpactSpeed = 0.25f;

        [SerializeField, Min(0f), Tooltip("Minimum time between two reported collisions of this object.")]
        private float impactCooldown = 0.15f;

        [SerializeField, Tooltip("Optional audio source used for the collision click.")]
        private AudioSource impactAudio;

        [SerializeField, Tooltip("The object returns to its start pose when it falls below this height (metres).")]
        private float recoverBelowHeight = -1f;

        [SerializeField, Min(0.1f)] private float recoverCheckInterval = 1f;

        private static AudioClip sharedImpactClip;

        private Rigidbody body;
        private Vector3 startPosition;
        private Quaternion startRotation;
        private Vector3 startScale;
        private float lastImpactTime = -10f;

        /// <summary>Raised for every reported collision of any lab object.</summary>
        public static event Action<Impact> AnyImpact;

        /// <summary>Number of collisions this object reported.</summary>
        public int ImpactCount { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            // Needed when "Enter Play Mode Options" skips the domain reload.
            AnyImpact = null;
            sharedImpactClip = null;
        }

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            startPosition = transform.position;
            startRotation = transform.rotation;
            startScale = transform.localScale;
        }

        private void OnEnable()
        {
            InvokeRepeating(nameof(RecoverIfOutOfBounds), recoverCheckInterval, recoverCheckInterval);
        }

        private void OnDisable()
        {
            CancelInvoke(nameof(RecoverIfOutOfBounds));
        }

        private void OnCollisionEnter(Collision collision)
        {
            float speed = collision.relativeVelocity.magnitude;
            if (speed < minimumImpactSpeed) return;
            if (Time.time - lastImpactTime < impactCooldown) return;

            lastImpactTime = Time.time;
            ImpactCount++;

            if (impactAudio != null)
            {
                if (sharedImpactClip == null) sharedImpactClip = CreateImpactClip();
                impactAudio.PlayOneShot(sharedImpactClip, Mathf.Clamp01(speed / 3f));
            }

            AnyImpact?.Invoke(new Impact
            {
                Source = this,
                OtherName = collision.collider != null ? NearestNamedParent(collision.collider.transform) : "unknown",
                Speed = speed,
            });
        }

        /// <summary>Puts the object back where it was when the scene started.</summary>
        public void ResetPose()
        {
            if (!body.isKinematic)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }

            transform.SetPositionAndRotation(startPosition, startRotation);
            transform.localScale = startScale;
            body.position = startPosition;
            body.rotation = startRotation;
        }

        private void RecoverIfOutOfBounds()
        {
            if (transform.position.y < recoverBelowHeight) ResetPose();
        }

        // Colliders are often on a child called "Body" or "Top"; report the meaningful parent instead.
        private static string NearestNamedParent(Transform collider)
        {
            Rigidbody owner = collider.GetComponentInParent<Rigidbody>();
            if (owner != null) return owner.name;

            Transform current = collider;
            while (current.parent != null && IsGenericName(current.name))
            {
                current = current.parent;
            }

            return current.name;
        }

        private static bool IsGenericName(string objectName)
        {
            return objectName == "Body" || objectName == "Top" || objectName == "Collider" || objectName == "Mesh";
        }

        // A 60 ms decaying "tick" generated in code so the project needs no audio file.
        private static AudioClip CreateImpactClip()
        {
            const int sampleRate = 44100;
            int length = sampleRate * 6 / 100;
            var data = new float[length];
            var random = new System.Random(7);
            for (int i = 0; i < length; i++)
            {
                float t = i / (float)length;
                float envelope = Mathf.Exp(-t * 9f);
                float tone = Mathf.Sin(2f * Mathf.PI * 1400f * i / sampleRate) * 0.6f;
                float noise = ((float)random.NextDouble() * 2f - 1f) * 0.4f;
                data[i] = (tone + noise) * envelope;
            }

            AudioClip clip = AudioClip.Create("LabImpact", length, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
