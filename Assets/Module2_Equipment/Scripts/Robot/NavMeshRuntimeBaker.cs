using Unity.AI.Navigation;
using UnityEngine;

namespace VRScienceLab.Module2
{
    /// <summary>
    /// Safety net for the robot's navigation: when the scene starts and its NavMesh Surface has
    /// no baked data (the NavMesh asset was not generated, was deleted, or was not committed),
    /// the NavMesh is built at runtime from the same colliders the editor bake uses.
    /// With baked data present this component does nothing.
    /// Runs before the NavMesh Surface and before the agents are used.
    /// </summary>
    [DefaultExecutionOrder(-200)]
    [AddComponentMenu("VR Science Lab/Module 2/NavMesh Runtime Baker")]
    public class NavMeshRuntimeBaker : MonoBehaviour
    {
        [SerializeField, Tooltip("Surface to check. Defaults to the NavMeshSurface on this GameObject, then to the first one in the scene.")]
        private NavMeshSurface surface;

        [SerializeField, Tooltip("Build the NavMesh at start when the surface has no baked data.")]
        private bool bakeWhenMissing = true;

        /// <summary>True when this component had to build the NavMesh at runtime.</summary>
        public bool BakedAtRuntime { get; private set; }

        /// <summary>True when the surface has NavMesh data after Awake.</summary>
        public bool HasNavMesh => surface != null && surface.navMeshData != null;

        private void Awake()
        {
            if (surface == null) surface = GetComponent<NavMeshSurface>();
            if (surface == null) surface = FindFirstObjectByType<NavMeshSurface>();

            if (surface == null)
            {
                Debug.LogError("[NavMeshRuntimeBaker] No NavMeshSurface found: the robot has nothing to navigate on.", this);
                return;
            }

            if (surface.navMeshData != null || !bakeWhenMissing) return;

            Debug.LogWarning("[NavMeshRuntimeBaker] The NavMesh Surface has no baked data; building the NavMesh now. " +
                             "Bake it in the editor with VR Science Lab > Module 2 > Bake NavMesh to avoid this at start.", this);
            Physics.SyncTransforms();
            surface.BuildNavMesh();
            BakedAtRuntime = surface.navMeshData != null;

            if (!BakedAtRuntime)
            {
                Debug.LogError("[NavMeshRuntimeBaker] Runtime NavMesh build produced no data. Check that the floor has a collider below the surface object.", this);
            }
        }
    }
}
