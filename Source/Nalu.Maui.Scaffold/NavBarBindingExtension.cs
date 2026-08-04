using System.Diagnostics.CodeAnalysis;
using Microsoft.Maui.Controls.Xaml;

namespace Nalu;

/// <summary>
/// Binds to the ambient <see cref="ScaffoldNavBarContext"/> from anywhere inside the scaffold's
/// element tree — custom bars, and page-parented content hosted in the bar (e.g. a
/// <see cref="Scaffold.TitleViewProperty"/> view, whose own binding context is the page model):
/// <c>IsVisible="{nalu:NavBarBinding CanNavigateBack}"</c>.
/// </summary>
/// <remarks>
/// Resolves through the nearest ancestor <see cref="Scaffold"/>, so it only binds while the
/// target is attached to the scaffold's tree (mounted chrome always is). The path is evaluated
/// by reflection (not compiled) — the reflected surface is preserved under trimming.
/// </remarks>
[ContentProperty(nameof(Path))]
[AcceptEmptyServiceProvider]
public sealed class NavBarBindingExtension : IMarkupExtension<BindingBase>
{
    /// <summary>Gets or sets the path within the <see cref="ScaffoldNavBarContext"/>.</summary>
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
        => NavBarBindings.Create(Path, Mode, Converter, ConverterParameter, StringFormat);

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
    public static RelativeBindingSource ScaffoldAncestor { get; }
        = new(RelativeBindingSourceMode.FindAncestor, typeof(Scaffold));

    /// <summary>Builds a string-path binding into the ambient <see cref="ScaffoldNavBarContext"/>.</summary>
    /// <param name="path">The path within the context ("." binds the context itself).</param>
    /// <param name="mode">The binding mode.</param>
    /// <param name="converter">The converter.</param>
    /// <param name="converterParameter">The converter parameter.</param>
    /// <param name="stringFormat">The string format.</param>
    // Runtime string-path bindings resolve via reflection; the dependencies keep the reflected
    // property surfaces alive under trimming/AOT (see maui-binding gotcha: unpreserved
    // library-type string bindings silently die in consumer Release builds).
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicProperties, typeof(Scaffold))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicProperties, typeof(ScaffoldNavBarContext))]
    public static BindingBase Create(
        string path = ".",
        BindingMode mode = BindingMode.Default,
        IValueConverter? converter = null,
        object? converterParameter = null,
        string? stringFormat = null)
    {
        var contextPath = nameof(Scaffold.NavBarContext);
        var fullPath = path is "." or "" ? contextPath : $"{contextPath}.{path}";

        return new Binding(fullPath, mode, converter, converterParameter, stringFormat)
        {
            Source = ScaffoldAncestor
        };
    }
}
