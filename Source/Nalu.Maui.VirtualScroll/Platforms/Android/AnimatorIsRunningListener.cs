using AndroidX.RecyclerView.Widget;

namespace Nalu;

internal class AnimatorIsRunningListener : Java.Lang.Object, RecyclerView.ItemAnimator.IItemAnimatorFinishedListener
{
    private readonly Action _onAnimationFinished;

    public AnimatorIsRunningListener(Action onAnimationFinished)
    {
        _onAnimationFinished = onAnimationFinished;
    }

    public void OnAnimationsFinished() => _onAnimationFinished();
}
