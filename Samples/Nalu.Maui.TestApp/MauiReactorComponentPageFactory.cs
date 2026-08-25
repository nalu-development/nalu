using MauiReactor;
using MauiPage = Microsoft.Maui.Controls.Page;

namespace Nalu.Maui.TestApp;

// The MauiReactor bridge for Nalu component-based navigation — deliberately app-side code, not
// a Nalu package: this is the canonical copy-paste implementation documented in
// conceptual_docs/navigation-mauireactor.md (keep the two in sync). Registered in MauiProgram
// via nav.UseComponentPageFactory<MauiReactorComponentPageFactory>().
//
// It mounts the component through MauiReactor's public TemplateHost: the Page the component
// renders becomes the navigation page, SetState re-renders update that same page in place, and
// disposing the handle (page popped) unmounts the component tree.
internal sealed class MauiReactorComponentPageFactory : IComponentPageFactory
{
    public IComponentPageHandle CreatePage(object component)
    {
        if (component is not VisualNode visualNode)
        {
            throw new InvalidOperationException($"{component.GetType().FullName} must derive from MauiReactor.Component to be used as a component-based page.");
        }

        var host = new TemplateHost(visualNode);

        if (host.NativeElement is not MauiPage page)
        {
            host.Stop();

            throw new InvalidOperationException($"{component.GetType().FullName} must render a Page-derived root (e.g. ContentPage) to be used as a navigation page.");
        }

        return new Handle(host, page, component);
    }

    private sealed class Handle(TemplateHost host, MauiPage page, object component) : IComponentPageHandle
    {
        public MauiPage Page => page;

        public object LifecycleTarget => component;

        public void Dispose() => host.Stop();
    }
}
