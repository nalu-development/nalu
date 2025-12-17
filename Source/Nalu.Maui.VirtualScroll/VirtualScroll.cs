using System.Collections;
using System.Collections.ObjectModel;
using Microsoft.Maui.Controls.Internals;

namespace Nalu;

/// <summary>
/// A scrollable view that virtualizes its content.
/// </summary>
/// <remarks>
/// This control is designed to replace traditional <see cref="CollectionView"/> control by providing a more efficient implementation tailored for Android and iOS platforms.
/// </remarks>
public class VirtualScroll : View, IVirtualScroll
{
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
        null
    );

    /// <summary>
    /// Bindable property for <see cref="SectionFooterTemplate"/>.
    /// </summary>
    public static readonly BindableProperty SectionFooterTemplateProperty = BindableProperty.Create(
        nameof(SectionFooterTemplate),
        typeof(DataTemplate),
        typeof(VirtualScroll),
        null
    );

    /// <summary>
    /// Bindable property for <see cref="Header"/>.
    /// </summary>
    public static readonly BindableProperty HeaderProperty = BindableProperty.Create(
        nameof(Header),
        typeof(View),
        typeof(VirtualScroll),
        null,
        propertyChanged: (bindable, _, newValue) => ((VirtualScroll)bindable).GlobalHeaderTemplate = newValue is View view ? new DataTemplate(() => view) : null
    );

    /// <summary>
    /// Bindable property for <see cref="Footer"/>.
    /// </summary>
    public static readonly BindableProperty FooterProperty = BindableProperty.Create(
        nameof(Footer),
        typeof(View),
        typeof(VirtualScroll),
        null,
        propertyChanged: (bindable, _, newValue) => ((VirtualScroll)bindable).GlobalFooterTemplate = newValue is View view ? new DataTemplate(() => view) : null
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
}
