namespace Nalu;

using System.ComponentModel;

#pragma warning disable CS8618

internal class ShellItemProxy : IShellItemProxy, IDisposable
{
    private readonly ShellItem _item;
    public string SegmentName { get; }
    public IShellSectionProxy CurrentSection { get; private set; }
    public IReadOnlyList<IShellSectionProxy> Sections { get; }
    public IShellProxy Parent { get; }

    public ShellItemProxy(ShellItem item, IShellProxy parent)
    {
        _item = item;

        Parent = parent;
        SegmentName = item.Route;
        Sections = item.Items.Select(s => new ShellSectionProxy(s, this)).ToList();
        UpdateCurrentSection();
        item.PropertyChanged += ItemOnPropertyChanged;
    }

    public void Dispose()
    {
        foreach (var sectionInfo in Sections)
        {
            ((IDisposable)sectionInfo).Dispose();
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
