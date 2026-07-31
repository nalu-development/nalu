using System.Diagnostics.CodeAnalysis;
using Microsoft.Maui.Controls.Xaml;
using Nalu.Internals;

namespace Nalu;

/// <summary>How a scroll-value interpolation behaves outside its [RampStart, RampEnd] window.</summary>
public enum ScrollValueExtrapolation
{
    /// <summary>The value holds its endpoint outside the window (the default).</summary>
    Clamp,

    /// <summary>
    /// The value keeps extrapolating linearly — a range mapping becomes a speed factor
    /// (e.g. parallax: <c>RampStart=0, RampEnd=100, From=0, To=50</c> = half-speed translation).
    /// </summary>
    Extend
}

/// <summary>
/// Shared machinery of <see cref="ScrollValueExtension"/> and
/// <see cref="ThemeScrollValueExtension"/>: builds the multi-binding over the ambient
/// scroll channel (<see cref="ScaffoldNavBarContext.ScrollOffset"/> plus the page-level
/// <see cref="Scaffold.ScrollRampStartProperty"/>/<see cref="Scaffold.ScrollRampEndProperty"/> ramp
/// defaults and the app theme), targeting elements in the scaffold's tree and
/// <see cref="ScaffoldNavBarAppearance"/> objects alike.
/// </summary>
public abstract class ScrollValueExtensionBase : IMarkupExtension<BindingBase>
{
    /// <summary>Gets or sets the scroll offset where interpolation starts (defaults to the page-level <see cref="Scaffold.ScrollRampStartProperty"/>).</summary>
    public double? RampStart { get; set; }

    /// <summary>Gets or sets the scroll offset where interpolation ends (defaults to the page-level <see cref="Scaffold.ScrollRampEndProperty"/>).</summary>
    public double? RampEnd { get; set; }

    /// <summary>Gets or sets the behavior outside the [RampStart, RampEnd] window.</summary>
    public ScrollValueExtrapolation Extrapolate { get; set; }

    /// <summary>Gets or sets the easing shaping the ramp interior.</summary>
    public Easing? Easing { get; set; }

    private protected abstract (object? FromLight, object? ToLight, object? FromDark, object? ToDark) GetEndpoints();

    /// <inheritdoc />
    // Runtime string-path bindings resolve via reflection; the dependencies keep the reflected
    // property surfaces alive under trimming/AOT.
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicProperties, typeof(Scaffold))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicProperties, typeof(ScaffoldNavBarContext))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicProperties, typeof(ScaffoldNavBarAppearance))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicProperties, typeof(ScrollValueThemeListener))]
    public BindingBase ProvideValue(IServiceProvider serviceProvider)
    {
        var provideValueTarget = (IProvideValueTarget?)serviceProvider.GetService(typeof(IProvideValueTarget));

        if (provideValueTarget?.TargetProperty is not BindableProperty targetProperty)
        {
            throw new InvalidOperationException(
                $"{GetType().Name} must be used directly on a bindable property (styles/setters are not supported).");
        }

        var kind = ScrollInterpolationConverter.KindFor(targetProperty.ReturnType)
            ?? throw new InvalidOperationException(
                $"{GetType().Name} cannot target '{targetProperty.PropertyName}' ({targetProperty.ReturnType.Name}): only numeric, Color and solid Brush properties are supported.");

        var (fromLight, toLight, fromDark, toDark) = GetEndpoints();

        var converter = new ScrollInterpolationConverter
        {
            Kind = kind,
            RampStart = RampStart,
            RampEnd = RampEnd,
            FromLight = fromLight,
            ToLight = toLight,
            FromDark = fromDark,
            ToDark = toDark,
            Extrapolation = Extrapolate,
            Easing = Easing
        };

        // Appearance objects live outside the visual tree: they carry the ambient context as a
        // stamped property instead of resolving it through ancestors.
        var multiBinding = new MultiBinding { Converter = converter, Mode = BindingMode.OneWay };

        if (provideValueTarget.TargetObject is ScaffoldNavBarAppearance appearance)
        {
            multiBinding.Bindings.Add(new Binding($"{nameof(ScaffoldNavBarAppearance.Context)}.{nameof(ScaffoldNavBarContext.ScrollOffset)}", source: appearance));
            multiBinding.Bindings.Add(new Binding($"{nameof(ScaffoldNavBarAppearance.Context)}.{nameof(ScaffoldNavBarContext.ScrollRampStart)}", source: appearance));
            multiBinding.Bindings.Add(new Binding($"{nameof(ScaffoldNavBarAppearance.Context)}.{nameof(ScaffoldNavBarContext.ScrollRampEnd)}", source: appearance));
        }
        else
        {
            var scaffoldAncestor = new RelativeBindingSource(RelativeBindingSourceMode.FindAncestor, typeof(Scaffold));
            multiBinding.Bindings.Add(new Binding($"{nameof(Scaffold.NavBarContext)}.{nameof(ScaffoldNavBarContext.ScrollOffset)}") { Source = scaffoldAncestor });
            multiBinding.Bindings.Add(new Binding($"{nameof(Scaffold.NavBarContext)}.{nameof(ScaffoldNavBarContext.ScrollRampStart)}") { Source = scaffoldAncestor });
            multiBinding.Bindings.Add(new Binding($"{nameof(Scaffold.NavBarContext)}.{nameof(ScaffoldNavBarContext.ScrollRampEnd)}") { Source = scaffoldAncestor });
        }

        multiBinding.Bindings.Add(new Binding(nameof(ScrollValueThemeListener.Theme), source: ScrollValueThemeListener.Instance));

        return multiBinding;
    }

    object IMarkupExtension.ProvideValue(IServiceProvider serviceProvider) => ProvideValue(serviceProvider);
}

/// <summary>
/// Interpolates the target property from the page's scroll position: the value moves from
/// <see cref="From"/> to <see cref="To"/> as <see cref="ScaffoldNavBarContext.ScrollOffset"/>
/// crosses the [<see cref="ScrollValueExtensionBase.RampStart"/>,
/// <see cref="ScrollValueExtensionBase.RampEnd"/>] window (defaulting to the page-level
/// <see cref="Scaffold.ScrollRampStartProperty"/>/<see cref="Scaffold.ScrollRampEndProperty"/> ramp).
/// Works on numeric, <see cref="Color"/> and solid <see cref="Brush"/> properties, on any
/// element inside the scaffold's tree and on <see cref="ScaffoldNavBarAppearance"/>:
/// <c>Opacity="{nalu:ScrollValue From=0, To=1}"</c>. For theme-dependent endpoints use
/// <see cref="ThemeScrollValueExtension"/>.
/// </summary>
public sealed class ScrollValueExtension : ScrollValueExtensionBase
{
    /// <summary>Gets or sets the value at (and below) <c>RampStart</c>.</summary>
    public object? From { get; set; }

    /// <summary>Gets or sets the value at (and above) <c>RampEnd</c>.</summary>
    public object? To { get; set; }

    private protected override (object?, object?, object?, object?) GetEndpoints() => (From, To, null, null);
}

/// <summary>
/// The theme-aware <see cref="ScrollValueExtension"/>: endpoints are declared per app theme
/// and a theme change re-evaluates the value immediately —
/// <c>Background="{nalu:ThemeScrollValue FromLight=Transparent, ToLight=White, ToDark=Black}"</c>
/// (dark endpoints fall back to the light ones when omitted).
/// </summary>
public sealed class ThemeScrollValueExtension : ScrollValueExtensionBase
{
    /// <summary>Gets or sets the light-theme value at (and below) <c>RampStart</c>.</summary>
    public object? FromLight { get; set; }

    /// <summary>Gets or sets the light-theme value at (and above) <c>RampEnd</c>.</summary>
    public object? ToLight { get; set; }

    /// <summary>Gets or sets the dark-theme value at (and below) <c>RampStart</c> (falls back to <see cref="FromLight"/>).</summary>
    public object? FromDark { get; set; }

    /// <summary>Gets or sets the dark-theme value at (and above) <c>RampEnd</c> (falls back to <see cref="ToLight"/>).</summary>
    public object? ToDark { get; set; }

    private protected override (object?, object?, object?, object?) GetEndpoints() => (FromLight, ToLight, FromDark, ToDark);
}
