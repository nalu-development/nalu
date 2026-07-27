using System.ComponentModel;
using Microsoft.Maui.Controls.Shapes;
using Nalu.Internals;

namespace Nalu;

/// <summary>Shared defaults of the default tab bar components.</summary>
internal static class ScaffoldTabBarDefaults
{
    /// <summary>The Nalu logo wave blue: the accent behind every tab bar default value.</summary>
    internal static readonly Color Accent = Color.FromArgb("#2C479D");
}

/// <summary>
/// One item of the default tab bar template: untinted icon (or the built-in ••• glyph for the
/// "More" item), optional badge, truncating label, and the rounded selection highlight.
/// Selection visuals react to the root's <see cref="ScaffoldRoot.IsSelected"/>; the badge to
/// the <see cref="ScaffoldTabBarView.BadgeTextProperty"/> attached value. The overflow panel
/// reuses this same component, so bar items and overflow rows share one look.
/// </summary>
/// <remarks>
/// <para>
/// Instances are created by the template (one per visible root, plus "More") — the type is
/// public purely as a styling surface:
/// <code>
/// &lt;Style TargetType="nalu:ScaffoldTabBarItemView"&gt;
///     &lt;Setter Property="SelectedTextColor" Value="{StaticResource Accent}" /&gt;
/// &lt;/Style&gt;
/// </code>
/// </para>
/// <para>
/// A Grid (not a Border): a Border clips its content to the stroke shape, cutting the badge's
/// icon overlap — the selection pill is an inner background Border instead. Icon host, label
/// and badge carry explicit dp heights so the bar measures IDENTICALLY on iOS and Android
/// (platform text metrics differ otherwise).
/// </para>
/// </remarks>
public sealed class ScaffoldTabBarItemView : Grid
{
    private readonly ScaffoldTabBarView _owner;
    private readonly Border _pill;
    private readonly Grid _iconHost;
    private readonly Image? _icon;
    private readonly HorizontalStackLayout? _dots;
    private readonly Label _label;
    private readonly Border _badge;
    private readonly Label _badgeLabel;
    private bool _selected;

    // Null-guards in the Apply* methods below: implicit styles apply from the VisualElement
    // base ctor, before the subviews exist; the ctor seeds the final values.

    #region Item properties

    /// <summary>Bindable property for <see cref="IconSize"/>.</summary>
    public static readonly BindableProperty IconSizeProperty =
        GenericBindableProperty<ScaffoldTabBarItemView>.Create(
            nameof(IconSize),
            26.0,
            propertyChanged: static item => (_, _) => item.ApplyIconSize()
        );

    /// <summary>Bindable property for <see cref="TextColor"/>.</summary>
    public static readonly BindableProperty TextColorProperty =
        GenericBindableProperty<ScaffoldTabBarItemView>.Create(
            nameof(TextColor),
            Color.FromArgb("#3A3A40"),
            propertyChanged: static item => (_, _) => item.ApplyTextColor()
        );

    /// <summary>Bindable property for <see cref="SelectedTextColor"/>.</summary>
    public static readonly BindableProperty SelectedTextColorProperty =
        GenericBindableProperty<ScaffoldTabBarItemView>.Create(
            nameof(SelectedTextColor),
            ScaffoldTabBarDefaults.Accent,
            propertyChanged: static item => (_, _) => item.ApplyTextColor()
        );

    /// <summary>Bindable property for <see cref="FontFamily"/>.</summary>
    public static readonly BindableProperty FontFamilyProperty =
        GenericBindableProperty<ScaffoldTabBarItemView>.Create<string?>(
            nameof(FontFamily),
            propertyChanged: static item => (_, _) => item.ApplyFontFamily()
        );

    /// <summary>Bindable property for <see cref="FontSize"/>.</summary>
    public static readonly BindableProperty FontSizeProperty =
        GenericBindableProperty<ScaffoldTabBarItemView>.Create(
            nameof(FontSize),
            11.0,
            propertyChanged: static item => (_, _) => item.ApplyFontSize()
        );

    /// <summary>Bindable property for <see cref="SelectionPillBackground"/>.</summary>
    public static readonly BindableProperty SelectionPillBackgroundProperty =
        GenericBindableProperty<ScaffoldTabBarItemView>.Create<Brush?>(
            nameof(SelectionPillBackground),
            defaultValueCreator: static _ => new SolidColorBrush(ScaffoldTabBarDefaults.Accent.WithAlpha(0.12f)),
            propertyChanged: static item => (_, _) => item.ApplySelectionPillBackground()
        );

    /// <summary>Bindable property for <see cref="SelectionPillCornerRadius"/>.</summary>
    public static readonly BindableProperty SelectionPillCornerRadiusProperty =
        GenericBindableProperty<ScaffoldTabBarItemView>.Create(
            nameof(SelectionPillCornerRadius),
            20.0,
            propertyChanged: static item => (_, value) => item._pill?.StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(value) }
        );

    /// <summary>Gets or sets the icon size (both dimensions). Icons render untinted.</summary>
    public double IconSize
    {
        get => (double)GetValue(IconSizeProperty);
        set => SetValue(IconSizeProperty, value);
    }

    /// <summary>Gets or sets the label (and drawn ••• glyph) color while the item is not selected.</summary>
    public Color TextColor
    {
        get => (Color)GetValue(TextColorProperty);
        set => SetValue(TextColorProperty, value);
    }

    /// <summary>Gets or sets the label (and drawn ••• glyph) color while the item is selected.</summary>
    public Color SelectedTextColor
    {
        get => (Color)GetValue(SelectedTextColorProperty);
        set => SetValue(SelectedTextColorProperty, value);
    }

    /// <summary>Gets or sets the font family of the label (and the badge).</summary>
    public string? FontFamily
    {
        get => (string?)GetValue(FontFamilyProperty);
        set => SetValue(FontFamilyProperty, value);
    }

    /// <summary>Gets or sets the label font size.</summary>
    public double FontSize
    {
        get => (double)GetValue(FontSizeProperty);
        set => SetValue(FontSizeProperty, value);
    }

    /// <summary>Gets or sets the background of the rounded highlight painted while the item is selected.</summary>
    public Brush? SelectionPillBackground
    {
        get => (Brush?)GetValue(SelectionPillBackgroundProperty);
        set => SetValue(SelectionPillBackgroundProperty, value);
    }

    /// <summary>Gets or sets the corner radius of the selection highlight.</summary>
    public double SelectionPillCornerRadius
    {
        get => (double)GetValue(SelectionPillCornerRadiusProperty);
        set => SetValue(SelectionPillCornerRadiusProperty, value);
    }

    #endregion

    #region Badge properties

    /// <summary>Bindable property for <see cref="BadgeBackground"/>.</summary>
    public static readonly BindableProperty BadgeBackgroundProperty =
        GenericBindableProperty<ScaffoldTabBarItemView>.Create<Brush?>(
            nameof(BadgeBackground),
            defaultValueCreator: static _ => new SolidColorBrush(ScaffoldTabBarDefaults.Accent),
            propertyChanged: static item => (_, value) => item._badge?.Background = value
        );

    /// <summary>Bindable property for <see cref="BadgeTextColor"/>.</summary>
    public static readonly BindableProperty BadgeTextColorProperty =
        GenericBindableProperty<ScaffoldTabBarItemView>.Create(
            nameof(BadgeTextColor),
            Colors.White,
            propertyChanged: static item => (_, value) => item._badgeLabel?.TextColor = value
        );

    /// <summary>Bindable property for <see cref="BadgeFontSize"/>.</summary>
    public static readonly BindableProperty BadgeFontSizeProperty =
        GenericBindableProperty<ScaffoldTabBarItemView>.Create(
            nameof(BadgeFontSize),
            11.0,
            propertyChanged: static item => (_, _) => item.ApplyBadgeFontSize()
        );

    /// <summary>Gets or sets the badge background.</summary>
    public Brush? BadgeBackground
    {
        get => (Brush?)GetValue(BadgeBackgroundProperty);
        set => SetValue(BadgeBackgroundProperty, value);
    }

    /// <summary>Gets or sets the badge text color.</summary>
    public Color BadgeTextColor
    {
        get => (Color)GetValue(BadgeTextColorProperty);
        set => SetValue(BadgeTextColorProperty, value);
    }

    /// <summary>Gets or sets the badge font size.</summary>
    public double BadgeFontSize
    {
        get => (double)GetValue(BadgeFontSizeProperty);
        set => SetValue(BadgeFontSizeProperty, value);
    }

    #endregion

    /// <summary>The represented root; null for the "More" item.</summary>
    internal ScaffoldRoot? Root { get; }

    /// <param name="owner">The owning template (tap routing, More item content).</param>
    /// <param name="root">The represented root; null for the "More" item.</param>
    /// <param name="tapOverride">
    /// Replaces the default tap behavior (selection through the owner) — used by the overflow
    /// panel, which reuses this component but must dismiss the overlay first.
    /// </param>
    /// <param name="automationIdOverride">
    /// Replaces the default automation id (MAUI allows setting it only once; the overflow panel
    /// needs distinct ids while the bar's parked item for the same root is still in the tree).
    /// </param>
    internal ScaffoldTabBarItemView(ScaffoldTabBarView owner, ScaffoldRoot? root, Func<Task>? tapOverride = null, string? automationIdOverride = null)
    {
        _owner = owner;
        Root = root;

        // The selection highlight: an inner background layer, so the item itself never clips.
        _pill = new Border
        {
            StrokeThickness = 0,
            Background = null,
            InputTransparent = true
        };
        Add(_pill);

        var iconHost = new Grid
        {
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center
        };
        _iconHost = iconHost;

        if (root is not null)
        {
            _icon = new Image { Aspect = Aspect.Fill };
            _icon.SetBinding(Image.SourceProperty, static (ScaffoldRoot r) => r.CurrentIcon, source: root);
            iconHost.Add(_icon);
        }
        else if (owner.OverflowIcon is { } overflowIcon)
        {
            _icon = new Image
            {
                Source = overflowIcon,
                Aspect = Aspect.Fill
            };
            iconHost.Add(_icon);
        }
        else
        {
            // Built-in ••• glyph: three dots matching the label color.
            _dots = new HorizontalStackLayout
            {
                Spacing = 4,
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center
            };

            for (var i = 0; i < 3; i++)
            {
                _dots.Add(new Ellipse { WidthRequest = 5, HeightRequest = 5 });
            }

            iconHost.Add(_dots);
        }

        _badgeLabel = new Label
        {
            FontAttributes = FontAttributes.Bold,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
            VerticalTextAlignment = TextAlignment.Center
        };

        _badge = new Border
        {
            StrokeThickness = 0,
            Padding = new Thickness(5, 0, 5, 0),
            StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(9) },
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Start,
            IsVisible = false,
            Content = _badgeLabel
        };

        iconHost.Add(_badge);

        _label = new Label
        {
            HorizontalTextAlignment = TextAlignment.Center,
            VerticalTextAlignment = TextAlignment.Center,
            LineBreakMode = LineBreakMode.TailTruncation,
            MaxLines = 1
        };

        if (root is not null)
        {
            _label.SetBinding(Label.TextProperty, Binding.Create(static (ScaffoldRoot r) => r.Title, source: root));
        }
        else
        {
            _label.SetBinding(Label.TextProperty, Binding.Create(static (ScaffoldTabBarView v) => v.OverflowTitle, source: owner));
        }

        Add(new VerticalStackLayout
        {
            // The vertical padding lives here (not on the item) so the selection pill layer
            // spans the full item bounds.
            Margin = new Thickness(0, 8, 0, 7),
            Spacing = 2,
            Children = { iconHost, _label }
        });

        var tap = new TapGestureRecognizer();
        tap.Tapped += (_, _) => _ = tapOverride?.Invoke() ?? _owner.OnItemTappedAsync(Root);
        GestureRecognizers.Add(tap);

        if (root is not null)
        {
            AutomationId = automationIdOverride ?? $"Tab{root.Title}";
            _badgeLabel.AutomationId = $"{AutomationId}Badge";
            root.PropertyChanged += OnRootPropertyChanged;
            _selected = root.IsSelected;
        }
        else
        {
            AutomationId = automationIdOverride ?? "TabMore";
        }

        // Defaults never raise propertyChanged: seed once from the current values (values set
        // by an implicit style land during the BASE ctor, before the subviews existed — the
        // Apply* callbacks no-op'd and are made whole here).
        ApplyIconSize();
        ApplyFontFamily();
        ApplyFontSize();
        _pill.StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(SelectionPillCornerRadius) };
        _badge.Background = BadgeBackground;
        _badgeLabel.TextColor = BadgeTextColor;
        ApplyBadgeFontSize();
        ApplySelectedAppearance();
        UpdateBadgeText();
    }

    internal void Unsubscribe()
    {
        if (Root is not null)
        {
            Root.PropertyChanged -= OnRootPropertyChanged;
        }
    }

    internal void SetSelectedState(bool selected)
    {
        if (_selected != selected)
        {
            _selected = selected;
            ApplySelectedAppearance();
        }
    }

    /// <summary>Icon slot geometry — and the badge's fixed protrusion, which is derived from it.</summary>
    private void ApplyIconSize()
    {
        if (_iconHost is null)
        {
            return;
        }

        var iconSize = IconSize;

        // Explicit dp heights everywhere text metrics would otherwise leak platform
        // differences: the bar must measure identically on iOS and Android.
        _iconHost.HeightRequest = iconSize;

        if (_icon is not null)
        {
            _icon.WidthRequest = iconSize;
            _icon.HeightRequest = iconSize;
        }

        if (_dots is not null)
        {
            // The dots row mimics the icon slot height so More aligns with icon items.
            _dots.HeightRequest = iconSize;
        }

        // Overlap the icon's top-right corner (translation only — no layout impact; the item
        // never clips, and the fixed protrusion stays inside the bar pill's padding headroom).
        _badge.TranslationX = iconSize * 0.5 + 6;
        _badge.TranslationY = -5;
    }

    private void ApplyFontFamily()
    {
        if (_label is null)
        {
            return;
        }

        _label.FontFamily = FontFamily;
        _badgeLabel.FontFamily = FontFamily;
    }

    private void ApplyFontSize()
    {
        if (_label is null)
        {
            return;
        }

        _label.FontSize = FontSize;
        _label.HeightRequest = Math.Ceiling(FontSize * 1.45);
    }

    /// <summary>The label (and drawn ••• glyph) color — selection-dependent.</summary>
    private void ApplyTextColor()
    {
        if (_label is null)
        {
            return;
        }

        var textColor = _selected ? SelectedTextColor : TextColor;
        _label.TextColor = textColor;

        if (_dots is not null)
        {
            var dotFill = new SolidColorBrush(textColor);

            foreach (var dot in _dots.Children.OfType<Ellipse>())
            {
                dot.Fill = dotFill;
            }
        }
    }

    /// <summary>The highlight brush — only the selected item paints it.</summary>
    private void ApplySelectionPillBackground()
        => _pill?.Background = _selected ? SelectionPillBackground : null;

    private void ApplyBadgeFontSize()
    {
        if (_badge is null)
        {
            return;
        }

        _badgeLabel.FontSize = BadgeFontSize;
        _badge.HeightRequest = Math.Ceiling(BadgeFontSize * 1.6);
    }

    /// <summary>Everything that flips with selection, in one place.</summary>
    private void ApplySelectedAppearance()
    {
        ApplyTextColor();
        ApplySelectionPillBackground();
        _label.FontAttributes = _selected ? FontAttributes.Bold : FontAttributes.None;
    }

    private void UpdateBadgeText()
    {
        var text = Root is null ? null : ScaffoldTabBarView.GetBadgeText(Root);
        _badge.IsVisible = !string.IsNullOrEmpty(text);
        _badgeLabel.Text = text;
    }

    private void OnRootPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(ScaffoldRoot.IsSelected):
                SetSelectedState(Root!.IsSelected);
                (Parent as ScaffoldTabBarItemsLayout)?.UpdateMoreState();

                break;

            case nameof(ScaffoldRoot.IsVisible):
                (Parent as ScaffoldTabBarItemsLayout)?.OnRootVisibilityChanged();

                break;

            case "BadgeText":
                UpdateBadgeText();

                break;
        }
    }
}
