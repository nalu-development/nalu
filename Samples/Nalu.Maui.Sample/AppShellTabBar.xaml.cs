using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.Maui.Controls.Shapes;

namespace Nalu.Maui.Sample;

public partial class AppShellTabBar
{
    private ShellItem Item => BindingContext as ShellItem ?? throw new InvalidOperationException("AppShellTabBar must have a ShellItem as its BindingContext");
    
    public AppShellTabBar()
    {
        InitializeComponent();
    }

    protected override void OnBindingContextChanged()
    {
        ShellItem?.PropertyChanged -= OnCurrentItemChanged;

        if (BindingContext is ShellItem item)
        {
            ShellItem = item;
            item.PropertyChanged += OnCurrentItemChanged;
            UpdateCurrentItem(item.CurrentItem);
        }
    }

    public ShellItem? ShellItem { get; set; }

    private void OnCurrentItemChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == ShellItem.CurrentItemProperty.PropertyName)
        {
            UpdateCurrentItem(ShellItem!.CurrentItem);
        }
    }

private void UpdateCurrentItem(ShellSection currentItem)
{
    this.CancelAnimations();
    var selectedIndex = Math.Min(4, ShellItem?.Items.IndexOf(currentItem) ?? 0);
    var startPosition = TabBarShape.InsetPosition;
    var endPosition = 0.25 * selectedIndex;
    var startTranslationX = SelectedShape.TranslationX;
    var availableTranslationWidth = ((View)SelectedShape.Parent).Width;
    var endTranslationX = (availableTranslationWidth - TabBarShape.InsetWidth) * 0.25 * selectedIndex + 36;
    var startTranslationY = SelectedShape.TranslationY;
    var middleTranslationY = 50;
    var startOpacity = SelectedButton.Opacity;
    var middleOpacity = 0f;
    var endTranslationY = 0;
    var endOpacity = 1f;

    SelectedShape.ZIndex = 0;

    this.Animate(
        "ButtonFadeOut",
        v =>
        {
            SelectedShape.TranslationY = startTranslationY + (middleTranslationY - startTranslationY) * v;
            SelectedButton.Opacity = startOpacity + (middleOpacity - startOpacity) * v;
        },
        length: 125,
        finished: (_, canceled) =>
        {
            if (canceled)
            {
                return;
            }
            
            ((FontImageSource)SelectedButton.Source).Glyph = ((FontImageSource)((ImageButton)Buttons[selectedIndex]!).Source).Glyph;

            this.Animate(
                "ButtonFadeIn",
                v =>
                {
                    SelectedShape.TranslationY = middleTranslationY + (endTranslationY - middleTranslationY) * v;
                    SelectedButton.Opacity = middleOpacity + (endOpacity - middleOpacity) * v;
                },
                finished: (_, canceled2) =>
                {
                    if (canceled2)
                    {
                        return;
                    }

                    SelectedShape.ZIndex = 2;
                }
            );
        }
    );
    this.Animate("CurrentItem",
                 v =>
                 {
                     TabBarShape.InsetPosition = (float)(startPosition + (endPosition - startPosition) * v);
                     SelectedShape.TranslationX = startTranslationX + (endTranslationX - startTranslationX) * v;
                 },
        length: 250);
}

private void IconClicked(object? sender, EventArgs e)
{
    var icon = (ImageButton)sender!;
    var parent = (Layout)icon.Parent!;
    var index = parent.IndexOf(icon);
    
    NaluTabBar.GoTo(Item.Items[index]);
}

private void SelectedButtonClicked(object? sender, EventArgs e)
{
    
}
}

public class FancyTabBarShape : Shape
{
    public static readonly BindableProperty InsetWidthProperty = BindableProperty.Create(nameof(InsetWidth), typeof(float), typeof(FancyTabBarShape), 80.0f);
    public static readonly BindableProperty InsetHeightProperty = BindableProperty.Create(nameof(InsetHeight), typeof(float), typeof(FancyTabBarShape), 24.0f);
    public static readonly BindableProperty TopRadiusProperty = BindableProperty.Create(nameof(TopRadius), typeof(float), typeof(FancyTabBarShape), 4.0f);
    public static readonly BindableProperty BottomRadiusProperty = BindableProperty.Create(nameof(BottomRadius), typeof(float), typeof(FancyTabBarShape), 24.0f);
    public static readonly BindableProperty InsetPositionProperty = BindableProperty.Create(nameof(InsetPosition), typeof(float), typeof(FancyTabBarShape), 0.0f);

    public float InsetWidth
    {
        get => (float) GetValue(InsetWidthProperty);
        set => SetValue(InsetWidthProperty, value);
    }

    public float InsetHeight
    {
        get => (float) GetValue(InsetHeightProperty);
        set => SetValue(InsetHeightProperty, value);
    }

    public float TopRadius
    {
        get => (float) GetValue(TopRadiusProperty);
        set => SetValue(TopRadiusProperty, value);
    }

    public float BottomRadius
    {
        get => (float) GetValue(BottomRadiusProperty);
        set => SetValue(BottomRadiusProperty, value);
    }

    public float InsetPosition
    {
        get => (float) GetValue(InsetPositionProperty);
        set => SetValue(InsetPositionProperty, value);
    }

    public FancyTabBarShape()
    {
        Aspect = Stretch.Fill;
    }
    
    public override PathF GetPath()
    {
        var width = (float)GetWidthForPathComputation(this);
        var height = (float)GetHeightForPathComputation(this);
        
        var availableWidth = width;

        var insetWidth = InsetWidth;
        var insetCurveWidth = insetWidth / 2;
        var insetCurveBezierWidth = insetCurveWidth / 2;
        var insetHeight = InsetHeight;
        var insetPosition = (float)Math.Clamp(InsetPosition, 0.0, 1.0);

        var path = new PathF(0, 0);
        path.RelativeLineTo(insetPosition * (availableWidth - insetWidth), 0);
        path.RelativeCurveTo(insetCurveBezierWidth, 0, insetCurveBezierWidth, insetHeight, insetCurveWidth, insetHeight);
        path.RelativeCurveTo(insetCurveBezierWidth, 0, insetCurveBezierWidth, -insetHeight, insetCurveWidth, -insetHeight);
        path.LineTo(width, 0);
        path.RelativeLineTo(0, height);
        path.LineTo(0, path.LastPoint.Y);
        path.LineTo(0, 0);
        path.Close();

        return path;
    }
    
    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "get_WidthForPathComputation")]
    private static extern double GetWidthForPathComputation(Shape shape);
    
    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "get_HeightForPathComputation")]
    private static extern double GetHeightForPathComputation(Shape shape);
}
