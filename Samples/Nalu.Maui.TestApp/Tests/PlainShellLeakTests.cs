namespace Nalu.Maui.TestApp.Tests;

/// <summary>Control group: a plain ContentPage must always collect after reset.</summary>
[TestPage("Plain Page")]
public class PlainLeakPage : ContentPage
{
    public PlainLeakPage()
    {
        LeakTracker.ExpectCollected(this);
        Content = new Label { Text = "Plain Page", AutomationId = "PlainPageLabel", FontSize = 22, Margin = 16 };
    }
}

/// <summary>
/// Manual evidence page (NOT used by automated tests): a minimal VANILLA MAUI Shell
/// (no Nalu navigation involved). On MAUI 10 iOS a Shell swapped out of the window is
/// retained by the platform even after DisconnectHandlers — open it, Exit, then tap the
/// GC button on MainPage: it reports "Leaked:2 PlainShell,ContentPage" while the
/// "Plain Page" control reports "Leaked:0". This is why the navigation leak checks only
/// assert pages disposed DURING navigation (see LeakTracker).
/// </summary>
[TestPage("Plain Shell")]
public class PlainShell : Shell
{
    public PlainShell()
    {
        LeakTracker.ExpectCollected(this);

        var exitButton = new Button { Text = "Exit", AutomationId = "ExitPlainShell", BackgroundColor = Colors.IndianRed };
        exitButton.Clicked += (_, _) => ((App) Application.Current!).ResetToMainPage();

        var page = new ContentPage
                   {
                       Title = "Plain",
                       Content = new VerticalStackLayout
                                 {
                                     Padding = 16,
                                     Children =
                                     {
                                         new Label { Text = "Plain Shell", AutomationId = "PlainShellLabel", FontSize = 22 },
                                         exitButton
                                     }
                                 }
                   };
        LeakTracker.ExpectCollected(page);

        Items.Add(new ShellContent { Title = "Plain", Content = page });
    }
}
