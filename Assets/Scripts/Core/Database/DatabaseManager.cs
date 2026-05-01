// DatabaseManager.cs
// Singleton MonoBehaviour that manages a local SQLite database for persisting
// all session, trial, and test result data on the Quest headset.
//
// Uses Mono.Data.Sqlite (ships with Unity) — no external plugins required.
// Database file: Application.persistentDataPath/dhrishtilite.db
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

// Unity ships with Mono.Data.Sqlite on supported platforms
using Mono.Data.Sqlite;

namespace OphthalSuite.Core.Database
{
    public class DatabaseManager : MonoBehaviour
    {
        // ── Singleton ────────────────────────────────────────────────────────────
        public static DatabaseManager Instance { get; private set; }

        private string _dbPath;
        private string _connectionString;

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

            _dbPath = Path.Combine(Application.persistentDataPath, "dhrishtilite.db");
            _connectionString = $"URI=file:{_dbPath}";

            InitializeDatabase();
            Debug.Log($"DatabaseManager: SQLite database at {_dbPath}");
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ── Schema initialisation ────────────────────────────────────────────────

        private void InitializeDatabase()
        {
            using (var conn = new SqliteConnection(_connectionString))
            {
                conn.Open();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"
                        CREATE TABLE IF NOT EXISTS sessions (
                            id              TEXT PRIMARY KEY,
                            patient_id      TEXT NOT NULL,
                            eye             TEXT NOT NULL,
                            age             INTEGER,
                            device_id       TEXT,
                            started_at      TEXT NOT NULL,
                            ended_at        TEXT,
                            duration_sec    REAL DEFAULT 0,
                            status          TEXT DEFAULT 'running'
                        );

                        CREATE TABLE IF NOT EXISTS trials (
                            id              INTEGER PRIMARY KEY AUTOINCREMENT,
                            session_id      TEXT NOT NULL,
                            test_id         TEXT NOT NULL,
                            trial_index     INTEGER,
                            stimulus_id     TEXT,
                            is_catch        INTEGER DEFAULT 0,
                            hit             INTEGER DEFAULT 0,
                            reaction_time   REAL,
                            fixation_lost   INTEGER DEFAULT 0,
                            extra_json      TEXT,
                            timestamp       TEXT NOT NULL,
                            FOREIGN KEY (session_id) REFERENCES sessions(id)
                        );

                        CREATE TABLE IF NOT EXISTS test_results (
                            id              INTEGER PRIMARY KEY AUTOINCREMENT,
                            session_id      TEXT NOT NULL,
                            test_id         TEXT NOT NULL,
                            display_name    TEXT,
                            duration_sec    REAL,
                            false_pos_rate  REAL,
                            false_neg_rate  REAL,
                            fixation_loss   REAL,
                            reliability     TEXT,
                            full_result     TEXT,
                            timestamp       TEXT NOT NULL,
                            FOREIGN KEY (session_id) REFERENCES sessions(id)
                        );

                        CREATE INDEX IF NOT EXISTS idx_trials_session ON trials(session_id);
                        CREATE INDEX IF NOT EXISTS idx_trials_test ON trials(test_id);
                        CREATE INDEX IF NOT EXISTS idx_results_session ON test_results(session_id);
                        CREATE INDEX IF NOT EXISTS idx_sessions_patient ON sessions(patient_id);
                    ";
                    cmd.ExecuteNonQuery();
                }
            }
        }

        // ── Insert methods ───────────────────────────────────────────────────────

        /// <summary>Insert a new session when StartSession is called.</summary>
        public void InsertSession(SessionContext ctx)
        {
            try
            {
                using (var conn = new SqliteConnection(_connectionString))
                {
                    conn.Open();
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = @"
                            INSERT OR IGNORE INTO sessions (id, patient_id, eye, age, device_id, started_at, status)
                            VALUES (@id, @pid, @eye, @age, @dev, @started, 'running')";
                        cmd.Parameters.AddWithValue("@id", ctx.sessionId);
                        cmd.Parameters.AddWithValue("@pid", ctx.patientId ?? "");
                        cmd.Parameters.AddWithValue("@eye", ctx.eye ?? "");
                        cmd.Parameters.AddWithValue("@age", ctx.age);
                        cmd.Parameters.AddWithValue("@dev", ctx.deviceId ?? "");
                        cmd.Parameters.AddWithValue("@started", DateTime.UtcNow.ToString("o"));
                        cmd.ExecuteNonQuery();
                    }
                }
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
                using (var conn = new SqliteConnection(_connectionString))
                {
                    conn.Open();
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = @"
                            INSERT INTO trials (session_id, test_id, trial_index, stimulus_id,
                                                is_catch, hit, reaction_time, fixation_lost,
                                                extra_json, timestamp)
                            VALUES (@sid, @tid, @idx, @stim, @catch, @hit, @rt, @fix, @extra, @ts)";
                        cmd.Parameters.AddWithValue("@sid", evt.sessionId ?? "");
                        cmd.Parameters.AddWithValue("@tid", evt.testId ?? "");
                        cmd.Parameters.AddWithValue("@idx", evt.trialIndex);
                        cmd.Parameters.AddWithValue("@stim", evt.stimulusId ?? "");
                        cmd.Parameters.AddWithValue("@catch", evt.isCatch ? 1 : 0);
                        cmd.Parameters.AddWithValue("@hit", evt.hit ? 1 : 0);
                        cmd.Parameters.AddWithValue("@rt", evt.reactionTimeSec);
                        cmd.Parameters.AddWithValue("@fix", evt.fixationLost ? 1 : 0);
                        cmd.Parameters.AddWithValue("@extra", evt.extraJson ?? "");
                        cmd.Parameters.AddWithValue("@ts", evt.timestamp ?? DateTime.UtcNow.ToString("o"));
                        cmd.ExecuteNonQuery();
                    }
                }
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
                using (var conn = new SqliteConnection(_connectionString))
                {
                    conn.Open();
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = @"
                            INSERT INTO test_results (session_id, test_id, display_name, duration_sec,
                                                     false_pos_rate, false_neg_rate, fixation_loss,
                                                     reliability, full_result, timestamp)
                            VALUES (@sid, @tid, @name, @dur, @fpr, @fnr, @fix, @rel, @full, @ts)";
                        cmd.Parameters.AddWithValue("@sid", result.sessionId ?? "");
                        cmd.Parameters.AddWithValue("@tid", result.testId ?? "");
                        cmd.Parameters.AddWithValue("@name", result.displayName ?? "");
                        cmd.Parameters.AddWithValue("@dur", result.durationSeconds);
                        cmd.Parameters.AddWithValue("@fpr", result.falsePosRate);
                        cmd.Parameters.AddWithValue("@fnr", result.falseNegRate);
                        cmd.Parameters.AddWithValue("@fix", result.fixationLossRate);
                        cmd.Parameters.AddWithValue("@rel", result.reliabilityCategory ?? "");
                        cmd.Parameters.AddWithValue("@full", result.fullResultJson ?? "");
                        cmd.Parameters.AddWithValue("@ts", result.timestamp ?? DateTime.UtcNow.ToString("o"));
                        cmd.ExecuteNonQuery();
                    }
                }
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
                using (var conn = new SqliteConnection(_connectionString))
                {
                    conn.Open();
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = @"
                            UPDATE sessions SET ended_at = @ended, duration_sec = @dur, status = @status
                            WHERE id = @id";
                        cmd.Parameters.AddWithValue("@id", sessionId);
                        cmd.Parameters.AddWithValue("@ended", DateTime.UtcNow.ToString("o"));
                        cmd.Parameters.AddWithValue("@dur", duration);
                        cmd.Parameters.AddWithValue("@status", status ?? "completed");
                        cmd.ExecuteNonQuery();
                    }
                }
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
            var list = new List<SessionRecord>();
            try
            {
                using (var conn = new SqliteConnection(_connectionString))
                {
                    conn.Open();
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = "SELECT * FROM sessions ORDER BY started_at DESC";
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                                list.Add(ReadSession(reader));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"DatabaseManager.GetAllSessions failed: {ex.Message}");
            }
            return list;
        }

        /// <summary>Get sessions for a specific patient.</summary>
        public List<SessionRecord> GetSessionsByPatient(string patientId)
        {
            var list = new List<SessionRecord>();
            try
            {
                using (var conn = new SqliteConnection(_connectionString))
                {
                    conn.Open();
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = "SELECT * FROM sessions WHERE patient_id = @pid ORDER BY started_at DESC";
                        cmd.Parameters.AddWithValue("@pid", patientId);
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                                list.Add(ReadSession(reader));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"DatabaseManager.GetSessionsByPatient failed: {ex.Message}");
            }
            return list;
        }

        /// <summary>Get all trials for a given session.</summary>
        public List<TrialRecord> GetTrialsBySession(string sessionId)
        {
            var list = new List<TrialRecord>();
            try
            {
                using (var conn = new SqliteConnection(_connectionString))
                {
                    conn.Open();
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = "SELECT * FROM trials WHERE session_id = @sid ORDER BY trial_index";
                        cmd.Parameters.AddWithValue("@sid", sessionId);
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                list.Add(new TrialRecord
                                {
                                    id = reader.GetInt64(0),
                                    sessionId = reader.GetString(1),
                                    testId = reader.GetString(2),
                                    trialIndex = reader.GetInt32(3),
                                    stimulusId = GetStringOrEmpty(reader, 4),
                                    isCatch = reader.GetInt32(5) != 0,
                                    hit = reader.GetInt32(6) != 0,
                                    reactionTime = reader.GetFloat(7),
                                    fixationLost = reader.GetInt32(8) != 0,
                                    extraJson = GetStringOrEmpty(reader, 9),
                                    timestamp = reader.GetString(10)
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"DatabaseManager.GetTrialsBySession failed: {ex.Message}");
            }
            return list;
        }

        /// <summary>Get all test results for a given session.</summary>
        public List<TestResultRecord> GetResultsBySession(string sessionId)
        {
            var list = new List<TestResultRecord>();
            try
            {
                using (var conn = new SqliteConnection(_connectionString))
                {
                    conn.Open();
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = "SELECT * FROM test_results WHERE session_id = @sid ORDER BY id";
                        cmd.Parameters.AddWithValue("@sid", sessionId);
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                list.Add(new TestResultRecord
                                {
                                    id = reader.GetInt64(0),
                                    sessionId = reader.GetString(1),
                                    testId = reader.GetString(2),
                                    displayName = GetStringOrEmpty(reader, 3),
                                    durationSec = reader.GetFloat(4),
                                    falsePosRate = reader.GetFloat(5),
                                    falseNegRate = reader.GetFloat(6),
                                    fixationLoss = reader.GetFloat(7),
                                    reliability = GetStringOrEmpty(reader, 8),
                                    fullResult = GetStringOrEmpty(reader, 9),
                                    timestamp = reader.GetString(10)
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"DatabaseManager.GetResultsBySession failed: {ex.Message}");
            }
            return list;
        }

        /// <summary>
        /// Export full session data as a JSON string for WebSocket DB_SYNC broadcast.
        /// Includes session meta, all trials, and all test results.
        /// </summary>
        public string ExportSessionJson(string sessionId)
        {
            var sessions = GetSessionsByPatient(""); // We'll query by ID instead
            SessionRecord session = null;

            // Query session directly
            try
            {
                using (var conn = new SqliteConnection(_connectionString))
                {
                    conn.Open();
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = "SELECT * FROM sessions WHERE id = @id";
                        cmd.Parameters.AddWithValue("@id", sessionId);
                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                                session = ReadSession(reader);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"DatabaseManager.ExportSessionJson failed: {ex.Message}");
                return "{}";
            }

            if (session == null) return "{}";

            var trials = GetTrialsBySession(sessionId);
            var results = GetResultsBySession(sessionId);

            // Build JSON manually (avoid external JSON dependency)
            var sb = new System.Text.StringBuilder();
            sb.Append("{");
            sb.Append($"\"messageType\":\"DB_SYNC\",");
            sb.Append($"\"sessionId\":\"{Escape(session.id)}\",");
            sb.Append($"\"patientId\":\"{Escape(session.patientId)}\",");
            sb.Append($"\"eye\":\"{Escape(session.eye)}\",");
            sb.Append($"\"age\":{session.age},");
            sb.Append($"\"status\":\"{Escape(session.status)}\",");
            sb.Append($"\"durationSec\":{session.durationSec:F2},");
            sb.Append($"\"startedAt\":\"{Escape(session.startedAt)}\",");
            sb.Append($"\"endedAt\":\"{Escape(session.endedAt ?? "")}\",");

            // Trials array
            sb.Append("\"trials\":[");
            for (int i = 0; i < trials.Count; i++)
            {
                if (i > 0) sb.Append(",");
                var t = trials[i];
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
            for (int i = 0; i < results.Count; i++)
            {
                if (i > 0) sb.Append(",");
                var r = results[i];
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

        // ── Helpers ──────────────────────────────────────────────────────────────

        private static SessionRecord ReadSession(SqliteDataReader reader)
        {
            return new SessionRecord
            {
                id = reader.GetString(0),
                patientId = reader.GetString(1),
                eye = reader.GetString(2),
                age = reader.GetInt32(3),
                deviceId = GetStringOrEmpty(reader, 4),
                startedAt = reader.GetString(5),
                endedAt = GetStringOrEmpty(reader, 6),
                durationSec = reader.IsDBNull(7) ? 0f : reader.GetFloat(7),
                status = GetStringOrEmpty(reader, 8)
            };
        }

        private static string GetStringOrEmpty(SqliteDataReader reader, int index)
        {
            return reader.IsDBNull(index) ? "" : reader.GetString(index);
        }

        private static string Escape(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n");
        }
    }
}
