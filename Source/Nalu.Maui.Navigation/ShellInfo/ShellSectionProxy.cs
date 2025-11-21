using System.Collections.Specialized;
using System.ComponentModel;
using System.Text;

namespace Nalu;

#pragma warning disable CS8618

internal sealed class ShellSectionProxy : IShellSectionProxy, IDisposable
{
    private readonly ShellSection _section;
    private readonly List<ShellContentProxy> _contents;
    private IShellContentProxy? _currentContent;
    public string SegmentName { get; }

    public IShellContentProxy CurrentContent => _currentContent ?? throw new InvalidOperationException($"Section '{_section.Route}' has no current content. This can happen if the section has no (visible) items.");

    public IReadOnlyList<IShellContentProxy> Contents => _contents;
    public IShellItemProxy Parent { get; init; }

    public ShellSectionProxy(ShellSection section, IShellItemProxy parent)
    {
        _section = section;

        Parent = parent;
        SegmentName = section.Route;
        _contents = section.Items.Select(i => new ShellContentProxy(i, this)).ToList();
        UpdateCurrentContent();

        section.PropertyChanged += SectionOnPropertyChanged;

        if (section.Items is INotifyCollectionChanged observableCollection)
        {
            observableCollection.CollectionChanged += OnItemsCollectionChanged;
        }
    }

    private void OnItemsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        ShellProxyHelper.UpdateProxyItemsCollection<ShellContent, ShellContentProxy>(e, _contents, item => new ShellContentProxy(item, this));
        UpdateCurrentContent();
    }

    public IEnumerable<NavigationStackPage> GetNavigationStack(IShellContentProxy? content = null)
    {
        content ??= CurrentContent;

        if (content.Page is null)
        {
            yield break;
        }

        var baseRoute = $"//{Parent.SegmentName}/{SegmentName}/{content.SegmentName}";

        yield return new NavigationStackPage(baseRoute, content.SegmentName, content.Page, false);

        if (content != CurrentContent)
        {
            yield break;
        }

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

    public void RemoveStackPages(int count = -1)
    {
        var navigation = _section.Navigation;

        if (count < 0)
        {
            count = navigation.NavigationStack.Count - 1;
        }

        while (count-- > 0)
        {
            var pageToRemove = navigation.NavigationStack[^1];
            MarkPageForRemoval(pageToRemove);
            navigation.RemovePage(pageToRemove);
        }
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
        // When the section is empty or has no visible items, we cannot determine the current content.
        var currentSegmentName = (_section.CurrentItem ?? _section.Items.FirstOrDefault())?.Route;

        if (currentSegmentName is not null)
        {
            _currentContent = Contents.FirstOrDefault(c => c.SegmentName == currentSegmentName);
        }
    }

    public static bool IsPageMarkedForRemoval(Page page) => (bool) page.GetValue(_navigationRemovalProperty);
    private static void MarkPageForRemoval(Page page) => page.SetValue(_navigationRemovalProperty, true);

    private static readonly BindableProperty _navigationRemovalProperty = BindableProperty.CreateAttached(
        "PageNavigationRemoval",
        typeof(bool),
        typeof(ShellSectionProxy),
        false
    );
}
