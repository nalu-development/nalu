using Android.Content;
using Android.Runtime;
using Android.Views;
using AndroidX.ConstraintLayout.Widget;
using AndroidX.Core.View;

namespace Nalu;

#pragma warning disable CS1591
public class NaluShellItemRendererOuterLayout : ConstraintLayout
{
    private int _tabBarHeight;
    
    public NaluShellItemRendererOuterLayout(IntPtr javaReference, JniHandleOwnership transfer) : base(javaReference, transfer)
    {
    }

    public NaluShellItemRendererOuterLayout(Context context) : base(context)
    {
        // ReSharper disable once VirtualMemberCallInConstructor
        SetClipChildren(false);
    }

    protected override void OnLayout(bool changed, int left, int top, int right, int bottom)
    {
        base.OnLayout(changed, left, top, right, bottom);

        var tabBarView = GetChildAt(1)!;
        var height = tabBarView.Visibility == ViewStates.Gone ? 0 : tabBarView.Height;
        if (height == _tabBarHeight)
        {
            return;
        }

        _tabBarHeight = height;
        ViewCompat.RequestApplyInsets(GetChildAt(0));
    }
}
