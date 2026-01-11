# Testing and Troubleshooting

Guide to unit testing navigation and solving common issues.

## Unit Testing Navigation

### Basic Navigation Test

```csharp
using NSubstitute;
using Xunit;

public class MainPageModelTests
{
    [Fact]
    public async Task NavigateToDetails_CallsNavigationService()
    {
        // Arrange
        var navigationService = Substitute.For<INavigationService>();
        navigationService.GoToAsync(Arg.Any<INavigationInfo>())
            .Returns(Task.FromResult(true));
        
        var viewModel = new MainPageModel(navigationService);

        // Act
        await viewModel.NavigateToDetailsAsync();

        // Assert
        await navigationService.Received(1).GoToAsync(Arg.Any<INavigationInfo>());
    }
}
```

### Testing Specific Navigation

```csharp
[Fact]
public async Task ViewProduct_NavigatesWithCorrectPageType()
{
    // Arrange
    var navigationService = Substitute.For<INavigationService>();
    var viewModel = new ProductListViewModel(navigationService);

    // Act
    await viewModel.ViewProductAsync();

    // Assert
    await navigationService.Received().GoToAsync(
        Arg.Is<INavigationInfo>(nav => 
            nav.Count == 1 && 
            nav[0].Type == typeof(ProductDetailPage)
        )
    );
}
```

### Testing Navigation with Intents

Use `record` types for automatic value equality:

```csharp
// Define intent as record
public record ProductIntent(int ProductId);

[Fact]
public async Task ViewProduct_PassesCorrectIntent()
{
    // Arrange
    var navigationService = Substitute.For<INavigationService>();
    var viewModel = new ProductListViewModel(navigationService);

    // Act
    await viewModel.ViewProductAsync(42);

    // Assert
    var expectedNav = Navigation.Relative()
        .Push<ProductDetailPageModel>()
        .WithIntent(new ProductIntent(42));

    await navigationService.Received().GoToAsync(
        Arg.Is<INavigationInfo>(n => n.Matches(expectedNav))
    );
}
```

### Testing Multiple Navigation Steps

```csharp
[Fact]
public async Task ComplexNavigation_BuildsCorrectPath()
{
    // Arrange
    var navigationService = Substitute.For<INavigationService>();
    var viewModel = new MyViewModel(navigationService);

    // Act
    await viewModel.ReplacePageAsync();

    // Assert - Pop then Push
    await navigationService.Received().GoToAsync(
        Arg.Is<INavigationInfo>(nav =>
            nav.Count == 2 &&
            nav[0] is NavigationPop &&
            nav[1].Type == typeof(NewPage)
        )
    );
}
```

### Testing Absolute Navigation

```csharp
[Fact]
public async Task NavigateToSettings_UsesAbsoluteNavigation()
{
    // Arrange
    var navigationService = Substitute.For<INavigationService>();
    var viewModel = new MainViewModel(navigationService);

    // Act
    await viewModel.GoToSettingsAsync();

    // Assert
    await navigationService.Received().GoToAsync(
        Arg.Is<INavigationInfo>(nav => 
            nav.IsAbsolute &&
            nav[0].Type == typeof(SettingsPage)
        )
    );
}
```

### Testing Navigation Guards

```csharp
[Fact]
public async Task CanLeaveAsync_WithUnsavedChanges_ReturnsFalse()
{
    // Arrange
    var viewModel = new EditPageModel();
    viewModel.Data = "modified";
    viewModel.MarkAsModified();

    // Act
    var canLeave = await viewModel.CanLeaveAsync();

    // Assert
    Assert.False(canLeave);
}

[Fact]
public async Task CanLeaveAsync_WithoutChanges_ReturnsTrue()
{
    // Arrange
    var viewModel = new EditPageModel();

    // Act
    var canLeave = await viewModel.CanLeaveAsync();

    // Assert
    Assert.True(canLeave);
}
```

### Testing Lifecycle Events

```csharp
[Fact]
public async Task OnEnteringAsync_WithIntent_LoadsProduct()
{
    // Arrange
    var productService = Substitute.For<IProductService>();
    var product = new Product { Id = 42, Name = "Test" };
    productService.GetProductAsync(42).Returns(product);
    
    var viewModel = new ProductDetailViewModel(productService);
    var intent = new ProductIntent(42);

    // Act
    await viewModel.OnEnteringAsync(intent);

    // Assert
    Assert.Equal(product, viewModel.Product);
    await productService.Received(1).GetProductAsync(42);
}

[Fact]
public async Task OnAppearingAsync_RefreshesData()
{
    // Arrange
    var dataService = Substitute.For<IDataService>();
    var viewModel = new MyViewModel(dataService);

    // Act
    await viewModel.OnAppearingAsync();

    // Assert
    await dataService.Received(1).RefreshAsync();
}
```

### Testing Intent Reception

```csharp
public class ProductDetailViewModelTests
{
    [Fact]
    public async Task OnEnteringAsync_WithValidIntent_LoadsCorrectProduct()
    {
        // Arrange
        var repository = Substitute.For<IProductRepository>();
        var expectedProduct = new Product { Id = 123, Name = "Widget" };
        repository.GetByIdAsync(123).Returns(expectedProduct);
        
        var viewModel = new ProductDetailViewModel(repository);
        var intent = new ProductIntent(123);

        // Act
        await viewModel.OnEnteringAsync(intent);

        // Assert
        Assert.Equal(expectedProduct, viewModel.Product);
        Assert.Equal(123, viewModel.ProductId);
    }

    [Fact]
    public async Task OnEnteringAsync_WithInvalidId_SetsErrorState()
    {
        // Arrange
        var repository = Substitute.For<IProductRepository>();
        repository.GetByIdAsync(Arg.Any<int>())
            .Returns(Task.FromException<Product>(new NotFoundException()));
        
        var viewModel = new ProductDetailViewModel(repository);
        var intent = new ProductIntent(999);

        // Act
        await viewModel.OnEnteringAsync(intent);

        // Assert
        Assert.True(viewModel.HasError);
        Assert.NotNull(viewModel.ErrorMessage);
    }
}
```

### Testing Awaitable Intents

**Testing the page that sets the result:**

```csharp
[Fact]
public async Task SelectContact_SetsResultOnIntent()
{
    // Arrange
    var navigationService = Substitute.For<INavigationService>();
    var viewModel = new ContactSelectionViewModel(navigationService);
    var intent = new SelectContactIntent();
    await viewModel.OnEnteringAsync(intent);
    
    var contact = new Contact { Id = 1, Name = "John" };

    // Act
    await viewModel.SelectContactCommand.ExecuteAsync(contact);

    // Assert - intent should have result set
    var result = await intent;
    Assert.Equal(contact, result);
    
    // Assert - should pop after setting result
    await navigationService.Received().GoToAsync(
        Arg.Is<INavigationInfo>(n => n[0] is NavigationPop)
    );
}

[Fact]
public async Task Cancel_SetsNullOnIntent()
{
    // Arrange
    var navigationService = Substitute.For<INavigationService>();
    var viewModel = new ContactSelectionViewModel(navigationService);
    var intent = new SelectContactIntent();
    await viewModel.OnEnteringAsync(intent);

    // Act
    await viewModel.CancelCommand.ExecuteAsync(null);

    // Assert
    var result = await intent;
    Assert.Null(result);
}

[Fact]
public async Task Error_SetsExceptionOnIntent()
{
    // Arrange
    var viewModel = new ContactSelectionViewModel();
    var intent = new SelectContactIntent();
    await viewModel.OnEnteringAsync(intent);

    // Act
    await viewModel.TriggerErrorAsync();

    // Assert
    await Assert.ThrowsAsync<ValidationException>(async () => await intent);
}
```

**Testing ResolveIntentAsync usage:**

```csharp
[Fact]
public async Task EditItem_UsesResolveIntentAsync()
{
    // Arrange
    var navigationService = Substitute.For<INavigationService>();
    var item = new Item { Id = 1, Name = "Test" };
    
    // Setup ResolveIntentAsync to return modified item
    navigationService
        .ResolveIntentAsync<ItemEditorPageModel, Item?>(Arg.Any<EditItemIntent>())
        .Returns(Task.FromResult<Item?>(item));
    
    var viewModel = new ItemListViewModel(navigationService);

    // Act
    await viewModel.EditItemCommand.ExecuteAsync(item);

    // Assert
    await navigationService.Received().ResolveIntentAsync<ItemEditorPageModel, Item?>(
        Arg.Is<EditItemIntent>(i => i.Item.Id == item.Id)
    );
}
```

### Integration Tests

For integration tests, use a real `NavigationService`:

```csharp
public class NavigationIntegrationTests
{
    [Fact]
    public async Task FullNavigationFlow_WorksCorrectly()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddScoped<MainPage>();
        services.AddScoped<MainPageModel>();
        services.AddScoped<DetailPage>();
        services.AddScoped<DetailPageModel>();
        
        var config = new NavigationConfiguration();
        var serviceProvider = services.BuildServiceProvider();
        var navigationService = new NavigationService(config, serviceProvider);

        // Act & Assert
        var result = await navigationService.GoToAsync(
            Navigation.Relative().Push<DetailPageModel>()
        );
        
        Assert.True(result);
    }
}
```

### Testing Best Practices

1. **Use `record` for intents** - automatic value equality makes assertions easier
2. **Test navigation, not navigation service** - verify your logic, not the framework
3. **Mock dependencies** - focus on the ViewModel behavior
4. **Test guards separately** - they have specific logic worth isolating
5. **Test lifecycle events** - ensure proper initialization and cleanup
6. **Use helper methods** - reduce boilerplate in test setup

**Helper method example:**

```csharp
public class TestHelpers
{
    public static INavigationService CreateMockNavigationService()
    {
        var nav = Substitute.For<INavigationService>();
        nav.GoToAsync(Arg.Any<INavigationInfo>()).Returns(Task.FromResult(true));
        return nav;
    }

    public static void VerifyNavigation<TPage>(
        INavigationService nav, 
        Times times = default)
    {
        nav.Received(times ?? 1).GoToAsync(
            Arg.Is<INavigationInfo>(n => n[0].Type == typeof(TPage))
        );
    }
}
```

## Troubleshooting

### Navigation Not Working

**Symptoms**: Navigation call doesn't navigate, no errors thrown

**Checklist**:

1. ✅ Verify `NaluShell` inheritance

```csharp
// ✅ Correct
public partial class AppShell : NaluShell

// ❌ Wrong
public partial class AppShell : Shell
```

2. ✅ Check `nalu:Navigation.PageType` on all `ShellContent`

```xml
<!-- ✅ Correct -->
<ShellContent nalu:Navigation.PageType="pages:MainPage" />

<!-- ❌ Wrong - missing PageType -->
<ShellContent Title="Main" />
```

3. ✅ Verify page registration

```csharp
// Manual registration
.UseNaluNavigation<App>(nav => nav
    .AddPage<MainPageModel, MainPage>()
)

// Or auto-discovery (⚠️ not AOT-compatible - use AddPage for each page instead)
.UseNaluNavigation<App>(nav => nav.AddPages())
```

4. ✅ Confirm page constructor signature

```csharp
// ✅ Correct
public partial class MainPage : ContentPage
{
    public MainPage(MainPageModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}

// ❌ Wrong - missing ViewModel parameter
public partial class MainPage : ContentPage
{
    public MainPage()
    {
        InitializeComponent();
    }
}
```

5. ✅ Check ViewModel implements `INotifyPropertyChanged`

```csharp
// ✅ Correct
public class MainPageModel : ObservableObject

// ❌ Wrong - plain class
public class MainPageModel
```

### Pages Not Disposing / Memory Leaks

**Symptoms**: Leak detector alerts, increasing memory usage

**Solutions**:

1. **Unsubscribe from events**

```csharp
public class MyPageModel : IAppearingAware, ILeavingAware
{
    private readonly IEventAggregator _events;
    
    public ValueTask OnAppearingAsync()
    {
        _events.Subscribe<MyEvent>(OnMyEvent);
        return ValueTask.CompletedTask;
    }
    
    public ValueTask OnLeavingAsync()
    {
        // ✅ Critical: Unsubscribe!
        _events.Unsubscribe<MyEvent>(OnMyEvent);
        return ValueTask.CompletedTask;
    }
}
```

2. **Dispose timers and resources**

```csharp
public class MyPageModel : IDisposable
{
    private Timer? _timer;
    private HttpClient? _httpClient;
    
    public void Dispose()
    {
        _timer?.Dispose();
        _httpClient?.Dispose();
    }
}
```

3. **Cancel long-running operations**

```csharp
public class MyPageModel : IAppearingAware, ILeavingAware, IDisposable
{
    private CancellationTokenSource? _cts;
    
    public async ValueTask OnAppearingAsync()
    {
        _cts = new CancellationTokenSource();
        await LongRunningOperationAsync(_cts.Token);
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

4. **Avoid static references**

```csharp
// ❌ Bad - prevents GC
public static Page CurrentPage { get; set; }

// ✅ Good - use weak reference if needed
public static WeakReference<Page>? CurrentPageRef { get; set; }
```

5. **Enable leak detection**

```csharp
.UseNaluNavigation<App>(nav => nav
    .AddPage<MainPageModel, MainPage>() // ⚠️ For AOT compatibility, use AddPage for each page instead of AddPages()
    .WithLeakDetectorState(NavigationLeakDetectorState.EnabledWithDebugger)
)
```

### Intent Not Received

**Symptoms**: Lifecycle method doesn't receive the intent

**Solutions**:

1. **Verify interface implementation**

```csharp
// ✅ Correct - implements IEnteringAware<TIntent>
public class DetailPageModel : IEnteringAware<ProductIntent>
{
    public ValueTask OnEnteringAsync(ProductIntent intent)
    {
        // Will receive intent
    }
}

// ❌ Wrong - missing generic interface
public class DetailPageModel : IEnteringAware
{
    public ValueTask OnEnteringAsync()
    {
        // Won't receive intent
    }
}
```

2. **Check intent type matches exactly**

```csharp
// Navigation
.WithIntent(new ProductIntent(42))

// ✅ Matches
public class PageModel : IEnteringAware<ProductIntent>

// ❌ Doesn't match - no inheritance support
public class PageModel : IEnteringAware<BaseIntent>
```

3. **Verify intent behavior configuration**

```csharp
// Strict mode (default) - only intent-aware method called
.WithNavigationIntentBehavior(NavigationIntentBehavior.Strict)

// Fallthrough mode - both methods called
.WithNavigationIntentBehavior(NavigationIntentBehavior.Fallthrough)
```

4. **Ensure intent is passed**

```csharp
// ✅ Correct - intent included
Navigation.Relative()
    .Push<DetailPageModel>()
    .WithIntent(new ProductIntent(42))

// ❌ Wrong - no intent
Navigation.Relative()
    .Push<DetailPageModel>()
```

### Guards Not Working

**Symptoms**: Navigation happens even though guard returns false

**Solutions**:

1. **Implement `ILeavingGuard` correctly**

```csharp
// ✅ Correct
public class PageModel : ILeavingGuard
{
    public async ValueTask<bool> CanLeaveAsync()
    {
        return await ConfirmAsync();
    }
}

// ❌ Wrong - typo in interface name
public class PageModel : ILeavingGard
```

2. **Return false to block navigation**

```csharp
// ✅ Blocks navigation
public async ValueTask<bool> CanLeaveAsync()
{
    if (hasUnsavedChanges)
        return false;  // Blocks!
    return true;
}

// ❌ Always allows
public async ValueTask<bool> CanLeaveAsync()
{
    await ShowDialogAsync();
    return true;  // Always allows
}
```

3. **Check for `IgnoreGuards` behavior**

```csharp
// Guards are bypassed with this flag
Navigation.Relative(NavigationBehavior.IgnoreGuards).Pop()
```

4. **Ensure guard is on the correct page**

```csharp
// Guard must be on the page being LEFT, not the target page
// Stack: [A, B] -> Pop back to A
// Guard must be on PageModel B, not A
```

### Lifecycle Events Not Firing

**Symptoms**: `OnEnteringAsync`, `OnAppearingAsync`, etc. not called

**Solutions**:

1. **Verify interface implementation**

```csharp
// ✅ Correct
public class MyPageModel : IEnteringAware
{
    public async ValueTask OnEnteringAsync()
    {
        // Will be called
    }
}

// ❌ Wrong - missing interface
public class MyPageModel
{
    public async ValueTask OnEnteringAsync()
    {
        // Won't be called - no interface
    }
}
```

2. **Check method signature**

```csharp
// ✅ Correct
public ValueTask OnEnteringAsync()

// ❌ Wrong return type
public Task OnEnteringAsync()

// ❌ Wrong method name
public ValueTask OnEntering()
```

3. **Intermediate pages don't "appear"**

```csharp
// Stack: [A, B, C] -> Pop -> Pop -> [A]
// Only A will fire OnAppearingAsync
// B and C will fire OnLeavingAsync but not OnAppearingAsync
```

4. **`OnAppearingAsync` only fires for target page**

```csharp
// When navigating A -> B -> C in one call
// Only C fires OnAppearingAsync
// A and B fire OnDisappearingAsync
await _navigationService.GoToAsync(
    Navigation.Relative()
        .Push<BPageModel>()
        .Push<CPageModel>()
);
```

### Async/Await Issues

**Symptoms**: Deadlocks, operations not completing

**Solutions**:

1. **Don't block on async operations**

```csharp
// ❌ Wrong - can cause deadlocks
public ValueTask OnEnteringAsync()
{
    LoadDataAsync().Wait();  // Don't do this!
    return ValueTask.CompletedTask;
}

// ✅ Correct
public async ValueTask OnEnteringAsync()
{
    await LoadDataAsync();
}
```

2. **Use `ConfigureAwait` appropriately**

```csharp
// In UI code, capture context (default)
await LoadDataAsync();

// In library code, don't capture if not needed
await LoadDataAsync().ConfigureAwait(false);
```

3. **Return `ValueTask.CompletedTask` for sync methods**

```csharp
public ValueTask OnEnteringAsync()
{
    // Synchronous operation
    InitializeData();
    return ValueTask.CompletedTask;
}
```

### ViewModel Not Found in DI

**Symptoms**: Exception about unable to resolve ViewModel type

**Solutions**:

1. **Ensure ViewModels are registered**

```csharp
// ✅ Automatic registration (⚠️ not AOT-compatible - use AddPage for each page instead)
.UseNaluNavigation<App>(nav => nav.AddPages())

// ✅ Manual registration
.UseNaluNavigation<App>(nav => nav
    .AddPage<MyPageModel, MyPage>()
)
```

2. **Check naming convention**

```csharp
// Default convention: MainPage -> MainPageModel
// If using different convention:
.UseNaluNavigation<App>(nav => nav
    .AddPages(pageType => 
        pageType.Name.Replace("Page", "ViewModel")
    )
)
```

3. **Verify ViewModel is in scanned assembly**

```csharp
// Scans the App assembly (⚠️ not AOT-compatible - use AddPage for each page instead)
.UseNaluNavigation<App>(nav => nav.AddPages())

// If ViewModel is in different assembly, register manually
.UseNaluNavigation<App>(nav => nav
    .AddPage<MyPageModel, MyPage>()
)
```

### Android: App Recreates on Foreground

**Symptoms**: App creates new window when returning from background

**Solution**: Cache the window instance

```csharp
public partial class App : Application
{
#if ANDROID
    private Window? _window;
#endif

    protected override Window CreateWindow(IActivationState? activationState)
    {
#if ANDROID
        return _window ??= new Window(new AppShell(_navigationService));
#else
        return new Window(new AppShell(_navigationService));
#endif
    }
}
```

## Debugging Tips

### Enable Navigation Event Logging

```csharp
public partial class AppShell : NaluShell
{
    public AppShell(INavigationService navigationService) 
        : base(navigationService, typeof(MainPage))
    {
        InitializeComponent();
        
        #if DEBUG
        NavigationEvent += (s, e) =>
        {
            Debug.WriteLine($"[NAV] {e.EventType}: {e.Target}");
        };
        #endif
    }
}
```

### Track Lifecycle Timing

```csharp
public class MyPageModel : IEnteringAware, IAppearingAware
{
    private readonly Stopwatch _stopwatch = new();
    
    public ValueTask OnEnteringAsync()
    {
        _stopwatch.Restart();
        // Your code
        Debug.WriteLine($"OnEntering took {_stopwatch.ElapsedMilliseconds}ms");
        return ValueTask.CompletedTask;
    }
    
    public ValueTask OnAppearingAsync()
    {
        var total = _stopwatch.ElapsedMilliseconds;
        Debug.WriteLine($"Total navigation time: {total}ms");
        return ValueTask.CompletedTask;
    }
}
```

### Verify Navigation State

```csharp
private void LogNavigationState()
{
    var shell = Shell.Current;
    Debug.WriteLine($"Current Item: {shell.CurrentItem.Route}");
    Debug.WriteLine($"Current Section: {shell.CurrentItem.CurrentItem.Route}");
    Debug.WriteLine($"Current Content: {shell.CurrentItem.CurrentItem.CurrentItem.Route}");
    Debug.WriteLine($"Navigation Stack Count: {shell.Navigation.NavigationStack.Count}");
}
```

## Back to Main Documentation

← [Back to Navigation Overview](navigation.md)

