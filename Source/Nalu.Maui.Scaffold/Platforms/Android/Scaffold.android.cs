using AndroidX.Activity;
using AndroidX.AppCompat.App;
using Nalu.Internals;

namespace Nalu;

public partial class Scaffold
{
    private ScaffoldBackCallback? _backCallback;

    /// <summary>
    /// Registers the system-back handler on the activity's OnBackPressedDispatcher.
    /// This is the only reliable back channel on modern Android (predictive-back enforcement,
    /// targetSdk 36+, ignores the legacy KEYCODE_BACK path entirely), and the base predictive
    /// back integration builds on: <see cref="OnBackPressedCallback.Enabled"/> mirrors whether
    /// the Scaffold can pop, so the system's back-to-home preview still works at root pages.
    /// </summary>
    internal void EnsureBackCallback(AppCompatActivity activity)
    {
        if (_backCallback is null || !ReferenceEquals(_backCallback.Activity, activity))
        {
            _backCallback?.Remove();
            _backCallback = new ScaffoldBackCallback(this, activity);
            activity.OnBackPressedDispatcher.AddCallback(activity, _backCallback);
        }

        UpdateBackCallbackEnabled();
    }

    /// <summary>
    /// Re-evaluates whether the scaffold consumes system back: only while the current stack has
    /// pushed pages. At a root page the callback stays disabled so the platform default applies —
    /// the app backgrounds with the native predictive back-to-home preview intact.
    /// </summary>
    internal void UpdateBackCallbackEnabled()
    {
        if (_backCallback is not null)
        {
            _backCallback.Enabled = HasPushedPages() || Presenter is { HasOverlay: true };
        }
    }

    private bool HasPushedPages()
        => (Proxy?.CurrentItem.CurrentSection as ScaffoldRootProxy)?.Root.NavigationStack.PushedPages.Count > 0;

    private void HandleSystemBack()
    {
        // Overlays (flyout, tab bar overflow panel) dismiss before the navigation engine
        // is ever consulted — the same policy §7.2 defines for popups.
        if (Presenter is { HasOverlay: true } presenter)
        {
            Dispatcher.Dispatch(() => presenter.CloseOverlayAsync().FireAndForget(Handler));

            return;
        }

        if (NavigationService is { } navigationService && HasPushedPages())
        {
            Dispatcher.Dispatch(() => navigationService.GoToAsync(Nalu.Navigation.Relative().Pop()).FireAndForget(Handler));
        }
    }

    internal sealed class ScaffoldBackCallback(Scaffold scaffold, AppCompatActivity activity) : OnBackPressedCallback(false)
    {
        public AppCompatActivity Activity => activity;

        public override void HandleOnBackPressed() => scaffold.HandleSystemBack();
    }
}
