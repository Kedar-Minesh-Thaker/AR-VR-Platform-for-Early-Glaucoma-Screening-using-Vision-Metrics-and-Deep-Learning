// AppOrchestrator.cs
// Top-level scene MonoBehaviour that manages patient sessions,
// runs test modules in sequence, routes events to SharedDoctorMirror,
// and writes unified output files.

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using OphthalSuite.Core.Database;
using OphthalSuite.Core;

namespace OphthalSuite.Core
{
    public class AppOrchestrator : MonoBehaviour
    {
        // ── Inspector ────────────────────────────────────────────────────────────
        [Header("Test modules")]
        [Tooltip("If set, module order and roots come from VisualTestManager (single source of truth).")]
        [SerializeField] private VisualTestManager visualTestManager;

        [Tooltip("Used only when Visual Test Manager is not assigned.")]
        [SerializeField] private List<MonoBehaviour> testModules = new List<MonoBehaviour>();

        [Tooltip("Before each test, activate only that module's root via VisualTestManager.LoadTest.")]
        [SerializeField] private bool activateTestsViaManager;

        [Header("Instruction UI")]
        [Tooltip("Assign the TestInstructionUI component. If null, tests start without instructions.")]
        [SerializeField] private TestInstructionUI instructionUI;

        [Header("Session End UI")]
        [Tooltip("Assign the SessionEndUI component. Shows summary + Next Patient after all tests.")]
        [SerializeField] private SessionEndUI sessionEndUI;

        [Header("Session Settings")]
        [SerializeField] private float pauseBetweenTestsSec = 3f;

        [Header("Output")]
        [SerializeField] private string sessionsFolderName = "sessions";

        // ── Runtime state ────────────────────────────────────────────────────────
        private SessionContext _ctx;
        private List<ITestModule> _tests = new List<ITestModule>();
        private List<ITestModule> _sessionQueue = new List<ITestModule>();
        private List<TestResult> _testResults = new List<TestResult>();
        private int _currentTestIndex = -1;
        private bool _sessionRunning;
        private float _sessionStartTime;
        private int _globalTrialIndex;
        private readonly Dictionary<string, string> _reliabilityByTestId = new Dictionary<string, string>();
        private bool _skipRestRequested;
        private bool _skipNextTestRequested;
        private bool _skipCurrentTestRequested;
        private bool _returnToMainMenuRequested;
        private string _requestedSwitchTestId;


        // CSV writer for unified trial log
        private StreamWriter _csvWriter;
        private string _sessionDir;

        // ── Properties ───────────────────────────────────────────────────────────
        public SessionContext CurrentSession => _ctx;
        public bool IsSessionRunning => _sessionRunning;
        public ITestModule CurrentTest => _currentTestIndex >= 0 && _currentTestIndex < _sessionQueue.Count
            ? _sessionQueue[_currentTestIndex] : null;

        // ── Unity lifecycle ──────────────────────────────────────────────────────
        private void Awake()
        {
            _tests.Clear();

            if (visualTestManager != null)
            {
                foreach (var mod in visualTestManager.GetModulesInOrder())
                    _tests.Add(mod);
            }
            else
            {
                foreach (var mb in testModules)
                {
                    if (mb == null) continue;
                    if (mb is ITestModule mod)
                        _tests.Add(mod);
                    else
                        Debug.LogError($"AppOrchestrator: {mb.name} does not implement ITestModule. Skipping.");
                }
            }

            if (_tests.Count == 0)
                Debug.LogWarning("AppOrchestrator: no test modules assigned.");
        }

        // ── Public API ───────────────────────────────────────────────────────────

        /// <summary>Begin a new patient session. Generates a GUID sessionId and starts the first test.</summary>
        /// <param name="onlyTheseTestIds">If non-null and non-empty, run only these tests (in this order). IDs must match <see cref="ITestModule.TestId"/>.</param>
        public void StartSession(string patientId, string eye, int age, IList<string> onlyTheseTestIds = null)
        {
            if (_sessionRunning)
            {
                Debug.LogWarning("AppOrchestrator: session already running.");
                return;
            }

            _sessionQueue = BuildSessionQueue(onlyTheseTestIds);
            if (_sessionQueue.Count == 0)
            {
                Debug.LogError("AppOrchestrator: no tests in session queue (check filter / VisualTestManager).");
                return;
            }

            if (string.IsNullOrWhiteSpace(patientId)) patientId = "P001";
            if (string.IsNullOrWhiteSpace(eye)) eye = "OD";
            age = 0; // Age intentionally not stored for patient sessions.

            _ctx = new SessionContext
            {
                sessionId = Guid.NewGuid().ToString(),
                patientId = patientId,
                eye       = eye,
                age       = age,
                deviceId  = SystemInfo.deviceUniqueIdentifier
            };

            _testResults.Clear();
            _currentTestIndex  = -1;
            _globalTrialIndex  = 0;
            _sessionStartTime  = Time.realtimeSinceStartup;
            _sessionRunning    = true;
            _reliabilityByTestId.Clear();

            // Create output directory
            _sessionDir = Path.Combine(Application.persistentDataPath,
                                       sessionsFolderName, _ctx.sessionId);
            Directory.CreateDirectory(_sessionDir);

            // Write session meta
            string metaJson = JsonUtility.ToJson(_ctx, true);
            File.WriteAllText(Path.Combine(_sessionDir, "session_meta.json"), metaJson);

            // ── Database: insert session ─────────────────────────────────────
            DatabaseManager.Instance?.InsertSession(_ctx);

            // Open unified CSV
            string csvPath = Path.Combine(_sessionDir, "session_trials.csv");
            _csvWriter = new StreamWriter(csvPath, false, Encoding.UTF8);
            _csvWriter.WriteLine("trial_index,test_id,stimulus_id,is_catch,hit," +
                                 "reaction_time_sec,fixation_lost,extra_json,timestamp");

            // Broadcast session state
            BroadcastSessionState();

            Debug.Log($"AppOrchestrator: session started — {_ctx.sessionId}");

            // Start first test
            StartCoroutine(RunTestSequence());
        }

        /// <summary>End the session early or after all tests complete.</summary>
        public void EndSession()
        {
            if (!_sessionRunning) return;

            // Stop current test if running
            if (_currentTestIndex >= 0 && _currentTestIndex < _sessionQueue.Count)
            {
                var test = _sessionQueue[_currentTestIndex];
                if (test.IsRunning)
                {
                    UnsubscribeTest(test);
                    test.StopTest();
                }
            }

            StopAllCoroutines();
            FinaliseSession();
        }

        /// <summary>Immediately starts the next test if in a rest period.</summary>
        public void SkipCurrentRest()
        {
            _skipRestRequested = true;
            Debug.Log("AppOrchestrator: Skip rest requested.");
        }

        /// <summary>Skips the next scheduled test in the session queue.</summary>
        public void SkipNextTest()
        {
            _skipNextTestRequested = true;
            Debug.Log("AppOrchestrator: Skip next test requested.");
        }

        /// <summary>Immediately stops the currently running test and advances.</summary>
        public void SkipCurrentTest()
        {
            _skipCurrentTestRequested = true;
            Debug.Log("AppOrchestrator: Skip current test requested.");
        }

        /// <summary>Abort current test and return to main menu (SessionStartUI).</summary>
        public void ReturnToMainMenu()
        {
            _returnToMainMenuRequested = true;
            _skipCurrentTestRequested = true; // also stops any running test
            Debug.Log("AppOrchestrator: Return to main menu requested.");
        }

        /// <summary>Stop the current test and run the requested test next in the same session.</summary>
        public void SwitchToTest(string testId)
        {
            if (string.IsNullOrEmpty(testId)) return;
            _requestedSwitchTestId = testId;
            _skipCurrentTestRequested = true;
            Debug.Log($"AppOrchestrator: switch to test requested — {testId}");
        }

        /// <summary>Total test count in current session queue.</summary>
        public int TotalTestCount => _sessionQueue?.Count ?? 0;

        /// <summary>Number of completed tests so far.</summary>
        public int CompletedTestCount => _testResults?.Count ?? 0;


        // ── Coroutine: run tests in sequence ─────────────────────────────────────

        private IEnumerator RunTestSequence()
        {
            for (int i = 0; i < _sessionQueue.Count; i++)
            {
                if (!_sessionRunning) yield break;

                // Pause between tests (skip before the very first one)
                if (i > 0)
                {
                    Debug.Log($"AppOrchestrator: pausing {pauseBetweenTestsSec}s before next test…");
                    _skipRestRequested = false;
                    _skipNextTestRequested = false;
                    
                    BroadcastSessionState();
                    
                    float restTimer = 0;
                    while (restTimer < pauseBetweenTestsSec && !_skipRestRequested && !_skipNextTestRequested)
                    {
                        restTimer += Time.deltaTime;
                        yield return null;
                    }

                    if (_skipNextTestRequested)
                    {
                        Debug.Log("AppOrchestrator: Skipping test module " + _sessionQueue[i].TestId);
                        continue;
                    }
                }

                if (!_sessionRunning) yield break;

                // ── Show pre-test instruction screen ─────────────────────────
                var upcomingTest = _sessionQueue[i];
                if (instructionUI != null)
                {
                    instructionUI.ShowInstruction(upcomingTest, i + 1, _sessionQueue.Count);

                    // Wait until patient presses START or SKIP
                    while (instructionUI.IsShowing && _sessionRunning)
                        yield return null;

                    if (!_sessionRunning) yield break;

                    if (instructionUI.WasSkipped)
                    {
                        Debug.Log($"AppOrchestrator: Patient skipped test {upcomingTest.TestId} from instruction screen.");
                        SavePartialResult(upcomingTest, "skipped_before_start");
                        continue;
                    }
                }

                if (!_sessionRunning) yield break;

                _currentTestIndex = i;
                var test = _sessionQueue[i];
                _skipCurrentTestRequested = false;

                if (activateTestsViaManager && visualTestManager != null)
                    visualTestManager.LoadTest(test.TestId);

                Debug.Log($"AppOrchestrator: starting test {test.TestId} ({test.DisplayName})");

                // Subscribe to events
                SubscribeTest(test);

                // Broadcast updated state
                BroadcastSessionState();

                // Start test
                test.StartTest(_ctx);

                // Wait until test completes or is skipped
                while (test.IsRunning && !_skipCurrentTestRequested)
                    yield return null;

                // If skip was requested, force-stop the running test
                if (_skipCurrentTestRequested && test.IsRunning)
                {
                    Debug.Log($"AppOrchestrator: Force-stopping test {test.TestId} (skip requested).");

                    // ── Save partial result before stopping ─────────────────────
                    SavePartialResult(test);

                    UnsubscribeTest(test);
                    test.StopTest();
                    _skipCurrentTestRequested = false;
                }

                if (!string.IsNullOrEmpty(_requestedSwitchTestId))
                {
                    var next = FindRegisteredTest(_requestedSwitchTestId);
                    string targetId = _requestedSwitchTestId;
                    _requestedSwitchTestId = null;

                    if (next != null)
                    {
                        for (int j = _sessionQueue.Count - 1; j > i; j--)
                        {
                            if (_sessionQueue[j].TestId == next.TestId)
                                _sessionQueue.RemoveAt(j);
                        }

                        _sessionQueue.Insert(i + 1, next);
                        Debug.Log($"AppOrchestrator: queued switched test next — {targetId}");
                    }
                    else
                    {
                        Debug.LogWarning($"AppOrchestrator: requested switch target not registered — {targetId}");
                    }
                }

                // Return to main menu if requested
                if (_returnToMainMenuRequested)
                {
                    _returnToMainMenuRequested = false;
                    break; // exit the test loop, go to FinaliseSession
                }
            }

            // All tests done (or main menu requested)
            yield return null;
            FinaliseSession();
        }

        // ── Event subscriptions ──────────────────────────────────────────────────

        private void SubscribeTest(ITestModule test)
        {
            test.OnTrialEnd    += HandleTrialEnd;
            test.OnTestComplete += HandleTestComplete;
        }

        private void UnsubscribeTest(ITestModule test)
        {
            test.OnTrialEnd    -= HandleTrialEnd;
            test.OnTestComplete -= HandleTestComplete;
        }

        private ITestModule FindRegisteredTest(string testId)
        {
            foreach (var t in _tests)
            {
                if (t != null && t.TestId == testId)
                    return t;
            }
            return null;
        }

        // ── Event handlers ───────────────────────────────────────────────────────

        private void HandleTrialEnd(TestTrialEvent evt)
        {
            _globalTrialIndex++;
            evt.trialIndex = _globalTrialIndex;
            evt.sessionId  = _ctx.sessionId;
            evt.timestamp  = DateTime.UtcNow.ToString("o");

            // Write to unified CSV
            if (_csvWriter != null)
            {
                _csvWriter.WriteLine(string.Join(",",
                    evt.trialIndex,
                    EscapeCsv(evt.testId),
                    EscapeCsv(evt.stimulusId),
                    evt.isCatch,
                    evt.hit,
                    evt.reactionTimeSec.ToString("F3"),
                    evt.fixationLost,
                    EscapeCsv(evt.extraJson ?? ""),
                    evt.timestamp
                ));
                _csvWriter.Flush();
            }

            // ── Database: insert trial ───────────────────────────────────────
            DatabaseManager.Instance?.InsertTrial(evt);

            // Broadcast to doctor dashboard
            if (SharedDoctorMirror.Instance != null)
            {
                SharedDoctorMirror.Instance.Broadcast(evt);
                BroadcastTrialOverlay(evt);
            }
        }

        private void HandleTestComplete(TestResult result)
        {
            result.sessionId = _ctx.sessionId;
            result.timestamp = DateTime.UtcNow.ToString("o");

            _testResults.Add(result);
            if (!string.IsNullOrEmpty(result.testId))
                _reliabilityByTestId[result.testId] = result.reliabilityCategory ?? "—";

            // Unsubscribe from this test
            if (_currentTestIndex >= 0 && _currentTestIndex < _sessionQueue.Count)
                UnsubscribeTest(_sessionQueue[_currentTestIndex]);

            // Broadcast result to dashboard
            if (SharedDoctorMirror.Instance != null)
                SharedDoctorMirror.Instance.Broadcast(result);

            // Write per-test result
            WritePerTestResult(result);

            // ── Database: insert test result ─────────────────────────────────
            DatabaseManager.Instance?.InsertTestResult(result);

            Debug.Log($"AppOrchestrator: test {result.testId} complete — {result.reliabilityCategory}");
        }

        // ── Session finalisation ─────────────────────────────────────────────────

        private void FinaliseSession()
        {
            _sessionRunning = false;
            _csvWriter?.Close();
            _csvWriter = null;

            float totalDuration = Time.realtimeSinceStartup - _sessionStartTime;

            // Build SessionComplete
            var complete = new SessionComplete
            {
                sessionId          = _ctx.sessionId,
                patientId          = _ctx.patientId,
                eye                = _ctx.eye,
                timestamp          = DateTime.UtcNow.ToString("o"),
                totalDurationSeconds = totalDuration,
                testResultsJson    = ToJsonArray(_testResults)
            };

            // Write session summary
            string summaryPath = Path.Combine(_sessionDir, "session_summary.json");
            File.WriteAllText(summaryPath, JsonUtility.ToJson(complete, true));

            // ── Database: finalise session ───────────────────────────────────
            DatabaseManager.Instance?.UpdateSessionEnd(_ctx.sessionId, totalDuration,
                _testResults.Count > 0 ? "completed" : "aborted");

            // Broadcast to dashboard
            if (SharedDoctorMirror.Instance != null)
            {
                SharedDoctorMirror.Instance.Broadcast(complete);

                // Broadcast full session data for laptop-side DB sync
                string dbSync = DatabaseManager.Instance?.ExportSessionJson(_ctx.sessionId);
                if (!string.IsNullOrEmpty(dbSync) && dbSync != "{}")
                {
                    SharedDoctorMirror.Instance.Broadcast(
                        new DbSyncEnvelope { rawJson = dbSync });
                }
            }

            Debug.Log($"AppOrchestrator: session ended — {_ctx.sessionId} ({totalDuration:F1}s)");

            // Show session-end summary with "Next Patient" button
            if (sessionEndUI != null)
            {
                sessionEndUI.Show(_testResults, _ctx, totalDuration);
            }
        }

        /// <summary>
        /// Called by SessionEndUI when "Next Patient" is clicked.
        /// Resets state so SessionStartUI can re-appear.
        /// </summary>
        public void PrepareNextPatient()
        {
            _ctx = null;
            _currentTestIndex = -1;
            _testResults.Clear();
            _reliabilityByTestId.Clear();
            Debug.Log("AppOrchestrator: Ready for next patient.");
        }

        /// <summary>
        /// Saves a partial TestResult when a test is skipped mid-execution.
        /// This ensures we never lose data even if the operator skips.
        /// </summary>
        private void SavePartialResult(ITestModule test, string reason = "skipped_partial")
        {
            var partial = new TestResult
            {
                testId              = test.TestId,
                displayName         = test.DisplayName,
                sessionId           = _ctx?.sessionId ?? "",
                durationSeconds     = 0,
                falsePosRate        = 0,
                falseNegRate        = 0,
                fixationLossRate    = 0,
                reliabilityCategory = "Partial",
                fullResultJson      = "{\"status\":\"" + reason + "\",\"testId\":\"" + EscapeJson(test.TestId) + "\",\"timestampUtc\":\"" + DateTime.UtcNow.ToString("o") + "\"}",
                timestamp           = DateTime.UtcNow.ToString("o")
            };

            _testResults.Add(partial);
            _reliabilityByTestId[test.TestId] = "Partial";
            WritePerTestResult(partial);
            DatabaseManager.Instance?.InsertTestResult(partial);

            Debug.Log($"AppOrchestrator: Saved partial result for skipped test {test.TestId}.");
        }

        // ── Per-test result files ────────────────────────────────────────────────

        private void WritePerTestResult(TestResult result)
        {
            if (string.IsNullOrEmpty(_sessionDir)) return;

            // E.g. sessions/{sessionId}/perimetry_24_2/
            string testDir = Path.Combine(_sessionDir,
                result.testId.ToLower().Replace(" ", "_"));
            Directory.CreateDirectory(testDir);

            string path = Path.Combine(testDir, "results_summary.json");
            File.WriteAllText(path, JsonUtility.ToJson(result, true));
        }

        // ── Session state broadcast ──────────────────────────────────────────────

        private void BroadcastSessionState()
        {
            if (SharedDoctorMirror.Instance == null || _ctx == null) return;

            var testSeq = new string[_sessionQueue.Count];
            var completed = new List<string>();

            for (int i = 0; i < _sessionQueue.Count; i++)
            {
                testSeq[i] = _sessionQueue[i].TestId;
                if (i < _currentTestIndex ||
                    (i == _currentTestIndex && _currentTestIndex >= 0 &&
                     !_sessionQueue[i].IsRunning && _testResults.Exists(r => r.testId == _sessionQueue[i].TestId)))
                {
                    completed.Add(_sessionQueue[i].TestId);
                }
            }

            var state = new SessionState
            {
                sessionId              = _ctx.sessionId,
                patientId              = _ctx.patientId,
                eye                    = _ctx.eye,
                age                    = _ctx.age,
                currentTestId          = _currentTestIndex >= 0 && _currentTestIndex < _sessionQueue.Count
                    ? _sessionQueue[_currentTestIndex].TestId : "",
                currentTestDisplayName = _currentTestIndex >= 0 && _currentTestIndex < _sessionQueue.Count
                    ? _sessionQueue[_currentTestIndex].DisplayName : "",
                testSequence           = testSeq,
                completedTests         = completed.ToArray()
            };

            SharedDoctorMirror.Instance.Broadcast(state);
        }

        private List<ITestModule> BuildSessionQueue(IList<string> onlyTheseTestIds)
        {
            var q = new List<ITestModule>();
            if (onlyTheseTestIds == null || onlyTheseTestIds.Count == 0)
            {
                q.AddRange(_tests);
                return q;
            }

            foreach (var id in onlyTheseTestIds)
            {
                if (string.IsNullOrEmpty(id)) continue;
                foreach (var t in _tests)
                {
                    if (t.TestId == id)
                    {
                        q.Add(t);
                        break;
                    }
                }
            }

            return q;
        }

        private void BroadcastTrialOverlay(TestTrialEvent evt)
        {
            if (SharedDoctorMirror.Instance == null) return;

            ITestModule cur = CurrentSessionTest;
            string display = cur?.DisplayName ?? evt.testId;
            float cx = 0f, cy = 0f, cz = 0f;
            string progressTxt = $"#{evt.trialIndex}";
            string intensityUnit = "";
            float intensityVal = float.NaN;

            if (!string.IsNullOrEmpty(evt.extraJson))
            {
                if (evt.extraJson.Contains("dbPresented"))
                {
                    var pe = JsonUtility.FromJson<PerimetryExtraParse>(evt.extraJson);
                    if (pe != null)
                    {
                        intensityVal = pe.dbPresented;
                        intensityUnit = "dB";
                        cx = pe.pointIndex;
                    }
                }
                else if (evt.extraJson.Contains("logMichelson"))
                {
                    var le = JsonUtility.FromJson<LogMichelsonParse>(evt.extraJson);
                    if (le != null)
                    {
                        intensityVal = le.logMichelson;
                        intensityUnit = "log_michelson";
                    }
                }
                else if (evt.extraJson.Contains("coherence"))
                {
                    var me = JsonUtility.FromJson<MotionExtraParse>(evt.extraJson);
                    if (me != null)
                    {
                        intensityVal = me.coherence;
                        intensityUnit = "coherence";
                    }
                }
                else if (evt.extraJson.Contains("logDelta"))
                {
                    var ed = JsonUtility.FromJson<EdgeLogParse>(evt.extraJson);
                    if (ed != null)
                    {
                        intensityVal = ed.logDelta;
                        intensityUnit = "log10_deltaL";
                    }
                }
            }

            var overlay = new DoctorTrialOverlay
            {
                sessionId = _ctx.sessionId,
                testId = evt.testId,
                testDisplayName = display,
                stimulusId = evt.stimulusId ?? "",
                intensityValue = float.IsNaN(intensityVal) ? -999f : intensityVal,
                intensityUnit = intensityUnit,
                coordX = cx,
                coordY = cy,
                coordZ = cz,
                progressText = progressTxt,
                hit = evt.hit,
                miss = !evt.hit,
                isCatch = evt.isCatch,
                fixationLost = evt.fixationLost,
                reactionTimeSec = evt.reactionTimeSec,
                responseLabel = evt.hit ? (evt.isCatch ? "CR" : "Hit") : (evt.isCatch ? "FA" : "Miss"),
                reliabilityCategory = _reliabilityByTestId.TryGetValue(evt.testId, out var rel) ? rel : "—",
                extraJson = evt.extraJson ?? ""
            };

            SharedDoctorMirror.Instance.Broadcast(overlay);
        }

        private ITestModule CurrentSessionTest =>
            _currentTestIndex >= 0 && _currentTestIndex < _sessionQueue.Count
                ? _sessionQueue[_currentTestIndex]
                : null;

        [Serializable]
        private class PerimetryExtraParse
        {
            public float dbPresented;
            public int pointIndex;
        }

        [Serializable]
        private class LogMichelsonParse
        {
            public float logMichelson;
        }

        [Serializable]
        private class MotionExtraParse
        {
            public float coherence;
        }

        [Serializable]
        private class EdgeLogParse
        {
            public float logDelta;
        }

        // ── Helpers ──────────────────────────────────────────────────────────────

        private static string EscapeCsv(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            if (s.Contains(",") || s.Contains("\"") || s.Contains("\n"))
                return "\"" + s.Replace("\"", "\"\"") + "\"";
            return s;
        }

        private static string EscapeJson(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n");
        }

        private static string ToJsonArray(List<TestResult> results)
        {
            var sb = new StringBuilder("[");
            for (int i = 0; i < results.Count; i++)
            {
                if (i > 0) sb.Append(",");
                sb.Append(JsonUtility.ToJson(results[i]));
            }
            sb.Append("]");
            return sb.ToString();
        }
    }
}
