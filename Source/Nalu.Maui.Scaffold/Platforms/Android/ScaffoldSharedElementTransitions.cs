using Android.Animation;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.Views;
using Android.Views.Animations;
using Android.Widget;
using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Platform;
using AView = Android.Views.View;
using AViewGroup = Android.Views.ViewGroup;
using RectF = Android.Graphics.RectF;
using View = Microsoft.Maui.Controls.View;

namespace Nalu;

/// <summary>
/// Android shared-element engine: OUR overlay flights instead of the androidx TransitionSet.
/// The native framework cannot animate corner radii (rounded cards snapped square), cannot
/// scale text (label pairs teleported), cannot cross-fade pairs and gives no control over
/// flight stacking — and managed Transition subclasses lose their peer on the framework's
/// clone, so it cannot be extended either. This engine mirrors the iOS one instead: geometry
/// is computed once (visible-rect flights, aspect-crop morph for image pairs, rendered-copy
/// cross-fades for everything else, images below scrims below labels), and a single
/// ValueAnimator applies it per frame to views hosted in the fragment container's
/// ViewGroupOverlay — above both pages and untouchable by MAUI layout.
/// The page motion stays on the fragment animators (the plain slide), so transitions with and
/// without shared elements move identically.
/// </summary>
internal static class ScaffoldSharedElementTransitions
{
    /// <summary>
    /// Captures the SOURCE side of every matched pair (geometry, rendered copies, radii) while
    /// the outgoing page is still at rest — the presenter calls this synchronously before the
    /// fragment transaction commits. Returns null when nothing usable matched.
    /// </summary>
    public static ScaffoldFlightSession? Prepare(
        IMauiContext mauiContext,
        AViewGroup container,
        Page fromPage,
        Page toPage,
        IReadOnlyList<string> names,
        Dictionary<string, View> fromTagged,
        double durationSeconds)
    {
        if (fromPage.Handler?.PlatformView is not AView fromRoot)
        {
            return null;
        }

        var preps = new List<PairPrep>();

        foreach (var name in names)
        {
            if (!fromTagged.TryGetValue(name, out var fromElement)
                || fromElement.Handler?.PlatformView is not AView fromPlatform
                || !fromPlatform.IsAttachedToWindow
                || fromPlatform.Width < 1
                || fromPlatform.Height < 1)
            {
                continue;
            }

            // Frames are measured relative to the PAGE ROOT (not the window): coordinates
            // inside the page are immune to any transform the page root itself carries, and
            // the page root fills the container at rest, so page space IS container space.
            var fromFull = FrameIn(fromPlatform, fromRoot);
            var fromVisible = ClipToAncestors(fromPlatform, fromRoot, fromFull);

            // The drawable is captured NOW — MAUI's async image pipeline may swap or clear it
            // between this capture and the flight start one frame later.
            var imageDrawable = fromElement is Microsoft.Maui.Controls.Image && fromPlatform is ImageView imageView
                ? imageView.Drawable
                : null;

            preps.Add(new PairPrep(
                name,
                fromElement,
                fromPlatform,
                fromFull,
                fromVisible,
                CornerRadiusOf(fromElement, fromPlatform),
                fromPlatform.Alpha,
                imageDrawable,
                imageDrawable is null ? Render(fromPlatform) : null));
        }

        return preps.Count > 0
            ? new ScaffoldFlightSession(mauiContext, container, toPage, preps, durationSeconds)
            : null;
    }

    internal sealed record PairPrep(
        string Name,
        View FromElement,
        AView FromPlatform,
        RectF FromFull,
        RectF FromVisible,
        float FromRadius,
        float FromAlpha,
        Drawable? ImageDrawable,
        Bitmap? FromBitmap)
    {
        public bool IsImagePair => ImageDrawable is not null;
    }

    /// <summary>The element's frame in <paramref name="root"/> coordinates.</summary>
    internal static RectF FrameIn(AView view, AView root)
    {
        var viewLocation = new int[2];
        var rootLocation = new int[2];
        view.GetLocationInWindow(viewLocation);
        root.GetLocationInWindow(rootLocation);
        var x = viewLocation[0] - rootLocation[0];
        var y = viewLocation[1] - rootLocation[1];

        return new RectF(x, y, x + view.Width, y + view.Height);
    }

    /// <summary>
    /// The part of <paramref name="fullFrame"/> the user can actually see: every clipping
    /// ancestor (ScrollView, clipped layouts) shrinks it. Flights must travel between VISIBLE
    /// rects — e.g. the DailyHelper detail photo bleeds 120dp off-screen for its parallax, and
    /// flying the unclipped frame lands the flight where the live view never was.
    /// </summary>
    internal static RectF ClipToAncestors(AView view, AView root, RectF fullFrame)
    {
        var visible = new RectF(fullFrame);

        for (var ancestor = view.Parent as AView; ancestor is not null && !ReferenceEquals(ancestor, root); ancestor = ancestor.Parent as AView)
        {
            if (ancestor is AViewGroup { ClipChildren: true } or AViewGroup { ClipToOutline: true })
            {
                var bounds = FrameIn(ancestor, root);

                if (!visible.Intersect(bounds))
                {
                    return fullFrame;
                }
            }
        }

        return visible.IsEmpty ? fullFrame : visible;
    }

    /// <summary>
    /// Corner radius of the pair element, read from the MAUI tree: the nearest Border ancestor
    /// with a RoundRectangle stroke shape, counted only when the element actually spans the
    /// Border content (a small label inside a rounded card is not itself round). Android's
    /// Border clips with a path — nothing readable on the platform view.
    /// </summary>
    internal static float CornerRadiusOf(View element, AView platformView)
    {
        for (var parent = element.Parent; parent is View maybeParent; parent = maybeParent.Parent)
        {
            if (maybeParent is Border border)
            {
                if (border.StrokeShape is not RoundRectangle rounded || border.Handler?.PlatformView is not AView borderView)
                {
                    return 0;
                }

                var frame = FrameIn(platformView, borderView);
                var tolerance = platformView.Context.ToPixels(2);
                var spans = Math.Abs(frame.Left) < tolerance
                    && Math.Abs(frame.Top) < tolerance
                    && Math.Abs(frame.Right - borderView.Width) < tolerance
                    && Math.Abs(frame.Bottom - borderView.Height) < tolerance;

                if (!spans)
                {
                    return 0;
                }

                var corner = rounded.CornerRadius;
                var radius = Math.Max(Math.Max(corner.TopLeft, corner.TopRight), Math.Max(corner.BottomLeft, corner.BottomRight));

                return platformView.Context.ToPixels(radius);
            }
        }

        return 0;
    }

    /// <summary>
    /// A rendered copy of a live view. <c>View.Draw</c> paints the subtree at full alpha (the
    /// view's own alpha is applied by its parent), which is exactly right: cross-fades multiply
    /// the captured base alpha back in, so a 0.32 scrim stays a 0.32 scrim mid-flight.
    /// </summary>
    internal static Bitmap? Render(AView view)
    {
        if (view.Width < 1 || view.Height < 1)
        {
            return null;
        }

        var bitmap = Bitmap.CreateBitmap(view.Width, view.Height, Bitmap.Config.Argb8888!);
        var canvas = new Canvas(bitmap);
        view.Draw(canvas);

        return bitmap;
    }

    internal static float Lerp(float from, float to, float progress) => from + ((to - from) * progress);

    internal static RectF Lerp(RectF from, RectF to, float progress)
        => new(
            Lerp(from.Left, to.Left, progress),
            Lerp(from.Top, to.Top, progress),
            Lerp(from.Right, to.Right, progress),
            Lerp(from.Bottom, to.Bottom, progress));

    /// <summary>Aspect-fit/fill rect of an image inside a container, in container-local coordinates.</summary>
    internal static RectF AspectRect(float containerWidth, float containerHeight, float imageWidth, float imageHeight, bool fill)
    {
        var scaleX = containerWidth / Math.Max(imageWidth, 1);
        var scaleY = containerHeight / Math.Max(imageHeight, 1);
        var scale = fill ? Math.Max(scaleX, scaleY) : Math.Min(scaleX, scaleY);
        var width = imageWidth * scale;
        var height = imageHeight * scale;
        var x = (containerWidth - width) / 2;
        var y = (containerHeight - height) / 2;

        return new RectF(x, y, x + width, y + height);
    }

    /// <summary>Positions a rect computed in FULL-frame space inside the flight container (whose origin is the VISIBLE rect).</summary>
    internal static RectF InnerRect(RectF rect, RectF full, RectF visible)
    {
        var dx = full.Left - visible.Left;
        var dy = full.Top - visible.Top;

        return new RectF(rect.Left + dx, rect.Top + dy, rect.Right + dx, rect.Bottom + dy);
    }

    /// <summary>Flight container: clips (rounded) and lays its children out manually per frame.</summary>
    internal sealed class FlightLayout(Android.Content.Context context) : AViewGroup(context)
    {
        public RoundedOutlineProvider Rounding { get; } = new();

        protected override void OnLayout(bool changed, int l, int t, int r, int b)
        {
            // Children are laid out manually by the flight's Apply — nothing to do here.
        }
    }

    internal sealed class RoundedOutlineProvider : ViewOutlineProvider
    {
        public float Radius { get; set; }

        public override void GetOutline(AView? view, Outline? outline)
        {
            if (view is not null)
            {
                outline?.SetRoundRect(0, 0, view.Width, view.Height, Radius);
            }
        }
    }
}

/// <summary>
/// One shared-element flight session: source side captured at prepare time (page at rest),
/// destination side measured and the whole choreography started at the incoming page's first
/// pre-draw — the same frame that page becomes visible, so the live-view handoff is seamless.
/// </summary>
internal sealed class ScaffoldFlightSession(
    IMauiContext mauiContext,
    AViewGroup container,
    Page toPage,
    List<ScaffoldSharedElementTransitions.PairPrep> preps,
    double durationSeconds)
{
    private interface IFlight
    {
        void Apply(float progress);
    }

    private bool _started;

    /// <summary>Measures the destination side, builds the overlay flights and starts the animator.</summary>
    public void Start()
    {
        try
        {
            StartCore();
        }
        catch (Exception exception)
        {
            // A failed flight must never take the navigation down with it — the page slide
            // still runs and the live views were not hidden yet (that happens last).
            System.Diagnostics.Debug.WriteLine($"[Nalu] Shared-element flight failed: {exception}");
        }
    }

    private void StartCore()
    {
        if (_started || container.Overlay is not ViewGroupOverlay overlay || toPage.Handler?.PlatformView is not AView toRoot)
        {
            return;
        }

        _started = true;

        var toTagged = ScaffoldTransitions.Collect(toPage);
        var flights = new List<IFlight>();
        var flightViews = new List<AView>();
        var cleanup = new List<Action>();
        var hide = new List<Action>();

        // Overlay stacking: images at the bottom, then larger elements (scrims) below smaller
        // ones (labels) — everything that sits ON a photo in the live layouts keeps the same
        // stacking order mid-flight.
        var ordered = preps
            .OrderBy(p => p.IsImagePair ? 0 : 1)
            .ThenByDescending(p => p.FromVisible.Width() * p.FromVisible.Height());

        foreach (var prep in ordered)
        {
            if (!toTagged.TryGetValue(prep.Name, out var toElement)
                || toElement.ToPlatform(mauiContext) is not { Width: >= 1, Height: >= 1 } toPlatform)
            {
                continue;
            }

            var toFull = ScaffoldSharedElementTransitions.FrameIn(toPlatform, toRoot);
            var toVisible = ScaffoldSharedElementTransitions.ClipToAncestors(toPlatform, toRoot, toFull);
            var toRadius = ScaffoldSharedElementTransitions.CornerRadiusOf(toElement, toPlatform);

            var flight = prep.IsImagePair
                ? BuildImageFlight(prep, toElement, toFull, toVisible, toRadius, flightViews)
                : BuildSnapshotFlight(prep, toPlatform, toFull, toVisible, toRadius, flightViews);

            if (flight is null)
            {
                continue;
            }

            flights.Add(flight);

            // Hide the live pair views for the flight's duration; restore their ORIGINAL
            // alphas (a pair view's platform alpha is its MAUI Opacity — never assume 1).
            // The hide runs AFTER every build succeeded — a failed build aborts the whole
            // session (see Start) with the pages still fully visible.
            var fromPlatform = prep.FromPlatform;
            var fromAlpha = prep.FromAlpha;
            var toAlpha = toPlatform.Alpha;

            cleanup.Add(() =>
            {
                fromPlatform.Alpha = fromAlpha;
                toPlatform.Alpha = toAlpha;
            });
            hide.Add(() =>
            {
                fromPlatform.Alpha = 0;
                toPlatform.Alpha = 0;
            });
        }

        if (flights.Count == 0)
        {
            return;
        }

        foreach (var action in hide)
        {
            action();
        }

        foreach (var view in flightViews)
        {
            overlay.Add(view);
        }

        foreach (var flight in flights)
        {
            flight.Apply(0);
        }

        var animator = ValueAnimator.OfFloat(0f, 1f)!;
        animator.SetDuration((long)(durationSeconds * 1000));
        animator.SetInterpolator(new AccelerateDecelerateInterpolator());
        animator.Update += (_, args) =>
        {
            var progress = (float)args.Animation.AnimatedValue!;

            foreach (var flight in flights)
            {
                flight.Apply(progress);
            }
        };
        animator.AnimationEnd += (_, _) =>
        {
            foreach (var view in flightViews)
            {
                overlay.Remove(view);
            }

            foreach (var action in cleanup)
            {
                action();
            }
        };
        animator.Start();
    }

    /// <summary>
    /// Image pair: a clipping flight container carries a raw FitXY ImageView whose frame is
    /// computed against the pair's FULL frames and positioned relative to the visible rect —
    /// animating container and image together morphs the visible crop exactly between what
    /// each page shows.
    /// </summary>
    private IFlight? BuildImageFlight(
        ScaffoldSharedElementTransitions.PairPrep prep,
        View toElement,
        RectF toFull,
        RectF toVisible,
        float toRadius,
        List<AView> flightViews)
    {
        var drawable = prep.ImageDrawable!;
        var imageWidth = (float)Math.Max(drawable.IntrinsicWidth, 1);
        var imageHeight = (float)Math.Max(drawable.IntrinsicHeight, 1);
        var fromFill = (prep.FromElement as Microsoft.Maui.Controls.Image)?.Aspect != Aspect.AspectFit;
        var toFill = (toElement as Microsoft.Maui.Controls.Image)?.Aspect != Aspect.AspectFit;

        var flight = new ScaffoldSharedElementTransitions.FlightLayout(container.Context!)
        {
            ClipToOutline = true
        };
        flight.OutlineProvider = flight.Rounding;

        var flyingImage = new ImageView(container.Context);
        flyingImage.SetScaleType(ImageView.ScaleType.FitXy!);
        flyingImage.SetImageDrawable(drawable.GetConstantState()?.NewDrawable(container.Resources) ?? drawable);
        flight.AddView(flyingImage);
        flightViews.Add(flight);

        var fromAspect = ScaffoldSharedElementTransitions.InnerRect(
            ScaffoldSharedElementTransitions.AspectRect(prep.FromFull.Width(), prep.FromFull.Height(), imageWidth, imageHeight, fromFill),
            prep.FromFull,
            prep.FromVisible);
        var toAspect = ScaffoldSharedElementTransitions.InnerRect(
            ScaffoldSharedElementTransitions.AspectRect(toFull.Width(), toFull.Height(), imageWidth, imageHeight, toFill),
            toFull,
            toVisible);

        return new ImageMorphFlight(flight, flyingImage, prep.FromVisible, toVisible, prep.FromRadius, toRadius, fromAspect, toAspect);
    }

    /// <summary>
    /// Generic pair (labels, scrims, boxes): two rendered stretchable copies cross-fade inside
    /// a clipping flight container while it travels the visible-rect path.
    /// </summary>
    private IFlight? BuildSnapshotFlight(
        ScaffoldSharedElementTransitions.PairPrep prep,
        AView toPlatform,
        RectF toFull,
        RectF toVisible,
        float toRadius,
        List<AView> flightViews)
    {
        var toBitmap = ScaffoldSharedElementTransitions.Render(toPlatform);

        if (prep.FromBitmap is null && toBitmap is null)
        {
            return null;
        }

        var flight = new ScaffoldSharedElementTransitions.FlightLayout(container.Context!)
        {
            ClipToOutline = true
        };
        flight.OutlineProvider = flight.Rounding;

        var fromCopy = new ImageView(container.Context);
        fromCopy.SetScaleType(ImageView.ScaleType.FitXy!);

        if (prep.FromBitmap is not null)
        {
            fromCopy.SetImageBitmap(prep.FromBitmap);
        }

        var toCopy = new ImageView(container.Context);
        toCopy.SetScaleType(ImageView.ScaleType.FitXy!);

        if (toBitmap is not null)
        {
            toCopy.SetImageBitmap(toBitmap);
        }

        flight.AddView(toCopy);
        flight.AddView(fromCopy);
        flightViews.Add(flight);

        var fromInner = ScaffoldSharedElementTransitions.InnerRect(
            new RectF(0, 0, prep.FromFull.Width(), prep.FromFull.Height()),
            prep.FromFull,
            prep.FromVisible);
        var toInner = ScaffoldSharedElementTransitions.InnerRect(
            new RectF(0, 0, toFull.Width(), toFull.Height()),
            toFull,
            toVisible);

        return new SnapshotMatchFlight(
            flight,
            fromCopy,
            toCopy,
            prep.FromVisible,
            toVisible,
            prep.FromRadius,
            toRadius,
            fromInner,
            toInner,
            prep.FromAlpha,
            toPlatform.Alpha);
    }

    private static void LayoutRect(AView view, RectF rect)
        => view.Layout(
            (int)Math.Round(rect.Left),
            (int)Math.Round(rect.Top),
            (int)Math.Round(rect.Right),
            (int)Math.Round(rect.Bottom));

    private sealed class ImageMorphFlight(
        ScaffoldSharedElementTransitions.FlightLayout flight,
        ImageView flyingImage,
        RectF fromFrame,
        RectF toFrame,
        float fromRadius,
        float toRadius,
        RectF fromAspect,
        RectF toAspect) : IFlight
    {
        public void Apply(float progress)
        {
            LayoutRect(flight, ScaffoldSharedElementTransitions.Lerp(fromFrame, toFrame, progress));
            flight.Rounding.Radius = ScaffoldSharedElementTransitions.Lerp(fromRadius, toRadius, progress);
            flight.InvalidateOutline();
            LayoutRect(flyingImage, ScaffoldSharedElementTransitions.Lerp(fromAspect, toAspect, progress));
            flight.Invalidate();
        }
    }

    private sealed class SnapshotMatchFlight(
        ScaffoldSharedElementTransitions.FlightLayout flight,
        ImageView fromCopy,
        ImageView toCopy,
        RectF fromFrame,
        RectF toFrame,
        float fromRadius,
        float toRadius,
        RectF fromInner,
        RectF toInner,
        float fromAlpha,
        float toAlpha) : IFlight
    {
        public void Apply(float progress)
        {
            LayoutRect(flight, ScaffoldSharedElementTransitions.Lerp(fromFrame, toFrame, progress));
            flight.Rounding.Radius = ScaffoldSharedElementTransitions.Lerp(fromRadius, toRadius, progress);
            flight.InvalidateOutline();
            var inner = ScaffoldSharedElementTransitions.Lerp(fromInner, toInner, progress);
            LayoutRect(fromCopy, inner);
            LayoutRect(toCopy, inner);
            fromCopy.Alpha = fromAlpha * (1 - progress);
            toCopy.Alpha = toAlpha * progress;
            flight.Invalidate();
        }
    }
}
