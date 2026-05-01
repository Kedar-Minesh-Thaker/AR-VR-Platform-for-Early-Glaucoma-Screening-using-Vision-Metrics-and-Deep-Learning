// VisualTestDescriptor.cs
// Static metadata for a visual test module (routing, UI, ML provenance).

using System;
using UnityEngine;

namespace OphthalSuite.Core
{
    public enum VisualTestCategory
    {
        Unknown = 0,
        Perimetry = 1,
        ContrastSensitivity = 2,
        ColorVision = 3,
        Custom = 99
    }

    [Flags]
    public enum VisualTestCapabilities
    {
        None = 0,
        TapToRespond = 1 << 0,
        FixationMonitoring = 1 << 1,
        AdaptiveStaircase = 1 << 2,
        CatchTrials = 1 << 3
    }

    /// <summary>
    /// Serializable descriptor for inspector/debug; runtime modules expose the same via <see cref="IVisualTestModule.Descriptor"/>.
    /// </summary>
    [Serializable]
    public class VisualTestDescriptor
    {
        [Tooltip("Stable id, e.g. PERIMETRY_24_2 — must match ITestModule.TestId.")]
        public string testId;

        public string displayName;
        public VisualTestCategory category = VisualTestCategory.Unknown;
        public VisualTestCapabilities capabilities = VisualTestCapabilities.None;

        [Tooltip("Semantic version of the test implementation (for ML / audit trails).")]
        public string moduleVersion = "0.0.0";

        [Tooltip("Optional schema id for extraJson / fullResultJson contracts.")]
        public string payloadSchemaId;
    }
}
