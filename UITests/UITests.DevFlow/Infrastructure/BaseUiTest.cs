using Nalu.Maui.UITests.Infrastructure;
using Xunit;

[assembly: AssemblyFixture(typeof(NaluApp))]

// All test classes drive the SAME running app instance (global UI state):
// force a single collection so classes never run in parallel.
[assembly: CollectionBehavior(CollectionBehavior.CollectionPerAssembly)]

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
