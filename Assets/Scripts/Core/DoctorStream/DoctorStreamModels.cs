// DoctorStreamModels.cs
// Additional WebSocket-friendly payloads for the laptop doctor mirror.
// Existing envelopes in SharedSchema (TRIAL, SESSION_STATE, etc.) remain the primary trial/result stream.

using System;
using UnityEngine;

namespace OphthalSuite.Core
{
    /// <summary>
    /// Passed to <see cref="IVisualTestModule.OnStimulusPresentation"/>; can also be wrapped for Broadcast.
    /// </summary>
    [Serializable]
    public class StimulusPresentationMeta
    {
        public string sessionId;
        public string testId;
        public string trialLocalId;
        public string stimulusId;
        public float intensityValue;
        public string intensityUnit;
        public string presentationOnsetUtc;
        public float plannedDurationSec;
        public float responseWindowSec;
        public bool isCatch;
        public string extraJson;
    }

    [Serializable]
    public class DoctorStimulusState
    {
        public string messageType = "STIMULUS_STATE";
        public string sessionId;
        public string testId;
        public string stimulusId;
        public bool visible;
        public float intensityValue;
        public string intensityUnit;
        public float worldPosX;
        public float worldPosY;
        public float worldPosZ;
        public string timestampUtc;
        public string extraJson;
    }

    [Serializable]
    public class DoctorPatientInput
    {
        public string messageType = "PATIENT_INPUT";
        public string sessionId;
        public string testId;
        public string kind;
        public float screenX;
        public float screenY;
        public string timestampUtc;
        public string extraJson;
    }

    /// <summary>
    /// Provenance and feature-bundle hooks for future ML optimization (policy / labeling).
    /// </summary>
    /// <summary>
    /// Unified clinician overlay row: sent after each trial (alongside TRIAL) for dashboard HUD.
    /// </summary>
    [Serializable]
    public class DoctorTrialOverlay
    {
        public string messageType = "TRIAL_OVERLAY";
        public string sessionId;
        public string testId;
        public string testDisplayName;
        public string stimulusId;
        public float intensityValue;
        public string intensityUnit;
        public float coordX;
        public float coordY;
        public float coordZ;
        public string progressText;
        public bool hit;
        public bool miss;
        public bool isCatch;
        public bool fixationLost;
        public float reactionTimeSec;
        public string responseLabel;
        public string reliabilityCategory;
        public string extraJson;
    }

    [Serializable]
    public class DoctorMlRunContext
    {
        public string messageType = "ML_RUN_CONTEXT";
        public string sessionId;
        public string testId;
        public string appVersion;
        public string unityVersion;
        public string platform;
        public string featureSchemaId;
        public string featureBundleVersion;
        public string timestampUtc;
        public string notes;
    }
}
