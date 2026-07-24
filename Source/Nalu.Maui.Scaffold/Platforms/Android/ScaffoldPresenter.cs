using Android.Views;
using AndroidX.AppCompat.App;
using AndroidX.Fragment.App;
using Microsoft.Maui.Platform;
using AView = Android.Views.View;
using AViewGroup = Android.Views.ViewGroup;

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

    private ScaffoldLayout? _hostPlatformView;
    private FragmentContainerView? _container;
    private ScaffoldPageFragment? _currentFragment;
    private Page? _currentPage;

    public async Task SynchronizeAsync(ScaffoldRoot root, ScaffoldPresentationHint hint)
    {
        if (scaffold.Handler is not IPlatformViewHandler { PlatformView: ScaffoldLayout platformView, MauiContext: { } mauiContext } ||
            platformView.Context?.GetActivity() is not AppCompatActivity activity)
        {
            return;
        }

        scaffold.EnsureBackCallback(activity);

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
}
