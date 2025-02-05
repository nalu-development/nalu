namespace Nalu;

#if WINDOWS
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using WRect = global::Windows.Foundation.Rect;
using WSize = global::Windows.Foundation.Size;
using PlatformView = Nalu.ViewBoxPanel;
#endif

using Microsoft.Maui.Handlers;

#if WINDOWS
internal class ViewBoxPanel : Microsoft.Maui.Platform.ContentPanel
{
    public bool ClipsToBounds { get; set; }

    protected override WSize ArrangeOverride(WSize finalSize)
	{
		var actual = base.ArrangeOverride(finalSize);

		if (!(Parent is ContentPanel contentPanel && contentPanel.BorderStroke?.Shape is not null))
		{
			Clip = ClipsToBounds ? new RectangleGeometry { Rect = new WRect(0, 0, finalSize.Width, finalSize.Height) } : null;
		}

		return actual;
	}
}
#endif

/// <summary>
/// Handler for the <see cref="IViewBox"/> view.
/// </summary>
public class ViewBoxHandler : ContentViewHandler
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
    protected override PlatformView CreatePlatformView()
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

    private static void MapClipsToBounds(ViewBoxHandler handler, IViewBox view)
    {
        if (handler.PlatformView is not { } platformView)
        {
            return;
        }

#if IOS || MACCATALYST
        platformView.ClipsToBounds = view.ClipsToBounds;
#elif ANDROID
        platformView.SetClipChildren(view.ClipsToBounds);
#elif WINDOWS
        platformView.ClipsToBounds = view.ClipsToBounds;
        platformView.InvalidateArrange();
#endif
    }
}
