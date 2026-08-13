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
        }
    }
}

[UsedImplicitly]
public class OrDetailPage : OrPageBase
{
    public OrDetailPage(OrDetailPageModel model)
        : base("OrDetailPage")
        => BindingContext = model;
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
