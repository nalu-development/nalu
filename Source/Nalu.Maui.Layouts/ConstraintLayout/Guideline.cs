namespace Nalu;

using static Cassowary.Strength;
using static Cassowary.WeightedRelation;

/// <summary>
/// Represents a guideline.
/// </summary>
public class Guideline : SceneElementBase<Guideline.ConstraintTypes>
{
#pragma warning disable CS1591,SA1602
    public enum ConstraintTypes
    {
        GuidelinePosition,
        StaticCoordinates,
    }
#pragma warning restore CS1591,SA1602

    /// <summary>
    /// Bindable property for the Percentage property.
    /// </summary>
    public static readonly BindableProperty PercentageProperty = BindableProperty.Create(nameof(Delta), typeof(double), typeof(Guideline), 0d, propertyChanged: AnyPropertyChanged);

    /// <summary>
    /// Bindable property for the Margin property.
    /// </summary>
    public static readonly BindableProperty DeltaProperty = BindableProperty.Create(nameof(Delta), typeof(double), typeof(Guideline), 0d, propertyChanged: AnyPropertyChanged);

    /// <summary>
    /// Bindable property for the Orientation property.
    /// </summary>
    public static readonly BindableProperty OrientationProperty = BindableProperty.Create(nameof(Orientation), typeof(GuidelineOrientation), typeof(Guideline), default(GuidelineOrientation), propertyChanged: OrientationPropertyChanged);

    /// <summary>
    /// Gets or sets the position of the guideline as a percentage of the layout size for the orientation.
    /// </summary>
    /// <remarks>
    /// The value should be between 0 and 1.
    /// </remarks>
    public double Percentage
    {
        get => (double)GetValue(PercentageProperty);
        set => SetValue(PercentageProperty, value);
    }

    /// <summary>
    /// Gets or sets the position of the guideline as a delta from the position calculated by the percentage.
    /// </summary>
    public double Delta
    {
        get => (double)GetValue(DeltaProperty);
        set => SetValue(DeltaProperty, value);
    }

    /// <summary>
    /// Gets or sets the orientation of the guideline.
    /// </summary>
    public GuidelineOrientation Orientation
    {
        get => (GuidelineOrientation)GetValue(OrientationProperty);
        set => SetValue(OrientationProperty, value);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Guideline"/> class.
    /// </summary>
    public Guideline()
    {
        SetConstraintsForOrientation(true);
    }

    private void SetConstraintsForOrientation(bool orientationChanged = false)
    {
        if (Orientation == GuidelineOrientation.Horizontal)
        {
            SetConstraint(ConstraintTypes.GuidelinePosition, scene =>
            {
                var sceneHeight = scene.Bottom - scene.Top;
                return [Top | Eq(Required) | ((sceneHeight * Percentage) + Delta)];
            });

            if (orientationChanged)
            {
                SetConstraint(ConstraintTypes.StaticCoordinates, scene => [
                    Bottom | Eq(Required) | Top,
                    Left | Eq(Required) | scene.Left,
                    Right | Eq(Required) | scene.Right,
                ]);
            }
        }
        else
        {
            SetConstraint(ConstraintTypes.GuidelinePosition, scene =>
            {
                var sceneWidth = scene.Right - scene.Left;
                return [Left | Eq(Required) | ((sceneWidth * Percentage) + Delta)];
            });

            if (orientationChanged)
            {
                SetConstraint(ConstraintTypes.StaticCoordinates, scene => [
                    Right | Eq(Required) | Left,
                    Top | Eq(Required) | scene.Top,
                    Bottom | Eq(Required) | scene.Bottom,
                ]);
            }
        }
    }

    private static void AnyPropertyChanged(BindableObject bindable, object oldvalue, object newvalue)
    {
        if (bindable is Guideline guideline)
        {
            guideline.SetConstraintsForOrientation();
            guideline.Scene?.InvalidateScene();
        }
    }

    private static void OrientationPropertyChanged(BindableObject bindable, object oldvalue, object newvalue)
    {
        if (bindable is Guideline guideline)
        {
            guideline.SetConstraintsForOrientation(true);
            guideline.Scene?.InvalidateScene();
        }
    }
}
