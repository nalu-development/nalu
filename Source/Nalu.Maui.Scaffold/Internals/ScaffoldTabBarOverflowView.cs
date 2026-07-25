using Microsoft.Maui.Controls.Shapes;

namespace Nalu;

/// <summary>
/// The overflow panel of the default tab bar template: a rounded panel reusing the tab item
/// template (icon over label, badge, selection pill, fixed <see cref="ScaffoldTabBar.ItemWidth"/>
/// slots) inside a <see cref="HorizontalWrapLayout"/> — overflow items look and behave exactly
/// like bar items, wrapping to as many rows as needed. Built fresh on every open (the overflow
/// set is recomputed per layout pass); items dismiss the overlay first, then route selection
/// through the engine.
/// </summary>
internal sealed class ScaffoldTabBarOverflowView : Border
{
    private readonly List<ScaffoldTabBarItemView> _items = [];

    public ScaffoldTabBarOverflowView(ScaffoldTabBarView barView, Func<Task> closeAsync)
    {
        var tabBar = barView.TabBar;
        var style = barView.EffectiveStyle;

        AutomationId = "TabBarOverflowPanel";
        StrokeThickness = 0;
        Background = style.OverflowPanelBackground;
        Shadow = style.OverflowPanelShadow;
        StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(tabBar.OverflowPanelCornerRadius) };
        Padding = tabBar.BarPadding;

        var wrap = new HorizontalWrapLayout();

        foreach (var root in barView.OverflowRoots)
        {
            var item = new ScaffoldTabBarItemView(
                barView,
                root,
                tapOverride: () => HandleItemTapAsync(tabBar, root, closeAsync),
                automationIdOverride: $"OverflowRow{root.Title}"
            )
            {
                // Same slot width as the bar.
                WidthRequest = tabBar.ItemWidth
            };

            _items.Add(item);
            wrap.Add(item);
        }

        Content = wrap;
    }

    /// <summary>Detaches the items' root subscriptions; invoked by the presenter's overlay cleanup.</summary>
    internal void Cleanup()
    {
        foreach (var item in _items)
        {
            item.Unsubscribe();
        }
    }

    private static async Task HandleItemTapAsync(ScaffoldTabBar tabBar, ScaffoldRoot root, Func<Task> closeAsync)
    {
        // Close first, then navigate: a no-op selection (current root, empty stack) must
        // still dismiss the panel, and the engine's own sync would close it anyway.
        await closeAsync().ConfigureAwait(true);
        await tabBar.SelectRootAsync(root).ConfigureAwait(true);
    }
}
