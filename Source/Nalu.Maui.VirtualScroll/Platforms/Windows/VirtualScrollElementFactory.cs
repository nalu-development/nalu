using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Nalu;

/// <summary>
/// Element factory for Windows ItemsRepeater that handles element creation and recycling.
/// </summary>
internal partial class VirtualScrollElementFactory : IElementFactory, IDisposable
{
    private readonly IMauiContext _mauiContext;
    private readonly IVirtualScroll _virtualScroll;
    private readonly VirtualScrollPlatformReuseIdManager _reuseIdManager;
    private readonly Dictionary<string, Queue<VirtualScrollElementContainer>> _recycledElements = [];
    private readonly VirtualScrollCellManager<VirtualScrollElementContainer> _cellManager = new(container => container.VirtualView);
    private readonly HashSet<VirtualScrollElementContainer> _liveContainers = new();

    public IReadOnlyCollection<VirtualScrollElementContainer> RealizedElements => _liveContainers;

    public VirtualScrollElementFactory(
        IMauiContext mauiContext,
        IVirtualScroll virtualScroll,
        VirtualScrollPlatformReuseIdManager reuseIdManager)
    {
        _mauiContext = mauiContext;
        _virtualScroll = virtualScroll;
        _reuseIdManager = reuseIdManager;
    }

    private VirtualScrollElementContainer? GetRecycledElement(string reuseId)
    {
        if (!_recycledElements.TryGetValue(reuseId, out var queue))
        {
            queue = new Queue<VirtualScrollElementContainer>();
            _recycledElements[reuseId] = queue;
        }

        return queue.TryDequeue(out var element) ? element : null;
    }

    private void AddRecycledElement(VirtualScrollElementContainer element)
    {
        var reuseId = element.ReuseId;
        
        if (!_recycledElements.TryGetValue(reuseId, out var queue))
        {
            queue = new Queue<VirtualScrollElementContainer>();
            _recycledElements[reuseId] = queue;
        }

        queue.Enqueue(element);
    }

    public UIElement GetElement(ElementFactoryGetArgs args)
    {
        if (args.Data is not VirtualScrollItemWrapper wrapper)
        {
            return new ContentControl(); // Return empty element for invalid data
        }

        var flattenedIndex = wrapper.FlattenedIndex;
        var item = wrapper.Item;
        var template = item.Type switch
        {
            VirtualScrollFlattenedPositionType.Item => _virtualScroll.GetItemTemplate(item.Value),
            VirtualScrollFlattenedPositionType.SectionHeader => _virtualScroll.GetSectionHeaderTemplate(item.Value),
            VirtualScrollFlattenedPositionType.SectionFooter => _virtualScroll.GetSectionFooterTemplate(item.Value),
            VirtualScrollFlattenedPositionType.GlobalHeader => _virtualScroll.GetGlobalHeaderTemplate(),
            VirtualScrollFlattenedPositionType.GlobalFooter => _virtualScroll.GetGlobalFooterTemplate(),
            _ => throw new NotSupportedException($"Item type {item.Type} is not supported.")
        };

        var reuseId = template is null
            ? _reuseIdManager.DefaultReuseId
            : _reuseIdManager.GetReuseId(template, item.Type.ToString());

        var container = GetRecycledElement(reuseId);
        if (container is null)
        {
            container = new VirtualScrollElementContainer(_mauiContext, reuseId, _virtualScroll);
            _cellManager.TrackCell(container);
        }

        if (container.NeedsView)
        {
            var view = template?.CreateContent() as IView ?? new ContentView();
            container.SetupView(view);
        }

        // Update binding context
        if (container.VirtualView is BindableObject bindable)
        {
            if (item.Type is VirtualScrollFlattenedPositionType.GlobalFooter or VirtualScrollFlattenedPositionType.GlobalHeader)
            {
                bindable.ClearValue(BindableObject.BindingContextProperty);
            }
            else
            {
                bindable.BindingContext = item.Value;
            }

            // Add as logical child if needed
            if (_virtualScroll is Element virtualScrollElement && bindable is Element { Parent: null } viewElement)
            {
                virtualScrollElement.AddLogicalChild(viewElement);
            }
        }

        container.UpdateItem(item, flattenedIndex);
        
        _liveContainers.Add(container);

        return container;
    }

    public void RecycleElement(ElementFactoryRecycleArgs args)
    {
        if (args.Element is VirtualScrollElementContainer container)
        {
            _liveContainers.Remove(container);
            container.IsRecycled = true;
            AddRecycledElement(container);
        }
    }

    public void Reset() => _recycledElements.Clear();

    public void Dispose()
    {
        _cellManager.Dispose();
        _recycledElements.Clear();
    }
}
