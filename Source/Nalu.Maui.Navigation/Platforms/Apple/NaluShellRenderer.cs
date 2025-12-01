using Microsoft.Maui.Controls.Handlers.Compatibility;
using Microsoft.Maui.Controls.Platform.Compatibility;

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace Nalu;

public class NaluShellRenderer : ShellRenderer
{
    protected override IShellItemRenderer CreateShellItemRenderer(ShellItem item)
        => new NaluShellItemRenderer(this)
           {
               ShellItem = item
           };

    protected override IShellSectionRenderer CreateShellSectionRenderer(ShellSection shellSection)
        => new NaluShellSectionRenderer(this);
}
