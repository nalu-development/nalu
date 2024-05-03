namespace Nalu;

using System.Globalization;
using System.Text.RegularExpressions;
using Cassowary;

/// <summary>
/// Defines an anchor point in a <see cref="ConstraintLayout"/>.
/// </summary>
[ContentProperty(nameof(Target))]
public partial class Anchor : BindableObject
{
    /// <summary>
    /// Bindable property for <see cref="Target"/>.
    /// </summary>
    public static readonly BindableProperty TargetProperty = BindableProperty.Create(
        nameof(Target),
        typeof(string),
        typeof(Anchor),
        propertyChanging: (bindable, _, _) =>
        {
            if (bindable.IsSet(TargetProperty))
            {
                throw new InvalidOperationException("The Target property cannot be changed once it has been set.");
            }
        });

    /// <summary>
    /// Bindable property for <see cref="Margin"/>.
    /// </summary>
    public static readonly BindableProperty MarginProperty = BindableProperty.Create(
        nameof(Margin),
        typeof(double),
        typeof(Anchor),
        propertyChanging: (bindable, _, _) =>
        {
            if (bindable.IsSet(MarginProperty))
            {
                throw new InvalidOperationException("The Margin property cannot be changed once it has been set.");
            }
        });

    /// <summary>
    /// Bindable property for <see cref="GoneMargin"/>.
    /// </summary>
    public static readonly BindableProperty GoneMarginProperty = BindableProperty.Create(
        nameof(GoneMargin),
        typeof(double),
        typeof(Anchor),
        propertyChanging: (bindable, _, _) =>
        {
            if (bindable.IsSet(GoneMarginProperty))
            {
                throw new InvalidOperationException("The GoneMargin property cannot be changed once it has been set.");
            }
        });

    /// <summary>
    /// Bindable property for <see cref="Tight"/>.
    /// </summary>
    public static readonly BindableProperty TightProperty = BindableProperty.Create(
        nameof(Tight),
        typeof(bool),
        typeof(Anchor),
        propertyChanging: (bindable, _, _) =>
        {
            if (bindable.IsSet(TightProperty))
            {
                throw new InvalidOperationException("The Tight property cannot be changed once it has been set.");
            }
        });

    /// <summary>
    /// Gets or sets the target element identifier.
    /// </summary>
    public string Target
    {
        get => (string)GetValue(TargetProperty);
        set => SetValue(TargetProperty, value);
    }

    /// <summary>
    /// Gets or sets the margin.
    /// </summary>
    public double Margin
    {
        get => (double)GetValue(MarginProperty);
        set => SetValue(MarginProperty, value);
    }

    /// <summary>
    /// Gets or sets the margin when the target element is gone (a.k.a. <see cref="IView.Visibility"/> = <see cref="Visibility.Collapsed"/> or <see cref="VisualElement.IsVisible"/> = false).
    /// </summary>
    public double GoneMargin
    {
        get => (double)GetValue(GoneMarginProperty);
        set => SetValue(GoneMarginProperty, value);
    }

    /// <summary>
    /// Gets or sets a value indicating whether the anchor is tight.
    /// </summary>
    public bool Tight
    {
        get => (bool)GetValue(TightProperty);
        set => SetValue(TightProperty, value);
    }

    /// <summary>
    /// Gets the source variable getter.
    /// </summary>
    public Func<ISceneElementBase, Variable> SourceFunc { get; internal set; } = null!;

    /// <summary>
    /// Gets the target variable getter.
    /// </summary>
    public Func<ISceneElementBase, Variable> TargetFunc { get; internal set; } = null!;

    /// <summary>
    /// Gets the anchor type.
    /// </summary>
    public AnchorType Type { get; internal set; }

    /// <summary>
    /// Gets the string representation of the anchor.
    /// </summary>
    public override string ToString()
    {
        var tightSymbol = Tight ? "!" : string.Empty;
        return $"{Target}{tightSymbol} | {Margin} | {GoneMargin}";
    }

    /// <summary>
    /// Parses a string to an <see cref="Anchor"/>.
    /// </summary>
    /// <remarks>A tight margin can be expressed by adding an '!' after the 'targetId'.</remarks>
    /// <param name="str">A string in the format of 'targetId | margin | goneMargin' (margins are optional).</param>
    public static implicit operator Anchor(string str)
    {
        if (_anchorRegex.Match(str) is not { Success: true } match)
        {
            throw new FormatException("Invalid anchor format. Expected format: 'targetId | margin | goneMargin'. Example: 'targetId | 16 | 8'.");
        }

        var target = match.Groups[1].Value;
        var tight = match.Groups[2].Success;
        var margin = match.Groups[3].Value;
        var goneMargin = match.Groups[4].Value;
        var marginValue = string.IsNullOrEmpty(margin) ? 0 : double.Parse(margin, CultureInfo.InvariantCulture);

        return new Anchor
        {
            Target = target,
            Margin = marginValue,
            Tight = tight,
            GoneMargin = string.IsNullOrEmpty(goneMargin) ? marginValue : double.Parse(goneMargin, CultureInfo.InvariantCulture),
        };
    }

    private static readonly Regex _anchorRegex = MyRegex();

    [GeneratedRegex(@"^\s*([^!]+?)(\!)?\s*(?:\|\s*(.+?)\s*)?(?:\|\s*(.+?)\s*)?$")]
    private static partial Regex MyRegex();
}
