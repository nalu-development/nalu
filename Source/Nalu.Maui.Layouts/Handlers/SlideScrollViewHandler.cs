using Microsoft.Maui.Handlers;
using UIKit;
using View = Android.Views.View;
#if __IOS__ || MACCATALYST
using PlatformView = UIKit.UIScrollView;
#elif ANDROID
using PlatformView = Microsoft.Maui.Platform.MauiScrollView;
#elif WINDOWS
using PlatformView = Microsoft.UI.Xaml.Controls.ScrollViewer;
#elif TIZEN
using PlatformView = Tizen.UIExtensions.NUI.ScrollView;
#elif (NETSTANDARD || !PLATFORM) || (NET6_0_OR_GREATER && !IOS && !ANDROID && !TIZEN)
using PlatformView = System.Object;
#endif
namespace Nalu;

#if ANDROID
internal class SlideScrollViewListener : Java.Lang.Object, Android.Views.View.IOnTouchListener, Android.Views.View.IOnScrollChangeListener
{
    private readonly SlideScrollViewHandler _handler;

    public SlideScrollViewListener(SlideScrollViewHandler handler)
    {
        _handler = handler;
    }

    public bool OnTouch(Android.Views.View? v, Android.Views.MotionEvent? e)
    {
        if (e?.Action == Android.Views.MotionEventActions.Up)
        {
            _handler.VirtualView.SendDraggingEnded();
        }

        return false;
    }

    public void OnScrollChange(View? v, int scrollX, int scrollY, int oldScrollX, int oldScrollY)
    {
        
    }
}
#endif

internal class SlideScrollViewHandler : ScrollViewHandler
{
#if ANDROID
    private SlideScrollViewListener? _touchListener;
#endif
    
    public new SlideScrollView VirtualView => (SlideScrollView)base.VirtualView;

    protected override void ConnectHandler(PlatformView platformView)
    {
        base.ConnectHandler(platformView);

#if IOS || MACCATALYST
        platformView.PagingEnabled = true;
        platformView.DraggingEnded += OnDraggingEnded;
        platformView.DecelerationEnded += OnDecelerationEnded;
#elif ANDROID
        _touchListener = new SlideScrollViewListener(this);
        platformView.SetOnTouchListener(_touchListener);
#endif
    }


#if IOS || MACCATALYST
    private void OnDecelerationEnded(object? sender, EventArgs e) => VirtualView.SendDraggingEnded();

    private void OnDraggingEnded(object? sender, DraggingEventArgs e)
    {
        if (!e.Decelerate)
        {
            VirtualView.SendDraggingEnded();
        }
    }
#endif

    protected override void DisconnectHandler(PlatformView platformView)
    {
#if IOS || MACCATALYST
        platformView.DraggingEnded -= OnDraggingEnded;
        platformView.DecelerationEnded -= OnDecelerationEnded;
#elif ANDROID
        if (_touchListener != null)        {
            platformView.SetOnTouchListener(null);
            _touchListener.Dispose();
            _touchListener = null;
        }
#endif

        base.DisconnectHandler(platformView);
    }
}
