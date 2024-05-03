namespace Nalu.Maui.Test.Layouts;

using Cassowary;

public class ViewConstraintTests
{
    [Theory(DisplayName = "ViewConstraint, should apply constraints")]
    [InlineData("1M", "1M", 20, 40, "t:t:parent,l:l:parent", 0, 20, 0, 40)]
    [InlineData("1M", "1M", 20, 40, "r:r:parent,l:l:parent,t:t:parent", 40, 60, 0, 40)]
    [InlineData("1M", "1M", 20, 40, "r:r:parent,l:l:parent,t:t:parent", 0, 20, 0, 40, "0,0")]
    [InlineData("1M", "1M", 20, 40, "r:r:parent,l:l:parent,t:t:parent", 80, 100, 0, 40, "1,0")]
    [InlineData("1M", "1M", 20, 40, "r:r:parent|20,l:l:parent|20,t:t:parent", 20, 40, 0, 40, "0,0")]
    [InlineData("1M", "1M", 20, 40, "r:r:parent|20,l:l:parent|20,t:t:parent", 60, 80, 0, 40, "1,0")]
    [InlineData("1M", "1M", 20, 40, "r:r:parent|20,l:l:parent|20,t:t:parent|20", 40, 60, 20, 60)]
    [InlineData("0.5M", "0.5M", 20, 40, "t:t:parent,r:r:parent", 90, 100, 0, 20)]
    [InlineData("1M", "1M", 20, 40, "t:t:parent,l:l:parent,r:r:parent", 40, 60, 0, 40)]
    [InlineData("0.5*", "0.5*", 20, 40, "t:t:parent,l:l:parent,r:r:parent,b:b:parent", 25, 75, 50, 150)]
    [InlineData("1M", "3R", 20, 40, "t:t:parent,l:l:parent", 0, 20, 0, 60)]
    public void ViewConstraintShouldApplyConstraints(
        SizeDefinition width,
        SizeDefinition height,
        double measuredWidth,
        double measuredHeight,
        string anchors,
        double expectedLeft,
        double expectedRight,
        double expectedTop,
        double expectedBottom,
        string? biases = null)
    {
        // Arrange
        var sceneWidth = 100;
        var sceneHeight = 200;
        var solver = new Solver();
        var scene = solver.SetUpScene(sceneWidth, sceneHeight);
        var view = new ViewConstraint
        {
            Id = "target",
            Width = width,
            Height = height,
        };

        if (biases is not null)
        {
            var parsedBiases = biases.Split(',').Select(double.Parse).ToArray();
            view.HorizontalBias = parsedBiases[0];
            view.VerticalBias = parsedBiases[1];
        }

        var anchor = anchors.Split(',');
        foreach (var anchorDef in anchor)
        {
            var parts = anchorDef.Split(':');
            var edge = parts[0];
            var targetEdge = parts[1];
            var target = parts[2];
            ApplyAnchor(view, edge, targetEdge, target);
        }

        var targetView = Substitute.For<IView>();
        targetView
            .Measure(Arg.Any<double>(), Arg.Any<double>())
            .Returns(new Size(measuredWidth, measuredHeight));

        scene.GetView("target").Returns(targetView);

        // Act
        view.SetScene(scene);
        view.ApplyConstraints();

        // Assert
        solver.ShouldHaveVariables(
            (view.Left, expectedLeft),
            (view.Right, expectedRight),
            (view.Top, expectedTop),
            (view.Bottom, expectedBottom));
    }

    [Fact(DisplayName = "View constraint, can be positioned side by side to another view constraint")]
    public void ViewConstraintCanBePositionedSideBySideToAnotherViewConstraint()
    {
        // Arrange
        var sceneWidth = 100;
        var sceneHeight = 200;
        var solver = new Solver();
        var scene = solver.SetUpScene(sceneWidth, sceneHeight);
        var view1 = new ViewConstraint
        {
            Id = "view1",
            LeftToLeftOf = "parent|10",
            TopToTopOf = "parent|10",
        };
        var view2 = new ViewConstraint
        {
            Id = "view2",
            LeftToRightOf = "view1",
            BottomToBottomOf = "view1",
        };

        var targetView1 = Substitute.For<IView>();
        targetView1
            .Measure(Arg.Any<double>(), Arg.Any<double>())
            .Returns(new Size(20, 40));

        var targetView2 = Substitute.For<IView>();
        targetView2
            .Measure(Arg.Any<double>(), Arg.Any<double>())
            .Returns(new Size(40, 20));

        scene.GetView("view1").Returns(targetView1);
        scene.GetElement("view1").Returns(view1);
        scene.GetView("view2").Returns(targetView2);
        scene.GetElement("view2").Returns(view2);

        // Act
        view1.SetScene(scene);
        view2.SetScene(scene);
        view1.ApplyConstraints();
        view2.ApplyConstraints();

        // Assert
        solver.ShouldHaveVariables(
            (view1.Left, 10),
            (view1.Right, 30),
            (view1.Top, 10),
            (view1.Bottom, 50),
            (view2.Left, 30),
            (view2.Right, 70),
            (view2.Top, 30),
            (view2.Bottom, 50));
    }

    [Fact(DisplayName = "View constraints, can generate chain")]
    public void ViewConstraintsCanGenerateChain()
    {
        // Arrange
        var sceneWidth = 100;
        var sceneHeight = 200;
        var solver = new Solver();
        var scene = solver.SetUpScene(sceneWidth, sceneHeight);
        var view1 = new ViewConstraint
        {
            Id = "view1",
            LeftToLeftOf = "parent",
            RightToLeftOf = "view2",
        };
        var view2 = new ViewConstraint
        {
            Id = "view2",
            LeftToRightOf = "view1",
            RightToLeftOf = "view3",
        };
        var view3 = new ViewConstraint
        {
            Id = "view3",
            LeftToRightOf = "view2",
            RightToRightOf = "parent",
        };

        var targetView1 = Substitute.For<IView>();
        targetView1
            .Measure(Arg.Any<double>(), Arg.Any<double>())
            .Returns(new Size(20, 40));

        var targetView2 = Substitute.For<IView>();
        targetView2
            .Measure(Arg.Any<double>(), Arg.Any<double>())
            .Returns(new Size(20, 40));

        var targetView3 = Substitute.For<IView>();
        targetView3
            .Measure(Arg.Any<double>(), Arg.Any<double>())
            .Returns(new Size(20, 40));

        scene.GetView("view1").Returns(targetView1);
        scene.GetElement("view1").Returns(view1);
        scene.GetView("view2").Returns(targetView2);
        scene.GetElement("view2").Returns(view2);
        scene.GetView("view3").Returns(targetView3);
        scene.GetElement("view3").Returns(view3);

        // Act
        view1.SetScene(scene);
        view2.SetScene(scene);
        view3.SetScene(scene);
        view1.ApplyConstraints();
        view2.ApplyConstraints();
        view3.ApplyConstraints();

        // Assert
        solver.ShouldHaveVariables(
            (view1.Left, 10),
            (view1.Right, 30),
            (view2.Left, 40),
            (view2.Right, 60),
            (view3.Left, 70),
            (view3.Right, 90));
    }

    [Fact(DisplayName = "View constraints, can generate weighted chain")]
    public void ViewConstraintsCanGenerateWeightedChain()
    {
        // Arrange
        var sceneWidth = 100;
        var sceneHeight = 200;
        var solver = new Solver();
        var scene = solver.SetUpScene(sceneWidth, sceneHeight);
        var view1 = new ViewConstraint
        {
            Id = "view1",
            LeftToLeftOf = "parent",
            RightToLeftOf = "view2",
            Width = "3*",
        };
        var view2 = new ViewConstraint
        {
            Id = "view2",
            LeftToRightOf = "view1",
            RightToLeftOf = "view3",
            Width = "m",
        };
        var view3 = new ViewConstraint
        {
            Id = "view3",
            LeftToRightOf = "view2",
            RightToRightOf = "parent",
            Width = "*",
        };

        var targetView1 = Substitute.For<IView>();
        targetView1
            .Measure(Arg.Any<double>(), Arg.Any<double>())
            .Returns(new Size(20, 40));

        var targetView2 = Substitute.For<IView>();
        targetView2
            .Measure(Arg.Any<double>(), Arg.Any<double>())
            .Returns(new Size(20, 40));

        var targetView3 = Substitute.For<IView>();
        targetView3
            .Measure(Arg.Any<double>(), Arg.Any<double>())
            .Returns(new Size(20, 40));

        scene.GetView("view1").Returns(targetView1);
        scene.GetElement("view1").Returns(view1);
        scene.GetView("view2").Returns(targetView2);
        scene.GetElement("view2").Returns(view2);
        scene.GetView("view3").Returns(targetView3);
        scene.GetElement("view3").Returns(view3);

        // Act
        view1.SetScene(scene);
        view2.SetScene(scene);
        view3.SetScene(scene);
        view1.ApplyConstraints();
        view2.ApplyConstraints();
        view3.ApplyConstraints();

        // Assert
        solver.ShouldHaveVariables(
            (view1.Left, 0),
            (view1.Right, 60),
            (view2.Left, 60),
            (view2.Right, 80),
            (view3.Left, 80),
            (view3.Right, 100));
    }

    [Fact(DisplayName = "View constraints, can generate chain spread inside")]
    public void ViewConstraintsCanGenerateChainSpreadInside()
    {
        // Arrange
        var sceneWidth = 100;
        var sceneHeight = 200;
        var solver = new Solver();
        var scene = solver.SetUpScene(sceneWidth, sceneHeight);
        var view1 = new ViewConstraint
        {
            Id = "view1",
            LeftToLeftOf = "parent!",
            RightToLeftOf = "view2",
        };
        var view2 = new ViewConstraint
        {
            Id = "view2",
            LeftToRightOf = "view1",
            RightToLeftOf = "view3",
        };
        var view3 = new ViewConstraint
        {
            Id = "view3",
            LeftToRightOf = "view2",
            RightToRightOf = "parent!",
        };

        var targetView1 = Substitute.For<IView>();
        targetView1
            .Measure(Arg.Any<double>(), Arg.Any<double>())
            .Returns(new Size(20, 40));

        var targetView2 = Substitute.For<IView>();
        targetView2
            .Measure(Arg.Any<double>(), Arg.Any<double>())
            .Returns(new Size(20, 40));

        var targetView3 = Substitute.For<IView>();
        targetView3
            .Measure(Arg.Any<double>(), Arg.Any<double>())
            .Returns(new Size(20, 40));

        scene.GetView("view1").Returns(targetView1);
        scene.GetElement("view1").Returns(view1);
        scene.GetView("view2").Returns(targetView2);
        scene.GetElement("view2").Returns(view2);
        scene.GetView("view3").Returns(targetView3);
        scene.GetElement("view3").Returns(view3);

        // Act
        view1.SetScene(scene);
        view2.SetScene(scene);
        view3.SetScene(scene);
        view1.ApplyConstraints();
        view2.ApplyConstraints();
        view3.ApplyConstraints();

        // Assert
        solver.ShouldHaveVariables(
            (view1.Left, 0),
            (view1.Right, 20),
            (view2.Left, 40),
            (view2.Right, 60),
            (view3.Left, 80),
            (view3.Right, 100));
    }

    [Fact(DisplayName = "View constraints, can generate chain pack")]
    public void ViewConstraintsCanGenerateChainPack()
    {
        // Arrange
        var sceneWidth = 100;
        var sceneHeight = 200;
        var solver = new Solver();
        var scene = solver.SetUpScene(sceneWidth, sceneHeight);
        var view1 = new ViewConstraint
        {
            Id = "view1",
            LeftToLeftOf = "parent",
            RightToLeftOf = "view2!",
        };
        var view2 = new ViewConstraint
        {
            Id = "view2",
            LeftToRightOf = "view1!",
            RightToLeftOf = "view3!",
        };
        var view3 = new ViewConstraint
        {
            Id = "view3",
            LeftToRightOf = "view2!",
            RightToRightOf = "parent",
        };

        var targetView1 = Substitute.For<IView>();
        targetView1
            .Measure(Arg.Any<double>(), Arg.Any<double>())
            .Returns(new Size(20, 40));

        var targetView2 = Substitute.For<IView>();
        targetView2
            .Measure(Arg.Any<double>(), Arg.Any<double>())
            .Returns(new Size(20, 40));

        var targetView3 = Substitute.For<IView>();
        targetView3
            .Measure(Arg.Any<double>(), Arg.Any<double>())
            .Returns(new Size(20, 40));

        scene.GetView("view1").Returns(targetView1);
        scene.GetElement("view1").Returns(view1);
        scene.GetView("view2").Returns(targetView2);
        scene.GetElement("view2").Returns(view2);
        scene.GetView("view3").Returns(targetView3);
        scene.GetElement("view3").Returns(view3);

        // Act
        view1.SetScene(scene);
        view2.SetScene(scene);
        view3.SetScene(scene);
        view1.ApplyConstraints();
        view2.ApplyConstraints();
        view3.ApplyConstraints();

        // Special case: bias on pack chain
        // var rightAnchor = scene.Right;
        // var leftAnchor = scene.Left;
        // var biasWidth = rightAnchor - leftAnchor - view3.Right + view1.Left;
        // solver.AddConstraint(view1.Left | WeightedRelation.Eq(Strength.Strong) | (leftAnchor + (biasWidth * 0.5)));

        // Assert
        solver.ShouldHaveVariables(
            (view1.Left, 20),
            (view1.Right, 40),
            (view2.Left, 40),
            (view2.Right, 60),
            (view3.Left, 60),
            (view3.Right, 80));
    }

    private static void ApplyAnchor(ViewConstraint view, string edge, string targetEdge, string target)
    {
        edge = ParseEdge(edge);
        targetEdge = ParseEdge(targetEdge);
        view.GetType().GetProperty($"{edge}To{targetEdge}Of")!.SetValue(view, (Anchor)target);
    }

    private static string ParseEdge(string edge) =>
        edge switch
        {
            "t" => "Top",
            "b" => "Bottom",
            "l" => "Left",
            "r" => "Right",
            _ => throw new ArgumentException($"Invalid edge: {edge}"),
        };
}
