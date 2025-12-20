# Scrolling

`VirtualScroll` provides comprehensive scrolling capabilities including programmatic scrolling, scroll event handling, and visible items tracking.

## Scroll To Item

`VirtualScroll` provides methods to programmatically scroll to specific items:

### Scroll by Index

```csharp
// Scroll to item at index 5 in section 0
virtualScroll.ScrollTo(sectionIndex: 0, itemIndex: 5);

// Scroll to section header (use itemIndex: -1)
virtualScroll.ScrollTo(sectionIndex: 1, itemIndex: -1);

// With position and animation control
virtualScroll.ScrollTo(0, 10, ScrollToPosition.Center, animated: true);
```

### Scroll by Object

```csharp
// Scroll to a specific item or section object
virtualScroll.ScrollTo(myItem);
virtualScroll.ScrollTo(myItem, ScrollToPosition.Start, animated: false);
```

The `ScrollToPosition` options are:
- `MakeVisible` (default): Scrolls just enough to make the item visible
- `Start`: Positions the item at the start of the viewport
- `Center`: Centers the item in the viewport
- `End`: Positions the item at the end of the viewport

## Scroll Events

`VirtualScroll` provides two ways to respond to scroll position changes:

### ScrolledCommand

Bind a command that executes whenever the scroll position changes:

```xml
<nalu:VirtualScroll Adapter="{Binding Items}"
                    ScrolledCommand="{Binding ScrollCommand}">
    ...
</nalu:VirtualScroll>
```

```csharp
[RelayCommand]
private void OnScrolled(VirtualScrollScrolledEventArgs args)
{
    // Access scroll position and dimensions
    var scrollX = args.ScrollX;
    var scrollY = args.ScrollY;
    var totalWidth = args.TotalScrollableWidth;
    var totalHeight = args.TotalScrollableHeight;
    
    // Use scroll percentages (0.0 to 1.0)
    var scrollPercent = args.ScrollPercentageY;
    
    // Update UI based on scroll position
    UpdateScrollIndicator(scrollPercent);
}
```

### OnScrolled Event

Alternatively, subscribe to the `OnScrolled` event:

```csharp
virtualScroll.OnScrolled += (sender, args) =>
{
    var scrollPercent = args.ScrollPercentageY;
    Console.WriteLine($"Scrolled to {scrollPercent:P0}%");
};
```

### VirtualScrollScrolledEventArgs

The event arguments provide comprehensive scroll information:

| Property | Type | Description |
|----------|------|-------------|
| `ScrollX` | `double` | Current horizontal scroll position in device-independent units |
| `ScrollY` | `double` | Current vertical scroll position in device-independent units |
| `TotalScrollableWidth` | `double` | Total scrollable width in device-independent units |
| `TotalScrollableHeight` | `double` | Total scrollable height in device-independent units |
| `ScrollPercentageX` | `double` | Horizontal scroll percentage (0.0 to 1.0), or 0.0 if not scrollable horizontally |
| `ScrollPercentageY` | `double` | Vertical scroll percentage (0.0 to 1.0), or 0.0 if not scrollable vertically |

**Example: Scroll Progress Indicator**

```csharp
[RelayCommand]
private void OnScrolled(VirtualScrollScrolledEventArgs args)
{
    // Update a progress bar based on scroll position
    ScrollProgress = args.ScrollPercentageY;
}

public double ScrollProgress
{
    get => _scrollProgress;
    set => SetProperty(ref _scrollProgress, value);
}
```

```xml
<ProgressBar Value="{Binding ScrollProgress}" 
             Maximum="1.0" 
             HeightRequest="4" 
             Margin="0,0,0,8" />
```

**Performance Note:** Scroll events are only processed when `ScrolledCommand` is set or `OnScrolled` has subscribers. This ensures optimal performance when scroll tracking is not needed.

## Visible Items Range

Get the range of currently visible items (including headers and footers) using `GetVisibleItemsRange()`:

> **⚠️ Performance Warning:** `GetVisibleItemsRange()` has a non-negligible performance cost as it queries the platform's native scroll view to determine visible items. 
> 
> **❌ Never call `GetVisibleItemsRange()` within `OnScrolled` event handlers or `ScrolledCommand`**—this would cause severe performance issues as it would be called on every scroll event.
> 
> **✅ For infinite scroll scenarios**, use `VirtualScrollScrolledEventArgs.ScrollPercentageY` instead (see example below).
> 
> **✅ Use `GetVisibleItemsRange()` sparingly**—only when you need to know the exact visible items (e.g., for analytics, debugging, or user-initiated actions like selecting all visible items), and call it outside of scroll event handlers with appropriate throttling.

```csharp
var range = virtualScroll.GetVisibleItemsRange();

if (range.HasValue)
{
    var r = range.Value;
    
    // Check if global header is visible
    if (r.StartSectionIndex == VirtualScrollRange.GlobalHeaderSectionIndex)
    {
        Console.WriteLine("Global header is visible");
    }
    
    // Check if section header is visible
    if (r.StartItemIndex == VirtualScrollRange.SectionHeaderItemIndex)
    {
        Console.WriteLine($"Section {r.StartSectionIndex} header is visible");
    }
    
    // Regular item
    Console.WriteLine($"Visible range: Section {r.StartSectionIndex}, Item {r.StartItemIndex} to Section {r.EndSectionIndex}, Item {r.EndItemIndex}");
}
else
{
    Console.WriteLine("No items are currently visible");
}
```

### VirtualScrollRange

The `VirtualScrollRange` struct represents the visible range with special constants for headers and footers:

| Constant | Value | Description |
|----------|-------|-------------|
| `GlobalHeaderSectionIndex` | `int.MinValue` | Section index indicating the global header |
| `GlobalFooterSectionIndex` | `int.MaxValue` | Section index indicating the global footer |
| `SectionHeaderItemIndex` | `int.MinValue` | Item index indicating a section header |
| `SectionFooterItemIndex` | `int.MaxValue` | Item index indicating a section footer |

**Example: Select All Visible Items (Button Action)**

```csharp
[RelayCommand]
private void SelectAllVisibleItems()
{
    var range = VirtualScroll.GetVisibleItemsRange();
    if (!range.HasValue)
    {
        return;
    }
    
    var r = range.Value;
    var adapter = VirtualScroll.Adapter as IVirtualScrollAdapter;
    if (adapter is null)
    {
        return;
    }
    
    var selectedItems = new List<object>();
    
    // Iterate through visible items
    for (var sectionIndex = r.StartSectionIndex; sectionIndex <= r.EndSectionIndex; sectionIndex++)
    {
        // Skip special sections (global header/footer)
        if (sectionIndex == VirtualScrollRange.GlobalHeaderSectionIndex || 
            sectionIndex == VirtualScrollRange.GlobalFooterSectionIndex)
        {
            continue;
        }
        
        var startItemIndex = sectionIndex == r.StartSectionIndex ? r.StartItemIndex : 0;
        var endItemIndex = sectionIndex == r.EndSectionIndex ? r.EndItemIndex : adapter.GetItemCount(sectionIndex) - 1;
        
        // Skip section headers/footers
        if (startItemIndex == VirtualScrollRange.SectionHeaderItemIndex || 
            startItemIndex == VirtualScrollRange.SectionFooterItemIndex)
        {
            startItemIndex = 0;
        }
        if (endItemIndex == VirtualScrollRange.SectionHeaderItemIndex || 
            endItemIndex == VirtualScrollRange.SectionFooterItemIndex)
        {
            endItemIndex = adapter.GetItemCount(sectionIndex) - 1;
        }
        
        // Add items in this section's visible range
        for (var itemIndex = startItemIndex; itemIndex <= endItemIndex; itemIndex++)
        {
            var item = adapter.GetItem(sectionIndex, itemIndex);
            if (item is not null)
            {
                selectedItems.Add(item);
            }
        }
    }
    
    // Select all visible items
    SelectedItems.Clear();
    foreach (var item in selectedItems)
    {
        SelectedItems.Add(item);
    }
}
```

**Example: Display Visible Range (with Throttling)**

> **Note:** This example shows throttling for display purposes (e.g., debugging). For production code, consider calling `GetVisibleItemsRange()` only on user actions (like button presses) rather than in scroll handlers.

```csharp
private DateTime _lastRangeCheck = DateTime.MinValue;
private const int RangeCheckThrottleMs = 500; // Check at most every 500ms

private void VirtualScroll_OnScrolled(object? sender, VirtualScrollScrolledEventArgs e)
{
    // Throttle GetVisibleItemsRange() calls to avoid performance impact
    var now = DateTime.UtcNow;
    if ((now - _lastRangeCheck).TotalMilliseconds < RangeCheckThrottleMs)
    {
        return; // Skip this check, too soon since last one
    }
    _lastRangeCheck = now;
    
    var range = VirtualScroll.GetVisibleItemsRange();
    
    if (range.HasValue)
    {
        var r = range.Value;
        var startType = GetPositionType(r.StartSectionIndex, r.StartItemIndex);
        var endType = GetPositionType(r.EndSectionIndex, r.EndItemIndex);
        
        RangeInfoLabel.Text = $"Visible: {startType} → {endType}";
    }
    else
    {
        RangeInfoLabel.Text = "Visible Range: None";
    }
}

private static string GetPositionType(int sectionIndex, int itemIndex)
{
    if (sectionIndex == VirtualScrollRange.GlobalHeaderSectionIndex)
        return "GlobalHeader";
    if (sectionIndex == VirtualScrollRange.GlobalFooterSectionIndex)
        return "GlobalFooter";
    if (itemIndex == VirtualScrollRange.SectionHeaderItemIndex)
        return $"SectionHeader[{sectionIndex}]";
    if (itemIndex == VirtualScrollRange.SectionFooterItemIndex)
        return $"SectionFooter[{sectionIndex}]";
    return $"Item[{sectionIndex},{itemIndex}]";
}
```

**Example: Infinite Scroll Loading (using Scroll Percentage)**

```csharp
private bool _isLoadingMore;
private System.Threading.Timer? _loadMoreTimer;
private const int LoadMoreDebounceMs = 300;

[RelayCommand]
private void OnScrolled(VirtualScrollScrolledEventArgs e)
{
    // Only check when near the bottom (e.g., within 10% of end)
    // Use ScrollPercentageY - it's efficient and doesn't require platform queries
    if (e.ScrollPercentageY < 0.9 || _isLoadingMore)
    {
        return;
    }
    
    // Debounce: reset the timer (reuse same instance)
    _loadMoreTimer ??= new System.Threading.Timer(_ => LoadMoreItems(), null, Timeout.Infinite, Timeout.Infinite);
    _loadMoreTimer.Change(LoadMoreDebounceMs, Timeout.Infinite);
}

private async void LoadMoreItems()
{
    if (_isLoadingMore)
    {
        return;
    }
    
    _isLoadingMore = true;
    try
    {
        await LoadMoreItemsCommand.ExecuteAsync(null);
    }
    finally
    {
        _isLoadingMore = false;
    }
}
```

**Why use ScrollPercentageY instead of GetVisibleItemsRange()?**
- `ScrollPercentageY` is calculated from scroll position data already available in the event args (no platform queries)
- `GetVisibleItemsRange()` requires querying the native platform scroll view, which is expensive
- For infinite scroll, you only need to know "are we near the bottom?"—scroll percentage is perfect for this

**Performance Notes:**
- Scroll events are only processed when `ScrolledCommand` is set or `OnScrolled` has subscribers. This ensures optimal performance when scroll tracking is not needed.
- `GetVisibleItemsRange()` queries the native platform scroll view and should be used sparingly. Always debounce or throttle calls when used in scroll event handlers.

