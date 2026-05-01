// TrialRecord.cs
// POCO mapping for the trials table in the local SQLite database.

using System;

namespace OphthalSuite.Core.Database
{
    [Serializable]
    public class TrialRecord
    {
        public long id;             // autoincrement PK
        public string sessionId;
        public string testId;
        public int trialIndex;
        public string stimulusId;
        public bool isCatch;
        public bool hit;
        public float reactionTime;
        public bool fixationLost;
        public string extraJson;    // test-specific payload
        public string timestamp;    // ISO-8601 UTC
    }
}
