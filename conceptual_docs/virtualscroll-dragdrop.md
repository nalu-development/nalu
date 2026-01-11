## Drag & Drop

`VirtualScroll` supports drag and drop operations, allowing users to reorder items by dragging them to new positions. This feature is available on both Android and iOS platforms and works seamlessly with observable collections.

### Overview

Drag and drop functionality in `VirtualScroll` is enabled through the `DragHandler` property. All built-in adapter implementations support drag and drop operations out of the box, making it easy to add reordering capabilities to your lists.

> **Note:** Section headers/footers and global headers/footers cannot be dragged. Only data items can be reordered.

### Enabling Drag & Drop

To enable drag and drop, simply bind the `DragHandler` property to your adapter:

```xml
<vs:VirtualScroll ItemsSource="{Binding Adapter}"
                  DragHandler="{Binding Adapter}">
    <!-- Templates -->
</vs:VirtualScroll>
```

Since all built-in adapters implement `IReorderableVirtualScrollAdapter` (which combines `IVirtualScrollAdapter` and `IVirtualScrollDragHandler`), you can bind the same adapter instance to both `ItemsSource` and `DragHandler`.

### Basic Usage with Observable Collections

The simplest way to enable drag and drop is with an `ObservableCollection<T>`:

**XAML:**
```xml
<vs:VirtualScroll ItemsSource="{Binding Adapter}"
                  DragHandler="{Binding Adapter}">
    <vs:VirtualScroll.ItemTemplate>
        <DataTemplate x:DataType="models:MyItem">
            <nalu:ViewBox>
                <Border StrokeShape="RoundRectangle 8" Margin="8" Padding="16">
                    <Label Text="{Binding Name}" />
                </Border>
            </nalu:ViewBox>
        </DataTemplate>
    </vs:VirtualScroll.ItemTemplate>
</vs:VirtualScroll>
```

**PageModel:**
```csharp
public partial class MyPageModel : ObservableObject
{
    public ObservableCollection<MyItem> Items { get; }
    public IReorderableVirtualScrollAdapter Adapter { get; }

    public MyPageModel()
    {
        Items = new ObservableCollection<MyItem>(
            Enumerable.Range(1, 20).Select(i => new MyItem($"Item {i}"))
        );
        
        // Create adapter - it automatically supports drag & drop
        Adapter = VirtualScroll.CreateObservableCollectionAdapter(Items);
    }
}
```

When you bind the adapter to `DragHandler`, users can long-press and drag items to reorder them. The underlying `ObservableCollection` is automatically updated, and change notifications are properly handled.

### Grouped Collections

Drag and drop also works with grouped collections, allowing items to be moved both within sections and between sections:

**XAML:**
```xml
<vs:VirtualScroll ItemsSource="{Binding Adapter}"
                  DragHandler="{Binding Adapter}">
    <vs:VirtualScroll.SectionHeaderTemplate>
        <DataTemplate x:DataType="models:Group">
            <Label Text="{Binding Title}" FontSize="18" FontAttributes="Bold" 
                   BackgroundColor="LightGray" Padding="16,8" />
        </DataTemplate>
    </vs:VirtualScroll.SectionHeaderTemplate>
    
    <vs:VirtualScroll.ItemTemplate>
        <DataTemplate x:DataType="models:Item">
            <nalu:ViewBox>
                <Border StrokeShape="RoundRectangle 8" Margin="8" Padding="16">
                    <Label Text="{Binding Name}" />
                </Border>
            </nalu:ViewBox>
        </DataTemplate>
    </vs:VirtualScroll.ItemTemplate>
</vs:VirtualScroll>
```

**PageModel:**
```csharp
public partial class GroupedPageModel : ObservableObject
{
    public ObservableCollection<Group> Groups { get; }
    public IReorderableVirtualScrollAdapter Adapter { get; }

    public GroupedPageModel()
    {
        Groups = new ObservableCollection<Group>(
            Enumerable.Range(1, 5).Select(i => new Group($"Group {i}"))
        );
        
        // Create grouped adapter - supports cross-section drag & drop
        Adapter = VirtualScroll.CreateObservableCollectionAdapter(
            Groups, 
            group => group.Items
        );
    }
}

public class Group : ObservableObject
{
    public string Title { get; }
    public ObservableCollection<Item> Items { get; }

    public Group(string title)
    {
        Title = title;
        Items = new ObservableCollection<Item>(
            Enumerable.Range(1, 5).Select(i => new Item($"{title} - Item {i}"))
        );
    }
}
```

When dragging items in a grouped collection:
- Items can be moved within the same section
- Items can be moved between different sections
- The source and destination collections are automatically updated
- Change notifications are properly handled for both collections

### Custom Drag Behavior

You can customize drag behavior by creating a custom adapter that implements `IReorderableVirtualScrollAdapter` or by overriding methods in the built-in adapters:

#### Controlling Which Items Can Be Dragged

Override `CanDragItem` to conditionally allow dragging:

```csharp
public class CustomAdapter : VirtualScrollObservableCollectionAdapter<MyItem>
{
    public CustomAdapter(ObservableCollection<MyItem> collection) 
        : base(collection)
    {
    }

    public override bool CanDragItem(VirtualScrollDragInfo dragInfo)
    {
        // Only allow dragging if the item is not locked
        if (dragInfo.Item is MyItem item)
        {
            return !item.IsLocked;
        }
        return false;
    }
}
```

#### Controlling Drop Locations

Override `CanDropItemAt` to restrict where items can be dropped:

```csharp
public override bool CanDropItemAt(VirtualScrollDragDropInfo dragDropInfo)
{
    // Prevent dropping items at the beginning of the list
    if (dragDropInfo.DestinationItemIndex == 0)
    {
        return false;
    }
    
    // Prevent moving items between certain sections
    if (dragDropInfo.OriginalSectionIndex != dragDropInfo.DestinationSectionIndex)
    {
        // Only allow cross-section moves if both sections allow it
        var sourceSection = GetSection(dragDropInfo.OriginalSectionIndex);
        var destSection = GetSection(dragDropInfo.DestinationSectionIndex);
        return sourceSection?.AllowExport == true && 
               destSection?.AllowImport == true;
    }
    
    return true;
}
```

#### Custom Move Logic

Override `MoveItem` to implement custom move behavior:

```csharp
public override void MoveItem(VirtualScrollDragMoveInfo dragMoveInfo)
{
    // Perform custom validation or side effects before moving
    if (dragMoveInfo.Item is MyItem item)
    {
        item.MovedAt = DateTime.Now;
    }
    
    // Call base implementation to perform the actual move
    base.MoveItem(dragMoveInfo);
    
    // Perform any post-move operations
    OnItemMoved(item);
}
```

### Lifecycle Hooks

The drag handler interface provides several lifecycle hooks that you can use to respond to drag operations:

#### OnDragInitiating

Called when a drag operation is about to start (before it actually begins):

```csharp
public override void OnDragInitiating(VirtualScrollDragInfo dragInfo)
{
    // Prepare for drag - e.g., show visual feedback
    if (dragInfo.Item is MyItem item)
    {
        item.IsDragging = true;
    }
}
```

#### OnDragStarted

Called when the drag operation has started:

```csharp
public override void OnDragStarted(VirtualScrollDragInfo dragInfo)
{
    // Track drag start - e.g., log analytics
    Analytics.TrackEvent("ItemDragStarted", new Dictionary<string, string>
    {
        { "ItemId", dragInfo.Item?.ToString() ?? "Unknown" }
    });
}
```

#### OnDragEnded

Called when the drag operation has completed (whether successful or cancelled):

```csharp
public override void OnDragEnded(VirtualScrollDragInfo dragInfo)
{
    // Clean up drag state
    if (dragInfo.Item is MyItem item)
    {
        item.IsDragging = false;
    }
    
    // Save changes if needed
    SaveChanges();
}
```

### Complete Example

Here's a complete example demonstrating drag and drop with custom behavior:

**XAML:**
```xml
<vs:VirtualScroll ItemsSource="{Binding Adapter}"
                  DragHandler="{Binding Adapter}"
                  FadingEdgeLength="32">
    <vs:VirtualScroll.ItemTemplate>
        <DataTemplate x:DataType="pageModels:TaskItem">
            <nalu:ViewBox>
                <Border StrokeShape="RoundRectangle 8" 
                        Margin="8" 
                        Padding="16"
                        BackgroundColor="{Binding BackgroundColor}">
                    <Grid ColumnDefinitions="*,Auto">
                        <Label Grid.Column="0" 
                               Text="{Binding Title}" 
                               FontSize="16" />
                        <Image Grid.Column="1" 
                               Source="drag_handle.png" 
                               WidthRequest="24" 
                               HeightRequest="24" 
                               Opacity="0.3" />
                    </Grid>
                </Border>
            </nalu:ViewBox>
        </DataTemplate>
    </vs:VirtualScroll.ItemTemplate>
</vs:VirtualScroll>
```

**PageModel:**
```csharp
public partial class TaskListPageModel : ObservableObject
{
    public ObservableCollection<TaskItem> Tasks { get; }
    public IReorderableVirtualScrollAdapter Adapter { get; }

    public TaskListPageModel()
    {
        Tasks = new ObservableCollection<TaskItem>(
            Enumerable.Range(1, 10).Select(i => new TaskItem($"Task {i}"))
        );
        
        Adapter = VirtualScroll.CreateObservableCollectionAdapter(Tasks);
    }
}

public partial class TaskItem : ObservableObject
{
    [ObservableProperty]
    private bool _isDragging;
    
    [ObservableProperty]
    private Color _backgroundColor = Colors.White;

    public string Title { get; }

    public TaskItem(string title)
    {
        Title = title;
    }
}
```

**Custom Adapter:**
```csharp
public class TaskListAdapter : VirtualScrollObservableCollectionAdapter<TaskItem>
{
    public TaskListAdapter(ObservableCollection<TaskItem> collection) 
        : base(collection)
    {
    }

    public override bool CanDragItem(VirtualScrollDragInfo dragInfo)
    {
        // Only allow dragging completed tasks
        return dragInfo.Item is TaskItem task && task.IsCompleted;
    }

    public override void OnDragStarted(VirtualScrollDragInfo dragInfo)
    {
        if (dragInfo.Item is TaskItem task)
        {
            task.IsDragging = true;
            task.BackgroundColor = Colors.LightBlue;
        }
    }

    public override void OnDragEnded(VirtualScrollDragInfo dragInfo)
    {
        if (dragInfo.Item is TaskItem task)
        {
            task.IsDragging = false;
            task.BackgroundColor = Colors.White;
        }
    }
}
```

### Limitations

1. **Headers and Footers**: Section headers/footers and global headers/footers cannot be dragged. Only data items support drag and drop.

2. **Read-Only Collections**: Drag and drop requires collections that support modification. Fixed-size or read-only collections will throw an exception when drag operations are attempted.

3. **Static Collections**: Static collection adapters (created with `CreateStaticCollectionAdapter`) do support drag and drop.

4. **Change Notifications**: During drag swap operations, change notifications from the underlying collection are temporarily suppressed to prevent conflicts. Notifications resume after the drag completes.

### Best Practices

1. **Visual Feedback**: Provide visual feedback during drag operations by updating item appearance in `OnDragStarted` and `OnDragEnded`.

2. **Validation**: Use `CanDragItem` and `CanDropItemAt` to validate drag operations before they occur, providing a better user experience.

3. **Custom Drag Handler**: While implementing a custom drag handler, make sure that `MoveItem` does NOT trigger a notification.
