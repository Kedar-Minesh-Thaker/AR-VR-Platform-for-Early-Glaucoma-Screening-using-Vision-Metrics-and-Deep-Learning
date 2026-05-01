// TestResultRecord.cs
// POCO mapping for the test_results table in the local SQLite database.

using System;

namespace OphthalSuite.Core.Database
{
    [Serializable]
    public class TestResultRecord
    {
        public long id;             // autoincrement PK
        public string sessionId;
        public string testId;
        public string displayName;
        public float durationSec;
        public float falsePosRate;
        public float falseNegRate;
        public float fixationLoss;
        public string reliability;  // "Acceptable" / "Questionable" / "Unreliable"
        public string fullResult;   // full JSON result blob
        public string timestamp;    // ISO-8601 UTC
    }
}
