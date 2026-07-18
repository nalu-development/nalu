using Nalu.Maui.UITests.Infrastructure;
using Xunit;

[assembly: AssemblyFixture(typeof(NaluApp))]

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
