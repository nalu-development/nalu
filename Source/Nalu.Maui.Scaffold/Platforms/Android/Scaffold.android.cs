using AndroidX.Activity;
using AndroidX.AppCompat.App;
using Nalu.Internals;

namespace Nalu;

public partial class Scaffold
{
    private ScaffoldBackCallback? _backCallback;
    private bool _backCallbackRegistered;

    /// <summary>
    /// Registers the system-back handler on the activity's OnBackPressedDispatcher.
    /// This is the only reliable back channel on modern Android (predictive-back enforcement,
    /// targetSdk 36+, ignores the legacy KEYCODE_BACK path entirely), and the base predictive
    /// back integration builds on: <see cref="OnBackPressedCallback.Enabled"/> mirrors whether
    /// the Scaffold can pop, so the system's back-to-home preview still works at root pages.
    /// </summary>
    /// <remarks>
    /// ORDERING CONTRACT. The dispatcher delivers back events — including the predictive
    /// Started/Progressed stream — to the topmost ENABLED callback only. MAUI's
    /// MauiOnBackPressedCallback overrides just Pressed, so whenever it is enabled (apps with
    /// OnBackPressed lifecycle handlers) and sits above ours, pages pop with no scrub preview.
    /// Ours must therefore stay above MAUI's — and both are lifecycle-aware adds, which androidx
    /// re-adds on every ON_START in lifecycle-OBSERVER registration order. MAUI registers inside
    /// MauiAppCompatActivity.OnCreate but AFTER CreatePlatformWindow (where we first land here),
    /// so a same-frame add would register our observer first and hand MAUI the top slot forever.
    /// Deferring our add by one frame flips that: androidx itself then keeps us on top — at
    /// startup and after every foreground — with no dispatcher churn and nothing to re-assert.
    /// </remarks>
    internal void EnsureBackCallback(AppCompatActivity activity)
    {
        if (_backCallback is null || !ReferenceEquals(_backCallback.Activity, activity))
        {
            TearDownBackCallback();
            _backCallback = new ScaffoldBackCallback(this, activity);
        }

        if (!_backCallbackRegistered)
        {
            var callback = _backCallback;

            // Retried on every sync until it lands (the guards make it idempotent); the one-frame
            // gap is unreachable by a human back press. Registered with the activity as owner:
            // Remove() tears down the lifecycle observer along with the callback.
            activity.Window?.DecorView?.Post(() =>
                {
                    if (!_backCallbackRegistered && ReferenceEquals(_backCallback, callback))
                    {
                        activity.OnBackPressedDispatcher.AddCallback(activity, callback);
                        _backCallbackRegistered = true;
                    }
                }
            );
        }

        UpdateBackCallbackEnabled();
    }

    /// <summary>
    /// Re-evaluates whether the scaffold consumes system back: only while the current stack has
    /// pushed pages or an overlay is presented. At a root page the callback stays disabled so the
    /// platform default applies — the app backgrounds with the native predictive back-to-home
    /// preview intact. (Enabled lives on the callback object, so it survives the lifecycle
    /// remove/re-add cycles across background/foreground.)
    /// </summary>
    internal void UpdateBackCallbackEnabled()
    {
        if (_backCallback is not null)
        {
            _backCallback.Enabled = HasPushedPages() || Presenter is { HasOverlay: true };
        }
    }

    internal void TearDownBackCallback()
    {
        _backCallback?.Remove();
        _backCallback = null;
        _backCallbackRegistered = false;
    }

    private bool HasPushedPages()
        => (Proxy?.CurrentItem.CurrentSection as ScaffoldRootProxy)?.Root.NavigationStack.PushedPages.Count > 0;

    internal void HandleSystemBack()
    {
        // Overlays dismiss (topmost first) before the navigation engine is ever consulted —
        // the same policy §7.2 defines for popups.
        if (Presenter is { HasOverlay: true } presenter)
        {
            Dispatcher.Dispatch(() => presenter.CloseTopOverlayAsync().FireAndForget(Handler));

            return;
        }

        // A predictive-back preview settles its own visuals forward and dispatches the pop
        // through the engine with a handoff (the sync adopts the settled state).
        if (Presenter is ScaffoldPresenter { HasBackPreview: true } previewPresenter)
        {
            Dispatcher.Dispatch(() => previewPresenter.CommitBackPreviewAsync().FireAndForget(Handler));

            return;
        }

        // A plain Modal cannot be dismissed by system back (predictive or not): the callback
        // stays enabled (no back-to-home preview) and the press is consumed silently.
        // DismissableModal deliberately falls through to the engine pop.
        if (TopPushedPage() is { } topPage && GetPageMode(topPage) == ScaffoldPageMode.Modal)
        {
            return;
        }

        if (NavigationService is { } navigationService && HasPushedPages())
        {
            Dispatcher.Dispatch(() => navigationService.GoToAsync(Nalu.Navigation.Relative().Pop()).FireAndForget(Handler));
        }
    }

    private Page? TopPushedPage()
        => (Proxy?.CurrentItem.CurrentSection as ScaffoldRootProxy)?.Root.NavigationStack.PushedPages is { Count: > 0 } pushed
            ? pushed[^1].Page
            : null;
}
