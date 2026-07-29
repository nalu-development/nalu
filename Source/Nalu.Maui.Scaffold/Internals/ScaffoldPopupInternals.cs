namespace Nalu;

/// <summary>The RESOLVED popup placement (call-site options ?? attached values ?? defaults).</summary>
internal sealed record ScaffoldPopupPresentation(
    ScaffoldPopupPlacement Placement,
    View? Anchor,
    Point AnchorOffset,
    Thickness Margin,
    IScaffoldPopupPlacer? CustomPlacer
);

/// <summary>
/// <see cref="IScaffoldPopup"/> implementation: a thin identity over the overlay-entry request —
/// the presenter's cleanup completes <see cref="Closed"/> on every close path.
/// </summary>
internal sealed class ScaffoldPopupHandle : IScaffoldPopup
{
    private readonly TaskCompletionSource _closed = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private Scaffold? _scaffold;
    private ScaffoldOverlayRequest? _request;

    public bool IsOpen => !_closed.Task.IsCompleted;

    public Task Closed => _closed.Task;

    /// <summary>Binds the handle to its presentation (before the presenter is invoked).</summary>
    internal void Attach(Scaffold scaffold, ScaffoldOverlayRequest request)
    {
        _scaffold = scaffold;
        _request = request;
    }

    /// <summary>Completes <see cref="Closed"/>; wired into the request's cleanup.</summary>
    internal void MarkClosed() => _closed.TrySetResult();

    public Task CloseAsync()
        => IsOpen && _scaffold is { Presenter: { } presenter } && _request is { } request
            ? presenter.CloseOverlayAsync(request)
            : Task.CompletedTask;

    public async ValueTask DisposeAsync() => await CloseAsync().ConfigureAwait(false);
}

/// <summary>
/// The shared popup placement math (device-independent coordinates; presenters supply the safe
/// area, the measured content size and the anchor frame). Anchor placements auto-flip when the
/// preferred side doesn't fit and clamp into the area; Start/End are logical (RTL-mapped).
/// </summary>
internal static class ScaffoldPopupPlacementResolver
{
    public static Rect Resolve(ScaffoldPopupPresentation presentation, Rect area, Size content, Rect? anchorBounds, bool isRtl)
    {
        if (presentation.CustomPlacer is { } placer)
        {
            return placer.Place(area, content, anchorBounds);
        }

        if (anchorBounds is not { } anchor || presentation.Placement == ScaffoldPopupPlacement.Center)
        {
            return new Rect(
                area.X + (area.Width - content.Width) / 2,
                area.Y + (area.Height - content.Height) / 2,
                content.Width,
                content.Height
            );
        }

        var offset = presentation.AnchorOffset;
        double x;
        double y;

        switch (presentation.Placement)
        {
            case ScaffoldPopupPlacement.AnchorAbove:
                x = StartAlignedX(anchor, content, offset, isRtl);
                y = anchor.Top - content.Height - offset.Y;

                if (y < area.Top)
                {
                    y = anchor.Bottom + offset.Y;
                }

                break;

            case ScaffoldPopupPlacement.AnchorStart:
            case ScaffoldPopupPlacement.AnchorEnd:
            {
                // Physical side of the anchor the popup leans to (logical Start/End, RTL-mapped).
                var toLeft = presentation.Placement == ScaffoldPopupPlacement.AnchorStart != isRtl;
                x = toLeft ? anchor.Left - content.Width - offset.X : anchor.Right + offset.X;

                if (toLeft ? x < area.Left : x + content.Width > area.Right)
                {
                    x = toLeft ? anchor.Right + offset.X : anchor.Left - content.Width - offset.X;
                }

                y = anchor.Top + offset.Y;

                break;
            }

            default: // AnchorBelow — the dropdown shape.
                x = StartAlignedX(anchor, content, offset, isRtl);
                y = anchor.Bottom + offset.Y;

                if (y + content.Height > area.Bottom)
                {
                    y = anchor.Top - content.Height - offset.Y;
                }

                break;
        }

        return new Rect(
            Math.Clamp(x, area.Left, Math.Max(area.Left, area.Right - content.Width)),
            Math.Clamp(y, area.Top, Math.Max(area.Top, area.Bottom - content.Height)),
            content.Width,
            content.Height
        );
    }

    /// <summary>The dropdown alignment: the popup's start edge rides the anchor's start edge.</summary>
    private static double StartAlignedX(Rect anchor, Size content, Point offset, bool isRtl)
        => isRtl
            ? anchor.Right - content.Width - offset.X
            : anchor.Left + offset.X;
}
