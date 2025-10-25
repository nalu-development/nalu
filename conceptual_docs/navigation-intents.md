# Navigation Intents

Intents provide **strongly-typed data passing** during navigation, replacing Shell's string-based query parameters.

## Basic Intent Usage

### Defining an Intent

Use `record` types for automatic value equality:

```csharp
public record ContactIntent(int ContactId);
public record ProductIntent(string Sku, string? Variant = null);
public record SearchIntent(string Query, int PageSize = 20);
```

### Passing an Intent

```csharp
await _navigationService.GoToAsync(
    Navigation.Relative()
        .Push<ContactDetailPageModel>()
        .WithIntent(new ContactIntent(42))
);
```

### Receiving an Intent

Implement `IEnteringAware<TIntent>` or `IAppearingAware<TIntent>`:

```csharp
public class ContactDetailPageModel : ObservableObject, IEnteringAware<ContactIntent>
{
    public async ValueTask OnEnteringAsync(ContactIntent intent)
    {
        // Use the intent data
        ContactId = intent.ContactId;
        await LoadContactAsync(intent.ContactId);
    }
}
```

## Intent-Aware Lifecycle Events

### IEnteringAware\<TIntent\>

Receive intent **before the navigation animation starts**:

```csharp
public class ProductDetailPageModel : IEnteringAware<ProductIntent>
{
    public async ValueTask OnEnteringAsync(ProductIntent intent)
    {
        // Fast initialization with intent data
        ProductSku = intent.Sku;
        SelectedVariant = intent.Variant;
        await LoadFromCacheAsync(intent.Sku);
    }
}
```

### IAppearingAware\<TIntent\>

Receive intent **after the navigation animation completes**:

```csharp
public class SearchPageModel : IAppearingAware<SearchIntent>
{
    public async ValueTask OnAppearingAsync(SearchIntent intent)
    {
        // Slow operation with intent data
        Query = intent.Query;
        PageSize = intent.PageSize;
        await PerformSearchAsync(intent.Query, intent.PageSize);
    }
}
```

### Both Generic and Non-Generic

You can implement both:

```csharp
public class ContactDetailPageModel : 
    IEnteringAware,           // Called when no intent
    IEnteringAware<ContactIntent>  // Called when intent provided
{
    public ValueTask OnEnteringAsync()
    {
        // Handle navigation without intent
        return LoadDefaultContactAsync();
    }

    public ValueTask OnEnteringAsync(ContactIntent intent)
    {
        // Handle navigation with intent
        return LoadContactAsync(intent.ContactId);
    }
}
```

## Intent Behavior Modes

### Strict Mode (Default)

Only intent-aware methods are called when an intent is provided:

```csharp
// With this configuration (default):
.UseNaluNavigation<App>(nav => nav
    .AddPages()
    .WithNavigationIntentBehavior(NavigationIntentBehavior.Strict)
)

// If you navigate WITH intent:
// - IEnteringAware<TIntent>.OnEnteringAsync(intent) is called
// - IEnteringAware.OnEnteringAsync() is NOT called

// If you navigate WITHOUT intent:
// - IEnteringAware.OnEnteringAsync() is called
// - IEnteringAware<TIntent>.OnEnteringAsync(intent) is NOT called
```

### Fallthrough Mode

Both methods are called:

```csharp
// With this configuration:
.UseNaluNavigation<App>(nav => nav
    .AddPages()
    .WithNavigationIntentBehavior(NavigationIntentBehavior.Fallthrough)
)

// If you navigate WITH intent:
// - IEnteringAware<TIntent>.OnEnteringAsync(intent) is called first
// - IEnteringAware.OnEnteringAsync() is also called

// If you navigate WITHOUT intent:
// - IEnteringAware.OnEnteringAsync() is called
```

**Use case for Fallthrough**: When you have common initialization that should always run, plus specific handling for intents.

```csharp
public class PageModel : IEnteringAware, IEnteringAware<SearchIntent>
{
    public ValueTask OnEnteringAsync()
    {
        // Common initialization - always runs with Fallthrough
        InitializeDefaults();
        return ValueTask.CompletedTask;
    }

    public ValueTask OnEnteringAsync(SearchIntent intent)
    {
        // Specific handling for search
        Query = intent.Query;
        return PerformSearchAsync(intent.Query);
    }
}
```

## Returning Results (Pop with Intent)

Pass data back to the previous page when popping:

### Returning Result on Pop

```csharp
// In the detail page, pop with result
await _navigationService.GoToAsync(
    Navigation.Relative()
        .Pop()
        .WithIntent(new ContactSelectedResult(selectedContact))
);
```

### Receiving Result in Previous Page

```csharp
public class ContactListPageModel : IAppearingAware<ContactSelectedResult>
{
    public ValueTask OnAppearingAsync(ContactSelectedResult result)
    {
        // Handle the returned result
        SelectedContact = result.Contact;
        return ValueTask.CompletedTask;
    }
}
```

### Complete Round-Trip Example

```csharp
// Define intents
public record SelectContactIntent();
public record ContactSelectedResult(Contact Contact);

// Page 1: Navigate to selection page
await _navigationService.GoToAsync(
    Navigation.Relative()
        .Push<ContactSelectionPageModel>()
        .WithIntent(new SelectContactIntent())
);

// Page 2: User selects contact, return result
[RelayCommand]
private Task SelectContactAsync(Contact contact)
{
    return _navigationService.GoToAsync(
        Navigation.Relative()
            .Pop()
            .WithIntent(new ContactSelectedResult(contact))
    );
}

// Page 1: Receive result
public class MainPageModel : IAppearingAware<ContactSelectedResult>
{
    public ValueTask OnAppearingAsync(ContactSelectedResult result)
    {
        SelectedContact = result.Contact;
        return ValueTask.CompletedTask;
    }
}
```

## Advanced Intent Patterns

### Multiple Intent Types

A page can handle different intent types:

```csharp
public class DetailPageModel : 
    IEnteringAware<CreateIntent>,
    IEnteringAware<EditIntent>,
    IEnteringAware<ViewIntent>
{
    public ValueTask OnEnteringAsync(CreateIntent intent)
    {
        Mode = PageMode.Create;
        return InitializeForCreateAsync();
    }

    public ValueTask OnEnteringAsync(EditIntent intent)
    {
        Mode = PageMode.Edit;
        return LoadForEditAsync(intent.ItemId);
    }

    public ValueTask OnEnteringAsync(ViewIntent intent)
    {
        Mode = PageMode.View;
        return LoadForViewAsync(intent.ItemId);
    }
}
```

### Complex Intent Data

Intents can contain any data:

```csharp
public record CheckoutIntent(
    List<CartItem> Items,
    ShippingAddress Address,
    PaymentMethod Payment,
    string? PromoCode = null
);

public class CheckoutPageModel : IEnteringAware<CheckoutIntent>
{
    public ValueTask OnEnteringAsync(CheckoutIntent intent)
    {
        Items = intent.Items;
        ShippingAddress = intent.Address;
        PaymentMethod = intent.Payment;
        PromoCode = intent.PromoCode;
        
        CalculateTotals();
        return ValueTask.CompletedTask;
    }
}
```

### Delegate-Based Configuration Intent

For complex setup, use a configuration delegate:

```csharp
public record EditorIntent(Action<EditorViewModel> Configure);

// Navigate
await _navigationService.GoToAsync(
    Navigation.Relative()
        .Push<EditorPageModel>()
        .WithIntent(new EditorIntent(editor =>
        {
            editor.Title = "Edit Contact";
            editor.Data = contact;
            editor.Mode = EditMode.Update;
            editor.ValidationRules = contactValidationRules;
        }))
);

// Receive
public class EditorPageModel : IEnteringAware<EditorIntent>
{
    public ValueTask OnEnteringAsync(EditorIntent intent)
    {
        intent.Configure(this);
        return ValueTask.CompletedTask;
    }
}
```

### Awaitable Intents

Awaitable intents allow you to navigate to a page and wait for a result in one flow.

**Step 1: Define an awaitable intent**

```csharp
// Intent that returns a result
public class SelectContactIntent : AwaitableIntent<Contact?>
{
    // Optional: Add properties for configuration
    public bool AllowMultiple { get; init; }
}

// Intent without result (just completion notification)
public class PerformActionIntent : AwaitableIntent
{
    public string ActionType { get; init; }
}
```

**Step 2: Navigate using ResolveIntentAsync**

```csharp
// Create and navigate with intent - waits for result
var intent = new SelectContactIntent { AllowMultiple = false };
var selectedContact = await _navigationService.ResolveIntentAsync<ContactSelectionPageModel, Contact?>(intent);

if (selectedContact != null)
{
    SelectedContact = selectedContact;
}

// Without result type (just completion)
var actionIntent = new PerformActionIntent { ActionType = "delete" };
await _navigationService.ResolveIntentAsync<ConfirmPageModel>(actionIntent);
```

**Step 3: Receive intent and set result in the target page**

The target page must:
1. Store the intent when received (via `IEnteringAware<TIntent>`)
2. Set the result on the intent using `SetResult(value)`
3. Pop back to complete the awaitable intent

```csharp
public class ContactSelectionPageModel : IEnteringAware<SelectContactIntent>
{
    private readonly INavigationService _navigationService;
    private SelectContactIntent _intent = null!;

    public ContactSelectionPageModel(INavigationService navigationService)
    {
        _navigationService = navigationService;
    }

    public ValueTask OnEnteringAsync(SelectContactIntent intent)
    {
        // Step 1: Store the intent
        _intent = intent;
        return ValueTask.CompletedTask;
    }

    [RelayCommand]
    private async Task SelectContactAsync(Contact contact)
    {
        // Step 2: Set the result on the intent
        _intent.SetResult(contact);
        
        // Step 3: Pop back (this completes the awaitable intent)
        await _navigationService.GoToAsync(Navigation.Relative().Pop());
    }

    [RelayCommand]
    private async Task CancelAsync()
    {
        // Return null to indicate cancellation
        _intent.SetResult(null);
        await _navigationService.GoToAsync(Navigation.Relative().Pop());
    }
}
```

**Key points:**
- The intent must be stored in a field when received in `OnEnteringAsync`
- Call `_intent.SetResult(value)` to set the result
- Pop the page to complete the awaitable intent and return control to the caller
- The caller's `await` will complete with the result you set

**Handling errors:**

```csharp
public class ContactSelectionPageModel : IEnteringAware<SelectContactIntent>
{
    [RelayCommand]
    private async Task SelectContactAsync(Contact contact)
    {
        try
        {
            var validated = await ValidateContactAsync(contact);
            _intent.SetResult(validated);
        }
        catch (Exception ex)
        {
            // Set exception - will throw when awaited
            _intent.SetException(ex);
        }
        
        await _navigationService.GoToAsync(Navigation.Relative().Pop());
    }
}

// Consumer handles exception
try
{
    var result = await intent;
    SelectedContact = result;
}
catch (ValidationException ex)
{
    await ShowErrorAsync(ex.Message);
}
```

**Complete example:**

```csharp
// 1. Define awaitable intent
public class EditItemIntent : AwaitableIntent<Item?>
{
    public Item Item { get; init; }
    public bool AllowDelete { get; init; }
}

// 2. Navigate and await result
[RelayCommand]
private async Task EditItemAsync(Item item)
{
    var intent = new EditItemIntent 
    { 
        Item = item.Clone(), 
        AllowDelete = true 
    };

    // ResolveIntentAsync handles navigation and awaiting the result
    var editedItem = await _navigationService.ResolveIntentAsync<ItemEditorPageModel, Item?>(intent);

    if (editedItem != null)
    {
        // Update with edited item
        await _repository.UpdateAsync(editedItem);
        RefreshList();
    }
    else
    {
        // User deleted or cancelled
        await RefreshAsync();
    }
}

// 3. Editor page receives intent and sets result
public class ItemEditorPageModel : IEnteringAware<EditItemIntent>
{
    private readonly INavigationService _navigationService;
    private EditItemIntent _intent = null!;

    public ItemEditorPageModel(INavigationService navigationService)
    {
        _navigationService = navigationService;
    }

    public ValueTask OnEnteringAsync(EditItemIntent intent)
    {
        // Store the intent and use its data
        _intent = intent;
        Item = intent.Item;
        CanDelete = intent.AllowDelete;
        return ValueTask.CompletedTask;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        // Set the edited item as result
        _intent.SetResult(Item);
        await _navigationService.GoToAsync(Navigation.Relative().Pop());
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        await _repository.DeleteAsync(Item.Id);
        // Return null to indicate deletion
        _intent.SetResult(null);
        await _navigationService.GoToAsync(Navigation.Relative().Pop());
    }

    [RelayCommand]
    private async Task CancelAsync()
    {
        // Return null to indicate cancellation
        _intent.SetResult(null);
        await _navigationService.GoToAsync(Navigation.Relative().Pop());
    }
}
```

**How ResolveIntentAsync works:**

`ResolveIntentAsync` is a convenience method that:
1. Navigates to the page with the intent using `GoToAsync`
2. Automatically awaits the intent completion
3. Returns the result when the page is popped

### Reusable Base Class Pattern

For scenarios like popups or similar pages that always return results, create a base class to reduce boilerplate:

```csharp
public abstract class ResultPageModelBase<TIntent, TResult> : ObservableObject, IEnteringAware<TIntent>
    where TIntent : AwaitableIntent<TResult>
{
    private readonly INavigationService _navigationService;
    protected TIntent Intent { get; private set; } = null!;

    protected ResultPageModelBase(INavigationService navigationService)
    {
        _navigationService = navigationService;
    }

    public virtual ValueTask OnEnteringAsync(TIntent intent)
    {
        Intent = intent;
        return ValueTask.CompletedTask;
    }

    protected async Task CloseAsync()
    {
        await _navigationService.GoToAsync(Navigation.Relative().Pop());
    }

    protected Task CloseAsync(TResult result)
    {
        Intent.SetResult(result);
        return CloseAsync();
    }

    protected Task CloseWithErrorAsync(Exception exception)
    {
        Intent.SetException(exception);
        return CloseAsync();
    }
}
```

Now your page models become much simpler:

```csharp
// Define intent
public class YesNoIntent : AwaitableIntent<bool?>;

// Simple page model using base class
public partial class YesNoPageModel : ResultPageModelBase<YesNoIntent, bool?>
{
    public YesNoPageModel(INavigationService navigationService) 
        : base(navigationService) { }

    [RelayCommand]
    public Task YesAsync() => CloseAsync(true);

    [RelayCommand]
    public Task NoAsync() => CloseAsync(false);
}

// Usage
var result = await _navigationService.ResolveIntentAsync<YesNoPageModel, bool?>(new YesNoIntent());
if (result == true)
{
    // User clicked Yes
}
```

This pattern is especially useful for:
- Confirmation dialogs
- Selection pages
- Form popups
- Any page that always returns a result

**Benefits of AwaitableIntent:**

1. **Clean async flow**: One line to navigate and get result
2. **Type safety**: Intent and result are strongly typed
3. **Reusability**: Intent classes can be reused across the app
4. **Error handling**: Built-in exception propagation via `SetException()`
5. **Explicit API**: Clear contract between pages
6. **Testability**: Easy to mock and test

## Intent Best Practices

### 1. Use Record Types

Records provide value equality, making testing easier:

```csharp
// ✅ Good - automatic value equality
public record ProductIntent(string Sku);

// ❌ Avoid - requires custom equality
public class ProductIntent
{
    public string Sku { get; set; }
}
```

### 2. Keep Intents Immutable

Use `record` or readonly properties:

```csharp
// ✅ Good - immutable
public record SearchIntent(string Query, int Page);

// ❌ Avoid - mutable
public record SearchIntent
{
    public string Query { get; set; }
    public int Page { get; set; }
}
```

### 3. Use Optional Parameters for Flexibility

```csharp
// ✅ Good - flexible with defaults
public record FilterIntent(
    string Category,
    decimal? MinPrice = null,
    decimal? MaxPrice = null,
    string? Brand = null
);
```

### 4. Name Intent Classes Descriptively

```csharp
// ✅ Good - clear intent
public record EditContactIntent(int ContactId);
public record CreateContactIntent();
public record ContactSavedResult(Contact Contact);

// ❌ Avoid - ambiguous
public record ContactIntent(int Id);
public record DataIntent(object Data);
```

### 5. Group Related Intents

```csharp
// In ContactIntents.cs
namespace MyApp.Navigation.Intents;

public record EditContactIntent(int ContactId);
public record ViewContactIntent(int ContactId);
public record DeleteContactIntent(int ContactId);
public record ContactModifiedResult(Contact Contact, ModificationType Type);
```

## Intent vs Navigation-Scoped Services

Choose the right approach for sharing data:

| Use Intent when... | Use Navigation-Scoped Service when... |
|-------------------|--------------------------------------|
| Passing data to a single page | Sharing data with nested pages |
| One-time initialization data | Data needs to be mutable and shared |
| Simple parameter passing | Complex context spanning multiple pages |
| Returning results | Parent-child data relationship |

**Example: When to use each**

```csharp
// Use Intent: Simple parameter passing
await _navigationService.GoToAsync(
    Navigation.Relative()
        .Push<ProductDetailPageModel>()
        .WithIntent(new ProductIntent(productId))
);

// Use Navigation-Scoped Service: Complex shared context
public class OrderPageModel : IEnteringAware<OrderIntent>
{
    private readonly INavigationServiceProvider _navProvider;

    public ValueTask OnEnteringAsync(OrderIntent intent)
    {
        // Create context shared with all nested pages
        var orderContext = new OrderContext(intent.OrderId);
        _navProvider.AddNavigationScoped<IOrderContext>(orderContext);
        return ValueTask.CompletedTask;
    }
}
```

See [Advanced Navigation - Navigation-Scoped Services](navigation-advanced.md#navigation-scoped-services) for more details.

## Testing with Intents

```csharp
[Fact]
public async Task NavigateToDetail_WithProductId_PassesCorrectIntent()
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
        Arg.Is<Navigation>(n => n.Matches(expectedNav))
    );
}

[Fact]
public async Task OnEnteringAsync_WithIntent_LoadsCorrectProduct()
{
    // Arrange
    var productService = Substitute.For<IProductService>();
    var viewModel = new ProductDetailPageModel(productService);
    var intent = new ProductIntent(42);

    // Act
    await viewModel.OnEnteringAsync(intent);

    // Assert
    await productService.Received().LoadProductAsync(42);
}
```

## Common Patterns

### Edit/Create Pattern

```csharp
public record EditItemIntent(int ItemId);
public record CreateItemIntent();
public record ItemSavedResult(Item Item, bool IsNew);

public class ItemEditorPageModel : 
    IEnteringAware<EditItemIntent>,
    IEnteringAware<CreateItemIntent>
{
    private bool _isNew;

    public async ValueTask OnEnteringAsync(EditItemIntent intent)
    {
        _isNew = false;
        Item = await _repository.GetAsync(intent.ItemId);
    }

    public ValueTask OnEnteringAsync(CreateItemIntent intent)
    {
        _isNew = true;
        Item = new Item();
        return ValueTask.CompletedTask;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        await _repository.SaveAsync(Item);
        
        await _navigationService.GoToAsync(
            Navigation.Relative()
                .Pop()
                .WithIntent(new ItemSavedResult(Item, _isNew))
        );
    }
}
```

### Wizard Pattern

```csharp
// Step 1
public record StartWizardIntent();
await _navigationService.GoToAsync(
    Navigation.Relative()
        .Push<WizardStep1PageModel>()
        .WithIntent(new StartWizardIntent())
);

// Step 2
public record WizardStep2Intent(WizardData DataFromStep1);
await _navigationService.GoToAsync(
    Navigation.Relative()
        .Push<WizardStep2PageModel>()
        .WithIntent(new WizardStep2Intent(wizardData))
);

// Complete
public record WizardCompletedResult(WizardData FinalData);
await _navigationService.GoToAsync(
    Navigation.Relative()
        .Pop()
        .Pop()  // Go back to start
        .WithIntent(new WizardCompletedResult(wizardData))
);
```

### Master-Detail Pattern

```csharp
// List page
public class ContactListPageModel : IAppearingAware<ContactUpdatedResult>
{
    [RelayCommand]
    private Task ViewContactAsync(int contactId)
    {
        return _navigationService.GoToAsync(
            Navigation.Relative()
                .Push<ContactDetailPageModel>()
                .WithIntent(new ViewContactIntent(contactId))
        );
    }

    public ValueTask OnAppearingAsync(ContactUpdatedResult result)
    {
        // Refresh the list with updated contact
        UpdateContactInList(result.Contact);
        return ValueTask.CompletedTask;
    }
}

// Detail page
public class ContactDetailPageModel : IEnteringAware<ViewContactIntent>
{
    public async ValueTask OnEnteringAsync(ViewContactIntent intent)
    {
        Contact = await _repository.GetContactAsync(intent.ContactId);
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        await _repository.UpdateContactAsync(Contact);
        
        await _navigationService.GoToAsync(
            Navigation.Relative()
                .Pop()
                .WithIntent(new ContactUpdatedResult(Contact))
        );
    }
}
```

## Back to Main Documentation

← [Back to Navigation Overview](navigation.md)

