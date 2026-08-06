using CommunityToolkit.Mvvm.ComponentModel;
using JetBrains.Annotations;

namespace Nalu.Maui.TestApp.Tests;

/// <summary>The intent of the overlay-service harness: display text + a lifecycle report channel.</summary>
public sealed class OverlayDemoIntent
{
    public required string Text { get; init; }

    public required Action<string> Report { get; init; }
}

/// <summary>
/// Sheet model: receives the intent via the reflection-dispatched OnEnteringAsync, closes with
/// an int result (or a deliberately wrong type to prove the runtime check), and reports its
/// lifecycle (entering, leaving, dispose) through the intent callback.
/// </summary>
[UsedImplicitly]
public partial class VmSheetModel(IOverlayRef overlay) : ObservableObject, ILeavingAware, IDisposable
{
    private OverlayDemoIntent? _intent;

    [ObservableProperty]
    private string _text = "no-intent";

    [UsedImplicitly]
    public ValueTask OnEnteringAsync(OverlayDemoIntent intent)
    {
        _intent = intent;
        Text = intent.Text;
        intent.Report("entered");

        return ValueTask.CompletedTask;
    }

    public Task CloseWithResult() => overlay.CloseAsync(42);

    public Task CloseNoResult() => overlay.CloseAsync();

    public Task CloseWrongType()
    {
        try
        {
            return overlay.CloseAsync("nope");
        }
        catch (InvalidOperationException)
        {
            _intent?.Report("wrong-type-threw");

            return Task.CompletedTask;
        }
    }

    public ValueTask OnLeavingAsync()
    {
        _intent?.Report("left");

        return ValueTask.CompletedTask;
    }

    public void Dispose() => _intent?.Report("disposed");
}

/// <summary>
/// Sheet view: single ctor taking the model (BindingContext convention), attached MaxWidth —
/// the attached-presentation channel the caller does NOT override.
/// </summary>
[UsedImplicitly]
public class VmSheetView : VerticalStackLayout
{
    public VmSheetView(VmSheetModel model)
    {
        BindingContext = model;
        ScaffoldBottomSheet.SetMaxWidth(this, 300);

        Spacing = 8;
        Padding = new Thickness(16, 0, 16, 16);

        var text = new Label { AutomationId = "VmSheetText", FontSize = 16, FontAttributes = FontAttributes.Bold };
        text.SetBinding(Label.TextProperty, static (VmSheetModel m) => m.Text);
        Add(text);

        var closeResult = new Button { Text = "Close 42", AutomationId = "VmSheetCloseResultButton", FontSize = 12 };
        closeResult.Clicked += async (_, _) => await model.CloseWithResult();
        Add(closeResult);

        var closeNone = new Button { Text = "Close none", AutomationId = "VmSheetCloseNoneButton", FontSize = 12 };
        closeNone.Clicked += async (_, _) => await model.CloseNoResult();
        Add(closeNone);

        var closeWrong = new Button { Text = "Close wrong type", AutomationId = "VmSheetCloseWrongTypeButton", FontSize = 12 };
        closeWrong.Clicked += async (_, _) => await model.CloseWrongType();
        Add(closeWrong);
    }
}

/// <summary>Popup model closing with a string result.</summary>
[UsedImplicitly]
public class VmPopupModel(IOverlayRef overlay)
{
    public Task ClosePicked() => overlay.CloseAsync("picked");
}

/// <summary>Popup view: proves the model resolves through the view ctor and bindings hold.</summary>
[UsedImplicitly]
public class VmPopupView : Border
{
    public VmPopupView(VmPopupModel model)
    {
        BindingContext = model;
        AutomationId = "VmPopupContent";
        StrokeThickness = 0;
        Background = new SolidColorBrush(Colors.White);
        Padding = 16;
        WidthRequest = 220;

        var pick = new Button { Text = "Pick", AutomationId = "VmPopupPickButton", FontSize = 12 };
        pick.Clicked += async (_, _) => await model.ClosePicked();

        Content = new VerticalStackLayout
        {
            Spacing = 8,
            Children =
            {
                new Label { Text = "Vm popup", FontSize = 14 },
                pick
            }
        };
    }
}

[UsedImplicitly]
public partial class OverlayServiceHomePageModel(IOverlayService overlays) : ObservableObject
{
    [ObservableProperty]
    private string _result = "result:idle";

    [ObservableProperty]
    private string _lifecycle = "lifecycle:idle";

    private readonly List<string> _events = [];

    public async Task ShowVmSheet()
    {
        _events.Clear();
        Lifecycle = "lifecycle:idle";

        var result = await overlays.ShowBottomSheetAsync<VmSheetModel, int>(
            new OverlayDemoIntent { Text = "hello-intent", Report = ReportEvent }
        );

        Result = $"result:{result}";
    }

    public async Task ShowVmPopup()
    {
        var result = await overlays.ShowPopupAsync<VmPopupModel, string>();
        Result = $"presult:{result}";
    }

    private void ReportEvent(string name)
    {
        _events.Add(name);
        Lifecycle = $"lifecycle:{string.Join(",", _events)}";
    }
}

[UsedImplicitly]
public class OverlayServiceHomePage : ContentPage
{
    public OverlayServiceHomePage(OverlayServiceHomePageModel model)
    {
        BindingContext = model;
        Title = "OverlayServiceHome";

        var showSheet = new Button { Text = "Show vm sheet", AutomationId = "ShowVmSheetButton", FontSize = 12 };
        showSheet.Clicked += async (_, _) => await model.ShowVmSheet();

        var showPopup = new Button { Text = "Show vm popup", AutomationId = "ShowVmPopupButton", FontSize = 12 };
        showPopup.Clicked += async (_, _) => await model.ShowVmPopup();

        var result = new Label { AutomationId = "OverlayResultLabel", FontSize = 12 };
        result.SetBinding(Label.TextProperty, static (OverlayServiceHomePageModel m) => m.Result);

        var lifecycle = new Label { AutomationId = "OverlayLifecycleLabel", FontSize = 12 };
        lifecycle.SetBinding(Label.TextProperty, static (OverlayServiceHomePageModel m) => m.Lifecycle);

        var exitButton = new Button { Text = "Exit", AutomationId = "ExitOverlayServiceTests", FontSize = 11, BackgroundColor = Colors.IndianRed };
        exitButton.Clicked += (_, _) => ((App)Application.Current!).ResetToMainPage();

        Content = new VerticalStackLayout
        {
            Spacing = 8,
            Padding = 16,
            Children =
            {
                new Label { Text = "Overlay Service Home", AutomationId = "OverlayServiceHomePage", FontSize = 22, FontAttributes = FontAttributes.Bold },
                showSheet,
                showPopup,
                result,
                lifecycle,
                exitButton
            }
        };
    }
}

/// <summary>Scaffold harness of the model-first overlay service.</summary>
[UsedImplicitly]
[TestPage("Scaffold Overlay Service Tests")]
public class OverlayServiceScaffold : Scaffold
{
    public OverlayServiceScaffold()
    {
        Areas.Add(new ScaffoldRoot { Title = "OverlayServiceHome", PageType = typeof(OverlayServiceHomePage) });
    }
}
