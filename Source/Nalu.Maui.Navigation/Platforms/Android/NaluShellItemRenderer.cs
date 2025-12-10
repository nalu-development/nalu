using System.ComponentModel;
using Android.Content;
using Android.Graphics;
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
using ARenderEffect = Android.Graphics.RenderEffect;
using JniHandleOwnership = Android.Runtime.JniHandleOwnership;
using View = Microsoft.Maui.Controls.View;

// ReSharper disable VirtualMemberCallInConstructor

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace Nalu;

public class NaluShellItemRenderer : ShellItemRendererBase
{
    public static readonly bool SupportsEdgeToEdge
#if NET10_0_OR_GREATER
        = true;
#else
        = false;
#endif
    
    private NaluShellItemRendererOuterLayout? _outerLayout;
    private NaluShellItemRendererNavigationLayout? _navigationLayout;
    private NaluShellItemRendererTabBarLayout? _tabBarLayout;
    private FrameLayout? _navigationTarget;
    private AView? _tabBar;
    private View? _crossPlatformTabBar;
    private AView? _tabBarScrim;
    private View? _crossPlatformTabBarScrim;
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
        _tabBarLayout = new NaluShellItemRendererTabBarLayout(context);
        _navigationLayout = SupportsEdgeToEdge && NaluTabBar.UseBlurEffect && NaluShellItemRendererNavigationBlurSupportLayout.IsSupported
            ? new NaluShellItemRendererNavigationBlurSupportLayout(context)
            : new NaluShellItemRendererNavigationLayout(context);

        _navigationTarget = new FrameLayout(context);
        _navigationLayout.AddView(_navigationTarget);
        
        // generate IDs
        var navigationTargetId = AView.GenerateViewId();
        var navigationLayoutId = AView.GenerateViewId();
        var tabBarLayoutId = AView.GenerateViewId();
        var outerLayoutId = AView.GenerateViewId();
        _navigationTarget.Id = navigationTargetId;
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
        UpdateTabBarScrimView();
        
        HookEvents(ShellItem);

        return _outerLayout;
    }

    public override void OnDestroy()
    {
        base.OnDestroy();

        _tabBarLayout?.SetTabBar(null);

        _tabBar = null;
        _crossPlatformTabBar?.DisconnectHandlers();
        _crossPlatformTabBar = null;
        
        _tabBarScrim?.RemoveFromParent();
        _tabBarScrim = null;
        _crossPlatformTabBarScrim?.DisconnectHandlers();
        _crossPlatformTabBarScrim = null;

        _navigationTarget?.Dispose();

        _outerLayout?.Dispose();
        _navigationLayout?.Dispose();
        _tabBarLayout?.Dispose();

        _outerLayout = null;
        _navigationLayout = null;
        _tabBarLayout = null;
        
        UnhookEvents(ShellItem);
    }

    protected override AViewGroup GetNavigationTarget() => _navigationTarget ?? throw new InvalidOperationException("NavigationTarget has not been created yet.");

    protected override void OnShellItemPropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        base.OnShellItemPropertyChanged(sender, e);

        if (e.PropertyName == NaluShell.TabBarViewProperty.PropertyName)
        {
            UpdateTabBarView();
        }
        else if (e.PropertyName == NaluShell.TabBarScrimViewProperty.PropertyName)
        {
            UpdateTabBarScrimView();
        }
    }

    private void UpdateTabBarView()
    {
        var tabBarView = NaluShell.GetTabBarView(ShellItem);

        _tabBar?.RemoveFromParent();
        _tabBar = null;
        _crossPlatformTabBar?.DisconnectHandlers();
        _crossPlatformTabBar = null;
        
        _tabBarScrim?.RemoveFromParent();
        _tabBarScrim = null;
        _crossPlatformTabBarScrim?.DisconnectHandlers();
        _crossPlatformTabBarScrim = null;

        if (tabBarView == null)
        {
            _tabBarLayout?.SetTabBar(null);
        }
        else
        {
            var mauiContext = ShellContext.Shell.Handler?.MauiContext ?? throw new NullReferenceException("MauiContext is null");
            var platformView = tabBarView.ToPlatform(mauiContext);
            _tabBar = platformView;
            _tabBarLayout?.SetTabBar(platformView);
            _crossPlatformTabBar = tabBarView;
        }

        UpdateTabBarHidden();
    }
    
    private void UpdateTabBarScrimView()
    {
        var tabBarScrimView = NaluShell.GetTabBarScrimView(ShellItem);

        _tabBarScrim?.RemoveFromParent();
        _tabBarScrim = null;
        _crossPlatformTabBarScrim?.DisconnectHandlers();
        _crossPlatformTabBarScrim = null;

        if (tabBarScrimView != null)
        {
            var mauiContext = ShellContext.Shell.Handler?.MauiContext ?? throw new NullReferenceException("MauiContext is null");
            var platformView = tabBarScrimView.ToPlatform(mauiContext);
            _tabBarScrim = platformView;
            _crossPlatformTabBarScrim = tabBarScrimView;
            _navigationLayout!.AddView(_tabBarScrim);
        }
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
    
    public NaluShellItemRendererOuterLayout(IntPtr javaReference, JniHandleOwnership transfer) : base(javaReference, transfer)
    {
    }

    public NaluShellItemRendererOuterLayout(Context context) : base(context)
    {
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

public class NaluShellItemRendererNavigationLayout : FrameLayout, IAndroidXOnApplyWindowInsetsListener
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

public class NaluShellItemRendererNavigationBlurSupportLayout : NaluShellItemRendererNavigationLayout
{
    public static readonly bool IsSupported = OperatingSystem.IsAndroidVersionAtLeast(31);
    
#pragma warning disable CA1416
    private readonly RenderNode _contentRenderNode = new("NaluShellItemRendererNavigationLayoutContent");
    private readonly RenderNode _blurRenderNode = new("NaluShellItemRendererNavigationLayoutBlur");
#pragma warning restore CA1416
    private ARenderEffect? _blurEffect;
    private ARenderEffect? _shaderEffect;
    private ARenderEffect? _combinedEffect;
    private Shader? _shader;

    private AView TabBarLayout => ((AViewGroup) Parent!).GetChildAt(1)!;
    
    public NaluShellItemRendererNavigationBlurSupportLayout(IntPtr javaReference, JniHandleOwnership transfer) : base(javaReference, transfer)
    {
    }

    public NaluShellItemRendererNavigationBlurSupportLayout(Context context) : base(context)
    {
    }

    protected override void DispatchDraw(Canvas canvas)
    {
        if (canvas.IsHardwareAccelerated)
        {
#pragma warning disable CA1416
            var canvasWidth = canvas.Width;
            var canvasHeight = canvas.Height;
            _contentRenderNode.SetPosition(0, 0, canvasWidth, canvasHeight);
            using var contentCanvas = _contentRenderNode.BeginRecording();
            base.DispatchDraw(contentCanvas);
            _contentRenderNode.EndRecording();

            canvas.DrawRenderNode(_contentRenderNode);

            var tabBarLayout = TabBarLayout;
            
            // Commented out for now
            var tabBarHeight = tabBarLayout.Visibility == ViewStates.Gone ? 0 : tabBarLayout.Height;
            
            if (tabBarHeight > 0)
            {
                DisposeResources();

                _blurRenderNode.SetPosition(0, 0, canvasWidth, tabBarHeight);
                _blurRenderNode.SetTranslationY(canvasHeight - tabBarHeight);
                
                _blurEffect = NaluTabBar.BlurEffectFactory(Context!);
                _shader = NaluTabBar.BlurShaderFactory(canvasWidth, tabBarHeight);

                if (_shader is null)
                {
                    _blurRenderNode.SetRenderEffect(_blurEffect);
                }
                else
                {
                    // Create a color filter effect that uses the gradient as a mask
                    _shaderEffect = ARenderEffect.CreateShaderEffect(_shader);
                    _combinedEffect = ARenderEffect.CreateBlendModeEffect(_shaderEffect, _blurEffect, Android.Graphics.BlendMode.SrcIn!);
                    _blurRenderNode.SetRenderEffect(_combinedEffect);
                }

                using var blurCanvas = _blurRenderNode.BeginRecording();
                blurCanvas.Translate(0f, -(canvasHeight - tabBarHeight));
                blurCanvas.DrawRenderNode(_contentRenderNode);
                _blurRenderNode.EndRecording();
                
                canvas.DrawRenderNode(_blurRenderNode);
            }
#pragma warning restore CA1416
        }
        else
        {
            base.DispatchDraw(canvas);
        }
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
        {
            DisposeResources();
            _blurRenderNode.Dispose();
            _contentRenderNode.Dispose();
        }
    }

    private void DisposeResources()
    {
        _shader?.Dispose();
        _blurEffect?.Dispose();
        _shaderEffect?.Dispose();
        _combinedEffect?.Dispose();
                
        _shader = null;
        _blurEffect = null;
        _shaderEffect = null;
        _combinedEffect = null;
    }
}

public class NaluShellItemRendererTabBarLayout : FrameLayout, IAndroidXOnApplyWindowInsetsListener
{
    private static readonly int _systemBarsInsetsType = WindowInsetsCompat.Type.SystemBars();
    private int _systemBarInset;
    private AView? _tabBar;

    public NaluShellItemRendererTabBarLayout(IntPtr javaReference, JniHandleOwnership transfer) : base(javaReference, transfer)
    {
    }
    
    public NaluShellItemRendererTabBarLayout(Context context) : base(context)
    {
        ViewCompat.SetOnApplyWindowInsetsListener(this, this);
        SetClipChildren(false);
    }

    public void SetTabBar(AView? tabBar)
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

    WindowInsetsCompat IAndroidXOnApplyWindowInsetsListener.OnApplyWindowInsets(AView? view, WindowInsetsCompat? insets)
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
