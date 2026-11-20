// HeadlessSim: physics components as data with correct world-space bounds. There is no rigid-body
// dynamics; trigger overlaps are computed by Simulation.UpdateTriggers. Not part of the Unity project.
using System;
using System.Collections.Generic;
using System.Linq;
using HeadlessSim;

namespace UnityEngine
{
    public enum RigidbodyInterpolation { None, Interpolate, Extrapolate }
    public enum CollisionDetectionMode { Discrete, Continuous, ContinuousDynamic, ContinuousSpeculative }
    public enum QueryTriggerInteraction { UseGlobal, Ignore, Collide }
    public enum ForceMode { Force = 0, Impulse = 1, VelocityChange = 2, Acceleration = 5 }
    public enum PhysicsMaterialCombine { Average = 0, Multiply = 1, Minimum = 2, Maximum = 3 }
    [Flags]
    public enum RigidbodyConstraints
    {
        None = 0, FreezePositionX = 2, FreezePositionY = 4, FreezePositionZ = 8, FreezeRotationX = 16, FreezeRotationY = 32, FreezeRotationZ = 64,
        FreezePosition = 14, FreezeRotation = 112, FreezeAll = 126
    }

    public class PhysicsMaterial : Object
    {
        public float dynamicFriction = 0.6f, staticFriction = 0.6f, bounciness;
        public PhysicsMaterialCombine frictionCombine, bounceCombine;
        public PhysicsMaterial() { }
        public PhysicsMaterial(string name) { this.name = name; }
    }

    internal static class PhysicsMath
    {
        /// <summary>World-space axis-aligned bounds of a local box, through the full transform chain.</summary>
        public static Bounds TransformBounds(Transform transform, Bounds local)
        {
            Vector3 c = local.center, e = local.extents;
            Vector3 min = Vector3.positiveInfinity, max = Vector3.negativeInfinity;
            for (int x = -1; x <= 1; x += 2)
            {
                for (int y = -1; y <= 1; y += 2)
                {
                    for (int z = -1; z <= 1; z += 2)
                    {
                        Vector3 world = transform.TransformPoint(c + new Vector3(e.x * x, e.y * y, e.z * z));
                        min = Vector3.Min(min, world);
                        max = Vector3.Max(max, world);
                    }
                }
            }
            var bounds = new Bounds();
            bounds.SetMinMax(min, max);
            return bounds;
        }
    }

    public class Rigidbody : Component
    {
        public float mass = 1f;
        public bool isKinematic;
        public bool useGravity = true;
        public float linearDamping;
        public float angularDamping = 0.05f;
        public RigidbodyInterpolation interpolation;
        public CollisionDetectionMode collisionDetectionMode;
        public RigidbodyConstraints constraints;
        public bool freezeRotation { get => (constraints & RigidbodyConstraints.FreezeRotation) == RigidbodyConstraints.FreezeRotation; set { if (value) constraints |= RigidbodyConstraints.FreezeRotation; else constraints &= ~RigidbodyConstraints.FreezeRotation; } }
        public bool detectCollisions = true;
        public float maxAngularVelocity = 7f;
        public float maxLinearVelocity = 1e16f;
        public float sleepThreshold = 0.005f;
        public int solverIterations = 6;
        public Vector3 centerOfMass;
        public bool automaticCenterOfMass = true;

        [NonSerialized] private Vector3 velocityValue;
        [NonSerialized] private Vector3 angularVelocityValue;

        public Vector3 linearVelocity { get => velocityValue; set => velocityValue = value; }
        public Vector3 angularVelocity { get => angularVelocityValue; set => angularVelocityValue = value; }
        [Obsolete("Use linearVelocity")] public Vector3 velocity { get => velocityValue; set => velocityValue = value; }
        [Obsolete("Use linearDamping")] public float drag { get => linearDamping; set => linearDamping = value; }
        [Obsolete("Use angularDamping")] public float angularDrag { get => angularDamping; set => angularDamping = value; }

        public Vector3 position { get => transform.position; set => transform.position = value; }
        public Quaternion rotation { get => transform.rotation; set => transform.rotation = value; }
        public Vector3 worldCenterOfMass => transform.TransformPoint(centerOfMass);

        public void MovePosition(Vector3 newPosition) { transform.position = newPosition; }
        public void MoveRotation(Quaternion newRotation) { transform.rotation = newRotation; }
        public void Move(Vector3 newPosition, Quaternion newRotation) { transform.SetPositionAndRotation(newPosition, newRotation); }
        public void AddForce(Vector3 force) { AddForce(force, ForceMode.Force); }
        public void AddForce(Vector3 force, ForceMode mode)
        {
            if (isKinematic) return;
            if (mode == ForceMode.VelocityChange) velocityValue += force;
            else if (mode == ForceMode.Impulse) velocityValue += force / Mathf.Max(mass, 1e-6f);
        }
        public void AddTorque(Vector3 torque) { }
        public void AddTorque(Vector3 torque, ForceMode mode) { }
        public void AddForceAtPosition(Vector3 force, Vector3 position) { AddForce(force); }
        public void AddForceAtPosition(Vector3 force, Vector3 position, ForceMode mode) { AddForce(force, mode); }
        public void Sleep() { velocityValue = Vector3.zero; angularVelocityValue = Vector3.zero; }
        public void WakeUp() { }
        public bool IsSleeping() => velocityValue.sqrMagnitude < 1e-6f;
        public void ResetInertiaTensor() { }
        public void ResetCenterOfMass() { }
        public Vector3 GetPointVelocity(Vector3 worldPoint) => velocityValue;
    }

    public class Collider : Component
    {
        [SerializeField] private bool m_ColliderEnabled = true;
        public bool isTrigger;
        public PhysicsMaterial sharedMaterial;
        public PhysicsMaterial material { get => sharedMaterial; set => sharedMaterial = value; }
        public float contactOffset = 0.01f;
        public LayerMask includeLayers, excludeLayers;
        public bool providesContacts;

        public bool enabled { get => m_ColliderEnabled; set { m_ColliderEnabled = value; Simulation.Touch(); } }
        internal override bool SimEnabled => m_ColliderEnabled;

        public Rigidbody attachedRigidbody
        {
            get
            {
                for (Transform t = transform; t != null; t = t.parent)
                {
                    Rigidbody body = t.gameObject.GetComponent<Rigidbody>();
                    if (body != null) return body;
                }
                return null;
            }
        }

        /// <summary>World-space axis-aligned bounding box.</summary>
        public virtual Bounds bounds => new Bounds(transform.position, Vector3.zero);

        public Vector3 ClosestPoint(Vector3 position) => bounds.ClosestPoint(position);
        public Vector3 ClosestPointOnBounds(Vector3 position) => bounds.ClosestPoint(position);
        public bool Raycast(Ray ray, out RaycastHit hitInfo, float maxDistance)
        {
            return PhysicsQueries.RayBounds(ray, bounds, maxDistance, this, out hitInfo);
        }
    }

    public class BoxCollider : Collider
    {
        public Vector3 center = Vector3.zero;
        public Vector3 size = Vector3.one;
        public override Bounds bounds => PhysicsMath.TransformBounds(transform, new Bounds(center, size));
    }

    public class SphereCollider : Collider
    {
        public Vector3 center = Vector3.zero;
        public float radius = 0.5f;
        public override Bounds bounds
        {
            get
            {
                Vector3 scale = transform.lossyScale;
                float worldRadius = radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
                return new Bounds(transform.TransformPoint(center), Vector3.one * (worldRadius * 2f));
            }
        }
    }

    public class CapsuleCollider : Collider
    {
        public Vector3 center = Vector3.zero;
        public float radius = 0.5f;
        public float height = 1f;
        /// <summary>0 = X, 1 = Y, 2 = Z.</summary>
        public int direction = 1;
        public override Bounds bounds
        {
            get
            {
                float half = Mathf.Max(height * 0.5f, radius);
                Vector3 extents = new Vector3(radius, radius, radius);
                extents[Mathf.Clamp(direction, 0, 2)] = half;
                return PhysicsMath.TransformBounds(transform, new Bounds(center, extents * 2f));
            }
        }
    }

    public class MeshCollider : Collider
    {
        public Mesh sharedMesh;
        public bool convex;
        public override Bounds bounds
        {
            get
            {
                Mesh mesh = sharedMesh != null ? sharedMesh : owner.GetComponent<MeshFilter>()?.sharedMesh;
                return mesh != null ? PhysicsMath.TransformBounds(transform, mesh.bounds) : new Bounds(transform.position, Vector3.zero);
            }
        }
    }

    public class CharacterController : Collider
    {
        public Vector3 center;
        public float radius = 0.5f, height = 2f, slopeLimit = 45f, stepOffset = 0.3f, skinWidth = 0.08f, minMoveDistance = 0.001f;
        public bool isGrounded => true;
        public Vector3 velocity => Vector3.zero;
        public override Bounds bounds => PhysicsMath.TransformBounds(transform, new Bounds(center, new Vector3(radius * 2f, height, radius * 2f)));
        public CollisionFlags Move(Vector3 motion) { transform.position += motion; return CollisionFlags.None; }
        public bool SimpleMove(Vector3 speed) { transform.position += speed * Time.deltaTime; return true; }
    }
    [Flags] public enum CollisionFlags { None = 0, Sides = 1, Above = 2, Below = 4 }

    public struct RaycastHit
    {
        public Vector3 point;
        public Vector3 normal;
        public float distance;
        internal Collider hitCollider;
        public Collider collider => hitCollider;
        public Transform transform => hitCollider != null ? hitCollider.transform : null;
        public Rigidbody rigidbody => hitCollider != null ? hitCollider.attachedRigidbody : null;
    }

    public struct ContactPoint
    {
        public Vector3 point, normal;
        public Collider thisCollider, otherCollider;
        public float separation;
    }

    public class Collision
    {
        public Vector3 relativeVelocity;
        public Vector3 impulse;
        public Collider collider;
        public Rigidbody rigidbody => collider != null ? collider.attachedRigidbody : null;
        public GameObject gameObject => collider != null ? collider.gameObject : null;
        public Transform transform => collider != null ? collider.transform : null;
        public ContactPoint[] contacts = new ContactPoint[0];
        public int contactCount => contacts.Length;
        public ContactPoint GetContact(int index) => contacts[index];
    }

    internal static class PhysicsQueries
    {
        public static IEnumerable<Collider> ActiveColliders()
        {
            foreach (GameObject go in Simulation.AllSceneObjects(false))
            {
                foreach (Component component in go.components)
                {
                    if (component is Collider collider && collider.enabled && !collider.destroyed) yield return collider;
                }
            }
        }

        public static bool Accept(Collider collider, int layerMask, QueryTriggerInteraction triggers)
        {
            if (((1 << collider.gameObject.layer) & layerMask) == 0) return false;
            if (collider.isTrigger && triggers == QueryTriggerInteraction.Ignore) return false;
            return true;
        }

        // Slab test of a ray against an axis-aligned box.
        public static bool RayBounds(Ray ray, Bounds bounds, float maxDistance, Collider collider, out RaycastHit hit)
        {
            hit = new RaycastHit();
            Vector3 min = bounds.min, max = bounds.max;
            float tMin = 0f, tMax = maxDistance;
            int enterAxis = -1; float enterSign = 0f;
            for (int axis = 0; axis < 3; axis++)
            {
                float origin = ray.origin[axis], direction = ray.direction[axis];
                if (Mathf.Abs(direction) < 1e-9f)
                {
                    if (origin < min[axis] || origin > max[axis]) return false;
                    continue;
                }
                float t1 = (min[axis] - origin) / direction, t2 = (max[axis] - origin) / direction;
                float sign = -1f;
                if (t1 > t2) { float swap = t1; t1 = t2; t2 = swap; sign = 1f; }
                if (t1 > tMin) { tMin = t1; enterAxis = axis; enterSign = sign; }
                if (t2 < tMax) tMax = t2;
                if (tMin > tMax) return false;
            }

            Vector3 normal = Vector3.zero;
            if (enterAxis >= 0) normal[enterAxis] = enterSign;
            hit.distance = tMin;
            hit.point = ray.GetPoint(tMin);
            hit.normal = normal;
            hit.hitCollider = collider;
            return true;
        }
    }

    /// <summary>Queries are answered with the colliders' axis-aligned bounds (no exact shapes).</summary>
    public static class Physics
    {
        public const int DefaultRaycastLayers = -5;
        public const int AllLayers = -1;
        public const int IgnoreRaycastLayer = 4;
        public static Vector3 gravity { get; set; } = new Vector3(0f, -9.81f, 0f);
        public static bool autoSyncTransforms { get; set; }
        public static bool queriesHitTriggers { get; set; } = true;
        public static float defaultContactOffset { get; set; } = 0.01f;
        public static float bounceThreshold { get; set; } = 2f;

        public static void SyncTransforms() { }
        public static void Simulate(float step) { }
        public static void IgnoreCollision(Collider a, Collider b) { }
        public static void IgnoreCollision(Collider a, Collider b, bool ignore) { }
        public static void IgnoreLayerCollision(int layer1, int layer2) { }
        public static void IgnoreLayerCollision(int layer1, int layer2, bool ignore) { }

        public static bool Raycast(Ray ray) => Raycast(ray, out RaycastHit _, Mathf.Infinity, DefaultRaycastLayers, QueryTriggerInteraction.UseGlobal);
        public static bool Raycast(Ray ray, float maxDistance) => Raycast(ray, out RaycastHit _, maxDistance, DefaultRaycastLayers, QueryTriggerInteraction.UseGlobal);
        public static bool Raycast(Ray ray, out RaycastHit hitInfo) => Raycast(ray, out hitInfo, Mathf.Infinity, DefaultRaycastLayers, QueryTriggerInteraction.UseGlobal);
        public static bool Raycast(Ray ray, out RaycastHit hitInfo, float maxDistance) => Raycast(ray, out hitInfo, maxDistance, DefaultRaycastLayers, QueryTriggerInteraction.UseGlobal);
        public static bool Raycast(Ray ray, out RaycastHit hitInfo, float maxDistance, int layerMask) => Raycast(ray, out hitInfo, maxDistance, layerMask, QueryTriggerInteraction.UseGlobal);
        public static bool Raycast(Ray ray, out RaycastHit hitInfo, float maxDistance, int layerMask, QueryTriggerInteraction queryTriggerInteraction)
        {
            hitInfo = new RaycastHit();
            bool found = false;
            float best = maxDistance;
            foreach (Collider collider in PhysicsQueries.ActiveColliders())
            {
                if (!PhysicsQueries.Accept(collider, layerMask, queryTriggerInteraction)) continue;
                if (PhysicsQueries.RayBounds(ray, collider.bounds, best, collider, out RaycastHit hit) && hit.distance <= best)
                {
                    best = hit.distance; hitInfo = hit; found = true;
                }
            }
            return found;
        }
        public static bool Raycast(Vector3 origin, Vector3 direction) => Raycast(new Ray(origin, direction));
        public static bool Raycast(Vector3 origin, Vector3 direction, float maxDistance) => Raycast(new Ray(origin, direction), maxDistance);
        public static bool Raycast(Vector3 origin, Vector3 direction, float maxDistance, int layerMask) => Raycast(new Ray(origin, direction), out RaycastHit _, maxDistance, layerMask);
        public static bool Raycast(Vector3 origin, Vector3 direction, out RaycastHit hitInfo) => Raycast(new Ray(origin, direction), out hitInfo);
        public static bool Raycast(Vector3 origin, Vector3 direction, out RaycastHit hitInfo, float maxDistance) => Raycast(new Ray(origin, direction), out hitInfo, maxDistance);
        public static bool Raycast(Vector3 origin, Vector3 direction, out RaycastHit hitInfo, float maxDistance, int layerMask) => Raycast(new Ray(origin, direction), out hitInfo, maxDistance, layerMask);
        public static bool Raycast(Vector3 origin, Vector3 direction, out RaycastHit hitInfo, float maxDistance, int layerMask, QueryTriggerInteraction queryTriggerInteraction) => Raycast(new Ray(origin, direction), out hitInfo, maxDistance, layerMask, queryTriggerInteraction);

        public static RaycastHit[] RaycastAll(Ray ray, float maxDistance = Mathf.Infinity, int layerMask = DefaultRaycastLayers, QueryTriggerInteraction queryTriggerInteraction = QueryTriggerInteraction.UseGlobal)
        {
            var hits = new List<RaycastHit>();
            foreach (Collider collider in PhysicsQueries.ActiveColliders())
            {
                if (!PhysicsQueries.Accept(collider, layerMask, queryTriggerInteraction)) continue;
                if (PhysicsQueries.RayBounds(ray, collider.bounds, maxDistance, collider, out RaycastHit hit)) hits.Add(hit);
            }
            return hits.OrderBy(h => h.distance).ToArray();
        }

        public static bool Linecast(Vector3 start, Vector3 end) => Raycast(start, end - start, (end - start).magnitude);
        public static bool Linecast(Vector3 start, Vector3 end, out RaycastHit hitInfo) => Raycast(start, end - start, out hitInfo, (end - start).magnitude);

        public static Collider[] OverlapBox(Vector3 center, Vector3 halfExtents) => OverlapBox(center, halfExtents, Quaternion.identity, AllLayers, QueryTriggerInteraction.UseGlobal);
        public static Collider[] OverlapBox(Vector3 center, Vector3 halfExtents, Quaternion orientation) => OverlapBox(center, halfExtents, orientation, AllLayers, QueryTriggerInteraction.UseGlobal);
        public static Collider[] OverlapBox(Vector3 center, Vector3 halfExtents, Quaternion orientation, int layerMask) => OverlapBox(center, halfExtents, orientation, layerMask, QueryTriggerInteraction.UseGlobal);
        public static Collider[] OverlapBox(Vector3 center, Vector3 halfExtents, Quaternion orientation, int layerMask, QueryTriggerInteraction queryTriggerInteraction)
        {
            Bounds box = OrientedBounds(center, halfExtents, orientation);
            return PhysicsQueries.ActiveColliders().Where(c => PhysicsQueries.Accept(c, layerMask, queryTriggerInteraction) && box.Intersects(c.bounds)).ToArray();
        }

        public static int OverlapBoxNonAlloc(Vector3 center, Vector3 halfExtents, Collider[] results) => OverlapBoxNonAlloc(center, halfExtents, results, Quaternion.identity, AllLayers, QueryTriggerInteraction.UseGlobal);
        public static int OverlapBoxNonAlloc(Vector3 center, Vector3 halfExtents, Collider[] results, Quaternion orientation) => OverlapBoxNonAlloc(center, halfExtents, results, orientation, AllLayers, QueryTriggerInteraction.UseGlobal);
        public static int OverlapBoxNonAlloc(Vector3 center, Vector3 halfExtents, Collider[] results, Quaternion orientation, int mask) => OverlapBoxNonAlloc(center, halfExtents, results, orientation, mask, QueryTriggerInteraction.UseGlobal);
        public static int OverlapBoxNonAlloc(Vector3 center, Vector3 halfExtents, Collider[] results, Quaternion orientation, int mask, QueryTriggerInteraction queryTriggerInteraction)
        {
            Collider[] found = OverlapBox(center, halfExtents, orientation, mask, queryTriggerInteraction);
            int count = Math.Min(found.Length, results.Length);
            Array.Copy(found, results, count);
            return count;
        }

        public static Collider[] OverlapSphere(Vector3 position, float radius) => OverlapSphere(position, radius, AllLayers, QueryTriggerInteraction.UseGlobal);
        public static Collider[] OverlapSphere(Vector3 position, float radius, int layerMask) => OverlapSphere(position, radius, layerMask, QueryTriggerInteraction.UseGlobal);
        public static Collider[] OverlapSphere(Vector3 position, float radius, int layerMask, QueryTriggerInteraction queryTriggerInteraction)
        {
            float sqr = radius * radius;
            return PhysicsQueries.ActiveColliders().Where(c => PhysicsQueries.Accept(c, layerMask, queryTriggerInteraction) && c.bounds.SqrDistance(position) <= sqr).ToArray();
        }
        public static int OverlapSphereNonAlloc(Vector3 position, float radius, Collider[] results) => OverlapSphereNonAlloc(position, radius, results, AllLayers, QueryTriggerInteraction.UseGlobal);
        public static int OverlapSphereNonAlloc(Vector3 position, float radius, Collider[] results, int layerMask) => OverlapSphereNonAlloc(position, radius, results, layerMask, QueryTriggerInteraction.UseGlobal);
        public static int OverlapSphereNonAlloc(Vector3 position, float radius, Collider[] results, int layerMask, QueryTriggerInteraction queryTriggerInteraction)
        {
            Collider[] found = OverlapSphere(position, radius, layerMask, queryTriggerInteraction);
            int count = Math.Min(found.Length, results.Length);
            Array.Copy(found, results, count);
            return count;
        }

        public static bool CheckBox(Vector3 center, Vector3 halfExtents) => OverlapBox(center, halfExtents).Length > 0;
        public static bool CheckBox(Vector3 center, Vector3 halfExtents, Quaternion orientation, int layerMask = AllLayers, QueryTriggerInteraction q = QueryTriggerInteraction.UseGlobal) => OverlapBox(center, halfExtents, orientation, layerMask, q).Length > 0;
        public static bool CheckSphere(Vector3 position, float radius) => OverlapSphere(position, radius).Length > 0;
        public static bool CheckSphere(Vector3 position, float radius, int layerMask, QueryTriggerInteraction q = QueryTriggerInteraction.UseGlobal) => OverlapSphere(position, radius, layerMask, q).Length > 0;

        private static Bounds OrientedBounds(Vector3 center, Vector3 halfExtents, Quaternion orientation)
        {
            Vector3 min = Vector3.positiveInfinity, max = Vector3.negativeInfinity;
            for (int x = -1; x <= 1; x += 2) for (int y = -1; y <= 1; y += 2) for (int z = -1; z <= 1; z += 2)
            {
                Vector3 corner = center + orientation * new Vector3(halfExtents.x * x, halfExtents.y * y, halfExtents.z * z);
                min = Vector3.Min(min, corner); max = Vector3.Max(max, corner);
            }
            var bounds = new Bounds();
            bounds.SetMinMax(min, max);
            return bounds;
        }
    }

    // ---------------------------------------------------------------- Joints (data only)
    [Serializable] public struct JointLimits { public float min, max, bounciness, bounceMinVelocity, contactDistance; }
    [Serializable] public struct JointSpring { public float spring, damper, targetPosition; }
    [Serializable] public struct JointMotor { public float targetVelocity, force; public bool freeSpin; }

    public class Joint : Component
    {
        public Rigidbody connectedBody;
        public Vector3 anchor, connectedAnchor;
        public Vector3 axis = Vector3.right;
        public bool autoConfigureConnectedAnchor = true;
        public bool enableCollision;
        public bool enablePreprocessing = true;
        public float breakForce = Mathf.Infinity, breakTorque = Mathf.Infinity;
        public float massScale = 1f, connectedMassScale = 1f;
    }

    public class HingeJoint : Joint
    {
        public bool useLimits, useSpring, useMotor;
        public JointLimits limits;
        public JointSpring spring;
        public JointMotor motor;
        /// <summary>Simulator: the angle is not simulated; it can be set by a test.</summary>
        [NonSerialized] public float SimAngle;
        public float angle => SimAngle;
        public float velocity => 0f;
    }

    public class FixedJoint : Joint { }
    public class ConfigurableJoint : Joint { }
}
