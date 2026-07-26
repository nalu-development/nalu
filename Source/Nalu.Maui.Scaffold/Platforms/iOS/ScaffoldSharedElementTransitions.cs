using System.Diagnostics;
using CoreGraphics;
using Microsoft.Maui.Platform;
using UIKit;

namespace Nalu;

/// <summary>
/// iOS shared-element engine (ported from PoC spike A, §8): C# computes the flight geometry
/// once, UIViewPropertyAnimator/Core Animation does all per-frame work natively.
/// Image pairs morph their aspect crop inside a clipping flight container (corner radii read
/// from the live views); any other pair transform-matches with a cross-fade. The page motion
/// matches the presenter's plain slide, so transitions with and without shared elements move
/// identically.
/// </summary>
internal static class ScaffoldSharedElementTransitions
{
    private const double _layoutWaitTimeoutMs = 500;

    private sealed record TagPair(string Name, UIView From, UIView To);

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
                pairs.Add(new TagPair(name, fromView.ToPlatform(mauiContext), toView.ToPlatform(mauiContext)));
            }
        }

        return pairs;
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
        double durationSeconds)
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

        var overlay = new UIView(container.Bounds) { UserInteractionEnabled = false };
        var width = (double)container.Bounds.Width;
        var elements = new List<IScrubElement> { new PageSlideElement(poppedView, width) };
        var cleanup = new List<Action> { () => overlay.RemoveFromSuperview() };

        foreach (var pair in pairs)
        {
            var fromFrame = pair.From.ConvertRectToView(pair.From.Bounds, container);
            var toFrame = pair.To.ConvertRectToView(pair.To.Bounds, container);

            if (pair.From is UIImageView { Image: not null } fromImage && pair.To is UIImageView)
            {
                var image = fromImage.Image!;
                var fromFill = fromImage.ContentMode is UIViewContentMode.ScaleAspectFill;
                var toFill = pair.To is UIImageView { ContentMode: UIViewContentMode.ScaleAspectFill };

                var flight = new UIView(fromFrame) { ClipsToBounds = true, BackgroundColor = UIColor.Clear };
                flight.Layer.CornerRadius = EffectiveCornerRadius(fromImage);

                var flyingImage = new UIImageView(image)
                {
                    ContentMode = UIViewContentMode.ScaleToFill,
                    Frame = AspectRect(fromFrame.Size, image.Size, fill: fromFill)
                };
                flight.AddSubview(flyingImage);
                overlay.AddSubview(flight);

                var fromView = fromImage;
                var toView = pair.To;
                fromView.Alpha = 0;
                toView.Alpha = 0;
                cleanup.Add(() =>
                {
                    fromView.Alpha = 1;
                    toView.Alpha = 1;
                });

                elements.Add(new ImageMorphElement(
                    flight,
                    flyingImage,
                    fromFrame,
                    toFrame,
                    EffectiveCornerRadius(fromImage),
                    EffectiveCornerRadius(pair.To),
                    AspectRect(fromFrame.Size, image.Size, fill: fromFill),
                    AspectRect(toFrame.Size, image.Size, fill: toFill)
                ));
            }
            else
            {
                var fromView = pair.From;
                var toView = pair.To;
                toView.Alpha = 0;
                toView.Transform = MatchTransform(toFrame, fromFrame);

                cleanup.Add(() =>
                {
                    fromView.Alpha = 1;
                    fromView.Transform = CGAffineTransform.MakeIdentity();
                    toView.Alpha = 1;
                    toView.Transform = CGAffineTransform.MakeIdentity();
                });

                elements.Add(new TransformMatchElement(fromView, toView, fromFrame, toFrame, width));
            }
        }

        container.AddSubview(overlay);

        return new ScaffoldPopAnimationSession(elements, cleanup, durationSeconds);
    }

    /// <summary>The per-fraction scrub surface of one animated element of the pop choreography.</summary>
    internal interface IScrubElement
    {
        /// <summary>Applies the visual state for <paramref name="progress"/> in [0, 1].</summary>
        void Apply(double progress);
    }

    private sealed class PageSlideElement(UIView movingView, double width) : IScrubElement
    {
        public void Apply(double progress) => movingView.Transform = CGAffineTransform.MakeTranslation((nfloat)(width * progress), 0);
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

    private sealed class TransformMatchElement(UIView fromView, UIView toView, CGRect fromFrame, CGRect toFrame, double width) : IScrubElement
    {
        public void Apply(double progress)
        {
            // The live destination flies in from the source geometry while the source flies out,
            // cross-fading — the same choreography BuildTransformMatch encodes as end states.
            // The FROM view lives on the popped page, which the page-slide element translates by
            // width * progress: compensate so the pair follows its own path, not the page slide.
            toView.Alpha = (nfloat)progress;
            toView.Transform = LerpTransform(MatchTransform(toFrame, fromFrame), CGAffineTransform.MakeIdentity(), progress);

            var exit = LerpTransform(CGAffineTransform.MakeIdentity(), MatchTransform(fromFrame, toFrame), progress);
            exit.Tx -= (nfloat)(width * progress);
            fromView.Alpha = (nfloat)(1 - progress);
            fromView.Transform = exit;
        }
    }

    private static double Lerp(double from, double to, double progress) => from + ((to - from) * progress);

    private static CGRect Lerp(CGRect from, CGRect to, double progress)
        => new(
            (nfloat)Lerp((double)from.X, (double)to.X, progress),
            (nfloat)Lerp((double)from.Y, (double)to.Y, progress),
            (nfloat)Lerp((double)from.Width, (double)to.Width, progress),
            (nfloat)Lerp((double)from.Height, (double)to.Height, progress));

    /// <summary>Component-wise interpolation — exact for the scale+translate matrices used here.</summary>
    private static CGAffineTransform LerpTransform(CGAffineTransform from, CGAffineTransform to, double progress)
        => new(
            (nfloat)Lerp((double)from.A, (double)to.A, progress),
            (nfloat)Lerp((double)from.B, (double)to.B, progress),
            (nfloat)Lerp((double)from.C, (double)to.C, progress),
            (nfloat)Lerp((double)from.D, (double)to.D, progress),
            (nfloat)Lerp((double)from.Tx, (double)to.Tx, progress),
            (nfloat)Lerp((double)from.Ty, (double)to.Ty, progress));

    /// <summary>
    /// One transition session: an overlay carrying the flights + the page slide, all inside a
    /// single UIViewPropertyAnimator (seekable by construction — the interactive-pop hook).
    /// </summary>
    private static async Task RunSessionAsync(UIView container, List<TagPair> pairs, UIView movingView, bool movingFromOffscreen, UIView counterpartView, double durationSeconds)
    {
        _ = counterpartView; // stays put — matches the plain slide choreography

        var (animator, completion) = BuildSession(container, pairs, movingView, movingFromOffscreen, durationSeconds);
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
            var fromFrame = pair.From.ConvertRectToView(pair.From.Bounds, container);
            var toFrame = pair.To.ConvertRectToView(pair.To.Bounds, container);

            if (pair.From is UIImageView { Image: not null } fromImage && pair.To is UIImageView)
            {
                BuildImageMorph(overlay, fromImage, pair.To, fromFrame, toFrame, prep, animations, cleanup);
            }
            else
            {
                BuildTransformMatch(pair.From, pair.To, fromFrame, toFrame, movingFromOffscreen, width, prep, animations, cleanup);
            }
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

    /// <summary>
    /// Image shared element whose scaling mode may differ between pages: a clipping flight
    /// container carries a raw UIImageView whose frame is computed fill-at-source and
    /// fit-at-destination — animating both frames together morphs the visible crop.
    /// Corner radii are read from the live views (source view or its clipping wrapper).
    /// </summary>
    private static void BuildImageMorph(
        UIView overlay,
        UIImageView fromImage,
        UIView toView,
        CGRect fromFrame,
        CGRect toFrame,
        List<Action> prep,
        List<Action> animations,
        List<Action> cleanup)
    {
        var image = fromImage.Image!;
        var imageSize = image.Size;
        var fromRadius = EffectiveCornerRadius(fromImage);
        var toRadius = EffectiveCornerRadius(toView);
        var fromFill = fromImage.ContentMode is UIViewContentMode.ScaleAspectFill;
        var toFill = toView is UIImageView { ContentMode: UIViewContentMode.ScaleAspectFill };

        var flight = new UIView(fromFrame) { ClipsToBounds = true, BackgroundColor = UIColor.Clear };
        flight.Layer.CornerRadius = fromRadius;

        var flyingImage = new UIImageView(image)
        {
            ContentMode = UIViewContentMode.ScaleToFill,
            Frame = AspectRect(fromFrame.Size, imageSize, fill: fromFill)
        };
        flight.AddSubview(flyingImage);
        overlay.AddSubview(flight);

        prep.Add(() =>
        {
            fromImage.Alpha = 0;
            toView.Alpha = 0;
        });
        animations.Add(() =>
        {
            flight.Frame = toFrame;
            flight.Layer.CornerRadius = toRadius;
            flyingImage.Frame = AspectRect(toFrame.Size, imageSize, fill: toFill);
        });
        cleanup.Add(() =>
        {
            fromImage.Alpha = 1;
            toView.Alpha = 1;
        });
    }

    /// <summary>Corner radius of the view itself or its clipping MAUI wrapper (Border content etc.).</summary>
    private static nfloat EffectiveCornerRadius(UIView view)
    {
        if (view.Layer.CornerRadius > 0)
        {
            return view.Layer.CornerRadius;
        }

        var parent = view.Superview;

        return parent is not null && parent.ClipsToBounds && parent.Layer.CornerRadius > 0
            ? parent.Layer.CornerRadius
            : 0;
    }

    /// <summary>
    /// Generic shared element (labels, boxes): no snapshots — the live destination view flies in
    /// from the source geometry via an affine transform while the source flies out, cross-fading.
    /// </summary>
    private static void BuildTransformMatch(
        UIView fromView,
        UIView toView,
        CGRect fromFrame,
        CGRect toFrame,
        bool movingFromOffscreen,
        double width,
        List<Action> prep,
        List<Action> animations,
        List<Action> cleanup)
    {
        // The pair views animate via transforms ON THE LIVE VIEWS, but one of them lives INSIDE
        // the sliding page: its effective position is pageTranslation + ownTransform. The match
        // frames are measured in un-translated space, so the view on the MOVING page must
        // compensate the page offset wherever the page is not at identity — otherwise the pair
        // rides the page slide (flies in from / out to the side even when its X barely changes).
        // Push (movingFromOffscreen): the TO view's page STARTS at +width — compensate the prep.
        // Pop: the FROM view's page ENDS at +width — compensate the end transform.
        prep.Add(() =>
        {
            var enter = MatchTransform(toFrame, fromFrame);

            if (movingFromOffscreen)
            {
                enter.Tx -= (nfloat)width;
            }

            toView.Alpha = 0;
            toView.Transform = enter;
        });
        animations.Add(() =>
        {
            var exit = MatchTransform(fromFrame, toFrame);

            if (!movingFromOffscreen)
            {
                exit.Tx -= (nfloat)width;
            }

            toView.Alpha = 1;
            toView.Transform = CGAffineTransform.MakeIdentity();
            fromView.Alpha = 0;
            fromView.Transform = exit;
        });
        cleanup.Add(() =>
        {
            fromView.Alpha = 1;
            fromView.Transform = CGAffineTransform.MakeIdentity();
            toView.Alpha = 1;
            toView.Transform = CGAffineTransform.MakeIdentity();
        });
    }

    /// <summary>Transform that makes a view with natural frame <paramref name="from"/> render at <paramref name="to"/>.</summary>
    private static CGAffineTransform MatchTransform(CGRect from, CGRect to)
    {
        var sx = to.Width / Math.Max((double)from.Width, 1);
        var sy = to.Height / Math.Max((double)from.Height, 1);
        var dx = to.GetMidX() - from.GetMidX();
        var dy = to.GetMidY() - from.GetMidY();

        // Scale about the (center) anchor point, then move the center: exact frame mapping.
        return CGAffineTransform.Multiply(
            CGAffineTransform.MakeScale((nfloat)sx, (nfloat)sy),
            CGAffineTransform.MakeTranslation(dx, dy));
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
