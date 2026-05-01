// ITestModule.cs
// Every ophthalmic test (Perimetry, Contrast Sensitivity, Colour Vision, etc.)
// MUST implement this interface.  The AppOrchestrator only talks to tests
// through ITestModule — it never references concrete test classes.

using System;

namespace OphthalSuite.Core
{
    public interface ITestModule
    {
        /// <summary>Unique machine-readable identifier, e.g. "PERIMETRY_24_2".</summary>
        string TestId { get; }

        /// <summary>Human-readable name shown in UI and doctor dashboard.</summary>
        string DisplayName { get; }

        /// <summary>
        /// Called by AppOrchestrator to begin the test.
        /// patientId, eye ("OD"/"OS"), and age come from the SessionContext.
        /// </summary>
        void StartTest(SessionContext ctx);

        /// <summary>Called by AppOrchestrator to abort early.</summary>
        void StopTest();

        /// <summary>True while test is running.</summary>
        bool IsRunning { get; }

        /// <summary>Fired when each trial ends. Payload is a TestTrialEvent.</summary>
        event Action<TestTrialEvent> OnTrialEnd;

        /// <summary>Fired once when the test finishes. Payload is a TestResult.</summary>
        event Action<TestResult> OnTestComplete;
    }
}
