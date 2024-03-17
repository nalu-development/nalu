namespace Nalu;

using System.ComponentModel;
using System.Text;

#pragma warning disable CS8618

internal sealed class ShellSectionProxy : IShellSectionProxy, IDisposable
{
    private readonly ShellSection _section;
    public string SegmentName { get; }
    public IShellContentProxy CurrentContent { get; private set; }
    public IReadOnlyList<IShellContentProxy> Contents { get; init; }
    public IShellItemProxy Parent { get; init; }

    public ShellSectionProxy(ShellSection section, IShellItemProxy parent)
    {
        _section = section;

        Parent = parent;
        SegmentName = section.Route;
        Contents = section.Items.Select(i => new ShellContentProxy(i, this)).ToList();
        UpdateCurrentContent();
        section.PropertyChanged += SectionOnPropertyChanged;
    }

    public IEnumerable<NavigationStackPage> GetNavigationStack()
    {
        var content = CurrentContent;
        if (content.Page is null)
        {
            yield break;
        }

        var baseRoute = $"//{Parent.SegmentName}/{SegmentName}/{content.SegmentName}";
        yield return new NavigationStackPage(baseRoute, content.SegmentName, content.Page, false);

        var navigation = _section.Navigation;
        var route = new StringBuilder(baseRoute);
        foreach (var stackPage in navigation.NavigationStack)
        {
            if (stackPage is not null)
            {
                var segmentName = NavigationSegmentAttribute.GetSegmentName(stackPage.GetType());
                route.Append('/');
                route.Append(segmentName);
                yield return new NavigationStackPage(route.ToString(), segmentName, stackPage, false);
            }
        }

        foreach (var stackPage in navigation.ModalStack)
        {
            if (stackPage is not null)
            {
                var segmentName = NavigationSegmentAttribute.GetSegmentName(stackPage.GetType());
                route.Append('/');
                route.Append(segmentName);
                yield return new NavigationStackPage(route.ToString(), segmentName, stackPage, true);
            }
        }
    }

    public Task PopAsync()
    {
        var navigation = _section.Navigation;
        if (navigation.ModalStack.Count > 0)
        {
            return navigation.PopModalAsync(true);
        }

        var item = (ShellItem)_section.Parent;
        var shell = (Shell)item.Parent;
        var animated = shell.CurrentItem == item && item.CurrentItem == _section;
        return navigation.PopAsync(animated);
    }

    public void Dispose() => _section.PropertyChanged -= SectionOnPropertyChanged;

    private void SectionOnPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(ShellSection.CurrentItem))
        {
            UpdateCurrentContent();
        }
    }

    private void UpdateCurrentContent()
    {
        var currentSegmentName = _section.CurrentItem.Route;
        CurrentContent = Contents.First(c => c.SegmentName == currentSegmentName);
    }
}
