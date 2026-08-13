using CommunityToolkit.Mvvm.ComponentModel;
using JetBrains.Annotations;

namespace Nalu.Maui.TestApp.Tests;

[UsedImplicitly]
public partial class OrRootPageModel(INavigationService navigationService) : ObservableObject
{
    public Task PushDetail() => navigationService.GoToAsync(Navigation.Relative().Push<OrDetailPageModel>());
}

[UsedImplicitly]
public partial class OrDetailPageModel(INavigationService navigationService) : ObservableObject
{
    public Task Pop() => navigationService.GoToAsync(Navigation.Relative().Pop());
}

[UsedImplicitly]
public partial class OrSecondPageModel : ObservableObject;

[UsedImplicitly]
public partial class OrThirdPageModel : ObservableObject;

[UsedImplicitly]
public partial class OrFourthPageModel : ObservableObject;

[UsedImplicitly]
public partial class OrFifthPageModel : ObservableObject;

[UsedImplicitly]
public partial class OrSixthPageModel : ObservableObject;

/// <summary>Shared shape of the orientation harness pages: the probes a rotation test reads.</summary>
public abstract class OrPageBase : ContentPage
{
    protected OrPageBase(string marker, params View[] extras)
    {
        Title = marker;

        var stack = new VerticalStackLayout { Spacing = 6, Padding = 16 };
        stack.Add(new Label { Text = marker, AutomationId = marker, FontSize = 20, FontAttributes = FontAttributes.Bold });

        foreach (var extra in extras)
        {
            stack.Add(extra);
        }

        var exitButton = new Button { Text = "Exit", AutomationId = $"Exit{marker}", FontSize = 11, BackgroundColor = Colors.IndianRed };
        exitButton.Clicked += (_, _) => ((App) Application.Current!).ResetToMainPage();
        stack.Add(exitButton);

        Content = new Grid { Children = { stack } };
    }
}

/// <summary>Root page: carries the rotation controls and both inset probes.</summary>
[UsedImplicitly]
public class OrRootPage : OrPageBase
{
    private async Task ShowCappedSheetAsync()
    {
        var content = new VerticalStackLayout
        {
            Padding = 16,
            Spacing = 8,
            Children =
            {
                new Label { Text = "OrSheetContent", AutomationId = "OrSheetContent", FontSize = 16 },
                new BoxView { Color = Colors.MediumPurple, HeightRequest = 40, AutomationId = "OrSheetBand" }
            }
        };

        await this.GetScaffold().ShowBottomSheetAsync(
            content,
            new ScaffoldBottomSheetOptions { MaxWidth = OrSheet.MaxWidth }
        );
    }

    /// <summary>
    /// A sheet resting at a FRACTION of the available height: the detent a rotation must re-resolve,
    /// since landscape leaves a fraction of the vertical space portrait had.
    /// </summary>
    private async Task ShowTallSheetAsync()
    {
        var content = new VerticalStackLayout
        {
            Padding = 16,
            Children = { new Label { Text = "OrTallSheetContent", AutomationId = "OrTallSheetContent", FontSize = 16 } }
        };

        await this.GetScaffold().ShowBottomSheetAsync(
            content,
            new ScaffoldBottomSheetOptions { Detents = [ScaffoldSheetDetent.Fraction(OrSheet.TallFraction)] }
        );
    }

    public OrRootPage(OrRootPageModel model)
        : base("OrRootPage")
    {
        BindingContext = model;

        // Built after the base ctor so the probes can anchor on THIS page.
        if (Content is Grid { Children: [VerticalStackLayout stack] })
        {
            stack.Insert(1, OrientationProbe.CreateControls());
            stack.Insert(2, SafeAreaProbe.CreateProbe(this));
            stack.Insert(3, NavPageFactory.MakeButton("Push detail", "PushOrDetail", model.PushDetail));

            var sheetButton = new Button { Text = "Show sheet", AutomationId = "ShowOrSheet", FontSize = 11 };
            sheetButton.Clicked += async (_, _) => await ShowCappedSheetAsync();
            stack.Insert(4, sheetButton);

            var tallSheetButton = new Button { Text = "Show tall sheet", AutomationId = "ShowOrTallSheet", FontSize = 11 };
            tallSheetButton.Clicked += async (_, _) => await ShowTallSheetAsync();
            stack.Insert(5, tallSheetButton);
        }
    }
}

/// <summary>
/// Pushed page that HIDES the tab bar: the strip is translated offscreen rather than torn down,
/// and a size change must not bring it back into view.
/// </summary>
[UsedImplicitly]
public class OrDetailPage : OrPageBase
{
    public OrDetailPage(OrDetailPageModel model)
        : base("OrDetailPage", NavPageFactory.MakeButton("Pop", "PopOrDetail", model.Pop), OrientationProbe.CreateControls())
    {
        BindingContext = model;
        Scaffold.SetTabBarVisibility(this, ScaffoldTabBarVisibility.Hidden);
    }
}

file static class OrSheet
{
    /// <summary>
    /// Wider than a portrait phone window and narrower than a landscape one: the sheet spans the
    /// window in portrait and floats capped in landscape, so one rotation shows both states.
    /// </summary>
    public const double MaxWidth = 500;

    /// <summary>The tall sheet's single detent, as a fraction of the height available above the top inset.</summary>
    public const double TallFraction = 0.8;
}

[UsedImplicitly]
public class OrSecondPage : OrPageBase
{
    public OrSecondPage(OrSecondPageModel model)
        : base("OrSecondPage")
        => BindingContext = model;
}

[UsedImplicitly]
public class OrThirdPage : OrPageBase
{
    public OrThirdPage(OrThirdPageModel model)
        : base("OrThirdPage")
        => BindingContext = model;
}

[UsedImplicitly]
public class OrFourthPage : OrPageBase
{
    public OrFourthPage(OrFourthPageModel model)
        : base("OrFourthPage")
        => BindingContext = model;
}

[UsedImplicitly]
public class OrFifthPage : OrPageBase
{
    public OrFifthPage(OrFifthPageModel model)
        : base("OrFifthPage")
        => BindingContext = model;
}

[UsedImplicitly]
public class OrSixthPage : OrPageBase
{
    public OrSixthPage(OrSixthPageModel model)
        : base("OrSixthPage")
        => BindingContext = model;
}

/// <summary>
/// Harness for what ROTATION does to the scaffold. Six roots on the default tab bar: in portrait
/// some of them overflow into the "More" panel, and a wider window must re-partition them — the
/// per-layout-pass overflow computation is the code under test. The root page also carries the
/// safe-area probe, so a test can assert the LANDSCAPE side insets (the notch edge) that no
/// portrait test can see, and the rotation controls themselves.
/// </summary>
[UsedImplicitly]
[TestPage("Scaffold Orientation Tests")]
public class OrientationScaffold : Scaffold
{
    public OrientationScaffold(INavigationService navigationService)
    {
        _ = navigationService;

        Areas.Add(
            new ScaffoldTabBar
            {
                Roots =
                {
                    new ScaffoldRoot { Title = "One", PageType = typeof(OrRootPage) },
                    new ScaffoldRoot { Title = "Two", PageType = typeof(OrSecondPage) },
                    new ScaffoldRoot { Title = "Three", PageType = typeof(OrThirdPage) },
                    new ScaffoldRoot { Title = "Four", PageType = typeof(OrFourthPage) },
                    new ScaffoldRoot { Title = "Five", PageType = typeof(OrFifthPage) },
                    new ScaffoldRoot { Title = "Six", PageType = typeof(OrSixthPage) }
                }
            }
        );
    }
}
