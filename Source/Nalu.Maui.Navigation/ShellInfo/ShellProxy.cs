namespace Nalu;

using System.ComponentModel;

internal class ShellProxy : IShellProxy, IDisposable
{
    private readonly NaluShell _shell;
    private readonly ShellRouteFactory _routeFactory = new();
    private readonly HashSet<string> _registeredSegments = [];

    public IShellItemProxy CurrentItem { get; private set; } = null!;
    public IReadOnlyList<IShellItemProxy> Items { get; private set; } = null!;
    private Dictionary<string, IShellContentProxy> _contentsBySegmentName = null!;
    private string? _navigationTarget;
    private bool _contentChanged;
    private IShellSectionProxy? _navigationCurrentSection;
    public string State => _shell.CurrentState.Location.OriginalString;

    public ShellProxy(NaluShell shell)
    {
        _shell = shell;

        UpdateItems();
        shell.PropertyChanged += ShellOnPropertyChanged;
        ((IShellController)shell).StructureChanged += ShellOnStructureChanged;
    }

    public bool BeginNavigation()
    {
        if (_navigationTarget is not null)
        {
            // Already inside a batch
            return false;
        }

        _navigationTarget = _shell.CurrentState.Location.OriginalString;
        _navigationCurrentSection = CurrentItem.CurrentSection;
        return true;
    }

    public async Task CommitNavigationAsync(Action? completeAction = null)
    {
        if (_navigationTarget is not { } targetState || targetState == _shell.CurrentState.Location.OriginalString)
        {
            if (completeAction is not null)
            {
                _shell.SetIsNavigating(true);
                completeAction();
                _shell.SetIsNavigating(false);
            }

            return;
        }

        try
        {
            _shell.SetIsNavigating(true);

            var contentChanged = _contentChanged;
            _navigationTarget = null;
            _contentChanged = false;
            _navigationCurrentSection = null;

            await _shell.GoToAsync(targetState, true).ConfigureAwait(true);
            await Task.Yield();

            if (contentChanged)
            {
                // Wait for the animation to complete
                // I know this is a hack, but I don't see any other way to do this
                // given `shell.GoToAsync` does not wait for the animation to complete
                await Task.Delay(600).ConfigureAwait(true);
            }

            completeAction?.Invoke();
        }
        finally
        {
            _shell.SetIsNavigating(false);
        }
    }

    public IShellContentProxy GetContent(string segmentName) => _contentsBySegmentName[segmentName];

    public IShellContentProxy FindContent(params string[] names)
    {
        var namesLength = names.Length;
        var name = names[0];
        for (var i = 0; i < namesLength; i++)
        {
            name = names[i];
            if (_contentsBySegmentName.TryGetValue(name, out var content))
            {
                return content;
            }
        }

        throw new KeyNotFoundException($"Could not find content with segment name '{name}'");
    }

    public Color GetToolbarIconColor(Page page) => Shell.GetTitleColor(page.IsSet(Shell.TitleColorProperty) ? page : _shell);

    public async Task PushAsync(string segmentName, Page page)
    {
        try
        {
            _shell.SetIsNavigating(true);

            var baseRoute = _navigationTarget ?? _shell.CurrentState.Location.OriginalString;
            var finalRoute = $"{baseRoute}/{segmentName}";

            var pageTypeRouteFactory = _routeFactory.GetRouteFactory(page.GetType());
            pageTypeRouteFactory.Push(page);

            if (!_registeredSegments.Contains(segmentName))
            {
                Routing.RegisterRoute(segmentName, pageTypeRouteFactory);
                _registeredSegments.Add(segmentName);
            }

            if (_navigationTarget != null)
            {
                _navigationTarget = finalRoute;
            }
            else
            {
                await _shell.GoToAsync(finalRoute).ConfigureAwait(true);
            }
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

            if (section == _navigationCurrentSection && _navigationTarget != null)
            {
                var previousSegmentEnd = _navigationTarget.LastIndexOf('/');
                _navigationTarget = _navigationTarget[..previousSegmentEnd];
            }
            else
            {
                await section.PopAsync().ConfigureAwait(true);
            }
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
            if (CurrentItem.CurrentSection.CurrentContent == contentProxy)
            {
                return;
            }

            _contentChanged = true;
            var content = contentProxy.Content;
            var section = (ShellSection)content.Parent;
            var item = (ShellItem)section.Parent;

            if (_navigationTarget is not null)
            {
                _navigationTarget = contentProxy.Parent.GetNavigationStack(contentProxy).LastOrDefault()?.Route
                                    ?? $"//{item.Route}/{section.Route}/{content.Route}";
                return;
            }

            if (section.CurrentItem != content)
            {
                section.CurrentItem = content;
            }

            if (item.CurrentItem != section)
            {
                item.CurrentItem = section;
            }

            if (_shell.CurrentItem != item)
            {
                _shell.CurrentItem = item;
            }

            await Task.Delay(250).ConfigureAwait(true);
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
