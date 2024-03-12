namespace Nalu.Maui.Sample;

using Microsoft.Maui.Controls.Handlers.Compatibility;
using Microsoft.Maui.Controls.Platform.Compatibility;
using Toolbar = AndroidX.AppCompat.Widget.Toolbar;

public class PatchedShellRenderer : ShellRenderer
{
    protected override IShellToolbarTracker CreateTrackerForToolbar(Toolbar toolbar)
    {
        // https://github.com/dotnet/maui/issues/7045
        var shellToolbarTracker = base.CreateTrackerForToolbar(toolbar);
        shellToolbarTracker.TintColor = Colors.Yellow;
        return shellToolbarTracker;
    }
}
