using Microsoft.Maui.Layouts;

namespace Nalu.Internals;

/// <summary>
/// The default bar's row — leading buttons | title | toolbar items | trailing buttons — as a
/// purpose-built layout. A Grid could not do it: an Auto column is measured against the WHOLE
/// row, so a long toolbar strip took everything, squeezed the title to nothing and pushed the
/// leading buttons off screen. Here the fixed parts (leading, trailing) are measured first, the
/// strip is offered what is left MINUS the title's reservation (so it folds the items that
/// don't fit into its overflow menu), and the title takes the remainder. The reservation is the
/// title's own natural width — items may fold, never truncate the title — with
/// <see cref="MinimumTitleWidth"/> as its floor; <see cref="KeepTitleWhole"/> off reduces it to
/// the floor alone, the native "items win" trade-off.
/// </summary>
internal sealed class ScaffoldNavBarRow : Layout
{
    private readonly View _leading;
    private readonly View _title;
    private readonly View _strip;
    private readonly View _trailing;
    private double _spacing;
    private double _minimumTitleWidth;
    private bool _keepTitleWhole = true;

    public ScaffoldNavBarRow(View leading, View title, View strip, View trailing)
    {
        _leading = leading;
        _title = title;
        _strip = strip;
        _trailing = trailing;

        Add(leading);
        Add(title);
        Add(strip);
        Add(trailing);
    }

    /// <summary>The gap on either side of the title.</summary>
    public double Spacing
    {
        get => _spacing;
        set
        {
            if (!_spacing.Equals(value))
            {
                _spacing = value;
                InvalidateMeasure();
            }
        }
    }

    /// <summary>The width the title keeps whatever the toolbar strip asks for.</summary>
    public double MinimumTitleWidth
    {
        get => _minimumTitleWidth;
        set
        {
            if (!_minimumTitleWidth.Equals(value))
            {
                _minimumTitleWidth = value;
                InvalidateMeasure();
            }
        }
    }

    /// <summary>Whether the title reserves its natural width (items fold before the title truncates).</summary>
    public bool KeepTitleWhole
    {
        get => _keepTitleWhole;
        set
        {
            if (_keepTitleWhole != value)
            {
                _keepTitleWhole = value;
                InvalidateMeasure();
            }
        }
    }

    /// <summary>
    /// The width reserved for the title before the strip is offered the rest: its natural width
    /// when it is kept whole, never less than the floor, never more than what the row has.
    /// </summary>
    internal static double ComputeTitleReservation(double minimum, double natural, bool keepWhole, double available)
    {
        var reservation = keepWhole ? Math.Max(minimum, double.IsInfinity(natural) ? 0 : natural) : minimum;

        return Math.Clamp(reservation, 0, Math.Max(0, available));
    }

    protected override ILayoutManager CreateLayoutManager() => new Manager(this);

    /// <summary>A fresh manager over this row (test seam: the manager is driven headlessly).</summary>
    internal ILayoutManager CreateManager() => new Manager(this);

    private sealed class Manager(ScaffoldNavBarRow row) : ILayoutManager
    {
        public Size Measure(double widthConstraint, double heightConstraint)
        {
            var padding = row.Padding;
            var width = Math.Max(0, widthConstraint - padding.HorizontalThickness);
            var height = Math.Max(0, heightConstraint - padding.VerticalThickness);

            var leading = ((IView)row._leading).Measure(double.PositiveInfinity, height);
            var trailing = ((IView)row._trailing).Measure(double.PositiveInfinity, height);
            var fixedWidth = leading.Width + trailing.Width + row._spacing * 2;

            // Unbounded (a bar measured for its natural size) fits everything; bounded, the strip
            // is offered the row minus the fixed parts and the title's reservation.
            double stripBudget;

            if (double.IsInfinity(width))
            {
                stripBudget = double.PositiveInfinity;
            }
            else
            {
                var natural = ((IView)row._title).Measure(double.PositiveInfinity, height).Width;
                var reservation = ComputeTitleReservation(row._minimumTitleWidth, natural, row._keepTitleWhole, width - fixedWidth);
                stripBudget = Math.Max(0, width - fixedWidth - reservation);
            }

            var strip = ((IView)row._strip).Measure(stripBudget, height);

            // The title's final measure is at the width it actually gets (truncation happens here).
            var titleWidth = double.IsInfinity(width) ? double.PositiveInfinity : Math.Max(0, width - fixedWidth - strip.Width);
            var title = ((IView)row._title).Measure(titleWidth, height);

            var desiredWidth = double.IsInfinity(width) ? fixedWidth + strip.Width + title.Width : width;
            var desiredHeight = Math.Max(Math.Max(leading.Height, trailing.Height), Math.Max(strip.Height, title.Height));

            return new Size(desiredWidth + padding.HorizontalThickness, desiredHeight + padding.VerticalThickness);
        }

        public Size ArrangeChildren(Rect bounds)
        {
            var padding = row.Padding;
            var left = bounds.Left + padding.Left;
            var top = bounds.Top + padding.Top;
            var width = Math.Max(0, bounds.Width - padding.HorizontalThickness);
            var height = Math.Max(0, bounds.Height - padding.VerticalThickness);
            var rtl = ((IView)row).FlowDirection == FlowDirection.RightToLeft;

            var leadingWidth = row._leading.DesiredSize.Width;
            var trailingWidth = row._trailing.DesiredSize.Width;
            var stripWidth = row._strip.DesiredSize.Width;
            var titleWidth = Math.Max(0, width - leadingWidth - trailingWidth - stripWidth - row._spacing * 2);

            var x = 0.0;
            Place(row._leading, x, leadingWidth);
            x += leadingWidth + row._spacing;
            Place(row._title, x, titleWidth);
            x += titleWidth + row._spacing;
            Place(row._strip, x, stripWidth);
            x += stripWidth;
            Place(row._trailing, x, trailingWidth);

            return bounds.Size;

            void Place(View view, double offset, double viewWidth)
                => ((IView)view).Arrange(new Rect(rtl ? left + width - offset - viewWidth : left + offset, top, viewWidth, height));
        }
    }
}
