using System.Diagnostics;
using Nalu.Maui.Sample.PageModels;

namespace Nalu.Maui.Sample.Pages;

public partial class MagnetDemoPage : ContentPage
{
    public MagnetDemoPage(MagnetDemoPageModel magnetDemoPageModel)
    {
        BindingContext = magnetDemoPageModel;
        InitializeComponent();
    }
}

public class MeasuredGrid : Grid
{
    /// <inheritdoc />
    protected override Size MeasureOverride(double widthConstraint, double heightConstraint)
    {
        var sw = new Stopwatch();
        sw.Start();
        var measureOverride = base.MeasureOverride(widthConstraint, heightConstraint);
        sw.Stop();
        Debug.WriteLine($"Grid MeasureOverride: {sw.Elapsed.Microseconds}us");

        return measureOverride;
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Rect bounds)
    {
        var sw = new Stopwatch();
        sw.Start();
        var arrangeOverride = base.ArrangeOverride(bounds);
        sw.Stop();
        Debug.WriteLine($"Grid ArrangeOverride: {sw.Elapsed.Microseconds}us");

        return arrangeOverride;
    }
}
