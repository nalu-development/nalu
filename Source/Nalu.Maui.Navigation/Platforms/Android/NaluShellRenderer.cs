#nullable enable
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.Widget;
using Google.Android.Material.BottomNavigation;
using Google.Android.Material.Internal;
using Microsoft.Maui.Controls.Handlers.Compatibility;
using Microsoft.Maui.Controls.Platform.Compatibility;
using Microsoft.Maui.Platform;
using Color = Android.Graphics.Color;
using Font = Microsoft.Maui.Font;
using Toolbar = AndroidX.AppCompat.Widget.Toolbar;
using View = Android.Views.View;

namespace Nalu;

public class NaluShellRenderer : ShellRenderer
{
    protected override IShellBottomNavViewAppearanceTracker CreateBottomNavViewAppearanceTracker(ShellItem shellItem)
        => new NaluShellBottomNavViewAppearanceTracker(this, ((IPlatformViewHandler)this).MauiContext ?? throw new InvalidOperationException("MauiContext should be set at this point."), shellItem);

    protected override IShellToolbarTracker CreateTrackerForToolbar(Toolbar toolbar)
    {
        // https://github.com/dotnet/maui/issues/7045
        var shellToolbarTracker = base.CreateTrackerForToolbar(toolbar);
        shellToolbarTracker.TintColor = Colors.Black;

        return shellToolbarTracker;
    }
}

file class NaluShellBottomNavViewAppearanceTracker(IShellContext shellContext, IMauiContext mauiContext, ShellItem shellItem) : ShellBottomNavViewAppearanceTracker(shellContext, shellItem)
{
    private readonly IShellContext _shellContext = shellContext;
    private View? _nativeMauiBar;

    public override void SetAppearance(BottomNavigationView bottomView, IShellAppearanceElement appearance)
    {
        base.SetAppearance(bottomView, appearance);

        bottomView.Elevation = bottomView.Context!.ToPixels(8); // this should work with 2dp, but the shadow is almost invisible
        bottomView.Background = new ColorDrawable(Color.White); // background must be set for the shadow to be visible
        
        if (_nativeMauiBar == null)
        {
            var mauiBar = new Grid
                          {
                              BackgroundColor = Colors.Red
                          };

            var platformView = mauiBar.ToPlatform(mauiContext);
            _nativeMauiBar = platformView;
            _nativeMauiBar.TranslationZ = 1;
            _nativeMauiBar.Clickable = true;
            _nativeMauiBar.Focusable = true;
            bottomView.AddView(_nativeMauiBar);
        }

#pragma warning disable XAOBS001
        if (bottomView.GetChildAt(0) is not BottomNavigationMenuView bottomNavMenuView)
        {
            return;
        }

        var fontManager = _shellContext.Shell.Handler!.GetRequiredService<IFontManager>();
        var font = Font.OfSize("InterRegular", 12);
        var fontSelected = Font.OfSize("InterSemiBold", 12);
        var fontFace = fontManager.GetTypeface(font);
        var fontFaceSelected = fontManager.GetTypeface(fontSelected);

        // Thankfully items are already available at this point
        // So we can loop through them and set the typeface
        for (var i = 0; i < bottomNavMenuView.ChildCount; i++)
        {
            var item = (BottomNavigationItemView) bottomNavMenuView.GetChildAt(i)!;
            var itemTitle = item.GetChildAt(1);

            var baselineLayout = itemTitle as BaselineLayout;
            var smallTextView = (TextView?) baselineLayout?.GetChildAt(0);
            var largeTextView = (TextView?) baselineLayout?.GetChildAt(1);

            smallTextView?.SetTypeface(fontFace, TypefaceStyle.Normal);
            largeTextView?.SetTypeface(fontFaceSelected, TypefaceStyle.Normal);
        }
#pragma warning restore XAOBS001
    }
}
