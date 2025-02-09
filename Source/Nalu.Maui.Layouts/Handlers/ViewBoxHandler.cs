namespace Nalu;

using Microsoft.Maui.Handlers;

#if WINDOWS
using Microsoft.UI.Xaml.Media;
using WRect = global::Windows.Foundation.Rect;
using WSize = global::Windows.Foundation.Size;
using PlatformView = Nalu.ViewBoxPanel;
using OriginalPlatformView = Microsoft.Maui.Platform.ContentPanel;

#if NET9_0_OR_GREATER
internal partial class ViewBoxPanel : OriginalPlatformView
#else
#pragma warning disable CsWinRT1029
internal partial class ViewBoxPanel : OriginalPlatformView
#pragma warning restore CsWinRT1029
#endif
{
    public bool ClipsToBounds { get; set; }

    protected override WSize ArrangeOverride(WSize finalSize)
    {
        var actual = base.ArrangeOverride(finalSize);

        Clip = ClipsToBounds ? new RectangleGeometry { Rect = new WRect(0, 0, finalSize.Width, finalSize.Height) } : null;

        return actual;
    }
}
#endif

#if ANDROID
using Android.Content;
using ARect = Android.Graphics.Rect;
using PlatformView = Nalu.ClippableContentViewGroup;
using OriginalPlatformView = Microsoft.Maui.Platform.ContentViewGroup;

internal class ClippableContentViewGroup : OriginalPlatformView
{
    private readonly ARect _clipRect = new();

    public bool ClipsToBounds { get; set; }

#pragma warning disable IDE0290
    // ReSharper disable once ConvertToPrimaryConstructor
    public ClippableContentViewGroup(Context context) : base(context)
    {
    }
#pragma warning restore IDE0290

    protected override void OnLayout(bool changed, int left, int top, int right, int bottom)
    {
        base.OnLayout(changed, left, top, right, bottom);

        if (ClipsToBounds)
        {
            _clipRect.Right = right - left;
            _clipRect.Bottom = bottom - top;
            ClipBounds = _clipRect;
        }
        else
        {
            ClipBounds = null;
        }
    }
}
#endif

/// <summary>
/// Handler for the <see cref="IViewBox"/> view.
/// </summary>
public class ViewBoxHandler() : ContentViewHandler(Mapper)
{
    /// <summary>
    /// The property mapper for the <see cref="IViewBox"/> interface.
    /// </summary>
    public static new readonly IPropertyMapper<IViewBox, ViewBoxHandler> Mapper =
        new PropertyMapper<IViewBox, ViewBoxHandler>(ContentViewHandler.Mapper)
        {
            [nameof(IViewBox.ClipsToBounds)] = MapClipsToBounds,
        };

#if WINDOWS
    /// <inheritdoc />
    protected override OriginalPlatformView CreatePlatformView()
    {
        if (VirtualView == null)
        {
            throw new InvalidOperationException($"{nameof(VirtualView)} must be set to create a LayoutView");
        }

        var view = new PlatformView
        {
            CrossPlatformLayout = VirtualView
        };

        return view;
    }
#endif

#if ANDROID
    /// <inheritdoc />
    protected override OriginalPlatformView CreatePlatformView()
    {
        if (VirtualView == null)
        {
            throw new InvalidOperationException($"{nameof(VirtualView)} must be set to create a ContentViewGroup");
        }

        var viewGroup = new PlatformView(Context)
        {
            CrossPlatformLayout = VirtualView
        };

        viewGroup.SetClipChildren(false);

        return viewGroup;
    }
#endif

    private static void MapClipsToBounds(ViewBoxHandler handler, IViewBox view)
    {
        if (handler.PlatformView is not { } platformView)
        {
            return;
        }

#if IOS || MACCATALYST
        platformView.ClipsToBounds = view.ClipsToBounds;
#elif ANDROID
        ((PlatformView)platformView).ClipsToBounds = view.ClipsToBounds;
        platformView.RequestLayout();
#elif WINDOWS
        ((PlatformView)platformView).ClipsToBounds = view.ClipsToBounds;
        platformView.InvalidateArrange();
#endif
    }
}
