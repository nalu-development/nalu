using System.ComponentModel;
using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Layouts;

namespace Nalu;

/// <summary>
/// Effective styling values of the default tab bar template: the user-set
/// <see cref="ScaffoldTabBarView"/> property when present, the theme-aware Nalu default otherwise.
/// </summary>
internal sealed record ScaffoldTabBarStyleValues(
    Brush BarBackground,
    Shadow BarShadow,
    Color TextColor,
    Color SelectedTextColor,
    Brush SelectionPillBackground,
    Brush BadgeBackground,
    Color BadgeTextColor,
    Color ScrimColor,
    Brush OverflowPanelBackground,
    Shadow OverflowPanelShadow
);

/// <summary>
/// Theme-aware defaults of the default tab bar template. The accent is the Nalu logo wave blue:
/// <c>#68A3F1</c> on dark, <c>#2C479D</c> on light. Applied as fallbacks by the template itself —
/// user values (set directly or through styles, e.g. with <c>AppThemeBinding</c>) always win.
/// </summary>
internal static class ScaffoldTabBarPalette
{
    internal static readonly Color AccentLight = Color.FromArgb("#2C479D");
    internal static readonly Color AccentDark = Color.FromArgb("#68A3F1");

    public static ScaffoldTabBarStyleValues Resolve(ScaffoldTabBarView barView)
    {
        var dark = (Application.Current?.RequestedTheme ?? AppTheme.Light) == AppTheme.Dark;
        var accent = dark ? AccentDark : AccentLight;

        return new ScaffoldTabBarStyleValues(
            BarBackground: barView.BarBackground ?? new SolidColorBrush(dark ? Color.FromArgb("#EB2E2E2E") : Color.FromArgb("#F2FFFFFF")),
            BarShadow: barView.BarShadow ?? new Shadow
            {
                Brush = Brush.Black,
                Opacity = dark ? 0.35f : 0.18f,
                Radius = 14,
                Offset = new Point(0, 3)
            },
            TextColor: barView.TextColor ?? (dark ? Colors.White : Color.FromArgb("#3A3A40")),
            SelectedTextColor: barView.SelectedTextColor ?? accent,
            SelectionPillBackground: barView.SelectionPillBackground ?? new SolidColorBrush(accent.WithAlpha(dark ? 0.18f : 0.12f)),
            BadgeBackground: barView.BadgeBackground ?? new SolidColorBrush(accent),
            BadgeTextColor: barView.BadgeTextColor ?? (dark ? Color.FromArgb("#042C53") : Colors.White),
            ScrimColor: barView.ScrimColor ?? Colors.Black.WithAlpha(dark ? 0.55f : 0.45f),
            OverflowPanelBackground: barView.OverflowPanelBackground ?? new SolidColorBrush(dark ? Color.FromArgb("#F7333333") : Color.FromArgb("#FAFFFFFF")),
            OverflowPanelShadow: barView.OverflowPanelShadow ?? new Shadow
            {
                Brush = Brush.Black,
                Opacity = dark ? 0.4f : 0.22f,
                Radius = 18,
                Offset = new Point(0, 4)
            }
        );
    }
}

/// <summary>
/// The pill's content: lays out fixed-width item slots and computes which items fit.
/// Given the measure width constraint: <c>slots = floor(width / ItemWidth)</c>; when every
/// visible root fits, all are shown and "More" is hidden; otherwise <c>slots − 1</c> roots are
/// shown followed by the "More" item, and the remainder (declaration order) becomes the
/// overflow set. Desired width is <c>shown × ItemWidth</c> — the pill hugs its content.
/// </summary>
internal sealed class ScaffoldTabBarItemsLayout : Layout
{
    private readonly ScaffoldTabBarView _owner;
    private readonly List<ScaffoldTabBarItemView> _rootItems = [];
    private ScaffoldTabBarItemView? _moreItem;
    private List<ScaffoldRoot> _overflowRoots = [];

    public ScaffoldTabBarItemsLayout(ScaffoldTabBarView owner)
    {
        _owner = owner;
    }

    internal IReadOnlyList<ScaffoldRoot> OverflowRoots => _overflowRoots;

    internal IEnumerable<ScaffoldTabBarItemView> ItemViews
        => _moreItem is null ? _rootItems : [.. _rootItems, _moreItem];

    internal ScaffoldTabBarItemView? MoreItem => _moreItem;

    internal void Rebuild()
    {
        foreach (var item in _rootItems)
        {
            item.Unsubscribe();
            Remove(item);
        }

        _rootItems.Clear();

        if (_moreItem is null)
        {
            _moreItem = new ScaffoldTabBarItemView(_owner, root: null);
        }
        else
        {
            Remove(_moreItem);
        }

        if (_owner.TabBar is { } tabBar)
        {
            foreach (var root in tabBar.Roots)
            {
                var item = new ScaffoldTabBarItemView(_owner, root);
                _rootItems.Add(item);
                Add(item);
            }
        }

        Add(_moreItem);
        InvalidateMeasure();
    }

    internal void OnRootVisibilityChanged() => InvalidateMeasure();

    internal void UpdateMoreState()
        => _moreItem?.SetSelectedState(_overflowRoots.Any(r => r.IsSelected));

    protected override ILayoutManager CreateLayoutManager() => new Manager(this);

    private sealed class Manager(ScaffoldTabBarItemsLayout layout) : ILayoutManager
    {
        private List<ScaffoldTabBarItemView> _plan = [];

        public Size Measure(double widthConstraint, double heightConstraint)
        {
            var itemWidth = Math.Max(1, layout._owner.ItemWidth);
            var visibleItems = layout._rootItems.Where(item => item.Root!.IsVisible).ToList();

            int shownRootCount;
            var showMore = false;

            if (double.IsInfinity(widthConstraint) || visibleItems.Count * itemWidth <= widthConstraint)
            {
                shownRootCount = visibleItems.Count;
            }
            else
            {
                var slots = Math.Max(2, (int)(widthConstraint / itemWidth));
                shownRootCount = Math.Min(slots - 1, visibleItems.Count);
                showMore = true;
            }

            _plan = visibleItems.Take(shownRootCount).ToList();

            if (showMore && layout._moreItem is { } moreItem)
            {
                _plan.Add(moreItem);
            }

            var overflow = visibleItems.Skip(shownRootCount).Select(item => item.Root!).ToList();

            if (!overflow.SequenceEqual(layout._overflowRoots))
            {
                layout._overflowRoots = overflow;
                layout.UpdateMoreState();
                layout._owner.NotifyOverflowRootsChanged();
            }

            double height = 0;

            foreach (var item in _plan)
            {
                var size = ((IView)item).Measure(itemWidth, heightConstraint);
                height = Math.Max(height, size.Height);
            }

            return new Size(_plan.Count * itemWidth, height);
        }

        public Size ArrangeChildren(Rect bounds)
        {
            var itemWidth = Math.Max(1, layout._owner.ItemWidth);
            var x = bounds.X;

            foreach (var child in layout.Cast<IView>())
            {
                if (child is ScaffoldTabBarItemView item && _plan.Contains(item))
                {
                    item.Arrange(new Rect(x, bounds.Y, itemWidth, bounds.Height));
                    x += itemWidth;
                }
                else
                {
                    // Out-of-plan items park far offscreen (a zero-size arrange still renders
                    // the platform view at its stale frame on iOS); the pill's clip hides them.
                    child.Arrange(new Rect(-10000, -10000, itemWidth, bounds.Height));
                }
            }

            return bounds.Size;
        }
    }
}

/// <summary>
/// One tab item: untinted icon (or the built-in ••• glyph for the "More" item), optional badge,
/// truncating label, and the rounded selection highlight. Selection visuals react to the root's
/// <see cref="ScaffoldRoot.IsSelected"/>; the badge to the <see cref="ScaffoldTabBarView.BadgeTextProperty"/>
/// attached value.
/// </summary>
/// <remarks>
/// A Grid (not a Border): a Border clips its content to the stroke shape, cutting the badge's
/// icon overlap — the selection pill is an inner background Border instead. Icon host, label
/// and badge carry explicit dp heights so the bar measures IDENTICALLY on iOS and Android
/// (platform text metrics differ otherwise).
/// </remarks>
internal sealed class ScaffoldTabBarItemView : Grid
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

    /// <summary>The represented root; null for the "More" item.</summary>
    internal ScaffoldRoot? Root { get; }

    /// <param name="owner">The default template owning the styling values.</param>
    /// <param name="root">The represented root; null for the "More" item.</param>
    /// <param name="tapOverride">
    /// Replaces the default tap behavior (selection through the owner) — used by the overflow
    /// panel, which reuses this template but must dismiss the overlay first.
    /// </param>
    /// <param name="automationIdOverride">
    /// Replaces the default automation id (MAUI allows setting it only once; the overflow panel
    /// needs distinct ids while the bar's parked item for the same root is still in the tree).
    /// </param>
    public ScaffoldTabBarItemView(ScaffoldTabBarView owner, ScaffoldRoot? root, Func<Task>? tapOverride = null, string? automationIdOverride = null)
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
            _icon = new Image
            {
                Aspect = Aspect.AspectFit,
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center
            };
            _icon.SetBinding(Image.SourceProperty, Binding.Create(static (ScaffoldRoot r) => r.CurrentIcon, source: root));
            iconHost.Add(_icon);
        }
        else if (owner.OverflowIcon is { } overflowIcon)
        {
            _icon = new Image
            {
                Source = overflowIcon,
                Aspect = Aspect.AspectFit,
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center
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
            HorizontalOptions = LayoutOptions.Center,
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

        ApplyStyling();
        UpdateBadge();
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
            ApplyStyling();
        }
    }

    internal void ApplyStyling()
    {
        var owner = _owner;
        var style = owner.EffectiveStyle;

        _pill.StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(owner.SelectionPillCornerRadius) };
        _pill.Background = _selected ? style.SelectionPillBackground : null;

        // Explicit dp heights everywhere text metrics would otherwise leak platform differences:
        // the bar must measure identically on iOS and Android.
        _iconHost.HeightRequest = owner.IconSize;

        if (_icon is not null)
        {
            _icon.WidthRequest = owner.IconSize;
            _icon.HeightRequest = owner.IconSize;
        }

        if (_dots is not null)
        {
            var dotFill = new SolidColorBrush(_selected ? style.SelectedTextColor : style.TextColor);

            foreach (var dot in _dots.Children.OfType<Ellipse>())
            {
                dot.Fill = dotFill;
            }

            // The dots row mimics the icon slot height so More aligns with icon items.
            _dots.HeightRequest = owner.IconSize;
        }

        _label.TextColor = _selected ? style.SelectedTextColor : style.TextColor;
        _label.FontFamily = owner.FontFamily;
        _label.FontSize = owner.FontSize;
        _label.FontAttributes = _selected ? FontAttributes.Bold : FontAttributes.None;
        _label.HeightRequest = Math.Ceiling(owner.FontSize * 1.45);

        _badge.Background = style.BadgeBackground;
        _badge.HeightRequest = Math.Ceiling(owner.BadgeFontSize * 1.6);
        _badgeLabel.TextColor = style.BadgeTextColor;
        _badgeLabel.FontSize = owner.BadgeFontSize;
        _badgeLabel.FontFamily = owner.FontFamily;

        // Overlap the icon's top-right corner (translation only — no layout impact; the item
        // never clips, and the fixed protrusion stays inside the bar pill's padding headroom).
        _badge.TranslationX = owner.IconSize * 0.5 + 6;
        _badge.TranslationY = -5;
    }

    private void UpdateBadge()
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
                UpdateBadge();

                break;
        }
    }
}
