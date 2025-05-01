using Microsoft.Maui.Layouts;
using Nalu.MagnetLayout;

namespace Nalu;

/// <summary>
/// A <see cref="Layout" /> that uses the magnet layout system where each element is positioned based on a set of constraints.
/// </summary>
public class Magnet : Layout, IStackLayout
{
    /// <summary>
    /// The bindable property for the <see cref="IMagnetStage" /> property.
    /// </summary>
    public static readonly BindableProperty StageProperty = BindableProperty.Create(
        nameof(Stage),
        typeof(IMagnetStage),
        typeof(Magnet),
        null,
        propertyChanged: OnScenePropertyChanged
    );

    /// <summary>
    /// The bindable property for the StageId attached property.
    /// </summary>
    public static readonly BindableProperty StageIdProperty = BindableProperty.CreateAttached(
        "StageId",
        typeof(string),
        typeof(Magnet),
        null,
        propertyChanged: OnStageIdPropertyChanged
    );

    /// <summary>
    /// Gets the StageId attached property.
    /// </summary>
    /// <param name="view">The view to get the property from.</param>
    /// <returns>The identifier.</returns>
    public static string? GetStageId(BindableObject view) => (string?) view.GetValue(StageIdProperty);

    /// <summary>
    /// Sets the StageId attached property.
    /// </summary>
    /// <param name="view">The view to set the property on.</param>
    /// <param name="value">The identifier.</param>
    public static void SetStageId(BindableObject view, string? value) => view.SetValue(StageIdProperty, value);

    /// <summary>
    /// Gets or sets the <see cref="IMagnetStage" /> for this layout.
    /// </summary>
    public IMagnetStage? Stage
    {
        get => (IMagnetStage?) GetValue(StageProperty);
        set => SetValue(StageProperty, value);
    }

    /// <inheritdoc />
    protected override ILayoutManager CreateLayoutManager() => new MagnetLayoutManager(this);

    double IStackLayout.Spacing => 0;

    /// <summary>
    /// Gets the stage <see cref="IMagnetElementBase.Id" /> for the specified view.
    /// </summary>
    /// <param name="view">The view to get the property from.</param>
    /// <returns>The identifier.</returns>
    public static string? GetStageId(IView view) => GetStageId((BindableObject) view);

    /// <summary>
    /// Sets the stage <see cref="IMagnetElementBase.Id" /> for the specified view.
    /// </summary>
    /// <param name="view">The view to set the property on.</param>
    /// <param name="id">The identifier.</param>
    public static void SetStageId(IView view, string? id) => ((BindableObject) view).SetValue(StageIdProperty, id);

    private static void OnStageIdPropertyChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is IView { Parent: not null })
        {
            throw new InvalidOperationException("The StageId property cannot be changed once the view is added to a parent.");
        }
    }

    private static void OnScenePropertyChanged(BindableObject bindable, object oldValue, object newValue) { }
}
