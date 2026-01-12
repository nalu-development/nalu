using System.Linq;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Platform;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace Nalu;

/// <summary>
/// Container for virtualized elements in Windows ItemsRepeater.
/// </summary>
internal class VirtualScrollElementContainer : ContentControl
{
    public VirtualScrollElementContainer(IMauiContext context, string reuseId, IVirtualScroll virtualScroll)
        : base()
    {
        MauiContext = context;
        ReuseId = reuseId;
        VirtualScroll = virtualScroll;

        var orientation = virtualScroll.ItemsLayout is LinearVirtualScrollLayout linearLayout
            ? linearLayout.Orientation
            : ItemsLayoutOrientation.Vertical;

        if (orientation == ItemsLayoutOrientation.Vertical)
        {
            HorizontalContentAlignment = UI.Xaml.HorizontalAlignment.Stretch;
            HorizontalAlignment = UI.Xaml.HorizontalAlignment.Stretch;
        }
        else
        {
            VerticalContentAlignment = UI.Xaml.VerticalAlignment.Stretch;
            VerticalAlignment = UI.Xaml.VerticalAlignment.Stretch;
        }
    }

    public readonly string ReuseId;
    public readonly IMauiContext MauiContext;
    public readonly IVirtualScroll VirtualScroll;

    public VirtualScrollFlattenedItem? FlattenedItem { get; private set; }
    public int FlattenedIndex { get; private set; } = -1;

    private bool _isRecycled;

    internal bool IsRecycled
    {
        get => _isRecycled;
        set => _isRecycled = value;
    }

    public bool NeedsView => VirtualView is null || VirtualView.Handler is null;

    public IView? VirtualView { get; private set; }

    public void SetupView(IView view)
    {
        if (VirtualView is null || VirtualView.Handler is null)
        {
            Content = view.ToPlatform(MauiContext);
            VirtualView = view;
        }
    }

    public void UpdateItem(VirtualScrollFlattenedItem item, int flattenedIndex)
    {
        FlattenedItem = item;
        FlattenedIndex = flattenedIndex;
    }

    protected override IEnumerable<DependencyObject> GetChildrenInTabFocusOrder()
    {
        if (IsRecycled)
            return Enumerable.Empty<DependencyObject>();
        else
            return base.GetChildrenInTabFocusOrder();
    }
}
