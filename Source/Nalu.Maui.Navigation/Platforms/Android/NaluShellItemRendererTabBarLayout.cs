#if NET10_0_OR_GREATER
using System.Diagnostics.CodeAnalysis;
using Java.Lang;
#endif
using Android.Content;
using Android.Runtime;
using Android.Widget;
using AndroidX.Core.View;
using View = Android.Views.View;

namespace Nalu;

#pragma warning disable CS1591
public class NaluShellItemRendererTabBarLayout : FrameLayout
{
    private View? _tabBar;

    public NaluShellItemRendererTabBarLayout(IntPtr javaReference, JniHandleOwnership transfer) : base(javaReference, transfer)
    {
    }

#if NET10_0_OR_GREATER
    [DynamicDependency(DynamicallyAccessedMemberTypes.NonPublicMethods, "Microsoft.Maui.Platform.MauiWindowInsetListener", "Microsoft.Maui")]
#endif
    public NaluShellItemRendererTabBarLayout(Context context) : base(context)
    {
        // ReSharper disable once VirtualMemberCallInConstructor
        SetClipChildren(false);
#if NET10_0_OR_GREATER
        var type = Type.GetType("Microsoft.Maui.Platform.MauiWindowInsetListener, Microsoft.Maui") ?? throw new UnsupportedOperationException("The MAUI version you are using is not supported because MauiWindowInsetListener is missing.");
        type
            .GetMethod("RegisterParentForChildViews", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
            .Invoke(null, [this, null]);
#endif
    }

    public void SetTabBar(View? tabBar)
    {
        if (_tabBar?.Parent?.Handle == Handle)
        {
            RemoveView(_tabBar);
        }

        _tabBar = tabBar;

        if (tabBar != null)
        {
            AddView(tabBar);
        }
    }

    private static readonly int _unspecifiedHeightSpec = MeasureSpec.MakeMeasureSpec(0, Android.Views.MeasureSpecMode.Unspecified);

    protected override void OnMeasure(int widthMeasureSpec, int heightMeasureSpec)
    {
        int width;
        int height;

        if (_tabBar != null)
        {
            // The strip wraps the bar vertically, so the bar's height must be measured
            // UNCONSTRAINED: newer ConstraintLayout/Android versions (observed on API 37) hand
            // wrap-content children a finite height spec where older ones passed UNSPECIFIED,
            // and stretchy TabBarView roots would fill it (full-screen bar). The width spec
            // passes through untouched.
            _tabBar.Measure(widthMeasureSpec, _unspecifiedHeightSpec);
            width = _tabBar.MeasuredWidth;
            height = _tabBar.MeasuredHeight;
        }
        else
        {
            width = 0;
            height = 0;
        }

        SetMeasuredDimension(width, height);
    }

#if NET10_0_OR_GREATER
    protected override void OnLayout(bool changed, int left, int top, int right, int bottom)
    {
        if (_tabBar != null)
        {
            // View.Layout, NOT a LayoutViewGroup cast: MAUI wraps the tab bar view in a
            // ContainerView whenever it needs a container (Shadow, Clip, InputTransparent),
            // and the cast made any such TabBarView crash with an NRE here.
            _tabBar.Layout(0, 0, right, bottom - top);
        }
        else
        {
            base.OnLayout(changed, left, top, right, bottom);
        }
    }
#endif

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
        {
            ViewCompat.SetOnApplyWindowInsetsListener(this, null);
        }
    }
}
