using Microsoft.Maui.Handlers;

#if IOS || MACCATALYST
using PlatformView = UIKit.UIView;
#elif ANDROID
using PlatformView = Android.Views.View;
#else
using PlatformView = object;
#endif

namespace Nalu;


/// <summary>
/// Handler for the <see cref="VirtualScroll" /> view.
/// </summary>
public partial class VirtualScrollHandler : ViewHandler<IVirtualScroll, PlatformView>
{
    /// <summary>
    /// The property mapper for the <see cref="IVirtualScroll" /> interface.
    /// </summary>
    public static readonly IPropertyMapper<IVirtualScroll, VirtualScrollHandler> Mapper =
        new PropertyMapper<IVirtualScroll, VirtualScrollHandler>(ViewMapper)
        {
#if IOS || MACCATALYST || ANDROID
            [nameof(IVirtualScroll.Adapter)] = MapAdapter,
            [nameof(IVirtualScroll.ItemsLayout)] = MapLayout,
            [nameof(IVirtualScroll.ItemTemplate)] = MapItemTemplate,
            [nameof(IVirtualScroll.SectionHeaderTemplate)] = MapSectionHeaderTemplate,
            [nameof(IVirtualScroll.SectionFooterTemplate)] = MapSectionFooterTemplate,
            [nameof(IVirtualScroll.HeaderTemplate)] = MapHeaderTemplate,
            [nameof(IVirtualScroll.FooterTemplate)] = MapFooterTemplate,
            [nameof(IVirtualScroll.IsRefreshEnabled)] = MapIsRefreshEnabled,
            [nameof(IVirtualScroll.RefreshAccentColor)] = MapRefreshAccentColor,
            [nameof(IVirtualScroll.IsRefreshing)] = MapIsRefreshing,
#endif
        };

    /// <summary>
    /// The command mapper for the <see cref="IVirtualScroll" /> interface.
    /// </summary>
    public static readonly CommandMapper<IVirtualScroll, VirtualScrollHandler> CommandMapper =
        new(ViewCommandMapper)
        {
#if IOS || MACCATALYST || ANDROID
            [nameof(IVirtualScroll.ScrollTo)] = MapScrollTo,
#endif
        };

#if !(IOS || MACCATALYST || ANDROID)
    /// <inheritdoc />
    protected override PlatformView CreatePlatformView() => throw new NotImplementedException();
#endif

    /// <summary>
    /// A flag to skip the layout mapper during initial setup.
    /// </summary>
    protected bool IsConnecting { get; private set; } = true;

    /// <summary>
    /// Initializes a new instance of the <see cref="VirtualScrollHandler" /> class.
    /// </summary>
    public VirtualScrollHandler()
        : base(Mapper, CommandMapper)
    {
    }

    /// <inheritdoc />
    public override void SetVirtualView(IView view)
    {
        IsConnecting = true;
        base.SetVirtualView(view);
        IsConnecting = false;
    }
}


