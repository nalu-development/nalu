using System.ComponentModel;
using Nalu.Internals;

namespace Nalu.MagnetLayout;

/// <summary>
/// Represents guideline base class for magnet layout.
/// </summary>
public abstract class BarrierBase : MagnetElementBase<BarrierBase.ConstraintTypes>
{
#pragma warning disable CS1591
    public enum ConstraintTypes
    {
        Position,
        Identity
    }
#pragma warning restore CS1591

    /// <summary>
    /// Bindable property for <see cref="Elements" />.
    /// </summary>
    public static readonly BindableProperty ElementsProperty = GenericBindableProperty<BarrierBase>.Create<string[]?>(
        nameof(Elements),
        propertyChanged: barrier => barrier.OnElementsChanged
    );

    /// <summary>
    /// Gets or sets the elements that this barrier is constrained to.
    /// </summary>
    [TypeConverter(typeof(ElementIdsConverter))]
    public string[]? Elements
    {
        get => (string[]?) GetValue(ElementsProperty);
        set => SetValue(ElementsProperty, value);
    }

    /// <summary>
    /// Bindable property for <see cref="Margin" />.
    /// </summary>
    public static readonly BindableProperty MarginProperty = GenericBindableProperty<BarrierBase>.Create<double>(
        nameof(Margin),
        propertyChanged: barrier => barrier.OnMarginChanged
    );

    /// <summary>
    /// Gets or sets a maring used to Margin the barrier.
    /// </summary>
    /// <remarks>
    /// Adds up to the barrier Margin property.
    /// </remarks>
    public double Margin
    {
        get => (double) GetValue(MarginProperty);
        set => SetValue(MarginProperty, value);
    }

    /// <summary>
    /// Invoked when the <see cref="Elements" /> property changes.
    /// </summary>
    protected abstract void OnElementsChanged(string[]? oldValue, string[]? newValue);

    /// <summary>
    /// Invoked when the <see cref="Margin" /> property changes.
    /// </summary>
    protected abstract void OnMarginChanged(double oldValue, double newValue);
}
