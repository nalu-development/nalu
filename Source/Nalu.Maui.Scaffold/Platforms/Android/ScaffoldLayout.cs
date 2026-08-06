using Android.Content;
using Android.Runtime;
using Android.Widget;
using AndroidX.Core.View;
using AView = Android.Views.View;

namespace Nalu;

/// <summary>
/// Platform root of a scaffold-hosted app: a plain FrameLayout — match-parent children
/// (the presenter's page layer, chrome layers, overlays) are measured and laid out natively.
/// Re-dispatches window insets to the page layer whenever the tab bar strip's height changes,
/// so the §5.4 inset rewrite always reflects the current chrome footprint.
/// </summary>
public sealed class ScaffoldLayout : FrameLayout
{
    /// <summary>The layer hosting page content (its insets are rewritten per §5.4).</summary>
    internal AView? PageLayer { get; set; }

    /// <summary>The bottom chrome strip (tab bar), when mounted.</summary>
    internal AView? TabBarLayer { get; set; }

    /// <summary>The top chrome strip (nav bar), when mounted.</summary>
    internal AView? NavBarLayer { get; set; }

    /// <summary>
    /// The bottom inset (px) the page layer rewrites into the system-bars insets. Deliberately
    /// DECOUPLED from the strip's visual state: bar hide/show animations never relayout the
    /// outgoing page — the presenter sets the target value BEFORE the fragment transaction and
    /// the incoming page attaches with its final insets.
    /// </summary>
    internal int PageBottomInsetPx { get; set; }

    /// <summary>The top inset (px) the page layer rewrites into the system-bars insets (see <see cref="PageBottomInsetPx"/>).</summary>
    internal int PageTopInsetPx { get; set; }

    /// <summary>Whether the presenter wants the bottom chrome footprint applied once the strip is measured.</summary>
    internal bool ChromeBottomDesired { get; set; }

    /// <summary>Whether the presenter wants the top chrome footprint applied once the strip is measured.</summary>
    internal bool ChromeTopDesired { get; set; }

    /// <summary>
    /// Full height (px) of the bottom chrome strip: bar footprint + system bottom inset.
    /// Zero when no tab bar is shown.
    /// </summary>
    internal int ChromeBottomFootprint
        => TabBarLayer is { Visibility: Android.Views.ViewStates.Visible } tabBar ? tabBar.Height : 0;

    /// <summary>Activation constructor.</summary>
    public ScaffoldLayout(IntPtr javaReference, JniHandleOwnership transfer)
        : base(javaReference, transfer)
    {
    }

    /// <summary>Initializes a new <see cref="ScaffoldLayout"/>.</summary>
    public ScaffoldLayout(Context context)
        : base(context)
    {
        SetClipChildren(false);
    }

    /// <inheritdoc />
    protected override void OnLayout(bool changed, int left, int top, int right, int bottom)
    {
        base.OnLayout(changed, left, top, right, bottom);

        // The strips' heights are only known after layout: on the first (or a re-)measure while
        // the chrome is desired, publish the footprints and re-dispatch insets to the page layer
        // (mirrors NaluShellItemRendererOuterLayout).
        var insetsChanged = false;

        if (ChromeBottomDesired && ChromeBottomFootprint is > 0 and var footprint && PageBottomInsetPx != footprint)
        {
            PageBottomInsetPx = footprint;
            insetsChanged = true;
        }

        if (ChromeTopDesired
            && NavBarLayer is { Visibility: Android.Views.ViewStates.Visible, Height: > 0 } navBar
            && PageTopInsetPx != navBar.Height)
        {
            PageTopInsetPx = navBar.Height;
            insetsChanged = true;
        }

        if (insetsChanged && PageLayer is { } pageLayer)
        {
            ViewCompat.RequestApplyInsets(pageLayer);
        }
    }

}
