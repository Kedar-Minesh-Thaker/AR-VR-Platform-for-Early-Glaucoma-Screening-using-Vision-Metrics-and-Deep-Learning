// XRSetup.cs
// Runtime VR bootstrap for Google Cardboard.
// Initialises the Cardboard XR loader to get stereoscopic side-by-side
// rendering with barrel distortion and gyro head tracking.
//
// In Editor (no Cardboard): falls back silently to flat-screen mode.
//
// Public API
// ----------
//   static bool IsVRActive       – true once Cardboard stereo is running
//   static bool IsInitialised    – true after detection completes
//   void        StartXR()        – turn on VR (stereo split)
//   void        StopXR()         – turn off VR (flat screen)

using System.Collections;
using UnityEngine;
using UnityEngine.XR.Management;

namespace OphthalSuite.Core
{
    public class XRSetup : MonoBehaviour
    {
        // ── Static state ─────────────────────────────────────────────────────────
        public static bool IsVRActive    { get; private set; }
        public static bool IsInitialised { get; private set; }

        [Header("Cardboard VR")]
        [Tooltip("If true, auto-start VR on launch. Set false to start flat and toggle later.")]
        [SerializeField] private bool autoStartVR = true;

        private void Awake()
        {
            if (IsInitialised) return;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
        }

        private IEnumerator Start()
        {
            // Wait one frame so all other scripts can Awake
            yield return null;

            if (autoStartVR)
                StartXR();
            else
            {
                IsInitialised = true;
                Debug.Log("XRSetup: VR auto-start disabled — running flat.");
            }
        }

        /// <summary>
        /// Initialise Cardboard XR loader → stereo split + barrel distortion + gyro tracking.
        /// </summary>
        public void StartXR()
        {
            if (IsVRActive) return;

            var xrManager = XRGeneralSettings.Instance;
            if (xrManager == null || xrManager.Manager == null)
            {
                Debug.LogWarning("XRSetup: No XR General Settings found.\n" +
                    "→ Edit → Project Settings → XR Plug-in Management → enable Cardboard XR Plugin.");
                IsInitialised = true;
                return;
            }

            // Initialise the loader (Cardboard)
            if (!xrManager.Manager.isInitializationComplete)
                xrManager.Manager.InitializeLoaderSync();

            if (xrManager.Manager.activeLoader == null)
            {
                Debug.LogWarning("XRSetup: No XR loader active — running flat.\n" +
                    "→ Install Cardboard plugin: Window → Package Manager → + → Add from Git URL\n" +
                    "→ https://github.com/googlevr/cardboard-xr-plugin.git\n" +
                    "→ Then enable it in XR Plug-in Management.");
                IsInitialised = true;
                return;
            }

            // Start subsystems → stereo rendering begins
            xrManager.Manager.StartSubsystems();

            IsVRActive    = true;
            IsInitialised = true;

            Debug.Log($"XRSetup: ✓ VR active — loader: {xrManager.Manager.activeLoader.name}\n" +
                "Stereoscopic rendering + barrel distortion + gyro tracking enabled.");
        }

        /// <summary>
        /// Stop VR — return to flat single-screen mode.
        /// </summary>
        public void StopXR()
        {
            if (!IsVRActive) return;

            var xrManager = XRGeneralSettings.Instance;
            if (xrManager != null && xrManager.Manager != null)
            {
                xrManager.Manager.StopSubsystems();
                xrManager.Manager.DeinitializeLoader();
            }

            IsVRActive = false;
            Debug.Log("XRSetup: VR stopped — flat-screen mode.");
        }

        private void OnDestroy()
        {
            if (IsVRActive) StopXR();
            IsInitialised = false;
        }
    }
}
