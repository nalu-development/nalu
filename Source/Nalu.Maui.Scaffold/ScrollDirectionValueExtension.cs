using System.Diagnostics.CodeAnalysis;
using Microsoft.Maui.Controls.Internals;
using Nalu.Internals;

namespace Nalu;

/// <summary>
/// Shared machinery of <see cref="ScrollDirectionValueExtension"/> and
/// <see cref="ThemeScrollDirectionValueExtension"/>: a two-state value driven by the scroll
/// DIRECTION rather than the scroll position. Scrolling down by <see cref="ActivateThreshold"/> dp
/// latches the <b>activated</b> state, scrolling up by <see cref="DeactivateThreshold"/> dp latches
/// back to <b>deactivated</b> (the initial state), wherever in the content that movement happens —
/// the classic "chrome slips away as you read on, returns the moment you scroll back" pattern.
/// Each state flip animates the target between the two endpoint values over
/// <see cref="ActivateDuration"/>/<see cref="DeactivateDuration"/>. Not derivable outside this
/// library — use <see cref="ScrollDirectionValueExtension"/> or <see cref="ThemeScrollDirectionValueExtension"/>.
/// </summary>
public abstract class ScrollDirectionValueExtensionBase : IMarkupExtension<BindingBase>
{
    /// <summary>
    /// Gets or sets the downward travel (dp) that latches the activated state (default 100).
    /// Travel accumulates while the scroll keeps moving down; any upward movement restarts the count.
    /// Zero latches on the first downward frame.
    /// </summary>
    public double ActivateThreshold { get; set; } = 100;

    /// <summary>
    /// Gets or sets the upward travel (dp) that latches back to deactivated
    /// (defaults to <see cref="ActivateThreshold"/>). Zero latches on the first upward frame.
    /// </summary>
    public double? DeactivateThreshold { get; set; }

    /// <summary>
    /// Gets or sets the scroll offset at or below which the deactivated state is always restored
    /// (default 0 = the content top): resting at the top never leaves the activated mode stuck on.
    /// </summary>
    public double DeactivateBelow { get; set; }

    /// <summary>Gets or sets the deactivated → activated transition duration in milliseconds (default 250; 0 snaps).</summary>
    public uint ActivateDuration { get; set; } = 250;

    /// <summary>Gets or sets the activated → deactivated transition duration in milliseconds (defaults to <see cref="ActivateDuration"/>).</summary>
    public uint? DeactivateDuration { get; set; }

    /// <summary>Gets or sets the time curve of both transitions (null = linear).</summary>
    public Easing? Easing { get; set; }

    private protected abstract (object? DeactivatedLight, object? ActivatedLight, object? DeactivatedDark, object? ActivatedDark) GetEndpoints();

    /// <inheritdoc />
    // Runtime string-path bindings resolve via reflection; the dependencies keep the reflected
    // property surfaces alive under trimming/AOT.
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicProperties, typeof(Scaffold))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicProperties, typeof(ScaffoldNavBarContext))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicProperties, typeof(ScrollValueThemeListener))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicProperties, typeof(ScrollDirectionAnimator))]
    public BindingBase ProvideValue(IServiceProvider serviceProvider)
    {
        var provideValueTarget = (IProvideValueTarget?)serviceProvider.GetService(typeof(IProvideValueTarget));

        if (provideValueTarget?.TargetProperty is not BindableProperty targetProperty)
        {
            throw new InvalidOperationException(
                $"{GetType().Name} must be used directly on a bindable property (styles/setters are not supported).");
        }

        var kind = ScrollValueMath.KindFor(targetProperty.ReturnType)
            ?? throw new InvalidOperationException(
                $"{GetType().Name} cannot target '{targetProperty.PropertyName}' ({targetProperty.ReturnType.Name}): only numeric, Color and solid Brush properties are supported.");

        if (provideValueTarget.TargetObject is not Element target)
        {
            throw new InvalidOperationException(
                $"{GetType().Name} must be used directly on an element's bindable property (styles/setters are not supported).");
        }

        var (deactivatedLight, activatedLight, deactivatedDark, activatedDark) = GetEndpoints();
        var animator = new ScrollDirectionAnimator();

        var converter = new ScrollDirectionInterpolationConverter
        {
            Kind = kind,
            ActivateThreshold = ActivateThreshold,
            DeactivateThreshold = DeactivateThreshold,
            DeactivateBelow = DeactivateBelow,
            ActivateDuration = ActivateDuration,
            DeactivateDuration = DeactivateDuration,
            Easing = Easing,
            Animator = animator,
            DeactivatedLight = deactivatedLight,
            ActivatedLight = activatedLight,
            DeactivatedDark = deactivatedDark,
            ActivatedDark = activatedDark
        };

        var multiBinding = new MultiBinding { Converter = converter, Mode = BindingMode.OneWay };

        // The scroll channel is PER PAGE: resolve the context of the page this element belongs to,
        // so the direction is read from this page's own offset even during transitions.
        multiBinding.Bindings.Add(NavBarContextBindings.Create(target, nameof(ScaffoldNavBarContext.ScrollOffset)));
        multiBinding.Bindings.Add(ScrollValueThemeListener.CreateBinding());

        // The time leg: each animator tick re-fires the multi-binding, pulling the converter again —
        // the converter reads the progress straight off the animator.
        multiBinding.Bindings.Add(CreateProgressTypedBinding(animator));

        return multiBinding;
    }

    private static TypedBinding<ScrollDirectionAnimator, double> CreateProgressTypedBinding(ScrollDirectionAnimator animator)
        => new(
               a => (a.Progress, true),
               null,
               [Tuple.Create<Func<ScrollDirectionAnimator, object>, string>(o => o, nameof(ScrollDirectionAnimator.Progress))]
           )
           {
               Source = animator
           };

    object IMarkupExtension.ProvideValue(IServiceProvider serviceProvider) => ProvideValue(serviceProvider);
}

/// <summary>
/// Switches the target property between two values from the scroll DIRECTION: scrolling down
/// <see cref="ScrollDirectionValueExtensionBase.ActivateThreshold"/> dp animates it from
/// <see cref="Deactivated"/> to <see cref="Activated"/>, scrolling back up
/// <see cref="ScrollDirectionValueExtensionBase.DeactivateThreshold"/> dp animates it back —
/// wherever in the content that happens (unlike <see cref="ScrollValueExtension"/>, which maps the
/// absolute offset). Starts deactivated, and the top of the content always restores deactivated.
/// Works on numeric, <see cref="Color"/> and solid <see cref="Brush"/> properties, on any element
/// inside the scaffold's tree:
/// <c>TranslationY="{nalu:ScrollDirectionValue Deactivated=0, Activated=80, ActivateThreshold=48, DeactivateThreshold=24}"</c>.
/// Must be applied directly on the target's bindable property — Style setters are not supported.
/// For theme-dependent endpoints use <see cref="ThemeScrollDirectionValueExtension"/>.
/// </summary>
[RequireService([typeof(IProvideValueTarget)])]
public sealed class ScrollDirectionValueExtension : ScrollDirectionValueExtensionBase
{
    /// <summary>Gets or sets the value of the deactivated state (the initial one).</summary>
    public object? Deactivated { get; set; }

    /// <summary>Gets or sets the value of the activated state.</summary>
    public object? Activated { get; set; }

    private protected override (object?, object?, object?, object?) GetEndpoints() => (Deactivated, Activated, null, null);
}

/// <summary>
/// The theme-aware <see cref="ScrollDirectionValueExtension"/>: endpoints are declared per app
/// theme and a theme change re-evaluates the value immediately —
/// <c>Background="{nalu:ThemeScrollDirectionValue DeactivatedLight=White, ActivatedLight=Transparent, DeactivatedDark=Black}"</c>
/// (dark endpoints fall back to the light ones when omitted).
/// </summary>
[RequireService([typeof(IProvideValueTarget)])]
public sealed class ThemeScrollDirectionValueExtension : ScrollDirectionValueExtensionBase
{
    /// <summary>Gets or sets the light-theme value of the deactivated state (the initial one).</summary>
    public object? DeactivatedLight { get; set; }

    /// <summary>Gets or sets the light-theme value of the activated state.</summary>
    public object? ActivatedLight { get; set; }

    /// <summary>Gets or sets the dark-theme value of the deactivated state (falls back to <see cref="DeactivatedLight"/>).</summary>
    public object? DeactivatedDark { get; set; }

    /// <summary>Gets or sets the dark-theme value of the activated state (falls back to <see cref="ActivatedLight"/>).</summary>
    public object? ActivatedDark { get; set; }

    private protected override (object?, object?, object?, object?) GetEndpoints() => (DeactivatedLight, ActivatedLight, DeactivatedDark, ActivatedDark);
}
