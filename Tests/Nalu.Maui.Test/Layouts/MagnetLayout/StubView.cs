using Microsoft.Maui.Graphics;

namespace Nalu.Maui.Test.Layouts.MagnetLayout;

/// <summary>
/// Allocation-free <see cref="IView" /> with a constant desired size.
/// </summary>
internal sealed class StubView(double width, double height) : IView
{
    public int MeasureCount { get; private set; }
    public Rect Frame { get; set; }
    public Size DesiredSize { get; private set; }
    public Visibility Visibility { get; set; } = Visibility.Visible;

    public Size Measure(double widthConstraint, double heightConstraint)
    {
        MeasureCount++;
        DesiredSize = new Size(Math.Min(width, widthConstraint), Math.Min(height, heightConstraint));

        return DesiredSize;
    }

    public Size Arrange(Rect bounds)
    {
        Frame = bounds;

        return bounds.Size;
    }

    public void InvalidateMeasure() { }
    public void InvalidateArrange() { }
    public bool Focus() => false;
    public void Unfocus() { }
    public IElementHandler? Handler { get; set; }
    IViewHandler? IView.Handler { get; set; }
    public IElement? Parent => null;
    public double AnchorX => 0.5;
    public double AnchorY => 0.5;
    public double Rotation => 0;
    public double RotationX => 0;
    public double RotationY => 0;
    public double Scale => 1;
    public double ScaleX => 1;
    public double ScaleY => 1;
    public double TranslationX => 0;
    public double TranslationY => 0;
    public string AutomationId => "";
    public Paint? Background => null;
    public IShape? Clip => null;
    public FlowDirection FlowDirection => FlowDirection.LeftToRight;
    public double Height => -1;
    public double Width => -1;
    public Microsoft.Maui.Primitives.LayoutAlignment HorizontalLayoutAlignment => Microsoft.Maui.Primitives.LayoutAlignment.Fill;
    public Microsoft.Maui.Primitives.LayoutAlignment VerticalLayoutAlignment => Microsoft.Maui.Primitives.LayoutAlignment.Fill;
    public bool InputTransparent => false;
    public bool IsEnabled => true;
    public bool IsFocused { get; set; }
    public Thickness Margin => default;
    public double MaximumHeight => double.PositiveInfinity;
    public double MaximumWidth => double.PositiveInfinity;
    public double MinimumHeight => -1;
    public double MinimumWidth => -1;
    public double Opacity => 1;
    public Semantics? Semantics => null;
    public IShadow? Shadow => null;
    public int ZIndex => 0;
}
