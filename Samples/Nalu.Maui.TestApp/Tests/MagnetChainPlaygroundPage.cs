using JetBrains.Annotations;

namespace Nalu.Maui.TestApp.Tests;

/// <summary>
/// Interactive playground for chains × margins × visibility: every demo chain declares per-pair margins
/// (with explicit gone margins) and the A/B/C/D buttons toggle the visibility of the corresponding member
/// in every chain — animated through <see cref="Magnet.TransitionToAsync(System.Action,uint,Easing?)" />
/// when the switch is on.
/// </summary>
[UsedImplicitly]
[TestPage("Magnet Chain Playground")]
public class MagnetChainPlaygroundPage : ContentPage
{
    private const string P = MagnetAnchor.Parent;

    private static readonly Color[] _colors = [Color.FromArgb("#5B8DEF"), Color.FromArgb("#9B7BF3"), Color.FromArgb("#F58C6E"), Color.FromArgb("#3EBD73")];
    private readonly Dictionary<char, List<(Magnet Magnet, View View)>> _byLetter = new()
    {
        ['A'] = [],
        ['B'] = [],
        ['C'] = [],
        ['D'] = []
    };

    private readonly Switch _animateSwitch = new() { IsToggled = true, AutomationId = "AnimateSwitch" };

    public MagnetChainPlaygroundPage()
    {
        // (margin, gone) of the anchor of member i towards member i-1 (index 0 = the leading margin
        // of the first member towards the chain start); gone null = default (falls back to the margin).
        (double Margin, double? Gone)?[] gaps = [null, (8, 2), (16, 4)];
        (double Margin, double? Gone)?[] gaps4 = [null, (5, 1), (6, 2), (7, 3)];

        var stack = new VerticalStackLayout { Spacing = 10, Padding = 16 };

        var toggles = new HorizontalStackLayout { Spacing = 8 };

        foreach (var letter in "ABCD")
        {
            var button = new Button { Text = $"{letter}", AutomationId = $"Toggle{letter}", WidthRequest = 52 };
            button.Clicked += async (_, _) => await ToggleAsync(letter);
            toggles.Children.Add(button);
        }

        toggles.Children.Add(new Label { Text = "Animate", VerticalOptions = LayoutOptions.Center });
        toggles.Children.Add(_animateSwitch);
        stack.Children.Add(toggles);

        AddDemo(stack, "Packed · gaps 8 (gone 2) / 16 (gone 4)", "pk", MagnetChainStyle.Packed, gaps, weights: null, bias: 0);
        AddDemo(stack, "Spread · same gaps", "sp", MagnetChainStyle.Spread, gaps, weights: null, bias: null);
        AddDemo(stack, "SpreadInside · same gaps", "si", MagnetChainStyle.SpreadInside, gaps, weights: null, bias: null);
        AddDemo(stack, "Weighted 1:2:1 · chain Gap 4", "wt", MagnetChainStyle.Spread, [null, null, null], weights: [1, 2, 1], bias: null, gap: 4);
        AddDemo(stack, "Packed ×4 · gaps 5/6/7 (gone 1/2/3)", "p4", MagnetChainStyle.Packed, gaps4, weights: null, bias: 0);
        AddDemo(stack, "10 A 20 B 30 C · gone = margin (default)", "ex", MagnetChainStyle.Packed, [(10, null), (20, null), (30, null)], weights: null, bias: 0);

        Content = new ScrollView { Content = stack };
    }

    private void AddDemo(
        VerticalStackLayout stack,
        string caption,
        string prefix,
        MagnetChainStyle style,
        (double Margin, double? Gone)?[] gaps,
        double[]? weights,
        double? bias,
        double gap = 0
    )
    {
        stack.Children.Add(new Label { Text = caption, FontSize = 12, TextColor = Color.FromArgb("#7A8194") });

        var magnet = new Magnet
        {
            AutomationId = $"{prefix}Root",
            WidthRequest = 340,
            HorizontalOptions = LayoutOptions.Start,
            BackgroundColor = Color.FromArgb("#ECEFF5"),
            Padding = new Thickness(0, 6)
        };

        var chain = new MagnetChain { MagnetId = $"{prefix}Chain", Style = style, Gap = gap };

        for (var i = 0; i < gaps.Length; i++)
        {
            var letter = (char) ('A' + i);
            var id = $"{prefix}{letter}";
            var view = CreateMember(id, letter, _colors[i]);
            var node = Magnet.GetConstraints(view).Id(id).Top(P);

            if (weights is null)
            {
                node.Size(40, 28);
            }
            else
            {
                node.Size(MagnetSizing.Constraint, 28);
            }

            if (i == 0)
            {
                node.Left(P, margin: gaps[0]?.Margin ?? 0, goneMargin: gaps[0]?.Gone);

                if (bias is { } b)
                {
                    node.Bias(b, 0.5);
                }
            }
            else if (gaps[i] is { } pairGap)
            {
                node.Left($"{prefix}{(char) (letter - 1)}", MagnetPole.Right, pairGap.Margin, goneMargin: pairGap.Gone);
            }

            chain.Nodes.Add(id);
            magnet.Add(view);
            _byLetter[letter].Add((magnet, view));
        }

        if (weights is not null)
        {
            foreach (var weight in weights)
            {
                chain.Weights.Add(weight);
            }
        }

        magnet.Definition = new MagnetDefinition().Add(chain);
        stack.Children.Add(magnet);
    }

    private static View CreateMember(string id, char letter, Color color)
        => new Border
        {
            BackgroundColor = color,
            StrokeThickness = 0,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 6 },
            Content = new Label
            {
                AutomationId = $"{id}Label",
                Text = $"{letter}",
                TextColor = Colors.White,
                FontSize = 13,
                FontAttributes = FontAttributes.Bold,
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center
            }
        };

    private async Task ToggleAsync(char letter)
    {
        var animate = _animateSwitch.IsToggled;
        var tasks = new List<Task>();

        foreach (var (magnet, view) in _byLetter[letter])
        {
            if (animate)
            {
                tasks.Add(magnet.TransitionToAsync(() => view.IsVisible = !view.IsVisible, 350));
            }
            else
            {
                view.IsVisible = !view.IsVisible;
            }
        }

        await Task.WhenAll(tasks);
    }
}
