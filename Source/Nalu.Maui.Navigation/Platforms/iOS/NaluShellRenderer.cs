using Microsoft.Maui.Controls.Handlers.Compatibility;
using Microsoft.Maui.Controls.Platform.Compatibility;

namespace Nalu;

internal class NaluShellRenderer : ShellRenderer
{
    protected override IShellItemRenderer CreateShellItemRenderer(ShellItem item)
    {
        var renderer = new NaluShellItemRenderer(this)
                                    {
                                        ShellItem = item
                                    };
        renderer.UpdateTabBarView();

        return renderer;
    }
}
