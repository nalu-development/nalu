using System.Diagnostics.CodeAnalysis;

namespace Nalu;

/// <summary>
/// Given <see cref="ObservingViewBox" /> measure constraints, returns the constraints to be used when observing the content size.
/// </summary>
/// <example>
/// To constrain the width and observe the height:
/// <code>
/// (widthConstraint, heightConstraint) => new Size(widthConstraint, double.PositiveInfinity)
/// </code>
/// </example>
public delegate Size ObservedConstraintsFactory(double widthConstraint, double heightConstraint);

/// <summary>
/// A <see cref="ViewBox" /> which observes the size of its content.
/// </summary>
[SuppressMessage("Style", "IDE0290:Use primary constructor")]
public class ObservingViewBox : ViewBox
{
    private ObservedConstraintsFactory _observedConstraintsFactory;

    /// <summary>
    /// Initializes a new instance of <see cref="ObservingViewBox" />.
    /// </summary>
    /// <param name="observedConstraintsFactory">
    /// A factory method that creates the constraints to be used when observing the content size based on the provided width and height
    /// constraints.
    /// </param>
    public ObservingViewBox(ObservedConstraintsFactory observedConstraintsFactory)
    {
        _observedConstraintsFactory = observedConstraintsFactory;
    }

    /// <summary>
    /// Gets the desired size of the content.
    /// </summary>
    public Size DesiredContentSize { get; private set; }

    /// <summary>
    /// Notifies that the size of the content has changed.
    /// </summary>
    public event EventHandler? ContentMeasureChanged;

    /// <summary>
    /// A factory method that creates the constraints to be used when observing the content size based on the provided width and height constraints.
    /// </summary>
    protected ObservedConstraintsFactory ObservedConstraintsFactory
    {
        get => _observedConstraintsFactory;
        set
        {
            _observedConstraintsFactory = value;
            InvalidateMeasure();
        }
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(double widthConstraint, double heightConstraint)
    {
        if (Content is { } content)
        {
            MeasureContentSize(widthConstraint, heightConstraint, content);
        }
        else if (DesiredContentSize != Size.Zero)
        {
            DesiredContentSize = Size.Zero;
            InvokeContentMeasureChanged();
        }

        return base.MeasureOverride(widthConstraint, heightConstraint);
    }

    private void MeasureContentSize(double widthConstraint, double heightConstraint, IView content)
    {
        var constraints = ObservedConstraintsFactory(widthConstraint, heightConstraint);
        var size = content.Measure(constraints.Width, constraints.Height);

        if (size != DesiredContentSize)
        {
            DesiredContentSize = size;
            InvokeContentMeasureChanged();
        }
    }

    private void InvokeContentMeasureChanged() =>
        // We cannot call ContentMeasureChanged directly because it will cause layout issues given that
        // the layout process is still running here. Let's wait for next UI thread cycle.
        Dispatcher.Dispatch(() => ContentMeasureChanged?.Invoke(this, EventArgs.Empty));
}
