using System.Diagnostics;
using CoreAnimation;
using CoreGraphics;
using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Platform;
using UIKit;

namespace Nalu;

/// <summary>
/// iOS shared-element engine (ported from PoC spike A, §8): C# computes the flight geometry
/// once, UIViewPropertyAnimator/Core Animation does all per-frame work natively.
/// Every pair flies inside the shared overlay as a clipping flight container so stacking is
/// controlled (images at the bottom, scrims/labels above, matching the live layouts). Flights
/// travel between VISIBLE rects — an element partially clipped by an ancestor (parallax bleeds,
/// scrolled-out content) flies as the user sees it, not as its unclipped frame says.
/// Image pairs morph their aspect crop; any other pair cross-fades two pre-rendered copies.
/// The page motion matches the presenter's plain slide, so transitions with and without shared
/// elements move identically.
/// </summary>
internal static class ScaffoldSharedElementTransitions
{
    private const double _layoutWaitTimeoutMs = 500;

    private sealed record TagPair(string Name, UIView From, UIView To, nfloat FromRadius, nfloat ToRadius, bool IsImagePair);

    private sealed record PairGeometry(CGRect FromFull, CGRect FromVisible, CGRect ToFull, CGRect ToVisible);

    private sealed record ImageFlight(UIView Flight, UIImageView FlyingImage, CGRect FromAspect, CGRect ToAspect);

    private sealed record SnapshotFlight(UIView Flight, UIView FromSnapshot, UIView ToSnapshot, CGRect FromInner, CGRect ToInner, nfloat FromAlpha, nfloat ToAlpha);

    /// <summary>
    /// Animates a push with shared elements. Returns false (without animating) when no pair
    /// matches or the incoming views never got laid out — the caller falls back to the plain
    /// slide. The incoming platform view must already be mounted at its final frame.
    /// </summary>
    public static async Task<bool> AnimatePushAsync(UIView container, IMauiContext mauiContext, Page outgoingPage, Page incomingPage, UIView outgoingView, UIView incomingView, double durationSeconds)
    {
        var pairs = MatchPairs(mauiContext, outgoingPage, incomingPage);

        if (pairs.Count == 0)
        {
            return false;
        }

        // Gate #1 (incoming readiness): end frames only exist once the target views are laid out.
        incomingView.LayoutIfNeeded();
        await WaitForLayoutAsync(pairs.Select(p => p.To).Append(incomingView).ToList());

        if (pairs.Any(p => !IsLaidOut(p.To)))
        {
            return false;
        }

        await RunSessionAsync(
            container,
            pairs,
            movingView: incomingView,
            movingFromOffscreen: true,
            counterpartView: outgoingView,
            durationSeconds
        );

        return true;
    }

    /// <summary>
    /// Animates a pop with shared elements: flights travel from the popped page to the revealed
    /// page while the popped page slides out. Returns false when no pair matches or the
    /// revealed views never got laid out.
    /// </summary>
    public static async Task<bool> AnimatePopAsync(UIView container, IMauiContext mauiContext, Page poppedPage, Page revealedPage, UIView poppedView, UIView revealedView, double durationSeconds)
    {
        var pairs = MatchPairs(mauiContext, poppedPage, revealedPage);

        if (pairs.Count == 0)
        {
            return false;
        }

        // The revealed page was just remounted: force a layout pass so end frames exist.
        revealedView.LayoutIfNeeded();
        await WaitForLayoutAsync(pairs.Select(p => p.To).Append(revealedView).ToList());

        if (pairs.Any(p => !IsLaidOut(p.To)))
        {
            return false;
        }

        await RunSessionAsync(
            container,
            pairs,
            movingView: poppedView,
            movingFromOffscreen: false,
            counterpartView: revealedView,
            durationSeconds
        );

        return true;
    }

    private static List<TagPair> MatchPairs(IMauiContext mauiContext, Page fromPage, Page toPage)
    {
        var from = ScaffoldTransitions.Collect(fromPage);
        var to = ScaffoldTransitions.Collect(toPage);
        var pairs = new List<TagPair>();

        foreach (var (name, fromView) in from)
        {
            if (to.TryGetValue(name, out var toView))
            {
                var fromPlatform = fromView.ToPlatform(mauiContext);
                var toPlatform = toView.ToPlatform(mauiContext);

                pairs.Add(new TagPair(
                    name,
                    fromPlatform,
                    toPlatform,
                    CornerRadiusOf(fromView, fromPlatform),
                    CornerRadiusOf(toView, toPlatform),
                    IsImagePair: fromPlatform is UIImageView { Image: not null } && toPlatform is UIImageView));
            }
        }

        // Overlay stacking: images at the bottom, then larger elements (scrims) below smaller
        // ones (labels) — everything that sits ON a photo in the live layouts keeps the same
        // stacking order mid-flight. Collect order comes from a dictionary, so sort explicitly.
        return pairs
            .OrderBy(p => p.IsImagePair ? 0 : 1)
            .ThenByDescending(p => (double)(p.From.Bounds.Width * p.From.Bounds.Height))
            .ToList();
    }

    private static bool IsLaidOut(UIView view)
        => view.Window is not null && view.Bounds.Width >= 1 && view.Bounds.Height >= 1;

    private static async Task WaitForLayoutAsync(IReadOnlyCollection<UIView> views)
    {
        var stopwatch = Stopwatch.StartNew();

        while (stopwatch.ElapsedMilliseconds < _layoutWaitTimeoutMs && views.Any(v => !IsLaidOut(v)))
        {
            await Task.Delay(16);
        }
    }

    /// <summary>
    /// Begins a SCRUBBABLE pop session for the interactive edge swipe: the same choreography a
    /// programmatic pop plays (page slide + shared-element flights when pairs match and the
    /// revealed views are already laid out), interpolated MANUALLY per fraction.
    /// A paused <see cref="UIViewPropertyAnimator"/> is deliberately NOT used: measured on
    /// iOS 26 (simulator and device), a paused animator accepts <c>FractionComplete</c> (state
    /// Active, read-back correct) but never renders the interpolation — started animators work,
    /// paused-scrub mode does not. Manual value application has no such dependency.
    /// The caller drives it with <see cref="ScaffoldPopAnimationSession.SetProgress"/> and
    /// settles it with Finish/Cancel (plain UIView animations to the end/start values).
    /// Never returns null: with no usable pairs the session is just the page slide.
    /// </summary>
    public static ScaffoldPopAnimationSession BeginInteractivePopSession(
        UIView container,
        IMauiContext mauiContext,
        Page poppedPage,
        Page revealedPage,
        UIView poppedView,
        UIView revealedView,
        ScaffoldPageTransition transition)
    {
        var pairs = MatchPairs(mauiContext, poppedPage, revealedPage);

        // Synchronous readiness gate only: the revealed page was just peek-mounted, one layout
        // pass makes its geometry real. Anything still unlaid drops the flights (page motion
        // still scrubs) — a gesture cannot await a polling gate.
        revealedView.LayoutIfNeeded();

        if (pairs.Any(p => !IsLaidOut(p.To)))
        {
            pairs = [];
        }

        // The flight geometry assumes the standard slide: shared-element pops always play it.
        if (pairs.Count > 0)
        {
            transition = ScaffoldPageTransition.Default;
        }

        var overlay = new UIView(container.Bounds) { UserInteractionEnabled = false };
        var identity = new ScaffoldTransitionMotion();

        var elements = new List<IScrubElement>
        {
            // The popped page replays its enter motion in reverse; the revealed page returns
            // from the behind state (§8.2 — the same spec the programmatic pop interprets).
            new MotionScrubElement(poppedView, identity, transition.Enter, container.Bounds),
            new MotionScrubElement(revealedView, transition.Behind, identity, container.Bounds)
        };

        var cleanup = new List<Action>
        {
            () => overlay.RemoveFromSuperview(),
            () =>
            {
                revealedView.Alpha = 1;
                revealedView.Transform = CGAffineTransform.MakeIdentity();
            }
        };

        foreach (var pair in pairs)
        {
            var geometry = MeasurePair(pair, container);

            if (pair.IsImagePair)
            {
                var flight = BuildImageFlight(overlay, pair, geometry);

                elements.Add(new ImageMorphElement(
                    flight.Flight,
                    flight.FlyingImage,
                    geometry.FromVisible,
                    geometry.ToVisible,
                    pair.FromRadius,
                    pair.ToRadius,
                    flight.FromAspect,
                    flight.ToAspect
                ));
            }
            else
            {
                var flight = BuildSnapshotFlight(overlay, pair, geometry);

                elements.Add(new SnapshotMatchElement(
                    flight,
                    geometry.FromVisible,
                    geometry.ToVisible,
                    pair.FromRadius,
                    pair.ToRadius
                ));
            }

            // AFTER the copies are rendered: hide the live pair views for the flight's duration.
            // Restore the ORIGINAL alphas — a pair view's platform alpha is its MAUI Opacity
            // (a 0.32 scrim forced back to 1 turns opaque), so 1 must never be assumed.
            var fromView = pair.From;
            var toView = pair.To;
            var fromAlpha = fromView.Alpha;
            var toAlpha = toView.Alpha;
            fromView.Alpha = 0;
            toView.Alpha = 0;

            cleanup.Add(() =>
            {
                fromView.Alpha = fromAlpha;
                toView.Alpha = toAlpha;
            });
        }

        container.AddSubview(overlay);

        return new ScaffoldPopAnimationSession(elements, cleanup, transition.DurationSeconds > 0 ? transition.DurationSeconds : 0.25);
    }

    /// <summary>The per-fraction scrub surface of one animated element of the pop choreography.</summary>
    internal interface IScrubElement
    {
        /// <summary>Applies the visual state for <paramref name="progress"/> in [0, 1].</summary>
        void Apply(double progress);
    }

    private sealed class MotionScrubElement(UIView view, ScaffoldTransitionMotion start, ScaffoldTransitionMotion end, CGRect bounds) : IScrubElement
    {
        public void Apply(double progress)
        {
            var scale = Lerp(start.Scale, end.Scale, progress);
            var transform = CGAffineTransform.MakeScale((nfloat)scale, (nfloat)scale);
            transform.Tx = (nfloat)(Lerp(start.FractionX, end.FractionX, progress) * bounds.Width);
            transform.Ty = (nfloat)(Lerp(start.FractionY, end.FractionY, progress) * bounds.Height);
            view.Transform = transform;
            view.Alpha = (nfloat)Lerp(start.Opacity, end.Opacity, progress);
        }
    }

    private sealed class ImageMorphElement(
        UIView flight,
        UIImageView flyingImage,
        CGRect fromFrame,
        CGRect toFrame,
        nfloat fromRadius,
        nfloat toRadius,
        CGRect fromAspect,
        CGRect toAspect) : IScrubElement
    {
        public void Apply(double progress)
        {
            flight.Frame = Lerp(fromFrame, toFrame, progress);
            flight.Layer.CornerRadius = (nfloat)Lerp((double)fromRadius, (double)toRadius, progress);
            flyingImage.Frame = Lerp(fromAspect, toAspect, progress);
        }
    }

    private sealed class SnapshotMatchElement(
        SnapshotFlight flight,
        CGRect fromFrame,
        CGRect toFrame,
        nfloat fromRadius,
        nfloat toRadius) : IScrubElement
    {
        public void Apply(double progress)
        {
            flight.Flight.Frame = Lerp(fromFrame, toFrame, progress);
            flight.Flight.Layer.CornerRadius = (nfloat)Lerp((double)fromRadius, (double)toRadius, progress);
            var inner = Lerp(flight.FromInner, flight.ToInner, progress);
            flight.FromSnapshot.Frame = inner;
            flight.ToSnapshot.Frame = inner;
            flight.FromSnapshot.Alpha = (nfloat)(flight.FromAlpha * (1 - progress));
            flight.ToSnapshot.Alpha = (nfloat)(flight.ToAlpha * progress);
        }
    }

    private static double Lerp(double from, double to, double progress) => from + ((to - from) * progress);

    private static CGRect Lerp(CGRect from, CGRect to, double progress)
        => new(
            (nfloat)Lerp((double)from.X, (double)to.X, progress),
            (nfloat)Lerp((double)from.Y, (double)to.Y, progress),
            (nfloat)Lerp((double)from.Width, (double)to.Width, progress),
            (nfloat)Lerp((double)from.Height, (double)to.Height, progress));

    /// <summary>
    /// One transition session: an overlay carrying the flights + the page slide, all inside a
    /// single UIViewPropertyAnimator (seekable by construction — the interactive-pop hook).
    /// </summary>
    private static async Task RunSessionAsync(UIView container, List<TagPair> pairs, UIView movingView, bool movingFromOffscreen, UIView counterpartView, double durationSeconds)
    {
        _ = counterpartView; // stays put — matches the plain slide choreography

        var (animator, completion) = BuildSession(container, pairs, movingView, movingFromOffscreen, durationSeconds);

        // The moving page was (re)mounted this very runloop tick: its first layout+render commit
        // is expensive and would otherwise land AFTER the animator's clock starts, eating the
        // first frames of the flight (visible start jump). Pay the commit now, then start.
        CATransaction.Flush();

        animator.StartAnimation();
        await completion;
    }

    private static (UIViewPropertyAnimator Animator, Task Completion) BuildSession(UIView container, List<TagPair> pairs, UIView movingView, bool movingFromOffscreen, double durationSeconds)
    {
        var overlay = new UIView(container.Bounds) { UserInteractionEnabled = false };
        var width = container.Bounds.Width;

        var prep = new List<Action>();
        var animations = new List<Action>();
        var cleanup = new List<Action> { () => overlay.RemoveFromSuperview() };

        // Flight geometry is computed in un-translated space: the moving page's slide transform
        // must not leak into the measured frames.
        var savedTransform = movingView.Transform;
        movingView.Transform = CGAffineTransform.MakeIdentity();

        foreach (var pair in pairs)
        {
            var geometry = MeasurePair(pair, container);

            if (pair.IsImagePair)
            {
                var flight = BuildImageFlight(overlay, pair, geometry);

                animations.Add(() =>
                {
                    flight.Flight.Frame = geometry.ToVisible;
                    flight.Flight.Layer.CornerRadius = pair.ToRadius;
                    flight.FlyingImage.Frame = flight.ToAspect;
                });
            }
            else
            {
                var flight = BuildSnapshotFlight(overlay, pair, geometry);

                animations.Add(() =>
                {
                    flight.Flight.Frame = geometry.ToVisible;
                    flight.Flight.Layer.CornerRadius = pair.ToRadius;
                    flight.FromSnapshot.Frame = flight.ToInner;
                    flight.FromSnapshot.Alpha = 0;
                    flight.ToSnapshot.Frame = flight.ToInner;
                    flight.ToSnapshot.Alpha = flight.ToAlpha;
                });
            }

            // The copies are rendered at build time (above): hiding the live views can safely
            // happen in prep without racing the captures. Cleanup restores the ORIGINAL alphas —
            // a pair view's platform alpha is its MAUI Opacity (a 0.32 scrim forced back to 1
            // turns opaque), so 1 must never be assumed.
            var fromView = pair.From;
            var toView = pair.To;
            var fromAlpha = fromView.Alpha;
            var toAlpha = toView.Alpha;

            prep.Add(() =>
            {
                fromView.Alpha = 0;
                toView.Alpha = 0;
            });
            cleanup.Add(() =>
            {
                fromView.Alpha = fromAlpha;
                toView.Alpha = toAlpha;
            });
        }

        movingView.Transform = savedTransform;

        // Page motion identical to the presenter's plain slide.
        if (movingFromOffscreen)
        {
            prep.Add(() => movingView.Transform = CGAffineTransform.MakeTranslation(width, 0));
            animations.Add(() => movingView.Transform = CGAffineTransform.MakeIdentity());
        }
        else
        {
            animations.Add(() => movingView.Transform = CGAffineTransform.MakeTranslation(width, 0));
        }

        container.AddSubview(overlay);

        foreach (var action in prep)
        {
            action();
        }

        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var animator = new UIViewPropertyAnimator(durationSeconds, (nfloat)1.0, () =>
        {
            foreach (var action in animations)
            {
                action();
            }
        });

        // Cleanup runs at EITHER end position: the prep/animations pairs are symmetric, so a
        // reversed (cancelled) session restores the exact pre-session state.
        animator.AddCompletion(_ =>
        {
            foreach (var action in cleanup)
            {
                action();
            }

            completion.TrySetResult();
        });

        return (animator, completion.Task);
    }

    private static PairGeometry MeasurePair(TagPair pair, UIView container)
    {
        var fromFull = pair.From.ConvertRectToView(pair.From.Bounds, container);
        var toFull = pair.To.ConvertRectToView(pair.To.Bounds, container);

        return new PairGeometry(
            fromFull,
            ClipToAncestors(pair.From, container, fromFull),
            toFull,
            ClipToAncestors(pair.To, container, toFull));
    }

    /// <summary>
    /// The part of <paramref name="fullFrame"/> the user can actually see: every clipping
    /// ancestor (ScrollView, clipped layouts) shrinks it. Flights must travel between VISIBLE
    /// rects — e.g. the DailyHelper detail photo bleeds 120pt off-screen for its parallax, and
    /// flying the unclipped frame lands the flight where the live view never was (crop shift +
    /// snap the instant the overlay comes off).
    /// </summary>
    private static CGRect ClipToAncestors(UIView view, UIView container, CGRect fullFrame)
    {
        var visible = fullFrame;

        for (var ancestor = view.Superview; ancestor is not null && ancestor != container; ancestor = ancestor.Superview)
        {
            if (ancestor.ClipsToBounds)
            {
                visible = CGRect.Intersect(visible, ancestor.ConvertRectToView(ancestor.Bounds, container));
            }
        }

        return visible.IsEmpty ? fullFrame : visible;
    }

    /// <summary>
    /// Image shared element whose scaling mode may differ between pages: a clipping flight
    /// container carries a raw UIImageView whose frame is computed against the pair's FULL
    /// frames and positioned relative to the visible rect — animating container and image
    /// together morphs the visible crop exactly between what each page shows.
    /// </summary>
    private static ImageFlight BuildImageFlight(UIView overlay, TagPair pair, PairGeometry geometry)
    {
        var fromImage = (UIImageView)pair.From;
        var image = fromImage.Image!;
        var imageSize = image.Size;
        var fromFill = fromImage.ContentMode is UIViewContentMode.ScaleAspectFill;
        var toFill = pair.To is UIImageView { ContentMode: UIViewContentMode.ScaleAspectFill };

        var flight = new UIView(geometry.FromVisible) { ClipsToBounds = true, BackgroundColor = UIColor.Clear };
        flight.Layer.CornerRadius = pair.FromRadius;

        var flyingImage = new UIImageView(image)
        {
            ContentMode = UIViewContentMode.ScaleToFill,
            Frame = InnerRect(AspectRect(geometry.FromFull.Size, imageSize, fill: fromFill), geometry.FromFull, geometry.FromVisible)
        };
        flight.AddSubview(flyingImage);
        overlay.AddSubview(flight);

        return new ImageFlight(
            flight,
            flyingImage,
            flyingImage.Frame,
            InnerRect(AspectRect(geometry.ToFull.Size, imageSize, fill: toFill), geometry.ToFull, geometry.ToVisible));
    }

    /// <summary>
    /// Generic shared element (labels, scrims, boxes): two pre-rendered stretchable copies
    /// cross-fade inside a clipping flight container while it travels the visible-rect path.
    /// </summary>
    private static SnapshotFlight BuildSnapshotFlight(UIView overlay, TagPair pair, PairGeometry geometry)
    {
        var (fromSnapshot, fromAlpha) = RenderedCopy(pair.From, afterScreenUpdates: false);
        var (toSnapshot, toAlpha) = RenderedCopy(pair.To, afterScreenUpdates: true);

        var flight = new UIView(geometry.FromVisible) { ClipsToBounds = true, BackgroundColor = UIColor.Clear };
        flight.Layer.CornerRadius = pair.FromRadius;

        var fromInner = InnerRect(new CGRect(CGPoint.Empty, geometry.FromFull.Size), geometry.FromFull, geometry.FromVisible);
        var toInner = InnerRect(new CGRect(CGPoint.Empty, geometry.ToFull.Size), geometry.ToFull, geometry.ToVisible);

        fromSnapshot.Frame = fromInner;
        fromSnapshot.Alpha = fromAlpha;
        toSnapshot.Frame = fromInner;
        toSnapshot.Alpha = 0;
        flight.AddSubview(toSnapshot);
        flight.AddSubview(fromSnapshot);
        overlay.AddSubview(flight);

        return new SnapshotFlight(flight, fromSnapshot, toSnapshot, fromInner, toInner, fromAlpha, toAlpha);
    }

    /// <summary>Positions a rect computed in FULL-frame space inside the flight container (whose origin is the VISIBLE rect).</summary>
    private static CGRect InnerRect(CGRect rect, CGRect full, CGRect visible)
        => new(rect.X + full.X - visible.X, rect.Y + full.Y - visible.Y, rect.Width, rect.Height);

    /// <summary>
    /// A stretchable rendered copy of a live view (content at alpha 1 + the view's own alpha
    /// returned separately so cross-fades can multiply it in). SnapshotView is deliberately NOT
    /// used: its afterScreenUpdates capture happens at the next commit — by then the live pair
    /// views are already hidden for the flight and the capture comes back blank. Rendering into
    /// an image NOW is deterministic, and a plain UIImageView stretches smoothly when its frame
    /// animates between the two pair sizes.
    /// </summary>
    private static (UIView View, nfloat Alpha) RenderedCopy(UIView view, bool afterScreenUpdates)
    {
        var size = view.Bounds.Size;

        if (size.Width < 1 || size.Height < 1)
        {
            return (new UIView(), 1);
        }

        var alpha = view.Alpha;
        view.Alpha = 1;
        var renderer = new UIGraphicsImageRenderer(size);
        var image = renderer.CreateImage(_ => view.DrawViewHierarchy(new CGRect(CGPoint.Empty, size), afterScreenUpdates));
        view.Alpha = alpha;

        return (new UIImageView(image) { ContentMode = UIViewContentMode.ScaleToFill }, alpha);
    }

    /// <summary>
    /// Corner radius of the pair view: its own layer, its clipping platform wrapper, or —
    /// because MAUI Border clips through a mask layer invisible to <c>Layer.CornerRadius</c> —
    /// the nearest MAUI Border ancestor, counted only when the element actually spans the
    /// Border content (a small label inside a rounded card is not itself round).
    /// </summary>
    private static nfloat CornerRadiusOf(View element, UIView platformView)
    {
        if (platformView.Layer.CornerRadius > 0)
        {
            return platformView.Layer.CornerRadius;
        }

        var wrapper = platformView.Superview;

        if (wrapper is not null && wrapper.ClipsToBounds && wrapper.Layer.CornerRadius > 0)
        {
            return wrapper.Layer.CornerRadius;
        }

        for (var parent = element.Parent; parent is View; parent = parent.Parent)
        {
            if (parent is Border border)
            {
                if (border.StrokeShape is not RoundRectangle rounded || border.Handler?.PlatformView is not UIView borderView)
                {
                    return 0;
                }

                var frame = platformView.ConvertRectToView(platformView.Bounds, borderView);
                var bounds = borderView.Bounds;
                var spans = Math.Abs(frame.X - bounds.X) < 2
                    && Math.Abs(frame.Y - bounds.Y) < 2
                    && Math.Abs(frame.Right - bounds.Right) < 2
                    && Math.Abs(frame.Bottom - bounds.Bottom) < 2;
                var corner = rounded.CornerRadius;

                return spans
                    ? (nfloat)Math.Max(Math.Max(corner.TopLeft, corner.TopRight), Math.Max(corner.BottomLeft, corner.BottomRight))
                    : 0;
            }
        }

        return 0;
    }

    private static CGRect AspectRect(CGSize container, CGSize image, bool fill)
    {
        var scaleX = container.Width / Math.Max((double)image.Width, 1);
        var scaleY = container.Height / Math.Max((double)image.Height, 1);
        var scale = fill ? Math.Max(scaleX, scaleY) : Math.Min(scaleX, scaleY);
        var width = image.Width * scale;
        var height = image.Height * scale;

        return new CGRect((container.Width - width) / 2, (container.Height - height) / 2, width, height);
    }
}

/// <summary>
/// A scrubbable pop session (page slide + shared-element flights) driven by MANUAL per-fraction
/// interpolation: the finger drives <see cref="SetProgress"/>; release settles with
/// <see cref="FinishAsync"/> (a UIView animation to the popped end state) or
/// <see cref="CancelAsync"/> (back to the resting state, then cleanup restores the exact
/// pre-session view state).
/// </summary>
internal sealed class ScaffoldPopAnimationSession(
    IReadOnlyList<ScaffoldSharedElementTransitions.IScrubElement> elements,
    IReadOnlyList<Action> cleanup,
    double durationSeconds)
{
    private double _progress;
    private bool _settled;

    /// <summary>Scrubs the session; safe to call only before Finish/Cancel.</summary>
    public void SetProgress(double progress)
    {
        if (_settled)
        {
            return;
        }

        _progress = Math.Clamp(progress, 0d, 1d);

        foreach (var element in elements)
        {
            element.Apply(_progress);
        }
    }

    /// <summary>Animates forward to the popped end state.</summary>
    public Task FinishAsync() => SettleAsync(1);

    /// <summary>Animates back to the resting state and restores the pre-session view state.</summary>
    public Task CancelAsync() => SettleAsync(0);

    private async Task SettleAsync(double target)
    {
        if (_settled)
        {
            return;
        }

        _settled = true;

        var remaining = Math.Abs(target - _progress);

        if (remaining > 0.001)
        {
            await UIView.AnimateAsync(durationSeconds * remaining, () =>
            {
                foreach (var element in elements)
                {
                    element.Apply(target);
                }
            });
        }

        foreach (var action in cleanup)
        {
            action();
        }
    }
}
