using Android.Views;
using AndroidX.AppCompat.App;
using AndroidX.Fragment.App;
using Microsoft.Maui.Platform;
using AView = Android.Views.View;
using AViewGroup = Android.Views.ViewGroup;
using View = Microsoft.Maui.Controls.View;

namespace Nalu;

/// <summary>
/// Android presenter (P0): hosts the visible page in a fragment (the MAUI Shell hosting model
/// and the base for predictive-back integration), synchronizing to the stack model with a
/// minimal slide transition. Single-visible-page policy: one fragment replaced per sync; the
/// fragment back stack and the full transition engine arrive with P2.
/// </summary>
internal sealed class ScaffoldPresenter(Scaffold scaffold) : IScaffoldPresenter
{
    private const int _settleTimeoutMs = 2000;

    // Provisional chrome metrics (final styling surface arrives with the P1 API review).
    private const int _flyoutDurationMs = 250;
    private const double _flyoutWidthRatio = 0.85;
    private const double _flyoutMaxWidthDp = 360;
    private const float _flyoutScrimAlpha = 0.4f;

    private ScaffoldLayout? _hostPlatformView;
    private FragmentContainerView? _container;
    private ScaffoldPageFragment? _currentFragment;
    private Page? _currentPage;
    private AView? _flyoutScrim;
    private AView? _flyoutPanel;
    private ScaffoldFlyoutSide _flyoutSide;

    public async Task SynchronizeAsync(ScaffoldRoot root, ScaffoldPresentationHint hint)
    {
        if (scaffold.Handler is not IPlatformViewHandler { PlatformView: ScaffoldLayout platformView, MauiContext: { } mauiContext } ||
            platformView.Context?.GetActivity() is not AppCompatActivity activity)
        {
            return;
        }

        scaffold.EnsureBackCallback(activity);

        // Navigation dismisses any open flyout.
        await CloseFlyoutAsync();

        var container = EnsureContainer(platformView);
        var stack = root.NavigationStack;
        var targetPage = stack.PushedPages.Count > 0 ? stack.PushedPages[^1].Page : stack.RootPage;

        if (targetPage is null || ReferenceEquals(targetPage, _currentPage))
        {
            scaffold.UpdateBackCallbackEnabled();

            return;
        }

        var previousFragment = _currentFragment;
        var fragment = new ScaffoldPageFragment(mauiContext, targetPage, hint, container);
        _currentFragment = fragment;
        _currentPage = targetPage;

        // Async commit only: a synchronous commit can run while MAUI's own ScopedFragment
        // transaction is still executing on the same FragmentManager ("already executing").
        activity.SupportFragmentManager
                .BeginTransaction()
                .SetReorderingAllowed(true)
                .Replace(container.Id, fragment)
                .CommitAllowingStateLoss();

        // Deterministic completion: presentation of the new page plus dismissal animation of
        // the previous one, with a settle timeout as a safety net.
        var settled = Task.WhenAll(fragment.PresentedTask, previousFragment?.DismissedTask ?? Task.CompletedTask);
        await Task.WhenAny(settled, Task.Delay(_settleTimeoutMs)).ConfigureAwait(true);

        scaffold.UpdateBackCallbackEnabled();
    }

    private FragmentContainerView EnsureContainer(ScaffoldLayout platformView)
    {
        // The host platform view changes when the activity is recreated (system back at root,
        // configuration change): the old container and mounted fragment died with it.
        if (_container is not null && ReferenceEquals(_hostPlatformView, platformView))
        {
            return _container;
        }

        _hostPlatformView = platformView;
        _currentFragment = null;
        _currentPage = null;

        // ScaffoldLayout is a FrameLayout: a match-parent child is measured and laid out natively.
        var container = new FragmentContainerView(platformView.Context!) { Id = AView.GenerateViewId() };
        _container = container;
        platformView.AddView(container, new AViewGroup.LayoutParams(AViewGroup.LayoutParams.MatchParent, AViewGroup.LayoutParams.MatchParent));

        return container;
    }

    public async Task OpenFlyoutAsync(ScaffoldFlyoutSide side, View content)
    {
        if (_flyoutPanel is not null
            || scaffold.Handler is not IPlatformViewHandler { PlatformView: ScaffoldLayout platformView, MauiContext: { } mauiContext }
            || platformView.Context is not { } context)
        {
            return;
        }

        var widthPx = (int)Math.Min(platformView.Width * _flyoutWidthRatio, context.ToPixels(_flyoutMaxWidthDp));

        var scrim = new AView(context) { Clickable = true, Alpha = 0 };
        scrim.SetBackgroundColor(Android.Graphics.Color.Black);
        scrim.Click += (_, _) => _ = CloseFlyoutAsync();
        platformView.AddView(scrim, new AViewGroup.LayoutParams(AViewGroup.LayoutParams.MatchParent, AViewGroup.LayoutParams.MatchParent));

        var panel = content.ToPlatform(mauiContext);
        (panel.Parent as AViewGroup)?.RemoveView(panel);
        panel.LayoutParameters = new Android.Widget.FrameLayout.LayoutParams(widthPx, AViewGroup.LayoutParams.MatchParent)
        {
            Gravity = side == ScaffoldFlyoutSide.Start ? GravityFlags.Start : GravityFlags.End
        };
        panel.TranslationX = side == ScaffoldFlyoutSide.Start ? -widthPx : widthPx;
        platformView.AddView(panel);

        _flyoutScrim = scrim;
        _flyoutPanel = panel;
        _flyoutSide = side;

        await AnimateFlyoutAsync(scrim, panel, scrimAlpha: _flyoutScrimAlpha, panelTranslationX: 0);
    }

    public async Task CloseFlyoutAsync()
    {
        if (_flyoutPanel is not { } panel || _flyoutScrim is not { } scrim)
        {
            return;
        }

        _flyoutPanel = null;
        _flyoutScrim = null;

        var offscreenX = _flyoutSide == ScaffoldFlyoutSide.Start ? -panel.Width : panel.Width;
        await AnimateFlyoutAsync(scrim, panel, scrimAlpha: 0, panelTranslationX: offscreenX);

        (panel.Parent as AViewGroup)?.RemoveView(panel);
        (scrim.Parent as AViewGroup)?.RemoveView(scrim);
    }

    private static Task AnimateFlyoutAsync(AView scrim, AView panel, float scrimAlpha, float panelTranslationX)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var panelAnimator = Android.Animation.ObjectAnimator.OfFloat(panel, "translationX", panel.TranslationX, panelTranslationX)!;
        panelAnimator.SetDuration(_flyoutDurationMs);
        panelAnimator.AnimationEnd += (_, _) => completion.TrySetResult();

        var scrimAnimator = Android.Animation.ObjectAnimator.OfFloat(scrim, "alpha", scrim.Alpha, scrimAlpha)!;
        scrimAnimator.SetDuration(_flyoutDurationMs);

        panelAnimator.Start();
        scrimAnimator.Start();

        return completion.Task;
    }
}
