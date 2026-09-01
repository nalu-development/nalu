using Nalu.MagnetLayout.Engine;

namespace Nalu.Maui.Test.Layouts.MagnetLayout;

public class MagnetEngineTests
{
    private const string P = MagnetAnchor.Parent;

    [Fact]
    public void LeftTopAnchorsPositionTheView()
    {
        var h = new EngineHarness();
        h.View("a", 40, 20).Left(P, margin: 10).Top(P, margin: 10);

        var measured = h.Layout(100, 100);

        measured.Should().Be(new Size(50, 30));
        h.Frame("a").ShouldBe(10, 10, 40, 20);
    }

    [Fact]
    public void RightBottomAnchorsPositionTheView()
    {
        var h = new EngineHarness();
        h.View("a", 40, 20).Right(P, margin: 10).Bottom(P, margin: 10);

        var measured = h.Layout(100, 100);

        // Hug: size + margin
        measured.Should().Be(new Size(50, 30));
        h.Frame("a").ShouldBe(0, 0, 40, 20);

        h.Layout(100, 100, 100, 100);
        h.Frame("a").ShouldBe(50, 70, 40, 20);
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(0.5, 40)]
    [InlineData(0.25, 25)]
    [InlineData(1, 70)]
    public void BiasPositionsTheViewBetweenAnchors(double bias, double expected)
    {
        var h = new EngineHarness();
        h.View("a", 20, 20).Within(P, 10).Bias(bias, bias);

        var measured = h.Layout(100, 100, 100, 100);

        measured.Should().Be(new Size(40, 40));
        h.Frame("a").ShouldBe(expected, expected, 20, 20);
    }

    [Fact]
    public void UnanchoredViewSitsAtOrigin()
    {
        var h = new EngineHarness();
        h.View("a", 40, 20);

        var measured = h.Layout(100, 100);

        measured.Should().Be(new Size(40, 20));
        h.Frame("a").ShouldBe(0, 0, 40, 20);
    }

    [Fact]
    public void FixedSize()
    {
        var h = new EngineHarness();
        h.View("a", 40, 20).Left(P).Top(P).Size(30, 15);

        h.Layout(100, 100);

        h.Frame("a").ShouldBe(0, 0, 30, 15);

        // MAUI contract: even a fully-determined child is measured (exactly once, with its resolved size),
        // so platform containers can lay out their own content.
        h.Fake("a").MeasureCount.Should().Be(1);
        h.Fake("a").Constraints[^1].Should().Be((30d, 15d));
    }

    [Fact]
    public void ConstraintSizeFillsTheSpan()
    {
        var h = new EngineHarness();
        h.View("a", 40, 20).Fill(P, new Thickness(5, 6, 7, 8));

        var measured = h.Layout(100, 100, 100, 100);

        // Hug of a fill-sized child = margins only.
        measured.Should().Be(new Size(12, 14));
        h.Frame("a").ShouldBe(5, 6, 88, 86);
    }

    [Fact]
    public void ConstraintPercentSize()
    {
        var h = new EngineHarness();
        h.View("a", 40, 20).Within(P).Size(MagnetSizing.Percent(0.5), MagnetSizing.Percent(0.25)).Bias(0, 1);

        h.Layout(100, 200, 100, 200);

        h.Frame("a").ShouldBe(0, 150, 50, 50);
    }

    [Fact]
    public void MinMaxBoundsApply()
    {
        var h = new EngineHarness();
        h.View("a", 40, 20).Within(P).Size(MagnetSizing.Constraint.WithBounds(max: 60), MagnetSizing.Constraint.WithBounds(min: 150));

        var measured = h.Layout(100, 100, 100, 100);

        h.Frame("a").ShouldBe(20, -25, 60, 150);

        // Min height contributes to the hug.
        measured.Height.Should().Be(100); // clamped to constraint
        h.Measure(100, double.PositiveInfinity).Height.Should().Be(150);
    }

    [Fact]
    public void RatioWidthFromFixedHeight()
    {
        var h = new EngineHarness();
        h.View("a", 40, 20).Left(P).Top(P).Size(MagnetSizing.Ratio(2), 30);

        h.Layout(100, 100);

        h.Frame("a").ShouldBe(0, 0, 60, 30);
    }

    [Fact]
    public void RatioHeightFromConstraintWidth()
    {
        var h = new EngineHarness();
        h.View("a", 40, 20).HorizontallyWithin(P).Top(P).Size(MagnetSizing.Constraint, MagnetSizing.Ratio(0.5));

        h.Layout(200, 500, 200, 500);

        h.Frame("a").ShouldBe(0, 0, 200, 100);
    }

    [Fact]
    public void RatioWidthFromMeasuredHeight()
    {
        var h = new EngineHarness();
        h.View("a", 40, 20).Left(P).Top(P).Size(MagnetSizing.Ratio(3), MagnetSizing.Measured);

        h.Layout(200, 500);

        h.Frame("a").ShouldBe(0, 0, 60, 20);
    }

    [Fact]
    public void RatioWidthFromRowHeightUsesCrossAxisFeedback()
    {
        // Thumbnail as tall as the row (whose height comes from the label), width = 1 × height.
        var h = new EngineHarness();
        h.View("thumb", 10, 10).Left(P).VerticallyWithin(P).Size(MagnetSizing.Ratio(1), MagnetSizing.Constraint);
        h.View("label", 100, 40).Left("thumb", MagnetPole.Right, 12).Top(P).Size(100, 40);

        var measured = h.Layout(400, double.PositiveInfinity, 400);

        measured.Height.Should().Be(40);
        h.Frame("thumb").ShouldBe(0, 0, 40, 40);
        h.Frame("label").ShouldBe(52, 0, 100, 40);
        h.Engine.Tape!.HasFeedback.Should().BeTrue();

        // Warm start: the second measure needs no feedback pass but yields the same frames.
        h.Layout(400, double.PositiveInfinity, 400);
        h.Frame("thumb").ShouldBe(0, 0, 40, 40);
        h.Frame("label").ShouldBe(52, 0, 100, 40);
    }

    [Fact]
    public void LayoutsWithoutRatioFeedbackCarryNoFeedbackSlots()
    {
        var h = new EngineHarness();
        h.View("a", 40, 20).Left(P).Top(P).Size(MagnetSizing.Ratio(2), 30);
        h.Compile();

        h.Engine.Tape!.HasFeedback.Should().BeFalse();
    }

    [Fact]
    public void ChainedAnchorsResolveTransitively()
    {
        var h = new EngineHarness();
        h.View("avatar", 48, 48).Left(P, margin: 16).Top(P, margin: 16).Size(48, 48);
        h.View("title", 100, 20).Left("avatar", MagnetPole.Right, 12).Top("avatar", MagnetPole.Top);
        h.View("subtitle", 80, 16).Left("title", MagnetPole.Left).Top("title", MagnetPole.Bottom, 2);

        var measured = h.Layout(400, 400);

        measured.Should().Be(new Size(176, 64));
        h.Frame("avatar").ShouldBe(16, 16, 48, 48);
        h.Frame("title").ShouldBe(76, 16, 100, 20);
        h.Frame("subtitle").ShouldBe(76, 38, 80, 16);
    }

    [Fact]
    public void WrappingLabelBetweenSiblingAndParentIsMeasuredWithItsSpan()
    {
        var h = new EngineHarness();
        h.View("avatar", 48, 48).Left(P).Top(P).Size(48, 48);
        h.View("title", 300, 20, wraps: true).Left("avatar", MagnetPole.Right, 12).Right(P, margin: 10).Top(P);

        var measured = h.Layout(200, double.PositiveInfinity, 200);

        var title = h.Fake("title");
        title.Constraints[0].W.Should().Be(130);
        title.DesiredSize.Should().Be(new Size(130, 60)); // 3 lines
        measured.Should().Be(new Size(200, 60));
        h.Frame("title").ShouldBe(60, 0, 130, 60);
        title.MeasureCount.Should().Be(1, "measure and arrange with equal constraints share one measure");
    }

    [Fact]
    public void UnboundedMeasureDoesNotLeakInfinityIntoFrames()
    {
        var h = new EngineHarness();
        h.View("a", 40, 20).Within(P, 10);
        h.View("b", 30, 30).Right(P, margin: 5).Bottom("a", MagnetPole.Top);

        var measured = h.Layout(double.PositiveInfinity, double.PositiveInfinity);

        // Hug contains every view: b hangs above a, so the stage grows to 80 and a is centered in it.
        measured.Should().Be(new Size(60, 80));
        h.Frame("a").ShouldBe(10, 30, 40, 20);
        h.Frame("b").ShouldBe(25, 0, 30, 30);
        h.Fake("a").Constraints[0].Should().Be((double.PositiveInfinity, double.PositiveInfinity));
    }

    [Fact]
    public void GoneMarginsApplyWhenTargetIsCollapsed()
    {
        var h = new EngineHarness();
        h.View("a", 40, 20).Left(P).Top(P);
        h.View("b", 30, 30).Left("a", MagnetPole.Right, 8, goneMargin: 2).Top(P);

        h.Layout(200, 200);
        h.Frame("b").ShouldBe(48, 0, 30, 30);

        h.Fake("a").Visibility = Visibility.Collapsed;
        h.Layout(200, 200);
        h.Frame("a").ShouldBe(0, 0, 0, 0);
        h.Frame("b").ShouldBe(2, 0, 30, 30);
        h.Fake("a").MeasureCount.Should().Be(1, "collapsed views are not measured");
    }

    [Fact]
    public void CollapsedViewDropsItsOwnMargins()
    {
        var h = new EngineHarness();
        h.View("a", 40, 20).Left(P, margin: 10).Top(P);
        h.View("b", 30, 30).Left("a", MagnetPole.Right, 8).Top(P);

        h.Fake("a").Visibility = Visibility.Collapsed;
        h.Layout(200, 200);

        h.Frame("a").ShouldBe(0, 0, 0, 0);
        h.Frame("b").ShouldBe(8, 0, 30, 30);
    }

    [Fact]
    public void BarrierFollowsTheOutermostMember()
    {
        var h = new EngineHarness();
        h.View("a", 40, 20).Left(P).Top(P);
        h.View("b", 70, 20).Left(P).Top("a", MagnetPole.Bottom);
        h.Add(new MagnetBarrier { MagnetId = "end", Direction = MagnetPole.Right, Margin = 8 }.With("a", "b"));
        h.View("c", 20, 20).Left("end", MagnetPole.Right).Top(P);

        var measured = h.Layout(200, 200);

        h.Frame("c").ShouldBe(78, 0, 20, 20);
        measured.Width.Should().Be(98);

        h.Fake("b").Visibility = Visibility.Collapsed;
        h.Layout(200, 200);
        h.Frame("c").ShouldBe(48, 0, 20, 20);
    }

    [Fact]
    public void GuidelinePercentAndPosition()
    {
        var h = new EngineHarness();
        h.Add(new MagnetGuideline { MagnetId = "g", Orientation = MagnetOrientation.Vertical, Percent = 0.5, Position = 10 });
        h.Add(new MagnetGuideline { MagnetId = "hg", Orientation = MagnetOrientation.Horizontal, Position = 30 });
        h.View("a", 40, 20).Left("g", MagnetPole.Right).Top("hg", MagnetPole.Bottom);

        h.Layout(200, 200, 200, 200);

        h.Frame("a").ShouldBe(110, 30, 40, 20);
    }

    [Fact]
    public void GuidelineHugIsExact()
    {
        var h = new EngineHarness();
        h.Add(new MagnetGuideline { MagnetId = "g", Percent = 0.5 });
        h.View("a", 40, 20).Left("g", MagnetPole.Left, 10).Top(P);

        var measured = h.Layout(double.PositiveInfinity, 100);

        // 0.5W + 10 + 40 ≤ W → W ≥ 100
        measured.Width.Should().Be(100);
        h.Frame("a").ShouldBe(60, 0, 40, 20);
    }

    [Theory]
    [InlineData(MagnetChainStyle.Spread, 15, 60, 105)]
    [InlineData(MagnetChainStyle.SpreadInside, 0, 60, 120)]
    [InlineData(MagnetChainStyle.Packed, 30, 60, 90)]
    public void HorizontalChainStyles(MagnetChainStyle style, double x1, double x2, double x3)
    {
        var h = new EngineHarness();
        h.View("a", 30, 20).Left(P).Top(P);
        h.View("b", 30, 20).Top(P);
        h.View("c", 30, 20).Right(P).Top(P);
        h.Add(new MagnetChain { MagnetId = "row", Style = style }.With("a", "b", "c"));

        h.Layout(150, 100, 150, 100);

        h.Frame("a").ShouldBe(x1, 0, 30, 20);
        h.Frame("b").ShouldBe(x2, 0, 30, 20);
        h.Frame("c").ShouldBe(x3, 0, 30, 20);
    }

    [Fact]
    public void ChainHugIsSumOfSizesAndMargins()
    {
        var h = new EngineHarness();
        h.View("a", 30, 20).Left(P, margin: 5).Top(P);
        h.View("b", 30, 20).Left("a", MagnetPole.Right, 4).Top(P);
        h.View("c", 30, 20).Right(P, margin: 5).Top(P);
        h.Add(new MagnetChain { MagnetId = "row" }.With("a", "b", "c"));

        var measured = h.Layout(double.PositiveInfinity, 100);

        measured.Width.Should().Be(104);
        h.Frame("a").ShouldBe(5, 0, 30, 20);
        h.Frame("b").ShouldBe(39, 0, 30, 20);
        h.Frame("c").ShouldBe(69, 0, 30, 20);
    }

    [Fact]
    public void WeightedChainDistributesRemainingSpace()
    {
        var h = new EngineHarness();
        h.View("a", 30, 20).Left(P).Top(P).Size(MagnetSizing.Constraint, 20);
        h.View("b", 40, 20).Top(P).Size(40, 20);
        h.View("c", 30, 20).Right(P).Top(P).Size(MagnetSizing.Constraint, 20);
        var chain = h.Add(new MagnetChain { MagnetId = "row" }.With("a", "b", "c"));
        chain.Weights.Add(1);
        chain.Weights.Add(0);
        chain.Weights.Add(3);

        h.Layout(200, 100, 200, 100);

        h.Frame("a").ShouldBe(0, 0, 40, 20);
        h.Frame("b").ShouldBe(40, 0, 40, 20);
        h.Frame("c").ShouldBe(80, 0, 120, 20);
    }

    [Fact]
    public void ZeroChainWeightsIsAnError()
    {
        var h = new EngineHarness();
        h.View("a", 30, 20).Left(P).Top(P).Size(MagnetSizing.Constraint, 20);
        var chain = h.Add(new MagnetChain { MagnetId = "row" }.With("a"));
        chain.Weights.Add(0);

        var act = () => h.Compile();

        act.Should().Throw<InvalidOperationException>().WithMessage("*'row'*weight*");
    }

    [Fact]
    public void CollapsedChainMemberIsSkipped()
    {
        var h = new EngineHarness();
        h.View("a", 30, 20).Left(P).Top(P);
        h.View("b", 30, 20).Top(P);
        h.View("c", 30, 20).Right(P).Top(P);
        h.Add(new MagnetChain { MagnetId = "row", Style = MagnetChainStyle.Spread }.With("a", "b", "c"));
        h.Fake("b").Visibility = Visibility.Collapsed;

        h.Layout(150, 100, 150, 100);

        // Two visible members spread in 150: gap = 90/3 = 30
        h.Frame("a").ShouldBe(30, 0, 30, 20);
        h.Frame("c").ShouldBe(90, 0, 30, 20);
    }

    [Fact]
    public void VerticalChainSpreadInside()
    {
        var h = new EngineHarness();
        h.View("a", 20, 10).Top(P).Left(P);
        h.View("b", 20, 10).Bottom(P).Left(P);
        h.Add(new MagnetChain { MagnetId = "col", Orientation = MagnetOrientation.Vertical, Style = MagnetChainStyle.SpreadInside }.With("a", "b"));

        h.Layout(100, 100, 100, 100);

        h.Frame("a").ShouldBe(0, 0, 20, 10);
        h.Frame("b").ShouldBe(0, 90, 20, 10);
    }

    [Fact]
    public void ChainMemberWithForeignAnchorIsAnError()
    {
        var h = new EngineHarness();
        h.View("a", 30, 20).Left(P).Top(P);
        h.View("b", 30, 20).Left(P).Top(P);
        h.Add(new MagnetChain { MagnetId = "row" }.With("a", "b"));

        var act = () => h.Compile();

        act.Should().Throw<InvalidOperationException>().WithMessage("*'b'*'row'*LeftTo*");
    }

    [Fact]
    public void UnknownTargetIsAnError()
    {
        var h = new EngineHarness();
        h.View("cta", 30, 20).Left("textEnd", MagnetPole.Right);

        var act = () => h.Compile();

        act.Should().Throw<InvalidOperationException>().WithMessage("MagnetView 'cta': LeftTo targets unknown id 'textEnd'.");
    }

    [Fact]
    public void AxisMismatchIsAnError()
    {
        var h = new EngineHarness();
        h.View("header", 30, 20);
        h.View("cta", 30, 20).Left("header", MagnetPole.Top);

        var act = () => h.Compile();

        act.Should().Throw<InvalidOperationException>().WithMessage("MagnetView 'cta': LeftTo cannot reference pole 'Top' of 'header'.");
    }

    [Fact]
    public void CycleIsAnError()
    {
        var h = new EngineHarness();
        h.View("a", 30, 20).Left("b", MagnetPole.Right);
        h.View("b", 30, 20).Left("a", MagnetPole.Right);

        var act = () => h.Compile();

        act.Should().Throw<InvalidOperationException>().WithMessage("Constraint cycle on X axis: *'a'*'b'*MagnetChain*");
    }

    [Fact]
    public void ArrangeWithMeasuredSizeDoesNotRemeasureChildren()
    {
        var h = new EngineHarness();
        h.View("a", 40, 20).Left(P).Top(P);
        h.View("b", 30, 30).Left("a", MagnetPole.Right).VerticallyWithin(P);

        h.Layout(300, 300);
        h.Fake("a").MeasureCount.Should().Be(1);
        h.Fake("b").MeasureCount.Should().Be(1);

        // Fill arrange with the same constraints: still no re-measure.
        h.Engine.Arrange(300, 300, false);
        h.Fake("a").MeasureCount.Should().Be(1);
        h.Frame("b").ShouldBe(40, 135, 30, 30);
    }

    [Fact]
    public void ValuePatchDoesNotRecompileAndMovesViews()
    {
        var h = new EngineHarness();
        var a = h.View("a", 40, 20).Left(P, margin: 10).Top(P);
        h.Layout(300, 300);
        var tape = h.Engine.Tape;

        a.LeftTo = a.LeftTo!.Value with { Margin = 30 };
        h.Engine.PatchValues();
        h.Layout(300, 300);

        h.Engine.Tape.Should().BeSameAs(tape);
        h.Frame("a").ShouldBe(30, 0, 40, 20);
    }

    [Fact]
    public void ComplexCardLayout()
    {
        // The Magnet 1.x integration scenario, expressed with an explicit chain.
        var h = new EngineHarness();
        h.View("CardImage", 60, 48).Size(60, 48).Left(P, margin: 4).VerticallyWithin(P, 4);
        h.View("CardName", 221, 22, shrink: true).Left("CardImage", MagnetPole.Right, 8).Right("Starred", MagnetPole.Left).Top(P).Bias(0, 0.5);
        h.View("Starred", 16, 16).Size(16, 16).Right("Money", MagnetPole.Left, 8).VerticallyWithin("CardName");
        h.View("CardDetail", 105, 16).Left("CardName", MagnetPole.Left).Top("CardName", MagnetPole.Bottom);
        h.View("Money", 98, 41).Size(98, MagnetSizing.Constraint).Right(P).VerticallyWithin(P);

        var measured = h.Layout(708, 359, 708);

        h.Frame("CardImage").ShouldBe(4, 4, 60, 48);
        h.Frame("CardName").ShouldBe(72, 0, 221, 22);
        h.Frame("CardDetail").ShouldBe(72, 22, 105, 16);
        h.Frame("Money").ShouldBe(610, 0, 98, 56);
        h.Frame("Starred").ShouldBe(586, 3, 16, 16);
        measured.Height.Should().Be(56);
    }
}

public class MagnetEngineAllocationTests
{
    private const string P = MagnetAnchor.Parent;

    [Fact]
    public void MeasureAndArrangeDoNotAllocate()
    {
        var h = new EngineHarness();
        MagnetView Stub(string id, double w, double hh) => h.Add(new MagnetView { MagnetId = id, View = new StubView(w, hh) });
        Stub("a", 60, 48).Left(P, margin: 4).VerticallyWithin(P);
        Stub("b", 40, 20).Left("a", MagnetPole.Right, 8).Right("c", MagnetPole.Left).Top(P).Bias(0, 0.5);
        Stub("c", 98, 20).Right(P).VerticallyWithin(P).Size(MagnetSizing.Measured, MagnetSizing.Constraint);
        h.Add(new MagnetGuideline { MagnetId = "g", Percent = 0.5 });
        h.Add(new MagnetBarrier { MagnetId = "bar", Direction = MagnetPole.Bottom }.With("a", "b"));
        Stub("d", 10, 10).Left("g", MagnetPole.Left).Top("bar", MagnetPole.Bottom);
        Stub("e", 10, 10).Size(10, MagnetSizing.Measured);
        Stub("f", 10, 10).Size(MagnetSizing.Constraint.WithBounds(max: 40), 10);
        h.Add(new MagnetChain { MagnetId = "row", Style = MagnetChainStyle.Spread }.With("e", "f"));
        h.Compile();
        h.Layout(500, 500, 500, 500);

        var before = GC.GetAllocatedBytesForCurrentThread();

        for (var i = 0; i < 100; i++)
        {
            h.Engine.Measure(500, double.PositiveInfinity);
            h.Engine.Arrange(500, 500, true);
        }

        var after = GC.GetAllocatedBytesForCurrentThread();
        (after - before).Should().Be(0);
    }
}

public class MagnetTapeCacheTests
{
    private const string P = MagnetAnchor.Parent;

    [Fact]
    public void StructurallyIdenticalDefinitionsShareTheTapeButNotTheValues()
    {
        var a = new EngineHarness();
        a.View("x", 40, 20).Left(P, margin: 10).Top(P);
        a.View("y", 30, 30).Left("x", MagnetPole.Right, 5).VerticallyWithin(P);
        var b = new EngineHarness();
        b.View("x", 50, 25).Left(P, margin: 20).Top(P);
        b.View("y", 30, 30).Left("x", MagnetPole.Right, 7).VerticallyWithin(P);

        a.Layout(200, 200);
        b.Layout(200, 200);

        a.Engine.Tape.Should().BeSameAs(b.Engine.Tape);
        a.Frame("y").ShouldBe(55, 0, 30, 30);
        b.Frame("y").ShouldBe(77, 0, 30, 30);

        // A structural difference produces a different tape.
        var c = new EngineHarness();
        c.View("x", 40, 20).Left(P, margin: 10).Top(P);
        c.View("y", 30, 30).Right("x", MagnetPole.Left, 5).VerticallyWithin(P);
        c.Layout(200, 200);
        c.Engine.Tape.Should().NotBeSameAs(a.Engine.Tape);
    }

    [Fact]
    public void MinAndMaxBoundednessAreSeparateStructuralBits()
    {
        // The compiler emits the measure-constraint clamp only for a finite Max, so a min-only and a
        // max-only Measured sizing are structurally different tapes and must not share a cache entry
        // (regardless of which one compiles first).
        var minOnly = new EngineHarness();
        minOnly.View("x", 100, 20, shrink: true).Left(P).Right(P).Top(P).Size(MagnetSizing.Measured.WithBounds(min: 10), 20);
        var maxOnly = new EngineHarness();
        maxOnly.View("x", 100, 20, shrink: true).Left(P).Right(P).Top(P).Size(MagnetSizing.Measured.WithBounds(max: 40), 20);

        minOnly.Layout(200, 200, 200, 200);
        maxOnly.Layout(200, 200, 200, 200);

        minOnly.Engine.Tape.Should().NotBeSameAs(maxOnly.Engine.Tape);

        // The unbounded-max view measures against the full span, the bounded one against its Max.
        minOnly.Fake("x").Constraints[^1].W.Should().Be(200);
        maxOnly.Fake("x").Constraints[^1].W.Should().Be(40);
        maxOnly.Frame("x").Width.Should().Be(40);
    }

    [Fact]
    public void CompilationCacheCapacityIsConfigurableAtRuntime()
    {
        var initial = Magnet.CompilationCacheCapacity;
        initial.Should().Be(64);

        try
        {
            MagnetTapeCache.Clear();
            Magnet.CompilationCacheCapacity = 2;

            var a = new EngineHarness();
            a.View("cap", 10, 10).Left(P).Top(P);
            a.Layout(100, 100);
            var b = new EngineHarness();
            b.View("cap", 10, 10).Left(P).Top(P).Size(20, 20);
            b.Layout(100, 100);
            var c = new EngineHarness();
            c.View("cap", 10, 10).Left(P).Right(P).Top(P).Size(MagnetSizing.Constraint, 20);
            c.Layout(100, 100);

            // Other test classes may add entries concurrently, but the capacity invariant always holds.
            MagnetTapeCache.Count.Should().BeLessThanOrEqualTo(2);

            // "a" (least recently used) was evicted: an identical structure compiles a fresh tape.
            var a2 = new EngineHarness();
            a2.View("cap", 10, 10).Left(P).Top(P);
            a2.Layout(100, 100);
            a2.Engine.Tape.Should().NotBeSameAs(a.Engine.Tape);

            // Shrinking trims immediately.
            Magnet.CompilationCacheCapacity = 1;
            MagnetTapeCache.Count.Should().BeLessThanOrEqualTo(1);

            var act = () => Magnet.CompilationCacheCapacity = -1;
            act.Should().Throw<ArgumentOutOfRangeException>();
        }
        finally
        {
            Magnet.CompilationCacheCapacity = initial;
        }
    }
}

public class MagnetChainShrinkTests
{
    private const string P = MagnetAnchor.Parent;

    /// <summary>
    /// The credit-card scenario: the name label grows until it must ellipsize, the star is always glued to its right.
    /// </summary>
    [Theory]
    [InlineData(500, 221, 297)] // fits: star right after the name, packed to the left
    [InlineData(400, 202, 278)] // squeezed: name shrinks to leave room for star + margins, star still glued
    [InlineData(200, 2, 78)] // almost no room left for the name
    public void PackedChainMeasuredMemberLeavesRoomForSiblings(double width, double expectedNameWidth, double expectedStarX)
    {
        var h = new EngineHarness();
        h.View("CardImage", 60, 48).Size(60, 48).Left(P, margin: 4).VerticallyWithin(P, 4);
        h.View("CardName", 221, 22, shrink: true).Left("CardImage", MagnetPole.Right, 8).Top(P).Bias(0, 0.5);
        h.View("Starred", 16, 16).Size(16, 16).Left("CardName", MagnetPole.Right, 4).Right("Money", MagnetPole.Left, 8).VerticallyWithin("CardName");
        h.View("Money", 98, 41).Size(98, MagnetSizing.Constraint).Right(P).VerticallyWithin(P);
        h.Add(new MagnetChain { MagnetId = "nameRow", Style = MagnetChainStyle.Packed }.With("CardName", "Starred"));

        h.Layout(width, double.PositiveInfinity, width);

        var name = h.Frame("CardName");
        name.X.Should().Be(72);
        name.Width.Should().Be(expectedNameWidth);
        h.Frame("Starred").X.Should().Be(expectedStarX);
        h.Frame("Starred").X.Should().Be(name.Right + 4);
        h.Frame("Money").X.Should().Be(width - 98);
        h.Fake("CardName").MeasureCount.Should().Be(1);
    }
}

public class MagnetPackedChainBiasTests
{
    private const string P = MagnetAnchor.Parent;

    [Theory]
    [InlineData(0, 0)]
    [InlineData(0.3, 30.6)]
    [InlineData(0.5, 51)]
    [InlineData(1, 102)]
    public void PackedChainUsesTheHeadBias(double bias, double expectedStart)
    {
        var h = new EngineHarness();
        h.View("a", 40, 20).Left(P).Top(P).Bias(bias, 0.5);
        h.View("b", 50, 20).Left("a", MagnetPole.Right, 8).Right(P).Top("a", MagnetPole.Top);
        h.Add(new MagnetChain { MagnetId = "row", Style = MagnetChainStyle.Packed }.With("a", "b"));

        // span 200, content 40 + 8 + 50 = 98, slack 102
        h.Layout(200, 100, 200, 100);

        h.Frame("a").ShouldBe(expectedStart, 0, 40, 20);
        h.Frame("b").ShouldBe(expectedStart + 48, 0, 50, 20);
    }

    [Fact]
    public void PackedChainHugsItsContentAndInnerBiasIsIrrelevantWhenHugging()
    {
        var h = new EngineHarness();
        h.View("a", 40, 20).Left(P).Top(P).Bias(0.3, 0.5);
        h.View("b", 50, 20).Left("a", MagnetPole.Right, 8).Right(P).Top("a", MagnetPole.Top);
        h.Add(new MagnetChain { MagnetId = "row", Style = MagnetChainStyle.Packed }.With("a", "b"));

        var measured = h.Layout(double.PositiveInfinity, 100);

        measured.Width.Should().Be(98);
        h.Frame("a").ShouldBe(0, 0, 40, 20);
        h.Frame("b").ShouldBe(48, 0, 50, 20);
    }

    [Fact]
    public void VerticalPackedChainUsesTheHeadVerticalBias()
    {
        var h = new EngineHarness();
        h.View("a", 20, 30).Left(P).Top(P).Bias(0.5, 0.25);
        h.View("b", 20, 30).Left(P).Top("a", MagnetPole.Bottom, 10).Bottom(P);
        h.Add(new MagnetChain { MagnetId = "col", Orientation = MagnetOrientation.Vertical, Style = MagnetChainStyle.Packed }.With("a", "b"));

        // span 170, content 70, slack 100 → start 25
        h.Layout(100, 170, 100, 170);

        h.Frame("a").ShouldBe(0, 25, 20, 30);
        h.Frame("b").ShouldBe(0, 65, 20, 30);
    }
}

public class MagnetStagePercentAndScaleTests
{
    private const string P = MagnetAnchor.Parent;

    [Fact]
    public void StagePercentIsRelativeToTheStageRegardlessOfAnchors()
    {
        var h = new EngineHarness();
        h.View("a", 40, 20).Left(P).Top(P).Size(60, 20);
        h.View("b", 40, 20).Left("a", MagnetPole.Right, 10).Top(P).Size(MagnetSizing.StagePercent(0.5), MagnetSizing.StagePercent(0.25));

        h.Layout(300, 200, 300, 200);

        h.Frame("b").ShouldBe(70, 0, 150, 50);
    }

    [Fact]
    public void StagePercentWithASingleAnchorFillsTheStageWhenValueIsOne()
    {
        var h = new EngineHarness();
        h.View("a", 40, 20).Right(P).Top(P).Size(MagnetSizing.StagePercent(1), 20);

        h.Layout(300, 200, 300, 200);

        h.Frame("a").ShouldBe(0, 0, 300, 20);
    }

    [Fact]
    public void MeasuredScaleMultipliesTheMeasuredSize()
    {
        var h = new EngineHarness();
        h.View("a", 40, 20).Left(P).Top(P).Size(MagnetSizing.Scaled(1.5), MagnetSizing.Scaled(2));

        var measured = h.Layout(300, 200);

        h.Frame("a").ShouldBe(0, 0, 60, 40);
        measured.Should().Be(new Size(60, 40));
    }

    [Fact]
    public void MeasuredScaleIsAValueChange()
    {
        MagnetSizing.Measured.DiffWith(MagnetSizing.Scaled(1.5)).Should().Be(MagnetChange.Values);
        MagnetSizing.StagePercent(0.5).ToString().Should().Be("StagePercent 0.5");
        MagnetSizing.Scaled(1.5).ToString().Should().Be("Measured 1.5");
    }
}
