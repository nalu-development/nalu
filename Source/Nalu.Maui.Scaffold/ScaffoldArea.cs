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

    /// <summary>Bindable property for <see cref="Icon"/>.</summary>
    public static readonly BindableProperty IconProperty =
        GenericBindableProperty<ScaffoldArea>.Create<ImageSource?>(nameof(Icon), propertyChanged: area => area.OnIconSourceChanged);

    /// <summary>Bindable property for <see cref="SelectedIcon"/>.</summary>
    public static readonly BindableProperty SelectedIconProperty =
        GenericBindableProperty<ScaffoldArea>.Create<ImageSource?>(nameof(SelectedIcon), propertyChanged: area => area.OnIconSourceChanged);

    private static readonly BindablePropertyKey _isSelectedPropertyKey =
        BindableProperty.CreateReadOnly(
            nameof(IsSelected),
            typeof(bool),
            typeof(ScaffoldArea),
            false,
            propertyChanged: (bindable, _, _) => ((ScaffoldArea)bindable).UpdateCurrentIcon());

    /// <summary>Bindable property for <see cref="IsSelected"/> (read-only).</summary>
    public static readonly BindableProperty IsSelectedProperty = _isSelectedPropertyKey.BindableProperty;

    private static readonly BindablePropertyKey _currentIconPropertyKey =
        BindableProperty.CreateReadOnly(nameof(CurrentIcon), typeof(ImageSource), typeof(ScaffoldArea), null);

    /// <summary>Bindable property for <see cref="CurrentIcon"/> (read-only).</summary>
    public static readonly BindableProperty CurrentIconProperty = _currentIconPropertyKey.BindableProperty;

    private static readonly BindablePropertyKey _currentRootPropertyKey =
        BindableProperty.CreateReadOnly(nameof(CurrentRoot), typeof(ScaffoldRoot), typeof(ScaffoldArea), null);

    /// <summary>Bindable property for <see cref="CurrentRoot"/> (read-only).</summary>
    public static readonly BindableProperty CurrentRootProperty = _currentRootPropertyKey.BindableProperty;

    /// <summary>Gets or sets the display title used by the default flyout template.</summary>
    public string? Title
    {
        get => (string?)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    /// <summary>Gets or sets the icon used by the default flyout template.</summary>
    public ImageSource? Icon
    {
        get => (ImageSource?)GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    /// <summary>
    /// Gets or sets the icon used by the default flyout template while this area is selected
    /// (e.g. the filled variant of <see cref="Icon"/>). Falls back to <see cref="Icon"/> when not set.
    /// </summary>
    public ImageSource? SelectedIcon
    {
        get => (ImageSource?)GetValue(SelectedIconProperty);
        set => SetValue(SelectedIconProperty, value);
    }

    /// <summary>
    /// Gets whether this area is the scaffold's <see cref="Scaffold.CurrentArea"/>.
    /// Read-only, updated by the navigation engine; observable via binding — the styling hook
    /// for flyout templates (highlight state, <see cref="SelectedIcon"/> swap).
    /// </summary>
    public bool IsSelected
    {
        get => (bool)GetValue(IsSelectedProperty);
        internal set => SetValue(_isSelectedPropertyKey, value);
    }

    /// <summary>
    /// Gets the icon the chrome should currently display: <see cref="SelectedIcon"/> while
    /// selected (when set), <see cref="Icon"/> otherwise. Read-only, recomputed whenever
    /// <see cref="IsSelected"/>, <see cref="Icon"/> or <see cref="SelectedIcon"/> change —
    /// bind flyout templates directly to it.
    /// </summary>
    public ImageSource? CurrentIcon => (ImageSource?)GetValue(CurrentIconProperty);

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

    private void OnIconSourceChanged(ImageSource? oldValue, ImageSource? newValue) => UpdateCurrentIcon();

    private void UpdateCurrentIcon()
        => SetValue(_currentIconPropertyKey, IsSelected ? SelectedIcon ?? Icon : Icon);
}
