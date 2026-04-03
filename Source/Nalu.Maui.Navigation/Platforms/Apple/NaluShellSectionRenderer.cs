using System.Runtime.CompilerServices;
using Microsoft.Maui.Controls.Internals;
using Microsoft.Maui.Controls.Platform.Compatibility;

namespace Nalu;

/// <inheritdoc/>
public class NaluShellSectionRenderer : ShellSectionRenderer
{
    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "PushPage")]
    private static extern void SectionPushPage(ShellSectionRenderer renderer, Page page, bool animated, TaskCompletionSource<bool> taskSource);
    
    /// <inheritdoc/>
    public NaluShellSectionRenderer(IShellContext context) : base(context)
    {
    }

    /// <inheritdoc/>
    public NaluShellSectionRenderer(IShellContext context, Type navigationBarType, Type toolbarType) : base(context, navigationBarType, toolbarType)
    {
    }

    /// <inheritdoc/>
    protected override void OnPushRequested(NavigationRequestedEventArgs e)
    {
        var page = e.Page;
        var animated = e.Animated;

        var taskSource = new TaskCompletionSource<bool>();
        var parentTabBar = ParentViewController as NaluShellSectionWrapperController;
        var showsPresentation = ReferenceEquals(parentTabBar?.SelectedViewController, this);
        
        SectionPushPage(this, page, animated, taskSource);

        if (!showsPresentation)
        {
            taskSource.TrySetResult(true);
        }
    }
}
