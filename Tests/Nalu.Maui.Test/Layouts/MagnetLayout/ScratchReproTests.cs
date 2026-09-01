using Microsoft.Maui.Layouts;

namespace Nalu.Maui.Test.Layouts.MagnetLayout;

public class ScratchReproTests
{
    private sealed class TestView(double width, double height) : View
    {
        protected override Size MeasureOverride(double wc, double hc) => new(Math.Min(width, wc), Math.Min(height, hc));
    }

    [Fact]
    public void PlaygroundLikeChainWithGapMeasuresUnderScrollViewConstraints()
    {
        var magnet = new Magnet { WidthRequest = 340 };
        var chain = new MagnetChain { MagnetId = "wtChain", Style = MagnetChainStyle.Spread, Gap = 4 };

        for (var i = 0; i < 3; i++)
        {
            var id = $"wt{(char) ('A' + i)}";
            var view = new TestView(40, 28);
            var node = Magnet.GetConstraints(view).Id(id).Top(MagnetAnchor.Parent).Size(MagnetSizing.Constraint, 28);

            if (i == 0)
            {
                node.Left(MagnetAnchor.Parent, margin: 0);
            }

            chain.Nodes.Add(id);
            magnet.Add(view);
        }

        chain.Weights.Add(1);
        chain.Weights.Add(2);
        chain.Weights.Add(1);
        magnet.Definition = new MagnetDefinition().Add(chain);

        var manager = (ILayoutManager) typeof(Layout).GetProperty("LayoutManager", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(magnet)!;
        var measured = manager.Measure(340, double.PositiveInfinity);
        manager.ArrangeChildren(new Rect(0, 0, 340, measured.Height));
    }
}
