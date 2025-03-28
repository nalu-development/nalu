using System.Diagnostics.CodeAnalysis;
using SkiaSharp;
using SkiaSharp.Views.Maui;

namespace Nalu;

/// <summary>
/// Represents a view that allows the user to select a duration by rotating a wheel.
/// </summary>
public class DurationWheel : InteractableCanvasView
{
    private float _angle;
    private float _density = 1f;
    private TimeSpan? _duration = TimeSpan.Zero;
    private DateTime _lastMoved = DateTime.MinValue;
    private Point _lastPointerPosition;

    private bool _isDragging;

    private SKColor _outerBackgroundColor = ((Color) OuterBackgroundColorProperty.DefaultValue).ToSKColor();
    private SKColor _innerBackgroundColor = ((Color) InnerBackgroundColorProperty.DefaultValue).ToSKColor();
    private SKColor _markersColor = ((Color) MarkersColorProperty.DefaultValue).ToSKColor();
    private SKColor _innerShadowColor = ((Color) InnerShadowColorProperty.DefaultValue).ToSKColor();
    private SKColor _lowValueColor = ((Color) LowValueColorProperty.DefaultValue).ToSKColor();
    private SKColor _highValueColor = ((Color) HighValueColorProperty.DefaultValue).ToSKColor();
    private float _markerWidth = (float) MarkerWidthProperty.DefaultValue;
    private float _markerSize = (float) MarkerSizeProperty.DefaultValue;
    private float _markerGap = (float) MarkerGapProperty.DefaultValue;
    private float _valueWidth = (float) ValueWidthProperty.DefaultValue;

    /// <summary>
    /// Bindable property for the ValueWidth property.
    /// </summary>
    public static readonly BindableProperty ValueWidthProperty =
        BindableProperty.Create(
            nameof(ValueWidth),
            typeof(float),
            typeof(DurationWheel),
            20f,
            propertyChanged: (bindable, _, newValue) =>
            {
                var control = (DurationWheel) bindable;
                control._valueWidth = (float) newValue;
                control.InvalidateSurface();
            }
        );

    /// <summary>
    /// Gets or sets the width of the value.
    /// </summary>
    public float ValueWidth
    {
        get => (float) GetValue(ValueWidthProperty);
        set => SetValue(ValueWidthProperty, value);
    }

    /// <summary>
    /// Bindable property for the OuterBackgroundColor property.
    /// </summary>
    public static readonly BindableProperty OuterBackgroundColorProperty =
        BindableProperty.Create(
            nameof(OuterBackgroundColor),
            typeof(Color),
            typeof(DurationWheel),
            Colors.LightGray,
            propertyChanged: (bindable, _, newValue) =>
            {
                var control = (DurationWheel) bindable;
                control._outerBackgroundColor = ((Color) newValue).ToSKColor();
                control.InvalidateSurface();
            }
        );

    /// <summary>
    /// Gets or sets the outer background color.
    /// </summary>
    public Color OuterBackgroundColor
    {
        get => (Color) GetValue(OuterBackgroundColorProperty);
        set => SetValue(OuterBackgroundColorProperty, value);
    }

    /// <summary>
    /// Bindable property for the InnerBackgroundColor property.
    /// </summary>
    public static readonly BindableProperty InnerBackgroundColorProperty =
        BindableProperty.Create(
            nameof(InnerBackgroundColor),
            typeof(Color),
            typeof(DurationWheel),
            Colors.White,
            propertyChanged: (bindable, _, newValue) =>
            {
                var control = (DurationWheel) bindable;
                control._innerBackgroundColor = ((Color) newValue).ToSKColor();
                control.InvalidateSurface();
            }
        );

    /// <summary>
    /// Gets or sets the inner background color.
    /// </summary>
    public Color InnerBackgroundColor
    {
        get => (Color) GetValue(InnerBackgroundColorProperty);
        set => SetValue(InnerBackgroundColorProperty, value);
    }

    /// <summary>
    /// Bindable property for the MarkersColor property.
    /// </summary>
    public static readonly BindableProperty MarkersColorProperty =
        BindableProperty.Create(
            nameof(MarkersColor),
            typeof(Color),
            typeof(DurationWheel),
            Colors.Black,
            propertyChanged: (bindable, _, newValue) =>
            {
                var control = (DurationWheel) bindable;
                control._markersColor = ((Color) newValue).ToSKColor();
                control.InvalidateSurface();
            }
        );

    /// <summary>
    /// Gets or sets the color of the markers.
    /// </summary>
    public Color MarkersColor
    {
        get => (Color) GetValue(MarkersColorProperty);
        set => SetValue(MarkersColorProperty, value);
    }

    /// <summary>
    /// Bindable property for the InnerShadowColor property.
    /// </summary>
    public static readonly BindableProperty InnerShadowColorProperty =
        BindableProperty.Create(
            nameof(InnerShadowColor),
            typeof(Color),
            typeof(DurationWheel),
            new Color(0, 0, 0, 48),
            propertyChanged: (bindable, _, newValue) =>
            {
                var control = (DurationWheel) bindable;
                control._innerShadowColor = ((Color) newValue).ToSKColor();
                control.InvalidateSurface();
            }
        );

    /// <summary>
    /// Gets or sets the color of the inner shadow.
    /// </summary>
    public Color InnerShadowColor
    {
        get => (Color) GetValue(InnerShadowColorProperty);
        set => SetValue(InnerShadowColorProperty, value);
    }

    /// <summary>
    /// Bindable property for the LowValueColor property.
    /// </summary>
    public static readonly BindableProperty LowValueColorProperty =
        BindableProperty.Create(
            nameof(LowValueColor),
            typeof(Color),
            typeof(DurationWheel),
            Colors.MidnightBlue,
            propertyChanged: (bindable, _, newValue) =>
            {
                var control = (DurationWheel) bindable;
                control._lowValueColor = ((Color) newValue).ToSKColor();
                control.InvalidateSurface();
            }
        );

    /// <summary>
    /// Gets or sets the color for low values.
    /// </summary>
    public Color LowValueColor
    {
        get => (Color) GetValue(LowValueColorProperty);
        set => SetValue(LowValueColorProperty, value);
    }

    /// <summary>
    /// Bindable property for the HighValueColor property.
    /// </summary>
    public static readonly BindableProperty HighValueColorProperty =
        BindableProperty.Create(
            nameof(HighValueColor),
            typeof(Color),
            typeof(DurationWheel),
            Colors.LightSkyBlue,
            propertyChanged: (bindable, _, newValue) =>
            {
                var control = (DurationWheel) bindable;
                control._highValueColor = ((Color) newValue).ToSKColor();
                control.InvalidateSurface();
            }
        );

    /// <summary>
    /// Gets or sets the color for high values.
    /// </summary>
    public Color HighValueColor
    {
        get => (Color) GetValue(HighValueColorProperty);
        set => SetValue(HighValueColorProperty, value);
    }

    /// <summary>
    /// Bindable property for the MarkerWidth property.
    /// </summary>
    public static readonly BindableProperty MarkerWidthProperty =
        BindableProperty.Create(
            nameof(MarkerWidth),
            typeof(float),
            typeof(DurationWheel),
            20f,
            propertyChanged: (bindable, _, newValue) =>
            {
                var control = (DurationWheel) bindable;
                control._markerWidth = (float) newValue;
                control.InvalidateSurface();
            }
        );

    /// <summary>
    /// Gets or sets the width of the markers.
    /// </summary>
    public float MarkerWidth
    {
        get => (float) GetValue(MarkerWidthProperty);
        set => SetValue(MarkerWidthProperty, value);
    }

    /// <summary>
    /// Bindable property for the MarkerSize property.
    /// </summary>
    public static readonly BindableProperty MarkerSizeProperty =
        BindableProperty.Create(
            nameof(MarkerSize),
            typeof(float),
            typeof(DurationWheel),
            2f,
            propertyChanged: (bindable, _, newValue) =>
            {
                var control = (DurationWheel) bindable;
                control._markerSize = (float) newValue;
                control.InvalidateSurface();
            }
        );

    /// <summary>
    /// Gets or sets the size of the markers.
    /// </summary>
    public float MarkerSize
    {
        get => (float) GetValue(MarkerSizeProperty);
        set => SetValue(MarkerSizeProperty, value);
    }

    /// <summary>
    /// Bindable property for the MarkerGap property.
    /// </summary>
    public static readonly BindableProperty MarkerGapProperty =
        BindableProperty.Create(
            nameof(MarkerGap),
            typeof(float),
            typeof(DurationWheel),
            12f,
            propertyChanged: (bindable, _, newValue) =>
            {
                var control = (DurationWheel) bindable;
                control._markerGap = (float) newValue;
                control.InvalidateSurface();
            }
        );

    /// <summary>
    /// Gets or sets the gap between markers.
    /// </summary>
    public float MarkerGap
    {
        get => (float) GetValue(MarkerGapProperty);
        set => SetValue(MarkerGapProperty, value);
    }

    /// <summary>
    /// Bindable property for the Duration property.
    /// </summary>
    public static readonly BindableProperty DurationProperty =
        BindableProperty.Create(
            nameof(Duration),
            typeof(TimeSpan?),
            typeof(DurationWheel),
            null,
            BindingMode.TwoWay,
            propertyChanged: (bindable, _, newValue) =>
            {
                ((DurationWheel) bindable).OnDurationChanged((TimeSpan?) newValue);
            }
        );

    /// <summary>
    /// Bindable property for the WholeDuration property.
    /// </summary>
    public static readonly BindableProperty WholeDurationProperty =
        BindableProperty.Create(
            nameof(WholeDuration),
            typeof(TimeSpan),
            typeof(DurationWheel),
            TimeSpan.FromHours(1),
            propertyChanged: InvalidateDrawing
        );

    private static void InvalidateDrawing(BindableObject bindable, object oldvalue, object newvalue)
        => ((DurationWheel) bindable).InvalidateSurface();

    /// <summary>
    /// Bindable property for the MaximumDuration property.
    /// </summary>
    public static readonly BindableProperty MaximumDurationProperty =
        BindableProperty.Create(nameof(MaximumDuration), typeof(TimeSpan?), typeof(DurationWheel));

    /// <summary>
    /// Gets or sets the selected duration. A value of TimeSpan.Zero indicates no duration.
    /// </summary>
    public TimeSpan? Duration
    {
        get => (TimeSpan?) GetValue(DurationProperty);
        set => SetValue(DurationProperty, value);
    }

    /// <summary>
    /// Gets or sets the time span corresponding to a full 360° rotation.
    /// </summary>
    public TimeSpan WholeDuration
    {
        get => (TimeSpan) GetValue(WholeDurationProperty);
        set => SetValue(WholeDurationProperty, value);
    }

    /// <summary>
    /// Gets or sets the maximum allowed duration. If null, no limit is applied.
    /// </summary>
    public TimeSpan? MaximumDuration
    {
        get => (TimeSpan?) GetValue(MaximumDurationProperty);
        set => SetValue(MaximumDurationProperty, value);
    }

    /// <summary>
    /// Triggered when the user has started rotating the wheel.
    /// </summary>
    public EventHandler? RotationStarted;

    /// <summary>
    /// Triggered when the user has finished rotating the wheel.
    /// </summary>
    public EventHandler? RotationEnded;

    /// <summary>
    /// Initializes a new instance of the <see cref="DurationWheel" /> class.
    /// </summary>
    public DurationWheel() { }

    /// <inheritdoc />
    protected override Size MeasureOverride(double widthConstraint, double heightConstraint)
    {
        if (!double.IsPositiveInfinity(widthConstraint))
        {
            if (double.IsPositiveInfinity(heightConstraint))
            {
                return new Size(widthConstraint, widthConstraint);
            }

            var size = Math.Min(widthConstraint, heightConstraint);

            return new Size(size, size);
        }

        if (double.IsPositiveInfinity(heightConstraint))
        {
            return new Size(300, 300);
        }

        return new Size(heightConstraint, heightConstraint);
    }

    /// <inheritdoc />
    protected override void OnPaintSurface(SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        var info = e.Info;
        var virtualWidth = (float) Width;
        _density = info.Width / (virtualWidth > 0 ? virtualWidth : info.Width);
        canvas.Clear();

        var arcSize = _valueWidth * _density;
        var lineWidth = _markerWidth * _density;
        var lineSize = _markerSize * _density;
        var lineGap = _markerGap * _density;

        var centerX = info.Width / 2f;
        var centerY = info.Height / 2f;
        var radius = Math.Min(centerX, centerY);
        var innerRadius = radius - arcSize;
        var angle = _angle;

        DrawArcGradient(canvas, centerX, centerY, radius, angle, arcSize);
        DrawArcHandle(canvas, centerX, centerY, radius, angle, arcSize);
        DrawInnerCircle(canvas, centerX, centerY, innerRadius, arcSize);
        DrawLines(canvas, centerX, centerY, innerRadius, angle, lineWidth, lineGap, lineSize);
    }

    private void DrawArcHandle(SKCanvas canvas, float centerX, float centerY, float radius, float angle, float arcSize)
    {
        if (angle == 0)
        {
            return;
        }

        canvas.Save();
        canvas.RotateDegrees(angle - 90.0f, centerX, centerY);
        using var paint = new SKPaint();
        paint.Color = _highValueColor;
        var handleRadius = arcSize / 2;
        centerX += radius - handleRadius;
        var circleRect = new SKRect(centerX - handleRadius, centerY - handleRadius, centerX + handleRadius, centerY + handleRadius);
        canvas.DrawArc(circleRect, 0, 180f, true, paint);
        canvas.Restore();
    }

    private void DrawLines(SKCanvas canvas, float centerX, float centerY, float innerRadius, float angle, float lineWidth, float lineGap, float lineSize)
    {
        canvas.Save();

        using var paint = new SKPaint();
        paint.Style = SKPaintStyle.Stroke;
        paint.Color = _markersColor;
        paint.StrokeWidth = lineSize;
        paint.StrokeCap = SKStrokeCap.Round;

        const int lineDegrees = 15;
        const int numberOfLines = 360 / lineDegrees;

        var rotateDegrees = angle - 90;
        var x1 = centerX + innerRadius - lineGap;
        var x0 = x1 - lineWidth;

        canvas.RotateDegrees(rotateDegrees, centerX, centerY);

        for (var i = 0; i < numberOfLines; i++)
        {
            canvas.DrawLine(x0, centerY, x1, centerY, paint);
            canvas.RotateDegrees(lineDegrees, centerX, centerY);
        }

        canvas.Restore();
    }

    private void DrawInnerCircle(SKCanvas canvas, float centerX, float centerY, float innerRadius, float arcSize)
    {
        canvas.Save();

        using var circlePaint = new SKPaint();

        // Inner circle shadow
        circlePaint.Style = SKPaintStyle.Fill;
        circlePaint.Color = _innerShadowColor;
        circlePaint.MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, arcSize / 3);
        canvas.DrawCircle(centerX, centerY + 2, innerRadius, circlePaint);

        // Inner circle
        circlePaint.Color = _innerBackgroundColor;
        circlePaint.MaskFilter = null;
        canvas.DrawCircle(centerX, centerY, innerRadius, circlePaint);
        canvas.Restore();
    }

    private void DrawArcGradient(SKCanvas canvas, float centerX, float centerY, float radius, float angle, float arcSize)
    {
        canvas.Save();

        var rotateDegrees = angle > 360 ? angle - 90 : -90;
        canvas.RotateDegrees(rotateDegrees, centerX, centerY);

        using var circlePaint = new SKPaint();

        var normalizedAngle = angle % 360;
        var floatAngle = normalizedAngle / 360f;

        using var circlePaintShader = SKShader.CreateSweepGradient(
            new SKPoint(centerX, centerY),
            [
                _lowValueColor,
                _highValueColor,
                angle >= 360 ? _lowValueColor : _outerBackgroundColor
            ],
            angle >= 360 ? [0f, 1f, 1f] : [0f, floatAngle, floatAngle]
        );

        circlePaint.Shader = circlePaintShader;
        circlePaint.StrokeWidth = arcSize;
        canvas.DrawCircle(centerX, centerY, radius, circlePaint);

        canvas.Restore();
    }

    private void OnDurationChanged(TimeSpan? newValue)
    {
        _duration = newValue;

        if (_duration.HasValue && WholeDuration.Ticks > 0)
        {
            _angle = (float) ((double) _duration.Value.Ticks / WholeDuration.Ticks * 360);
        }
        else
        {
            _angle = 0;
        }

        InvalidateSurface();
    }

    /// <inheritdoc />
    [Experimental("NLU001")]
    protected override void OnTouchPressed(TouchEventArgs args)
    {
        var position = args.Position;

        // Determine center and radius (same as in Draw)
        var centerX = Width / 2;
        var centerY = Height / 2;
        var radius = Math.Min(centerX, centerY) - 20;

        // Check if the press is within the circle.
        var dx = position.X - centerX;
        var dy = position.Y - centerY;
        var distance = Math.Sqrt((dx * dx) + (dy * dy));

        if (distance <= radius)
        {
            _isDragging = true;
            _lastPointerPosition = position;
            args.StopPropagation();
            RotationStarted?.Invoke(this, EventArgs.Empty);
        }
        else
        {
            _isDragging = false;
        }
    }

    /// <inheritdoc />
    [Experimental("NLU001")]
    protected override void OnTouchMoved(TouchEventArgs args)
    {
        if (!_isDragging)
        {
            return;
        }

        args.StopPropagation();
        var now = DateTime.Now;

        if ((now - _lastMoved).TotalMilliseconds < 16)
        {
            return;
        }

        _lastMoved = now;

        UpdateDuration(args.Position);
    }

    private void UpdateDuration(Point p)
    {
        // Determine center of the control.
        var centerX = Width / 2;
        var centerY = Height / 2;

        // Calculate angle from center for the previous and new pointer positions.
        var lastAngle = GetAngle(centerX, centerY, _lastPointerPosition.X, _lastPointerPosition.Y);
        var newAngle = GetAngle(centerX, centerY, p.X, p.Y);
        var deltaAngle = newAngle - lastAngle;

        // Handle the angle wrap-around.
        if (deltaAngle > 180)
        {
            deltaAngle -= 360;
        }
        else if (deltaAngle < -180)
        {
            deltaAngle += 360;
        }

        _angle += (float) deltaAngle;

        if (_angle < 0)
        {
            _angle = 0;
        }

        // Compute new duration based on WholeDuration.
        var newTicks = (long) (_angle / 360.0 * WholeDuration.Ticks);
        var newDuration = TimeSpan.FromTicks(newTicks);

        // If MaximumDuration is set, clamp the value.
        if (MaximumDuration.HasValue && newDuration > MaximumDuration.Value)
        {
            newDuration = MaximumDuration.Value;
            _angle = MaximumDuration.Value.Ticks / 360.0f;
        }

        Duration = newDuration;

        _lastPointerPosition = p;
        InvalidateSurface();
    }

    /// <inheritdoc />
    [Experimental("NLU001")]
    protected override void OnTouchReleased(TouchEventArgs args)
    {
        if (_isDragging)
        {
            args.StopPropagation();
            UpdateDuration(args.Position);
            _isDragging = false;
            RotationEnded?.Invoke(this, EventArgs.Empty);
        }
    }

    private static double GetAngle(double centerX, double centerY, double x, double y) => Math.Atan2(y - centerY, x - centerX) * 180 / Math.PI;
}
