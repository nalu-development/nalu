using Nalu.Maui.UITests.Infrastructure;
using Xunit;

[assembly: AssemblyFixture(typeof(NaluApp))]

// All test classes drive the SAME running app instance (global UI state), so NOTHING here may
// ever run concurrently: two tests tapping the same app interleave taps, and the loser fails in
// its teardown reset on a page it never opened.
// Three locks, because each covers a hole the others don't:
//   - CollectionPerAssembly: every class lands in ONE collection (no per-class collections).
//   - DisableTestParallelization: that single collection is also marked non-parallel, so a runner
//     that ignores the grouping still can't overlap its cases.
//   - MaxParallelThreads = 1: one worker, full stop.
// xunit.runner.json repeats the same three for runners (Rider/VS) that read the config file
// instead of the attributes.
[assembly: CollectionBehavior(CollectionBehavior.CollectionPerAssembly, DisableTestParallelization = true, MaxParallelThreads = 1)]

namespace Nalu.Maui.UITests.Infrastructure;

/// <summary>
/// Base class for DevFlow-driven UI tests.
/// The <see cref="NaluApp"/> assembly fixture connects once to the running TestApp
/// and is shared by every test class through constructor injection.
/// </summary>
public abstract class BaseUiTest(NaluApp app)
{
    protected NaluApp App { get; } = app;
}
