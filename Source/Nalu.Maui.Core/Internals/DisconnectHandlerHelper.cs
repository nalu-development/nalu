using System.ComponentModel;

#pragma warning disable CS0618 // Type or member is obsolete
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

#if !NET9_0_OR_GREATER
using System.Reflection;
#endif

namespace Nalu.Internals;

[EditorBrowsable(EditorBrowsableState.Never)]
public static class DisconnectHandlerHelper
{
#if !NET9_0_OR_GREATER
    private static readonly BindableProperty _disconnectPolicyProperty;

    static DisconnectHandlerHelper()
    {
        var policyBindableProperty =
            typeof(Button).Assembly.GetType("Microsoft.Maui.Controls.HandlerProperties")?.GetField("DisconnectPolicyProperty", BindingFlags.Static | BindingFlags.Public)?.GetValue(null) as BindableProperty;

        _disconnectPolicyProperty = policyBindableProperty ?? HandlerPropertiesBackport.DisconnectPolicyBackportProperty;
    }
#endif

    public static void DisconnectHandlers(IView view)
    {
#if !NET9_0_OR_GREATER
        // For our first go here
        // My thinking is to build a flat list of all views in the tree
        // And then iterate down the list disconnecting handlers.
        // This gives me a stable list of views to call DisconnectHandler on
        // I'm assuming as this PR evolves we'll probably add some interfaces
        // that allow handlers to manage their own children and disconnect flow
        var flatList = new List<IView>();
        BuildFlatList(view, flatList);

        foreach (var viewToDisconnect in flatList)
        {
            viewToDisconnect.Handler?.DisconnectHandler();
        }

        return;

        static void BuildFlatList(IView view, List<IView> flatList)
        {
            if (view is BindableObject bindable && (int) bindable.GetValue(_disconnectPolicyProperty) == 1)
            {
                return;
            }

            flatList.Add(view);

            if (view is IVisualTreeElement vte)
            {
                foreach (var child in vte.GetVisualChildren())
                {
                    if (child is IView childView)
                    {
                        BuildFlatList(childView, flatList);
                    }
                }
            }
        }
#else
        view.DisconnectHandlers();
#endif
    }
}
