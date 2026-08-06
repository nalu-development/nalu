using System.Collections.ObjectModel;

namespace Nalu;

/// <summary>
/// Collection keeping its items wired as logical children of the owning element,
/// so BindingContext and resource resolution flow through the scaffold structure.
/// </summary>
internal sealed class ScaffoldElementCollection<T>(Element owner) : ObservableCollection<T>
    where T : Element
{
    protected override void InsertItem(int index, T item)
    {
        base.InsertItem(index, item);
        owner.AddLogicalChild(item);
    }

    protected override void RemoveItem(int index)
    {
        var item = this[index];
        base.RemoveItem(index);
        owner.RemoveLogicalChild(item);
    }

    protected override void SetItem(int index, T item)
    {
        var oldItem = this[index];
        base.SetItem(index, item);
        owner.RemoveLogicalChild(oldItem);
        owner.AddLogicalChild(item);
    }

    protected override void ClearItems()
    {
        foreach (var item in this)
        {
            owner.RemoveLogicalChild(item);
        }

        base.ClearItems();
    }
}
