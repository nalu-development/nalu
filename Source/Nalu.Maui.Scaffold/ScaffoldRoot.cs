using System.Diagnostics.CodeAnalysis;
using System.Windows.Input;
using Nalu.Internals;

namespace Nalu;

/// <summary>
/// A root destination hosting an independent navigation stack: the root page (created lazily
/// from <see cref="PageType"/> through the Nalu navigation engine — own DI scope, page-model
/// lifecycle, destroyable while not displayed) plus the pages pushed onto it, preserved while
/// the root is not selected. Its identity in absolute navigation derives from the page type.
/// </summary>
public class ScaffoldRoot : Element
{
    /// <summary>Bindable property for <see cref="Title"/>.</summary>
    public static readonly BindableProperty TitleProperty =
        GenericBindableProperty<ScaffoldRoot>.Create<string?>(nameof(Title));

    /// <summary>Bindable property for <see cref="Icon"/>.</summary>
    public static readonly BindableProperty IconProperty =
        GenericBindableProperty<ScaffoldRoot>.Create<ImageSource?>(nameof(Icon), propertyChanged: root => root.OnIconSourceChanged);

    /// <summary>Bindable property for <see cref="SelectedIcon"/>.</summary>
    public static readonly BindableProperty SelectedIconProperty =
        GenericBindableProperty<ScaffoldRoot>.Create<ImageSource?>(nameof(SelectedIcon), propertyChanged: root => root.OnIconSourceChanged);

    /// <summary>Bindable property for <see cref="IsVisible"/>.</summary>
    public static readonly BindableProperty IsVisibleProperty =
        GenericBindableProperty<ScaffoldRoot>.Create(nameof(IsVisible), true);

    private static readonly BindablePropertyKey _isSelectedPropertyKey =
        BindableProperty.CreateReadOnly(
            nameof(IsSelected),
            typeof(bool),
            typeof(ScaffoldRoot),
            false,
            propertyChanged: (bindable, _, _) => ((ScaffoldRoot)bindable).UpdateCurrentIcon());

    /// <summary>Bindable property for <see cref="IsSelected"/> (read-only).</summary>
    public static readonly BindableProperty IsSelectedProperty = _isSelectedPropertyKey.BindableProperty;

    private static readonly BindablePropertyKey _currentIconPropertyKey =
        BindableProperty.CreateReadOnly(nameof(CurrentIcon), typeof(ImageSource), typeof(ScaffoldRoot), null);

    /// <summary>Bindable property for <see cref="CurrentIcon"/> (read-only).</summary>
    public static readonly BindableProperty CurrentIconProperty = _currentIconPropertyKey.BindableProperty;

    /// <summary>
    /// Gets or sets the root page type, registered with the Nalu navigation configuration
    /// (<c>AddPage</c>/<c>AddPages</c>).
    /// </summary>
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
    public Type? PageType { get; set; }

    /// <summary>
    /// Gets the live navigation stack hosted by this root — the state the Scaffold's navigation
    /// proxies mutate and the platform presenter realizes. Pages in the stack are parented as
    /// logical children of this root.
    /// </summary>
    internal ScaffoldNavigationStack NavigationStack => field ??= new ScaffoldNavigationStack(this);

    /// <summary>Gets or sets the display title used by the default tab bar template.</summary>
    public string? Title
    {
        get => (string?)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    /// <summary>Gets or sets the icon used by the default tab bar template.</summary>
    public ImageSource? Icon
    {
        get => (ImageSource?)GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    /// <summary>
    /// Gets or sets the icon used by the default tab bar template while this root is selected
    /// (e.g. the filled variant of <see cref="Icon"/>). Falls back to <see cref="Icon"/> when not set.
    /// </summary>
    public ImageSource? SelectedIcon
    {
        get => (ImageSource?)GetValue(SelectedIconProperty);
        set => SetValue(SelectedIconProperty, value);
    }

    /// <summary>
    /// Gets or sets whether this destination is shown by the chrome. Defaults to true.
    /// A non-visible root is omitted from the default tab bar / flyout templates, but its
    /// route stays fully navigable (programmatic and absolute navigation still work).
    /// </summary>
    public bool IsVisible
    {
        get => (bool)GetValue(IsVisibleProperty);
        set => SetValue(IsVisibleProperty, value);
    }

    /// <summary>
    /// Gets whether this root is its owning area's <see cref="ScaffoldArea.CurrentRoot"/>.
    /// Read-only, updated by the navigation engine; observable via binding — the styling hook
    /// for tab templates (highlight state, <see cref="SelectedIcon"/> swap).
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
    /// bind tab templates directly to it.
    /// </summary>
    public ImageSource? CurrentIcon => (ImageSource?)GetValue(CurrentIconProperty);

    /// <summary>
    /// Gets the parameterless command selecting this root through the navigation engine
    /// (preserved-stack restore, active-root-pops-to-root, guards always run) — the selection
    /// hook for custom tab bar and flyout templates alike: with the root as binding context,
    /// <c>Command="{Binding SelectCommand}"</c> is all a template needs.
    /// <c>CanExecute</c> is false while ANY selection on the owning scaffold is navigating
    /// (all roots' commands disable together, so a second tap can't race the first) and the
    /// command no-ops while the root is not part of a presented <see cref="Scaffold"/>.
    /// </summary>
    public ICommand SelectCommand => field ??= new ScaffoldRootSelectCommand(this);

    private void OnIconSourceChanged(ImageSource? oldValue, ImageSource? newValue) => UpdateCurrentIcon();

    private void UpdateCurrentIcon()
        => SetValue(_currentIconPropertyKey, IsSelected ? SelectedIcon ?? Icon : Icon);
}
