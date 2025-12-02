using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Layouts;

namespace Nalu;

/// <summary>
/// A default tab bar implementation for NaluShell to be used with <see cref="NaluShell.TabBarViewProperty"/> on a <see cref="TabBar"/> node.
/// </summary>
public partial class NaluTabBar
{
    #region Tab Properties

    /// <summary>
    /// Bindable property for <see cref="TabStrokeShape"/>.
    /// </summary>
    public static readonly BindableProperty TabStrokeShapeProperty =
        BindableProperty.Create(nameof(TabStrokeShape), typeof(IShape), typeof(NaluTabBar), new RoundRectangle { CornerRadius = new CornerRadius(8) });

    /// <summary>
    /// Bindable property for <see cref="TabStroke"/>.
    /// </summary>
    public static readonly BindableProperty TabStrokeProperty =
        BindableProperty.Create(nameof(TabStroke), typeof(Brush), typeof(NaluTabBar), null);

    /// <summary>
    /// Bindable property for <see cref="TabStrokeThickness"/>.
    /// </summary>
    public static readonly BindableProperty TabStrokeThicknessProperty =
        BindableProperty.Create(nameof(TabStrokeThickness), typeof(double), typeof(NaluTabBar), 0.0);

    /// <summary>
    /// Bindable property for <see cref="TabForegroundColor"/>.
    /// </summary>
    public static readonly BindableProperty TabForegroundColorProperty =
        BindableProperty.Create(nameof(TabForegroundColor), typeof(Color), typeof(NaluTabBar), Colors.Black);

    /// <summary>
    /// Bindable property for <see cref="TabBackground"/>.
    /// </summary>
    public static readonly BindableProperty TabBackgroundProperty =
        BindableProperty.Create(nameof(TabBackground), typeof(Brush), typeof(NaluTabBar), new SolidColorBrush(new Color(255,255,255, 128)));

    /// <summary>
    /// Gets or sets the stroke shape for tab buttons.
    /// </summary>
    public IShape? TabStrokeShape
    {
        get => (IShape?)GetValue(TabStrokeShapeProperty);
        set => SetValue(TabStrokeShapeProperty, value);
    }

    /// <summary>
    /// Gets or sets the stroke brush for tab buttons.
    /// </summary>
    public Brush? TabStroke
    {
        get => (Brush?)GetValue(TabStrokeProperty);
        set => SetValue(TabStrokeProperty, value);
    }

    /// <summary>
    /// Gets or sets the stroke thickness for tab buttons.
    /// </summary>
    public double TabStrokeThickness
    {
        get => (double)GetValue(TabStrokeThicknessProperty);
        set => SetValue(TabStrokeThicknessProperty, value);
    }

    /// <summary>
    /// Gets or sets the foreground color for tab buttons.
    /// </summary>
    public Color TabForegroundColor
    {
        get => (Color)GetValue(TabForegroundColorProperty);
        set => SetValue(TabForegroundColorProperty, value);
    }

    /// <summary>
    /// Gets or sets the background brush for tab buttons.
    /// </summary>
    public Brush? TabBackground
    {
        get => (Brush?)GetValue(TabBackgroundProperty);
        set => SetValue(TabBackgroundProperty, value);
    }

    #endregion

    #region Active Tab Properties

    /// <summary>
    /// Bindable property for <see cref="ActiveTabStrokeShape"/>.
    /// </summary>
    public static readonly BindableProperty ActiveTabStrokeShapeProperty =
        BindableProperty.Create(nameof(ActiveTabStrokeShape), typeof(IShape), typeof(NaluTabBar), new RoundRectangle { CornerRadius = new CornerRadius(8) });

    /// <summary>
    /// Bindable property for <see cref="ActiveTabStroke"/>.
    /// </summary>
    public static readonly BindableProperty ActiveTabStrokeProperty =
        BindableProperty.Create(nameof(ActiveTabStroke), typeof(Brush), typeof(NaluTabBar), null);

    /// <summary>
    /// Bindable property for <see cref="ActiveTabStrokeThickness"/>.
    /// </summary>
    public static readonly BindableProperty ActiveTabStrokeThicknessProperty =
        BindableProperty.Create(nameof(ActiveTabStrokeThickness), typeof(double), typeof(NaluTabBar), 0.0);

    /// <summary>
    /// Bindable property for <see cref="ActiveTabForegroundColor"/>.
    /// </summary>
    public static readonly BindableProperty ActiveTabForegroundColorProperty =
        BindableProperty.Create(nameof(ActiveTabForegroundColor), typeof(Color), typeof(NaluTabBar), Colors.Black);

    /// <summary>
    /// Bindable property for <see cref="ActiveTabBackground"/>.
    /// </summary>
    public static readonly BindableProperty ActiveTabBackgroundProperty =
        BindableProperty.Create(nameof(ActiveTabBackground), typeof(Brush), typeof(NaluTabBar), Brush.White);

    /// <summary>
    /// Gets or sets the stroke shape for active tab buttons.
    /// </summary>
    public IShape? ActiveTabStrokeShape
    {
        get => (IShape?)GetValue(ActiveTabStrokeShapeProperty);
        set => SetValue(ActiveTabStrokeShapeProperty, value);
    }

    /// <summary>
    /// Gets or sets the stroke brush for active tab buttons.
    /// </summary>
    public Brush? ActiveTabStroke
    {
        get => (Brush?)GetValue(ActiveTabStrokeProperty);
        set => SetValue(ActiveTabStrokeProperty, value);
    }

    /// <summary>
    /// Gets or sets the stroke thickness for active tab buttons.
    /// </summary>
    public double ActiveTabStrokeThickness
    {
        get => (double)GetValue(ActiveTabStrokeThicknessProperty);
        set => SetValue(ActiveTabStrokeThicknessProperty, value);
    }

    /// <summary>
    /// Gets or sets the foreground color for active tab buttons.
    /// </summary>
    public Color ActiveTabForegroundColor
    {
        get => (Color)GetValue(ActiveTabForegroundColorProperty);
        set => SetValue(ActiveTabForegroundColorProperty, value);
    }

    /// <summary>
    /// Gets or sets the background brush for active tab buttons.
    /// </summary>
    public Brush? ActiveTabBackground
    {
        get => (Brush?)GetValue(ActiveTabBackgroundProperty);
        set => SetValue(ActiveTabBackgroundProperty, value);
    }

    #endregion

    #region Bar Properties

    /// <summary>
    /// Bindable property for <see cref="BarStrokeShape"/>.
    /// </summary>
    public static readonly BindableProperty BarStrokeShapeProperty =
        BindableProperty.Create(nameof(BarStrokeShape), typeof(IShape), typeof(NaluTabBar), null);

    /// <summary>
    /// Bindable property for <see cref="BarStroke"/>.
    /// </summary>
    public static readonly BindableProperty BarStrokeProperty =
        BindableProperty.Create(nameof(BarStroke), typeof(Brush), typeof(NaluTabBar), null);

    /// <summary>
    /// Bindable property for <see cref="BarStrokeThickness"/>.
    /// </summary>
    public static readonly BindableProperty BarStrokeThicknessProperty =
        BindableProperty.Create(nameof(BarStrokeThickness), typeof(double), typeof(NaluTabBar), 0.0);

    /// <summary>
    /// Bindable property for <see cref="BarBackground"/>.
    /// </summary>
    public static readonly BindableProperty BarBackgroundProperty =
        BindableProperty.Create(nameof(BarBackground), typeof(Brush), typeof(NaluTabBar), Brush.Transparent);

    /// <summary>
    /// Bindable property for <see cref="BarPadding"/>.
    /// </summary>
    public static readonly BindableProperty BarPaddingProperty =
        BindableProperty.Create(nameof(BarPadding), typeof(Thickness), typeof(NaluTabBar), new Thickness(8, 0));

    /// <summary>
    /// Bindable property for <see cref="BarMargin"/>.
    /// </summary>
    public static readonly BindableProperty BarMarginProperty =
        BindableProperty.Create(nameof(BarMargin), typeof(Thickness), typeof(NaluTabBar), Thickness.Zero);

    /// <summary>
    /// Gets or sets the stroke shape for the tab bar container.
    /// </summary>
    public IShape? BarStrokeShape
    {
        get => (IShape?)GetValue(BarStrokeShapeProperty);
        set => SetValue(BarStrokeShapeProperty, value);
    }

    /// <summary>
    /// Gets or sets the stroke brush for the tab bar container.
    /// </summary>
    public Brush? BarStroke
    {
        get => (Brush?)GetValue(BarStrokeProperty);
        set => SetValue(BarStrokeProperty, value);
    }

    /// <summary>
    /// Gets or sets the stroke thickness for the tab bar container.
    /// </summary>
    public double BarStrokeThickness
    {
        get => (double)GetValue(BarStrokeThicknessProperty);
        set => SetValue(BarStrokeThicknessProperty, value);
    }

    /// <summary>
    /// Gets or sets the background brush for the tab bar container.
    /// </summary>
    public Brush? BarBackground
    {
        get => (Brush?)GetValue(BarBackgroundProperty);
        set => SetValue(BarBackgroundProperty, value);
    }

    /// <summary>
    /// Gets or sets the padding for the tab bar container.
    /// </summary>
    public Thickness BarPadding
    {
        get => (Thickness)GetValue(BarPaddingProperty);
        set => SetValue(BarPaddingProperty, value);
    }

    /// <summary>
    /// Gets or sets the margin for the tab bar container.
    /// </summary>
    public Thickness BarMargin
    {
        get => (Thickness)GetValue(BarMarginProperty);
        set => SetValue(BarMarginProperty, value);
    }

    #endregion

    /// <summary>
    /// Initializes a new instance of the <see cref="NaluTabBar"/> class.
    /// </summary>
    public NaluTabBar()
    {
        InitializeComponent();
    }

    private volatile int _navigating;
    
    // ReSharper disable once AsyncVoidEventHandlerMethod
#pragma warning disable VSTHRD100
    private async void TapGestureRecognizer_OnTapped(object? sender, TappedEventArgs e)
#pragma warning restore VSTHRD100
    {
        if (sender is not View { BindingContext: ShellSection shellSection })
        {
            return;
        }

        if (Interlocked.CompareExchange(ref _navigating, 1, 0) != 0)
        {
            return;
        }

        try
        {
            await Shell.Current.GoToAsync($"//{shellSection.CurrentItem.Route}");
        }
        finally
        {
            Interlocked.Exchange(ref _navigating, 0);
        }
    }

    /// <inheritdoc/>
    protected override ILayoutManager CreateLayoutManager()
    {
        var layoutManager = base.CreateLayoutManager();
        var wrappedLayoutManager = new WrappedLayoutManager(layoutManager);
        return wrappedLayoutManager;
    }
}

file class WrappedLayoutManager(ILayoutManager layoutManager) : ILayoutManager
{
    public Size Measure(double widthConstraint, double heightConstraint) => layoutManager.Measure(widthConstraint, heightConstraint);

    public Size ArrangeChildren(Rect bounds) => layoutManager.ArrangeChildren(bounds);
}
