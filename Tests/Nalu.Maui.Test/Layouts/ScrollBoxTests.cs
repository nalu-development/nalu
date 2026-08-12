namespace Nalu.Maui.Test.Layouts;

public class ScrollBoxTests
{
    // ComputeTargetDistance(position, elementStart, elementSize, referenceDistance, visibleSize, contentSize)

    [Fact(DisplayName = "Start scrolls the element to the leading edge")]
    public void StartScrollsTheElementToTheLeadingEdge()
        => ScrollBox.ComputeTargetDistance(ScrollToPosition.Start, 500, 100, 0, 400, 2000).Should().Be(500);

    [Fact(DisplayName = "Center scrolls the element to the middle of the visible area")]
    public void CenterScrollsTheElementToTheMiddle()
        => ScrollBox.ComputeTargetDistance(ScrollToPosition.Center, 500, 100, 0, 400, 2000).Should().Be(500 + 50 - 200);

    [Fact(DisplayName = "End scrolls the element to the trailing edge")]
    public void EndScrollsTheElementToTheTrailingEdge()
        => ScrollBox.ComputeTargetDistance(ScrollToPosition.End, 500, 100, 0, 400, 2000).Should().Be(500 + 100 - 400);

    [Fact(DisplayName = "MakeVisible does not move when the element is already fully visible")]
    public void MakeVisibleDoesNotMoveWhenAlreadyVisible()
        => ScrollBox.ComputeTargetDistance(ScrollToPosition.MakeVisible, 500, 100, 450, 400, 2000).Should().Be(450);

    [Fact(DisplayName = "MakeVisible scrolls minimally forward when the element is below the viewport")]
    public void MakeVisibleScrollsMinimallyForward()
        => ScrollBox.ComputeTargetDistance(ScrollToPosition.MakeVisible, 900, 100, 0, 400, 2000).Should().Be(900 + 100 - 400);

    [Fact(DisplayName = "MakeVisible scrolls minimally backward when the element is above the viewport")]
    public void MakeVisibleScrollsMinimallyBackward()
        => ScrollBox.ComputeTargetDistance(ScrollToPosition.MakeVisible, 100, 100, 800, 400, 2000).Should().Be(100);

    [Fact(DisplayName = "Targets are clamped to the valid scroll range")]
    public void TargetsAreClampedToTheValidScrollRange()
    {
        // Element at the very end: Start would overshoot past the max distance.
        ScrollBox.ComputeTargetDistance(ScrollToPosition.Start, 1900, 100, 0, 400, 2000).Should().Be(1600);

        // Element at the very beginning: End would undershoot below zero.
        ScrollBox.ComputeTargetDistance(ScrollToPosition.End, 0, 100, 500, 400, 2000).Should().Be(0);

        // Content smaller than the viewport: nothing to scroll.
        ScrollBox.ComputeTargetDistance(ScrollToPosition.Center, 100, 50, 0, 400, 300).Should().Be(0);
    }
}

public class ScrollBoxSizingStrategyTests
{
    [Fact(DisplayName = "Fill is the default-constructed strategy")]
    public void FillIsTheDefaultConstructedStrategy()
    {
        var strategy = ScrollBoxSizingStrategy.Fill;

        strategy.Mode.Should().Be(ScrollBoxSizingMode.Fill);
        strategy.Should().Be(default(ScrollBoxSizingStrategy));
        strategy.MaxExtent.Should().Be(double.PositiveInfinity);
    }

    [Fact(DisplayName = "Max carries its extent and compares by value")]
    public void MaxCarriesItsExtentAndComparesByValue()
    {
        var strategy = ScrollBoxSizingStrategy.Max(300);

        strategy.Mode.Should().Be(ScrollBoxSizingMode.Max);
        strategy.MaxExtent.Should().Be(300);
        strategy.Should().Be(ScrollBoxSizingStrategy.Max(300));
        strategy.Should().NotBe(ScrollBoxSizingStrategy.Unbounded);
    }

    [Theory(DisplayName = "Max rejects non-positive and NaN extents")]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    public void MaxRejectsInvalidExtents(double extent)
        => ((Action) (() => ScrollBoxSizingStrategy.Max(extent))).Should().Throw<ArgumentOutOfRangeException>();

    [Theory(DisplayName = "String conversion parses modes and bare numbers")]
    [InlineData("Fill", ScrollBoxSizingMode.Fill)]
    [InlineData("fill", ScrollBoxSizingMode.Fill)]
    [InlineData("Unbounded", ScrollBoxSizingMode.Unbounded)]
    [InlineData("300", ScrollBoxSizingMode.Max)]
    [InlineData("", ScrollBoxSizingMode.Fill)]
    public void StringConversionParsesModesAndBareNumbers(string input, ScrollBoxSizingMode expectedMode)
        => ((ScrollBoxSizingStrategy) input).Mode.Should().Be(expectedMode);

    [Fact(DisplayName = "String conversion rejects unknown values")]
    public void StringConversionRejectsUnknownValues()
        => ((Action) (() => _ = (ScrollBoxSizingStrategy) "nope")).Should().Throw<FormatException>();

    [Theory(DisplayName = "ToString round-trips")]
    [InlineData("Fill")]
    [InlineData("Unbounded")]
    [InlineData("300")]
    public void ToStringRoundTrips(string value)
        => ((ScrollBoxSizingStrategy) value).ToString().Should().Be(value);
}

/// <summary>
/// Covers the <see cref="ScrollBox.ScrollToAsync(double, double, bool)" /> completion contract at
/// the control level — the paths that do not need a device: queueing without a handler,
/// supersession, and teardown while a request is outstanding. A hang here is the failure mode the
/// contract exists to prevent, so every assertion has a timeout.
/// </summary>
public class ScrollBoxScrollToContractTests
{
    private static readonly TimeSpan _completionTimeout = TimeSpan.FromSeconds(2);

    private static async Task<bool> CompletedAsync(Task task)
    {
#pragma warning disable VSTHRD003 // The task under test is deliberately created by the code under test.
        var finished = await Task.WhenAny(task, Task.Delay(_completionTimeout)).ConfigureAwait(false);
#pragma warning restore VSTHRD003

        return ReferenceEquals(finished, task);
    }

    [Fact(DisplayName = "A request issued without a handler is queued, not completed")]
    public async Task RequestWithoutHandlerIsQueued()
    {
        var scrollBox = new ScrollBox();

        var task = scrollBox.ScrollToAsync(0, 100, animated: false);

        // It must WAIT for the handler (that is the pre-layout queue), not resolve as a no-op.
        (await CompletedAsync(task)).Should().BeFalse();
    }

    [Fact(DisplayName = "A request queued before the handler existed is flushed to it on connect")]
    public void QueuedRequestIsFlushedWhenTheHandlerConnects()
    {
        var scrollBox = new ScrollBox();
        _ = scrollBox.ScrollToAsync(0, 100, animated: false);

        var handler = Substitute.For<IViewHandler>();
        ((IElement) scrollBox).Handler = handler;

        // The pre-layout queue exists to survive exactly this gap.
        handler.Received().Invoke("ScrollTo", Arg.Any<object>());
    }

    [Fact(DisplayName = "Detaching the handler completes a request still awaiting the first layout")]
    public async Task DetachingTheHandlerCompletesARequestAwaitingLayout()
    {
        var content = new VerticalStackLayout();
        var target = new Label();
        content.Add(target);
        var scrollBox = new ScrollBox { Content = content };
        ((IElement) scrollBox).Handler = Substitute.For<IViewHandler>();

        // No geometry yet, so the descendant target cannot be resolved: the request waits for the
        // first layout, which will now never come.
        var task = scrollBox.ScrollToAsync(target);

        (await CompletedAsync(task)).Should().BeFalse();

        ((IElement) scrollBox).Handler = null;

        // Detached: awaiting must return rather than hang the caller forever.
        (await CompletedAsync(task)).Should().BeTrue();
    }

    [Fact(DisplayName = "A superseded request completes rather than dangling")]
    public async Task SupersededRequestsComplete()
    {
        var scrollBox = new ScrollBox();

        var first = scrollBox.ScrollToAsync(0, 100, animated: false);
        _ = scrollBox.ScrollToAsync(0, 200, animated: false);

        // Only the latest target survives, but the abandoned task must not be left dangling.
        (await CompletedAsync(first)).Should().BeTrue();
    }

    [Fact(DisplayName = "Scrolling to a view that is not a descendant is rejected")]
    public async Task ScrollingToANonDescendantIsRejected()
    {
        var scrollBox = new ScrollBox { Content = new VerticalStackLayout() };
        var stranger = new Label();

        await ((Func<Task>) (() => scrollBox.ScrollToAsync(stranger))).Should().ThrowAsync<ArgumentException>();
    }

    [Fact(DisplayName = "Replacing the content releases the previous content's invalidation subscription")]
    public void ReplacingContentReleasesThePreviousSubscription()
    {
        var scrollBox = new ScrollBox { SizingStrategy = ScrollBoxSizingStrategy.Unbounded };
        var first = new VerticalStackLayout();
        scrollBox.Content = first;
        scrollBox.Content = new VerticalStackLayout();

        // The replaced content must no longer be able to reach the box: a stale MeasureInvalidated
        // subscription is both a leak and a source of phantom re-measures.
        var invalidated = false;
        scrollBox.MeasureInvalidated += (_, _) => invalidated = true;
        first.Add(new Label { HeightRequest = 40 });

        invalidated.Should().BeFalse();
    }
}
