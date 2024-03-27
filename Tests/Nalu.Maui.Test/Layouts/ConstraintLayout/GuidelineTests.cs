namespace Nalu.Maui.Test.Layouts;

using Cassowary;

#pragma warning disable FAA0001

public class GuidelineTests
{
    [Theory(DisplayName = "Guideline, should apply constraints")]
    [InlineData(GuidelineOrientation.Vertical, 0.5, 5, 55, 55, 0, 200)]
    [InlineData(GuidelineOrientation.Vertical, 0.2, -5, 15, 15, 0, 200)]
    [InlineData(GuidelineOrientation.Horizontal, 0.5, 5, 0, 100, 105, 105)]
    [InlineData(GuidelineOrientation.Horizontal, 0.2, -5, 0, 100, 35, 35)]
    [InlineData(GuidelineOrientation.Horizontal, 0, 0, 0, 100, 0, 0)]
    [InlineData(GuidelineOrientation.Horizontal, 1, 0, 0, 100, 200, 200)]
    public void GuidelineShouldApplyConstraints(
        GuidelineOrientation orientation,
        double percentage,
        double delta,
        double expectedLeft,
        double expectedRight,
        double expectedTop,
        double expectedBottom)
    {
        // Arrange
        var sceneWidth = 100;
        var sceneHeight = 200;
        var solver = new Solver();
        var scene = solver.SetUpScene(sceneWidth, sceneHeight);
        var guideline = new Guideline
        {
            Id = "guideline",
            Delta = delta,
            Percentage = percentage,
            Orientation = orientation,
        };

        // Act
        guideline.SetScene(scene);
        guideline.ApplyConstraints();

        // Assert
        solver.ShouldHaveVariables(
            (guideline.Left, expectedLeft),
            (guideline.Right, expectedRight),
            (guideline.Top, expectedTop),
            (guideline.Bottom, expectedBottom));
    }

    [Fact(DisplayName = "Guideline, reapply constraints, should not cause changes")]
    public void GuidelineReapplyConstraintsShouldNotCauseChanges()
    {
        // Arrange
        var sceneWidth = 100;
        var sceneHeight = 200;
        var solver = new Solver();
        var scene = solver.SetUpScene(sceneWidth, sceneHeight);
        var guideline = new Guideline
        {
            Id = "guideline",
            Delta = 5,
            Percentage = 0.1,
            Orientation = GuidelineOrientation.Vertical,
        };

        // Act
        guideline.SetScene(scene);
        guideline.ApplyConstraints();

        // Assert
        solver.ShouldHaveVariables(
            (guideline.Left, 15),
            (guideline.Right, 15),
            (guideline.Top, 0),
            (guideline.Bottom, 200));

        // Act again
        guideline.ApplyConstraints();

        // Assert again
        solver.FetchChanges().Should().BeEmpty();
    }
}
