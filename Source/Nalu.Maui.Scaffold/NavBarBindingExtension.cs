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
    // Runtime string-path bindings resolve via reflection; the dependencies keep the reflected
    // property surfaces alive under trimming/AOT (see maui-binding gotcha: unpreserved
    // library-type string bindings silently die in consumer Release builds).
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicProperties, typeof(Scaffold))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicProperties, typeof(ScaffoldNavBarContext))]
    public BindingBase ProvideValue(IServiceProvider serviceProvider)
    {
        var contextPath = nameof(Scaffold.NavBarContext);
        var path = Path is "." or "" ? contextPath : $"{contextPath}.{Path}";

        return new Binding(path, Mode, Converter, ConverterParameter, StringFormat)
        {
            Source = new RelativeBindingSource(RelativeBindingSourceMode.FindAncestor, typeof(Scaffold))
        };
    }

    object IMarkupExtension.ProvideValue(IServiceProvider serviceProvider) => ProvideValue(serviceProvider);
}
