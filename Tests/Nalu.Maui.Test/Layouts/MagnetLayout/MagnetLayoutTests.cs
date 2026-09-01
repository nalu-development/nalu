using Microsoft.Maui.Layouts;

namespace Nalu.Maui.Test.Layouts.MagnetLayout;

public class MagnetLayoutTests
{
    private const string _p = MagnetAnchor.Parent;

    private sealed class TestView(double width, double height) : View
    {
        public int MeasureCount { get; private set; }

        protected override Size MeasureOverride(double widthConstraint, double heightConstraint)
        {
            MeasureCount++;

            return new Size(Math.Min(width, widthConstraint), Math.Min(height, heightConstraint));
        }
    }

    private static (Magnet Magnet, ILayoutManager Manager) CreateMagnet()
    {
        var magnet = new Magnet();

        return (magnet, magnet.GetLayoutManager());
    }

    private static Size Layout(ILayoutManager manager, double wc, double hc, double? aw = null, double? ah = null)
    {
        var measured = manager.Measure(wc, hc);
        manager.ArrangeChildren(new Rect(0, 0, aw ?? measured.Width, ah ?? measured.Height));

        return measured;
    }

    [Fact]
    public void InlineAttachedPropertiesLayoutChildren()
    {
        var (magnet, manager) = CreateMagnet();
        var avatar = new TestView(48, 48);
        Magnet.SetMagnetId(avatar, "avatar");
        Magnet.SetLeftTo(avatar, "parent.Left,16");
        Magnet.SetTopTo(avatar, "parent.Top,16");
        Magnet.SetWidthSizing(avatar, "48");
        Magnet.SetHeightSizing(avatar, "48");
        var title = new TestView(100, 20);
        Magnet.SetMagnetId(title, "title");
        Magnet.SetLeftTo(title, "avatar.Right,12");
        Magnet.SetTopTo(title, "avatar.Top");
        magnet.Add(avatar);
        magnet.Add(title);

        var measured = Layout(manager, 400, 400);

        measured.Should().Be(new Size(176, 64));
        avatar.Frame.Should().Be(new Rect(16, 16, 48, 48));
        title.Frame.Should().Be(new Rect(76, 16, 100, 20));
        avatar.AutomationId.Should().Be("avatar", "MagnetId propagates to AutomationId");
    }

    [Fact]
    public void PaddingOffsetsTheStage()
    {
        var (magnet, manager) = CreateMagnet();
        magnet.Padding = new Thickness(5, 7, 9, 11);
        var a = new TestView(40, 20);
        Magnet.SetMagnetId(a, "a");
        Magnet.SetLeftTo(a, "parent.Left");
        Magnet.SetTopTo(a, "parent.Top");
        magnet.Add(a);

        var measured = Layout(manager, 400, 400);

        measured.Should().Be(new Size(54, 38));
        a.Frame.Should().Be(new Rect(5, 7, 40, 20));
    }

    [Fact]
    public void DefinitionDeclaredNodesBindById()
    {
        var (magnet, manager) = CreateMagnet();
        magnet.Definition = new MagnetDefinition().Add(
            new MagnetView().Id("a").Left(_p, margin: 10).Top(_p),
            new MagnetView().Id("b").Left("a", MagnetPole.Right, 5).Top(_p)
        );
        var a = new TestView(40, 20);
        Magnet.SetMagnetId(a, "a");
        var b = new TestView(30, 30);
        Magnet.SetMagnetId(b, "b");
        magnet.Add(a);
        magnet.Add(b);

        Layout(manager, 400, 400);

        a.Frame.Should().Be(new Rect(10, 0, 40, 20));
        b.Frame.Should().Be(new Rect(55, 0, 30, 30));

        // Removing a view keeps the declared node, the anchor uses the gone margin (defaults to the margin) and a collapses to 0.
        magnet.Remove(a);
        Layout(manager, 400, 400);
        b.Frame.Should().Be(new Rect(5, 0, 30, 30));
    }

    [Fact]
    public void DuplicateIdBetweenDefinitionAndInlineIsAnError()
    {
        var (magnet, _) = CreateMagnet();
        magnet.Definition = new MagnetDefinition().Add(new MagnetView().Id("title").Left(_p));
        var title = new TestView(40, 20);
        Magnet.SetMagnetId(title, "title");
        Magnet.SetLeftTo(title, "parent.Left");

        var act = () => magnet.Add(title);

        act.Should().Throw<InvalidOperationException>().WithMessage("MagnetId 'title' is defined both in the MagnetDefinition and inline on a child view.");
    }

    [Fact]
    public void MissingIdIsAnError()
    {
        var (magnet, _) = CreateMagnet();
        var view = new TestView(40, 20);
        Magnet.SetLeftTo(view, "parent.Left");

        var act = () => magnet.Add(view);

        act.Should().Throw<InvalidOperationException>().WithMessage("*no MagnetId*");
    }

    [Fact]
    public void DefinitionCannotBeShared()
    {
        var definition = new MagnetDefinition();
        var m1 = new Magnet { Definition = definition };
        var m2 = new Magnet();

        var act = () => m2.Definition = definition;

        act.Should().Throw<InvalidOperationException>().WithMessage("*cannot be shared*");
        m1.Definition.Should().BeSameAs(definition);
    }

    [Fact]
    public void ChildrenWithoutIdAreArrangedAtOrigin()
    {
        var (magnet, manager) = CreateMagnet();
        var plain = new TestView(40, 20);
        var a = new TestView(30, 30);
        Magnet.SetMagnetId(a, "a");
        Magnet.SetRightTo(a, "parent.Right");
        magnet.Add(plain);
        magnet.Add(a);

        var measured = Layout(manager, 400, 400, 400, 400);

        measured.Should().Be(new Size(40, 30));
        plain.Frame.Should().Be(new Rect(0, 0, 40, 20));
        a.Frame.Should().Be(new Rect(370, 0, 30, 30));
    }

    [Fact]
    public void ValueChangesPatchWithoutRecompiling()
    {
        var (magnet, manager) = CreateMagnet();
        var a = new TestView(40, 20);
        Magnet.SetMagnetId(a, "a");
        Magnet.SetLeftTo(a, "parent.Left,10");
        magnet.Add(a);
        Layout(manager, 400, 400);
        var tape = magnet.Engine.Tape;

        Magnet.SetLeftTo(a, "parent.Left,30");
        Layout(manager, 400, 400);

        magnet.Engine.Tape.Should().BeSameAs(tape);
        a.Frame.Should().Be(new Rect(30, 0, 40, 20));

        Magnet.SetLeftTo(a, "parent.Right,-40");
        Layout(manager, 400, 400, 400, 400);
        magnet.Engine.Tape.Should().NotBeSameAs(tape, "target changes recompile");
        a.Frame.Should().Be(new Rect(360, 0, 40, 20));
    }

    [Fact]
    public void ArrangeWithFillDoesNotRemeasureChildren()
    {
        var (magnet, manager) = CreateMagnet();
        var a = new TestView(40, 20);
        Magnet.SetMagnetId(a, "a");
        Magnet.SetLeftTo(a, "parent.Left");
        Magnet.SetTopTo(a, "parent.Top");
        magnet.Add(a);

        var count = a.MeasureCount;
        Layout(manager, 400, 400, 400, 400);

        a.MeasureCount.Should().Be(count + 1, "the arrange following a measure with matching bounds reuses the child measures");

        // A second arrange without a measure in between cannot know whether children changed: it re-measures.
        manager.ArrangeChildren(new Rect(0, 0, 400, 400));
        a.MeasureCount.Should().Be(count + 2);
    }

    [Fact]
    public void ExplicitAutomationIdWins()
    {
        var (magnet, _) = CreateMagnet();
        var a = new TestView(40, 20) { AutomationId = "custom" };
        Magnet.SetMagnetId(a, "a");
        magnet.Add(a);

        a.AutomationId.Should().Be("custom");

        var (m2, _) = CreateMagnet();
        m2.PropagateMagnetIdToAutomationId = false;
        var b = new TestView(40, 20);
        Magnet.SetMagnetId(b, "b");
        m2.Add(b);

        b.AutomationId.Should().BeNull();
    }

    [Fact]
    public void FluentConstraintsOnAChild()
    {
        var (magnet, manager) = CreateMagnet();
        var a = new TestView(40, 20);
        Magnet.GetConstraints(a).Id("a").Within(_p).Size(20, 20);
        magnet.Add(a);

        Layout(manager, 100, 100, 100, 100);

        a.Frame.Should().Be(new Rect(40, 40, 20, 20));
    }

    [Fact]
    public async Task ValuesTransitionInterpolatesInputs()
    {
        Animation? captured = null;
        Action<double, bool>? finished = null;
        Magnet.AnimationDriver = (_, animation, _, f) =>
        {
            captured = animation;
            finished = f;
        };

        try
        {
            var (magnet, manager) = CreateMagnet();
            var a = new TestView(40, 20);
            Magnet.SetMagnetId(a, "a");
            Magnet.SetLeftTo(a, "parent.Left,0");
            Magnet.SetTopTo(a, "parent.Top");
            magnet.Add(a);
            Layout(manager, 400, 400, 400, 400);

            var task = magnet.TransitionToAsync(() => Magnet.SetLeftTo(a, "parent.Left,100"), easing: Easing.Linear);

            magnet.IsTransitioning.Should().BeTrue();
            captured.Should().NotBeNull();
            captured!.GetCallback()(0.5);

            // The layout's own size animates too (40 → 140): the parent re-measures and re-arranges.
            manager.Measure(400, 400).Width.Should().Be(90);
            manager.ArrangeChildren(new Rect(0, 0, 400, 400));
            a.Frame.Should().Be(new Rect(50, 0, 40, 20));
            captured.GetCallback()(1);
            manager.Measure(400, 400).Width.Should().Be(140);
            manager.ArrangeChildren(new Rect(0, 0, 400, 400));
            a.Frame.Should().Be(new Rect(100, 0, 40, 20));
            finished!(1, false);

            (await task).Should().BeTrue();
            magnet.IsTransitioning.Should().BeFalse();
            Layout(manager, 400, 400, 400, 400);
            a.Frame.Should().Be(new Rect(100, 0, 40, 20));
        }
        finally
        {
            Magnet.AnimationDriver = null;
        }
    }

    [Fact]
    public async Task StructureTransitionInterpolatesFrames()
    {
        Animation? captured = null;
        Action<double, bool>? finished = null;
        Magnet.AnimationDriver = (_, animation, _, f) =>
        {
            captured = animation;
            finished = f;
        };

        try
        {
            var (magnet, manager) = CreateMagnet();
            var a = new TestView(40, 20);
            Magnet.SetMagnetId(a, "a");
            Magnet.SetLeftTo(a, "parent.Left");
            Magnet.SetTopTo(a, "parent.Top");
            magnet.Add(a);
            Layout(manager, 400, 400, 400, 400);

            var task = magnet.TransitionToAsync(() =>
                {
                    Magnet.SetLeftTo(a, null);
                    Magnet.SetRightTo(a, "parent.Right");
                },
                easing: Easing.Linear
            );

            captured!.GetCallback()(0.5);
            a.Frame.Should().Be(new Rect(180, 0, 40, 20));
            finished!(1, false);

            (await task).Should().BeTrue();
            Layout(manager, 400, 400, 400, 400);
            a.Frame.Should().Be(new Rect(360, 0, 40, 20));
        }
        finally
        {
            Magnet.AnimationDriver = null;
        }
    }
}

public class MagnetValueTypeTests
{
    [Theory]
    [InlineData("parent.Left", "parent", MagnetPole.Left, 0, null)]
    [InlineData("avatar.Right,12", "avatar", MagnetPole.Right, 12, null)]
    [InlineData("avatar.Right,12,gone:0", "avatar", MagnetPole.Right, 12, 0d)]
    [InlineData("a.b.Bottom, 1.5", "a.b", MagnetPole.Bottom, 1.5, null)]
    public void AnchorParsing(string input, string target, MagnetPole pole, double margin, double? gone)
    {
        var anchor = MagnetAnchor.Parse(input);

        anchor.Should().Be(new MagnetAnchor(target, pole, margin, gone));
        MagnetAnchor.Parse(anchor.ToString()).Should().Be(anchor);
    }

    [Theory]
    [InlineData("48", MagnetSizingUnit.Fixed, 48)]
    [InlineData("*", MagnetSizingUnit.Constraint, 0)]
    [InlineData("50%", MagnetSizingUnit.ConstraintPercent, 0.5)]
    [InlineData("", MagnetSizingUnit.Measured, 0)]
    public void SizeParsing(string input, MagnetSizingUnit unit, double value)
    {
        var size = MagnetSizing.Parse(input);

        size.Should().Be(new MagnetSizing(unit, value));

        if (input.Length > 0)
        {
            MagnetSizing.Parse(size.ToString()).Should().Be(size);
        }
    }

    [Fact]
    public void OtherUnitsUseTheMarkupExtension()
    {
        var act = () => MagnetSizing.Parse("ratio:1.5");
        act.Should().Throw<FormatException>();

        new MagnetSizingExtension { Value = 1.5, Unit = MagnetSizingUnit.Ratio }.ProvideValue(null!).Should().Be(MagnetSizing.Ratio(1.5));
        new MagnetSizingExtension { Unit = MagnetSizingUnit.Constraint, Max = 320 }.ProvideValue(null!).Should().Be(MagnetSizing.Constraint.WithBounds(max: 320));
        new MagnetSizingExtension { Value = 48 }.ProvideValue(null!).Should().Be(MagnetSizing.Fixed(48));
    }

    [Fact]
    public void AnchorDiffClassification()
    {
        var a = new MagnetAnchor("x", MagnetPole.Left, 10);
        a.DiffWith(a with { Margin = 20 }).Should().Be(MagnetChange.Values);
        a.DiffWith(a with { GoneMargin = 3 }).Should().Be(MagnetChange.Values);
        a.DiffWith(a with { Pole = MagnetPole.Right }).Should().Be(MagnetChange.Structure);
        a.DiffWith(a with { Target = "y" }).Should().Be(MagnetChange.Structure);
        a.DiffWith(a).Should().Be(MagnetChange.None);
        MagnetAnchor.Diff(null, a).Should().Be(MagnetChange.Structure);
    }

    [Fact]
    public void SizeDiffClassification()
    {
        var s = MagnetSizing.Fixed(10);
        s.DiffWith(MagnetSizing.Fixed(20)).Should().Be(MagnetChange.Values);
        s.DiffWith(MagnetSizing.Constraint).Should().Be(MagnetChange.Structure);
        s.DiffWith(s.WithBounds(max: 50)).Should().Be(MagnetChange.Structure);
        s.WithBounds(max: 50).DiffWith(s.WithBounds(max: 60)).Should().Be(MagnetChange.Values);
    }

    [Fact]
    public void NodeIdsAreCoercibleFromCommaSeparatedStrings()
    {
        var converter = new MagnetNodeIdsTypeConverter();
        converter.ConvertFrom("avatar, subtitle").Should().BeEquivalentTo(new[] { "avatar", "subtitle" }, o => o.WithStrictOrdering());
        converter.ConvertFrom("a,,b, ").Should().BeEquivalentTo(new[] { "a", "b" }, o => o.WithStrictOrdering());
        converter.ConvertTo(new[] { "a", "b" }, typeof(string)).Should().Be("a, b");

        // The setter replaces the contents: the change-tracked backing list never changes identity.
        var barrier = new MagnetBarrier();
        var backing = barrier.Nodes;
        barrier.Nodes = (IList<string>) converter.ConvertFrom("avatar,subtitle")!;
        barrier.Nodes.Should().BeSameAs(backing).And.Equal("avatar", "subtitle");
        barrier.Nodes = ["other"];
        barrier.Nodes.Should().BeSameAs(backing).And.Equal("other");

        var chain = new MagnetChain { Nodes = ["a", "b"] };
        chain.Nodes.Should().Equal("a", "b");
    }

    [Fact]
    public void WeightsAreCoercibleFromCommaSeparatedStrings()
    {
        var converter = new MagnetWeightsTypeConverter();
        converter.ConvertFrom("2, 1.5").Should().BeEquivalentTo(new[] { 2d, 1.5 }, o => o.WithStrictOrdering());
        converter.ConvertTo(new[] { 2d, 1.5 }, typeof(string)).Should().Be("2, 1.5");

        var chain = new MagnetChain();
        var backing = chain.Weights;
        chain.Weights = (IList<double>) converter.ConvertFrom("2,1")!;
        chain.Weights.Should().BeSameAs(backing).And.Equal(2, 1);
    }
}

public class MagnetRecycledCellTests
{
    private sealed class TextView : View
    {
        public string Text { get; private set; } = "";

        public void SetText(string text)
        {
            Text = text;
            InvalidateMeasure();
        }

        protected override Size MeasureOverride(double widthConstraint, double heightConstraint) => new(Text.Length * 10, 20);
    }

    [Fact]
    public void ArrangeAfterAChildInvalidationRemeasuresEvenWithTheSameBounds()
    {
        var magnet = new Magnet();
        var manager = magnet.GetLayoutManager();
        var label = new TextView();
        Magnet.SetMagnetId(label, "label");
        Magnet.SetLeftTo(label, "parent.Left");
        Magnet.SetTopTo(label, "parent.Top");
        magnet.Add(label);

        // Template inflated before binding: empty text.
        manager.Measure(300, 60);
        manager.ArrangeChildren(new Rect(0, 0, 300, 60));
        label.Frame.Width.Should().Be(0);

        // Binding applies, the recycler re-arranges the cell without a fresh measure.
        label.SetText("Visa");
        manager.ArrangeChildren(new Rect(0, 0, 300, 60));

        label.Frame.Width.Should().Be(40);
    }
}

public class MagnetShortcutTests
{
    private sealed class TestView(double width, double height) : View
    {
        protected override Size MeasureOverride(double widthConstraint, double heightConstraint) => new(width, height);
    }

    private static (Magnet Magnet, Microsoft.Maui.Layouts.ILayoutManager Manager) CreateMagnet()
    {
        var magnet = new Magnet();

        return (magnet, magnet.GetLayoutManager());
    }

    [Fact]
    public void FluentShortcutsWithTypedTargets()
    {
        var (magnet, manager) = CreateMagnet();
        var avatar = new TestView(48, 48);
        Magnet.GetConstraints(avatar).Id("avatar").AlignLeft("parent", 16).AlignTop("parent", 16);
        var title = new TestView(100, 20);
        Magnet.GetConstraints(title).Id("title").After(avatar, 12).AlignTop(avatar);
        var subtitle = new TestView(80, 16);
        Magnet.GetConstraints(subtitle).Id("subtitle").AlignLeft(title).Below(title, 2);
        var badge = new TestView(20, 20);
        Magnet.GetConstraints(badge).Id("badge").Within(avatar);
        var bar = new TestView(10, 10);
        Magnet.GetConstraints(bar).Id("bar").FillWidth("parent", 16).Below(subtitle, 8).Size(MagnetSizing.Constraint, 4);
        magnet.Add(avatar);
        magnet.Add(title);
        magnet.Add(subtitle);
        magnet.Add(badge);
        magnet.Add(bar);

        manager.Measure(400, 400);
        manager.ArrangeChildren(new Rect(0, 0, 400, 400));

        avatar.Frame.Should().Be(new Rect(16, 16, 48, 48));
        title.Frame.Should().Be(new Rect(76, 16, 100, 20));
        subtitle.Frame.Should().Be(new Rect(76, 38, 80, 16));
        badge.Frame.Should().Be(new Rect(30, 30, 20, 20));
        bar.Frame.Should().Be(new Rect(16, 62, 368, 4));
    }

    [Fact]
    public void ChainAndBarrierAcceptViewsAsMembers()
    {
        var a = new TestView(30, 20);
        Magnet.SetMagnetId(a, "a");
        var b = new TestView(30, 20);
        Magnet.SetMagnetId(b, "b");
        var chain = new MagnetChain { MagnetId = "row" }.With(a, b);
        chain.Nodes.Should().Equal("a", "b");
        var barrier = new MagnetBarrier { MagnetId = "end" }.With(a, Magnet.GetConstraints(b));
        barrier.Nodes.Should().Equal("a", "b");

        var act = () => new MagnetChain { MagnetId = "x" }.With(new TestView(1, 1));
        act.Should().Throw<InvalidOperationException>().WithMessage("*no MagnetId*");
    }

    [Theory]
    [InlineData("avatar", "avatar", 0, null)]
    [InlineData("avatar,12", "avatar", 12, null)]
    [InlineData("avatar,12,0", "avatar", 12, 0d)]
    [InlineData("avatar, 12, gone:4", "avatar", 12, 4d)]
    public void TargetParsing(string input, string target, double margin, double? gone)
    {
        var t = MagnetTarget.Parse(input);
        t.Should().Be(new MagnetTarget(target, margin, gone));
        MagnetTarget.Parse(t.ToString()).Should().Be(t);
    }
}

public class MagnetSetOnlyAttachedTests
{
    private sealed class TestView(double width, double height) : View
    {
        protected override Size MeasureOverride(double widthConstraint, double heightConstraint) => new(width, height);
    }

    [Fact]
    public void ShortcutsWriteTheNodeAndClearOnlyWhatTheyWrote()
    {
        var label = new TestView(40, 20);
        Magnet.SetMagnetId(label, "label");
        var node = Magnet.GetConstraints(label);

        Magnet.SetAfter(label, "avatar,12,0");
        node.LeftTo.Should().Be(new MagnetAnchor("avatar", MagnetPole.Right, 12, 0));

        // Somebody else (fluent) overwrites the same side…
        node.AlignLeft("other");

        // …so clearing the shortcut must NOT remove the newer constraint.
        Magnet.SetAfter(label, null);
        node.LeftTo.Should().Be(new MagnetAnchor("other", MagnetPole.Left));

        // Clearing an untouched shortcut removes exactly what it wrote.
        Magnet.SetBelow(label, "title,2");
        node.TopTo.Should().Be(new MagnetAnchor("title", MagnetPole.Bottom, 2));
        Magnet.SetBelow(label, null);
        node.TopTo.Should().BeNull();

        // Composite shortcuts.
        Magnet.SetFillWidth(label, "parent,16");
        node.LeftTo.Should().Be(new MagnetAnchor("parent", MagnetPole.Left, 16));
        node.RightTo.Should().Be(new MagnetAnchor("parent", MagnetPole.Right, 16));
        node.WidthSizing.Should().Be(MagnetSizing.Constraint);
        node.RightTo = new MagnetAnchor("x", MagnetPole.Left);
        Magnet.SetFillWidth(label, null);
        node.LeftTo.Should().BeNull("written by FillWidth and untouched");
        node.RightTo.Should().Be(new MagnetAnchor("x", MagnetPole.Left), "overwritten in the meantime");
        node.WidthSizing.Should().Be(MagnetSizing.Measured);
    }

    [Fact]
    public void PrimitiveAttachedPropertiesFollowTheSameRule()
    {
        var label = new TestView(40, 20);
        Magnet.SetMagnetId(label, "label");
        var node = Magnet.GetConstraints(label);

        Magnet.SetLeftTo(label, "a.Right,4");
        Magnet.SetWidthSizing(label, "*");
        Magnet.SetHorizontalBias(label, 0.2);
        node.LeftTo.Should().Be(new MagnetAnchor("a", MagnetPole.Right, 4));
        node.WidthSizing.Should().Be(MagnetSizing.Constraint);
        node.HorizontalBias.Should().Be(0.2);

        // Last set wins across attached and fluent.
        Magnet.SetAlignLeft(label, "b");
        node.LeftTo.Should().Be(new MagnetAnchor("b", MagnetPole.Left));
        Magnet.SetLeftTo(label, null);
        node.LeftTo.Should().Be(new MagnetAnchor("b", MagnetPole.Left), "LeftTo no longer owns the value");

        node.WidthSizing = MagnetSizing.Fixed(10);
        label.ClearValue(Magnet.WidthSizingProperty);
        node.WidthSizing.Should().Be(MagnetSizing.Fixed(10));
        node.HorizontalBias = 0.7;
        label.ClearValue(Magnet.HorizontalBiasProperty);
        node.HorizontalBias.Should().Be(0.7);
    }
}
