using AndroidX.RecyclerView.Widget;
using AView = Android.Views.View;

namespace Nalu;

#pragma warning disable IDE0060
// ReSharper disable UnusedParameter.Local

/// <summary>
/// Handler for the <see cref="VirtualScroll" /> view on Android.
/// </summary>
public partial class VirtualScrollHandler
{
    /// <inheritdoc />
    protected override AView CreatePlatformView() => new RecyclerView(Context);

    /// <summary>
    /// Maps the adapter property from the virtual scroll to the platform recycler view.
    /// </summary>
    public static void MapAdapter(VirtualScrollHandler handler, IVirtualScroll virtualScroll)
    {
    }

    /// <summary>
    /// Maps the layout property from the virtual scroll to the platform recycler view.
    /// </summary>
    public static void MapLayout(VirtualScrollHandler handler, IVirtualScroll virtualScroll)
    {
    }

    /// <summary>
    /// Maps the item template property from the virtual scroll to the platform recycler view.
    /// </summary>
    public static void MapItemTemplate(VirtualScrollHandler handler, IVirtualScroll virtualScroll)
    {
    }

    /// <summary>
    /// Maps the section header template property from the virtual scroll to the platform recycler view.
    /// </summary>
    public static void MapSectionHeaderTemplate(VirtualScrollHandler handler, IVirtualScroll virtualScroll)
    {
    }

    /// <summary>
    /// Maps the section footer template property from the virtual scroll to the platform recycler view.
    /// </summary>
    public static void MapSectionFooterTemplate(VirtualScrollHandler handler, IVirtualScroll virtualScroll)
    {
    }

    /// <summary>
    /// Maps the header property from the virtual scroll to the platform recycler view.
    /// </summary>
    public static void MapHeader(VirtualScrollHandler handler, IVirtualScroll virtualScroll)
    {
    }

    /// <summary>
    /// Maps the footer property from the virtual scroll to the platform recycler view.
    /// </summary>
    public static void MapFooter(VirtualScrollHandler handler, IVirtualScroll virtualScroll)
    {
    }
}
