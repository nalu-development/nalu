using Android.Views;
using AndroidX.RecyclerView.Widget;
using Microsoft.Maui.Platform;
using AView = Android.Views.View;

namespace Nalu;

internal class VirtualScrollRecyclerViewAdapter : RecyclerView.Adapter
{
    private readonly IMauiContext _mauiContext;
    private readonly IVirtualScroll _virtualScroll;
    private readonly IVirtualScrollFlattenedAdapter _adapter;
    private readonly VirtualScrollPlatformReuseIdManager _reuseIdManager;

    public VirtualScrollRecyclerViewAdapter(IMauiContext mauiContext, IVirtualScroll virtualScroll, IVirtualScrollFlattenedAdapter adapter)
    {
        _mauiContext = mauiContext;
        _virtualScroll = virtualScroll;
        _adapter = adapter;
        _reuseIdManager = new VirtualScrollPlatformReuseIdManager();

        HasStableIds = false;
    }
    
    public override long GetItemId(int position) => RecyclerView.NoId;

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
        {
            _adapter.Dispose();
        }
    }

    public override int GetItemViewType(int position)
    {
        var item = _adapter.GetItem(position);
        var template = item.Type switch
        {
            VirtualScrollFlattenedPositionType.Item => _virtualScroll.GetItemTemplate(item.Value),
            VirtualScrollFlattenedPositionType.SectionHeader => _virtualScroll.GetSectionHeaderTemplate(item.Value),
            VirtualScrollFlattenedPositionType.SectionFooter => _virtualScroll.GetSectionFooterTemplate(item.Value),
            VirtualScrollFlattenedPositionType.GlobalHeader => _virtualScroll.GetGlobalHeaderTemplate(),
            VirtualScrollFlattenedPositionType.GlobalFooter => _virtualScroll.GetGlobalFooterTemplate(),
            _ => throw new NotSupportedException($"Item type {item.Type} is not supported.")
        };

        var reuseId = template is null ? _reuseIdManager.DefaultReuseId : _reuseIdManager.GetReuseId(template, item.Type.ToString());

        return reuseId;
    }

    public override void OnBindViewHolder(RecyclerView.ViewHolder holder, int position)
    {
        var item = _adapter.GetItem(position);

        if (holder is VirtualScrollViewHolder { ViewWrapper.VirtualView: BindableObject bindable })
        {
            var itemValue = item.Type is VirtualScrollFlattenedPositionType.GlobalFooter or VirtualScrollFlattenedPositionType.GlobalHeader
                ? (_virtualScroll as BindableObject)?.BindingContext
                : item.Value;
            bindable.BindingContext = itemValue;
        }
    }

    public override RecyclerView.ViewHolder OnCreateViewHolder(ViewGroup parent, int viewType)
    {
        var template = _reuseIdManager.GetTemplateById(viewType);
        var view = (IView)template.CreateContent();
        if (_virtualScroll is Element virtualScrollElement && view is Element { Parent: null } viewElement)
        {
            virtualScrollElement.AddLogicalChild(viewElement);
        }

        var platformView = view.ToPlatform(_mauiContext);
        var recyclerView = (VirtualScrollRecyclerView) parent;
        var wrapperPlatformView = CreateViewHolderViewWrapper(recyclerView, view, platformView);
        var holder = new VirtualScrollViewHolder(wrapperPlatformView);
        
        return holder;
    }

    private static VirtualScrollViewWrapper CreateViewHolderViewWrapper(VirtualScrollRecyclerView recyclerView, IView view, AView platformView)
    {
        var wrapperPlatformView = new VirtualScrollViewWrapper(recyclerView.Context!);
        wrapperPlatformView.VirtualView = view;
        wrapperPlatformView.AddView(platformView);
        wrapperPlatformView.Id = AView.GenerateViewId();
        wrapperPlatformView.LayoutParameters = recyclerView.Orientation == ItemsLayoutOrientation.Vertical
            ? new ViewGroup.LayoutParams(
                ViewGroup.LayoutParams.MatchParent,
                ViewGroup.LayoutParams.WrapContent
            )
            : new ViewGroup.LayoutParams(
                ViewGroup.LayoutParams.WrapContent,
                ViewGroup.LayoutParams.MatchParent
            );
        return wrapperPlatformView;
    }

    public override int ItemCount => _adapter.GetItemCount();
}
