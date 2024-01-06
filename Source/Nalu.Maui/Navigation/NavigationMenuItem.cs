namespace Nalu;

#pragma warning disable CS0659

/// <summary>
/// A shell item that can be used to navigate to a specific page with Nalu navigation.
/// </summary>
public class NavigationMenuItem : MenuItem
{
    /// <inheritdoc />
    protected override void OnParentSet()
    {
        base.OnParentSet();
        UpdateShellItemRoute();
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(string? propertyName = null)
    {
        base.OnPropertyChanged(propertyName);

        if (propertyName == nameof(CommandParameter))
        {
            UpdateShellItemRoute();
        }
    }

    private void UpdateShellItemRoute()
    {
        if (Parent is ShellItem shellItem)
        {
            if (CommandParameter is not AbsoluteNavigation absoluteNavigation)
            {
                throw new NotSupportedException("Only AbsoluteNavigation is supported on NavigationMenuItem.");
            }

            shellItem.Route = absoluteNavigation.Path;
        }
    }
}
