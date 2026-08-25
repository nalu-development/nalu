using MauiReactor;
using Microsoft.Extensions.DependencyInjection;
using MauiControls = Microsoft.Maui.Controls;

namespace Nalu.Maui.Test.NavigationTests;

/// <summary>
/// Validates the DOCUMENTED MauiReactor bridge (conceptual_docs/navigation-mauireactor.md):
/// Nalu ships no adapter package — apps paste this IComponentPageFactory. The copy below must
/// stay in sync with the docs and with the TestApp's MauiReactorComponentPageFactory.
/// </summary>
public class MauiReactorAdapterTests
{
    private sealed class MauiReactorComponentPageFactory : IComponentPageFactory
    {
        public IComponentPageHandle CreatePage(object component)
        {
            if (component is not VisualNode visualNode)
            {
                throw new InvalidOperationException($"{component.GetType().FullName} must derive from MauiReactor.Component to be used as a component-based page.");
            }

            var host = new TemplateHost(visualNode);

            if (host.NativeElement is not MauiControls.Page page)
            {
                host.Stop();

                throw new InvalidOperationException($"{component.GetType().FullName} must render a Page-derived root (e.g. ContentPage) to be used as a navigation page.");
            }

            return new Handle(host, page, component);
        }

        private sealed class Handle(TemplateHost host, MauiControls.Page page, object component) : IComponentPageHandle
        {
            public MauiControls.Page Page => page;

            public object LifecycleTarget => component;

            public void Dispose() => host.Stop();
        }
    }

    private class CounterState
    {
        public int Count { get; set; }
    }

    private class CounterComponent : Component<CounterState>, IEnteringAware, ILeavingAware
    {
        public List<string> Events { get; } = [];

        public ValueTask OnEnteringAsync()
        {
            Events.Add("Entering");

            return ValueTask.CompletedTask;
        }

        public ValueTask OnLeavingAsync()
        {
            Events.Add("Leaving");

            return ValueTask.CompletedTask;
        }

        public void Increment() => SetState(s => s.Count++);

        public override VisualNode Render()
            => ContentPage(
                Label($"Count: {State.Count}")
                    .AutomationId("CounterLabel")
            );
    }

    private class ViewOnlyComponent : Component
    {
        public override VisualNode Render() => Label("Not a page");
    }

    private static MauiReactorComponentPageFactory CreateFactory() => new();

    [Fact(DisplayName = "MauiReactor factory should mount the component and return the native page it renders")]
    public void FactoryShouldMountTheComponentAndReturnTheNativePage()
    {
        var component = new CounterComponent();
        var handle = CreateFactory().CreatePage(component);

        handle.LifecycleTarget.Should().BeSameAs(component);
        var page = handle.Page.Should().BeAssignableTo<MauiControls.ContentPage>().Subject;
        var label = page.Content.Should().BeOfType<MauiControls.Label>().Subject;
        label.Text.Should().Be("Count: 0");
    }

    [Fact(DisplayName = "MauiReactor state changes should re-render into the SAME native page instance")]
    public void StateChangesShouldReRenderIntoTheSameNativePageInstance()
    {
        var component = new CounterComponent();
        var handle = CreateFactory().CreatePage(component);
        var page = (MauiControls.ContentPage) handle.Page;
        var label = (MauiControls.Label) page.Content;

        component.Increment();

        handle.Page.Should().BeSameAs(page);
        ((MauiControls.Label) page.Content).Should().BeSameAs(label);
        label.Text.Should().Be("Count: 1");
    }

    [Fact(DisplayName = "MauiReactor component page should be the navigation lifecycle target, without touching the page BindingContext")]
    public void ComponentShouldBeTheNavigationLifecycleTarget()
    {
        var component = new CounterComponent();
        var handle = CreateFactory().CreatePage(component);

        // Wire the page exactly like NavigationService.CreatePage does.
        var scope = new ServiceCollection().BuildServiceProvider().CreateScope();
        PageNavigationContext.Set(handle.Page, new PageNavigationContext(scope) { ComponentHandle = handle });

        NavigationHelper.GetLifecycleTarget(handle.Page).Should().BeSameAs(component);
        handle.Page.IsSet(BindableObject.BindingContextProperty).Should().BeFalse();
        (NavigationHelper.GetLifecycleTarget(handle.Page) as ILeavingAware).Should().NotBeNull();

        PageNavigationContext.Dispose(handle.Page);
    }

    [Fact(DisplayName = "MauiReactor factory should throw a descriptive error when the component does not render a page")]
    public void FactoryShouldThrowWhenTheComponentDoesNotRenderAPage()
    {
        var create = () => CreateFactory().CreatePage(new ViewOnlyComponent());

        create.Should().Throw<InvalidOperationException>().WithMessage("*must render a Page-derived root*");
    }

    [Fact(DisplayName = "MauiReactor factory should throw a descriptive error for non-component objects")]
    public void FactoryShouldThrowForNonComponentObjects()
    {
        var create = () => CreateFactory().CreatePage(new object());

        create.Should().Throw<InvalidOperationException>().WithMessage("*must derive from MauiReactor.Component*");
    }
}
