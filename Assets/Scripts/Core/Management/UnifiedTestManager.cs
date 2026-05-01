// UnifiedTestManager.cs
// Single entry point: known test IDs, full vs single-test sessions, optional module activation.

using System.Collections.Generic;
using UnityEngine;

namespace OphthalSuite.Core
{
    public class UnifiedTestManager : MonoBehaviour
    {
        public static class TestIds
        {
            public const string Perimetry        = "PERIMETRY_24_2";
            public const string Csv1000          = "CSV_1000";
            public const string PelliRobson      = "PELLI_ROBSON";
            public const string Sparcs           = "SPARCS";
            public const string MotionDetection  = "MOTION_DETECTION";
            public const string EdgeDetection    = "EDGE_DETECTION";
            public const string PatternDetection = "PATTERN_DETECTION";
        }

        [Header("Required")]
        [SerializeField] private AppOrchestrator orchestrator;
        [SerializeField] private VisualTestManager visualTestManager;

        private void Awake()
        {
            if (orchestrator == null)
                orchestrator = FindFirstObjectByType<AppOrchestrator>();
            if (visualTestManager == null)
                visualTestManager = FindFirstObjectByType<VisualTestManager>();
        }

        /// <summary>Default order for a full battery (must match modules registered on <see cref="VisualTestManager"/>).</summary>
        public static IReadOnlyList<string> DefaultFullBatteryOrder => new[]
        {
            TestIds.Perimetry,
            TestIds.Csv1000,
            TestIds.PelliRobson,
            TestIds.Sparcs,
            TestIds.MotionDetection,
            TestIds.EdgeDetection,
            TestIds.PatternDetection
        };

        public AppOrchestrator Orchestrator => orchestrator;
        public VisualTestManager TestManager => visualTestManager;

        /// <summary>Run every registered test in VisualTestManager list order (or filtered subset if <paramref name="orderedSubset"/> set).</summary>
        public void StartFullSession(string patientId, string eye, int age, IList<string> orderedSubset = null)
        {
            if (orchestrator == null) return;
            if (orderedSubset == null || orderedSubset.Count == 0)
                orchestrator.StartSession(patientId, eye, age, null);
            else
                orchestrator.StartSession(patientId, eye, age, orderedSubset);
        }

        /// <summary>Run one test by id (session queue contains only that module).</summary>
        public void StartSingleTestSession(string patientId, string eye, int age, string testId)
        {
            if (orchestrator == null || string.IsNullOrEmpty(testId)) return;
            orchestrator.StartSession(patientId, eye, age, new[] { testId });
        }

        /// <summary>Activate one test root without starting a session (e.g. editor layout / manual).</summary>
        public bool SwitchActiveModule(string testId)
        {
            return visualTestManager != null && visualTestManager.LoadTest(testId);
        }

        public void UnloadActiveModule()
        {
            visualTestManager?.UnloadCurrent();
        }

        public List<string> GetRegisteredTestIds()
        {
            var list = new List<string>();
            if (visualTestManager == null) return list;
            foreach (var m in visualTestManager.GetModulesInOrder())
                list.Add(m.TestId);
            return list;
        }
    }
}
