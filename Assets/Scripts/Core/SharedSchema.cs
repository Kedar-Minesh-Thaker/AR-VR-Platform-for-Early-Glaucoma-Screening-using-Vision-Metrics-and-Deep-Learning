// SharedSchema.cs
// Unified WebSocket envelope types used by ALL test modules.
// The doctor's dashboard uses the "testId" field to route display logic.

using System;
using UnityEngine;

namespace OphthalSuite.Core
{
    // ── Session context passed from orchestrator to each test ────────────────

    [Serializable]
    public class SessionContext
    {
        public string sessionId;        // UUID generated at session start
        public string patientId;
        public string eye;              // "OD" or "OS"
        public int    age;
        public string deviceId;         // SystemInfo.deviceUniqueIdentifier
    }

    // ── Per-trial event (sent after every trial in every test) ───────────────

    [Serializable]
    public class TestTrialEvent
    {
        // --- Envelope (all tests must populate these) ---
        public string messageType   = "TRIAL";      // always "TRIAL"
        public string sessionId;
        public string testId;                        // e.g. "PERIMETRY_24_2"
        public string timestamp;                     // UTC ISO-8601

        // --- Trial identity ---
        public int    trialIndex;
        public string stimulusId;     // locus name, spatial-frequency label, plate number, etc.
        public bool   isCatch;

        // --- Response ---
        public bool   hit;
        public float  reactionTimeSec;
        public bool   fixationLost;

        // --- Test-specific payload (JSON string, parsed by dashboard per testId) ---
        public string extraJson;      // e.g. "{\"dbPresented\":15.0}" for perimetry
    }

    // ── Test-complete event (sent once when a single test finishes) ──────────

    [Serializable]
    public class TestResult
    {
        // --- Envelope ---
        public string messageType   = "TEST_COMPLETE";
        public string sessionId;
        public string testId;
        public string displayName;
        public string timestamp;

        // --- Summary metrics (all tests) ---
        public float  durationSeconds;
        public float  falsePosRate;
        public float  falseNegRate;
        public float  fixationLossRate;
        public string reliabilityCategory;   // "Acceptable" / "Questionable" / "Unreliable"

        // --- Full JSON result from the test (test-specific, stored as string) ---
        public string fullResultJson;
    }

    // ── Session-complete event (sent once when the whole session ends) ───────

    [Serializable]
    public class SessionComplete
    {
        public string messageType   = "SESSION_COMPLETE";
        public string sessionId;
        public string patientId;
        public string eye;
        public string timestamp;
        public float  totalDurationSeconds;
        // Array of TestResult, serialised as JSON array string
        public string testResultsJson;
    }

    // ── Session-state snapshot (sent to newly connected dashboard clients) ───

    [Serializable]
    public class SessionState
    {
        public string messageType   = "SESSION_STATE";
        public string sessionId;
        public string patientId;
        public string eye;
        public int    age;
        public string currentTestId;
        public string currentTestDisplayName;
        public string[] testSequence;            // ordered list of TestIds
        public string[] completedTests;          // TestIds already finished
    }

    // ── DB Sync envelope (sent at end of session for laptop-side storage) ────

    [Serializable]
    public class DbSyncEnvelope
    {
        public string messageType = "DB_SYNC";
        /// <summary>Pre-built JSON string from DatabaseManager.ExportSessionJson()</summary>
        public string rawJson;
    }
}
