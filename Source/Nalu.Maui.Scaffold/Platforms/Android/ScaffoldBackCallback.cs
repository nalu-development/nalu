using AndroidX.Activity;
using AndroidX.AppCompat.App;

namespace Nalu;

/// <summary>
/// Predictive-back <see cref="OnBackPressedCallback"/> for <see cref="Scaffold"/>.
/// Kept as a <b>top-level</b> type so the Android Callable Wrapper exports the virtual
/// progress methods (<c>handleOnBackStarted</c> / Progressed / Cancelled) reliably.
/// </summary>
/// <remarks>
/// MAUI also registers <c>MauiOnBackPressedCallback</c> on the same dispatcher, but that
/// type only overrides <see cref="OnBackPressedCallback.HandleOnBackPressed"/>. When MAUI's
/// callback sits above ours and is enabled, the system delivers Started/Progressed to MAUI's
/// empty defaults — the page still pops on Pressed, but the scrub preview never runs.
/// Registration order guarantees this callback stays above MAUI's; see the ordering contract
/// on <c>Scaffold.EnsureBackCallback</c> (<c>Scaffold.android.cs</c>).
/// </remarks>
internal sealed class ScaffoldBackCallback(Scaffold scaffold, AppCompatActivity activity) : OnBackPressedCallback(false)
{
    public AppCompatActivity Activity => activity;

    public override void HandleOnBackStarted(BackEventCompat backEvent)
        => (scaffold.Presenter as ScaffoldPresenter)?.StartBackPreview();

    public override void HandleOnBackProgressed(BackEventCompat backEvent)
        => (scaffold.Presenter as ScaffoldPresenter)?.UpdateBackPreview(backEvent.Progress);

    public override void HandleOnBackCancelled()
        => (scaffold.Presenter as ScaffoldPresenter)?.CancelBackPreview();

    public override void HandleOnBackPressed() => scaffold.HandleSystemBack();
}
