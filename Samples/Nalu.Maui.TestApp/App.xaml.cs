using Microsoft.Maui.Platform;

namespace Nalu.Maui.TestApp;

public partial class App : Application
{
    private readonly IServiceProvider _serviceProvider;

    private Window? _window;

    public App(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        InitializeComponent();
    }

    private void SetupResetButton()
    {
#if IOS
        var window = (UIKit.UIWindow) Windows[0].Handler!.PlatformView!;
        if (window.Subviews.FirstOrDefault(v => v.Tag == 0x835E7) is { } existingButton)
        {
            window.BringSubviewToFront(existingButton);
            return;
        }

        var resetButton = new UIKit.UIButton(UIKit.UIButtonType.System)
                          {
                              BackgroundColor = UIKit.UIColor.Red,
                              Tag = 0x835E7
                          };
        resetButton.UpdateAutomationId(new Button { AutomationId = "ResetButton" });
        // add action to the button
        resetButton.TouchUpInside += (_, _) =>
        {
            Windows[0].Page = new MainPage(_serviceProvider);
        };
        window.AddSubview(resetButton);
        resetButton.TranslatesAutoresizingMaskIntoConstraints = false;
        UIKit.NSLayoutConstraint.ActivateConstraints([
            resetButton.BottomAnchor.ConstraintEqualTo(window.SafeAreaLayoutGuide.BottomAnchor),
            resetButton.TrailingAnchor.ConstraintEqualTo(window.SafeAreaLayoutGuide.TrailingAnchor),
            resetButton.WidthAnchor.ConstraintEqualTo(32),
            resetButton.HeightAnchor.ConstraintEqualTo(32)
        ]);
        window.BringSubviewToFront(resetButton);
#endif
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        _window ??= CreateTestWindow();
        return _window;
    }

    private Window CreateTestWindow()
    {
        var testWindow = new Window(new MainPage(_serviceProvider));
        testWindow.PropertyChanged += async (_, e) =>
        {
            if (e.PropertyName == nameof(Window.Page))
            {
                await Task.Delay(100);
                SetupResetButton();
            }
        };

        return testWindow;
    }
}
