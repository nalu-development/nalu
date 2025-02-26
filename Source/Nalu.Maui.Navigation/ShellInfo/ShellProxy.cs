using System.Collections.Specialized;
using System.ComponentModel;
using System.Text.RegularExpressions;

namespace Nalu;

internal partial class ShellProxy : IShellProxy, IDisposable
{
    private readonly NaluShell _shell;
    private readonly ShellRouteFactory _routeFactory = new();
    private readonly HashSet<string> _registeredSegments = [];
    private readonly List<ShellItemProxy> _items;

    private Dictionary<string, IShellContentProxy> _contentsBySegmentName = null!;
    private string? _navigationTarget;
    private bool _contentChanged;
    private IShellSectionProxy? _navigationCurrentSection;

    public IShellItemProxy CurrentItem { get; private set; } = null!;
    public IReadOnlyList<IShellItemProxy> Items => _items;
    public string OriginalState => _shell.CurrentState.Location.OriginalString;
    public string State => "//" + string.Join("/", CurrentItem.CurrentSection.GetNavigationStack().Select(p => p.SegmentName));

    public ShellProxy(NaluShell shell)
    {
        _shell = shell;
        _items = shell.Items.Select(i => new ShellItemProxy(i, this)).ToList();
        ShellOnStructureChanged(shell, EventArgs.Empty);
        UpdateCurrentItem();

        ((IShellController) shell).StructureChanged += ShellOnStructureChanged;
        shell.PropertyChanged += ShellOnPropertyChanged;

        if (shell.Items is INotifyCollectionChanged observableCollection)
        {
            observableCollection.CollectionChanged += OnItemsCollectionChanged;
        }
    }

    private void ShellOnStructureChanged(object? sender, EventArgs e)
        => _contentsBySegmentName = Items
                                    .SelectMany(i => i.Sections)
                                    .SelectMany(s => s.Contents)
                                    .ToDictionary(c => c.SegmentName);

    private void OnItemsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        ShellProxyHelper.UpdateProxyItemsCollection<ShellItem, ShellItemProxy>(e, _items, item => new ShellItemProxy(item, this));
        UpdateCurrentItem();
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

    public bool ProposeNavigation(INavigationInfo navigation)
    {
        var args = new NaluShellNavigatingEventArgs { Navigation = navigation };
        _shell.SendOnNavigating(args);

        return !args.Canceled;
    }

    public async Task CommitNavigationAsync(Action? completeAction = null)
    {
        if (_navigationTarget is not { } targetState || targetState == _shell.CurrentState.Location.OriginalString)
        {
            completeAction?.Invoke();

            return;
        }

        var contentChanged = _contentChanged;
        _navigationTarget = null;
        _contentChanged = false;
        _navigationCurrentSection = null;

        var currentState = _shell.CurrentState.Location.OriginalString;
        var currentContentState = TrimRouteToContent(currentState);
        var targetContentState = TrimRouteToContent(targetState);

        if (targetContentState.StartsWith(currentContentState))
        {
            if (targetContentState.Length != currentContentState.Length)
            {
                var commonPathLength = currentContentState.Length + 1; // includes path separator which we don't want
                targetState = targetContentState[commonPathLength..];
            }

            // else: do nothing, we're already at the target state
        }
        else if (currentContentState.StartsWith(targetContentState))
        {
            var popCount = currentContentState[targetContentState.Length..].Count(c => c == '/');
            targetState = string.Concat(Enumerable.Repeat("../", popCount));
        }

        await _shell.GoToAsync(targetState + "?nalu", true).ConfigureAwait(true);

        await Task.Yield();

        if (contentChanged)
        {
            // Wait for the animation to complete
            // I know this is a hack, but I don't see any other way to do this
            // given `shell.GoToAsync` does not wait for the animation to complete
            await Task.Delay(500).ConfigureAwait(true);
        }

        completeAction?.Invoke();
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

    public Color GetToolbarIconColor(Page page) =>
        Shell.GetTitleColor(page.IsSet(Shell.TitleColorProperty) ? page : _shell);

    public Task PushAsync(string segmentName, Page page)
    {
        if (_navigationTarget == null)
        {
            throw new NotSupportedException("PushAsync is not supported outside of a navigation batch");
        }

        var baseRoute = _navigationTarget ?? _shell.CurrentState.Location.OriginalString;
        var finalRoute = $"{baseRoute}/{segmentName}";

        var pageTypeRouteFactory = _routeFactory.GetRouteFactory(page.GetType());
        pageTypeRouteFactory.Push(page);

        if (!_registeredSegments.Contains(segmentName))
        {
            Routing.RegisterRoute(segmentName, pageTypeRouteFactory);
            _registeredSegments.Add(segmentName);
        }

        _navigationTarget = finalRoute;

        return Task.CompletedTask;
    }

    public Task PopAsync(IShellSectionProxy? section = null)
    {
        if (_navigationTarget == null)
        {
            throw new NotSupportedException("PopAsync is not supported outside of a navigation batch");
        }

        section ??= CurrentItem.CurrentSection;

        if (section != _navigationCurrentSection)
        {
            section.RemoveStackPages(1);

            return Task.CompletedTask;
        }

        var previousSegmentEnd = _navigationTarget.LastIndexOf('/');
        _navigationTarget = _navigationTarget[..previousSegmentEnd];

        return Task.CompletedTask;
    }

    public void SendNavigationLifecycleEvent(NavigationLifecycleEventArgs args) => _shell.SendNavigationLifecycleEvent(args);

    public Task SelectContentAsync(string segmentName)
    {
        if (_navigationTarget is null)
        {
            throw new NotSupportedException("SelectContentAsync is not supported outside of a navigation batch");
        }

        var contentProxy = (ShellContentProxy) GetContent(segmentName);

        if (CurrentItem.CurrentSection.CurrentContent == contentProxy)
        {
            return Task.CompletedTask;
        }

        _contentChanged = true;
        var content = contentProxy.Content;
        var section = (ShellSection) content.Parent;
        var item = (ShellItem) section.Parent;

        _navigationTarget = contentProxy.Parent.GetNavigationStack(contentProxy).LastOrDefault()?.Route
                            ?? $"//{item.Route}/{section.Route}/{content.Route}";

        return Task.CompletedTask;
    }

    public void InitializeWithContent(string segmentName)
    {
        var contentProxy = (ShellContentProxy) GetContent(segmentName);

        if (CurrentItem.CurrentSection.CurrentContent == contentProxy)
        {
            return;
        }

        _contentChanged = true;
        var content = contentProxy.Content;
        var section = (ShellSection) content.Parent;
        var item = (ShellItem) section.Parent;

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
    }

    public void Dispose()
    {
        foreach (var itemInfo in Items)
        {
            ((IDisposable) itemInfo).Dispose();
        }

        _shell.PropertyChanged -= ShellOnPropertyChanged;

        if (_shell.Items is INotifyCollectionChanged observableCollection)
        {
            observableCollection.CollectionChanged -= OnItemsCollectionChanged;
        }
    }

    private void ShellOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(Shell.CurrentItem))
        {
            UpdateCurrentItem();
        }
    }

    private void UpdateCurrentItem()
    {
        var currentSegmentName = _shell.CurrentItem.Route;
        CurrentItem = Items.First(i => i.SegmentName == currentSegmentName);
    }

    private static string TrimRouteToContent(string uri) => TrimRouteToContentRegex().Replace(uri, string.Empty);

    [GeneratedRegex("^//[^/]+/[^/]+/")]
    private static partial Regex TrimRouteToContentRegex();
}
