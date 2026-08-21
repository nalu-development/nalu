using JetBrains.Annotations;
using Microsoft.Maui.Controls.Xaml;

namespace Nalu.Maui.TestApp.Tests;

/// <summary>
/// The markup extension needs an <see cref="IProvideValueTarget"/> naming the target
/// element/property; XAML supplies one, code-built pages fake it with this stub.
/// </summary>
file sealed class ScrollDirectionProvideValueStub(BindableObject target, BindableProperty property) : IServiceProvider, IProvideValueTarget
{
    public object TargetObject { get; } = target;

    public object TargetProperty { get; } = property;

    public object? GetService(Type serviceType) => serviceType == typeof(IProvideValueTarget) ? this : null;
}

file static class ScrollDirectionFactory
{
    public static void Apply(BindableObject target, BindableProperty property, ScrollDirectionValueExtensionBase extension)
        => target.SetBinding(property, extension.ProvideValue(new ScrollDirectionProvideValueStub(target, property)));
}

/// <summary>
/// Exercises {nalu:ScrollDirectionValue} against a tracked ScrollView: a snap target
/// (duration 0, deterministic reads), a sticky target only the content top can restore
/// (deactivate threshold out of reach), and an animated bottom bar (the classic
/// hide-on-scroll chrome). Page-side buttons scroll by exact deltas — synthetic swipes
/// travel differently per platform and would make the thresholds flaky.
/// </summary>
[UsedImplicitly]
public class ScrollDirectionPage : ContentPage
{
    public ScrollDirectionPage()
    {
        Title = "Scroll Direction";

        var contentStack = new VerticalStackLayout { Padding = new Thickness(0, 160, 0, 0) };

        for (var i = 0; i < 40; i++)
        {
            contentStack.Add(new BoxView { HeightRequest = 80, Color = i % 2 == 0 ? Colors.LightGray : Colors.Silver });
        }

        var scrollView = new ScrollView { Content = contentStack };
        Scaffold.SetScrollTracker(this, scrollView);

        // Live offset probe: tests synchronize on the actual channel value before asserting.
        var offsetProbe = new Label { AutomationId = "ScrollDirOffset", FontSize = 11 };
        offsetProbe.SetBinding(Label.TextProperty, NavBarBindings.Create(offsetProbe, nameof(ScaffoldNavBarContext.ScrollOffset), stringFormat: "{0:F0}"));

        // Snap target: opacity steps 1 → 0.2 on activation, no animation.
        var snapTarget = new BoxView { AutomationId = "ScrollDirSnap", HeightRequest = 14, WidthRequest = 14, Color = Colors.DarkOrange, HorizontalOptions = LayoutOptions.Start };

        ScrollDirectionFactory.Apply(
            snapTarget,
            OpacityProperty,
            new ScrollDirectionValueExtension { Deactivated = 1.0, Activated = 0.2, ActivateThreshold = 100, DeactivateThreshold = 50, ActivateDuration = 0 }
        );

        // Sticky target: the deactivate threshold is unreachable, so ONLY the content top
        // (the built-in force-deactivate) can restore it.
        var stickyTarget = new BoxView { AutomationId = "ScrollDirSticky", HeightRequest = 14, WidthRequest = 14, Color = Colors.SeaGreen, HorizontalOptions = LayoutOptions.Start };

        ScrollDirectionFactory.Apply(
            stickyTarget,
            OpacityProperty,
            new ScrollDirectionValueExtension { Deactivated = 1.0, Activated = 0.2, ActivateThreshold = 100, DeactivateThreshold = 100_000, ActivateDuration = 0 }
        );

        // Solid ↔ gradient background, duration 0: pixel checks verify the reused-and-mutated
        // brush instance actually repaints natively on activation.
        var gradientTarget = new Grid { AutomationId = "ScrollDirGradient", HeightRequest = 20 };

        ScrollDirectionFactory.Apply(
            gradientTarget,
            BackgroundProperty,
            new ScrollDirectionValueExtension
            {
                Deactivated = new SolidColorBrush(Colors.LightGray),
                Activated = new LinearGradientBrush(
                    [new GradientStop(Colors.Red, 0f), new GradientStop(Colors.Blue, 1f)],
                    new Point(0, 0),
                    new Point(1, 0)
                ),
                ActivateThreshold = 100,
                DeactivateThreshold = 50,
                ActivateDuration = 0
            }
        );

        // The classic chrome: a bottom bar sliding out of view over 150ms when activated.
        var bar = new Grid { AutomationId = "ScrollDirBar", HeightRequest = 56, BackgroundColor = Colors.DarkSlateBlue, VerticalOptions = LayoutOptions.End };

        ScrollDirectionFactory.Apply(
            bar,
            TranslationYProperty,
            new ScrollDirectionValueExtension { Deactivated = 0.0, Activated = 96.0, ActivateThreshold = 100, DeactivateThreshold = 50, ActivateDuration = 150, Easing = Easing.SinInOut }
        );

        Button Move(string text, string automationId, Func<double, double> target)
        {
            var button = new Button { Text = text, AutomationId = automationId, FontSize = 11 };
            button.Clicked += (_, _) => _ = scrollView.ScrollToAsync(0, target(scrollView.ScrollY), animated: false);

            return button;
        }

        var exitButton = new Button { Text = "Exit", AutomationId = "ExitScrollDirection", FontSize = 11, BackgroundColor = Colors.IndianRed };
        exitButton.Clicked += (_, _) => ((App)Application.Current!).ResetToMainPage();

        // Controls live in a fixed overlay, not in the scrolled content: they must stay
        // on screen (and tappable) at any offset.
        var controls = new VerticalStackLayout
        {
            Spacing = 4,
            Padding = 8,
            BackgroundColor = Colors.White.WithAlpha(0.85f),
            VerticalOptions = LayoutOptions.Start,
            Children =
            {
                offsetProbe,
                snapTarget,
                stickyTarget,
                gradientTarget,
                Move("Down 120", "ScrollDirDown120", y => y + 120),
                Move("Down 40", "ScrollDirDown40", y => y + 40),
                Move("Up 60", "ScrollDirUp60", y => y - 60),
                Move("Top", "ScrollDirTop", _ => 0),
                exitButton
            }
        };

        var grid = new Grid();
        grid.Add(scrollView);
        grid.Add(controls);
        grid.Add(bar);

        Content = grid;
    }
}

/// <summary>Scaffold harness hosting the scroll-direction page (the extension resolves the page's scroll channel through the scaffold tree).</summary>
[UsedImplicitly]
[TestPage("Scaffold Scroll Direction Tests")]
public class ScrollDirectionScaffold : Scaffold
{
    public ScrollDirectionScaffold(INavigationService navigationService)
    {
        _ = navigationService;

        Areas.Add(new ScaffoldRoot { Title = "Scroll Direction", PageType = typeof(ScrollDirectionPage) });
    }
}
