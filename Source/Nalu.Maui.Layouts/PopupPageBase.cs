using Microsoft.Maui.Controls.PlatformConfiguration;
using Microsoft.Maui.Controls.PlatformConfiguration.iOSSpecific;
using Nalu.Internals;

namespace Nalu;

/// <summary>
/// The <see cref="Border"/> acting as a container for the popup content.
/// </summary>
/// <remarks>
/// This class is used to provide a visual container for the popup content.
/// It can be styled and customized as needed.
/// <code>
/// <![CDATA[
/// <Style TargetType="nalu:PopupContainer">
///     <Setter Property="Background" Value="{AppThemeBinding Light={StaticResource White}, Dark={StaticResource OffBlack}}" />
///     <Setter Property="Margin" Value="16" />
///     <Setter Property="StrokeShape" Value="RoundRectangle 24" />
///     <Setter Property="StrokeThickness" Value="0" />
///     <Setter Property="VerticalOptions" Value="Center" />
///     <Setter Property="HorizontalOptions" Value="Center" />
/// </Style>
/// ]]>
/// </code>
/// </remarks>
public sealed class PopupContainer : Border
{
    /// <summary>
    /// Bindable property for <see cref="OverlapsSafeArea"/>.
    /// </summary>
    public static readonly BindableProperty OverlapsSafeAreaProperty = GenericBindableProperty<PopupContainer>.Create(nameof(OverlapsSafeArea), false, propertyChanged: bindable => bindable.OnOverlapsSafeAreaPropertyChanged);

    /// <summary>
    /// Gets or sets whether this popup container should overlap safe area when needed
    /// </summary>
    public bool OverlapsSafeArea 
    {
        get => (bool)GetValue(OverlapsSafeAreaProperty);
        set => SetValue(OverlapsSafeAreaProperty, value);
    }

    /// <inheritdoc />
    protected override void OnParentSet()
    {
        base.OnParentSet();

        UpdateIgnoreSafeArea();
    }

    private void OnOverlapsSafeAreaPropertyChanged(bool oldValue, bool newValue) => UpdateIgnoreSafeArea();

    private void UpdateIgnoreSafeArea()
    {
        if (Parent is Layout layout)
        {
            layout.IgnoreSafeArea = OverlapsSafeArea;
        }
    }
}

/// <summary>
/// Defines the presentation mode
/// </summary>
public enum PopupPresentationMode
{
    /// <summary>
    /// Shows as a popup
    /// </summary>
    Popup,

    /// <summary>
    /// Shows as <see cref="PresentationMode.Animated"/>
    /// </summary>
    Page,

    /// <summary>
    /// Shows as <see cref="PresentationMode.ModalAnimated"/>
    /// </summary>
    ModalPage,
}

/// <summary>
/// The <see cref="ViewBox"/> acting as a scrim for the popup.
/// </summary>
/// <remarks>
/// This class is used to provide a semi-transparent background that covers the entire screen,
/// allowing the popup to stand out. It can be styled and customized as needed.
/// <code>
/// <![CDATA[
/// <Style TargetType="nalu:PopupScrim">
///     <Setter Property="BackgroundColor" Value="{AppThemeBinding Light='#20000000', Dark='#20FFFFFF'}" />
/// </Style>
/// ]]>
/// </code>
/// </remarks>
public sealed class PopupScrim : ViewBox;

/// <summary>
/// A base class for pages acting as popups.
/// </summary>
[ContentProperty(nameof(PopupContent))]
public abstract class PopupPageBase : ContentPage
{
    private bool _hasAnimated;
    private PopupPresentationMode _mode;

    /// <summary>
    /// Bindable property for <see cref="CloseOnScrimTapped"/>.
    /// </summary>
    public static readonly BindableProperty CloseOnScrimTappedProperty =
        BindableProperty.Create(nameof(CloseOnScrimTapped), typeof(bool), typeof(PopupPageBase), true);
    
    /// <summary>
    /// Bindable property for <see cref="PopupContent"/>.
    /// </summary>
    public static readonly BindableProperty PopupContentProperty =
        BindableProperty.Create(nameof(PopupContent), typeof(View), typeof(PopupPageBase), null,
                                propertyChanged: (bindable, _, newValue) =>
                                {
                                    if (bindable is PopupPageBase popupPage)
                                    {
                                        popupPage.PopupBorder.Content = newValue as View;
                                    }
                                });
    
    /// <summary>
    /// Gets or sets the content of the popup.
    /// </summary>
    public View PopupContent
    {
        get => (View)GetValue(PopupContentProperty);
        set => SetValue(PopupContentProperty, value);
    }

    /// <summary>
    /// Gets or sets a value indicating whether the popup should close when the scrim is tapped.
    /// If set to true, tapping on the scrim will close the popup.
    /// </summary>
    public bool CloseOnScrimTapped
    {
        get => (bool)GetValue(CloseOnScrimTappedProperty);
        set => SetValue(CloseOnScrimTappedProperty, value);
    }
    
    /// <summary>
    /// Initializes a new instance of the <see cref="PopupPageBase"/> class.
    /// </summary>
    protected PopupPageBase()
    {
        SetPopupPresentationMode(PopupPresentationMode.Popup);

        PopupBorder = new PopupContainer();
        Scrim = new PopupScrim();

        var scrimTapRecognizer = new TapGestureRecognizer();
        var closing = false;
        scrimTapRecognizer.Tapped += (_, _) =>
        {
            if (CloseOnScrimTapped && !closing)
            {
                closing = true;
                _ = Navigation?.PopModalAsync();
            }
        };

        var tappableScrim = new Grid();
        tappableScrim.GestureRecognizers.Add(scrimTapRecognizer);

        var ignoreSafeAreaPopupBorderLayout = new Grid { tappableScrim, PopupBorder };

        var ignoreSafeAreaPageLayout = new Grid { IgnoreSafeArea = true };
        ignoreSafeAreaPageLayout.Add(Scrim);
        ignoreSafeAreaPageLayout.Add(ignoreSafeAreaPopupBorderLayout);

        Content = ignoreSafeAreaPageLayout;
    }

    /// <summary>
    /// Enables or disables popup presentation mode.
    /// </summary>
    /// <remarks>
    /// The popup presentation mode involves: transparent background, no navbar, not animated modal <see cref="PresentationMode"/>.
    /// This mode is enabled by default.
    /// </remarks>
    /// <param name="mode">The presentation mode</param>
    protected void SetPopupPresentationMode(PopupPresentationMode mode)
    {
        switch (mode)
        {
            case PopupPresentationMode.Popup:
                BackgroundColor = Colors.Transparent;
                Background = Brush.Transparent;
                Shell.SetPresentationMode(this, PresentationMode.ModalNotAnimated);
                Shell.SetNavBarIsVisible(this, false);
                On<iOS>().SetModalPresentationStyle(UIModalPresentationStyle.OverFullScreen);
                break;
            case PopupPresentationMode.Page:
            case PopupPresentationMode.ModalPage:
                if (_mode == PopupPresentationMode.Popup)
                {
                    ClearValue(BackgroundColorProperty);
                    ClearValue(BackgroundProperty);
                    On<iOS>().SetModalPresentationStyle(UIModalPresentationStyle.FullScreen);
                    Shell.SetNavBarIsVisible(this, true);
                }
                Shell.SetPresentationMode(this, mode == PopupPresentationMode.Page ? PresentationMode.Animated : PresentationMode.Modal);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(mode), mode, null);
        }

        _mode = mode;
    }

    /// <inheritdoc />
    protected override void OnParentChanging(ParentChangingEventArgs args)
    {
        base.OnParentChanging(args);

        if (args.NewParent is not null && args.OldParent is null)
        {
            PreparePopupAnimation();
        }
    }

    /// <inheritdoc />
    protected override bool OnBackButtonPressed()
    {
        if (CloseOnScrimTapped)
        {
            return base.OnBackButtonPressed();
        }

        return true;
    }

    /// <inheritdoc />
    protected override void OnAppearing()
    {
        base.OnAppearing();

        if (!_hasAnimated)
        {
            _hasAnimated = true;
            AnimatePopup();
        }
    }

    /// <summary>
    /// Sets up the initial state of the popup animation.
    /// This method can be overridden to customize the initial state of the popup before it appears.
    /// By default, this method sets the opacity and scale of the popup to zero,
    /// </summary>
    protected virtual void PreparePopupAnimation()
    {
        if (_mode == PopupPresentationMode.Popup)
        {
            PopupBorder.Opacity = 0;
            PopupBorder.Scale = 0;
        }
    }

    /// <summary>
    /// Animates the popup when it appears.
    /// This method can be overridden to customize the animation behavior.
    /// By default, it animates the opacity and scale of the popup from 0 to 1 over a duration of 250 milliseconds using a cubic easing function.
    /// </summary>
    protected virtual void AnimatePopup()
    {
        if (_mode == PopupPresentationMode.Popup)
        {
            PopupBorder.Animate(
                "PopupAppearing",
                callback: v =>
                {
                    PopupBorder.Opacity = v;
                    PopupBorder.Scale = v;
                },
                start: 0,
                end: 1,
                length: 250,
                easing: Easing.CubicInOut
            );
        }
    }

    /// <summary>
    /// The <see cref="PopupScrim"/> that covers the entire screen when the popup is displayed.
    /// </summary>
    public PopupScrim Scrim { get; private init; }

    /// <summary>
    /// The <see cref="PopupContainer"/> that acts as a container for the popup content.
    /// </summary>
    public PopupContainer PopupBorder { get; private init; }
}
