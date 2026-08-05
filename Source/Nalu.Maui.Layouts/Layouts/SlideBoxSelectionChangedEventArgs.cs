namespace Nalu;

/// <summary>Orientation of the <see cref="SlideBox" /> sliding axis.</summary>
public enum SlideBoxOrientation
{
    /// <summary>Slides sit side by side and slide horizontally.</summary>
    Horizontal,

    /// <summary>Slides are stacked and slide vertically.</summary>
    Vertical
}

/// <summary>Event data for <see cref="SlideBox.SelectedIndexChanged" />.</summary>
public sealed class SlideBoxSelectionChangedEventArgs(int oldIndex, int newIndex, SlideBoxItem? oldItem, SlideBoxItem? newItem) : EventArgs
{
    /// <summary>Gets the previously selected index (-1 when none).</summary>
    public int OldIndex { get; } = oldIndex;

    /// <summary>Gets the newly selected index (-1 when none).</summary>
    public int NewIndex { get; } = newIndex;

    /// <summary>Gets the previously selected item.</summary>
    public SlideBoxItem? OldItem { get; } = oldItem;

    /// <summary>Gets the newly selected item.</summary>
    public SlideBoxItem? NewItem { get; } = newItem;
}
