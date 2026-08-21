using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Nalu.Maui.Test.ScaffoldTests;

/// <summary>
/// SPIKE: a home-made "relative binding source" — a relay object we own, populated by our own
/// ancestry walk, used as a plain <see cref="Binding.Source"/> so full reflection string paths
/// survive. Not production code; measures viability only.
/// </summary>
public class SpikeRelativeSourceTests
{
    public SpikeRelativeSourceTests() => DispatcherProvider.SetCurrent(new DispatcherProviderStub());

    // ---- stand-in for ScaffoldNavBarContext -------------------------------------------------
    private sealed class FakeContext : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        public string? Title
        {
            get;
            set => Set(ref field, value);
        }

        public Page? CurrentPage
        {
            get;
            set => Set(ref field, value);
        }

        private void Set<T>(ref T f, T v, [CallerMemberName] string? n = null)
        {
            if (!EqualityComparer<T>.Default.Equals(f, v))
            {
                f = v;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
            }
        }
    }

    // ---- the home-made relative source ------------------------------------------------------
    private sealed class ContextRelay : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        public object? Context
        {
            get;
            private set
            {
                if (!ReferenceEquals(field, value))
                {
                    field = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Context)));
                }
            }
        }

        private readonly Func<Page, object?> _lookup;
        private readonly List<Element> _chain = [];
        private Element? _target;

        public ContextRelay(Func<Page, object?> lookup) => _lookup = lookup;

        public void Attach(Element target)
        {
            _target = target;
            Resolve();
        }

        private void Resolve()
        {
            Unsubscribe();

            for (Element? current = _target; current is not null; current = current.Parent)
            {
                _chain.Add(current);
                current.ParentChanged += OnAncestryChanged;

                // The walk is OURS: it recognises both carriers in one pass — the bar host
                // (bar content, incl. a TitleView) and the page (page content).
                if (current is FakeBarHost { BarContext: { } barCtx })
                {
                    Context = barCtx;

                    return;
                }

                if (current is Page and not Scaffold && _lookup((Page) current) is { } ctx)
                {
                    Context = ctx;

                    return;
                }
            }

            Context = null;
        }

        private void OnAncestryChanged(object? sender, EventArgs e) => Resolve();

        private void Unsubscribe()
        {
            foreach (var element in _chain)
            {
                element.ParentChanged -= OnAncestryChanged;
            }

            _chain.Clear();
        }
    }

    /// <summary>Stand-in for ScaffoldNavBarHost: a Scaffold-parented bar host carrying its page's context.</summary>
    private sealed class FakeBarHost : Grid
    {
        public FakeContext? BarContext { get; set; }
    }

    private static readonly Dictionary<Page, FakeContext> _contexts = [];

    private static BindingBase CreateRelayBinding(Element target, string path)
    {
        var relay = new ContextRelay(p => _contexts.GetValueOrDefault(p));
        relay.Attach(target);

        return new Binding($"Context.{path}") { Source = relay };
    }

    [Fact(DisplayName = "SPIKE 1: binds before parenting, resolves once the page is attached")]
    public void ResolvesAfterLateParenting()
    {
        var scaffold = new Scaffold();
        var page = new ContentPage();
        var ctx = new FakeContext { Title = "Alpha" };
        _contexts[page] = ctx;

        // Content built and bound BEFORE it is placed in the page (worst case).
        var label = new Label();
        label.SetBinding(Label.TextProperty, CreateRelayBinding(label, nameof(FakeContext.Title)));

        label.Text.Should().BeNull("nothing resolvable yet");

        var stack = new VerticalStackLayout { Children = { label } };
        page.Content = stack;
        scaffold.AddLogicalChild(page);

        label.Text.Should().Be("Alpha", "the ancestry walk re-ran when the chain changed");

        ctx.Title = "Beta";
        label.Text.Should().Be("Beta", "the relay is a plain source: normal INPC flows through");
    }

    [Fact(DisplayName = "SPIKE 2: deep reflection path survives")]
    public void DeepPathWorks()
    {
        var scaffold = new Scaffold();
        var page = new ContentPage();
        var inner = new ContentPage { Title = "Inner" };
        _contexts[page] = new FakeContext { CurrentPage = inner };

        var label = new Label();
        var stack = new VerticalStackLayout { Children = { label } };
        page.Content = stack;
        scaffold.AddLogicalChild(page);

        label.SetBinding(Label.TextProperty, CreateRelayBinding(label, "CurrentPage.Title"));

        label.Text.Should().Be("Inner", "deep string paths are what the relay buys over a typed map");
    }

    [Fact(DisplayName = "SPIKE 3: two live pages each resolve their OWN context")]
    public void TwoPagesTwoContexts()
    {
        var scaffold = new Scaffold();
        var pageA = new ContentPage();
        var pageB = new ContentPage();
        _contexts[pageA] = new FakeContext { Title = "A" };
        _contexts[pageB] = new FakeContext { Title = "B" };

        var labelA = new Label();
        var labelB = new Label();
        pageA.Content = labelA;
        pageB.Content = labelB;
        scaffold.AddLogicalChild(pageA);
        scaffold.AddLogicalChild(pageB);

        labelA.SetBinding(Label.TextProperty, CreateRelayBinding(labelA, nameof(FakeContext.Title)));
        labelB.SetBinding(Label.TextProperty, CreateRelayBinding(labelB, nameof(FakeContext.Title)));

        labelA.Text.Should().Be("A");
        labelB.Text.Should().Be("B", "this is the transition bug the whole plan exists to fix");
    }

    [Fact(DisplayName = "SPIKE 4: content moved to another page re-resolves")]
    public void ReparentingReresolves()
    {
        var scaffold = new Scaffold();
        var pageA = new ContentPage();
        var pageB = new ContentPage();
        _contexts[pageA] = new FakeContext { Title = "A" };
        _contexts[pageB] = new FakeContext { Title = "B" };
        scaffold.AddLogicalChild(pageA);
        scaffold.AddLogicalChild(pageB);

        var label = new Label();
        pageA.Content = label;
        label.SetBinding(Label.TextProperty, CreateRelayBinding(label, nameof(FakeContext.Title)));
        label.Text.Should().Be("A");

        pageA.Content = null;
        pageB.Content = label;

        label.Text.Should().Be("B", "the relay re-walked on the ancestry change");
    }

    [Fact(DisplayName = "SPIKE 5: MultiBinding + Self gives the target element with no IProvideValueTarget")]
    public void SelfSourcedChildYieldsTarget()
    {
        var scaffold = new Scaffold();
        var page = new ContentPage();
        _contexts[page] = new FakeContext { Title = "CodeBehind" };

        var relay = new ContextRelay(p => _contexts.GetValueOrDefault(p));
        var seen = new List<object?>();

        var multi = new MultiBinding
        {
            Converter = new SelfProbeConverter(relay, seen),
            Bindings =
            {
                new Binding(".") { Source = RelativeBindingSource.Self },
                new Binding("Context.Title") { Source = relay }
            }
        };

        var label = new Label();
        page.Content = label;
        scaffold.AddLogicalChild(page);
        label.SetBinding(Label.TextProperty, multi);

        seen.Should().NotBeEmpty();
        seen[0].Should().BeSameAs(label, "MultiBinding forwards the REAL target to Self-sourced children");
        label.Text.Should().Be("CodeBehind", "the relay populated by the converter closed the loop");
    }

    private sealed class SelfProbeConverter(ContextRelay relay, List<object?> seen) : IMultiValueConverter
    {
        private bool _attached;

        public object? Convert(object?[]? values, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
        {
            seen.Add(values?[0]);

            if (!_attached && values?[0] is Element element)
            {
                _attached = true;
                relay.Attach(element);
            }

            return values?[1];
        }

        public object?[] ConvertBack(object? value, Type[] targetTypes, object? parameter, System.Globalization.CultureInfo culture)
            => throw new NotSupportedException();
    }

    [Fact(DisplayName = "SPIKE 6: relay does not retain the page after the content is detached")]
    public void RelayDoesNotRetain()
    {
        var scaffold = new Scaffold();
        WeakReference pageRef;
        var label = new Label();

        void Build()
        {
            var page = new ContentPage();
            _contexts[page] = new FakeContext { Title = "X" };
            page.Content = label;
            scaffold.AddLogicalChild(page);
            label.SetBinding(Label.TextProperty, CreateRelayBinding(label, nameof(FakeContext.Title)));
            label.Text.Should().Be("X");
            pageRef = new WeakReference(page);
            _contexts.Remove(page);
            page.Content = null;
            scaffold.RemoveLogicalChild(page);
        }

        Build();
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        pageRef.IsAlive.Should().BeFalse("the relay must not be a retainer — see the TestApp leak probe");
    }

    [Fact(DisplayName = "SPIKE 7: ONE walk serves bar content and page content, with the bar host parented to the Scaffold")]
    public void OneWalkServesBothCarriers()
    {
        var scaffold = new Scaffold();

        var page = new ContentPage();
        var pageCtx = new FakeContext { Title = "PAGE" };
        _contexts[page] = pageCtx;
        var pageLabel = new Label();
        page.Content = pageLabel;
        scaffold.AddLogicalChild(page);

        // The bar host is a SIBLING of the page (today's tree — no style leak, no page.Parent change).
        var barHost = new FakeBarHost { BarContext = pageCtx };
        var titleView = new Label();
        barHost.Add(titleView);
        scaffold.AddLogicalChild(barHost);

        // A SECOND page mid-transition, with its own bar host and its own context.
        var otherPage = new ContentPage();
        var otherCtx = new FakeContext { Title = "OTHER" };
        _contexts[otherPage] = otherCtx;
        var otherLabel = new Label();
        otherPage.Content = otherLabel;
        scaffold.AddLogicalChild(otherPage);
        var otherBarHost = new FakeBarHost { BarContext = otherCtx };
        var otherTitleView = new Label();
        otherBarHost.Add(otherTitleView);
        scaffold.AddLogicalChild(otherBarHost);

        // Same factory, one code path, for all four targets.
        pageLabel.SetBinding(Label.TextProperty, CreateRelayBinding(pageLabel, nameof(FakeContext.Title)));
        titleView.SetBinding(Label.TextProperty, CreateRelayBinding(titleView, nameof(FakeContext.Title)));
        otherLabel.SetBinding(Label.TextProperty, CreateRelayBinding(otherLabel, nameof(FakeContext.Title)));
        otherTitleView.SetBinding(Label.TextProperty, CreateRelayBinding(otherTitleView, nameof(FakeContext.Title)));

        pageLabel.Text.Should().Be("PAGE");
        titleView.Text.Should().Be("PAGE", "bar content resolves through the bar-host carrier");
        otherLabel.Text.Should().Be("OTHER");
        otherTitleView.Text.Should().Be("OTHER", "two bars on screen show their OWN page's state");
    }

    [Fact(DisplayName = "SPIKE 8: RETENTION RISK — a fallback walk that reaches the app-lifetime Scaffold")]
    public void FallbackWalkRetentionRisk()
    {
        var scaffold = new Scaffold();
        WeakReference pageRef;

        void Build()
        {
            // No context registered for this page: the walk falls through to the Scaffold and
            // subscribes to ParentChanged all the way up.
            var page = new ContentPage();
            var label = new Label();
            page.Content = label;
            scaffold.AddLogicalChild(page);
            label.SetBinding(Label.TextProperty, CreateRelayBinding(label, nameof(FakeContext.Title)));

            pageRef = new WeakReference(page);
            page.Content = null;
            scaffold.RemoveLogicalChild(page);
        }

        Build();
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        pageRef.IsAlive.Should().BeFalse(
            "IF THIS FAILS: the Scaffold's ParentChanged subscription roots the relay, which roots the target and its page");
    }

    [Fact(DisplayName = "SPIKE 9: RETENTION — does the TARGET survive a fallback walk that subscribed to the Scaffold?")]
    public void FallbackWalkRetainsTarget()
    {
        var scaffold = new Scaffold();
        WeakReference labelRef;

        void Build()
        {
            var page = new ContentPage();
            var label = new Label();
            page.Content = label;
            scaffold.AddLogicalChild(page);

            // No context for this page -> the walk climbs to the Scaffold and subscribes there.
            label.SetBinding(Label.TextProperty, CreateRelayBinding(label, nameof(FakeContext.Title)));

            labelRef = new WeakReference(label);
            page.Content = null;
            scaffold.RemoveLogicalChild(page);
        }

        Build();
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        labelRef.IsAlive.Should().BeFalse(
            "IF THIS FAILS: Scaffold.ParentChanged -> relay -> _target roots every element that ever bound while unresolved");
    }
}
