namespace Nalu;

/// <summary>
/// A linear layout for virtual scroll that arranges items in a single line.
/// </summary>
public abstract class LinearVirtualScrollLayout : VirtualScrollLayout
{
    /// <summary>
    /// Bindable property for <see cref="EstimatedItemSize"/>.
    /// </summary>
    public static readonly BindableProperty EstimatedItemSizeProperty =
        BindableProperty.Create(
            nameof(EstimatedItemSize),
            typeof(double),
            typeof(LinearVirtualScrollLayout),
            64d,
            BindingMode.OneTime);
    
    /// <summary>
    /// Gets or sets the estimated size of each item in the layout.
    /// </summary>
    /// <remarks>
    /// This is useful on UIKit to highly reduce layout calculations, especially while UICollectionView estimates the total content size.
    /// </remarks>
    public double EstimatedItemSize
    {
        get => (double)GetValue(EstimatedItemSizeProperty);
        set => SetValue(EstimatedItemSizeProperty, value);
    }
    
    /// <summary>
    /// Bindable property for <see cref="EstimatedHeaderSize"/>.
    /// </summary>
    public static readonly BindableProperty EstimatedHeaderSizeProperty =
        BindableProperty.Create(
            nameof(EstimatedHeaderSize),
            typeof(double),
            typeof(LinearVirtualScrollLayout),
            64d,
            BindingMode.OneTime);
    
    /// <summary>
    /// Gets or sets the estimated size of the header in the layout.
    /// </summary>
    /// <remarks>
    /// This is useful on UIKit to highly reduce layout calculations, especially while UICollectionView estimates the total content size.
    /// </remarks>
    public double EstimatedHeaderSize
    {
        get => (double)GetValue(EstimatedHeaderSizeProperty);
        set => SetValue(EstimatedHeaderSizeProperty, value);
    }
    
    /// <summary>
    /// Bindable property for <see cref="EstimatedFooterSize"/>.
    /// </summary>
    public static readonly BindableProperty EstimatedFooterSizeProperty =
        BindableProperty.Create(
            nameof(EstimatedFooterSize),
            typeof(double),
            typeof(LinearVirtualScrollLayout),
            64d,
            BindingMode.OneTime);
    
    /// <summary>
    /// Gets or sets the estimated size of the footer in the layout.
    /// </summary>
    /// <remarks>
    /// This is useful on UIKit to highly reduce layout calculations, especially while UICollectionView estimates the total content size.
    /// </remarks>
    public double EstimatedFooterSize
    {
        get => (double)GetValue(EstimatedFooterSizeProperty);
        set => SetValue(EstimatedFooterSizeProperty, value);
    }
    
    /// <summary>
    /// Bindable property for <see cref="EstimatedSectionHeaderSize"/>.
    /// </summary>
    public static readonly BindableProperty EstimatedSectionHeaderSizeProperty =
        BindableProperty.Create(
            nameof(EstimatedSectionHeaderSize),
            typeof(double),
            typeof(LinearVirtualScrollLayout),
            64d,
            BindingMode.OneTime);
    
    /// <summary>
    /// Gets or sets the estimated size of section headers in the layout.
    /// </summary>
    /// <remarks>
    /// This is useful on UIKit to highly reduce layout calculations, especially while UICollectionView estimates the total content size.
    /// </remarks>
    public double EstimatedSectionHeaderSize
    {
        get => (double)GetValue(EstimatedSectionHeaderSizeProperty);
        set => SetValue(EstimatedSectionHeaderSizeProperty, value);
    }
    
    /// <summary>
    /// Bindable property for <see cref="EstimatedSectionFooterSize"/>.
    /// </summary>
    public static readonly BindableProperty EstimatedSectionFooterSizeProperty =
        BindableProperty.Create(
            nameof(EstimatedSectionFooterSize),
            typeof(double),
            typeof(LinearVirtualScrollLayout),
            64d,
            BindingMode.OneTime);
    
    /// <summary>
    /// Gets or sets the estimated size of section footers in the layout.
    /// </summary>
    /// <remarks>
    /// This is useful on UIKit to highly reduce layout calculations, especially while UICollectionView estimates the total content size.
    /// </remarks>
    public double EstimatedSectionFooterSize
    {
        get => (double)GetValue(EstimatedSectionFooterSizeProperty);
        set => SetValue(EstimatedSectionFooterSizeProperty, value);
    }
    
    /// <summary>
    /// Initializes a new instance of the <see cref="LinearVirtualScrollLayout" /> class.
    /// </summary>
    /// <param name="orientation">The orientation of the layout.</param>
    protected LinearVirtualScrollLayout(ItemsLayoutOrientation orientation)
        : base(orientation)
    {
    }
}
