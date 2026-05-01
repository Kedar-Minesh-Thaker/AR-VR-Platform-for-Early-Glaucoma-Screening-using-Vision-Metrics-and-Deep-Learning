// ClinicalLogModels.cs
// Row-oriented models for unified export (CSV/JSONL) and future ML pipelines.
// Distinct from doctor-stream WebSocket envelopes; can be derived from TestTrialEvent.

using System;
using UnityEngine;

namespace OphthalSuite.Core.Logging
{
    public enum ResponseOutcome
    {
        Unknown = 0,
        Seen = 1,
        NotSeen = 2,
        Invalid = 3,
        Withheld = 4
    }

    /// <summary>
    /// One row per completed trial — stable column set for analytics / ML.
    /// </summary>
    [Serializable]
    public class TrialLogRecord
    {
        public string sessionId;
        public string testId;
        public string testDisplayName;
        public int globalTrialIndex;
        public string stimulusId;
        public string stimulusLabel;
        public float intensityValue;
        public string intensityUnit;
        public ResponseOutcome responseOutcome;
        public bool hit;
        public float reactionTimeSec;
        public bool fixationLost;
        public bool isCatch;
        public string presentationOnsetUtc;
        public string responseUtc;
        public float interStimulusIntervalSec;
        public string platform;
        public string appVersion;
        public string moduleVersion;
        public string featureSchemaId;
        public string featureBundleVersion;
        public string extensionJson;
    }

    [Serializable]
    public class SessionLogHeader
    {
        public string sessionId;
        public string patientId;
        public string eye;
        public int age;
        public string deviceId;
        public string startedUtc;
        public string appVersion;
        public string unityVersion;
        public string platform;
    }

    /// <summary>
    /// Maps core trial events into <see cref="TrialLogRecord"/> without interpreting test-specific extraJson.
    /// </summary>
    public static class TrialLogRecordFactory
    {
        public static TrialLogRecord FromTestTrialEvent(
            global::OphthalSuite.Core.TestTrialEvent e,
            string testDisplayName,
            string moduleVersion,
            string featureSchemaId,
            string featureBundleVersion)
        {
            var outcome = e.hit ? ResponseOutcome.Seen : ResponseOutcome.NotSeen;
            return new TrialLogRecord
            {
                sessionId = e.sessionId,
                testId = e.testId,
                testDisplayName = testDisplayName ?? "",
                globalTrialIndex = e.trialIndex,
                stimulusId = e.stimulusId ?? "",
                stimulusLabel = e.stimulusId ?? "",
                intensityValue = float.NaN,
                intensityUnit = "",
                responseOutcome = outcome,
                hit = e.hit,
                reactionTimeSec = e.reactionTimeSec,
                fixationLost = e.fixationLost,
                isCatch = e.isCatch,
                presentationOnsetUtc = "",
                responseUtc = e.timestamp ?? "",
                interStimulusIntervalSec = float.NaN,
                platform = Application.platform.ToString(),
                appVersion = Application.version,
                moduleVersion = moduleVersion ?? "",
                featureSchemaId = featureSchemaId ?? "",
                featureBundleVersion = featureBundleVersion ?? "",
                extensionJson = e.extraJson ?? ""
            };
        }
    }
}
