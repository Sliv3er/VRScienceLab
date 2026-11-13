using UnityEngine;

namespace VRScienceLab.Module2
{
    /// <summary>
    /// First-person body of the VR user: a rigged laboratory researcher that stands under the
    /// headset, turns with it, plays idle / walk animations from the rig's horizontal speed and
    /// reaches its arms to the VR controllers (two-bone IK applied after the Animator).
    ///
    /// The avatar is a pure follower: it never moves the XR Origin, has no collider and no
    /// interactor, so locomotion and XR interactions are unaffected. In first person the head
    /// bone is collapsed so the model can never cover the VR camera; the observer view (Editor
    /// demonstration) restores it and renders the scientist from the outside.
    /// </summary>
    [AddComponentMenu("VR Science Lab/Module 2/Scientist Avatar")]
    public class ScientistAvatar : MonoBehaviour
    {
        [Header("Tracking sources (XR Origin)")]
        [SerializeField, Tooltip("The VR camera. Defaults to Camera.main.")]
        private Transform headTarget;
        [SerializeField] private Transform leftHandTarget;
        [SerializeField] private Transform rightHandTarget;
        [SerializeField, Tooltip("Floor reference (the XR Origin). The avatar's feet stay at its height.")]
        private Transform floorReference;

        [Header("Model")]
        [SerializeField] private Animator animator;
        [SerializeField, Tooltip("Animator float parameter driven with the walking speed (m/s).")]
        private string speedParameter = "Speed";
        [SerializeField] private Transform headBone;
        [SerializeField] private Transform leftUpperArm;
        [SerializeField] private Transform leftLowerArm;
        [SerializeField] private Transform leftHand;
        [SerializeField] private Transform rightUpperArm;
        [SerializeField] private Transform rightLowerArm;
        [SerializeField] private Transform rightHand;

        [Header("Body follow")]
        [SerializeField, Tooltip("How far behind the camera the body stands, so the user looks down at the chest and not into it (metres).")]
        private float bodyBackOffset = 0.14f;
        [SerializeField, Min(0f), Tooltip("The body turns when the head looks further than this from the body direction (degrees).")]
        private float turnThreshold = 35f;
        [SerializeField, Min(1f)] private float turnSpeed = 220f;
        [SerializeField, Min(0f), Tooltip("Speed (m/s) above which the body faces the head direction and the walk animation plays.")]
        private float walkSpeedThreshold = 0.15f;
        [SerializeField, Min(0.01f)] private float speedSmoothing = 0.12f;

        [Header("Hands")]
        [SerializeField, Tooltip("Reach the arms to the VR controllers. When off, the arms follow the animation only.")]
        private bool armsFollowControllers = true;
        [SerializeField, Tooltip("Rotation applied on top of the controller rotation so the palm matches the grip.")]
        private Vector3 leftHandRotationOffset = new Vector3(0f, 0f, 90f);
        [SerializeField] private Vector3 rightHandRotationOffset = new Vector3(0f, 0f, -90f);
        [SerializeField, Tooltip("Offset from the controller to the wrist, in controller space (metres).")]
        private Vector3 handPositionOffset = new Vector3(0f, -0.02f, -0.08f);
        [SerializeField, Min(0.1f), Tooltip("A controller further than this from the head counts as not tracked (arms then follow the animation).")]
        private float maximumReach = 1.1f;

        [Header("Observer view (Editor demonstration)")]
        [SerializeField, Tooltip("Optional camera that shows the scientist from the outside.")]
        private Camera observerCamera;

        private Vector3 lastHeadPosition;
        private float smoothedSpeed;
        private float speedVelocity;
        private int speedParameterId;
        private bool hasSpeedParameter;
        private Vector3 headBoneRestScale = Vector3.one;

        /// <summary>True while the outside view is active.</summary>
        public bool ObserverViewActive { get; private set; }

        /// <summary>Smoothed horizontal speed of the user in metres per second.</summary>
        public float CurrentSpeed => smoothedSpeed;

        /// <summary>True when the left / right arm followed its controller during the last frame.</summary>
        public bool LeftArmTracked { get; private set; }
        public bool RightArmTracked { get; private set; }

        private void Awake()
        {
            speedParameterId = Animator.StringToHash(speedParameter);
            if (headBone != null) headBoneRestScale = headBone.localScale;
        }

        private void Start()
        {
            if (headTarget == null && Camera.main != null) headTarget = Camera.main.transform;
            if (headTarget != null) lastHeadPosition = headTarget.position;

            if (animator != null)
            {
                foreach (AnimatorControllerParameter parameter in animator.parameters)
                {
                    if (parameter.nameHash == speedParameterId) hasSpeedParameter = true;
                }
            }

            if (observerCamera != null) observerCamera.gameObject.SetActive(false);
            SnapToHead();
        }

        /// <summary>Switches between the first-person view and the outside view of the scientist.</summary>
        public void ToggleObserverView()
        {
            SetObserverView(!ObserverViewActive);
        }

        public void SetObserverView(bool active)
        {
            if (observerCamera == null) active = false;
            ObserverViewActive = active;
            if (observerCamera != null) observerCamera.gameObject.SetActive(active);
        }

        private void Update()
        {
            if (headTarget == null) return;

            float delta = Time.deltaTime;
            Vector3 headPosition = headTarget.position;

            // Horizontal speed of the head = walking speed (continuous move, teleport excluded).
            Vector3 movement = headPosition - lastHeadPosition;
            movement.y = 0f;
            lastHeadPosition = headPosition;
            float rawSpeed = delta > 0f ? movement.magnitude / delta : 0f;
            if (rawSpeed > 6f) rawSpeed = 0f; // a teleport is not a walk
            smoothedSpeed = Mathf.SmoothDamp(smoothedSpeed, rawSpeed, ref speedVelocity, speedSmoothing);

            if (animator != null && hasSpeedParameter) animator.SetFloat(speedParameterId, smoothedSpeed);

            // Yaw: follow the head while walking, or when it turns past the threshold.
            Vector3 headForward = headTarget.forward;
            headForward.y = 0f;
            if (headForward.sqrMagnitude > 0.0001f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(headForward.normalized, Vector3.up);
                float angle = Quaternion.Angle(transform.rotation, targetRotation);
                if (smoothedSpeed > walkSpeedThreshold || angle > turnThreshold)
                {
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, turnSpeed * delta);
                }
            }

            PlaceUnderHead();
        }

        private void SnapToHead()
        {
            if (headTarget == null) return;
            Vector3 forward = headTarget.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(forward.normalized, Vector3.up);
            PlaceUnderHead();
        }

        private void PlaceUnderHead()
        {
            float floorY = floorReference != null ? floorReference.position.y : 0f;
            Vector3 position = headTarget.position - transform.forward * bodyBackOffset;
            position.y = floorY;
            transform.position = position;
        }

        // After the Animator has written the pose: hide the head and bring the hands to the controllers.
        private void LateUpdate()
        {
            if (headBone != null)
            {
                headBone.localScale = ObserverViewActive ? headBoneRestScale : Vector3.zero;
            }

            LeftArmTracked = SolveArm(leftUpperArm, leftLowerArm, leftHand, leftHandTarget, leftHandRotationOffset, -1f);
            RightArmTracked = SolveArm(rightUpperArm, rightLowerArm, rightHand, rightHandTarget, rightHandRotationOffset, 1f);
        }

        private bool SolveArm(Transform upper, Transform lower, Transform hand, Transform target, Vector3 rotationOffset, float side)
        {
            if (!armsFollowControllers) return false;
            if (upper == null || lower == null || hand == null || target == null) return false;
            if (!target.gameObject.activeInHierarchy) return false;
            if (headTarget != null && (target.position - headTarget.position).sqrMagnitude > maximumReach * maximumReach) return false;
            // An untracked controller sits exactly at the rig origin.
            if (target.localPosition == Vector3.zero) return false;

            Vector3 wrist = target.TransformPoint(handPositionOffset);
            // Elbows point down, slightly outward and backward, like relaxed arms.
            Vector3 elbowHint = (-transform.up + transform.right * (0.45f * side) - transform.forward * 0.35f).normalized;

            SolveTwoBoneIK(upper, lower, hand, wrist, elbowHint);
            hand.rotation = target.rotation * Quaternion.Euler(rotationOffset);
            return true;
        }

        /// <summary>
        /// Analytic two-bone IK in world space. Step 1 bends the elbow so the shoulder-to-wrist
        /// distance matches the target distance; step 2 swings the whole arm onto the target;
        /// step 3 rolls the arm so the elbow points toward <paramref name="elbowHint"/>.
        /// </summary>
        public static void SolveTwoBoneIK(Transform upper, Transform lower, Transform hand, Vector3 target, Vector3 elbowHint)
        {
            Vector3 a = upper.position;
            Vector3 b = lower.position;
            Vector3 c = hand.position;

            float upperLength = Vector3.Distance(a, b);
            float lowerLength = Vector3.Distance(b, c);
            if (upperLength < 0.0001f || lowerLength < 0.0001f) return;

            float reach = Mathf.Clamp(Vector3.Distance(a, target), Mathf.Abs(upperLength - lowerLength) + 0.001f, upperLength + lowerLength - 0.001f);

            // Step 1: elbow bend (law of cosines).
            Vector3 ac = (c - a).normalized;
            Vector3 ab = (b - a).normalized;
            Vector3 ba = -ab;
            Vector3 bc = (c - b).normalized;

            float shoulderNow = Mathf.Acos(Mathf.Clamp(Vector3.Dot(ac, ab), -1f, 1f));
            float elbowNow = Mathf.Acos(Mathf.Clamp(Vector3.Dot(ba, bc), -1f, 1f));
            float shoulderWanted = Mathf.Acos(Mathf.Clamp((upperLength * upperLength + reach * reach - lowerLength * lowerLength) / (2f * upperLength * reach), -1f, 1f));
            float elbowWanted = Mathf.Acos(Mathf.Clamp((upperLength * upperLength + lowerLength * lowerLength - reach * reach) / (2f * upperLength * lowerLength), -1f, 1f));

            Vector3 bendAxis = Vector3.Cross(ac, ab);
            if (bendAxis.sqrMagnitude < 0.0001f) bendAxis = Vector3.Cross(ac, elbowHint);
            if (bendAxis.sqrMagnitude < 0.0001f) bendAxis = Vector3.Cross(ac, Vector3.up);
            bendAxis.Normalize();

            upper.rotation = Quaternion.AngleAxis((shoulderWanted - shoulderNow) * Mathf.Rad2Deg, bendAxis) * upper.rotation;
            lower.rotation = Quaternion.AngleAxis((elbowWanted - elbowNow) * Mathf.Rad2Deg, bendAxis) * lower.rotation;

            // Step 2: swing the arm so the wrist lands on the target.
            Vector3 toHand = hand.position - a;
            Vector3 toTarget = target - a;
            if (toHand.sqrMagnitude > 0.000001f && toTarget.sqrMagnitude > 0.000001f)
            {
                upper.rotation = Quaternion.FromToRotation(toHand, toTarget) * upper.rotation;
            }

            // Step 3: roll around the shoulder-wrist axis so the elbow follows the hint.
            Vector3 axis = (hand.position - a).normalized;
            Vector3 elbowDirection = Vector3.ProjectOnPlane(lower.position - a, axis);
            Vector3 wantedDirection = Vector3.ProjectOnPlane(elbowHint, axis);
            if (elbowDirection.sqrMagnitude > 0.000001f && wantedDirection.sqrMagnitude > 0.000001f)
            {
                float roll = Vector3.SignedAngle(elbowDirection, wantedDirection, axis);
                upper.rotation = Quaternion.AngleAxis(roll, axis) * upper.rotation;
            }
        }
    }
}
