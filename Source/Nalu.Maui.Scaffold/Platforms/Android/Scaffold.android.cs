using AndroidX.Activity;
using AndroidX.AppCompat.App;
using Microsoft.Maui.LifecycleEvents;
using Nalu.Internals;

namespace Nalu;

public partial class Scaffold
{
    private ScaffoldBackCallback? _backCallback;
    private bool _backCallbackRegistered;
    private bool? _hasBackPressedDelegates;

    /// <summary>
    /// Whether the app registered <see cref="AndroidLifecycle.OnBackPressed"/> lifecycle
    /// delegates (popup frameworks and other libraries hook back this way). Cached: the
    /// lifecycle registry is fixed at builder time.
    /// </summary>
    private bool HasBackPressedLifecycleDelegates
        => _hasBackPressedDelegates ??=
            IPlatformApplication.Current?.Services?.GetService<ILifecycleEventService>()?.ContainsEvent(nameof(AndroidLifecycle.OnBackPressed)) ?? false;

    /// <summary>
    /// Gives the app's <see cref="AndroidLifecycle.OnBackPressed"/> delegates the same
    /// first-chance MAUI's own activity gives them (OR-ing their handled results), because
    /// under the Scaffold MAUI's callback — the only other caller of those delegates — is
    /// permanently disabled (its window reports CanConsumeBackNavigation=false for non-Shell
    /// content). Without this, libraries that close their popups from that hook never hear
    /// about back at all on predictive-back devices, where the legacy KEYCODE_BACK path that
    /// used to reach them no longer exists.
    /// </summary>
    private bool InvokeBackPressedLifecycleDelegates()
    {
        if (_backCallback is null)
        {
            return false;
        }

        var lifecycle = IPlatformApplication.Current?.Services?.GetService<ILifecycleEventService>();

        if (lifecycle is null)
        {
            return false;
        }

        var handled = false;

        foreach (var del in lifecycle.GetEventDelegates<AndroidLifecycle.OnBackPressed>(nameof(AndroidLifecycle.OnBackPressed)))
        {
            handled = del(_backCallback.Activity) || handled;
        }

        return handled;
    }

    /// <summary>
    /// Registers the system-back handler on the activity's OnBackPressedDispatcher.
    /// This is the only reliable back channel on modern Android (predictive-back enforcement,
    /// targetSdk 36+, ignores the legacy KEYCODE_BACK path entirely), and the base predictive
    /// back integration builds on: <see cref="OnBackPressedCallback.Enabled"/> mirrors whether
    /// the Scaffold can pop, so the system's back-to-home preview still works at root pages.
    /// </summary>
    /// <remarks>
    /// ORDERING CONTRACT. The dispatcher delivers back events — including the predictive
    /// Started/Progressed stream — to the topmost ENABLED callback only, and the ecosystem
    /// plants Pressed-only callbacks above ours at unpredictable times: MAUI's own callback
    /// re-adds itself on every ON_START, and third-party libraries (measured in the field with
    /// DevExpress popups) keep a PERMANENTLY ENABLED callback registered that swallows the
    /// predictive stream in its empty Started/Progressed defaults and re-dispatches Pressed
    /// down the chain — the page still pops, the scrub silently never runs. Top position is
    /// therefore ASSERTED, not assumed: on every presentation sync and on every
    /// disabled→enabled transition, <see cref="AssertBackCallbackOnTop"/> re-adds ours last.
    /// Every add is LIFECYCLE-aware (activity as owner), which also moves our lifecycle
    /// observer to the end of the observer list — so on each ON_START androidx itself re-adds
    /// ours after everyone else's, keeping it topmost across background/foreground with no
    /// extra machinery. The first add is deferred by one frame so MAUI's OnCreate registration
    /// exists to be ordered after. Cooperation with other callbacks is preserved by Enabled —
    /// while the Scaffold has nothing to consume, the dispatcher falls straight through.
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

            // Retried on every sync until it lands (the guards make it idempotent); the
            // one-frame gap is unreachable by a human back press.
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
        else
        {
            // Every sync re-asserts: a callback registered by a library since the last
            // navigation (popup services initialize lazily) would otherwise sit above ours.
            AssertBackCallbackOnTop();
        }

        UpdateBackCallbackEnabled();
    }

    /// <summary>
    /// Re-evaluates whether the scaffold consumes system back: only while the current stack has
    /// pushed pages or an overlay is presented. At a root page the callback stays disabled so the
    /// platform default applies — the app backgrounds with the native predictive back-to-home
    /// preview intact, and back events flow to whatever other callbacks the app registered.
    /// </summary>
    internal void UpdateBackCallbackEnabled()
    {
        if (_backCallback is null)
        {
            return;
        }

        var wasEnabled = _backCallback.Enabled;

        // Delegates keep the callback enabled even at rest: they must receive every back press
        // (that is the .NET MAUI activity contract), at the cost of the native back-to-home
        // preview — the exact trade MAUI itself makes when those delegates are registered.
        var enabled = HasPushedPages() || Presenter is { HasOverlay: true } || HasBackPressedLifecycleDelegates;
        _backCallback.Enabled = enabled;

        // Overlay presentations reach this WITHOUT a sync: the moment we start consuming back
        // is exactly when top position starts mattering.
        if (enabled && !wasEnabled)
        {
            AssertBackCallbackOnTop();
        }
    }

    /// <summary>
    /// Moves our callback to the top of the dispatcher (last position): remove + lifecycle-aware
    /// re-add is the ordering primitive, and it refreshes our ON_START observer position too
    /// (see the ordering contract on <see cref="EnsureBackCallback"/>). Enabled is untouched by
    /// either call. Skipped while a scrub is in flight — removing the in-progress callback
    /// would cancel the gesture mid-preview.
    /// </summary>
    internal void AssertBackCallbackOnTop()
    {
        if (_backCallback is null || !_backCallbackRegistered || Presenter is ScaffoldPresenter { HasBackPreview: true })
        {
            return;
        }

        _backCallback.Remove();
        _backCallback.Activity.OnBackPressedDispatcher.AddCallback(_backCallback.Activity, _backCallback);
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
        // A predictive-back preview in flight is a page-back the user ALREADY committed to with
        // the gesture — it takes priority over every other concern, and deliberately does NOT
        // consult the lifecycle delegates below. Those delegates model the MAUI "first chance to
        // intercept back" contract, but a predictive preview only ever exists for an unguarded
        // page (ILeavingGuard pages never preview) with no Nalu overlay open (StartBackPreview
        // bails on HasOverlay), and a foreign popup would own its own focused window — so there
        // is nothing here for a delegate to veto, and letting one (e.g. an analytics SDK that
        // returns handled) cancel the settle would snap the committed page back instead of
        // completing the exit. The preview settles forward and dispatches the pop through the
        // engine with a handoff (the sync adopts the settled state).
        if (Presenter is ScaffoldPresenter { HasBackPreview: true } previewPresenter)
        {
            Dispatcher.Dispatch(() => previewPresenter.CommitBackPreviewAsync().FireAndForget(Handler));

            return;
        }

        // No preview (button/key back, or a guarded page): the app's OnBackPressed lifecycle
        // delegates get the FIRST chance, exactly as they do under MAUI's own activity handling —
        // a popup framework that consumes here (e.g. closing its topmost popup) wins over every
        // Scaffold concern.
        if (InvokeBackPressedLifecycleDelegates())
        {
            return;
        }

        // Overlays dismiss (topmost first) before the navigation engine is ever consulted —
        // the same policy §7.2 defines for popups.
        if (Presenter is { HasOverlay: true } presenter)
        {
            Dispatcher.Dispatch(() => presenter.CloseTopOverlayAsync().FireAndForget(Handler));

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

            return;
        }

        // Enabled solely because lifecycle delegates exist (root page, nothing of ours to
        // consume) and no delegate handled it: mirror MauiAppCompatActivity.HandleBackNavigation
        // — re-dispatch with our callback disabled so the chain below us (other callbacks, the
        // activity default) runs and the app backgrounds.
        if (_backCallback is { Enabled: true } callback)
        {
            callback.Enabled = false;

            try
            {
                callback.Activity.OnBackPressedDispatcher.OnBackPressed();
            }
            finally
            {
                UpdateBackCallbackEnabled();
            }
        }
    }

    private Page? TopPushedPage()
        => (Proxy?.CurrentItem.CurrentSection as ScaffoldRootProxy)?.Root.NavigationStack.PushedPages is { Count: > 0 } pushed
            ? pushed[^1].Page
            : null;
}
