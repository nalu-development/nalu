#nullable enable
using Microsoft.Maui.Controls.Handlers.Compatibility;
using Microsoft.Maui.Controls.Platform.Compatibility;

namespace Nalu;

public class NaluShellRenderer : ShellRenderer
{
    protected override IShellItemRenderer CreateShellItemRenderer(ShellItem shellItem)
    {
        var renderer = new NaluShellItemRenderer(this);
        ((IShellItemRenderer)renderer).ShellItem = shellItem;
        return renderer;
    }
}
