using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Microsoft.Maui.Controls.Internals;

namespace Nalu;

/// <summary>
/// A scrollable view that virtualizes its content.
/// </summary>
/// <remarks>
/// This control is designed to replace traditional <see cref="CollectionView"/> control by providing a more efficient implementation tailored for Android and iOS platforms.
/// </remarks>
public class VirtualScroll : View, IVirtualScroll, IVirtualScrollLayoutInfo, IVirtualScrollController
{
    private bool _hasGlobalHeader;
    private bool _hasSectionHeader;
    private bool _hasGlobalFooter;
    private bool _hasSectionFooter;

    /// <summary>
    /// Bindable property for <see cref="ItemsSource"/>.
    /// </summary>
    public static readonly BindableProperty ItemsSourceProperty = BindableProperty.Create(
        nameof(ItemsSource),
        typeof(object),
        typeof(VirtualScroll),
        null,
        propertyChanged: (bindable, _, value) =>
        {
            var virtualScroll = (VirtualScroll)bindable;
            virtualScroll._adapter = CoerceAdapter(value);
            virtualScroll.Handler?.UpdateValue(nameof(Adapter));
        }
    );

    /// <summary>
    /// Bindable property for <see cref="DragHandler"/>.
    /// </summary>
    public static readonly BindableProperty DragHandlerProperty = BindableProperty.Create(
        nameof(DragHandler),
        typeof(IVirtualScrollDragHandler),
        typeof(VirtualScroll),
        null
    );

    /// <summary>
    /// Bindable property for <see cref="ItemTemplate"/>.
    /// </summary>
    public static readonly BindableProperty ItemTemplateProperty = BindableProperty.Create(
        nameof(ItemTemplate),
        typeof(DataTemplate),
        typeof(VirtualScroll),
        null
    );

    /// <summary>
    /// Bindable property for <see cref="SectionHeaderTemplate"/>.
    /// </summary>
    public static readonly BindableProperty SectionHeaderTemplateProperty = BindableProperty.Create(
        nameof(SectionHeaderTemplate),
        typeof(DataTemplate),
        typeof(VirtualScroll),
        null,
        propertyChanged: (bindable, _, newValue) => ((VirtualScroll)bindable)._hasSectionHeader = newValue is not null
    );

    /// <summary>
    /// Bindable property for <see cref="SectionFooterTemplate"/>.
    /// </summary>
    public static readonly BindableProperty SectionFooterTemplateProperty = BindableProperty.Create(
        nameof(SectionFooterTemplate),
        typeof(DataTemplate),
        typeof(VirtualScroll),
        null,
        propertyChanged: (bindable, _, newValue) => ((VirtualScroll)bindable)._hasSectionFooter = newValue is not null
    );

    /// <summary>
    /// Bindable property for <see cref="HeaderTemplate"/>.
    /// </summary>
    public static readonly BindableProperty HeaderTemplateProperty = BindableProperty.Create(
        nameof(HeaderTemplate),
        typeof(DataTemplate),
        typeof(VirtualScroll),
        null,
        propertyChanged: (bindable, _, newValue) =>
        {
            var virtualScroll = (VirtualScroll) bindable;
            virtualScroll.GlobalHeaderTemplate = (DataTemplate?)newValue;
            virtualScroll._hasGlobalHeader = newValue is not null;
        }
    );

    /// <summary>
    /// Bindable property for <see cref="FooterTemplate"/>.
    /// </summary>
    public static readonly BindableProperty FooterTemplateProperty = BindableProperty.Create(
        nameof(FooterTemplate),
        typeof(DataTemplate),
        typeof(VirtualScroll),
        null,
        propertyChanged: (bindable, _, newValue) =>
        {
            var virtualScroll = (VirtualScroll) bindable;
            virtualScroll.GlobalFooterTemplate = (DataTemplate?)newValue;
            virtualScroll._hasGlobalFooter = newValue is not null;
        }
    );

    /// <summary>
    /// Bindable property for <see cref="ItemsLayout"/>.
    /// </summary>
    public static readonly BindableProperty ItemsLayoutProperty = BindableProperty.Create(
        nameof(ItemsLayout),
        typeof(IVirtualScrollLayout),
        typeof(VirtualScroll),
        defaultValueCreator: _ => new VerticalVirtualScrollLayout()
    );

    /// <summary>
    /// Bindable property for <see cref="RefreshCommand"/>.
    /// </summary>
    public static readonly BindableProperty RefreshCommandProperty = BindableProperty.Create(
        nameof(RefreshCommand),
        typeof(ICommand),
        typeof(VirtualScroll),
        null
    );

    /// <summary>
    /// Bindable property for <see cref="IsRefreshEnabled"/>.
    /// </summary>
    public static readonly BindableProperty IsRefreshEnabledProperty = BindableProperty.Create(
        nameof(IsRefreshEnabled),
        typeof(bool),
        typeof(VirtualScroll),
        false
    );

    /// <summary>
    /// Bindable property for <see cref="RefreshAccentColor"/>.
    /// </summary>
    public static readonly BindableProperty RefreshAccentColorProperty = BindableProperty.Create(
        nameof(RefreshAccentColor),
        typeof(Color),
        typeof(VirtualScroll),
        null
    );

    /// <summary>
    /// Bindable property for <see cref="IsRefreshing"/>.
    /// </summary>
    public static readonly BindableProperty IsRefreshingProperty = BindableProperty.Create(
        nameof(IsRefreshing),
        typeof(bool),
        typeof(VirtualScroll),
        false,
        BindingMode.TwoWay);

    /// <summary>
    /// Bindable property for <see cref="FadingEdgeLength"/>.
    /// </summary>
    public static readonly BindableProperty FadingEdgeLengthProperty = BindableProperty.Create(
        nameof(FadingEdgeLength),
        typeof(double),
        typeof(VirtualScroll),
        0.0);

    /// <summary>
    /// Gets or sets the data source for the virtual scroll.
    /// </summary>
    /// <remarks>
    /// Providing an <see cref="IVirtualScrollAdapter"/> directly is recommended for optimal functionality,
    /// otherwise, common collection types such as <see cref="ObservableCollection{T}"/> or any <see cref="IEnumerable"/> will be automatically
    /// wrapped in a suitable adapter.
    /// </remarks>
    public object? ItemsSource
    {
        get => (object?)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }
    
    /// <summary>
    /// Gets or sets the drag handler for the virtual scroll.
    /// </summary>
    /// <remarks>
    /// Drag and drop is enabled only when a drag handler is provided.
    /// Adapter created via <see cref="CreateObservableCollectionAdapter{T}(ObservableCollection{T})"/> can be used as drag handler.
    /// It's also possible to customize its behavior by inheriting from <see cref="VirtualScrollNotifyCollectionChangedAdapter{TItemCollection}"/>. 
    /// </remarks>
    public IVirtualScrollDragHandler? DragHandler
    {
        get => (IVirtualScrollDragHandler?)GetValue(DragHandlerProperty);
        set => SetValue(DragHandlerProperty, value);
    }

    /// <summary>
    /// Gets the <see cref="ItemsSource"/> underlying adapter that provides data to the virtual scroll.
    /// </summary>
    public IVirtualScrollAdapter? Adapter => _adapter;

    /// <summary>
    /// Gets or sets the layout for the virtual scroll.
    /// </summary>
    public IVirtualScrollLayout ItemsLayout
    {
        get => (IVirtualScrollLayout)GetValue(ItemsLayoutProperty);
        set => SetValue(ItemsLayoutProperty, value);
    }

    /// <summary>
    /// Gets or sets the template used to display each item.
    /// </summary>
    public DataTemplate? ItemTemplate
    {
        get => (DataTemplate?)GetValue(ItemTemplateProperty);
        set => SetValue(ItemTemplateProperty, value);
    }

    /// <summary>
    /// Gets or sets the template used to display section headers.
    /// </summary>
    public DataTemplate? SectionHeaderTemplate
    {
        get => (DataTemplate?)GetValue(SectionHeaderTemplateProperty);
        set => SetValue(SectionHeaderTemplateProperty, value);
    }

    /// <summary>
    /// Gets or sets the template used to display section footers.
    /// </summary>
    public DataTemplate? SectionFooterTemplate
    {
        get => (DataTemplate?)GetValue(SectionFooterTemplateProperty);
        set => SetValue(SectionFooterTemplateProperty, value);
    }

    /// <summary>
    /// Gets or sets the template used to display the header at the top of the scroll view.
    /// </summary>
    public DataTemplate? HeaderTemplate
    {
        get => (DataTemplate?)GetValue(HeaderTemplateProperty);
        set => SetValue(HeaderTemplateProperty, value);
    }

    /// <summary>
    /// Gets or sets the template used to display the footer at the bottom of the scroll view.
    /// </summary>
    public DataTemplate? FooterTemplate
    {
        get => (DataTemplate?)GetValue(FooterTemplateProperty);
        set => SetValue(FooterTemplateProperty, value);
    }

    /// <summary>
    /// Gets or sets the command to execute when the user requests a refresh.
    /// </summary>
    public ICommand? RefreshCommand
    {
        get => (ICommand?)GetValue(RefreshCommandProperty);
        set => SetValue(RefreshCommandProperty, value);
    }

    /// <summary>
    /// Gets or sets a value indicating whether pull-to-refresh is enabled.
    /// </summary>
    public bool IsRefreshEnabled
    {
        get => (bool)GetValue(IsRefreshEnabledProperty);
        set => SetValue(IsRefreshEnabledProperty, value);
    }

    /// <summary>
    /// Gets or sets the accent color for the refresh indicator.
    /// </summary>
    public Color? RefreshAccentColor
    {
        get => (Color?)GetValue(RefreshAccentColorProperty);
        set => SetValue(RefreshAccentColorProperty, value);
    }

    /// <summary>
    /// Gets or sets a value indicating whether the refresh indicator is currently showing.
    /// Setting this to true programmatically will trigger the refresh indicator.
    /// Setting this to false will stop the refresh indicator.
    /// </summary>
    public bool IsRefreshing
    {
        get => (bool)GetValue(IsRefreshingProperty);
        set => SetValue(IsRefreshingProperty, value);
    }

    /// <summary>
    /// Gets or sets the length of the fading edge effect in device-independent units.
    /// </summary>
    /// <remarks>
    /// A value of 0 means no fading edge is applied (default).
    /// The orientation of the fading edge is determined by the layout orientation (horizontal or vertical).
    /// </remarks>
    public double FadingEdgeLength
    {
        get => (double)GetValue(FadingEdgeLengthProperty);
        set => SetValue(FadingEdgeLengthProperty, value);
    }

    /// <summary>
    /// Event raised when the user triggers a refresh.
    /// </summary>
    public event EventHandler<RefreshEventArgs>? OnRefresh;

    private int _onScrolledSubscriberCount;
    private IVirtualScrollAdapter? _adapter;

    /// <summary>
    /// Bindable property for <see cref="ScrolledCommand"/>.
    /// </summary>
    public static readonly BindableProperty ScrolledCommandProperty = BindableProperty.Create(
        nameof(ScrolledCommand),
        typeof(ICommand),
        typeof(VirtualScroll),
        null,
        propertyChanged: OnScrolledCommandChanged
    );

    /// <summary>
    /// Bindable property for <see cref="ScrollStartedCommand"/>.
    /// </summary>
    public static readonly BindableProperty ScrollStartedCommandProperty = BindableProperty.Create(
        nameof(ScrollStartedCommand),
        typeof(ICommand),
        typeof(VirtualScroll),
        null,
        propertyChanged: OnScrollStartedCommandChanged
    );

    /// <summary>
    /// Bindable property for <see cref="ScrollEndedCommand"/>.
    /// </summary>
    public static readonly BindableProperty ScrollEndedCommandProperty = BindableProperty.Create(
        nameof(ScrollEndedCommand),
        typeof(ICommand),
        typeof(VirtualScroll),
        null,
        propertyChanged: OnScrollEndedCommandChanged
    );

    private static void OnScrolledCommandChanged(BindableObject bindable, object? oldValue, object? newValue)
    {
        var virtualScroll = (VirtualScroll)bindable;
        virtualScroll.UpdateScrollEventSubscription();
    }

    private static void OnScrollStartedCommandChanged(BindableObject bindable, object? oldValue, object? newValue)
    {
        var virtualScroll = (VirtualScroll)bindable;
        virtualScroll.UpdateScrollEventSubscription();
    }

    private static void OnScrollEndedCommandChanged(BindableObject bindable, object? oldValue, object? newValue)
    {
        var virtualScroll = (VirtualScroll)bindable;
        virtualScroll.UpdateScrollEventSubscription();
    }

    /// <summary>
    /// Gets or sets the command to execute when the scroll position changes.
    /// </summary>
    public ICommand? ScrolledCommand
    {
        get => (ICommand?)GetValue(ScrolledCommandProperty);
        set => SetValue(ScrolledCommandProperty, value);
    }

    /// <summary>
    /// Gets or sets the command to execute when scrolling starts.
    /// </summary>
    public ICommand? ScrollStartedCommand
    {
        get => (ICommand?)GetValue(ScrollStartedCommandProperty);
        set => SetValue(ScrollStartedCommandProperty, value);
    }

    /// <summary>
    /// Gets or sets the command to execute when scrolling ends.
    /// </summary>
    public ICommand? ScrollEndedCommand
    {
        get => (ICommand?)GetValue(ScrollEndedCommandProperty);
        set => SetValue(ScrollEndedCommandProperty, value);
    }

    /// <summary>
    /// Event raised when the scroll position changes.
    /// </summary>
    public event EventHandler<VirtualScrollScrolledEventArgs>? OnScrolled
    {
        add
        {
            _onScrolledSubscriberCount++;
            UpdateScrollEventSubscription();
            // Use a private backing field to store the actual event handler
            OnScrolledEvent += value;
        }
        remove
        {
            OnScrolledEvent -= value;
            _onScrolledSubscriberCount--;
            UpdateScrollEventSubscription();
        }
    }

    private event EventHandler<VirtualScrollScrolledEventArgs>? OnScrolledEvent;

    private int _onScrollStartedSubscriberCount;

    /// <summary>
    /// Event raised when scrolling starts.
    /// </summary>
    public event EventHandler<VirtualScrollScrolledEventArgs>? OnScrollStarted
    {
        add
        {
            _onScrollStartedSubscriberCount++;
            UpdateScrollEventSubscription();
            OnScrollStartedEvent += value;
        }
        remove
        {
            OnScrollStartedEvent -= value;
            _onScrollStartedSubscriberCount--;
            UpdateScrollEventSubscription();
        }
    }

    private event EventHandler<VirtualScrollScrolledEventArgs>? OnScrollStartedEvent;

    private int _onScrollEndedSubscriberCount;

    /// <summary>
    /// Event raised when the scrolling ends.
    /// </summary>
    public event EventHandler<VirtualScrollScrolledEventArgs>? OnScrollEnded
    {
        add
        {
            _onScrollEndedSubscriberCount++;
            UpdateScrollEventSubscription();
            OnScrollEndedEvent += value;
        }
        remove
        {
            OnScrollEndedEvent -= value;
            _onScrollEndedSubscriberCount--;
            UpdateScrollEventSubscription();
        }
    }

    private event EventHandler<VirtualScrollScrolledEventArgs>? OnScrollEndedEvent;

    /// <summary>
    /// Internal event raised when a batch layout update completes.
    /// Used by layouts to update their state after items are added/removed/moved.
    /// </summary>
    internal event EventHandler? OnLayoutUpdateCompleted;

    private void UpdateScrollEventSubscription()
    {
        if (Handler is null)
        {
            return;
        }

        var hasSubscribers = ScrolledCommand != null || 
                            ScrollStartedCommand != null ||
                            ScrollEndedCommand != null || 
                            _onScrolledSubscriberCount > 0 ||
                            _onScrollStartedSubscriberCount > 0 ||
                            _onScrollEndedSubscriberCount > 0;
        Handler.Invoke("SetScrollEventEnabled", hasSubscribers);
    }

    /// <inheritdoc/>
    protected override void OnHandlerChanged()
    {
        base.OnHandlerChanged();
        // Update scroll event subscription when handler is connected
        UpdateScrollEventSubscription();
    }

    internal DataTemplate? GlobalHeaderTemplate { get; private set; }
    internal DataTemplate? GlobalFooterTemplate { get; private set; }

    /// <inheritdoc/>
    public DataTemplate? GetItemTemplate(object? item) => ItemTemplate.SelectDataTemplate(item, this);

    /// <inheritdoc/>
    public DataTemplate? GetSectionHeaderTemplate(object? section) => SectionHeaderTemplate.SelectDataTemplate(section, this);

    /// <inheritdoc/>
    public DataTemplate? GetSectionFooterTemplate(object? section) => SectionFooterTemplate.SelectDataTemplate(section, this);

    /// <inheritdoc/>
    public DataTemplate? GetGlobalHeaderTemplate() => GlobalHeaderTemplate;

    /// <inheritdoc/>
    public DataTemplate? GetGlobalFooterTemplate() => GlobalFooterTemplate;

    bool IVirtualScrollLayoutInfo.HasGlobalHeader => _hasGlobalHeader;

    bool IVirtualScrollLayoutInfo.HasGlobalFooter => _hasGlobalFooter;

    bool IVirtualScrollLayoutInfo.HasSectionHeader => _hasSectionHeader;

    bool IVirtualScrollLayoutInfo.HasSectionFooter => _hasSectionFooter;

    bool IEquatable<IVirtualScrollLayoutInfo>.Equals(IVirtualScrollLayoutInfo? other)
        => other is not null &&
           _hasGlobalHeader == other.HasGlobalHeader &&
           _hasGlobalFooter == other.HasGlobalFooter &&
           _hasSectionHeader == other.HasSectionHeader &&
           _hasSectionFooter == other.HasSectionFooter;

    /// <inheritdoc/>
    void IVirtualScrollController.Refresh(Action completionCallback)
    {
        // Set IsRefreshing to true to show the indicator
        IsRefreshing = true;
        
        // Wrap completion callback to also set IsRefreshing to false
        Action wrappedCompletion = () =>
        {
            IsRefreshing = false;
            completionCallback();
        };

        var handled = false;

        if (RefreshCommand is not null)
        {
            handled = true;

            if (RefreshCommand.CanExecute(null))
            {
                RefreshCommand.Execute(wrappedCompletion);
            }
        }

        if (OnRefresh is not null)
        {
            handled = true;
            OnRefresh.Invoke(this, new RefreshEventArgs(wrappedCompletion));
        }

        // If no one handled the refresh, just call the completion callback immediately
        if (!handled)
        {
            wrappedCompletion();
        }
    }

    /// <inheritdoc/>
    void IVirtualScrollController.Scrolled(double scrollX, double scrollY, double totalScrollableWidth, double totalScrollableHeight)
    {
        var args = new VirtualScrollScrolledEventArgs(scrollX, scrollY, totalScrollableWidth, totalScrollableHeight);

        if (ScrolledCommand != null && ScrolledCommand.CanExecute(args))
        {
            ScrolledCommand.Execute(args);
        }

        OnScrolledEvent?.Invoke(this, args);
    }

    void IVirtualScrollController.ScrollStarted(double scrollX, double scrollY, double totalScrollableWidth, double totalScrollableHeight)
    {
        var args = new VirtualScrollScrolledEventArgs(scrollX, scrollY, totalScrollableWidth, totalScrollableHeight);

        if (ScrollStartedCommand != null && ScrollStartedCommand.CanExecute(args))
        {
            ScrollStartedCommand.Execute(args);
        }

        OnScrollStartedEvent?.Invoke(this, args);
    }

    void IVirtualScrollController.ScrollEnded(double scrollX, double scrollY, double totalScrollableWidth, double totalScrollableHeight)
    {
        var args = new VirtualScrollScrolledEventArgs(scrollX, scrollY, totalScrollableWidth, totalScrollableHeight);

        if (ScrollEndedCommand != null && ScrollEndedCommand.CanExecute(args))
        {
            ScrollEndedCommand.Execute(args);
        }

        OnScrollEndedEvent?.Invoke(this, args);
    }

    void IVirtualScrollController.LayoutUpdateCompleted()
        => OnLayoutUpdateCompleted?.Invoke(this, EventArgs.Empty);

    /// <summary>
    /// Gets the range of currently visible items in the virtual scroll.
    /// </summary>
    /// <returns>A <see cref="VirtualScrollRange"/> containing the first and last visible item positions, or <c>null</c> if no items are visible or the handler is not available.</returns>
    public VirtualScrollRange? GetVisibleItemsRange()
    {
#if ANDROID || IOS || MACCATALYST || WINDOWS
        if (Handler is VirtualScrollHandler handler)
        {
            return handler.GetVisibleItemsRange();
        }
#endif
        return null;
    }

    /// <inheritdoc/>
    public void ScrollTo(int sectionIndex, int itemIndex, ScrollToPosition position = ScrollToPosition.MakeVisible, bool animated = true) => Handler?.Invoke(nameof(ScrollTo), new VirtualScrollCommandScrollToArgs(sectionIndex, itemIndex, position, animated));

    /// <inheritdoc/>
    public void ScrollTo(object itemOrSection, ScrollToPosition position = ScrollToPosition.MakeVisible, bool animated = true)
    {
        if (ItemsSource is not IVirtualScrollAdapter adapter)
        {
            return;
        }

        var sectionCount = adapter.GetSectionCount();
        
        // First, check if it's a section
        for (var sectionIdx = 0; sectionIdx < sectionCount; sectionIdx++)
        {
            var section = adapter.GetSection(sectionIdx);
            if (ReferenceEquals(section, itemOrSection) || Equals(section, itemOrSection))
            {
                // Found as a section, scroll to section header
                ScrollTo(sectionIdx, -1, position, animated);
                return;
            }
        }
        
        // Then, check if it's an item
        for (var sectionIdx = 0; sectionIdx < sectionCount; sectionIdx++)
        {
            var itemCount = adapter.GetItemCount(sectionIdx);
            for (var itemIdx = 0; itemIdx < itemCount; itemIdx++)
            {
                var item = adapter.GetItem(sectionIdx, itemIdx);
                if (ReferenceEquals(item, itemOrSection) || Equals(item, itemOrSection))
                {
                    ScrollTo(sectionIdx, itemIdx, position, animated);
                    return;
                }
            }
        }
    }

    private static IVirtualScrollAdapter? CoerceAdapter(object value)
    {
        if (value is null or IVirtualScrollAdapter)
        {
            return (IVirtualScrollAdapter?)value;
        }

        // Check if it's an ObservableCollection<T> or inherits from it
        var valueType = value.GetType();
        if (value is IList and INotifyCollectionChanged)
        {
            if (!RuntimeFeature.IsDynamicCodeSupported)
            {
                throw new NotSupportedException("By using AOT it's not possible to create a VirtualScroll adapter for INotifyCollectionChanged collections, please provide an IVirtualScrollAdapter instead.");
            }

            var adapterType = GetObservableCollectionItemType(valueType) is { } itemType
                ? typeof(VirtualScrollObservableCollectionAdapter<>).MakeGenericType(itemType)
                : typeof(VirtualScrollNotifyCollectionChangedAdapter<>).MakeGenericType(valueType);

            return (IVirtualScrollAdapter?) Activator.CreateInstance(adapterType, value);
        }

        if (value is IEnumerable enumerable)
        {
            return new VirtualScrollListAdapter(enumerable);
        }
            
        throw new NotSupportedException($"{value.GetType()} is not supported as an adapter for VirtualScroll. Please provide an IVirtualScrollAdapter or a supported enumerable.");
    }

    private static Type? GetObservableCollectionItemType(Type? type)
    {
        // go through type and base types to see whether this is an ObservableCollection<>
        while (type is not null)
        {
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(ObservableCollection<>))
            {
                return type.GetGenericArguments()[0];
            }
            
            type = type.BaseType;
        }
        
        return null;
    }

    /// <summary>
    /// Creates a virtual scroll adapter for the specified observable collection.
    /// </summary>
    public static IVirtualScrollSource CreateObservableCollectionAdapter<T>(ObservableCollection<T> collection)
        => new VirtualScrollObservableCollectionAdapter<T>(collection);
    
    /// <summary>
    /// Creates a virtual scroll adapter for the specified observable collection.
    /// </summary>
    public static IVirtualScrollBatchableSource CreateObservableCollectionAdapter<T>(ReadOnlyObservableCollection<T> collection)
        => new VirtualScrollNotifyCollectionChangedAdapter<ReadOnlyObservableCollection<T>>(collection);
    
    /// <summary>
    /// Creates a grouped virtual scroll adapter for the specified observable collection.
    /// </summary>
    public static IVirtualScrollSource CreateObservableCollectionAdapter<TSection, TItem>(ObservableCollection<TSection> collection, Func<TSection, ObservableCollection<TItem>> sectionItemsGetter)
        => new VirtualScrollGroupedObservableCollectionAdapter<ObservableCollection<TSection>, TItem>(collection, section => sectionItemsGetter((TSection)section));
    
    /// <summary>
    /// Creates a grouped virtual scroll adapter for the specified observable collection.
    /// </summary>
    public static IVirtualScrollBatchableSource CreateObservableCollectionAdapter<TSection, TItem>(ObservableCollection<TSection> collection, Func<TSection, ReadOnlyObservableCollection<TItem>> sectionItemsGetter)
        => new VirtualScrollGroupedNotifyCollectionChangedAdapter<ObservableCollection<TSection>, ReadOnlyObservableCollection<TItem>>(collection, section => sectionItemsGetter((TSection)section));
    
    /// <summary>
    /// Creates a grouped virtual scroll adapter for the specified observable collection.
    /// </summary>
    public static IVirtualScrollSource CreateObservableCollectionAdapter<TSection, TItem>(ReadOnlyObservableCollection<TSection> collection, Func<TSection, ObservableCollection<TItem>> sectionItemsGetter)
        => new VirtualScrollGroupedObservableCollectionAdapter<ReadOnlyObservableCollection<TSection>, TItem>(collection, section => sectionItemsGetter((TSection)section));
    
    /// <summary>
    /// Creates a grouped virtual scroll adapter for the specified observable collection.
    /// </summary>
    public static IVirtualScrollBatchableSource CreateObservableCollectionAdapter<TSection, TItem>(ReadOnlyObservableCollection<TSection> collection, Func<TSection, ReadOnlyObservableCollection<TItem>> sectionItemsGetter)
        => new VirtualScrollGroupedNotifyCollectionChangedAdapter<ReadOnlyObservableCollection<TSection>, ReadOnlyObservableCollection<TItem>>(collection, section => sectionItemsGetter((TSection)section));
    
    /// <summary>
    /// Creates a virtual scroll adapter for the specified static collection.
    /// </summary>
    public static IVirtualScrollReorderableSource CreateStaticCollectionAdapter<T>(IEnumerable<T> collection)
        => new VirtualScrollListAdapter(collection);
    
    /// <summary>
    /// Creates a grouped virtual scroll adapter for the specified static collection.
    /// </summary>
    public static IVirtualScrollReorderableSource CreateStaticCollectionAdapter<TSection, TItem>(IEnumerable<TSection> collection, Func<TSection, IEnumerable<TItem>> sectionItemsGetter)
        => new VirtualScrollGroupedListAdapter(collection, section => sectionItemsGetter((TSection)section));
}
