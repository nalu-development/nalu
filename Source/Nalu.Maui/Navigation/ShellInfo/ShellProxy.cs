namespace Nalu;

using System.ComponentModel;

internal class ShellProxy : IShellProxy, IDisposable
{
    private readonly NaluShell _shell;
    public IShellItemProxy CurrentItem { get; private set; } = null!;
    public IReadOnlyList<IShellItemProxy> Items { get; private set; } = null!;
    private Dictionary<string, IShellContentProxy> _contentsBySegmentName = null!;

    public ShellProxy(NaluShell shell)
    {
        _shell = shell;

        UpdateItems();
        shell.PropertyChanged += ShellOnPropertyChanged;
        ((IShellController)shell).StructureChanged += ShellOnStructureChanged;
    }

    public IShellContentProxy GetContent(string segmentName) => _contentsBySegmentName[segmentName];

    public Color GetForegroundColor(Page page) => Shell.GetForegroundColor(page.IsSet(Shell.ForegroundColorProperty) ? page : _shell);

    public async Task PushAsync(string segmentName, Page page)
    {
        try
        {
            _shell.SetIsNavigating(true);

            var baseRoute = _shell.CurrentState.Location.OriginalString;
            var finalRoute = $"{baseRoute}/{segmentName}";
            Routing.UnRegisterRoute(finalRoute);
            Routing.RegisterRoute(finalRoute, new FixedRouteFactory(page));
            await _shell.GoToAsync(finalRoute).ConfigureAwait(true);
        }
        finally
        {
            _shell.SetIsNavigating(false);
        }
    }

    public async Task PopAsync(IShellSectionProxy? section = null)
    {
        try
        {
            _shell.SetIsNavigating(true);
            section ??= CurrentItem.CurrentSection;
            await section.PopAsync().ConfigureAwait(true);
        }
        finally
        {
            _shell.SetIsNavigating(false);
        }
    }

    public async Task SelectContentAsync(string segmentName)
    {
        try
        {
            _shell.SetIsNavigating(true);
            var contentProxy = (ShellContentProxy)GetContent(segmentName);
            var content = contentProxy.Content;
            var section = (ShellSection)content.Parent;
            var item = (ShellItem)section.Parent;
            var navigated = false;

            if (section.CurrentItem != content)
            {
                section.CurrentItem = content;
                navigated = true;
            }

            if (item.CurrentItem != section)
            {
                item.CurrentItem = section;
                navigated = true;
            }

            if (_shell.CurrentItem != item)
            {
                _shell.CurrentItem = item;
                navigated = true;
            }

            if (navigated)
            {
                await Task.Delay(200).ConfigureAwait(true);
            }
        }
        finally
        {
            _shell.SetIsNavigating(false);
        }
    }

    public void Dispose()
    {
        foreach (var itemInfo in Items)
        {
            ((IDisposable)itemInfo).Dispose();
        }

        _shell.PropertyChanged -= ShellOnPropertyChanged;
        ((IShellController)_shell).StructureChanged -= ShellOnStructureChanged;
    }

    private void ShellOnStructureChanged(object? sender, EventArgs e) => UpdateItems();

    private void ShellOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(Shell.CurrentItem))
        {
            UpdateCurrentItem();
        }
    }

    private void UpdateItems()
    {
        if (Items is { } items)
        {
            foreach (var itemInfo in items)
            {
                ((IDisposable)itemInfo).Dispose();
            }
        }

        Items = _shell.Items.Select(i => new ShellItemProxy(i, this)).ToList();
        _contentsBySegmentName = Items
            .SelectMany(i => i.Sections)
            .SelectMany(s => s.Contents)
            .ToDictionary(c => c.SegmentName);
        UpdateCurrentItem();
    }

    private void UpdateCurrentItem()
    {
        var currentSegmentName = _shell.CurrentItem.Route;
        CurrentItem = Items.First(i => i.SegmentName == currentSegmentName);
    }
}
