using Nalu.Cassowary;

namespace Nalu.MagnetLayout;

/// <summary>
/// Represents guideline base class for magnet layout.
/// </summary>
public abstract class GuidelineBase : MagnetElementBase<GuidelineBase.ConstraintTypes>
{
#pragma warning disable CS1591
    public enum ConstraintTypes
    {
        Position,
        Identity,
    }
#pragma warning restore CS1591

    /// <summary>
    /// Bindable property for <see cref="FractionalPosition"/>.
    /// </summary>
    public static readonly BindableProperty FractionalPositionProperty = BindableProperty.Create(
        nameof(FractionalPosition),
        typeof(double),
        typeof(GuidelineBase),
        propertyChanged: OnFractionalPositionChanged);

    /// <summary>
    /// Gets or sets the position of the guideline relative to the <see cref="IMagnetStage"/> bounds.
    /// </summary>
    /// <remarks>
    /// Adds up to the <see cref="Position"/> property.
    /// </remarks>
    public double FractionalPosition
    {
        get => (double)GetValue(FractionalPositionProperty);
        set => SetValue(FractionalPositionProperty, value);
    }

    /// <summary>
    /// Bindable property for <see cref="Position"/>.
    /// </summary>
    public static readonly BindableProperty PositionProperty = BindableProperty.Create(
        nameof(Position),
        typeof(double),
        typeof(GuidelineBase),
        propertyChanged: OnPositionChanged);

    /// <summary>
    /// Gets or sets the position in DP of the guideline.
    /// </summary>
    /// <remarks>
    /// Adds up to the <see cref="FractionalPosition"/> property.
    /// </remarks>
    public double Position
    {
        get => (double)GetValue(PositionProperty);
        set => SetValue(PositionProperty, value);
    }

    /// <summary>
    /// Gets the constraint for the specified position.
    /// </summary>
    protected abstract IEnumerable<Constraint> GetConstraintForPosition(IMagnetStage stage);

    /// <summary>
    /// Gets the constraint to enforce the two poles to be the same.
    /// </summary>
    protected abstract IEnumerable<Constraint> GetIdentityConstraint(IMagnetStage stage);
    
    private static void OnFractionalPositionChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is GuidelineBase guideline)
        {
            guideline.TryAddConstraints(ConstraintTypes.Identity, guideline.GetIdentityConstraint);
            guideline.SetConstraints(ConstraintTypes.Position, guideline.GetConstraintForPosition);
        }
    }

    private static void OnPositionChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is GuidelineBase guideline)
        {
            guideline.TryAddConstraints(ConstraintTypes.Identity, guideline.GetIdentityConstraint);
            guideline.SetConstraints(ConstraintTypes.Position, guideline.GetConstraintForPosition);
        }
    }
}
