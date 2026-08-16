using System.Diagnostics.CodeAnalysis;

namespace Nalu;

/// <summary>
/// Binds to the ambient <see cref="ScaffoldKeyboardState"/> from anywhere inside the scaffold's
/// element tree — pages, sheet/popup content, chrome:
/// <c>IsVisible="{nalu:KeyboardBinding IsVisible, Converter={StaticResource InvertedBool}}"</c>,
/// <c>HeightRequest="{nalu:KeyboardBinding Height}"</c>.
/// </summary>
/// <remarks>
/// Resolves through the nearest ancestor <see cref="Scaffold"/>, so it only binds while the
/// target is attached to the scaffold's tree. The path is evaluated by reflection (not
/// compiled) — the reflected surface is preserved under trimming.
/// </remarks>
[ContentProperty(nameof(Path))]
[AcceptEmptyServiceProvider]
public sealed class KeyboardBindingExtension : IMarkupExtension<BindingBase>
{
    /// <summary>Gets or sets the path within the <see cref="ScaffoldKeyboardState"/> ('.', the default, binds the state object itself).</summary>
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
        => KeyboardBindings.Create(Path, Mode, Converter, ConverterParameter, StringFormat);

    object IMarkupExtension.ProvideValue(IServiceProvider serviceProvider) => ProvideValue(serviceProvider);
}

/// <summary>
/// The code-behind counterpart of <see cref="KeyboardBindingExtension"/>: builds bindings to the
/// ambient <see cref="ScaffoldKeyboardState"/> from anywhere inside the scaffold's element tree.
/// </summary>
/// <remarks>
/// <code>
/// // String path (same reflection semantics as {nalu:KeyboardBinding}):
/// banner.SetBinding(VisualElement.IsVisibleProperty, KeyboardBindings.Create("IsVisible", converter: new InvertedBoolConverter()));
///
/// // Fully typed and compiled (trimming/AOT-safe):
/// spacer.SetBinding(VisualElement.HeightRequestProperty,
///     static (Scaffold s) => s.KeyboardState.Height,
///     source: KeyboardBindings.ScaffoldAncestor);
/// </code>
/// </remarks>
public static class KeyboardBindings
{
    /// <summary>
    /// The relative source resolving the nearest ancestor <see cref="Scaffold"/> — combine it
    /// with the typed <c>SetBinding(property, static (Scaffold s) =&gt; s.KeyboardState.…,
    /// source: KeyboardBindings.ScaffoldAncestor)</c> for fully typed, compiled bindings.
    /// </summary>
    public static RelativeBindingSource ScaffoldAncestor => NavBarBindings.ScaffoldAncestor;

    /// <summary>Builds a string-path binding into the ambient <see cref="ScaffoldKeyboardState"/>.</summary>
    /// <param name="path">The path within the state ("." binds the state object itself).</param>
    /// <param name="mode">The binding mode.</param>
    /// <param name="converter">The converter.</param>
    /// <param name="converterParameter">The converter parameter.</param>
    /// <param name="stringFormat">The string format.</param>
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicProperties, typeof(Scaffold))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicProperties, typeof(ScaffoldKeyboardState))]
    public static BindingBase Create(
        string path = ".",
        BindingMode mode = BindingMode.Default,
        IValueConverter? converter = null,
        object? converterParameter = null,
        string? stringFormat = null)
    {
        var statePath = nameof(Scaffold.KeyboardState);
        var fullPath = path is "." or "" ? statePath : $"{statePath}.{path}";

        return new Binding(fullPath, mode, converter, converterParameter, stringFormat)
        {
            Source = ScaffoldAncestor
        };
    }
}
