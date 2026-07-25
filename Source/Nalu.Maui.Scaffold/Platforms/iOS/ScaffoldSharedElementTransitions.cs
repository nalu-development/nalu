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
    /// One transition session: an overlay carrying the flights + the page slide, all inside a
    /// single UIViewPropertyAnimator (seekable by construction — the interactive-pop hook).
    /// </summary>
    private static async Task RunSessionAsync(UIView container, List<TagPair> pairs, UIView movingView, bool movingFromOffscreen, UIView counterpartView, double durationSeconds)
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
                BuildTransformMatch(pair.From, pair.To, fromFrame, toFrame, prep, animations, cleanup);
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

        _ = counterpartView; // stays put — matches the plain slide choreography

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

        animator.AddCompletion(_ =>
        {
            foreach (var action in cleanup)
            {
                action();
            }

            completion.TrySetResult();
        });

        animator.StartAnimation();
        await completion.Task;
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
        List<Action> prep,
        List<Action> animations,
        List<Action> cleanup)
    {
        prep.Add(() =>
        {
            toView.Alpha = 0;
            toView.Transform = MatchTransform(toFrame, fromFrame);
        });
        animations.Add(() =>
        {
            toView.Alpha = 1;
            toView.Transform = CGAffineTransform.MakeIdentity();
            fromView.Alpha = 0;
            fromView.Transform = MatchTransform(fromFrame, toFrame);
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
