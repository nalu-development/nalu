using CommunityToolkit.Mvvm.ComponentModel;
using JetBrains.Annotations;

namespace Nalu.Maui.TestApp.Tests;

[UsedImplicitly]
public partial class WindowInsetsProbeModel : ObservableObject;

/// <summary>
/// Reads the platform's own safe-area numbers straight out of the native window, live.
/// The question it exists to answer: on iPadOS 26 an app is a resizable window with system
/// windowing controls ("traffic lights") drawn over its top-leading corner, and no UIKit API
/// reports their frame — so the only way to know whether they are detectable is to watch what
/// the platform publishes as the window's safe area while they are on screen.
/// </summary>
[UsedImplicitly]
public class WindowInsetsProbePage : ContentPage
{
    private readonly Label _readout;
    private IDispatcherTimer? _timer;

    public WindowInsetsProbePage(WindowInsetsProbeModel model)
    {
        BindingContext = model;
        Title = "Insets";

        _readout = new Label
                   {
                       AutomationId = "InsetsReadout",
                       FontFamily = "Courier",
                       FontSize = 13,
                       LineBreakMode = LineBreakMode.WordWrap
                   };

        Content = new VerticalStackLayout
                  {
                      Padding = new Thickness(16, 80, 16, 16),
                      Spacing = 10,
                      Children =
                      {
                          new Label { Text = "Native safe area (live)", FontSize = 20, FontAttributes = FontAttributes.Bold },
                          _readout,
                          MakeExitButton()
                      }
                  };
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        _timer = Dispatcher.CreateTimer();
        _timer.Interval = TimeSpan.FromMilliseconds(400);
        _timer.Tick += (_, _) => _readout.Text = Read();
        _timer.Start();
    }

    protected override void OnDisappearing()
    {
        _timer?.Stop();
        _timer = null;
        base.OnDisappearing();
    }

    private static string Read()
    {
#if IOS || MACCATALYST
        var window = UIKit.UIApplication.SharedApplication.ConnectedScenes
                          .OfType<UIKit.UIWindowScene>()
                          .SelectMany(scene => scene.Windows)
                          .FirstOrDefault(w => w.IsKeyWindow);

        if (window is null)
        {
            return "no key window";
        }

        var w = window.SafeAreaInsets;
        var root = window.RootViewController;
        var rv = root?.View?.SafeAreaInsets ?? default;
        var add = root?.AdditionalSafeAreaInsets ?? default;
        var scene = window.WindowScene;

        // Where this window sits ON THE SCREEN. UIScreen.MainScreen is deprecated in iOS 26 (the
        // scene owns the screen now), and the window's own frame is expressed in its own space —
        // so the only way to learn that the window is a smaller rectangle somewhere on the screen
        // is to convert through the screen's coordinate space, the same trick the keyboard manager
        // uses to place the keyboard frame. On iPadOS 26 that is what distinguishes a windowed
        // app (system controls drawn over its top-leading corner) from a full-screen one.
        var screen = scene?.Screen;
        var onScreen = screen is not null
            ? window.ConvertRectToCoordinateSpace(window.Bounds, screen.CoordinateSpace)
            : CoreGraphics.CGRect.Empty;
        var screenBounds = screen?.Bounds ?? CoreGraphics.CGRect.Empty;
        var windowed = screen is not null && onScreen.Size != screenBounds.Size;

        return $"window.bounds     {window.Bounds.Width:0}x{window.Bounds.Height:0}\n"
               + $"window on screen  [{onScreen.X:0},{onScreen.Y:0} {onScreen.Width:0}x{onScreen.Height:0}]\n"
               + $"screen.bounds     {screenBounds.Width:0}x{screenBounds.Height:0}\n"
               + $"WINDOWED          {windowed}\n"
               + $"window.safeArea   L{w.Left:0.#} T{w.Top:0.#} R{w.Right:0.#} B{w.Bottom:0.#}\n"
               + $"rootView.safeArea L{rv.Left:0.#} T{rv.Top:0.#} R{rv.Right:0.#} B{rv.Bottom:0.#}\n"
               + $"rootVC.additional L{add.Left:0.#} T{add.Top:0.#} R{add.Right:0.#} B{add.Bottom:0.#}\n"
               + $"sizeClass         H:{window.TraitCollection.HorizontalSizeClass} V:{window.TraitCollection.VerticalSizeClass}\n"
               + $"sysMinMargins     L{root?.SystemMinimumLayoutMargins.Leading ?? 0:0.#} T{root?.SystemMinimumLayoutMargins.Top ?? 0:0.#}\n"
               + $"rootView.margins  L{root?.View?.DirectionalLayoutMargins.Leading ?? 0:0.#} T{root?.View?.LayoutMargins.Top ?? 0:0.#}\n"
               + "--- window subtree, top 220pt ---\n"
               + DescribeTopOfWindow(window);
#else
        return "iOS/Catalyst only";
#endif
    }

#if IOS || MACCATALYST
    /// <summary>
    /// Every native view intersecting the window's top band, with its class and frame. If the
    /// system draws its windowing controls INSIDE our window, they appear here as a view we did
    /// not create — and then their geometry is readable at runtime. If they are hosted in a
    /// separate system window above ours, nothing in this dump will cover the top-leading corner,
    /// and no in-process API can measure them.
    /// </summary>
    private static string DescribeTopOfWindow(UIKit.UIWindow window)
    {
        var lines = new List<string>();

        void Walk(UIKit.UIView view, int depth)
        {
            if (depth > 4 || lines.Count > 40)
            {
                return;
            }

            var frame = view.ConvertRectToView(view.Bounds, window);

            if (frame.Top < 220 && frame.Width > 1 && frame.Height > 1)
            {
                lines.Add($"{new string(' ', depth * 2)}{view.GetType().Name} "
                          + $"[{frame.X:0},{frame.Y:0} {frame.Width:0}x{frame.Height:0}]");
            }

            foreach (var child in view.Subviews)
            {
                Walk(child, depth + 1);
            }
        }

        Walk(window, 0);

        return string.Join("\n", lines);
    }

#endif

    private static Button MakeExitButton()
    {
        var exitButton = new Button { Text = "Exit", AutomationId = "ExitInsets", FontSize = 11, BackgroundColor = Colors.IndianRed };
        exitButton.Clicked += (_, _) => ((App) Application.Current!).ResetToMainPage();

        return exitButton;
    }
}

[TestPage("Window Insets Probe")]
public class WindowInsetsProbeScaffold : Scaffold
{
    public WindowInsetsProbeScaffold(INavigationService navigationService)
    {
        _ = navigationService;

        Areas.Add(
            new ScaffoldRoot { Title = "Insets", PageType = typeof(WindowInsetsProbePage) }
        );
    }
}
