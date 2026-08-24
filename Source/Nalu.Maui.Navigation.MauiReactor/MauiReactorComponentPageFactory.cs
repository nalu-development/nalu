using MauiReactor;
using MauiPage = Microsoft.Maui.Controls.Page;

namespace Nalu;

/// <summary>
/// Renders MauiReactor components into native pages for Nalu navigation, using MauiReactor's
/// public <see cref="TemplateHost" /> integration primitive: the component is mounted
/// synchronously, the page it renders becomes the navigation page, and re-renders (state
/// changes) keep updating that same page in place.
/// </summary>
internal sealed class MauiReactorComponentPageFactory : IComponentPageFactory
{
    public IComponentPageHandle CreatePage(object component)
    {
        if (component is not VisualNode visualNode)
        {
            throw new InvalidOperationException(
                $"{component.GetType().FullName} must derive from MauiReactor.Component to be used as a component-based page."
            );
        }

        var host = new TemplateHost(visualNode);

        if (host.NativeElement is not MauiPage page)
        {
            host.Stop();

            throw new InvalidOperationException(
                $"{component.GetType().FullName} must render a Page-derived root (e.g. ContentPage) to be used as a navigation page."
            );
        }

        return new MauiReactorComponentPageHandle(host, page, component);
    }

    private sealed class MauiReactorComponentPageHandle(TemplateHost host, MauiPage page, object component) : IComponentPageHandle
    {
        public MauiPage Page => page;

        public object LifecycleTarget => component;

        public void Dispose() => host.Stop();
    }
}
