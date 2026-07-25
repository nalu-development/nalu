using Microsoft.Maui.Handlers;
using UIKit;

namespace Nalu;

public partial class ScaffoldHandler : ViewHandler<Scaffold, UIView>, IPlatformViewHandler
{
    private ScaffoldViewController? _viewController;

    /// <summary>Initializes a new <see cref="ScaffoldHandler"/>.</summary>
    public ScaffoldHandler()
        : base(ViewMapper)
    {
    }

    // The scaffold's root controller: MAUI hosts window-root pages through the handler's
    // ViewController (same integration point Shell's renderer uses), and the presenter
    // mounts page child view controllers onto it (UIKit containment).
    UIViewController? IPlatformViewHandler.ViewController => _viewController;

    /// <inheritdoc />
    protected override UIView CreatePlatformView()
    {
        _viewController = new ScaffoldViewController();

        return _viewController.View!;
    }

    /// <inheritdoc />
    protected override void ConnectHandler(UIView platformView)
    {
        base.ConnectHandler(platformView);

        if (VirtualView is { } scaffold && MauiContext is { } mauiContext)
        {
            // One presenter per connection: a re-attached handler starts from clean mount state.
            scaffold.Presenter = new ScaffoldPresenter(scaffold);
            _ = scaffold.InitializeAndPresentAsync(mauiContext.Services);
        }
    }

    /// <inheritdoc />
    protected override void DisconnectHandler(UIView platformView)
    {
        if (VirtualView is { } scaffold)
        {
            scaffold.Presenter = null;
        }

        _viewController = null;
        base.DisconnectHandler(platformView);
    }
}
