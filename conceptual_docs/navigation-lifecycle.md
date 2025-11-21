# Navigation Lifecycle Events

Understanding when lifecycle events fire relative to navigation animations is crucial for optimal user experience.

## Quick Reference

| Interface | When It Fires | Common Use Cases | Fired Multiple Times? |
|-----------|--------------|------------------|----------------------|
| `IEnteringAware` | Before animation starts | Fast initialization, cache loading | ❌ Once per stack entry |
| `IAppearingAware` | After animation completes | Slow loading, refresh data | ✅ Yes (when returning from child) |
| `IDisappearingAware` | Before leaving (push/pop) | Pause updates, stop timers | ✅ Yes (when pushing child) |
| `ILeavingAware` | After disappearing, before removal | Save drafts, unsubscribe | ❌ Once when removed |
| `IDisposable` | After animation, page removed | Dispose resources | ❌ Once at disposal |
| `ILeavingGuard` | Before any navigation away | Prevent navigation, confirm action | As needed |

> 💡 **Quick tip**: Implement only the interfaces you need! Most pages only need one or two lifecycle events.
>
> 💡 Use `IEnteringAware` for fast operations (<30ms), `IAppearingAware` for slow operations, or the [Background Loading Pattern](#background-loading-pattern) for the best of both worlds.

## Event Timing Sequence

Here's the precise order of events during navigation:

1. **ILeavingGuard** → Evaluated before anything else (can block navigation)
2. **Entering** → Fires **before** navigation animation starts
3. **Navigation animation** plays
4. **Appearing** → Fires **after** navigation animation completes
5. **Disappearing** → Fires **before** navigation away starts (push or pop)
6. **Leaving** → Fires right after Disappearing, before the page is removed
7. **Navigation animation** completes
8. **Disposal** → DI Scope and all scoped instances are disposed

## Simple Examples

Most pages only need one or two lifecycle interfaces:

```csharp
// Simple page: just load data when appearing
public class ContactListPageModel : ObservableObject, IAppearingAware
{
    public async ValueTask OnAppearingAsync()
    {
        await LoadContactsAsync();
    }
}
```

```csharp
// Page with timer: needs startup and cleanup
public class DashboardPageModel : ObservableObject, IAppearingAware, IDisposable
{
    private readonly Timer _refreshTimer;

    public DashboardPageModel()
    {
        _refreshTimer = new Timer(OnRefresh, null, Timeout.Infinite, Timeout.Infinite);
    }

    public ValueTask OnAppearingAsync()
    {
        _refreshTimer.Change(TimeSpan.Zero, TimeSpan.FromSeconds(30));
        return ValueTask.CompletedTask;
    }

    public void Dispose()
    {
        _refreshTimer?.Dispose();
    }
}
```

```csharp
// Page with intent: receive data during navigation
public class ProductDetailPageModel : ObservableObject, IEnteringAware<ProductIntent>
{
    public async ValueTask OnEnteringAsync(ProductIntent intent)
    {
        await LoadProductAsync(intent.ProductId);
    }
}
```

## Choosing Between IEnteringAware and IAppearingAware

The timing difference is critical for UX:

| Use IEnteringAware when... | Use IAppearingAware when... |
|---------------------------|----------------------------|
| You need data ready before the page appears | The operation is slow and would delay animation |
| Loading is fast (<30ms) | You can show a loading indicator |
| You want smooth, prepared content despite a potential delay in the navigation | Fast navigation is more important than having prepared content |
| One-time initialization when entering stack | Operations that should run every time page becomes visible |

**Critical difference**: 
- `OnEnteringAsync` is called **once** when the page enters the navigation stack
- `OnAppearingAware` is called **every time** the page becomes visible (initial appearance + returning from child pages)

**For very slow operations** (>500ms), consider the [Background Loading Pattern](#background-loading-pattern) to start loading in `OnEnteringAsync` without blocking navigation.

### Example: Entering (blocks animation)

```csharp
public class ContactDetailPageModel : ObservableObject, IEnteringAware
{
    public async ValueTask OnEnteringAsync()
    {
        // This blocks the navigation animation but ensures data is ready
        // User sees: [Current Page] → (slight delay) → [Animated transition] → [Fully loaded page]
        await LoadContactAsync();
    }
}
```

**User experience**: Brief delay → Smooth animation → Complete page

### Example: Appearing (shows loading state)

```csharp
public class ContactDetailPageModel : ObservableObject, IAppearingAware
{
    [ObservableProperty]
    private bool _isLoading = true;

    public async ValueTask OnAppearingAsync()
    {
        // Animation plays immediately, then data loads
        // User sees: [Current Page] → [Animated transition] → [Page with loading spinner] → [Loaded page]
        try
        {
            await RefreshContactAsync();
        }
        finally
        {
            IsLoading = false;
        }
    }
}
```

**User experience**: Instant animation → Loading indicator → Complete page

## Lifecycle Interfaces

### IEnteringAware

Called **before the navigation animation starts** when a page is pushed onto the stack.

```csharp
public interface IEnteringAware
{
    ValueTask OnEnteringAsync();
}
```

**Use cases:**
- Quick initialization (<100ms)
- Loading cached data
- Setting up UI state that must be ready immediately

**Example:**

```csharp
public class ProductDetailPageModel : ObservableObject, IEnteringAware
{
    private readonly IProductCache _cache;

    public async ValueTask OnEnteringAsync()
    {
        // Fast: Load from cache
        Product = await _cache.GetProductAsync(ProductId);
        
        // Don't do this here - too slow:
        // Product = await _api.GetProductAsync(ProductId);
    }
}
```

> ⚠️ **Performance Warning**: Every millisecond here is perceived as navigation lag. Keep operations under 30ms.

### IAppearingAware

Called **after the navigation animation completes** when the page becomes visible.

```csharp
public interface IAppearingAware
{
    ValueTask OnAppearingAsync();
}
```

**Important**: `OnAppearingAsync` is called in TWO scenarios:
1. When the page first appears (after `OnEnteringAsync`)
2. **When returning to the page from a child page** (after the child is popped)

**Use cases:**
- Loading/refreshing data from network or database
- Starting animations or timers
- Resuming real-time updates
- Any operation that should run whenever the page becomes visible

**Example:**

```csharp
public class ProductListPageModel : ObservableObject, IAppearingAware
{
    [ObservableProperty]
    private bool _isRefreshing;

    public async ValueTask OnAppearingAsync()
    {
        IsRefreshing = true;
        try
        {
            // This runs on initial appearance AND when coming back from ProductDetailPage
            await RefreshProductsAsync();
            StartRealtimeUpdates();
        }
        finally
        {
            IsRefreshing = false;
        }
    }
}
```

> ✅ **Best Practice**: Use this for operations that should run whenever the page becomes visible, including when returning from child pages. Perfect for refreshing data and resuming animations/updates.

### IDisappearingAware

Called **before the navigation animation starts** when the page is about to be hidden.

```csharp
public interface IDisappearingAware
{
    ValueTask OnDisappearingAsync();
}
```

**Important**: `OnDisappearingAsync` is called in TWO scenarios:
1. When navigating away by **pushing a child page** (page stays in stack but becomes hidden)
2. When navigating away by **popping** this page (page will be removed)

**Use cases:**
- Pausing animations
- Stopping timers
- Pausing video/audio playback
- Stopping real-time updates
- Saving temporary state

**Example:**

```csharp
public class VideoPlayerPageModel : ObservableObject, IDisappearingAware
{
    private readonly IVideoPlayer _player;

    public ValueTask OnDisappearingAsync()
    {
        // Called when pushing child page OR when being popped
        // Pause playback while page is not visible
        _player.Pause();
        
        // Save current position
        SavePlaybackPosition(_player.CurrentPosition);
        
        // Stop real-time updates
        StopRealtimeUpdates();
        
        return ValueTask.CompletedTask;
    }
}
```

> 💡 **Key insight**: When pushing a child page, `OnDisappearingAsync` is called but `OnLeavingAsync` is NOT. The page stays in the stack and will receive `OnAppearingAsync` when you return to it.

### ILeavingAware

Called **right after Disappearing** and before the page is removed from the stack.

```csharp
public interface ILeavingAware
{
    ValueTask OnLeavingAsync();
}
```

**Use cases:**
- Final cleanup before disposal
- Saving drafts or form data
- Unsubscribing from events
- Releasing resources

**Example:**

```csharp
public class EditFormPageModel : ObservableObject, ILeavingAware
{
    private readonly IEventAggregator _events;

    public async ValueTask OnLeavingAsync()
    {
        // Save draft before leaving
        await SaveDraftAsync();
        
        // Unsubscribe from events
        _events.GetEvent<DataUpdatedEvent>().Unsubscribe(OnDataUpdated);
    }
}
```

> 📝 **Note**: After this event, the page is removed and the DI Scope is disposed once the navigation animation completes.

### IDisposable

Automatically called **after the navigation animation completes** for all scoped services.

```csharp
public class MyPageModel : ObservableObject, IDisposable
{
    private readonly Timer _timer;
    private readonly HttpClient _httpClient;

    public void Dispose()
    {
        // Clean up unmanaged resources
        _timer?.Dispose();
        _httpClient?.Dispose();
    }
}
```

**When to use:**
- Disposing timers, HttpClient, or other IDisposable resources
- Releasing unmanaged resources
- Final cleanup after all lifecycle events

> 💡 **Tip**: You can use both `ILeavingAware` (for early cleanup) and `IDisposable` (for resource disposal).

## Complete Lifecycle Example

This example demonstrates **all** lifecycle interfaces for educational purposes. In practice, **most pages only need 1-2 interfaces**.

```csharp
public class CompletePageModel : ObservableObject, 
    IEnteringAware, 
    IAppearingAware,
    IDisappearingAware,
    ILeavingAware,
    IDisposable
{
    private readonly ILogger<CompletePageModel> _logger;
    private readonly Timer _refreshTimer;
    private readonly IEventAggregator _events;

    public CompletePageModel(ILogger<CompletePageModel> logger, IEventAggregator events)
    {
        _logger = logger;
        _events = events;
        _refreshTimer = new Timer(OnRefreshTimer, null, Timeout.Infinite, Timeout.Infinite);
    }

    public async ValueTask OnEnteringAsync()
    {
        _logger.LogDebug("1. Entering - before animation");
        
        // Quick setup - loads from cache
        Data = await LoadFromCacheAsync();
    }

    public async ValueTask OnAppearingAsync()
    {
        _logger.LogDebug("2. Appearing - after animation");
        
        // Slower operations - fetch fresh data
        await RefreshDataAsync();
        
        // Start periodic refresh
        _refreshTimer.Change(TimeSpan.Zero, TimeSpan.FromMinutes(1));
        
        // Subscribe to events
        _events.GetEvent<DataChangedEvent>().Subscribe(OnDataChanged);
    }

    public ValueTask OnDisappearingAsync()
    {
        _logger.LogDebug("3. Disappearing - about to leave");
        
        // Stop the timer
        _refreshTimer.Change(Timeout.Infinite, Timeout.Infinite);
        
        return ValueTask.CompletedTask;
    }

    public ValueTask OnLeavingAsync()
    {
        _logger.LogDebug("4. Leaving - removing from stack");
        
        // Unsubscribe from events
        _events.GetEvent<DataChangedEvent>().Unsubscribe(OnDataChanged);
        
        // Save any pending changes
        SavePendingChanges();
        
        return ValueTask.CompletedTask;
    }

    public void Dispose()
    {
        _logger.LogDebug("5. Dispose - after animation completed");
        
        // Dispose unmanaged resources
        _refreshTimer?.Dispose();
    }
}
```

## Intent-Aware Lifecycle Events

You can receive strongly-typed data during lifecycle events:

```csharp
public class DetailPageModel : IEnteringAware<DetailIntent>
{
    public async ValueTask OnEnteringAsync(DetailIntent intent)
    {
        // Receive intent data
        await LoadDetailAsync(intent.ItemId);
    }
}
```

See [Navigation Intents](navigation-intents.md) for more details.

## Special Cases

### Navigation Within Lifecycle Events

**Important**: Lifecycle events are awaited by the navigation system, so you **cannot directly trigger another navigation** from within them. You must dispatch it to avoid blocking.

```csharp
// ❌ Wrong - this will block/deadlock
public async ValueTask OnAppearingAsync()
{
    await LoadDataAsync();
    await _navigationService.GoToAsync(Navigation.Relative().Push<NextPageModel>());
}

// ✅ Correct - dispatch the navigation
public async ValueTask OnAppearingAsync(StartupIntent intent)
{
    await LoadDataAsync();
    
    // Dispatch navigation to avoid blocking
    _ = _dispatcher.DispatchAsync(() => 
        _navigationService.GoToAsync(Navigation.Relative().Push<NextPageModel>())
    );
}

// ✅ Alternative - use a separate method
public async ValueTask OnAppearingAsync()
{
    await LoadDataAsync();
    NavigateNext(); // Fire and forget
}

private async void NavigateNext()
{
    await _navigationService.GoToAsync(Navigation.Relative().Push<NextPageModel>());
}
```

**Real-world example from Weather sample:**

```csharp
public class InitializationPageModel : ObservableObject, IAppearingAware<StartupIntent>
{
    public async ValueTask OnAppearingAsync(StartupIntent intent)
    {
        // Load data
        await LoadWeatherDataAsync();
        
        // Dispatch navigation to home page
        _ = _dispatcher.DispatchAsync(NavigateToHomePage);
    }

    private Task NavigateToHomePage()
    {
        return _navigationService.GoToAsync(
            Navigation.Absolute(NavigationBehavior.Immediate)
                .Root<HomePageModel>()
        );
    }
}
```

### Page Appears Without Animation

When switching between tabs in the same `ShellItem`, `OnAppearingAsync` fires without a navigation animation.

### Intermediate Pages

When navigating with multiple pushes/pops in one navigation (e.g., `Pop().Pop().Push()`), intermediate pages won't trigger `OnAppearingAsync` - only the final target page will.

### Guard Evaluation

When `ILeavingGuard` needs evaluation on an intermediate page, that page will trigger `OnAppearingAsync` temporarily to allow the guard prompt to be shown.

## Performance Tips

1. **Measure your operations**: Use a stopwatch to ensure `OnEnteringAsync` operations stay under 30ms
2. **Use caching**: Load from cache in `OnEnteringAsync`, refresh from network in `OnAppearingAsync`
3. **Background loading for slow operations**: For operations >500ms, use the [Background Loading Pattern](#background-loading-pattern) to avoid blocking navigation
4. **Show progress indicators**: In `OnAppearingAsync` (or directly in `OnEnteringAsync`), set `IsLoading = true` immediately
5. **Debounce rapid navigation**: Consider using rate limiting for rapid back-and-forth navigation
6. **Lazy load**: Don't load everything upfront - load sections as needed (`ToggleTemplate` may help here)

## Common Patterns

### Fast Initial Load + Background Refresh

```csharp
public class ProductPageModel : IEnteringAware, IAppearingAware
{
    public async ValueTask OnEnteringAsync()
    {
        // Fast: Load cached or placeholder data
        Product = await _cache.GetProductAsync(ProductId) ?? Product.Placeholder;
    }

    public async ValueTask OnAppearingAsync()
    {
        // Slow: Refresh from API
        var freshProduct = await _api.GetProductAsync(ProductId);
        Product = freshProduct;
        await _cache.SaveProductAsync(freshProduct);
    }
}
```

### Initialization/Splash Pattern

```csharp
public class InitializationPageModel : IAppearingAware<StartupIntent>
{
    private readonly IDispatcher _dispatcher;
    private readonly INavigationService _navigationService;

    public async ValueTask OnAppearingAsync(StartupIntent intent)
    {
        // Do initialization work
        await LoginUserAsync();
        await LoadEssentialDataAsync();
        
        // Navigate to main app - MUST dispatch!
        _ = _dispatcher.DispatchAsync(NavigateToHome);
    }

    private Task NavigateToHome()
    {
        return _navigationService.GoToAsync(
            Navigation.Absolute(NavigationBehavior.Immediate).Root<HomePageModel>()
        );
    }
}
```

### Background Loading Pattern

For slow operations that you want to start in `OnEnteringAsync` without blocking navigation.

**Why this pattern is needed:**

Without this pattern, slow operations in `OnEnteringAsync` would:
- ❌ **Block the navigation animation** - making navigation feel sluggish (every millisecond counts!)
- ❌ **Cause unwanted side effects** - if the user changes their mind and pops back immediately, the slow operation would still complete and potentially update state on a page that's no longer visible

This pattern solves both problems by:
- ✅ Starting the operation immediately (best user experience)
- ✅ Not blocking the navigation animation (stays fast and responsive)
- ✅ Cancelling cleanly if the user navigates away (no wasted work or side effects)

**Example:**

```csharp
// Intent definition
public record ProductIntent(int ProductId);

// Page model with background loading
public class ProductPageModel : 
    IEnteringAware<ProductIntent>,
    IAppearingAware,
    IDisappearingAware,
    ILeavingAware,
    IDisposable
{
    private readonly IProductService _productService;
    private CancellationTokenSource? _leavingCts;
    private Task? _loadingTask;
    private int _productId;

    [ObservableProperty]
    private bool _isLoading = true;

    [ObservableProperty]
    private Product? _product;

    public ValueTask OnEnteringAsync(ProductIntent intent)
    {
        _productId = intent.ProductId;
        
        // Start slow loading without blocking navigation animation
        _leavingCts = new CancellationTokenSource();
        _loadingTask = LoadProductAsync(_leavingCts.Token);
        
        // Return immediately - navigation animation continues
        return ValueTask.CompletedTask;
    }

    private async Task LoadProductAsync(CancellationToken cancellationToken)
    {
        try
        {
            IsLoading = true;
            
            // This can be very slow - navigation already happened
            Product = await _productService.GetProductAsync(_productId, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // User navigated away before loading completed
        }
        catch (Exception ex)
        {
            // Handle error
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }

    public ValueTask OnAppearingAsync()
    {
        // Page appeared - loading may still be in progress
        // UI can show loading indicator
        return ValueTask.CompletedTask;
    }

    public ValueTask OnDisappearingAsync()
    {
        // ⚠️ DON'T cancel here! 
        // If user navigates to a child page (e.g., image viewer), 
        // we want loading to continue while page is in the stack.
        // Only cancel in OnLeavingAsync when page is actually being removed.
        return ValueTask.CompletedTask;
    }

    public async ValueTask OnLeavingAsync()
    {
        // Cancel ongoing loading
        _leavingCts?.Cancel();
        
        // Wait for the task to complete (important for cleanup)
        if (_loadingTask != null)
        {
            try
            {
                await _loadingTask;
            }
            catch (OperationCanceledException)
            {
                // Expected when canceling
            }
        }
        
        return;
    }

    public void Dispose()
    {
        _leavingCts?.Dispose();
    }
}
```

**Key benefits:**
- ✅ **Fast navigation**: Animation is never blocked, navigation feels instant
- ✅ **Early loading**: Work starts immediately in `OnEnteringAsync`
- ✅ **No side effects**: If user pops back immediately, operation is cancelled cleanly
- ✅ **Safe cleanup**: Task is properly awaited before page disposal
- ✅ **Continues during child navigation**: Loading continues if user navigates to a child page

**When to use:**
- Slow operations (>500ms) that you want to start as early as possible
- Operations that can be cancelled gracefully
- When you want to avoid blocking navigation animation
- When users might navigate away quickly (before loading completes)

**Cleanup pattern note:**
- `_leavingCts` is created in `OnEnteringAsync` but disposed in `Dispose()` (not `OnLeavingAsync`)
- This follows the rule: **logical cleanup** (Cancel) in `OnLeavingAsync`, **resource disposal** (Dispose) in `Dispose()`
- The task itself is **awaited** in `OnLeavingAsync` to ensure it completes before page disposal

### Loading Indicator Pattern

```csharp
public class SearchPageModel : IAppearingAware
{
    [ObservableProperty]
    private bool _isLoading;
    
    [ObservableProperty]
    private string _errorMessage;

    public async ValueTask OnAppearingAsync()
    {
        IsLoading = true;
        ErrorMessage = null;
        
        try
        {
            Results = await _searchService.SearchAsync(Query);
        }
        catch (Exception ex)
        {
            ErrorMessage = "Failed to load results";
        }
        finally
        {
            IsLoading = false;
        }
    }
}
```

### Cleanup Pattern

**Important rule**: Match cleanup to creation based on lifecycle scope:
- **Constructor** creates → **Dispose** cleans up (page lifetime)
- **OnEnteringAsync** subscribes → **OnLeavingAsync** unsubscribes (stack lifetime)
- **OnAppearingAsync** starts → **OnDisappearingAsync** pauses/stops (visibility lifetime)

**Key insight**: `OnAppearingAsync` and `OnDisappearingAsync` can be called multiple times as you navigate to/from child pages, while `OnEnteringAsync` and `OnLeavingAsync` are called only once per stack entry.

> ⚠️ **Note**: You don't need to implement all interfaces! Only implement the lifecycle events your page actually needs. The example below shows all interfaces for demonstration purposes.

```csharp
public class ProductListPageModel : 
    IEnteringAware,
    IAppearingAware,
    IDisappearingAware,
    ILeavingAware,
    IDisposable
{
    private readonly Timer _heartbeatTimer;
    private IDisposable? _dataChangedSubscription;
    private IDisposable? _realtimeSubscription;

    public ProductListPageModel()
    {
        // Created in constructor → cleaned in Dispose (page lifetime)
        _heartbeatTimer = new Timer(OnHeartbeat, null, Timeout.Infinite, Timeout.Infinite);
    }

    public ValueTask OnEnteringAsync()
    {
        // Subscribe to data change events - stays active while page is in stack
        // Created in Entering → cleaned in Leaving (stack lifetime)
        _dataChangedSubscription = _dataService.SubscribeToChanges(OnDataChanged);
        return ValueTask.CompletedTask;
    }

    public ValueTask OnAppearingAsync()
    {
        // Called when page first appears AND when returning from child pages
        // Start real-time updates - active only while visible
        // Started in Appearing → stopped in Disappearing (visibility lifetime)
        _realtimeSubscription = _realtimeService.Subscribe(OnRealtimeUpdate);
        
        // Start periodic refresh
        _heartbeatTimer.Change(TimeSpan.Zero, TimeSpan.FromSeconds(30));
        
        // Refresh data when returning from detail page
        _ = RefreshDataAsync();
        
        return ValueTask.CompletedTask;
    }

    public ValueTask OnDisappearingAsync()
    {
        // Called when navigating to child page OR when being popped
        // Pause/stop things that should only run when visible
        
        _realtimeSubscription?.Dispose();
        _realtimeSubscription = null;
        
        _heartbeatTimer.Change(Timeout.Infinite, Timeout.Infinite);
        
        return ValueTask.CompletedTask;
    }

    public ValueTask OnLeavingAsync()
    {
        // Called only when being removed from stack
        // Clean up stack-lifetime resources
        
        _dataChangedSubscription?.Dispose();
        _dataChangedSubscription = null;
        return ValueTask.CompletedTask;
    }

    public void Dispose()
    {
        // Clean up page-lifetime resources
        _heartbeatTimer?.Dispose();
        
        // Safety cleanup
        _dataChangedSubscription?.Dispose();
        _realtimeSubscription?.Dispose();
    }
}
```

**Navigation flow example:**

```
1. ProductListPage.OnEnteringAsync()     // Subscribe to data changes
2. ProductListPage.OnAppearingAsync()    // Start real-time updates
   → User taps a product
3. ProductListPage.OnDisappearingAsync() // Pause real-time updates
4. ProductDetailPage.OnEnteringAsync()   
5. ProductDetailPage.OnAppearingAsync()  
   → User presses back
6. ProductDetailPage.OnDisappearingAsync()
7. ProductDetailPage.OnLeavingAsync()    
8. ProductDetailPage.Dispose()           
9. ProductListPage.OnAppearingAsync()    // Resume real-time updates + refresh data
   → User navigates away completely
10. ProductListPage.OnDisappearingAsync()
11. ProductListPage.OnLeavingAsync()     // Unsubscribe from data changes
12. ProductListPage.Dispose()
```

## Back to Main Documentation

← [Back to Navigation Overview](navigation.md)

