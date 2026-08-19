using System.Diagnostics.CodeAnalysis;
using Microsoft.Maui.Controls.Internals;
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
    /// Numeric targets only: <see cref="Color"/>/<see cref="Brush"/> endpoints always clamp.
    /// </summary>
    Extend
}

/// <summary>
/// Shared machinery of <see cref="ScrollValueExtension"/> and
/// <see cref="ThemeScrollValueExtension"/>: builds the multi-binding over the ambient
/// scroll channel (<see cref="ScaffoldNavBarContext.ScrollOffset"/> plus the page-level
/// <see cref="Scaffold.ScrollRampStartProperty"/>/<see cref="Scaffold.ScrollRampEndProperty"/> ramp
/// defaults and the app theme), targeting elements in the scaffold's tree and
/// <see cref="ScaffoldNavBarAppearance"/> objects alike. Not derivable outside this library —
/// use <see cref="ScrollValueExtension"/> or <see cref="ThemeScrollValueExtension"/>.
/// </summary>
public abstract class ScrollValueExtensionBase : IMarkupExtension<BindingBase>
{
    /// <summary>Gets or sets the scroll offset where interpolation starts (defaults to the page-level <see cref="Scaffold.ScrollRampStartProperty"/>).</summary>
    public double? RampStart { get; set; }

    /// <summary>
    /// Gets or sets the scroll offset where interpolation ends (defaults to the page-level
    /// <see cref="Scaffold.ScrollRampEndProperty"/>); equal to <see cref="RampStart"/> makes
    /// the value step at that offset.
    /// </summary>
    public double? RampEnd { get; set; }

    /// <summary>Gets or sets the behavior outside the [RampStart, RampEnd] window.</summary>
    public ScrollValueExtrapolation Extrapolate { get; set; }

    /// <summary>Gets or sets the easing applied inside [RampStart, RampEnd]; extrapolated values stay linear.</summary>
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
            multiBinding.Bindings.Add(CreateNavBarContextTypedBinding(ctx => ctx.ScrollOffset, nameof(ScaffoldNavBarContext.ScrollOffset), appearance));
            multiBinding.Bindings.Add(CreateNavBarContextTypedBinding(ctx => ctx.ScrollRampStart, nameof(ScaffoldNavBarContext.ScrollRampStart), appearance));
            multiBinding.Bindings.Add(CreateNavBarContextTypedBinding(ctx => ctx.ScrollRampEnd, nameof(ScaffoldNavBarContext.ScrollRampEnd), appearance));
        }
        else if (provideValueTarget.TargetObject is Element target)
        {
            // The scroll channel is PER PAGE: resolve the context of the page this element
            // belongs to, so a page's parallax reads its own offset even while another page is
            // presented over (or under) it during a transition.
            multiBinding.Bindings.Add(NavBarContextBindings.Create(target, nameof(ScaffoldNavBarContext.ScrollOffset)));
            multiBinding.Bindings.Add(NavBarContextBindings.Create(target, nameof(ScaffoldNavBarContext.ScrollRampStart)));
            multiBinding.Bindings.Add(NavBarContextBindings.Create(target, nameof(ScaffoldNavBarContext.ScrollRampEnd)));
        }
        else
        {
            throw new InvalidOperationException(
                $"{GetType().Name} must target an element or a {nameof(ScaffoldNavBarAppearance)} (styles/setters are not supported).");
        }

        multiBinding.Bindings.Add(CreateThemeTypedBinding());

        return multiBinding;
    }

    private static TypedBinding<ScrollValueThemeListener, AppTheme> CreateThemeTypedBinding()
        => new(
               tl => (tl.Theme, true),
               null,
               [Tuple.Create<Func<ScrollValueThemeListener, object>, string>(o => o, nameof(ScrollValueThemeListener.Theme))]
           )
           {
               Source = ScrollValueThemeListener.Instance
           };

    private static TypedBinding<ScaffoldNavBarAppearance, TProperty> CreateNavBarContextTypedBinding<TProperty>(
        Func<ScaffoldNavBarContext, TProperty> propertyGetter,
        string propertyName,
        ScaffoldNavBarAppearance source
    )
        => new(
               a => a.Context is { } context ? (propertyGetter(context), true) : (default!, false),
               null,
               [
                   Tuple.Create<Func<ScaffoldNavBarAppearance, object>, string>(o => o, nameof(ScaffoldNavBarAppearance.Context)),
                   Tuple.Create<Func<ScaffoldNavBarAppearance, object>, string>(o => o.Context!, propertyName)
               ]
           )
           {
               Source = source
           };
    

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
/// <c>Opacity="{nalu:ScrollValue From=0, To=1}"</c>. Must be applied directly on the target's
/// bindable property — Style setters are not supported. For theme-dependent endpoints use
/// <see cref="ThemeScrollValueExtension"/>.
/// </summary>
[RequireService([typeof(IProvideValueTarget)])]
public sealed class ScrollValueExtension : ScrollValueExtensionBase
{
    /// <summary>Gets or sets the value at <c>RampStart</c> (held below it unless <see cref="ScrollValueExtrapolation.Extend"/>).</summary>
    public object? From { get; set; }

    /// <summary>Gets or sets the value at <c>RampEnd</c> (held above it unless <see cref="ScrollValueExtrapolation.Extend"/>).</summary>
    public object? To { get; set; }

    private protected override (object?, object?, object?, object?) GetEndpoints() => (From, To, null, null);
}

/// <summary>
/// The theme-aware <see cref="ScrollValueExtension"/>: endpoints are declared per app theme
/// and a theme change re-evaluates the value immediately —
/// <c>Background="{nalu:ThemeScrollValue FromLight=Transparent, ToLight=White, ToDark=Black}"</c>
/// (dark endpoints fall back to the light ones when omitted).
/// </summary>
[RequireService([typeof(IProvideValueTarget)])]
public sealed class ThemeScrollValueExtension : ScrollValueExtensionBase
{
    /// <summary>Gets or sets the light-theme value at <c>RampStart</c> (held below it unless <see cref="ScrollValueExtrapolation.Extend"/>).</summary>
    public object? FromLight { get; set; }

    /// <summary>Gets or sets the light-theme value at <c>RampEnd</c> (held above it unless <see cref="ScrollValueExtrapolation.Extend"/>).</summary>
    public object? ToLight { get; set; }

    /// <summary>Gets or sets the dark-theme value at <c>RampStart</c> (falls back to <see cref="FromLight"/>).</summary>
    public object? FromDark { get; set; }

    /// <summary>Gets or sets the dark-theme value at <c>RampEnd</c> (falls back to <see cref="ToLight"/>).</summary>
    public object? ToDark { get; set; }

    private protected override (object?, object?, object?, object?) GetEndpoints() => (FromLight, ToLight, FromDark, ToDark);
}
