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
        new PropertyMapper<IVirtualScroll, VirtualScrollHandler>(ViewHandler.ViewMapper)
        {
            [nameof(IVirtualScroll.Adapter)] = MapAdapter,
            [nameof(IVirtualScroll.ItemsLayout)] = MapLayout,
            [nameof(IVirtualScroll.ItemTemplate)] = MapItemTemplate,
            [nameof(IVirtualScroll.SectionHeaderTemplate)] = MapSectionHeaderTemplate,
            [nameof(IVirtualScroll.SectionFooterTemplate)] = MapSectionFooterTemplate,
            [nameof(IVirtualScroll.Header)] = MapHeader,
            [nameof(IVirtualScroll.Footer)] = MapFooter,
        };

    /// <summary>
    /// A flag to skip the layout mapper during initial setup.
    /// </summary>
    protected bool IsConnecting { get; private set; } = true;

    /// <summary>
    /// Initializes a new instance of the <see cref="VirtualScrollHandler" /> class.
    /// </summary>
    public VirtualScrollHandler()
        : base(Mapper)
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

