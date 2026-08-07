using FluentAssertions;
using Nalu.Maui.UITests.Infrastructure;
using Xunit;

namespace Nalu.Maui.UITests.Tests;

/// <summary>
/// Covers what is ON SCREEN WHILE a page transition plays — the blind spot of the end-state
/// transition suites, and where both platforms have gone wrong: the covered page vanishing the
/// moment a push commits (the window background flashing through beside the incoming page), and
/// the popped page being torn down — or drawn UNDER the page it reveals — instead of sliding
/// away above it.
/// The harness ("Scaffold Motion Tests") stretches the stock slide to <see cref="MotionSeconds"/>
/// over two flat, far-apart colours, so screenshots taken between agent round trips land
/// mid-flight. Each frame is sampled at two window-relative points: LEFT, which the outgoing page
/// owns until the very end of a horizontal slide, and RIGHT, which the incoming one takes first.
/// A frame reading LEFT=root and RIGHT=detail is therefore proof that BOTH pages were on screen
/// together — the property the fast stock transitions cannot be asked about.
/// Both reference colours are MEASURED from settled frames rather than assumed: a screenshot goes
/// through the platform's colour management (iOS renders sRGB #00A000 as #469E2C), so hardcoding
/// the values the harness declares would only ever test the conversion.
/// </summary>
public class ScaffoldMotionChromeTests(NaluApp app) : BaseUiTest(app), IAsyncLifetime
{
    private const string PageName = "Scaffold Motion Tests";

    /// <summary>Mirrors MoDetailPage.TransitionSeconds in the TestApp harness.</summary>
    private const double MotionSeconds = 1.5;

    // Sampled low on the page, where only its own background is ever drawn (the harness keeps
    // every control in the top stack).
    private static readonly (double X, double Y) _left = (0.15, 0.75);
    private static readonly (double X, double Y) _right = (0.85, 0.75);

    private record struct Color(byte R, byte G, byte B)
    {
        public override string ToString() => $"#{R:X2}{G:X2}{B:X2}";
    }

    private record struct Frame(Color Left, Color Right)
    {
        public override string ToString() => $"{Left}/{Right}";
    }

    public async ValueTask InitializeAsync() => await App.OpenTestPageAsync(PageName);

    public async ValueTask DisposeAsync() => await App.ResetAsync();

    // Wide enough for compression and anti-aliasing noise, far narrower than the gap between the
    // harness colours.
    private static bool Is(Color sample, Color expected)
        => Math.Abs(sample.R - expected.R) <= 12 && Math.Abs(sample.G - expected.G) <= 12 && Math.Abs(sample.B - expected.B) <= 12;

    private static string Describe(IEnumerable<Frame> frames) => string.Join(" ", frames);

    private async Task<Frame> SampleFrameAsync()
    {
        var samples = await App.SampleWindowPixelsAsync(_left, _right);

        return new Frame(new Color(samples[0].R, samples[0].G, samples[0].B), new Color(samples[1].R, samples[1].G, samples[1].B));
    }

    /// <summary>Samples both points of every frame it can capture within the given window.</summary>
    private async Task<List<Frame>> CaptureFramesAsync(double seconds)
    {
        var frames = new List<Frame>();
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(seconds);

        while (DateTime.UtcNow < deadline)
        {
            frames.Add(await SampleFrameAsync());
        }

        return frames;
    }

    /// <summary>
    /// Waits until nothing moves — two consecutive frames showing the same single colour at both
    /// sample points — and returns that colour as the reference for the page now displayed.
    /// <paramref name="replacing"/> is the colour of the page being navigated AWAY from: without
    /// it, a transition that has not visually started yet reads as "settled" on the OLD page.
    /// </summary>
    private async Task<Color> MeasureSettledPageAsync(Color? replacing = null)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(MotionSeconds * 4);
        Frame previous = default;
        var hasPrevious = false;

        while (DateTime.UtcNow < deadline)
        {
            var frame = await SampleFrameAsync();

            if (Is(frame.Left, frame.Right)
                && hasPrevious
                && Is(frame.Left, previous.Left)
                && Is(frame.Right, previous.Right)
                && (replacing is not { } outgoing || !Is(frame.Left, outgoing)))
            {
                return frame.Left;
            }

            previous = frame;
            hasPrevious = true;
        }

        throw new TimeoutException(
            $"The window never settled on a page{(replacing is { } c ? $" other than {c}" : string.Empty)} (last frame {previous})."
        );
    }

    /// <summary>
    /// Waits until BOTH sample points read the given colour — "this page is back at rest",
    /// asserted against the expected value rather than inferred from two similar samples: a
    /// fading motion changes slowly enough near its end that consecutive samples can look
    /// identical while the page is still visibly dimmed.
    /// </summary>
    private async Task WaitForPageColorAsync(Color expected, string because)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(MotionSeconds * 4);
        Frame last = default;

        while (DateTime.UtcNow < deadline)
        {
            last = await SampleFrameAsync();

            if (Is(last.Left, expected) && Is(last.Right, expected))
            {
                return;
            }
        }

        throw new TimeoutException($"The window never came to rest on {expected} ({because}); last frame {last}.");
    }

    /// <summary>Every frame of a page transition shows a PAGE — never the window background.</summary>
    private static void AssertNoBackgroundFlash(IReadOnlyList<Frame> frames, Color first, Color second)
        => frames.Should()
                 .OnlyContain(
                     frame => (Is(frame.Left, first) || Is(frame.Left, second))
                              && (Is(frame.Right, first) || Is(frame.Right, second)),
                     $"no frame of a transition may show the window background ({first} and {second} are the pages) — frames: " + Describe(frames)
                 );

    [Fact]
    public async Task PushKeepsTheCoveredPageOnScreen()
    {
        await App.WaitForElementAsync("MoRootPage");
        var root = await MeasureSettledPageAsync();

        await App.TapAsync("PushMoDetail");
        var frames = await CaptureFramesAsync(MotionSeconds * 1.6);

        await App.WaitForElementAsync("MoDetailPage");
        var detail = await MeasureSettledPageAsync(replacing: root);

        // THE REGRESSION: while the pushed page slides in over it, the covered page must still be
        // drawn where the incoming one has not reached yet. Torn down at commit it leaves the
        // window background there, and this frame never occurs.
        frames.Should()
              .Contain(
                  frame => Is(frame.Left, root) && Is(frame.Right, detail),
                  "the covered page stays on screen while the pushed one slides over it — frames: " + Describe(frames)
              );

        AssertNoBackgroundFlash(frames, root, detail);
    }

    [Fact]
    public async Task PopKeepsThePoppedPageAboveTheRevealedOne()
    {
        await App.WaitForElementAsync("MoRootPage");
        var root = await MeasureSettledPageAsync();

        await App.TapAsync("PushMoDetail");
        await App.WaitForElementAsync("MoDetailPage");
        var detail = await MeasureSettledPageAsync(replacing: root);

        await App.TapAsync("PopMoDetail");
        var frames = await CaptureFramesAsync(MotionSeconds * 1.6);

        // THE REGRESSION: the popped page slides away ABOVE the page it reveals. Drawn underneath
        // it — or dropped at commit — it is never seen again and this frame never occurs.
        frames.Should()
              .Contain(
                  frame => Is(frame.Left, root) && Is(frame.Right, detail),
                  "the popped page stays on screen, on top, while it slides away — frames: " + Describe(frames)
              );

        AssertNoBackgroundFlash(frames, root, detail);

        await App.WaitForElementAsync("MoRootPage");
    }

    [Fact]
    public async Task CustomSpecMovesBothPagesAndLeavesNeitherBehind()
    {
        await App.WaitForElementAsync("MoRootPage");
        var root = await MeasureSettledPageAsync();

        // This spec enters from the BOTTOM (nothing enters from the right) and gives the covered
        // page a real Behind motion (scale + dim) instead of leaving it at rest.
        await App.TapAsync("PushMoCustom");
        var frames = await CaptureFramesAsync(MotionSeconds * 1.6);

        await App.WaitForElementAsync("MoCustomPage");
        var custom = await MeasureSettledPageAsync(replacing: root);

        // A vertical entry covers both sample points at once, so the proof that the covered page
        // was held is simply that SOME frame still shows it while the incoming page is arriving —
        // and that no frame shows the window background at either point.
        frames.Should()
              .Contain(
                  frame => Is(frame.Left, root) || Is(frame.Right, root),
                  "the covered page is on screen while the custom entry plays over it — frames: " + Describe(frames)
              );

        frames.Should()
              .Contain(frame => Is(frame.Left, custom), "the pushed page arrives — frames: " + Describe(frames));

        // No per-frame background check here: a page rising from the BOTTOM sweeps its own
        // controls through the sample points, so a frame may legitimately be button-coloured.
        // The horizontal slides above own that assertion, where the sample points stay on flat
        // page background for the whole motion.

        // The pop replays it in reverse and must restore the covered page exactly.
        await App.TapAsync("PopMoCustom");
        await App.WaitForElementGoneAsync("MoCustomPage");

        // Full opacity and natural scale again — a Behind motion that is not reversed leaves the
        // revealed page permanently dimmed.
        await WaitForPageColorAsync(root, "the page the custom spec dimmed and scaled is restored");
    }

    [Fact]
    public async Task SharedElementFlightLandsAndRestoresWithoutFlashing()
    {
        await App.WaitForElementAsync("MoRootPage");
        var rootHero = await App.WaitForStableBoundsAsync("MoRootHero");
        var root = await MeasureSettledPageAsync();

        // Shared element + a custom slow spec on the same navigation: the flight runs in the
        // presenter's overlay while the pages themselves still play their motion.
        await App.TapAsync("PushMoShared");
        var frames = await CaptureFramesAsync(MotionSeconds * 1.6);

        await App.WaitForElementAsync("MoSharedPage");
        var shared = await MeasureSettledPageAsync(replacing: root);

        // Only the pages may ever be on screen. How much page MOTION accompanies the flight is
        // deliberately platform-specific — iOS hands the whole transition to the flight and swaps
        // the pages under it, Android slides the pushed page in as usual — so this asserts the
        // property both must honour rather than one platform's choreography.
        AssertNoBackgroundFlash(frames, root, shared);

        // The hero must LAND at the destination's own (larger) geometry: a flight that never
        // completes leaves the destination hero at the source size, or invisible.
        var sharedHero = await App.WaitForStableBoundsAsync("MoSharedHero");
        sharedHero.Width.Should().BeGreaterThan(rootHero.Width * 2, "the destination hero is the larger one");
        (await App.WaitForElementAsync("MoSharedHero")).IsVisible.Should().BeTrue();

        // Back: the source hero must be rendered again — the flight hides it while it is away,
        // and only the engine's cleanup brings it back.
        await App.TapAsync("PopMoShared");
        await App.WaitForElementGoneAsync("MoSharedPage");
        await App.WaitForBoundsAsync(
            "MoRootHero",
            b => Math.Abs(b.X - rootHero.X) <= 1 && Math.Abs(b.Y - rootHero.Y) <= 1 && Math.Abs(b.Width - rootHero.Width) <= 1
        );

        (await App.WaitForElementAsync("MoRootHero")).IsVisible.Should().BeTrue();
    }

    [Fact]
    public async Task PushAndPopSettleAtTheirNaturalGeometry()
    {
        await App.WaitForElementAsync("MoRootPage");
        var rootLabel = await App.WaitForStableBoundsAsync("MoRootPage");
        var root = await MeasureSettledPageAsync();

        await App.TapAsync("PushMoDetail");
        await App.WaitForElementAsync("MoDetailPage");
        var detailLabel = await App.WaitForStableBoundsAsync("MoDetailPage");
        detailLabel.X.Should().BeApproximately(rootLabel.X, 1, "the pushed page lands unshifted");
        var detail = await MeasureSettledPageAsync(replacing: root);

        await App.TapAsync("PopMoDetail");
        await App.WaitForElementGoneAsync("MoDetailPage");

        // The revealed page must be left motion-clean: no leftover transform from the transition.
        await App.WaitForBoundsAsync(
            "MoRootPage",
            b => Math.Abs(b.X - rootLabel.X) <= 1 && Math.Abs(b.Y - rootLabel.Y) <= 1 && Math.Abs(b.Width - rootLabel.Width) <= 1
        );

        await WaitForPageColorAsync(root, "the popped page is gone and the revealed one is back at full opacity");
    }
}
