using Microsoft.Maui.Controls.Platform.Compatibility;

namespace Nalu;

/// <summary>
/// A custom ShellSectionRenderer that avoids updating the TabBarItem when a custom <see cref="NaluShell.TabBarViewProperty"/> is set.
/// </summary>
/// <param name="context"></param>
public class NaluShellSectionRenderer(IShellContext context) : ShellSectionRenderer(context)
{
    /// <inheritdoc/>
    protected override void UpdateTabBarItem()
    {
        if (NaluShell.GetTabBarView(ShellSection.Parent) is not null)
        {
            return;
        }
        
        base.UpdateTabBarItem();
    }
}
