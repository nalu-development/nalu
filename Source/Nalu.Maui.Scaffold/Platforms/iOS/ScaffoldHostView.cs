using UIKit;

namespace Nalu;

/// <summary>
/// Base of the native views the scaffold creates to HOST MAUI views (the chrome strips, the overlay
/// panel host). Hosts are added once and removed once: leaving the superview IS retirement, and a
/// retired host gives back everything it hosts.
/// </summary>
/// <remarks>
/// <para>
/// The hosted MAUI views outlive their host — a tab bar area keeps its bar for the return, a page
/// host keeps its nav bar host across presenters, overlay content outlives its presentation — and a
/// retired host still holding one is a trap. Out of the hierarchy, only the managed peer retains the
/// host, so the GC may collect that peer while the native view lingers until its release drains on
/// the main thread. Any managed read of a hosted view's <c>Superview</c> in that window (the next
/// host adopting it, MAUI's measure-invalidation walk up from it) has to marshal a native host with
/// no managed instance, and throws "Failed to marshal the Objective-C object". A <c>NativeHandle</c>
/// constructor would only resurrect a host with none of its state.
/// </para>
/// <para>
/// Releasing on removal keeps that state unreachable whoever removes the host — the scaffold, UIKit
/// or MAUI — and at every retirement site, present or future. DIRECT subviews, whatever they are by
/// then: MAUI may have wrapped a hosted view in a container (shadow, clip) since it was mounted, and
/// the wrapper is what must leave.
/// </para>
/// </remarks>
internal abstract class ScaffoldHostView : UIView
{
    public override void MovedToSuperview()
    {
        base.MovedToSuperview();

        if (Superview is null)
        {
            foreach (var hosted in Subviews)
            {
                hosted.RemoveFromSuperview();
            }
        }
    }
}
