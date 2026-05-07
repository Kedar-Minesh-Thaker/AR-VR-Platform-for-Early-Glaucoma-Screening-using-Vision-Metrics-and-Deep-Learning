// SessionRecord.cs
// POCO mapping for the sessions table in the local SQLite database.

using System;

namespace OphthalSuite.Core.Database
{
    [Serializable]
    public class SessionRecord
    {
        public string id;           // UUID (sessionId)
        public string patientId;
        public string eye;          // "OD" / "OS"
        public string deviceId;
        public string startedAt;    // ISO-8601 UTC
        public string endedAt;      // ISO-8601 UTC (null while running)
        public float durationSec;
        public string status;       // "running" / "completed" / "aborted"
    }
}
