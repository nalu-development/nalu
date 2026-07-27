using Microsoft.Maui.Controls.Shapes;
using Nalu.Internals;

namespace Nalu;

/// <summary>
/// The overflow panel of the default tab bar template: a rounded panel reusing
/// <see cref="ScaffoldTabBarItemView"/> (icon over label, badge, selection pill, fixed
/// <see cref="ScaffoldTabBarView.ItemWidth"/> slots) inside a <see cref="HorizontalWrapLayout"/> —
/// overflow items look and behave exactly like bar items, wrapping to as many rows as needed.
/// Built fresh on every open (the overflow set is recomputed per layout pass); items dismiss
/// the overlay first, then route selection through the engine.
/// </summary>
/// <remarks>
/// Instances are created by the template when the "More" item opens — the type is public purely
/// as a styling surface (item appearance rides the <see cref="ScaffoldTabBarItemView"/> style):
/// <code>
/// &lt;Style TargetType="nalu:ScaffoldTabBarOverflowView"&gt;
///     &lt;Setter Property="PanelBackground" Value="{AppThemeBinding Light=..., Dark=...}" /&gt;
/// &lt;/Style&gt;
/// </code>
/// </remarks>
public sealed class ScaffoldTabBarOverflowView : Border
{
    private readonly List<ScaffoldTabBarItemView> _items = [];

    /// <summary>Bindable property for <see cref="PanelBackground"/>.</summary>
    public static readonly BindableProperty PanelBackgroundProperty =
        GenericBindableProperty<ScaffoldTabBarOverflowView>.Create<Brush?>(
            nameof(PanelBackground),
            defaultValueCreator: static _ => new SolidColorBrush(Color.FromArgb("#FAFFFFFF")),
            propertyChanged: static panel => (_, value) => panel.Background = value
        );

    /// <summary>Bindable property for <see cref="PanelCornerRadius"/>.</summary>
    public static readonly BindableProperty PanelCornerRadiusProperty =
        GenericBindableProperty<ScaffoldTabBarOverflowView>.Create(
            nameof(PanelCornerRadius),
            22.0,
            propertyChanged: static panel => (_, value) => panel.StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(value) }
        );

    /// <summary>Bindable property for <see cref="PanelShadow"/>.</summary>
    public static readonly BindableProperty PanelShadowProperty =
        GenericBindableProperty<ScaffoldTabBarOverflowView>.Create<Shadow>(
            nameof(PanelShadow),
            defaultValueCreator: static _ => new Shadow
            {
                Brush = Brush.Black,
                Opacity = 0.22f,
                Radius = 18,
                Offset = new Point(0, 4)
            },
            propertyChanged: static panel => (_, value) => panel.Shadow = value
        );

    /// <summary>Bindable property for <see cref="Scrim"/>.</summary>
    public static readonly BindableProperty ScrimProperty =
        GenericBindableProperty<ScaffoldTabBarOverflowView>.Create<Brush?>(
            nameof(Scrim),
            defaultValueCreator: static _ => new SolidColorBrush(Colors.Black.WithAlpha(0.45f))
        );

    /// <summary>
    /// Gets or sets the panel background. Drives the view's own
    /// <see cref="VisualElement.Background"/> — style THIS, not <c>Background</c>.
    /// </summary>
    public Brush? PanelBackground
    {
        get => (Brush?)GetValue(PanelBackgroundProperty);
        set => SetValue(PanelBackgroundProperty, value);
    }

    /// <summary>Gets or sets the panel corner radius.</summary>
    public double PanelCornerRadius
    {
        get => (double)GetValue(PanelCornerRadiusProperty);
        set => SetValue(PanelCornerRadiusProperty, value);
    }

    /// <summary>Gets or sets the panel shadow.</summary>
    public Shadow PanelShadow
    {
        get => (Shadow)GetValue(PanelShadowProperty);
        set => SetValue(PanelShadowProperty, value);
    }

    /// <summary>
    /// Gets or sets the scrim brush shown behind the panel (gradients supported). The scrim
    /// renders below the tab bar in z-order — the bar stays undimmed and interactive while the
    /// panel is open. Read when the panel opens.
    /// </summary>
    public Brush? Scrim
    {
        get => (Brush?)GetValue(ScrimProperty);
        set => SetValue(ScrimProperty, value);
    }

    internal ScaffoldTabBarOverflowView(ScaffoldTabBarView barView, Func<Task> closeAsync)
    {
        AutomationId = "TabBarOverflowPanel";
        StrokeThickness = 0;

        var wrap = new HorizontalWrapLayout();

        foreach (var root in barView.OverflowRoots)
        {
            var item = new ScaffoldTabBarItemView(
                barView,
                root,
                tapOverride: () => HandleItemTapAsync(barView, root, closeAsync),
                automationIdOverride: $"OverflowRow{root.Title}"
            )
            {
                // Same slot width as the bar.
                WidthRequest = barView.ItemWidth
            };

            _items.Add(item);
            wrap.Add(item);
        }

        Content = wrap;

        // Defaults never raise propertyChanged: seed once from the current values. The panel's
        // padding mirrors the bar pill's on purpose — overflow rows keep the bar's slot geometry.
        Background = PanelBackground;
        Shadow = PanelShadow;
        StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(PanelCornerRadius) };
        Padding = barView.BarPadding;
    }

    /// <summary>Detaches the items' root subscriptions; invoked by the presenter's overlay cleanup.</summary>
    internal void Cleanup()
    {
        foreach (var item in _items)
        {
            item.Unsubscribe();
        }
    }

    private static async Task HandleItemTapAsync(ScaffoldTabBarView barView, ScaffoldRoot root, Func<Task> closeAsync)
    {
        // Close first, then navigate: a no-op selection (current root, empty stack) must
        // still dismiss the panel, and the engine's own sync would close it anyway.
        await closeAsync().ConfigureAwait(true);

        if (barView.TabBar is { } tabBar)
        {
            await tabBar.SelectRootAsync(root).ConfigureAwait(true);
        }
    }
}
