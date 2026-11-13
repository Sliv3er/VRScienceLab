using UnityEngine;
using UnityEngine.XR;

namespace VRScienceLab.Module2
{
    /// <summary>
    /// Makes the scene testable without a headset: when no XR device is active at start, it
    /// instantiates the XR Interaction Toolkit simulator prefab (keyboard and mouse drive the
    /// headset and the controllers). With a headset connected nothing is created, so the same
    /// scene runs unchanged in VR.
    /// </summary>
    [AddComponentMenu("VR Science Lab/Module 2/XR Simulator Loader")]
    public class XRSimulatorLoader : MonoBehaviour
    {
        [SerializeField, Tooltip("Simulator prefab from the XR Interaction Toolkit samples.")]
        private GameObject simulatorPrefab;

        [SerializeField, Tooltip("Skip the simulator when a real XR device is running.")]
        private bool onlyWithoutHeadset = true;

        /// <summary>The simulator created by this component, or null.</summary>
        public GameObject Instance { get; private set; }

        private void Start()
        {
            if (simulatorPrefab == null) return;
            if (onlyWithoutHeadset && XRSettings.isDeviceActive) return;

            // The XRI project setting "Use XR Interaction Simulator in scenes" may already have created one.
            if (GameObject.Find(simulatorPrefab.name) != null || GameObject.Find(simulatorPrefab.name + "(Clone)") != null) return;

            Instance = Instantiate(simulatorPrefab);
            Instance.name = simulatorPrefab.name;
        }
    }
}
