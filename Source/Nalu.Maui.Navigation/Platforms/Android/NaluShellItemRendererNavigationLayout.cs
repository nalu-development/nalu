using Android.Content;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using AndroidX.Core.View;
using Insets = AndroidX.Core.Graphics.Insets;
using AView = Android.Views.View;

namespace Nalu;

#pragma warning disable CS1591
public class NaluShellItemRendererNavigationLayout : FrameLayout, IOnApplyWindowInsetsListener
{
    private static readonly int _systemBarsInsetsType = WindowInsetsCompat.Type.SystemBars();
    
    public NaluShellItemRendererNavigationLayout(IntPtr javaReference, JniHandleOwnership transfer) : base(javaReference, transfer)
    {
    }
    
    public NaluShellItemRendererNavigationLayout(Context context) : base(context)
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
        var parent = view?.Parent as NaluShellItemRendererOuterLayout ?? throw new InvalidOperationException("NaluShellItemRendererNavigationLayout cannot be used outside NaluShellItemRendererOuterLayout.");

        if (NaluShellItemRenderer.SupportsEdgeToEdge && parent.GetChildAt(1) is { Visibility: ViewStates.Visible, Height: >0 } tabBarLayout)
        {
            var systemBarInsets = insets.GetInsets(_systemBarsInsetsType) ?? throw new InvalidOperationException("SystemBars insets are null.");
            var tabBarHeight = tabBarLayout.Height;
            var modifiedSystemBarInsets = Insets.Of(systemBarInsets.Left, systemBarInsets.Top, systemBarInsets.Right, tabBarHeight);
            
            using var builder = new WindowInsetsCompat.Builder(insets);
            insets = builder
                     .SetInsets(_systemBarsInsetsType, modifiedSystemBarInsets)!
                     .Build();
        }

        return insets;
    }
}
