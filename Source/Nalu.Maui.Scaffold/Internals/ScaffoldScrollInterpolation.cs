using System.Globalization;
using Microsoft.Maui.Controls.Internals;

namespace Nalu.Internals;

/// <summary>What a scroll-value interpolation targets — decides the lerp semantics.</summary>
internal enum ScrollValueKind
{
    Double,
    Color,
    Brush
}

/// <summary>
/// Shared math of the scroll-value converters: endpoint coercion, typed lerps and the
/// target-property → <see cref="ScrollValueKind"/> mapping.
/// </summary>
internal static class ScrollValueMath
{
    public static T ValueOrDefault<T>(object?[]? values, int index, T fallback)
        => values is not null && values.Length > index && values[index] is T value ? value : fallback;

    public static double Lerp(double from, double to, double t) => from + ((to - from) * t);

    public static Color LerpColor(Color from, Color to, double t)
    {
        var amount = (float)Math.Clamp(t, 0, 1);

        return new Color(
            from.Red + ((to.Red - from.Red) * amount),
            from.Green + ((to.Green - from.Green) * amount),
            from.Blue + ((to.Blue - from.Blue) * amount),
            from.Alpha + ((to.Alpha - from.Alpha) * amount)
        );
    }

    public static object Interpolate(ScrollValueKind kind, object? from, object? to, double t)
        => kind switch
        {
            ScrollValueKind.Double => Lerp(ToDouble(from), ToDouble(to), t),
            ScrollValueKind.Color => LerpColor(ToColor(from), ToColor(to), t),
            _ => new SolidColorBrush(LerpColor(ToColor(from), ToColor(to), t))
        };

    public static double ToDouble(object? value)
        => value switch
        {
            double d => d,
            IConvertible convertible => convertible.ToDouble(CultureInfo.InvariantCulture),
            _ => throw new InvalidOperationException($"ScrollValue endpoint '{value}' is not a number.")
        };

    public static Color ToColor(object? value)
        => value switch
        {
            Color color => color,
            SolidColorBrush brush => brush.Color,
            string text when Color.TryParse(text, out var parsed) => parsed,
            null => Colors.Transparent,
            _ => throw new InvalidOperationException($"ScrollValue endpoint '{value}' is not a color (or solid brush).")
        };

    /// <summary>Maps a target property type onto the lerp semantics (null = unsupported).</summary>
    public static ScrollValueKind? KindFor(Type targetType)
    {
        if (targetType == typeof(double) || targetType == typeof(float) || targetType == typeof(int))
        {
            return ScrollValueKind.Double;
        }

        if (targetType == typeof(Color))
        {
            return ScrollValueKind.Color;
        }

        return typeof(Brush).IsAssignableFrom(targetType) ? ScrollValueKind.Brush : null;
    }
}

/// <summary>
/// The engine behind <see cref="ScrollValueExtension"/>/<see cref="ThemeScrollValueExtension"/>:
/// a multi-value converter over [offset, defaultRampStart, defaultRampEnd, theme] mapping the scroll
/// offset window [RampStart, RampEnd] onto the endpoint values, typed per target property.
/// </summary>
internal sealed class ScrollInterpolationConverter : IMultiValueConverter
{
    public required ScrollValueKind Kind { get; init; }

    /// <summary>Explicit ramp bounds; null falls back to the page-level defaults (values [1]/[2]).</summary>
    public double? RampStart { get; init; }

    public double? RampEnd { get; init; }

    public required object? FromLight { get; init; }

    public required object? ToLight { get; init; }

    public object? FromDark { get; init; }

    public object? ToDark { get; init; }

    public ScrollValueExtrapolation Extrapolation { get; init; }

    public Easing? Easing { get; init; }

    public object? Convert(object?[]? values, Type targetType, object? parameter, CultureInfo culture)
    {
        var offset = ScrollValueMath.ValueOrDefault(values, 0, 0.0);
        var yFrom = RampStart ?? ScrollValueMath.ValueOrDefault(values, 1, 0.0);
        var yTo = RampEnd ?? ScrollValueMath.ValueOrDefault(values, 2, 100.0);
        var dark = ScrollValueMath.ValueOrDefault(values, 3, AppTheme.Light) == AppTheme.Dark;

        var from = dark ? FromDark ?? FromLight : FromLight;
        var to = dark ? ToDark ?? ToLight : ToLight;

        var t = yTo - yFrom == 0
            ? offset < yFrom ? 0 : 1
            : (offset - yFrom) / (yTo - yFrom);

        if (Extrapolation == ScrollValueExtrapolation.Clamp)
        {
            t = Math.Clamp(t, 0, 1);
        }

        // Easing shapes the ramp interior only; extension outside stays linear.
        if (Easing is not null && t is >= 0 and <= 1)
        {
            t = Easing.Ease(t);
        }

        return ScrollValueMath.Interpolate(Kind, from, to, t);
    }

    public object?[]? ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// Observable app-theme token: the theme leg of the interpolation multi-binding (a plain
/// converter cannot re-fire on theme changes by itself).
/// </summary>
internal sealed class ScrollValueThemeListener : BindableObject
{
    // Declared BEFORE Instance: static initializers run in declaration order, and the
    // singleton's ctor writes through the property key.
    private static readonly BindablePropertyKey _themePropertyKey =
        BindableProperty.CreateReadOnly(nameof(Theme), typeof(AppTheme), typeof(ScrollValueThemeListener), AppTheme.Unspecified);

    public static readonly BindableProperty ThemeProperty = _themePropertyKey.BindableProperty;

    public static ScrollValueThemeListener Instance { get; } = new();

    public AppTheme Theme => (AppTheme)GetValue(ThemeProperty);

    /// <summary>The theme leg of a scroll-value multi-binding.</summary>
    public static TypedBinding<ScrollValueThemeListener, AppTheme> CreateBinding()
        => new(
               tl => (tl.Theme, true),
               null,
               [Tuple.Create<Func<ScrollValueThemeListener, object>, string>(o => o, nameof(Theme))]
           )
           {
               Source = Instance
           };

    private ScrollValueThemeListener()
    {
        if (Application.Current is { } application)
        {
            SetValue(_themePropertyKey, application.RequestedTheme);
            application.RequestedThemeChanged += (_, args) => SetValue(_themePropertyKey, args.RequestedTheme);
        }
    }
}
