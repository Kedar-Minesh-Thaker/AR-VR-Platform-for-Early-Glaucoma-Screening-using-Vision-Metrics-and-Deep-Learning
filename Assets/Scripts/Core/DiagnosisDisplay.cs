// DiagnosisDisplay.cs
// Reads the CNN prediction JSON written by train_multimodal_cnn.py and
// displays the final diagnosis category on the Results Screen canvas.
//
// ── Isolation guarantee ──────────────────────────────────────────────────────
//   This script is COMPLETELY passive:
//   • It only activates when SessionEndUI calls Show(sessionId).
//   • It never subscribes to any test events.
//   • It never modifies any test-loop state.
//   • It has no Update() or coroutine that runs during testing.
//
// ── Data contract ────────────────────────────────────────────────────────────
//   Reads:  <persistentDataPath>/diagnosis/diagnosis_<sessionId>.json
//   Format: { "prediction":"Normal", "confidence":0.9123,
//             "probabilities":{"Normal":0.91,"Mild Deficit":0.07,"Severe Deficit":0.02} }
//
//   That file is written by the Python inference pipeline after the session.
//   If the file doesn't exist yet (offline headset), the display shows a
//   friendly "Pending" state with a poll-retry until the file appears.
//
// ── How to wire in Unity ────────────────────────────────────────────────────
//   1. Attach this MonoBehaviour to any persistent GameObject (e.g. the
//      SessionEndUI GameObject or a dedicated "DiagnosisDisplayRoot").
//   2. In the Inspector assign:
//        diagnosisPanel   → the GameObject that wraps the whole diagnosis card
//        predictionLabel  → TextMeshProUGUI for the main category text
//        confidenceLabel  → TextMeshProUGUI for the confidence % (optional)
//        detailLabel      → TextMeshProUGUI for per-class probabilities (optional)
//        pendingLabel     → TextMeshProUGUI shown while waiting for the file
//   3. SessionEndUI.Show() already exists and receives the sessionId;
//      call diagnosisDisplay.ShowForSession(ctx.sessionId) from there, OR
//      wire the public method to AppOrchestrator's session-complete event.
//
// ── DO NOT ──────────────────────────────────────────────────────────────────
//   • Call ShowForSession() from within any test module.
//   • Modify SessionEndUI.cs, VRPointer.cs, or any test logic.
// ────────────────────────────────────────────────────────────────────────────

using System;
using System.Collections;
using System.IO;
using UnityEngine;
using TMPro;

namespace OphthalSuite.Core
{
    public class DiagnosisDisplay : MonoBehaviour
    {
        // ── Inspector ────────────────────────────────────────────────────────────
        [Header("Panel root — enable/disable this to show/hide the card")]
        [SerializeField] private GameObject diagnosisPanel;

        [Header("Main prediction label  (e.g. 'Normal')")]
        [SerializeField] private TextMeshProUGUI predictionLabel;

        [Header("Confidence label  (optional, e.g. '91.2% confidence')")]
        [SerializeField] private TextMeshProUGUI confidenceLabel;

        [Header("Detail label  (optional — shows per-class probabilities)")]
        [SerializeField] private TextMeshProUGUI detailLabel;

        [Header("Shown while waiting for the diagnosis file to appear")]
        [SerializeField] private TextMeshProUGUI pendingLabel;

        [Header("Max seconds to poll for the JSON file before giving up")]
        [SerializeField] [Range(5f, 120f)] private float pollTimeoutSec = 60f;

        [Header("How often to re-check for the file (seconds)")]
        [SerializeField] [Range(0.5f, 10f)] private float pollIntervalSec = 2f;

        // ── Private ──────────────────────────────────────────────────────────────

        /// <summary>Sub-folder inside persistentDataPath where the Python pipeline
        /// writes per-session prediction JSON files.</summary>
        private const string DiagnosisFolder = "diagnosis";

        private Coroutine _pollCoroutine;

        // ── Unity lifecycle ──────────────────────────────────────────────────────

        private void Awake()
        {
            // Start visible — "Analysing..." card should be seen by default.
            if (diagnosisPanel != null)
                diagnosisPanel.SetActive(true);
        }

        private void OnDestroy()
        {
            StopPolling();
        }

        // ── Public API ───────────────────────────────────────────────────────────

        /// <summary>
        /// Call this from SessionEndUI.Show() (or AppOrchestrator's session-complete
        /// handler) AFTER all tests are finished.
        /// Safe to call multiple times — cancels any previous poll.
        /// </summary>
        /// <param name="sessionId">The sessionId from SessionContext (UUID string).</param>
        public void ShowForSession(string sessionId)
        {
            StopPolling();

            if (string.IsNullOrWhiteSpace(sessionId))
            {
                Debug.LogWarning("DiagnosisDisplay: sessionId is empty — cannot look up prediction.");
                SetPending("Diagnosis N/A (no session ID)");
                ShowPanel();
                return;
            }

            // Activate panel in "pending" state immediately so the doctor
            // can see the card while the file is being generated.
            SetPending("Analysing results…");
            ShowPanel();

            _pollCoroutine = StartCoroutine(PollForDiagnosisFile(sessionId));
        }

        /// <summary>Hide the panel (e.g. when "Next Patient" is pressed).</summary>
        public void HidePanel()
        {
            StopPolling();
            if (diagnosisPanel != null)
                diagnosisPanel.SetActive(false);
        }

        // ── Internals ────────────────────────────────────────────────────────────

        private IEnumerator PollForDiagnosisFile(string sessionId)
        {
            string filePath = BuildFilePath(sessionId);
            float elapsed   = 0f;

            Debug.Log($"DiagnosisDisplay: waiting for → {filePath}");

            while (elapsed < pollTimeoutSec)
            {
                if (File.Exists(filePath))
                {
                    LoadAndDisplay(filePath);
                    yield break;
                }

                yield return new WaitForSeconds(pollIntervalSec);
                elapsed += pollIntervalSec;

                // Update the "pending" dot animation so the doctor knows it's live
                if (pendingLabel != null)
                {
                    int dots = ((int)(elapsed / pollIntervalSec) % 3) + 1;
                    pendingLabel.text = "Analysing results" + new string('.', dots);
                }
            }

            // Timed out
            Debug.LogWarning($"DiagnosisDisplay: file not found after {pollTimeoutSec}s — {filePath}");

            // --- TASK 1: Session Recovery Fallback ---
            string fallbackSessionId = GetMostRecentSessionId();
            if (!string.IsNullOrEmpty(fallbackSessionId) && fallbackSessionId != sessionId)
            {
                Debug.Log($"DiagnosisDisplay: Attempting fallback to recent session: {fallbackSessionId}");
                string fallbackPath = BuildFilePath(fallbackSessionId);
                if (File.Exists(fallbackPath))
                {
                    LoadAndDisplay(fallbackPath);
                    yield break;
                }
            }

            SetPending("Diagnosis pending\n(file not yet available)");
        }

        private void LoadAndDisplay(string filePath)
        {
            try
            {
                string json = File.ReadAllText(filePath);
                var payload = JsonUtility.FromJson<DiagnosisPayload>(json);

                if (payload == null || string.IsNullOrEmpty(payload.prediction))
                {
                    Debug.LogWarning($"DiagnosisDisplay: could not parse {filePath} or prediction missing");
                    // TASK 2: Override to "Data Incomplete"
                    SetPending("Data Incomplete");
                    return;
                }

                // ── Main prediction ──────────────────────────────────────────
                if (predictionLabel != null)
                {
                    predictionLabel.text = payload.prediction;
                    predictionLabel.color = CategoryColor(payload.prediction);
                }

                // ── Confidence ───────────────────────────────────────────────
                if (confidenceLabel != null)
                {
                    float pct = payload.confidence * 100f;
                    confidenceLabel.text = $"{pct:F1}% confidence";
                    confidenceLabel.gameObject.SetActive(payload.confidence > 0f);
                }

                // ── Per-class probabilities (detail line) ────────────────────
                if (detailLabel != null && payload.probabilities != null)
                {
                    detailLabel.text =
                        $"Normal {payload.probabilities.Normal * 100f:F0}%  " +
                        $"Mild {payload.probabilities.MildDeficit * 100f:F0}%  " +
                        $"Severe {payload.probabilities.SevereDeficit * 100f:F0}%";
                    detailLabel.gameObject.SetActive(true);
                }

                // Hide pending message now that we have a result
                if (pendingLabel != null)
                    pendingLabel.gameObject.SetActive(false);

                Debug.Log($"DiagnosisDisplay: loaded prediction='{payload.prediction}' " +
                          $"confidence={payload.confidence:F4}");
            }
            catch (Exception e)
            {
                Debug.LogError($"DiagnosisDisplay: error reading {filePath} — {e.Message}");
                SetPending("Diagnosis read error");
            }
        }

        private void ShowPanel()
        {
            if (diagnosisPanel != null)
                diagnosisPanel.SetActive(true);
        }

        private void SetPending(string message)
        {
            if (pendingLabel != null)
            {
                pendingLabel.text = message;
                pendingLabel.gameObject.SetActive(true);
            }

            if (predictionLabel  != null) predictionLabel.gameObject.SetActive(false);
            if (confidenceLabel  != null) confidenceLabel.gameObject.SetActive(false);
            if (detailLabel      != null) detailLabel.gameObject.SetActive(false);
        }

        private void StopPolling()
        {
            if (_pollCoroutine != null)
            {
                StopCoroutine(_pollCoroutine);
                _pollCoroutine = null;
            }
        }

        // ── Path helpers ─────────────────────────────────────────────────────────

        private string GetMostRecentSessionId()
        {
            try
            {
                string sessionsDir = Path.Combine(Application.persistentDataPath, "sessions");
                if (!Directory.Exists(sessionsDir)) return null;

                string[] subdirs = Directory.GetDirectories(sessionsDir);
                string latestSessionId = null;
                DateTime latestTime = DateTime.MinValue;

                foreach (var dir in subdirs)
                {
                    string summaryPath = Path.Combine(dir, "session_summary.json");
                    if (File.Exists(summaryPath))
                    {
                        var info = new FileInfo(summaryPath);
                        if (info.Length > 0 && info.LastWriteTime > latestTime)
                        {
                            latestTime = info.LastWriteTime;
                            latestSessionId = Path.GetFileName(dir);
                        }
                    }
                }
                return latestSessionId;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"DiagnosisDisplay: Fallback check failed: {e.Message}");
                return null;
            }
        }

        private static string BuildFilePath(string sessionId)
        {
            // Mirror the folder structure written by train_multimodal_cnn.py:
            //   <persistentDataPath>/diagnosis/diagnosis_<sessionId>.json
            return Path.Combine(
                Application.persistentDataPath,
                DiagnosisFolder,
                $"diagnosis_{sessionId}.json");
        }

        // ── Color coding ─────────────────────────────────────────────────────────

        private static Color CategoryColor(string prediction)
        {
            switch (prediction)
            {
                case "Normal":        return new Color(0.30f, 0.95f, 0.50f);   // green
                case "Mild Deficit":  return new Color(1.00f, 0.80f, 0.20f);   // amber
                case "Severe Deficit":return new Color(0.95f, 0.35f, 0.35f);   // red
                default:              return new Color(0.75f, 0.75f, 0.75f);   // grey
            }
        }

        // ── Serialisable JSON payload ─────────────────────────────────────────────

        /// <summary>
        /// Matches the JSON written by the Python pipeline for one session.
        /// JsonUtility requires a flat [Serializable] class; field names must
        /// match the Python dict keys exactly.
        /// </summary>
        [Serializable]
        private class DiagnosisPayload
        {
            public string sessionId;
            public string prediction;   // "Normal" | "Mild Deficit" | "Severe Deficit"
            public float  confidence;   // 0.0 – 1.0

            // Nested probabilities — Unity JsonUtility supports nested objects
            public DiagnosisProbabilities probabilities;
        }

        [Serializable]
        private class DiagnosisProbabilities
        {
            // Field names mirror Python CLASS_NAMES but with underscores
            // so JsonUtility can deserialise them.  The Python pipeline
            // must use these exact keys.
            public float Normal;
            // TASK 3: Strict JSON matching - exact names, no spaces
            public float MildDeficit;
            public float SevereDeficit;
        }
    }
}
