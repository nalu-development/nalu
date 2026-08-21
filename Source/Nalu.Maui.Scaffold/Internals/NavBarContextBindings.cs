using System.Diagnostics.CodeAnalysis;
using Microsoft.Maui.Controls.Internals;

namespace Nalu.Internals;

/// <summary>
/// Builds the bindings that read a page's <see cref="ScaffoldNavBarContext"/> through a
/// <see cref="NavBarContextRelay"/> — THE single factory every public entry point routes
/// through (<see cref="NavBarBindingExtension"/>, <see cref="NavBarBindings"/>,
/// <see cref="ScrollValueExtensionBase"/>).
/// </summary>
/// <remarks>
/// The context has a small, closed set of properties, so a single-segment path compiles to an
/// ad-hoc <see cref="TypedBinding{TSource,TProperty}"/>: no reflection, trimming/AOT-safe (the
/// failure mode this library has been bitten by — a string binding over library types silently
/// dying in a consumer's Release build), and a renamed property breaks the build instead of
/// returning null. Anything the switch does not know — notably the documented
/// <c>PageBindingContext.SomeCommand</c> escape hatch — falls back to a reflection
/// <see cref="Binding"/> over the same relay, which keeps working precisely because the relay is
/// a real source object.
/// </remarks>
internal static class NavBarContextBindings
{
    /// <summary>
    /// A binding to <paramref name="path"/> within the context of the page
    /// <paramref name="target"/> belongs to.
    /// </summary>
    public static BindingBase Create(
        Element target,
        string path,
        BindingMode mode = BindingMode.Default,
        IValueConverter? converter = null,
        object? converterParameter = null,
        string? stringFormat = null)
    {
        var relay = new NavBarContextRelay();
        relay.Attach(target);

        if (CreateTyped(relay, path) is { } typed)
        {
            typed.Mode = mode;
            typed.Converter = converter;
            typed.ConverterParameter = converterParameter;
            typed.StringFormat = stringFormat;

            return typed;
        }

        return CreateReflected(relay, path, mode, converter, converterParameter, stringFormat);
    }

    /// <summary>The reflection fallback: deeper paths, and paths added to the context by a newer version.</summary>
    // The dependencies keep the reflected property surfaces alive under trimming/AOT for the
    // fallback path; the typed switch above needs none of them.
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicProperties, typeof(NavBarContextRelay))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicProperties, typeof(ScaffoldNavBarContext))]
    private static BindingBase CreateReflected(
        NavBarContextRelay relay,
        string path,
        BindingMode mode,
        IValueConverter? converter,
        object? converterParameter,
        string? stringFormat)
    {
        var contextPath = nameof(NavBarContextRelay.Context);
        var fullPath = path is "." or "" ? contextPath : $"{contextPath}.{path}";

        return new Binding(fullPath, mode, converter, converterParameter, stringFormat) { Source = relay };
    }

    /// <summary>
    /// Typed bindings for the context's own properties. Every public property of
    /// <see cref="ScaffoldNavBarContext"/> must appear here — <c>NavBarContextBindingsTests</c>
    /// asserts the coverage by reflection, so adding a property without a case fails the build's
    /// test run rather than silently demoting it to the reflection path.
    /// </summary>
    private static TypedBindingBase? CreateTyped(NavBarContextRelay relay, string path)
        => path switch
        {
            // The context itself: only the relay hop is observable, there is no inner property.
            "" or "." => new TypedBinding<NavBarContextRelay, ScaffoldNavBarContext?>(
                r => (r.Context, true),
                null,
                [Tuple.Create<Func<NavBarContextRelay, object>, string>(o => o, nameof(NavBarContextRelay.Context))]
            ) { Source = relay },
            nameof(ScaffoldNavBarContext.Title) => Typed(relay, path, c => c.Title),
            nameof(ScaffoldNavBarContext.TitleView) => Typed(relay, path, c => c.TitleView),
            nameof(ScaffoldNavBarContext.PageBindingContext) => Typed(relay, path, c => c.PageBindingContext),
            nameof(ScaffoldNavBarContext.Foreground) => Typed(relay, path, c => c.Foreground),
            nameof(ScaffoldNavBarContext.TitleForeground) => Typed(relay, path, c => c.TitleForeground),
            nameof(ScaffoldNavBarContext.ScrollOffset) => Typed(relay, path, c => c.ScrollOffset),
            nameof(ScaffoldNavBarContext.IsScrolledUnder) => Typed(relay, path, c => c.IsScrolledUnder),
            nameof(ScaffoldNavBarContext.ScrollRampStart) => Typed(relay, path, c => c.ScrollRampStart),
            nameof(ScaffoldNavBarContext.ScrollRampEnd) => Typed(relay, path, c => c.ScrollRampEnd),
            nameof(ScaffoldNavBarContext.CanNavigateBack) => Typed(relay, path, c => c.CanNavigateBack),
            nameof(ScaffoldNavBarContext.IsFlyoutStartButtonVisible) => Typed(relay, path, c => c.IsFlyoutStartButtonVisible),
            nameof(ScaffoldNavBarContext.IsFlyoutEndButtonVisible) => Typed(relay, path, c => c.IsFlyoutEndButtonVisible),
            nameof(ScaffoldNavBarContext.IsModal) => Typed(relay, path, c => c.IsModal),
            nameof(ScaffoldNavBarContext.IsCloseButtonVisible) => Typed(relay, path, c => c.IsCloseButtonVisible),
            nameof(ScaffoldNavBarContext.BackCommand) => Typed(relay, path, c => c.BackCommand),
            nameof(ScaffoldNavBarContext.OpenFlyoutStartCommand) => Typed(relay, path, c => c.OpenFlyoutStartCommand),
            nameof(ScaffoldNavBarContext.OpenFlyoutEndCommand) => Typed(relay, path, c => c.OpenFlyoutEndCommand),
            _ => null
        };

    /// <summary>
    /// One typed binding over the relay. The handler chain is (relay → Context) then
    /// (context → the property), so the value re-evaluates both when the walk resolves a
    /// different page's context and when that context's property changes.
    /// </summary>
    private static TypedBinding<NavBarContextRelay, TProperty> Typed<TProperty>(
        NavBarContextRelay relay,
        string propertyName,
        Func<ScaffoldNavBarContext, TProperty> getter)
        => new(
               r => r.Context is { } context ? (getter(context), true) : (default!, false),
               null,
               [
                   Tuple.Create<Func<NavBarContextRelay, object>, string>(o => o, nameof(NavBarContextRelay.Context)),
                   Tuple.Create<Func<NavBarContextRelay, object>, string>(o => o.Context!, propertyName)
               ]
           )
           {
               Source = relay
           };

    /// <summary>Every context property the typed switch covers (the coverage test reads this).</summary>
    internal static bool CoversTypedPath(string path) => CreateTyped(new NavBarContextRelay(), path) is not null;
}
