using System.Windows.Input;

namespace Nalu;

/// <summary>
/// A scrollable view that virtualizes its content.
/// </summary>
public interface IVirtualScroll : IView
{
    /// <summary>
    /// The adapter that provides data to the virtual scroll.
    /// </summary>
    IVirtualScrollAdapter? Adapter { get; }

    /// <summary>
    /// The drag handler for the virtual scroll.
    /// </summary>
    /// <remarks>
    /// Drag and drop is enabled only when a drag handler is provided.
    /// All provided adapter implementations support drag and drop.
    /// </remarks>
    IVirtualScrollDragHandler? DragHandler { get; }

    /// <summary>
    /// Gets or sets the layout for the virtual scroll.
    /// </summary>
    IVirtualScrollLayout ItemsLayout { get; }
    
    /// <summary>
    /// Gets or sets the template used to display each item.
    /// </summary>
    DataTemplate? ItemTemplate { get; }
    
    /// <summary>
    /// Gets or sets the template used to display section headers.
    /// </summary>
    DataTemplate? SectionHeaderTemplate { get; }
    
    /// <summary>
    /// Gets or sets the template used to display section footers.
    /// </summary>
    DataTemplate? SectionFooterTemplate { get; }
    
    /// <summary>
    /// Gets or sets the template used to display the header at the top of the scroll view.
    /// </summary>
    DataTemplate? HeaderTemplate { get; }
    
    /// <summary>
    /// Gets or sets the template used to display the footer at the bottom of the scroll view.
    /// </summary>
    DataTemplate? FooterTemplate { get; }
    
    /// <summary>
    /// Gets the appropriate item template for the given item.
    /// </summary>
    DataTemplate? GetItemTemplate(object? item);

    /// <summary>
    /// Gets the appropriate section header template for the given section.
    /// </summary>
    DataTemplate? GetSectionHeaderTemplate(object? section);

    /// <summary>
    /// Gets the appropriate section footer template for the given section.
    /// </summary>
    DataTemplate? GetSectionFooterTemplate(object? section);

    /// <summary>
    /// Gets the appropriate global header template.
    /// </summary>
    DataTemplate? GetGlobalHeaderTemplate();

    /// <summary>
    /// Gets the appropriate global footer template.
    /// </summary>
    DataTemplate? GetGlobalFooterTemplate();
    
    /// <summary>
    /// Gets or sets the command to execute when the user requests a refresh.
    /// </summary>
    ICommand? RefreshCommand { get; }
    
    /// <summary>
    /// Gets or sets a value indicating whether pull-to-refresh is enabled.
    /// </summary>
    bool IsRefreshEnabled { get; }
    
    /// <summary>
    /// Gets or sets the accent color for the refresh indicator.
    /// </summary>
    Color? RefreshAccentColor { get; }
    
    /// <summary>
    /// Gets or sets a value indicating whether the refresh indicator is currently showing.
    /// Setting this to true programmatically will trigger the refresh indicator.
    /// Setting this to false will stop the refresh indicator.
    /// </summary>
    bool IsRefreshing { get; }

    /// <summary>
    /// Gets or sets the length of the fading edge effect in device-independent units.
    /// </summary>
    /// <remarks>
    /// A value of 0 means no fading edge is applied (default).
    /// The orientation of the fading edge is determined by the layout orientation (horizontal or vertical).
    /// </remarks>
    double FadingEdgeLength { get; }

    /// <summary>
    /// Scrolls to the specified item in the virtual scroll.
    /// </summary>
    /// <param name="sectionIndex">The index of the section.</param>
    /// <param name="itemIndex">The index of the item within the section. Use -1 to scroll to the section header.</param>
    /// <param name="position">The position to scroll to. Defaults to <see cref="Microsoft.Maui.Controls.ScrollToPosition.MakeVisible"/>.</param>
    /// <param name="animated">Whether the scroll should be animated. Defaults to <c>true</c>.</param>
    void ScrollTo(int sectionIndex, int itemIndex, ScrollToPosition position = ScrollToPosition.MakeVisible, bool animated = true);
    
    /// <summary>
    /// Scrolls to the specified item or section in the virtual scroll.
    /// This method searches through all sections and items to find the matching object.
    /// </summary>
    /// <param name="itemOrSection">The item or section object to scroll to.</param>
    /// <param name="position">The position to scroll to. Defaults to <see cref="Microsoft.Maui.Controls.ScrollToPosition.MakeVisible"/>.</param>
    /// <param name="animated">Whether the scroll should be animated. Defaults to <c>true</c>.</param>
    void ScrollTo(object itemOrSection, ScrollToPosition position = ScrollToPosition.MakeVisible, bool animated = true);
    
    /// <summary>
    /// Event raised when the scroll position changes.
    /// </summary>
    event EventHandler<VirtualScrollScrolledEventArgs> OnScrolled;
}
