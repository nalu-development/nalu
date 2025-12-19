using System.Collections;
using System.Collections.ObjectModel;
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
    /// Bindable property for <see cref="Adapter"/>.
    /// </summary>
    public static readonly BindableProperty AdapterProperty = BindableProperty.Create(
        nameof(Adapter),
        typeof(object),
        typeof(VirtualScroll),
        null,
        coerceValue: (_, value) =>
        {
            if (value is null or IVirtualScrollAdapter)
            {
                return value;
            }

            // Check if it's an ObservableCollection<T> or inherits from it
            var valueType = value.GetType();
            var observableCollectionBaseType = FindObservableCollectionBaseType(valueType);
            if (observableCollectionBaseType is not null)
            {
                var itemType = observableCollectionBaseType.GetGenericArguments()[0];
                var adapterType = typeof(VirtualScrollObservableCollectionAdapter<>).MakeGenericType(itemType);
                return Activator.CreateInstance(adapterType, value);
            }

            if (value is IEnumerable enumerable)
            {
                return new VirtualScrollListAdapter(enumerable);
            }
            
            throw new NotSupportedException($"{value.GetType()} is not supported as an adapter for VirtualScroll. Please provide an IVirtualScrollAdapter or a supported enumerable.");
        }
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
    /// Bindable property for <see cref="Header"/>.
    /// </summary>
    public static readonly BindableProperty HeaderProperty = BindableProperty.Create(
        nameof(Header),
        typeof(View),
        typeof(VirtualScroll),
        null,
        propertyChanged: (bindable, _, newValue) =>
        {
            var globalHeaderTemplate = newValue is View view ? new DataTemplate(() => view) : null;
            var virtualScroll = (VirtualScroll) bindable;
            virtualScroll.GlobalHeaderTemplate = globalHeaderTemplate;
            virtualScroll._hasGlobalHeader = globalHeaderTemplate is not null;
        }
    );

    /// <summary>
    /// Bindable property for <see cref="Footer"/>.
    /// </summary>
    public static readonly BindableProperty FooterProperty = BindableProperty.Create(
        nameof(Footer),
        typeof(View),
        typeof(VirtualScroll),
        null,
        propertyChanged: (bindable, _, newValue) =>
        {
            var globalFooterTemplate = newValue is View view ? new DataTemplate(() => view) : null;
            var virtualScroll = (VirtualScroll) bindable;
            virtualScroll.GlobalFooterTemplate = globalFooterTemplate;
            virtualScroll._hasGlobalFooter = globalFooterTemplate is not null;
        }
    );

    /// <summary>
    /// Bindable property for <see cref="ItemsLayout"/>.
    /// </summary>
    public static readonly BindableProperty ItemsLayoutProperty = BindableProperty.Create(
        nameof(ItemsLayout),
        typeof(IVirtualScrollLayout),
        typeof(VirtualScroll),
        LinearVirtualScrollLayout.Vertical
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
    /// The adapter that provides data to the virtual scroll.
    /// </summary>
    public object? Adapter
    {
        get => (object?)GetValue(AdapterProperty);
        set => SetValue(AdapterProperty, value);
    }

    IVirtualScrollAdapter? IVirtualScroll.Adapter
    {
        get => (IVirtualScrollAdapter?)Adapter;
        set => Adapter = value;
    }

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
    /// Gets or sets the header view displayed at the top of the scroll view.
    /// </summary>
    public View? Header
    {
        get => (View?)GetValue(HeaderProperty);
        set => SetValue(HeaderProperty, value);
    }

    /// <summary>
    /// Gets or sets the footer view displayed at the bottom of the scroll view.
    /// </summary>
    public View? Footer
    {
        get => (View?)GetValue(FooterProperty);
        set => SetValue(FooterProperty, value);
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
    /// Event raised when the user triggers a refresh.
    /// </summary>
    public event EventHandler<RefreshEventArgs>? OnRefresh;

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

    /// <summary>
    /// Finds the ObservableCollection&lt;T&gt; base type in the inheritance hierarchy.
    /// </summary>
    /// <param name="type">The type to check.</param>
    /// <returns>The ObservableCollection&lt;T&gt; base type if found, otherwise null.</returns>
    private static Type? FindObservableCollectionBaseType(Type type)
    {
        var currentType = type;
        while (currentType is not null)
        {
            if (currentType.IsGenericType && currentType.GetGenericTypeDefinition() == typeof(ObservableCollection<>))
            {
                return currentType;
            }

            currentType = currentType.BaseType;
        }

        return null;
    }

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
}
