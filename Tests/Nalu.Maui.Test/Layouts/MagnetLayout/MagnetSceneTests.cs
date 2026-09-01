using Microsoft.Maui.Layouts;
using Nalu.MagnetLayout.Engine;

namespace Nalu.Maui.Test.Layouts.MagnetLayout;

/// <summary>
/// Scene-visibility semantics: <see cref="MagnetView.ApplyVisibility" /> stamped on attach/bind/change,
/// deferred and animated inside <see cref="Magnet.TransitionToAsync(System.Action,uint,Easing?)" />.
/// </summary>
[Collection("MagnetSharedState")] // shared statics (Magnet.AnimationDriver, MagnetTapeCache): never run these classes concurrently.
public class MagnetSceneTests
{
    private const string _p = MagnetAnchor.Parent;

    private sealed class TestView(double width, double height) : View
    {
        protected override Size MeasureOverride(double widthConstraint, double heightConstraint)
            => new(Math.Min(width, widthConstraint), Math.Min(height, heightConstraint));
    }

    private static readonly System.Reflection.PropertyInfo _layoutManagerProperty =
        typeof(Layout).GetProperty("LayoutManager", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;

    private static (Magnet Magnet, ILayoutManager Manager) CreateMagnet()
    {
        var magnet = new Magnet();

        return (magnet, (ILayoutManager) _layoutManagerProperty.GetValue(magnet)!);
    }

    private static Size Layout(ILayoutManager manager, double wc, double hc, double? aw = null, double? ah = null)
    {
        var measured = manager.Measure(wc, hc);
        manager.ArrangeChildren(new Rect(0, 0, aw ?? measured.Width, ah ?? measured.Height));

        return measured;
    }

    private static TestView NamedView(string id, double width = 40, double height = 20)
    {
        var view = new TestView(width, height);
        Magnet.SetMagnetId(view, id);

        return view;
    }

    [Fact]
    public void HideIsAppliedWhenSwappingDefinitionsWithoutATransition()
    {
        var (magnet, manager) = CreateMagnet();
        magnet.Definition = new MagnetDefinition().Add(new MagnetView().Id("badge").Left(_p).Top(_p));
        var badge = NamedView("badge");
        magnet.Add(badge);
        Layout(manager, 400, 400);

        badge.IsVisible.Should().BeTrue();

        magnet.Definition = new MagnetDefinition().Add(
            new MagnetView().Id("badge").Left(_p).Top(_p).Visibility(MagnetVisibilityAction.Hide)
        );

        badge.IsVisible.Should().BeFalse("a plain definition swap applies immediately");
    }

    [Fact]
    public void HideIsAppliedWhenTheChildBindsLate()
    {
        var (magnet, _) = CreateMagnet();
        magnet.Definition = new MagnetDefinition().Add(
            new MagnetView().Id("badge").Left(_p).Top(_p).Visibility(MagnetVisibilityAction.Hide)
        );

        var badge = NamedView("badge");
        magnet.Add(badge);

        badge.IsVisible.Should().BeFalse();
    }

    [Fact]
    public void NoneLeavesTheViewUntouchedAndShowRestores()
    {
        var (magnet, _) = CreateMagnet();
        var badge = NamedView("badge");
        badge.IsVisible = false;
        var node = new MagnetView().Id("badge").Left(_p).Top(_p);
        magnet.Definition = new MagnetDefinition().Add(node);
        magnet.Add(badge);

        badge.IsVisible.Should().BeFalse("None has no opinion");

        // Changing the node value applies immediately (outside a transition).
        node.ApplyVisibility = MagnetVisibilityAction.Show;
        badge.IsVisible.Should().BeTrue();
    }

    [Fact]
    public void ApplyVisibilityIsNotPartOfTheStructuralFingerprint()
    {
        var plain = new MagnetView().Id("x").Left(_p).Top(_p);
        var hidden = new MagnetView().Id("x").Left(_p).Top(_p).Visibility(MagnetVisibilityAction.Hide);

        MagnetCompiler.GetOrCompile([plain]).Should().BeSameAs(MagnetCompiler.GetOrCompile([hidden]));
    }

    [Fact]
    public async Task DeferredHideFadesOutThenWritesIsVisibleAtSettle()
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

            var sceneA = new MagnetDefinition().Add(
                new MagnetView().Id("badge").Left(_p).Top(_p),
                new MagnetView().Id("label").Left("badge", MagnetPole.Right, 12).Top(_p)
            );
            var sceneB = new MagnetDefinition().Add(
                new MagnetView().Id("badge").Left(_p).Top(_p).Visibility(MagnetVisibilityAction.Hide),
                new MagnetView().Id("label").Left("badge", MagnetPole.Right, 12, goneMargin: 0).Top(_p)
            );

            var badge = NamedView("badge", 24, 24);
            var label = NamedView("label", 100, 20);
            magnet.Definition = sceneA;
            magnet.Add(badge);
            magnet.Add(label);
            Layout(manager, 400, 400, 400, 400);
            label.Frame.X.Should().Be(36);

            var task = magnet.TransitionToAsync(sceneB, easing: Easing.Linear);

            // Mid-transition: the badge is STILL natively visible, fading out in place;
            // the label interpolates towards the collapsed-state position (gone margin 0 → x 0).
            // The measured size changes across the scenes, so the arrange flows through the parent re-layout.
            badge.IsVisible.Should().BeTrue();
            captured!.GetCallback()(0.5);
            manager.Measure(400, 400);
            manager.ArrangeChildren(new Rect(0, 0, 400, 400));
            badge.Opacity.Should().BeApproximately(0.5, 0.001);
            badge.Frame.X.Should().Be(0, "a disappearing view is frozen in place");
            label.Frame.X.Should().Be(18);

            captured.GetCallback()(1);
            manager.Measure(400, 400);
            manager.ArrangeChildren(new Rect(0, 0, 400, 400));
            label.Frame.X.Should().Be(0);
            finished!(1, false);

            (await task).Should().BeTrue();
            badge.IsVisible.Should().BeFalse("the deferred write lands at settle");
            badge.Opacity.Should().Be(1, "opacity is restored for a future Show");

            Layout(manager, 400, 400, 400, 400);
            label.Frame.X.Should().Be(0);
        }
        finally
        {
            Magnet.AnimationDriver = null;
        }
    }

    [Fact]
    public async Task ShowAppliesUpFrontAndFadesIn()
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

            var sceneA = new MagnetDefinition().Add(
                new MagnetView().Id("badge").Left(_p).Top(_p).Visibility(MagnetVisibilityAction.Hide),
                new MagnetView().Id("label").Left("badge", MagnetPole.Right, 12, goneMargin: 0).Top(_p)
            );
            var sceneB = new MagnetDefinition().Add(
                new MagnetView().Id("badge").Left(_p).Top(_p).Visibility(MagnetVisibilityAction.Show),
                new MagnetView().Id("label").Left("badge", MagnetPole.Right, 12).Top(_p)
            );

            var badge = NamedView("badge", 24, 24);
            var label = NamedView("label", 100, 20);
            magnet.Definition = sceneA;
            magnet.Add(badge);
            magnet.Add(label);
            Layout(manager, 400, 400, 400, 400);
            badge.IsVisible.Should().BeFalse();
            label.Frame.X.Should().Be(0);

            var task = magnet.TransitionToAsync(sceneB, easing: Easing.Linear);

            badge.IsVisible.Should().BeTrue("Show applies up front so the view participates in the end solve");
            captured!.GetCallback()(0.5);
            manager.Measure(400, 400);
            manager.ArrangeChildren(new Rect(0, 0, 400, 400));
            badge.Opacity.Should().BeApproximately(0.5, 0.001);
            label.Frame.X.Should().Be(18);
            finished!(1, false);

            (await task).Should().BeTrue();
            badge.Opacity.Should().Be(1);

            Layout(manager, 400, 400, 400, 400);
            label.Frame.X.Should().Be(36);
        }
        finally
        {
            Magnet.AnimationDriver = null;
        }
    }

    [Fact]
    public async Task InterruptedTransitionStillAppliesTheDeferredHide()
    {
        Animation? captured = null;
        Magnet.AnimationDriver = (_, animation, _, _) => captured = animation;

        try
        {
            var (magnet, manager) = CreateMagnet();
            var sceneA = new MagnetDefinition().Add(new MagnetView().Id("badge").Left(_p).Top(_p));
            var sceneB = new MagnetDefinition().Add(new MagnetView().Id("badge").Left(_p).Top(_p).Visibility(MagnetVisibilityAction.Hide));
            var badge = NamedView("badge", 24, 24);
            magnet.Definition = sceneA;
            magnet.Add(badge);
            Layout(manager, 400, 400, 400, 400);

            var first = magnet.TransitionToAsync(sceneB, easing: Easing.Linear);
            captured!.GetCallback()(0.5);

            // Retarget mid-flight: the pending logical write is applied anyway.
            _ = magnet.TransitionToAsync(() => { }, easing: Easing.Linear);

            (await first).Should().BeFalse();
            badge.IsVisible.Should().BeFalse();
            badge.Opacity.Should().Be(1);
            magnet.IsTransitioning.Should().BeTrue();
        }
        finally
        {
            Magnet.AnimationDriver = null;
        }
    }
}
