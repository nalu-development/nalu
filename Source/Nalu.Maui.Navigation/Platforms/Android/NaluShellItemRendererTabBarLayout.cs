using Android.Content;
using Android.Runtime;
using Android.Widget;
using AndroidX.Core.View;
using View = Android.Views.View;

namespace Nalu;

#pragma warning disable CS1591
public class NaluShellItemRendererTabBarLayout : FrameLayout, IOnApplyWindowInsetsListener
{
    private static readonly int _systemBarsInsetsType = WindowInsetsCompat.Type.SystemBars();
    private int _systemBarInset;
    private View? _tabBar;

    public NaluShellItemRendererTabBarLayout(IntPtr javaReference, JniHandleOwnership transfer) : base(javaReference, transfer)
    {
    }
    
    public NaluShellItemRendererTabBarLayout(Context context) : base(context)
    {
        ViewCompat.SetOnApplyWindowInsetsListener(this, this);
        // ReSharper disable once VirtualMemberCallInConstructor
        SetClipChildren(false);
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

            if (height > 0)
            {
                height += _systemBarInset;
            }
        }
        else
        {
            width = 0;
            height = 0;
        }
        
        SetMeasuredDimension(width, height);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
        {
            ViewCompat.SetOnApplyWindowInsetsListener(this, null);
        }
    }

    WindowInsetsCompat AndroidX.Core.View.IOnApplyWindowInsetsListener.OnApplyWindowInsets(View? view, WindowInsetsCompat? insets)
    {
        ArgumentNullException.ThrowIfNull(insets);
        var bottomInset = insets.GetInsets(_systemBarsInsetsType)?.Bottom ?? throw new InvalidOperationException("SystemBars insets are null.");

        if (_systemBarInset != bottomInset)
        {
            _systemBarInset = bottomInset;
            RequestLayout();
        }

        return insets;
    }
}
