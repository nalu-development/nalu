using AndroidX.Activity;
using AndroidX.AppCompat.App;

namespace Nalu;

/// <summary>
/// Must stay a <b>top-level</b> type (not nested under <see cref="Scaffold"/>).
/// Nested <see cref="OnBackPressedCallback"/> subclasses often fail to receive the virtual
/// predictive-back JNI methods (<c>handleOnBackStarted</c> / Progressed / Cancelled) via the
/// Android Callable Wrapper — only the abstract <c>handleOnBackPressed</c> is dispatched —
/// so the page pops without a scrub preview. A top-level type generates a reliable ACW.
/// </summary>
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
