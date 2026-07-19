using JetBrains.Annotations;

namespace Nalu.Maui.TestApp.Tests;

[UsedImplicitly]
[TestPage("Popup Tests")]
public class PopupTestsPage : ContentPage
{
    public PopupTestsPage()
    {
        var stack = new VerticalStackLayout { Spacing = 12, Padding = 16 };

        var openPopupButton = new Button { Text = "Open popup", AutomationId = "OpenPopupButton" };
        openPopupButton.Clicked += (_, _) => Navigation.PushModalAsync(new TestPopupPage(closeOnScrimTapped: true));

        var openStubbornPopupButton = new Button { Text = "Open stubborn popup", AutomationId = "OpenStubbornPopupButton" };
        openStubbornPopupButton.Clicked += (_, _) => Navigation.PushModalAsync(new TestPopupPage(closeOnScrimTapped: false));

        stack.Add(openPopupButton);
        stack.Add(openStubbornPopupButton);

        Content = stack;
    }
}

/// <summary>
/// Minimal <see cref="PopupPageBase" /> implementation for UI tests.
/// The popup container is docked at the top so the (full screen) scrim tap area
/// stays unobstructed at the screen center, where taps land.
/// </summary>
public class TestPopupPage : PopupPageBase
{
    public TestPopupPage(bool closeOnScrimTapped)
    {
        CloseOnScrimTapped = closeOnScrimTapped;

        Scrim.BackgroundColor = Color.FromArgb("#20000000");

        PopupBorder.Background = Colors.White;
        PopupBorder.StrokeThickness = 0;
        PopupBorder.StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 16 };
        PopupBorder.HorizontalOptions = LayoutOptions.Center;
        PopupBorder.VerticalOptions = LayoutOptions.Start;
        PopupBorder.Margin = new Thickness(24, 60, 24, 0);

        // Expose the (library-internal) tappable scrim overlay to the tests:
        // it is the first child of the grid hosting the PopupBorder.
        if (PopupBorder.Parent is Grid popupBorderHost && popupBorderHost.Children[0] is View tapArea)
        {
            tapArea.AutomationId = "PopupScrimTapArea";
        }

        // Tap-gesture based close: DevFlow taps inside modal pages don't reach native
        // Button controls (no windowBounds), while gesture recognizers are invoked directly.
        var closeTapRecognizer = new TapGestureRecognizer();
        closeTapRecognizer.Tapped += (_, _) => Navigation.PopModalAsync();

        var closePopupButton = new Label
        {
            Text = "Close",
            AutomationId = "ClosePopupButton",
            TextColor = Colors.White,
            BackgroundColor = Colors.DarkSlateBlue,
            Padding = new Thickness(16, 8),
            HorizontalTextAlignment = TextAlignment.Center
        };
        closePopupButton.GestureRecognizers.Add(closeTapRecognizer);

        PopupContent = new VerticalStackLayout
        {
            Padding = 16,
            Spacing = 8,
            Children =
            {
                // Explicit color: the app's dark theme would render white text on the white popup.
                new Label { Text = "Popup is open", AutomationId = "PopupContentLabel", TextColor = Colors.Black },
                closePopupButton
            }
        };
    }
}
