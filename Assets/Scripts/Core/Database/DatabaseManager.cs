// DatabaseManager.cs
// Singleton MonoBehaviour that manages a local JSON-file database for persisting
// all session, trial, and test result data on the Quest headset.
//
// Uses Unity's built-in JsonUtility — no external plugins or DLLs required.
// Database location: Application.persistentDataPath/db/
//   ├── sessions_index.json   (list of all sessions)
//   ├── sessions/
//   │   ├── {sessionId}.json  (session meta + trials + results)
//   │   └── ...
//
// Public API
// ----------
//   static DatabaseManager Instance
//   void InsertSession(SessionContext ctx)
//   void InsertTrial(TestTrialEvent evt)
//   void InsertTestResult(TestResult result)
//   void UpdateSessionEnd(string sessionId, float duration, string status)
//   List<SessionRecord> GetAllSessions()
//   List<SessionRecord> GetSessionsByPatient(string patientId)
//   List<TrialRecord> GetTrialsBySession(string sessionId)
//   List<TestResultRecord> GetResultsBySession(string sessionId)
//   string ExportSessionJson(string sessionId) — full session data as JSON

using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace OphthalSuite.Core.Database
{
    public class DatabaseManager : MonoBehaviour
    {
        // ── Singleton ────────────────────────────────────────────────────────────
        public static DatabaseManager Instance { get; private set; }

        private string _dbRoot;
        private string _sessionsDir;
        private string _indexPath;

        // In-memory index of session headers (kept in sync with disk)
        private SessionIndex _index;

        // Currently active session data (loaded into memory for fast writes)
        private SessionData _activeSession;
        private string _activeSessionId;

        // ── Unity lifecycle ──────────────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            _dbRoot = Path.Combine(Application.persistentDataPath, "db");
            _sessionsDir = Path.Combine(_dbRoot, "sessions");
            _indexPath = Path.Combine(_dbRoot, "sessions_index.json");

            Directory.CreateDirectory(_sessionsDir);
            LoadIndex();

            Debug.Log($"DatabaseManager: JSON database at {_dbRoot}");
        }

        private void OnDestroy()
        {
            // Flush any active session before shutdown
            FlushActiveSession();
            if (Instance == this) Instance = null;
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused) FlushActiveSession();
        }

        // ── Index management ─────────────────────────────────────────────────────

        private void LoadIndex()
        {
            if (File.Exists(_indexPath))
            {
                try
                {
                    string json = File.ReadAllText(_indexPath);
                    _index = JsonUtility.FromJson<SessionIndex>(json);
                    if (_index == null || _index.sessions == null)
                        _index = new SessionIndex();
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"DatabaseManager: Failed to load index: {ex.Message}");
                    _index = new SessionIndex();
                }
            }
            else
            {
                _index = new SessionIndex();
            }
        }

        private void SaveIndex()
        {
            try
            {
                string json = JsonUtility.ToJson(_index, true);
                File.WriteAllText(_indexPath, json);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"DatabaseManager: Failed to save index: {ex.Message}");
            }
        }

        // ── Session file I/O ─────────────────────────────────────────────────────

        private string SessionFilePath(string sessionId)
        {
            return Path.Combine(_sessionsDir, $"{sessionId}.json");
        }

        private SessionData LoadSessionData(string sessionId)
        {
            string path = SessionFilePath(sessionId);
            if (!File.Exists(path)) return null;

            try
            {
                string json = File.ReadAllText(path);
                return JsonUtility.FromJson<SessionData>(json);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"DatabaseManager: Failed to load session {sessionId}: {ex.Message}");
                return null;
            }
        }

        private void SaveSessionData(string sessionId, SessionData data)
        {
            try
            {
                string json = JsonUtility.ToJson(data, true);
                File.WriteAllText(SessionFilePath(sessionId), json);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"DatabaseManager: Failed to save session {sessionId}: {ex.Message}");
            }
        }

        private void FlushActiveSession()
        {
            if (_activeSession != null && !string.IsNullOrEmpty(_activeSessionId))
            {
                SaveSessionData(_activeSessionId, _activeSession);
            }
        }

        // ── Insert methods ───────────────────────────────────────────────────────

        /// <summary>Insert a new session when StartSession is called.</summary>
        public void InsertSession(SessionContext ctx)
        {
            try
            {
                // Create index entry
                var header = new SessionRecord
                {
                    id = ctx.sessionId,
                    patientId = ctx.patientId ?? "",
                    eye = ctx.eye ?? "",
                    age = ctx.age,
                    deviceId = ctx.deviceId ?? "",
                    startedAt = DateTime.UtcNow.ToString("o"),
                    endedAt = "",
                    durationSec = 0f,
                    status = "running"
                };

                // Add to index (avoid duplicates)
                _index.sessions.RemoveAll(s => s.id == ctx.sessionId);
                _index.sessions.Insert(0, header);
                SaveIndex();

                // Create session data file
                _activeSessionId = ctx.sessionId;
                _activeSession = new SessionData
                {
                    session = header,
                    trials = new List<TrialRecord>(),
                    testResults = new List<TestResultRecord>()
                };
                SaveSessionData(ctx.sessionId, _activeSession);

                Debug.Log($"DatabaseManager: Session {ctx.sessionId} created.");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"DatabaseManager.InsertSession failed: {ex.Message}");
            }
        }

        /// <summary>Insert a trial record after each stimulus/response.</summary>
        public void InsertTrial(TestTrialEvent evt)
        {
            try
            {
                // Ensure we have the active session loaded
                if (_activeSession == null || _activeSessionId != evt.sessionId)
                {
                    FlushActiveSession();
                    _activeSessionId = evt.sessionId;
                    _activeSession = LoadSessionData(evt.sessionId);
                    if (_activeSession == null)
                    {
                        Debug.LogWarning($"DatabaseManager.InsertTrial: Session {evt.sessionId} not found.");
                        return;
                    }
                }

                var record = new TrialRecord
                {
                    id = _activeSession.trials.Count + 1,
                    sessionId = evt.sessionId ?? "",
                    testId = evt.testId ?? "",
                    trialIndex = evt.trialIndex,
                    stimulusId = evt.stimulusId ?? "",
                    isCatch = evt.isCatch,
                    hit = evt.hit,
                    reactionTime = evt.reactionTimeSec,
                    fixationLost = evt.fixationLost,
                    extraJson = evt.extraJson ?? "",
                    timestamp = evt.timestamp ?? DateTime.UtcNow.ToString("o")
                };

                _activeSession.trials.Add(record);

                // Flush to disk every 10 trials (balance performance vs safety)
                if (_activeSession.trials.Count % 10 == 0)
                    SaveSessionData(_activeSessionId, _activeSession);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"DatabaseManager.InsertTrial failed: {ex.Message}");
            }
        }

        /// <summary>Insert a test result when a test completes.</summary>
        public void InsertTestResult(TestResult result)
        {
            try
            {
                if (_activeSession == null || _activeSessionId != result.sessionId)
                {
                    FlushActiveSession();
                    _activeSessionId = result.sessionId;
                    _activeSession = LoadSessionData(result.sessionId);
                    if (_activeSession == null) return;
                }

                var record = new TestResultRecord
                {
                    id = _activeSession.testResults.Count + 1,
                    sessionId = result.sessionId ?? "",
                    testId = result.testId ?? "",
                    displayName = result.displayName ?? "",
                    durationSec = result.durationSeconds,
                    falsePosRate = result.falsePosRate,
                    falseNegRate = result.falseNegRate,
                    fixationLoss = result.fixationLossRate,
                    reliability = result.reliabilityCategory ?? "",
                    fullResult = result.fullResultJson ?? "",
                    timestamp = result.timestamp ?? DateTime.UtcNow.ToString("o")
                };

                _activeSession.testResults.Add(record);
                SaveSessionData(_activeSessionId, _activeSession);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"DatabaseManager.InsertTestResult failed: {ex.Message}");
            }
        }

        /// <summary>Update session status and duration when session ends.</summary>
        public void UpdateSessionEnd(string sessionId, float duration, string status)
        {
            try
            {
                if (_activeSession == null || _activeSessionId != sessionId)
                {
                    FlushActiveSession();
                    _activeSessionId = sessionId;
                    _activeSession = LoadSessionData(sessionId);
                }

                if (_activeSession != null)
                {
                    _activeSession.session.endedAt = DateTime.UtcNow.ToString("o");
                    _activeSession.session.durationSec = duration;
                    _activeSession.session.status = status ?? "completed";
                    SaveSessionData(sessionId, _activeSession);
                }

                // Update index
                var idx = _index.sessions.Find(s => s.id == sessionId);
                if (idx != null)
                {
                    idx.endedAt = DateTime.UtcNow.ToString("o");
                    idx.durationSec = duration;
                    idx.status = status ?? "completed";
                    SaveIndex();
                }

                // Clear active session
                _activeSession = null;
                _activeSessionId = null;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"DatabaseManager.UpdateSessionEnd failed: {ex.Message}");
            }
        }

        // ── Query methods ────────────────────────────────────────────────────────

        /// <summary>Get all sessions, most recent first.</summary>
        public List<SessionRecord> GetAllSessions()
        {
            return new List<SessionRecord>(_index.sessions);
        }

        /// <summary>Get sessions for a specific patient.</summary>
        public List<SessionRecord> GetSessionsByPatient(string patientId)
        {
            return _index.sessions.FindAll(s =>
                string.Equals(s.patientId, patientId, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>Get all trials for a given session.</summary>
        public List<TrialRecord> GetTrialsBySession(string sessionId)
        {
            // Check active session first
            if (_activeSession != null && _activeSessionId == sessionId)
                return new List<TrialRecord>(_activeSession.trials);

            var data = LoadSessionData(sessionId);
            return data?.trials ?? new List<TrialRecord>();
        }

        /// <summary>Get all test results for a given session.</summary>
        public List<TestResultRecord> GetResultsBySession(string sessionId)
        {
            if (_activeSession != null && _activeSessionId == sessionId)
                return new List<TestResultRecord>(_activeSession.testResults);

            var data = LoadSessionData(sessionId);
            return data?.testResults ?? new List<TestResultRecord>();
        }

        /// <summary>
        /// Export full session data as a JSON string for WebSocket DB_SYNC broadcast.
        /// </summary>
        public string ExportSessionJson(string sessionId)
        {
            // Flush if this is the active session
            if (_activeSession != null && _activeSessionId == sessionId)
                SaveSessionData(sessionId, _activeSession);

            string path = SessionFilePath(sessionId);
            if (!File.Exists(path)) return "{}";

            try
            {
                // Read the raw JSON and wrap it with messageType
                string raw = File.ReadAllText(path);
                var data = JsonUtility.FromJson<SessionData>(raw);
                if (data == null) return "{}";

                // Build the DB_SYNC envelope manually
                var sb = new System.Text.StringBuilder();
                sb.Append("{");
                sb.Append("\"messageType\":\"DB_SYNC\",");
                sb.Append($"\"sessionId\":\"{Escape(data.session.id)}\",");
                sb.Append($"\"patientId\":\"{Escape(data.session.patientId)}\",");
                sb.Append($"\"eye\":\"{Escape(data.session.eye)}\",");
                sb.Append($"\"age\":{data.session.age},");
                sb.Append($"\"status\":\"{Escape(data.session.status)}\",");
                sb.Append($"\"durationSec\":{data.session.durationSec:F2},");
                sb.Append($"\"startedAt\":\"{Escape(data.session.startedAt)}\",");
                sb.Append($"\"endedAt\":\"{Escape(data.session.endedAt)}\",");

                // Trials array
                sb.Append("\"trials\":[");
                for (int i = 0; i < data.trials.Count; i++)
                {
                    if (i > 0) sb.Append(",");
                    var t = data.trials[i];
                    sb.Append("{");
                    sb.Append($"\"testId\":\"{Escape(t.testId)}\",");
                    sb.Append($"\"trialIndex\":{t.trialIndex},");
                    sb.Append($"\"stimulusId\":\"{Escape(t.stimulusId)}\",");
                    sb.Append($"\"isCatch\":{(t.isCatch ? "true" : "false")},");
                    sb.Append($"\"hit\":{(t.hit ? "true" : "false")},");
                    sb.Append($"\"reactionTime\":{t.reactionTime:F3},");
                    sb.Append($"\"fixationLost\":{(t.fixationLost ? "true" : "false")},");
                    sb.Append($"\"timestamp\":\"{Escape(t.timestamp)}\"");
                    sb.Append("}");
                }
                sb.Append("],");

                // Results array
                sb.Append("\"testResults\":[");
                for (int i = 0; i < data.testResults.Count; i++)
                {
                    if (i > 0) sb.Append(",");
                    var r = data.testResults[i];
                    sb.Append("{");
                    sb.Append($"\"testId\":\"{Escape(r.testId)}\",");
                    sb.Append($"\"displayName\":\"{Escape(r.displayName)}\",");
                    sb.Append($"\"durationSec\":{r.durationSec:F2},");
                    sb.Append($"\"falsePosRate\":{r.falsePosRate:F3},");
                    sb.Append($"\"falseNegRate\":{r.falseNegRate:F3},");
                    sb.Append($"\"fixationLoss\":{r.fixationLoss:F3},");
                    sb.Append($"\"reliability\":\"{Escape(r.reliability)}\",");
                    sb.Append($"\"timestamp\":\"{Escape(r.timestamp)}\"");
                    sb.Append("}");
                }
                sb.Append("]");

                sb.Append("}");
                return sb.ToString();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"DatabaseManager.ExportSessionJson failed: {ex.Message}");
                return "{}";
            }
        }

        // ── Helpers ──────────────────────────────────────────────────────────────

        private static string Escape(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n");
        }

        // ── Serializable data structures ─────────────────────────────────────────

        [Serializable]
        private class SessionIndex
        {
            public List<SessionRecord> sessions = new List<SessionRecord>();
        }

        [Serializable]
        private class SessionData
        {
            public SessionRecord session;
            public List<TrialRecord> trials = new List<TrialRecord>();
            public List<TestResultRecord> testResults = new List<TestResultRecord>();
        }
    }
}
