// XRSetup.cs
// Runtime VR bootstrap for Meta Quest 2.
// Initialises the Oculus XR loader to get stereoscopic rendering
// with 6DOF head tracking and controller input.
//
// In Editor (no headset): falls back silently to flat-screen mode.
//
// Public API
// ----------
//   static bool IsVRActive       – true once Quest stereo is running
//   static bool IsInitialised    – true after detection completes
//   void        StartXR()        – turn on VR (stereo split)
//   void        StopXR()         – turn off VR (flat screen)

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Management;

namespace OphthalSuite.Core
{
    public class XRSetup : MonoBehaviour
    {
        // ── Static state ─────────────────────────────────────────────────────────
        public static bool IsVRActive    { get; private set; }
        public static bool IsInitialised { get; private set; }

        [Header("Quest VR")]
        [Tooltip("If true, auto-start VR on launch. Set false to start flat and toggle later.")]
        [SerializeField] private bool autoStartVR = true;

        [Tooltip("Target refresh rate for Quest 2 (72, 80, 90, or 120 Hz). 90 recommended for perimetry.")]
        [SerializeField] private float targetRefreshRate = 90f;

        [Tooltip("Foveated rendering level. 0 = OFF (recommended for perimetry to preserve peripheral clarity).")]
        [SerializeField] [Range(0, 4)] private int foveatedRenderingLevel = 0;

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
        /// Initialise Oculus XR loader → stereo rendering + 6DOF head tracking.
        /// </summary>
        public void StartXR()
        {
            if (IsVRActive) return;

            var xrManager = XRGeneralSettings.Instance;
            if (xrManager == null || xrManager.Manager == null)
            {
                Debug.LogWarning("XRSetup: No XR General Settings found.\n" +
                    "→ Edit → Project Settings → XR Plug-in Management → enable Oculus.");
                IsInitialised = true;
                return;
            }

            // Initialise the loader (Oculus)
            if (!xrManager.Manager.isInitializationComplete)
                xrManager.Manager.InitializeLoaderSync();

            if (xrManager.Manager.activeLoader == null)
            {
                Debug.LogWarning("XRSetup: No XR loader active — running flat.\n" +
                    "→ Install Oculus plugin: Window → Package Manager\n" +
                    "→ Then enable it in XR Plug-in Management (Android tab → Oculus).");
                IsInitialised = true;
                return;
            }

            // Start subsystems → stereo rendering begins
            xrManager.Manager.StartSubsystems();

            // Quest 2 optimizations — apply after subsystems are running
            ApplyQuestSettings();

            IsVRActive    = true;
            IsInitialised = true;

            Debug.Log($"XRSetup: ✓ VR active — loader: {xrManager.Manager.activeLoader.name}\n" +
                "Stereoscopic rendering + 6DOF head tracking enabled.");
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

        /// <summary>
        /// Apply Quest 2-specific display and rendering settings.
        /// Must be called AFTER subsystems are started.
        /// </summary>
        private void ApplyQuestSettings()
        {
            // ── Refresh rate ────────────────────────────────────────────────────
            // Quest 2 supports 72, 80, 90, 120 Hz. Higher = smoother but more GPU.
            // 90 Hz recommended for perimetry (good balance of smoothness + battery).
            TrySetRefreshRate(targetRefreshRate);

            // ── Foveated rendering ──────────────────────────────────────────────
            // For clinical perimetry we must keep peripheral clarity intact.
            // Level 0 = OFF. Higher levels blur the edges to save GPU — bad for us.
            TrySetFoveatedRendering(foveatedRenderingLevel);

            Debug.Log($"XRSetup: Quest settings applied — refresh={targetRefreshRate}Hz, " +
                      $"foveation=level {foveatedRenderingLevel}");
        }

        /// <summary>
        /// Attempt to set the Quest display refresh rate.
        /// Uses reflection to find the correct API (varies across Unity/Oculus plugin versions).
        /// </summary>
        private void TrySetRefreshRate(float rate)
        {
            try
            {
                // Try Oculus-specific API via reflection: Unity.XR.Oculus.Performance.TrySetDisplayRefreshRate
                var oculusPerf = System.Type.GetType("Unity.XR.Oculus.Performance, Unity.XR.Oculus");
                if (oculusPerf != null)
                {
                    var method = oculusPerf.GetMethod("TrySetDisplayRefreshRate",
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                    if (method != null)
                    {
                        var result = method.Invoke(null, new object[] { rate });
                        Debug.Log($"XRSetup: Display refresh rate → {rate} Hz (Oculus API).");
                        return;
                    }
                }

                // Try OVRManager.display.displayFrequency via reflection
                var ovrMgr = System.Type.GetType("OVRManager, Oculus.VR");
                if (ovrMgr != null)
                {
                    var displayProp = ovrMgr.GetProperty("display",
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                    if (displayProp != null)
                    {
                        var display = displayProp.GetValue(null);
                        if (display != null)
                        {
                            var freqProp = display.GetType().GetProperty("displayFrequency");
                            if (freqProp != null && freqProp.CanWrite)
                            {
                                freqProp.SetValue(display, rate);
                                Debug.Log($"XRSetup: Display refresh rate → {rate} Hz (OVRManager).");
                                return;
                            }
                        }
                    }
                }

                Debug.Log($"XRSetup: Refresh rate API not found — using headset default.");
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"XRSetup: Failed to set refresh rate: {ex.Message}");
            }
        }

        /// <summary>
        /// Attempt to disable/set foveated rendering level.
        /// Uses Unity.XR.Oculus APIs if available, otherwise logs a warning.
        /// </summary>
        private void TrySetFoveatedRendering(int level)
        {
            // Use reflection to call Unity.XR.Oculus.Performance if the package is present.
            // This avoids a hard compile-time dependency on the Oculus namespace.
            try
            {
                var oculusPerf = System.Type.GetType("Unity.XR.Oculus.Performance, Unity.XR.Oculus");
                if (oculusPerf != null)
                {
                    var method = oculusPerf.GetMethod("TrySetFoveatedRenderingLevel",
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                    if (method != null)
                    {
                        method.Invoke(null, new object[] { level });
                        Debug.Log($"XRSetup: Foveated rendering set to level {level}.");
                        return;
                    }
                }
                Debug.Log("XRSetup: Oculus Performance API not found — foveation not changed.");
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"XRSetup: Failed to set foveated rendering: {ex.Message}");
            }
        }

        private void OnDestroy()
        {
            if (IsVRActive) StopXR();
            IsInitialised = false;
        }
    }
}
