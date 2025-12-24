using Xunit;
using ITestContext = VisualTestUtils.ITestContext;

namespace Nalu.Maui.UITests;

public class VisualTestUtilsTestContext : ITestContext
{
    public static VisualTestUtilsTestContext Current { get; } = new();

    public void AddTestAttachment(string filePath, string? description = null) => TestContext.Current.AddAttachment(filePath, description ?? string.Empty);
}
