using Microsoft.Maui.Handlers;

namespace Nalu;

public partial class ScaffoldHandler : ViewHandler<Scaffold, ScaffoldLayout>
{
    /// <summary>Initializes a new <see cref="ScaffoldHandler"/>.</summary>
    public ScaffoldHandler()
        : base(ViewMapper)
    {
    }

    /// <inheritdoc />
    protected override ScaffoldLayout CreatePlatformView() => new(Context);

    /// <inheritdoc />
    protected override void ConnectHandler(ScaffoldLayout platformView)
    {
        base.ConnectHandler(platformView);

        if (VirtualView is { } scaffold && MauiContext is { } mauiContext)
        {
            // One presenter per connection: a re-attached handler (activity recreation) starts
            // from clean mount state. The back callback is (re)registered on first synchronize.
            scaffold.Presenter = new ScaffoldPresenter(scaffold);
            _ = scaffold.InitializeAndPresentAsync(mauiContext.Services);
        }
    }

    /// <inheritdoc />
    protected override void DisconnectHandler(ScaffoldLayout platformView)
    {
        if (VirtualView is { } scaffold)
        {
            (scaffold.Presenter as IDisposable)?.Dispose();
            scaffold.Presenter = null;
        }

        base.DisconnectHandler(platformView);
    }
}
