using Nalu.Internals;

namespace Nalu;

/// <summary>
/// A destination group holding one or more <see cref="ScaffoldRoot"/>s, each hosting an
/// independent navigation stack. A plain area has no visible root switcher: with a single root
/// it is a plain page host; with multiple roots, switching happens through navigation.
/// <see cref="ScaffoldTabBar"/> derives from it to add a tab UI.
/// </summary>
/// <remarks>
/// Areas carry no route identity of their own: absolute navigation is type-based and resolves
/// the destination from the roots' <see cref="ScaffoldRoot.PageType"/> registrations.
/// </remarks>
[ContentProperty(nameof(Roots))]
public class ScaffoldArea : Element
{
    /// <summary>Bindable property for <see cref="Title"/>.</summary>
    public static readonly BindableProperty TitleProperty =
        GenericBindableProperty<ScaffoldArea>.Create<string?>(nameof(Title));

    private static readonly BindablePropertyKey _isSelectedPropertyKey =
        BindableProperty.CreateReadOnly(
            nameof(IsSelected),
            typeof(bool),
            typeof(ScaffoldArea),
            false);

    /// <summary>Bindable property for <see cref="IsSelected"/> (read-only).</summary>
    public static readonly BindableProperty IsSelectedProperty = _isSelectedPropertyKey.BindableProperty;

    private static readonly BindablePropertyKey _currentRootPropertyKey =
        BindableProperty.CreateReadOnly(nameof(CurrentRoot), typeof(ScaffoldRoot), typeof(ScaffoldArea), null);

    /// <summary>Bindable property for <see cref="CurrentRoot"/> (read-only).</summary>
    public static readonly BindableProperty CurrentRootProperty = _currentRootPropertyKey.BindableProperty;

    /// <summary>
    /// Gets or sets the display title used by the default flyout template as the group header
    /// of a multi-root area. Icons live on the roots (<see cref="ScaffoldRoot.Icon"/>) — an
    /// area deliberately carries text-only metadata.
    /// </summary>
    public string? Title
    {
        get => (string?)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    /// <summary>
    /// Gets whether this area is the scaffold's <see cref="Scaffold.CurrentArea"/>.
    /// Read-only, updated by the navigation engine; observable via binding — the styling hook
    /// for flyout templates (highlight state).
    /// </summary>
    public bool IsSelected
    {
        get => (bool)GetValue(IsSelectedProperty);
        internal set => SetValue(_isSelectedPropertyKey, value);
    }

    /// <summary>Gets the root destinations owned by this area.</summary>
    public IList<ScaffoldRoot> Roots { get; }

    /// <summary>
    /// Gets the selected root (defaults to the first root). Read-only: selection changes only
    /// through the Nalu navigation engine (absolute navigation or tab/flyout interaction), so
    /// lifecycle events and leaving guards can never be bypassed. Observable via binding.
    /// Each root preserves its navigation stack while unselected.
    /// </summary>
    public ScaffoldRoot? CurrentRoot
    {
        get => (ScaffoldRoot?)GetValue(CurrentRootProperty);
        internal set => SetValue(_currentRootPropertyKey, value);
    }

    /// <summary>Initializes a new <see cref="ScaffoldArea"/>.</summary>
    public ScaffoldArea()
    {
        Roots = new ScaffoldElementCollection<ScaffoldRoot>(this);
    }

    /// <summary>
    /// Wraps a standalone root into a single-root <see cref="ScaffoldArea"/>, enabling the
    /// terse form <c>&lt;nalu:ScaffoldRoot PageType="..." /&gt;</c> directly inside a
    /// <see cref="Scaffold"/>. Composition happens once at conversion time — the resulting
    /// structure is real and is never mutated afterwards.
    /// </summary>
    /// <param name="root">The root to wrap.</param>
    public static implicit operator ScaffoldArea(ScaffoldRoot root)
        => new() { Roots = { root } };
}
