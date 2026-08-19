using System.Diagnostics.CodeAnalysis;
using Nalu.Internals;

namespace Nalu;

/// <summary>
/// Binds to the ambient <see cref="ScaffoldNavBarContext"/> from anywhere inside the scaffold's
/// element tree — custom bars, and page-parented content hosted in the bar (e.g. a
/// <see cref="Scaffold.TitleViewProperty"/> view, whose own binding context is the page model):
/// <c>IsVisible="{nalu:NavBarBinding CanNavigateBack}"</c>.
/// </summary>
/// <remarks>
/// Resolves the context of the page the target belongs to — page content through its page, bar
/// content (and a hosted title view) through the bar it is mounted in — so during a transition
/// each of the two live pages reads its OWN state. Resolution happens when the target is
/// parented, so declaring the binding before the element is in the tree is fine.
/// Paths naming a <see cref="ScaffoldNavBarContext"/> property compile to a typed binding;
/// deeper paths (e.g. <c>CurrentPage.BindingContext.SomeCommand</c>) are evaluated by
/// reflection, with the reflected surface preserved under trimming.
/// </remarks>
[ContentProperty(nameof(Path))]
[RequireService([typeof(IProvideValueTarget)])]
public sealed class NavBarBindingExtension : IMarkupExtension<BindingBase>
{
    /// <summary>Gets or sets the path within the <see cref="ScaffoldNavBarContext"/> ('.', the default, binds the context itself).</summary>
    public string Path { get; set; } = ".";

    /// <summary>Gets or sets the binding mode.</summary>
    public BindingMode Mode { get; set; } = BindingMode.Default;

    /// <summary>Gets or sets the converter.</summary>
    public IValueConverter? Converter { get; set; }

    /// <summary>Gets or sets the converter parameter.</summary>
    public object? ConverterParameter { get; set; }

    /// <summary>Gets or sets the string format.</summary>
    public string? StringFormat { get; set; }

    /// <inheritdoc />
    public BindingBase ProvideValue(IServiceProvider serviceProvider)
    {
        // The target element is what the context is resolved FROM; without one (a Style setter,
        // a non-element target) there is no page to resolve, and the binding falls back to
        // whatever the scaffold currently presents.
        var target = (serviceProvider.GetService(typeof(IProvideValueTarget)) as IProvideValueTarget)?.TargetObject as Element;

        return target is not null
            ? NavBarContextBindings.Create(target, Path, Mode, Converter, ConverterParameter, StringFormat)
            : NavBarBindings.CreateForCurrentPage(Path, Mode, Converter, ConverterParameter, StringFormat);
    }

    object IMarkupExtension.ProvideValue(IServiceProvider serviceProvider) => ProvideValue(serviceProvider);
}

/// <summary>
/// The code-behind counterpart of <see cref="NavBarBindingExtension"/>: builds bindings to the
/// ambient <see cref="ScaffoldNavBarContext"/> from anywhere inside the scaffold's element tree.
/// </summary>
/// <remarks>
/// Two flavors:
/// <code>
/// // String path (same reflection semantics as {nalu:NavBarBinding}):
/// label.SetBinding(Label.TextProperty, NavBarBindings.Create("Title"));
///
/// // Fully typed and compiled (trimming/AOT-safe — the interceptor rewrites YOUR call site):
/// label.SetBinding(Label.TextProperty,
///     static (Scaffold s) => s.NavBarContext.Title,
///     source: NavBarBindings.ScaffoldAncestor);
/// </code>
/// </remarks>
public static class NavBarBindings
{
    /// <summary>
    /// The relative source resolving the nearest ancestor <see cref="Scaffold"/> — combine it
    /// with the typed <c>SetBinding(property, static (Scaffold s) =&gt; s.NavBarContext.…,
    /// source: NavBarBindings.ScaffoldAncestor)</c> for fully typed, compiled context bindings.
    /// </summary>
    /// <remarks>
    /// This resolves the scaffold, whose <see cref="Scaffold.NavBarContext"/> is the CURRENT
    /// page's — not the context of the page the bound element belongs to. Prefer
    /// <see cref="Create(Element,string,BindingMode,IValueConverter?,object?,string?)"/>, which
    /// resolves the element's own page and is therefore correct while two pages are on screen.
    /// </remarks>
    [Obsolete("Resolves the CURRENT page's context, not the bound element's own page. Use NavBarBindings.Create(target, path, ...) instead.")]
    public static RelativeBindingSource ScaffoldAncestor { get; }
        = new(RelativeBindingSourceMode.FindAncestor, typeof(Scaffold));

    /// <summary>
    /// Builds a binding into the <see cref="ScaffoldNavBarContext"/> of the page
    /// <paramref name="target"/> belongs to — page content through its page, bar content (and a
    /// hosted title view) through the bar it is mounted in. Correct while two pages are on
    /// screen during a transition.
    /// </summary>
    /// <param name="target">The element the binding will be applied to.</param>
    /// <param name="path">The path within the context ("." binds the context itself).</param>
    /// <param name="mode">The binding mode.</param>
    /// <param name="converter">The converter.</param>
    /// <param name="converterParameter">The converter parameter.</param>
    /// <param name="stringFormat">The string format.</param>
    /// <remarks>
    /// The target need not be parented yet: the context resolves when it enters the tree, and
    /// re-resolves if it is moved. Paths naming a context property compile to a typed binding;
    /// deeper paths are evaluated by reflection.
    /// </remarks>
    public static BindingBase Create(
        Element target,
        string path = ".",
        BindingMode mode = BindingMode.Default,
        IValueConverter? converter = null,
        object? converterParameter = null,
        string? stringFormat = null)
        => NavBarContextBindings.Create(target, path, mode, converter, converterParameter, stringFormat);

    /// <summary>Builds a string-path binding into the CURRENT page's <see cref="ScaffoldNavBarContext"/>.</summary>
    /// <param name="path">The path within the context ("." binds the context itself).</param>
    /// <param name="mode">The binding mode.</param>
    /// <param name="converter">The converter.</param>
    /// <param name="converterParameter">The converter parameter.</param>
    /// <param name="stringFormat">The string format.</param>
    [Obsolete("Resolves the CURRENT page's context, which is wrong while two pages are on screen. Use Create(target, path, ...).")]
    public static BindingBase Create(
        string path = ".",
        BindingMode mode = BindingMode.Default,
        IValueConverter? converter = null,
        object? converterParameter = null,
        string? stringFormat = null)
        => CreateForCurrentPage(path, mode, converter, converterParameter, stringFormat);

    /// <summary>
    /// The current-page fallback: used when no target element is available to resolve a page
    /// from (a Style setter, a non-element target, or the obsolete target-less
    /// <see cref="Create(string,BindingMode,IValueConverter?,object?,string?)"/>).
    /// </summary>
    // Runtime string-path bindings resolve via reflection; the dependencies keep the reflected
    // property surfaces alive under trimming/AOT (see maui-binding gotcha: unpreserved
    // library-type string bindings silently die in consumer Release builds).
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicProperties, typeof(Scaffold))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicProperties, typeof(ScaffoldNavBarContext))]
    internal static BindingBase CreateForCurrentPage(
        string path,
        BindingMode mode,
        IValueConverter? converter,
        object? converterParameter,
        string? stringFormat)
    {
        var contextPath = nameof(Scaffold.NavBarContext);
        var fullPath = path is "." or "" ? contextPath : $"{contextPath}.{path}";

#pragma warning disable CS0618 // the fallback is exactly what ScaffoldAncestor still provides
        return new Binding(fullPath, mode, converter, converterParameter, stringFormat)
        {
            Source = ScaffoldAncestor
        };
#pragma warning restore CS0618
    }
}
