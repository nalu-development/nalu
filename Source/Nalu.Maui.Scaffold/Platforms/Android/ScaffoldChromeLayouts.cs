using System.Diagnostics.CodeAnalysis;
using Android.Content;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using AndroidX.Core.View;
using Microsoft.Maui.Platform;
using AView = Android.Views.View;
using Insets = AndroidX.Core.Graphics.Insets;

namespace Nalu;

/// <summary>
/// Hosts the page content (the presenter's fragment container) and rewrites the system-bars
/// insets before they propagate down (§5.4): while the tab bar strip is visible, the bottom
/// inset becomes the strip height (which already covers the system inset) — the page treats the
/// bar exactly like a system bar. Mirrors NaluShellItemRendererNavigationLayout.
/// </summary>
internal sealed class ScaffoldPageLayerLayout : FrameLayout, AndroidX.Core.View.IOnApplyWindowInsetsListener
{
    private static readonly int _systemBarsInsetsType = WindowInsetsCompat.Type.SystemBars();

    public ScaffoldPageLayerLayout(IntPtr javaReference, JniHandleOwnership transfer)
        : base(javaReference, transfer)
    {
    }

    public ScaffoldPageLayerLayout(Context context)
        : base(context)
    {
        ViewCompat.SetOnApplyWindowInsetsListener(this, this);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
        {
            ViewCompat.SetOnApplyWindowInsetsListener(this, null);
        }
    }

    WindowInsetsCompat? AndroidX.Core.View.IOnApplyWindowInsetsListener.OnApplyWindowInsets(AView? view, WindowInsetsCompat? insets)
    {
        ArgumentNullException.ThrowIfNull(insets);

        if (view?.Parent is ScaffoldLayout { } scaffoldLayout
            && (scaffoldLayout.PageBottomInsetPx > 0 || scaffoldLayout.PageTopInsetPx > 0))
        {
            var systemBarInsets = insets.GetInsets(_systemBarsInsetsType) ?? throw new InvalidOperationException("SystemBars insets are null.");

            var modifiedSystemBarInsets = Insets.Of(
                systemBarInsets.Left,
                scaffoldLayout.PageTopInsetPx > 0 ? scaffoldLayout.PageTopInsetPx : systemBarInsets.Top,
                systemBarInsets.Right,
                scaffoldLayout.PageBottomInsetPx > 0 ? scaffoldLayout.PageBottomInsetPx : systemBarInsets.Bottom
            )!;

            using var builder = new WindowInsetsCompat.Builder(insets);

            insets = builder
                     .SetInsets(_systemBarsInsetsType, modifiedSystemBarInsets)!
                     .Build();
        }

        return insets;
    }
}

/// <summary>
/// Bottom chrome strip hosting the MAUI tab bar platform view: measures it against the full
/// width, adds the system bottom inset below it (the floating pill sits above the system
/// navigation area), and stays touch-transparent outside the bar content.
/// Mirrors NaluShellItemRendererTabBarLayout, including the MauiWindowInsetListener
/// registration required on .NET 10 so hosted MAUI views participate in the insets chain.
/// </summary>
internal sealed class ScaffoldTabBarStripLayout : FrameLayout
{
    private static readonly int _systemBarsInsetsType = WindowInsetsCompat.Type.SystemBars();

    private AView? _bar;
    private int _barMeasuredHeight;

    public AView? Bar => _bar;

    public ScaffoldTabBarStripLayout(IntPtr javaReference, JniHandleOwnership transfer)
        : base(javaReference, transfer)
    {
    }

    [DynamicDependency(DynamicallyAccessedMemberTypes.NonPublicMethods, "Microsoft.Maui.Platform.MauiWindowInsetListener", "Microsoft.Maui")]
    public ScaffoldTabBarStripLayout(Context context)
        : base(context)
    {
        SetClipChildren(false);

        var type = Type.GetType("Microsoft.Maui.Platform.MauiWindowInsetListener, Microsoft.Maui")
                   ?? throw new NotSupportedException("The MAUI version you are using is not supported because MauiWindowInsetListener is missing.");

        type
            .GetMethod("RegisterParentForChildViews", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
            .Invoke(null, [this, null]);
    }

    public void SetBar(AView? bar)
    {
        if (_bar?.Parent?.Handle == Handle)
        {
            RemoveView(_bar);
        }

        _bar = bar;

        if (bar is not null)
        {
            (bar.Parent as ViewGroup)?.RemoveView(bar);
            AddView(bar);
        }
    }

    protected override void OnMeasure(int widthMeasureSpec, int heightMeasureSpec)
    {
        if (_bar is null)
        {
            SetMeasuredDimension(0, 0);

            return;
        }

        MeasureChild(_bar, widthMeasureSpec, heightMeasureSpec);
        _barMeasuredHeight = _bar.MeasuredHeight;

        var bottomInset = ViewCompat.GetRootWindowInsets(this)?.GetInsets(_systemBarsInsetsType)?.Bottom ?? 0;
        SetMeasuredDimension(_bar.MeasuredWidth, _barMeasuredHeight + bottomInset);
    }

    protected override void OnLayout(bool changed, int left, int top, int right, int bottom)
    {
        if (_bar is LayoutViewGroup layoutViewGroup)
        {
            // MAUI layout roots need the explicit cross-platform arrange call (net10 behavior,
            // mirrored from NaluShellItemRendererTabBarLayout).
            layoutViewGroup.Layout(0, 0, right - left, _barMeasuredHeight);
        }
        else
        {
            _bar?.Layout(0, 0, right - left, _barMeasuredHeight);
        }
    }

    public override bool OnTouchEvent(MotionEvent? e)
        // Touch-transparent glass: only the bar's own children consume touches — taps on the
        // pill's side margins must reach the page below.
        => false;
}

/// <summary>
/// Top chrome strip hosting the MAUI nav bar platform view. Unlike the tab bar strip, the bar
/// view FILLS the strip (its background extends under the status bar) and consumes the
/// safe-area padding itself (SafeAreaEdges via the MauiWindowInsetListener registration) —
/// the measured height therefore already includes the status inset.
/// </summary>
internal sealed class ScaffoldNavBarStripLayout : FrameLayout
{
    private AView? _bar;
    private int _barMeasuredHeight;

    public AView? Bar => _bar;

    public ScaffoldNavBarStripLayout(IntPtr javaReference, JniHandleOwnership transfer)
        : base(javaReference, transfer)
    {
    }

    [DynamicDependency(DynamicallyAccessedMemberTypes.NonPublicMethods, "Microsoft.Maui.Platform.MauiWindowInsetListener", "Microsoft.Maui")]
    public ScaffoldNavBarStripLayout(Context context)
        : base(context)
    {
        SetClipChildren(false);

        var type = Type.GetType("Microsoft.Maui.Platform.MauiWindowInsetListener, Microsoft.Maui")
                   ?? throw new NotSupportedException("The MAUI version you are using is not supported because MauiWindowInsetListener is missing.");

        type
            .GetMethod("RegisterParentForChildViews", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
            .Invoke(null, [this, null]);
    }

    public void SetBar(AView? bar)
    {
        if (_bar?.Parent?.Handle == Handle)
        {
            RemoveView(_bar);
        }

        _bar = bar;

        if (bar is not null)
        {
            (bar.Parent as ViewGroup)?.RemoveView(bar);
            AddView(bar);
        }
    }

    protected override void OnMeasure(int widthMeasureSpec, int heightMeasureSpec)
    {
        if (_bar is null)
        {
            SetMeasuredDimension(0, 0);

            return;
        }

        MeasureChild(_bar, widthMeasureSpec, heightMeasureSpec);
        _barMeasuredHeight = _bar.MeasuredHeight;
        SetMeasuredDimension(_bar.MeasuredWidth, _barMeasuredHeight);
    }

    protected override void OnLayout(bool changed, int left, int top, int right, int bottom)
    {
        if (_bar is LayoutViewGroup layoutViewGroup)
        {
            layoutViewGroup.Layout(0, 0, right - left, _barMeasuredHeight);
        }
        else
        {
            _bar?.Layout(0, 0, right - left, _barMeasuredHeight);
        }
    }

    public override bool OnTouchEvent(MotionEvent? e)
        // Touch-transparent glass outside the bar content.
        => false;
}
