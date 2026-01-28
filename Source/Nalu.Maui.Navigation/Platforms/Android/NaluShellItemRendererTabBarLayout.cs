#if NET10_0_OR_GREATER
using System.Diagnostics.CodeAnalysis;
using Java.Lang;
using Microsoft.Maui.Platform;
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

    protected override void OnMeasure(int widthMeasureSpec, int heightMeasureSpec)
    {
        int width;
        int height;
        
        if (_tabBar != null)
        {
            MeasureChild(_tabBar, widthMeasureSpec, heightMeasureSpec);
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
            (_tabBar as LayoutViewGroup)!.Layout(0, 0, right, bottom - top);
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
