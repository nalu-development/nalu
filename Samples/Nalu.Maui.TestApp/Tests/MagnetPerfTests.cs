using System.Diagnostics;
using JetBrains.Annotations;

namespace Nalu.Maui.TestApp.Tests;

/// <summary>
/// The credit-card cell in its two flavours, with counters on the layout passes:
/// nested Grid + VerticalStackLayout + FlexLayout vs a flat Magnet.
/// </summary>
internal static class MagnetPerfCards
{
    public static int MeasureCount;
    public static int ArrangeCount;
    public static int InflatedCount;

    /// <summary>Arrange passes that were not preceded by a measure pass on the same card (e.g. recycled cell re-arranged).</summary>
    public static int ArrangesWithoutMeasure;

    private static readonly string[] _names =
    [
        "Mastercard Platinum", "Visa", "American Express Gold Business", "Revolut", "N26 Metal", "Curve", "PostePay Evolution", "Amex"
    ];

    public static void ResetCounters()
    {
        MeasureCount = 0;
        ArrangeCount = 0;
        InflatedCount = 0;
        ArrangesWithoutMeasure = 0;
    }

    private sealed class CountingGrid : Grid
    {
        private bool _measured;

        protected override Size MeasureOverride(double widthConstraint, double heightConstraint)
        {
            MeasureCount++;
            _measured = true;

            return base.MeasureOverride(widthConstraint, heightConstraint);
        }

        protected override Size ArrangeOverride(Rect bounds)
        {
            ArrangeCount++;

            if (!_measured)
            {
                ArrangesWithoutMeasure++;
            }

            _measured = false;

            return base.ArrangeOverride(bounds);
        }
    }

    private sealed class CountingMagnet : Magnet
    {
        private bool _measured;

        protected override Size MeasureOverride(double widthConstraint, double heightConstraint)
        {
            MeasureCount++;
            _measured = true;

            return base.MeasureOverride(widthConstraint, heightConstraint);
        }

        protected override Size ArrangeOverride(Rect bounds)
        {
            ArrangeCount++;

            if (!_measured)
            {
                ArrangesWithoutMeasure++;
            }

            _measured = false;

            return base.ArrangeOverride(bounds);
        }
    }

    public static string NameFor(int i) => _names[i % _names.Length];

    /// <summary>The name label is exposed so that "text change" scenarios can invalidate every card.</summary>
    public static View CreateGridCard(int i, out Label name)
    {
        InflatedCount++;
        var grid = new CountingGrid
        {
            ColumnDefinitions = new ColumnDefinitionCollection(
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            )
        };

        var image = new Border { BackgroundColor = Colors.SteelBlue, StrokeThickness = 0, WidthRequest = 60, HeightRequest = 48, Margin = 4, StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 8 } };
        name = new Label { Text = NameFor(i), FontSize = 16, MaxLines = 1, LineBreakMode = LineBreakMode.TailTruncation };
        var star = new Border { BackgroundColor = Colors.Goldenrod, StrokeThickness = 0, WidthRequest = 16, HeightRequest = 16, Margin = new Thickness(4, 0, 0, 0), VerticalOptions = LayoutOptions.Center, IsVisible = i % 3 == 0 };
        var detail = new Label { Text = $"Mastercard · {(i % 12) + 1:00}/{27 + (i % 5)}", FontSize = 12 };
        var money = new Label { Text = $"€ {(i * 37) % 900 + 12:N2}", FontSize = 18, Padding = 8, BackgroundColor = Colors.LightGoldenrodYellow, VerticalTextAlignment = TextAlignment.Center };

        var flex = new FlexLayout { Direction = Microsoft.Maui.Layouts.FlexDirection.Row, Wrap = Microsoft.Maui.Layouts.FlexWrap.NoWrap };
        FlexLayout.SetShrink(name, 1);
        flex.Add(name);
        flex.Add(star);

        var texts = new VerticalStackLayout { VerticalOptions = LayoutOptions.Center, Margin = new Thickness(8, 0, 8, 0) };
        texts.Add(flex);
        texts.Add(detail);
        Grid.SetColumn(texts, 1);
        Grid.SetColumn(money, 2);

        grid.Add(image);
        grid.Add(texts);
        grid.Add(money);

        return Wrap(grid);
    }

    public static View CreateMagnetCard(int i, out Label name)
    {
        InflatedCount++;
        const string p = MagnetAnchor.Parent;
        var magnet = new CountingMagnet
        {
            Definition = new MagnetDefinition().Add(new MagnetChain { MagnetId = "nameRow", Style = MagnetChainStyle.Packed }.With("name", "star"))
        };

        var image = new Border { BackgroundColor = Colors.SteelBlue, StrokeThickness = 0, StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 8 } };
        Magnet.GetConstraints(image).Id("image").Size(60, 48).AlignLeft(p, 4).VerticallyWithin(p, 4);
        name = new Label { Text = NameFor(i), FontSize = 16, MaxLines = 1, LineBreakMode = LineBreakMode.TailTruncation };
        Magnet.GetConstraints(name).Id("name").After(image, 8).AlignTop(p).Bias(0, 0.5);
        var star = new Border { BackgroundColor = Colors.Goldenrod, StrokeThickness = 0, IsVisible = i % 3 == 0 };
        Magnet.GetConstraints(star).Id("star").Size(16, 16).After(name, 4).Before("money", 8).VerticallyWithin(name);
        var detail = new Label { Text = $"Mastercard · {(i % 12) + 1:00}/{27 + (i % 5)}", FontSize = 12 };
        Magnet.GetConstraints(detail).Id("detail").AlignLeft(name).Below(name);
        var money = new Label { Text = $"€ {(i * 37) % 900 + 12:N2}", FontSize = 18, Padding = 8, BackgroundColor = Colors.LightGoldenrodYellow, VerticalTextAlignment = TextAlignment.Center };
        Magnet.GetConstraints(money).Id("money").AlignRight(p).FillHeight(p);

        magnet.Add(image);
        magnet.Add(name);
        magnet.Add(star);
        magnet.Add(detail);
        magnet.Add(money);

        return Wrap(magnet);
    }

    private static View Wrap(View content)
        => new Border
        {
            Margin = new Thickness(8, 4),
            StrokeThickness = 0,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 8 },
            BackgroundColor = Colors.White,
            Content = content
        };
}

/// <summary>
/// Manual rendering benchmark: inflate N cards (Grid vs Magnet), then relayout / text-change them,
/// measuring managed creation time and the time until the native layout pass settles.
/// </summary>
[UsedImplicitly]
[TestPage("Magnet Perf")]
public class MagnetPerfTestsPage : ContentPage
{
    private readonly VerticalStackLayout _cards = new() { Spacing = 0 };
    private readonly Label _status = new() { AutomationId = "PerfStatus", FontSize = 12, FontFamily = "Courier" };
    private readonly Entry _count = new() { Text = "200", Keyboard = Keyboard.Numeric, WidthRequest = 70, AutomationId = "PerfCount" };
    private readonly List<Label> _names = [];
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private long _t0;
    private long _firstLayout;
    private long _lastLayout;
    private int _layoutEvents;
    private IDispatcherTimer? _settleTimer;
    private string _scenario = "";
    private string _prelude = "";
    private bool _wide = true;

    public MagnetPerfTestsPage()
    {
        var gridButton = new Button { Text = "Grid ×N", AutomationId = "PerfGridButton", FontSize = 12 };
        gridButton.Clicked += (_, _) => Inflate(false);
        var magnetButton = new Button { Text = "Magnet ×N", AutomationId = "PerfMagnetButton", FontSize = 12 };
        magnetButton.Clicked += (_, _) => Inflate(true);
        var relayoutButton = new Button { Text = "Relayout", AutomationId = "PerfRelayoutButton", FontSize = 12 };
        relayoutButton.Clicked += (_, _) => Relayout();
        var textButton = new Button { Text = "Text change", AutomationId = "PerfTextButton", FontSize = 12 };
        textButton.Clicked += (_, _) => ChangeTexts();
        var clearButton = new Button { Text = "Clear", AutomationId = "PerfClearButton", FontSize = 12 };
        clearButton.Clicked += (_, _) => Clear();

        var toolbar = new HorizontalStackLayout
        {
            Spacing = 6,
            Padding = new Thickness(8, 4),
            Children = { _count, gridButton, magnetButton, relayoutButton, textButton, clearButton }
        };

        Content = new Grid
        {
            RowDefinitions = [new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Star)],
            Children =
            {
                toolbar,
                WithRow(_status, 1),
                WithRow(new ScrollView { Content = _cards, BackgroundColor = Colors.LightGray }, 2)
            }
        };
    }

    private static View WithRow(View view, int row)
    {
        Grid.SetRow(view, row);

        return view;
    }

    private void Clear()
    {
        _cards.Clear();
        _names.Clear();
        MagnetPerfCards.ResetCounters();
        _status.Text = "cleared";
    }

    private void Inflate(bool magnet)
    {
        Clear();
        var n = int.TryParse(_count.Text, out var parsed) ? parsed : 200;
        _scenario = $"{(magnet ? "Magnet" : "Grid")} ×{n}";
        Begin();

        var sw = Stopwatch.StartNew();
        var views = new View[n];

        for (var i = 0; i < n; i++)
        {
            views[i] = magnet ? MagnetPerfCards.CreateMagnetCard(i, out var name) : MagnetPerfCards.CreateGridCard(i, out name);
            _names.Add(name);
            HookSizeChanged(views[i]);
        }

        var created = sw.Elapsed.TotalMilliseconds;

        foreach (var view in views)
        {
            _cards.Add(view);
        }

        var added = sw.Elapsed.TotalMilliseconds;
        _prelude = $"managed: created {created:F0} ms, added {added:F0} ms · ";
        _status.Text = $"{_scenario}: {_prelude}waiting for layout…";
    }

    private void Relayout()
    {
        _wide = !_wide;
        _scenario = $"relayout ({_cards.Count} cards, width {(_wide ? "full" : "-40")})";
        Begin();
        _cards.Margin = _wide ? new Thickness(0) : new Thickness(0, 0, 40, 0);
    }

    private void ChangeTexts()
    {
        _scenario = $"text change ({_names.Count} labels)";
        Begin();

        var sw = Stopwatch.StartNew();

        foreach (var name in _names)
        {
            name.Text = name.Text.EndsWith('!') ? name.Text[..^1] : name.Text + "!";
        }

        _prelude = $"managed: set {sw.Elapsed.TotalMilliseconds:F0} ms · ";
        _status.Text = $"{_scenario}: {_prelude}waiting for layout…";
    }

    private void Begin()
    {
        _prelude = "";
        MagnetPerfCards.MeasureCount = 0;
        MagnetPerfCards.ArrangeCount = 0;
        _t0 = _clock.ElapsedTicks;
        _firstLayout = 0;
        _lastLayout = 0;
        _layoutEvents = 0;
        _settleTimer?.Stop();
        _settleTimer = Dispatcher.CreateTimer();
        _settleTimer.Interval = TimeSpan.FromMilliseconds(100);
        _settleTimer.Tick += OnSettleTick;
        _settleTimer.Start();
    }

    /// <summary>
    /// Hooks SizeChanged on the whole subtree: the LAST element to settle marks the real end of the layout pass.
    /// </summary>
    private void HookSizeChanged(IView view)
    {
        if (view is VisualElement ve)
        {
            ve.SizeChanged += OnCardLayout;
        }

        switch (view)
        {
            case Layout layout:
                foreach (var child in layout)
                {
                    HookSizeChanged(child);
                }

                break;

            case Border { Content: { } content }:
                HookSizeChanged(content);

                break;

            case ContentView { Content: { } content }:
                HookSizeChanged(content);

                break;
        }
    }

    private void OnCardLayout(object? sender, EventArgs e)
    {
        var now = _clock.ElapsedTicks;
        _layoutEvents++;

        if (_firstLayout == 0)
        {
            _firstLayout = now;
        }

        _lastLayout = now;
    }

    private void OnSettleTick(object? sender, EventArgs e)
    {
        var now = _clock.ElapsedTicks;
        var sinceLast = Ms(now - (_lastLayout == 0 ? _t0 : _lastLayout));

        // Settled: no card changed size for 400 ms (and at least one did, or 1.5 s passed).
        if (sinceLast < 400 && !(_lastLayout == 0 && Ms(now - _t0) > 1500))
        {
            return;
        }

        _settleTimer?.Stop();
        _settleTimer = null;
        var first = _firstLayout == 0 ? double.NaN : Ms(_firstLayout - _t0);
        var last = _lastLayout == 0 ? double.NaN : Ms(_lastLayout - _t0);
        var cards = _cards.Count;
        _status.Text = $"{_scenario}\n{_prelude}first layout {first:F0} ms · settled {last:F0} ms · size events {_layoutEvents}\n" +
                       $"card measures {MagnetPerfCards.MeasureCount} ({(cards > 0 ? (double) MagnetPerfCards.MeasureCount / cards : 0):F1}/card) · arranges {MagnetPerfCards.ArrangeCount}";
    }

    private double Ms(long ticks) => ticks * 1000.0 / Stopwatch.Frequency;
}
