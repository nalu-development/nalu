using System.Collections.Specialized;
using System.ComponentModel;

namespace Nalu;

#pragma warning disable CS8618

internal class ShellItemProxy : IShellItemProxy, IDisposable
{
    private readonly ShellItem _item;
    private readonly List<ShellSectionProxy> _sections = [];
    public string SegmentName { get; }
    public IShellSectionProxy CurrentSection { get; private set; }
    public IReadOnlyList<IShellSectionProxy> Sections => _sections;
    public IShellProxy Parent { get; }

    public ShellItemProxy(ShellItem item, IShellProxy parent)
    {
        _item = item;

        Parent = parent;
        SegmentName = item.Route;
        _sections = item.Items.Select(s => new ShellSectionProxy(s, this)).ToList();
        UpdateCurrentSection();

        item.PropertyChanged += ItemOnPropertyChanged;

        if (item.Items is INotifyCollectionChanged observableCollection)
        {
            observableCollection.CollectionChanged += OnItemsCollectionChanged;
        }
    }

    private void OnItemsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        ShellProxyHelper.UpdateProxyItemsCollection<ShellSection, ShellSectionProxy>(e, _sections, item => new ShellSectionProxy(item, this));
        UpdateCurrentSection();
    }

    public void Dispose()
    {
        foreach (var sectionInfo in Sections)
        {
            ((IDisposable) sectionInfo).Dispose();
        }

        _item.PropertyChanged -= ItemOnPropertyChanged;
    }

    private void ItemOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ShellItem.CurrentItem))
        {
            UpdateCurrentSection();
        }
    }

    private void UpdateCurrentSection()
    {
        var currentSegmentName = _item.CurrentItem.Route;
        CurrentSection = Sections.First(s => s.SegmentName == currentSegmentName);
    }
}
