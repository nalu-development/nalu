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
    IVirtualScrollAdapter? Adapter { get; set; }

    /// <summary>
    /// Gets or sets the layout for the virtual scroll.
    /// </summary>
    IVirtualScrollLayout ItemsLayout { get; set; }
    
    /// <summary>
    /// Gets or sets the template used to display each item.
    /// </summary>
    DataTemplate? ItemTemplate { get; set; }
    
    /// <summary>
    /// Gets or sets the template used to display section headers.
    /// </summary>
    DataTemplate? SectionHeaderTemplate { get; set; }
    
    /// <summary>
    /// Gets or sets the template used to display section footers.
    /// </summary>
    DataTemplate? SectionFooterTemplate { get; set; }
    
    /// <summary>
    /// Gets or sets the header view displayed at the top of the scroll view.
    /// </summary>
    View? Header { get; set; }
    
    /// <summary>
    /// Gets or sets the footer view displayed at the bottom of the scroll view.
    /// </summary>
    View? Footer { get; set; }
    
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
    ICommand? RefreshCommand { get; set; }
    
    /// <summary>
    /// Gets or sets a value indicating whether pull-to-refresh is enabled.
    /// </summary>
    bool IsRefreshEnabled { get; set; }
    
    /// <summary>
    /// Gets or sets the accent color for the refresh indicator.
    /// </summary>
    Color? RefreshAccentColor { get; set; }
    
    /// <summary>
    /// Gets or sets a value indicating whether the refresh indicator is currently showing.
    /// Setting this to true programmatically will trigger the refresh indicator.
    /// Setting this to false will stop the refresh indicator.
    /// </summary>
    bool IsRefreshing { get; set; }
}
