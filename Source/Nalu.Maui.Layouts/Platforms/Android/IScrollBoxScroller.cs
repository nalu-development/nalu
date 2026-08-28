using AView = Android.Views.View;
using AViewGroup = Android.Views.ViewGroup;

namespace Nalu;

/// <summary>
/// The common surface of the two ScrollBox platform scrollers
/// (<see cref="NaluNestedScrollView" /> and <see cref="NaluHorizontalScrollView" />), which are
/// swapped at runtime when <see cref="ScrollBox.Orientation" /> changes.
/// </summary>
internal interface IScrollBoxScroller
{
    /// <summary>The scroller as an Android view.</summary>
    AView View { get; }

    /// <summary>The scroller as a view group (hosts the content wrapper).</summary>
    AViewGroup ViewGroup { get; }

    /// <summary>Gets or sets whether user scroll gestures are processed.</summary>
    bool ScrollGesturesEnabled { get; set; }

    /// <summary>True between touch-down and touch-up/cancel on the scroller.</summary>
    bool IsUserInteracting { get; }

    /// <summary>Invoked at the tail of every native layout pass.</summary>
    Action? LayoutCallback { get; set; }

    /// <summary>Invoked on every scroll position change.</summary>
    Action? ScrollChangedCallback { get; set; }

    /// <summary>Animates to the given pixel offsets (retargets any in-flight scroll).</summary>
    void SmoothScrollToPx(int x, int y);

    /// <summary>Stops any in-flight fling and jumps to the given pixel offsets.</summary>
    void StopAndJumpToPx(int x, int y);
}
