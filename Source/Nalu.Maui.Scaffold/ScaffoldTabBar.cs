namespace Nalu;

/// <summary>
/// A <see cref="ScaffoldArea"/> rendering a Nalu-drawn tab bar that switches between its roots.
/// </summary>
/// <remarks>
/// <para>
/// The bar itself is just a view: <see cref="TabBarView"/> defaults to a
/// <see cref="ScaffoldTabBarView"/> (the Telegram-style pill template carrying the whole styling
/// surface) and can be replaced with any custom view — this class carries only structure and
/// behavior, none of the default template's styling.
/// </para>
/// <para>
/// Tab selection always routes through the Nalu navigation engine (guards apply); switching to
/// another root restores its preserved navigation stack; tapping the active tab pops that
/// root's stack back to the root page. Custom bars call <see cref="SelectRootAsync"/>.
/// </para>
/// </remarks>
public class ScaffoldTabBar : ScaffoldArea
{
    /// <summary>
    /// Bindable property for <see cref="TabBarView"/>. Defaults to a fresh
    /// <see cref="ScaffoldTabBarView"/> per tab bar (created lazily via the default value factory).
    /// </summary>
    public static readonly BindableProperty TabBarViewProperty =
        BindableProperty.Create(
            nameof(TabBarView),
            typeof(View),
            typeof(ScaffoldTabBar),
            defaultValueCreator: _ => new ScaffoldTabBarView()
        );

    /// <summary>
    /// Gets or sets the view presented as the tab bar. Defaults to the Nalu
    /// <see cref="ScaffoldTabBarView"/>; replace it with any custom view — the view's binding
    /// context is this <see cref="ScaffoldTabBar"/> (exposing the roots and their selection
    /// state); call <see cref="SelectRootAsync"/> to trigger tab selection.
    /// </summary>
    public View? TabBarView
    {
        get => (View?)GetValue(TabBarViewProperty);
        set => SetValue(TabBarViewProperty, value);
    }

    /// <summary>
    /// Selects the given root through the Nalu navigation engine: switching to another root
    /// restores its preserved navigation stack; selecting the current root pops its stack back
    /// to the root page. Guards and lifecycle events always run — a guarded page can cancel the
    /// switch. Calls while ANY selection on the owning scaffold is in flight are ignored (the
    /// same scaffold-wide gate <see cref="ScaffoldRoot.SelectCommand"/> reports via
    /// <c>CanExecute</c>).
    /// </summary>
    /// <param name="root">The root to select; must belong to this tab bar's <see cref="ScaffoldArea.Roots"/>.</param>
    /// <returns>True when the selection navigation completed; false when a guard canceled it, another selection is already in flight, or the tab bar is not hosted in a Scaffold.</returns>
    public Task<bool> SelectRootAsync(ScaffoldRoot root)
        => this.GetScaffoldOrDefault() is { } scaffold ? scaffold.SelectRootGatedAsync(root) : Task.FromResult(false);

    /// <summary>
    /// Gets the view the presenter mounts as the bar (the default template or a user
    /// replacement), attaching it to the element tree: BindingContext and resource resolution
    /// flow through the scaffold structure, and the default template resolves this tab bar
    /// from its logical parent.
    /// </summary>
    internal View GetOrCreateBarView()
    {
        var barView = TabBarView;

        if (barView is null)
        {
            barView = new ScaffoldTabBarView();
            TabBarView = barView;
        }

        if (barView.Parent is null)
        {
            AddLogicalChild(barView);

            if (barView is not ScaffoldTabBarView)
            {
                barView.BindingContext = this;
            }
        }

        return barView;
    }

    /// <summary>
    /// Releases a bar view PERMANENTLY replaced by a live <c>TabBarView</c> swap (runtime
    /// replacement or XAML hot reload): unlike an area switch — where the outgoing bar stays
    /// alive for the return — a replaced bar is never remounted, so it is detached and its
    /// handlers disconnected.
    /// </summary>
    internal void OnBarViewReplaced(View oldBarView)
    {
        if (ReferenceEquals(oldBarView.Parent, this))
        {
            RemoveLogicalChild(oldBarView);
        }

        oldBarView.DisconnectHandlers();
    }

    /// <summary>
    /// Detaches the bar view from the element tree while the chrome is unmounted (hidden page,
    /// non-tab-bar area): the element tree reflects the actually-presented chrome.
    /// <see cref="GetOrCreateBarView"/> re-attaches on the next mount.
    /// </summary>
    internal void OnBarViewUnmounted()
    {
        if (TabBarView is { } barView && ReferenceEquals(barView.Parent, this))
        {
            RemoveLogicalChild(barView);
        }
    }

    /// <summary>
    /// Presents a panel anchored above the bottom chrome with the tab bar kept interactive —
    /// the same primitive the default template's "More" overflow uses, for custom tab bars
    /// (e.g. a special button opening its own panel).
    /// See <see cref="Scaffold.ShowTabBarPanelAsync"/> for the full contract.
    /// </summary>
    /// <param name="content">The panel view (reusable; horizontal margin insets it).</param>
    /// <param name="scrim">The scrim brush; a theme-aware translucent black when omitted.</param>
    /// <param name="closeIfOpened">Toggle (true, default) vs replace-in-place (false) when a panel is already presented.</param>
    public Task ShowPanelAsync(View content, Brush? scrim = null, bool closeIfOpened = true)
        => this.GetScaffoldOrDefault() is { } scaffold ? scaffold.ShowTabBarPanelAsync(content, scrim, closeIfOpened) : Task.CompletedTask;

    /// <summary>
    /// Opens the overflow panel listing the roots that don't fit the bar (toggling when already
    /// open). Only meaningful for the default <see cref="ScaffoldTabBarView"/> template; no-op
    /// when nothing overflows or a custom bar is installed.
    /// </summary>
    internal Task OpenOverflowAsync()
    {
        if (this.GetScaffoldOrDefault() is not { } scaffold
            || TabBarView is not ScaffoldTabBarView { OverflowRoots.Count: > 0 } barView)
        {
            return Task.CompletedTask;
        }

        if (scaffold.HasTabBarPanel)
        {
            // Toggle: the bar stays interactive above the scrim, so a second More tap can only
            // mean "dismiss" (the flyout's fullscreen scrim makes any other overlay unreachable).
            return scaffold.CloseTabBarPanelAsync();
        }

        var panel = new ScaffoldTabBarOverflowView(barView, scaffold.CloseTabBarPanelAsync)
        {
            Margin = new Thickness(barView.BarMargin.Left, 0, barView.BarMargin.Right, 0)
        };

        // Logical parenting: the panel participates in the element tree while presented
        // (BindingContext/resource flow, visual-tree visibility for tooling and UI tests).
        // Must precede the Scrim read below — implicit styles have applied by then.
        AddLogicalChild(panel);

        // The overflow set is recomputed per layout pass: rotation/resize migrating items
        // between bar and panel invalidates an open panel.
        barView.OverflowRootsChanged += OnOverflowRootsChanged;

        return scaffold.ShowTabBarPanelCoreAsync(
            panel,
            panel.Scrim,
            closeIfOpened: true,
            disconnectContentOnClose: true,
            cleanup: () =>
            {
                barView.OverflowRootsChanged -= OnOverflowRootsChanged;
                RemoveLogicalChild(panel);
                panel.Cleanup();
            }
        );

        void OnOverflowRootsChanged() => _ = scaffold.CloseTabBarPanelAsync();
    }
}
