using System.ComponentModel;
using Android.OS;
using Android.Views;
using Android.Widget;
using AndroidX.ConstraintLayout.Widget;
using AndroidX.Core.View;
using Microsoft.Maui.Controls.Platform.Compatibility;
using Microsoft.Maui.Platform;
using AView = Android.Views.View;
using AViewGroup = Android.Views.ViewGroup;
using IAndroidXOnApplyWindowInsetsListener = AndroidX.Core.View.IOnApplyWindowInsetsListener;
using AInsets = AndroidX.Core.Graphics.Insets;

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace Nalu;

public class NaluShellItemRenderer : ShellItemRendererBase
{
    public static readonly bool SupportsEdgeToEdge
#if NET10_OR_GREATER
        = OperatingSystem.IsAndroidVersionAtLeast(35);
#else
        = false;
#endif
    
    private ConstraintLayout? _outerLayout;
    private FrameLayout? _navigationLayout;
    private FrameLayout? _tabBarLayout;
    private AView? _tabBar;
    private IMauiContext MauiContext => ShellContext.Shell.Handler?.MauiContext ?? throw new InvalidOperationException("MauiContext is not available.");

    public NaluShellItemRenderer(IShellContext shellContext)
        : base(shellContext)
    {
    }

    public override AView? OnCreateView(LayoutInflater inflater, AViewGroup? container, Bundle? savedInstanceState)
    {
        base.OnCreateView(inflater, container, savedInstanceState);
        
        var context = MauiContext.Context ?? throw new InvalidOperationException("Context is not available.");
        _outerLayout = new NaluShellItemRendererOuterLayout(context);
        _navigationLayout = new NaluShellItemRendererNavigationLayout(context);
        _tabBarLayout = new NaluShellItemRendererTabBarLayout(context);
        
        // generate IDs
        var navigationLayoutId = AView.GenerateViewId();
        var tabBarLayoutId = AView.GenerateViewId();
        var outerLayoutId = AView.GenerateViewId();
        _navigationLayout.Id = navigationLayoutId;
        _tabBarLayout.Id = tabBarLayoutId;
        _outerLayout.Id = outerLayoutId;
        
        // constrain navigation layout to fill the parent
        var navigationLayoutParams = new ConstraintLayout.LayoutParams(0, 0);
        navigationLayoutParams.TopToTop = outerLayoutId;

        if (SupportsEdgeToEdge)
        {
            navigationLayoutParams.BottomToBottom = outerLayoutId;
        }
        else
        {
            _outerLayout.SetFitsSystemWindows(true);
            navigationLayoutParams.BottomToTop = tabBarLayoutId;
        }

        navigationLayoutParams.StartToStart = outerLayoutId;
        navigationLayoutParams.EndToEnd = outerLayoutId;
        _navigationLayout.LayoutParameters = navigationLayoutParams;

        _outerLayout.AddView(_navigationLayout);
        
        // constrain tab bar layout to the bottom
        var tabBarLayoutParams = new ConstraintLayout.LayoutParams(0, AViewGroup.LayoutParams.WrapContent);
        tabBarLayoutParams.BottomToBottom = outerLayoutId;
        tabBarLayoutParams.StartToStart = outerLayoutId;
        tabBarLayoutParams.EndToEnd = outerLayoutId;
        _tabBarLayout.LayoutParameters = tabBarLayoutParams;
        
        _outerLayout.AddView(_tabBarLayout);

        UpdateTabBarView();
        
        HookEvents(ShellItem);

        return _outerLayout;
    }

    public override void OnDestroy()
    {
        base.OnDestroy();

        if (_tabBar?.Parent == _tabBarLayout && _tabBarLayout is not null)
        {
            _tabBarLayout.RemoveView(_tabBar);
        }

        _outerLayout?.Dispose();
        _navigationLayout?.Dispose();
        _tabBarLayout?.Dispose();
        
        _tabBar = null;
        _outerLayout = null;
        _navigationLayout = null;
        _tabBarLayout = null;
    }

    protected override AViewGroup GetNavigationTarget() => _navigationLayout ?? throw new InvalidOperationException("NavigationTarget has not been created yet.");

    protected override void OnShellItemPropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        base.OnShellItemPropertyChanged(sender, e);

        if (e.PropertyName == NaluShell.TabBarViewProperty.PropertyName)
        {
            UpdateTabBarView();
        }
    }

    private void UpdateTabBarView()
    {
        var tabBarView = NaluShell.GetTabBarView(ShellItem);

        if (_tabBar is not null)
        {
            _tabBarLayout?.RemoveView(_tabBar);
        }

        if (tabBarView == null)
        {
            _tabBar = null;
        }
        else
        {
            var mauiContext = ShellContext.Shell.Handler?.MauiContext ?? throw new NullReferenceException("MauiContext is null");
            var platformView = tabBarView.ToPlatform(mauiContext);
            _tabBar = platformView;
            _tabBarLayout?.AddView(platformView);
        }

        UpdateTabBarHidden();
    }

    private void UpdateTabBarHidden()
    {
        var isTabBarVisible = _tabBar is not null && (DisplayedPage?.GetValue(Shell.TabBarIsVisibleProperty) as bool? ?? true);

        if (_tabBarLayout is not null)
        {
            _tabBarLayout.Visibility = isTabBarVisible ? ViewStates.Visible : ViewStates.Gone;
            ViewCompat.RequestApplyInsets(_outerLayout);
        }
    }

    protected override void OnDisplayedPageChanged(Page? newPage, Page? oldPage)
    {
        base.OnDisplayedPageChanged(newPage, oldPage);

        if (oldPage is not null)
        {
            oldPage.PropertyChanged -= OnDisplayedElementPropertyChanged;
        }

        if (newPage is not null)
        {
            newPage.PropertyChanged += OnDisplayedElementPropertyChanged;
        }

        UpdateTabBarVisibility();
    }

    private void UpdateTabBarVisibility()
    {
        if (_tabBarLayout is null)
        {
            return;
        }

        var isTabBarVisible = DisplayedPage?.GetValue(Shell.TabBarIsVisibleProperty) as bool? ?? true;
        _tabBarLayout.Visibility = isTabBarVisible ? ViewStates.Visible : ViewStates.Gone;
    }

    private void OnDisplayedElementPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == Shell.TabBarIsVisibleProperty.PropertyName)
        {
            UpdateTabBarVisibility();
        }
    }
}

public class NaluShellItemRendererOuterLayout : ConstraintLayout
{
    private int _tabBarHeight;

    public NaluShellItemRendererOuterLayout(Android.Content.Context context) : base(context)
    {
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

public class NaluShellItemRendererNavigationLayout : FrameLayout, IAndroidXOnApplyWindowInsetsListener
{
    private static readonly int _systemBarsInsetsType = WindowInsetsCompat.Type.SystemBars();
    
    public NaluShellItemRendererNavigationLayout(Android.Content.Context context) : base(context)
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

    WindowInsetsCompat? IAndroidXOnApplyWindowInsetsListener.OnApplyWindowInsets(AView? view, WindowInsetsCompat? insets)
    {
        ArgumentNullException.ThrowIfNull(insets);
        var parent = view?.Parent as NaluShellItemRendererOuterLayout ?? throw new InvalidOperationException("NaluShellItemRendererNavigationLayout cannot be used outside NaluShellItemRendererOuterLayout.");

        if (NaluShellItemRenderer.SupportsEdgeToEdge && parent.GetChildAt(1) is { Visibility: ViewStates.Visible, Height: >0 } tabBarLayout)
        {
            var systemBarInsets = insets.GetInsets(_systemBarsInsetsType) ?? throw new InvalidOperationException("SystemBars insets are null.");
            var tabBarHeight = tabBarLayout.Height;
            var modifiedSystemBarInsets = AInsets.Of(systemBarInsets.Left, systemBarInsets.Top, systemBarInsets.Right, tabBarHeight);
            
            using var builder = new WindowInsetsCompat.Builder(insets);
            insets = builder
                              .SetInsets(_systemBarsInsetsType, modifiedSystemBarInsets)!
                              .Build();
        }

        return insets;
    }
}

public class NaluShellItemRendererTabBarLayout : FrameLayout, IAndroidXOnApplyWindowInsetsListener
{
    private static readonly int _systemBarsInsetsType = WindowInsetsCompat.Type.SystemBars();
    private int _systemBarInset;
    
    public NaluShellItemRendererTabBarLayout(Android.Content.Context context) : base(context)
    {
        ViewCompat.SetOnApplyWindowInsetsListener(this, this);
    }

    protected override void OnMeasure(int widthMeasureSpec, int heightMeasureSpec)
    {
        int width;
        int height;
        
        if (ChildCount > 0)
        {
            var child = GetChildAt(0)!;
            MeasureChild(child, widthMeasureSpec, heightMeasureSpec);
            width = child.MeasuredWidth;
            height = child.MeasuredHeight;

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

    WindowInsetsCompat? IAndroidXOnApplyWindowInsetsListener.OnApplyWindowInsets(AView? view, WindowInsetsCompat? insets)
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
