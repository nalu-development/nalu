namespace Nalu.Maui.Test.Layouts;

using Cassowary;
using FluentAssertions.Collections;

#pragma warning disable FAA0001

public static class SceneTestingExtensions
{
    public static void ShouldHaveVariables(
        this Solver solver,
        params (Variable Variable, double Value)[] expected)
    {
        solver.FetchChanges();
        foreach (var (variable, value) in expected)
        {
            var actual = variable.Value;
            Math.Abs(actual - value).Should().BeLessOrEqualTo(0.1, $"Variable {variable.Name} should be {value} but was {actual}");
        }
    }

    public static IConstraintLayoutScene SetUpScene(this Solver solver, int sceneWidth, int sceneHeight)
    {
        var scene = Substitute.For<IConstraintLayoutScene>();
        scene.Solver.Returns(solver);

        var sceneLeft = new Variable();
        sceneLeft.SetName("scene.Left");
        solver.AddConstraint(sceneLeft | WeightedRelation.Eq(Strength.Required) | 0);
        scene.Left.Returns(sceneLeft);
        scene.Left.Value = 0;

        var sceneRight = new Variable();
        sceneRight.SetName("scene.Right");
        solver.AddConstraint(sceneRight | WeightedRelation.Eq(Strength.Required) | sceneWidth);
        scene.Right.Returns(sceneRight);
        scene.Right.Value = sceneWidth;

        var sceneTop = new Variable();
        sceneTop.SetName("scene.Top");
        solver.AddConstraint(sceneTop | WeightedRelation.Eq(Strength.Required) | 0);
        scene.Top.Returns(sceneTop);
        scene.Top.Value = 0;

        var sceneBottom = new Variable();
        sceneBottom.SetName("scene.Bottom");
        solver.AddConstraint(sceneBottom | WeightedRelation.Eq(Strength.Required) | sceneHeight);
        scene.Bottom.Returns(sceneBottom);
        scene.Bottom.Value = sceneHeight;

        scene.GetElement("parent").Returns(scene);

        return scene;
    }
}
