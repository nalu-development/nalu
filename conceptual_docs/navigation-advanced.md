# Advanced Navigation Features

This guide covers advanced navigation features: guards, behaviors, navigation-scoped services, leak detection, and more.

## Navigation Guards

Guards allow you to prevent navigation based on conditions (e.g., unsaved changes).

### ILeavingGuard

Implement `ILeavingGuard` to control when a page can be left:

```csharp
public class EditFormPageModel : ObservableObject, ILeavingGuard
{
    private bool _hasUnsavedChanges;

    public async ValueTask<bool> CanLeaveAsync()
    {
        if (!_hasUnsavedChanges)
            return true;

        return await Application.Current.MainPage.DisplayAlert(
            "Unsaved Changes",
            "You have unsaved changes. Are you sure you want to leave?",
            "Leave", 
            "Stay"
        );
    }
}
```

**Important**: `ILeavingGuard` is evaluated **before** any lifecycle events fire. Returning `false` completely prevents the navigation.

### Guard Evaluation Timing

```
Navigation Request
    ↓
ILeavingGuard.CanLeaveAsync() on all affected pages
    ↓ (if all return true)
IEnteringAware/IAppearingAware on target page
```

If any guard returns `false`, the navigation is cancelled and no lifecycle events fire.

### Bypassing Guards

Use `NavigationBehavior.IgnoreGuards` when you need to force navigation:

```csharp
// User explicitly cancels - bypass guard
await _navigationService.GoToAsync(
    Navigation.Relative(NavigationBehavior.IgnoreGuards).Pop()
);
```

### Multiple Guards

If multiple pages have guards (e.g., popping multiple pages), all are evaluated:

```csharp
// Both Page B and Page C guards are checked
// Stack: [A, B, C] → Pop → Pop → [A]
await _navigationService.GoToAsync(
    Navigation.Relative().Pop().Pop()
);
```

If any guard returns `false`, the entire navigation is cancelled.

### Guard Best Practices

1. **Keep prompts simple**: Don't show multiple dialogs - combine into one
2. **Use async dialogs**: Always use async display methods
3. **Consider UX**: Don't block every navigation - only when data could be lost
4. **Track dirty state**: Use property change tracking to detect modifications
5. **Test guards**: Verify both allow and block scenarios

**Example: Dirty tracking**

```csharp
public class EditPageModel : ObservableObject, ILeavingGuard, IEnteringAware
{
    private string _originalData;
    private bool _saveInProgress;

    [ObservableProperty]
    private string _data;

    public ValueTask OnEnteringAsync()
    {
        _originalData = Data;
        return ValueTask.CompletedTask;
    }

    public async ValueTask<bool> CanLeaveAsync()
    {
        // Don't block if saving
        if (_saveInProgress)
            return true;

        // Check if modified
        var hasChanges = _data != _originalData;
        if (!hasChanges)
            return true;

        return await ConfirmLeaveAsync();
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        _saveInProgress = true;
        try
        {
            await _repository.SaveAsync(Data);
            _originalData = Data;
            
            await _navigationService.GoToAsync(Navigation.Relative().Pop());
        }
        finally
        {
            _saveInProgress = false;
        }
    }
}
```

## Navigation Behaviors

Control navigation behavior using `NavigationBehavior` flags.

### Available Behaviors

```csharp
public enum NavigationBehavior
{
    None = 0,
    PopAllPagesOnSectionChange = 0x01,
    PopAllPagesOnItemChange = 0x02,
    IgnoreGuards = 0x04,
    Immediate = 0x08,
    
    // Convenience combinations
    DefaultIgnoreGuards = IgnoreGuards | PopAllPagesOnItemChange,
    DefaultImmediate = Immediate | PopAllPagesOnItemChange,
    DefaultImmediateIgnoreGuards = Immediate | IgnoreGuards | PopAllPagesOnItemChange
}
```

### PopAllPagesOnSectionChange

When switching to a different `ShellSection` within the same `ShellItem`, clear the navigation stack:

```csharp
Navigation.Absolute(NavigationBehavior.PopAllPagesOnSectionChange)
    .Root<OtherSectionPageModel>()
```

**Default behavior**: Navigation stacks are preserved when switching sections.

### PopAllPagesOnItemChange

When switching to a different `ShellItem`, clear all navigation stacks:

```csharp
Navigation.Absolute(NavigationBehavior.PopAllPagesOnItemChange)
    .Root<OtherItemPageModel>()
```

**Default behavior**: This is the default for absolute navigation.

### IgnoreGuards

Skip `ILeavingGuard` checks:

```csharp
// Force navigation even if guards say no
Navigation.Relative(NavigationBehavior.IgnoreGuards).Pop()
```

**Use cases:**
- User explicitly cancels an operation
- Session timeout/forced logout
- Error states requiring immediate navigation

### Immediate

Skip the 60ms delay before navigation:

```csharp
Navigation.Absolute(NavigationBehavior.Immediate)
    .Root<HomePageModel>()
```

**Default behavior**: Nalu waits 60ms to let touch feedback display before starting navigation.

**Use cases:**
- Programmatic navigation (no user interaction)
- Background updates
- Startup navigation

### Combining Behaviors

Use bitwise OR to combine behaviors:

```csharp
// Immediate + ignore guards
Navigation.Relative(NavigationBehavior.Immediate | NavigationBehavior.IgnoreGuards)
    .Pop()

// Or use convenience constant
Navigation.Relative(NavigationBehavior.DefaultImmediateIgnoreGuards)
    .Pop()
```

### Behavior Examples

**Startup navigation:**

```csharp
// Fast, no guards, clear stacks
await _navigationService.GoToAsync(
    Navigation.Absolute(NavigationBehavior.DefaultImmediateIgnoreGuards)
        .Root<HomePageModel>()
);
```

**Force logout:**

```csharp
// Ignore guards (unsaved changes), immediate
await _navigationService.GoToAsync(
    Navigation.Absolute(NavigationBehavior.DefaultImmediateIgnoreGuards)
        .Root<LoginPageModel>()
);
```

**Tab switching with clean state:**

```csharp
// Clear navigation stack when switching tabs
await _navigationService.GoToAsync(
    Navigation.Absolute(NavigationBehavior.PopAllPagesOnSectionChange)
        .Root<TabTwoPageModel>()
);
```

## Navigation-Scoped Services

Share data between a page and all its nested child pages.

### Concept

Navigation-scoped services create a "context" that:
- Is provided by a parent page
- Lives as long as that page is in the navigation stack
- Is accessible to all child pages navigated from it
- Is automatically disposed when the parent page is popped

### Providing a Service

```csharp
public class PersonPageModel : ObservableObject, IEnteringAware<PersonIntent>
{
    private readonly INavigationServiceProvider _navProvider;

    public PersonPageModel(INavigationServiceProvider navProvider)
    {
        _navProvider = navProvider;
    }

    public ValueTask OnEnteringAsync(PersonIntent intent)
    {
        // Create and provide a service for child pages
        var personContext = new PersonContext(intent.PersonId);
        _navProvider.AddNavigationScoped<IPersonContext>(personContext);
        
        return ValueTask.CompletedTask;
    }
}
```

### Consuming a Service

```csharp
public class PersonDetailsPageModel : ObservableObject
{
    private readonly IPersonContext _personContext;

    public PersonDetailsPageModel(INavigationServiceProvider navProvider)
    {
        // Get the service from the navigation scope
        _personContext = navProvider.GetRequiredService<IPersonContext>();
    }

    // Use _personContext throughout the page
}
```

### Complete Example

```csharp
// 1. Define the context interface and implementation
public interface IOrderContext
{
    int OrderId { get; }
    Order Order { get; set; }
    decimal TotalAmount { get; }
    void RecalculateTotal();
}

public class OrderContext : IOrderContext
{
    public int OrderId { get; }
    public Order Order { get; set; }
    public decimal TotalAmount => Order.Items.Sum(i => i.Price * i.Quantity);
    
    public OrderContext(int orderId)
    {
        OrderId = orderId;
    }
    
    public void RecalculateTotal()
    {
        // Notify all pages using this context
        OnPropertyChanged(nameof(TotalAmount));
    }
}

// 2. Parent page provides the context
public class OrderPageModel : IEnteringAware<OrderIntent>
{
    private readonly INavigationServiceProvider _navProvider;

    public async ValueTask OnEnteringAsync(OrderIntent intent)
    {
        var order = await _orderService.GetOrderAsync(intent.OrderId);
        var context = new OrderContext(intent.OrderId) { Order = order };
        
        _navProvider.AddNavigationScoped<IOrderContext>(context);
    }
}

// 3. Child pages consume the context
public class OrderItemsPageModel : ObservableObject
{
    private readonly IOrderContext _orderContext;

    public OrderItemsPageModel(INavigationServiceProvider navProvider)
    {
        _orderContext = navProvider.GetRequiredService<IOrderContext>();
    }

    [RelayCommand]
    private Task AddItemAsync()
    {
        _orderContext.Order.Items.Add(newItem);
        _orderContext.RecalculateTotal();
        return Task.CompletedTask;
    }
}

public class OrderSummaryPageModel : ObservableObject
{
    private readonly IOrderContext _orderContext;

    public OrderSummaryPageModel(INavigationServiceProvider navProvider)
    {
        _orderContext = navProvider.GetRequiredService<IOrderContext>();
    }

    public decimal TotalAmount => _orderContext.TotalAmount;
}
```

### Accessing the Context Page

Get a reference to the page that created the scope:

```csharp
public class ChildPageModel : ObservableObject
{
    public ChildPageModel(INavigationServiceProvider navProvider)
    {
        // Get the parent page
        Page contextPage = navProvider.ContextPage;
        
        // Can access parent page properties/methods if needed
        if (contextPage.BindingContext is IParentContext parent)
        {
            // Interact with parent
        }
    }
}
```

### Nested Scopes

Child pages can create their own scopes:

```csharp
// Parent: Order scope
_navProvider.AddNavigationScoped<IOrderContext>(orderContext);

// Child: Payment scope (nested under order scope)
_navProvider.AddNavigationScoped<IPaymentContext>(paymentContext);

// Grandchild: Has access to both
var orderContext = _navProvider.GetRequiredService<IOrderContext>();
var paymentContext = _navProvider.GetRequiredService<IPaymentContext>();
```

### When to Use

| Use Navigation-Scoped Services | Use Intents |
|-------------------------------|-------------|
| Data shared across multiple nested pages | One-time parameter passing |
| Mutable shared state | Immutable initialization data |
| Complex context (shopping cart, wizard) | Simple values |
| Parent-child relationships | Peer-to-peer passing |

## Leak Detection

Nalu can automatically detect memory leaks during development.

### Enabling Leak Detection

```csharp
.UseNaluNavigation<App>(nav => nav
    .AddPage<MainPageModel, MainPage>() // ⚠️ For AOT compatibility, use AddPage for each page instead of AddPages()
    .WithLeakDetectorState(NavigationLeakDetectorState.EnabledWithDebugger)
)
```

**Options:**
- `Disabled` - No leak detection
- `EnabledWithDebugger` - Only when debugger is attached (recommended)
- `Enabled` - Always enabled

### How It Works

When a page is popped, Nalu monitors if it's garbage collected within a reasonable time. If not, you'll see an alert:

```
Memory Leak Detected!

Page 'ContactDetailPage' was not collected after navigation.
Check for event subscriptions or static references.
```

### Common Causes of Leaks

1. **Event subscriptions**

```csharp
// ❌ Bad - never unsubscribes
public class PageModel
{
    public PageModel(IEventAggregator events)
    {
        events.GetEvent<DataChanged>().Subscribe(OnDataChanged);
    }
}

// ✅ Good - unsubscribes
public class PageModel : ILeavingAware
{
    private readonly IEventAggregator _events;
    
    public PageModel(IEventAggregator events)
    {
        _events = events;
        _events.GetEvent<DataChanged>().Subscribe(OnDataChanged);
    }
    
    public ValueTask OnLeavingAsync()
    {
        _events.GetEvent<DataChanged>().Unsubscribe(OnDataChanged);
        return ValueTask.CompletedTask;
    }
}
```

2. **Static references**

```csharp
// ❌ Bad - static reference prevents GC
public static class Cache
{
    public static Page CurrentPage { get; set; }
}

// ✅ Good - use weak references if needed
public static class Cache
{
    public static WeakReference<Page> CurrentPage { get; set; }
}
```

3. **Timers not disposed**

```csharp
// ❌ Bad - timer keeps page alive
public class PageModel
{
    private Timer _timer = new Timer(Callback, null, 0, 1000);
}

// ✅ Good - dispose timer
public class PageModel : IDisposable
{
    private Timer _timer;
    
    public PageModel()
    {
        _timer = new Timer(Callback, null, 0, 1000);
    }
    
    public void Dispose()
    {
        _timer?.Dispose();
    }
}
```

4. **Long-running tasks**

```csharp
// ❌ Bad - task holds reference
public async ValueTask OnAppearingAsync()
{
    await Task.Run(async () =>
    {
        while (true)
        {
            await Task.Delay(1000);
            UpdateUI(); // Holds reference to page
        }
    });
}

// ✅ Good - use cancellation
public class PageModel : IAppearingAware, ILeavingAware, IDisposable
{
    private CancellationTokenSource _cts;
    
    public async ValueTask OnAppearingAsync()
    {
        _cts = new CancellationTokenSource();
        
        try
        {
            await Task.Run(async () =>
            {
                while (!_cts.Token.IsCancellationRequested)
                {
                    await Task.Delay(1000, _cts.Token);
                    UpdateUI();
                }
            }, _cts.Token);
        }
        catch (OperationCanceledException) { }
    }
    
    public ValueTask OnLeavingAsync()
    {
        _cts?.Cancel();
        return ValueTask.CompletedTask;
    }
    
    public void Dispose()
    {
        _cts?.Dispose();
    }
}
```

### Debugging Leaks

1. Enable leak detection
2. Navigate to a page and then away from it
3. If a leak is detected, check:
   - Event subscriptions (most common)
   - Timers and periodic tasks
   - Static references
   - Long-running operations
4. Add `IDisposable` and cleanup in `Dispose()` or `OnLeavingAsync()`

## Customizing Navigation Bar

Globally customize navigation icons:

```csharp
.UseNaluNavigation<App>(nav => nav
    .AddPage<MainPageModel, MainPage>() // ⚠️ For AOT compatibility, use AddPage for each page instead of AddPages()
    .WithMenuIcon(ImageSource.FromFile("menu.png"))
    .WithBackIcon(ImageSource.FromFile("back.png"))
)
```

## Monitoring Navigation Events

Subscribe to all navigation events at the Shell level:

```csharp
public partial class AppShell : NaluShell
{
    public AppShell(INavigationService navigationService) 
        : base(navigationService, typeof(MainPage))
    {
        InitializeComponent();
        NavigationEvent += OnNavigationEvent;
    }

    private void OnNavigationEvent(object? sender, NavigationLifecycleEventArgs e)
    {
        Debug.WriteLine($"Navigation Event: {e.EventType}");
        
        switch (e.EventType)
        {
            case NavigationLifecycleEventType.NavigationRequested:
                var info = (NavigationLifecycleInfo)e.Target;
                Debug.WriteLine($"  Requested: {info.RequestedNavigation}");
                Debug.WriteLine($"  From: {info.CurrentState}");
                Debug.WriteLine($"  To: {info.TargetState}");
                break;
                
            case NavigationLifecycleEventType.NavigationCompleted:
                // Track navigation for analytics
                LogNavigationToAnalytics(e);
                break;
                
            case NavigationLifecycleEventType.NavigationCanceled:
                // User guard blocked navigation
                Debug.WriteLine("  Navigation was blocked by guard");
                break;
                
            case NavigationLifecycleEventType.Entering:
            case NavigationLifecycleEventType.Appearing:
            case NavigationLifecycleEventType.Disappearing:
            case NavigationLifecycleEventType.Leaving:
                Debug.WriteLine($"  Page: {e.Target.GetType().Name}");
                if (e.Data != null)
                    Debug.WriteLine($"  Intent: {e.Data.GetType().Name}");
                break;
        }
    }
}
```

### Event Types

- `NavigationRequested` - Navigation starts
- `NavigationCompleted` - Navigation finished successfully
- `NavigationCanceled` - Blocked by guard
- `NavigationFailed` - Exception occurred
- `NavigationIgnored` - Navigation triggered on wrong state
- `Entering` / `Appearing` / `Disappearing` / `Leaving` - Page lifecycle
- `LeavingGuard` - Guard evaluation

### Use Cases

- **Analytics**: Track user navigation patterns
- **Logging**: Debug navigation issues
- **Performance**: Measure navigation timing
- **State management**: Update global state on navigation

## Back to Main Documentation

← [Back to Navigation Overview](navigation.md)

