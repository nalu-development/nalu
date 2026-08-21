namespace Nalu.Maui.Test.ScaffoldTests;

/// <summary>
/// <see cref="Scaffold.OverlayEvent"/> contract: one Presented/Closed pair per overlay instance,
/// in order, whatever the close path, with the content/model/result the consumer needs; nothing
/// for a presentation that failed. Presenter stubbed (the event is raised by the scaffold, not
/// by the platform realization).
/// </summary>
public class ScaffoldOverlayEventTests
{
    /// <summary>Presenter stub: succeeds, closes through the request's cleanup (like the real ones), replace = close old.</summary>
    private sealed class StubPresenter : IScaffoldPresenter
    {
        private readonly List<ScaffoldOverlayRequest> _presented = [];

        /// <summary>When set, the next presentation fails (cleanup already run, false returned).</summary>
        public bool FailNext { get; set; }

        /// <summary>When set, the next presentation is closed BEFORE it reports success (a close racing the enter animation).</summary>
        public bool CloseDuringNext { get; set; }

        public Task SynchronizeAsync(ScaffoldRoot root, ScaffoldPresentationHint hint) => Task.CompletedTask;

        public Task<bool> ShowOverlayAsync(ScaffoldOverlayRequest request)
        {
            if (FailNext)
            {
                FailNext = false;
                request.Cleanup?.Invoke();

                return Task.FromResult(false);
            }

            _presented.Add(request);

            if (CloseDuringNext)
            {
                CloseDuringNext = false;
                _presented.Remove(request);
                request.Cleanup?.Invoke();
            }

            return Task.FromResult(true);
        }

        public Task ReplaceTabBarPanelAsync(ScaffoldOverlayRequest replacement)
        {
            var previous = _presented.LastOrDefault(r => r.Kind == ScaffoldOverlayKind.TabBarPanel);

            if (previous is not null)
            {
                _presented.Remove(previous);
                previous.Cleanup?.Invoke();
            }

            _presented.Add(replacement);

            return Task.CompletedTask;
        }

        public Task CloseOverlayAsync(ScaffoldOverlayRequest request)
        {
            if (_presented.Remove(request))
            {
                request.Cleanup?.Invoke();
            }

            return Task.CompletedTask;
        }

        public Task CloseTopOverlayAsync() => Task.CompletedTask;

        public Task CloseAllOverlaysAsync() => Task.CompletedTask;

        public bool HasOverlay => _presented.Count > 0;

        public void ReleasePage(Page page)
    {
    }

    public bool IsOverlayPresented(ScaffoldOverlayRequest request) => _presented.Contains(request);
    }

    private readonly StubPresenter _presenter = new();
    private readonly Scaffold _scaffold;
    private readonly List<ScaffoldOverlayEventArgs> _events = [];

    public ScaffoldOverlayEventTests()
    {
        _scaffold = new Scaffold { Presenter = _presenter };
        _scaffold.OverlayEvent += (_, e) => _events.Add(e);
    }

    [Fact(DisplayName = "Popup: Presented then Closed, with the content and no model")]
    public async Task PopupRaisesAPair()
    {
        var content = new Label();
        var handle = await _scaffold.ShowPopupAsync(content);

        _events.Should().ContainSingle().Which.Should().Match<ScaffoldOverlayEventArgs>(e =>
            e.Kind == ScaffoldOverlayKind.Popup && e.EventType == ScaffoldOverlayEventType.Presented && ReferenceEquals(e.Content, content) && e.Model == null && e.Result == null);

        await handle.CloseAsync();

        _events.Should().HaveCount(2);
        _events[1].EventType.Should().Be(ScaffoldOverlayEventType.Closed);
        _events[1].Kind.Should().Be(ScaffoldOverlayKind.Popup);
        _events[1].Content.Should().BeSameAs(content);

        // Closing again is a no-op: still one pair.
        await handle.CloseAsync();
        _events.Should().HaveCount(2);
    }

    [Fact(DisplayName = "Bottom sheet: the event carries YOUR content, not the sheet wrapper")]
    public async Task BottomSheetReportsTheUserContent()
    {
        var content = new Label();
        var handle = await _scaffold.ShowBottomSheetAsync(content);
        await handle.CloseAsync();

        _events.Select(e => e.Kind).Should().AllBeEquivalentTo(ScaffoldOverlayKind.BottomSheet);
        _events.Select(e => e.EventType).Should().Equal(ScaffoldOverlayEventType.Presented, ScaffoldOverlayEventType.Closed);
        _events.Should().OnlyContain(e => ReferenceEquals(e.Content, content));
    }

    [Fact(DisplayName = "A failed presentation raises nothing (its cleanup still runs)")]
    public async Task FailedPresentationRaisesNothing()
    {
        _presenter.FailNext = true;
        var handle = await _scaffold.ShowPopupAsync(new Label());

        handle.IsOpen.Should().BeFalse();
        _events.Should().BeEmpty();
    }

    [Fact(DisplayName = "A close racing the presentation still yields Presented THEN Closed")]
    public async Task CloseRacingPresentationKeepsTheOrder()
    {
        _presenter.CloseDuringNext = true;
        var handle = await _scaffold.ShowPopupAsync(new Label());

        handle.IsOpen.Should().BeFalse();
        _events.Select(e => e.EventType).Should().Equal(ScaffoldOverlayEventType.Presented, ScaffoldOverlayEventType.Closed);
    }

    [Fact(DisplayName = "Tab bar panel: replacing the panel closes the old one and presents the new one")]
    public async Task TabBarPanelReplaceClosesAndPresents()
    {
        var first = new Label();
        var second = new Label();

        await _scaffold.ShowTabBarPanelAsync(first);
        await _scaffold.ShowTabBarPanelAsync(second, closeIfOpened: false);
        await _scaffold.CloseTabBarPanelAsync();

        _events.Select(e => (e.EventType, e.Content)).Should().Equal(
            (ScaffoldOverlayEventType.Presented, first),
            (ScaffoldOverlayEventType.Closed, first),
            (ScaffoldOverlayEventType.Presented, second),
            (ScaffoldOverlayEventType.Closed, second)
        );
        _events.Should().OnlyContain(e => e.Kind == ScaffoldOverlayKind.TabBarPanel);
    }

    [Fact(DisplayName = "Tab bar panel: the toggle close raises Closed for the presented panel only")]
    public async Task TabBarPanelToggleClosesThePresentedOne()
    {
        var first = new Label();

        await _scaffold.ShowTabBarPanelAsync(first);
        await _scaffold.ShowTabBarPanelAsync(new Label()); // toggle: closes, the new content is never presented

        _events.Select(e => (e.EventType, e.Content)).Should().Equal(
            (ScaffoldOverlayEventType.Presented, first),
            (ScaffoldOverlayEventType.Closed, first)
        );
    }

    [Fact(DisplayName = "A model-first close with a result reports it on Closed")]
    public async Task ResultIsReportedOnClosed()
    {
        var handle = await _scaffold.ShowPopupCoreAsync(new Label(), options: null, model: null, intent: null);

        // What IOverlayService does after presenting: bind the ref to the handle; the model then
        // closes through the ref with a result.
        var overlayRef = new ScaffoldOverlayRef<string>();
        overlayRef.Bind(handle);
        await overlayRef.CloseAsync("picked");

        _events.Select(e => e.EventType).Should().Equal(ScaffoldOverlayEventType.Presented, ScaffoldOverlayEventType.Closed);
        _events[0].Result.Should().BeNull();
        _events[1].Result.Should().Be("picked");
    }

    [Fact(DisplayName = "Flyout: Presented/Closed carry the side")]
    public async Task FlyoutRaisesAPairWithTheSide()
    {
        _scaffold.FlyoutStart = new Label();
        Scaffold.SetFlyoutStartMode(_scaffold, ScaffoldFlyoutMode.Flyout);

        await _scaffold.OpenFlyoutAsync(ScaffoldFlyoutSide.Start);
        await _scaffold.CloseFlyoutAsync();

        _events.Select(e => e.EventType).Should().Equal(ScaffoldOverlayEventType.Presented, ScaffoldOverlayEventType.Closed);
        _events.Should().OnlyContain(e => e.Kind == ScaffoldOverlayKind.Flyout && e.FlyoutSide == ScaffoldFlyoutSide.Start);
    }
}
