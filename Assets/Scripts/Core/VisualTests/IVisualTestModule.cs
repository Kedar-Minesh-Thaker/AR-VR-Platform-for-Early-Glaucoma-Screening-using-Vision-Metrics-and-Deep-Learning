// IVisualTestModule.cs
// Contract for all patient-screen visual tests. Extends ITestModule with
// descriptor metadata and optional stimulus timing for doctor mirror / ML alignment.

using System;

namespace OphthalSuite.Core
{
    /// <summary>
    /// All visual tests implement this interface. Non-visual tests may implement only <see cref="ITestModule"/>.
    /// </summary>
    public interface IVisualTestModule : ITestModule
    {
        /// <summary>Static metadata (category, capabilities, versioning).</summary>
        VisualTestDescriptor Descriptor { get; }

        /// <summary>
        /// Fired when a stimulus is committed for presentation (onset), before the patient responds.
        /// Optional: modules may omit raises until stimulus logic is wired (Part 2+).
        /// </summary>
        event Action<StimulusPresentationMeta> OnStimulusPresentation;
    }
}
