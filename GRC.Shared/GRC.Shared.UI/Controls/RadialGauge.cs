using System;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;

namespace GRC.Shared.UI.Controls;

public class RadialGauge : Control
{
    public static readonly StyledProperty<double> MinimumProperty =
        AvaloniaProperty.Register<RadialGauge, double>(nameof(Minimum), 0d);

    public static readonly StyledProperty<double> MaximumProperty =
        AvaloniaProperty.Register<RadialGauge, double>(nameof(Maximum), 100d);

    public static readonly StyledProperty<double> ValueProperty =
        AvaloniaProperty.Register<RadialGauge, double>(nameof(Value), 0d);

    public static readonly StyledProperty<double> StartAngleProperty =
        AvaloniaProperty.Register<RadialGauge, double>(nameof(StartAngle), -135d);

    public static readonly StyledProperty<double> SweepAngleProperty =
        AvaloniaProperty.Register<RadialGauge, double>(nameof(SweepAngle), 270d);

    public static readonly StyledProperty<double> ArcThicknessProperty =
        AvaloniaProperty.Register<RadialGauge, double>(nameof(ArcThickness), 12d);

    public static readonly StyledProperty<double> TickFrequencyProperty =
        AvaloniaProperty.Register<RadialGauge, double>(nameof(TickFrequency), 10d);

    public static readonly StyledProperty<double> TickLengthProperty =
        AvaloniaProperty.Register<RadialGauge, double>(nameof(TickLength), 6d);

    public static readonly StyledProperty<double> TickThicknessProperty =
        AvaloniaProperty.Register<RadialGauge, double>(nameof(TickThickness), 1.5d);

    public static readonly StyledProperty<bool> ShowTicksProperty =
        AvaloniaProperty.Register<RadialGauge, bool>(nameof(ShowTicks), true);

    public static readonly StyledProperty<bool> ShowNeedleProperty =
        AvaloniaProperty.Register<RadialGauge, bool>(nameof(ShowNeedle), true);

    public static readonly StyledProperty<double> NeedleThicknessProperty =
        AvaloniaProperty.Register<RadialGauge, double>(nameof(NeedleThickness), 2d);

    public static readonly StyledProperty<bool> ShowValueProperty =
        AvaloniaProperty.Register<RadialGauge, bool>(nameof(ShowValue), true);

    public static readonly StyledProperty<double> ValueFontSizeProperty =
        AvaloniaProperty.Register<RadialGauge, double>(nameof(ValueFontSize), 18d);

    public static readonly StyledProperty<string> ValueFormatProperty =
        AvaloniaProperty.Register<RadialGauge, string>(nameof(ValueFormat), "{0:0}");

    public static readonly StyledProperty<IBrush?> BaseArcBrushProperty =
        AvaloniaProperty.Register<RadialGauge, IBrush?>(nameof(BaseArcBrush));

    public static readonly StyledProperty<IBrush?> IndicatorBrushProperty =
        AvaloniaProperty.Register<RadialGauge, IBrush?>(nameof(IndicatorBrush));

    public static readonly StyledProperty<IBrush?> TickBrushProperty =
        AvaloniaProperty.Register<RadialGauge, IBrush?>(nameof(TickBrush));

    public static readonly StyledProperty<IBrush?> NeedleBrushProperty =
        AvaloniaProperty.Register<RadialGauge, IBrush?>(nameof(NeedleBrush));

    public static readonly StyledProperty<IBrush?> ValueBrushProperty =
        AvaloniaProperty.Register<RadialGauge, IBrush?>(nameof(ValueBrush));

    static RadialGauge()
    {
        AffectsRender<RadialGauge>(
            MinimumProperty,
            MaximumProperty,
            ValueProperty,
            StartAngleProperty,
            SweepAngleProperty,
            ArcThicknessProperty,
            TickFrequencyProperty,
            TickLengthProperty,
            TickThicknessProperty,
            ShowTicksProperty,
            ShowNeedleProperty,
            NeedleThicknessProperty,
            ShowValueProperty,
            ValueFontSizeProperty,
            ValueFormatProperty,
            BaseArcBrushProperty,
            IndicatorBrushProperty,
            TickBrushProperty,
            NeedleBrushProperty,
            ValueBrushProperty);
    }

    public RadialGauge()
    {
        ClipToBounds = false;
    }

    public double Minimum
    {
        get => GetValue(MinimumProperty);
        set => SetValue(MinimumProperty, value);
    }

    public double Maximum
    {
        get => GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    public double Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public double StartAngle
    {
        get => GetValue(StartAngleProperty);
        set => SetValue(StartAngleProperty, value);
    }

    public double SweepAngle
    {
        get => GetValue(SweepAngleProperty);
        set => SetValue(SweepAngleProperty, value);
    }

    public double ArcThickness
    {
        get => GetValue(ArcThicknessProperty);
        set => SetValue(ArcThicknessProperty, value);
    }

    public double TickFrequency
    {
        get => GetValue(TickFrequencyProperty);
        set => SetValue(TickFrequencyProperty, value);
    }

    public double TickLength
    {
        get => GetValue(TickLengthProperty);
        set => SetValue(TickLengthProperty, value);
    }

    public double TickThickness
    {
        get => GetValue(TickThicknessProperty);
        set => SetValue(TickThicknessProperty, value);
    }

    public bool ShowTicks
    {
        get => GetValue(ShowTicksProperty);
        set => SetValue(ShowTicksProperty, value);
    }

    public bool ShowNeedle
    {
        get => GetValue(ShowNeedleProperty);
        set => SetValue(ShowNeedleProperty, value);
    }

    public double NeedleThickness
    {
        get => GetValue(NeedleThicknessProperty);
        set => SetValue(NeedleThicknessProperty, value);
    }

    public bool ShowValue
    {
        get => GetValue(ShowValueProperty);
        set => SetValue(ShowValueProperty, value);
    }

    public double ValueFontSize
    {
        get => GetValue(ValueFontSizeProperty);
        set => SetValue(ValueFontSizeProperty, value);
    }

    public string ValueFormat
    {
        get => GetValue(ValueFormatProperty);
        set => SetValue(ValueFormatProperty, value);
    }

    public IBrush? BaseArcBrush
    {
        get => GetValue(BaseArcBrushProperty);
        set => SetValue(BaseArcBrushProperty, value);
    }

    public IBrush? IndicatorBrush
    {
        get => GetValue(IndicatorBrushProperty);
        set => SetValue(IndicatorBrushProperty, value);
    }

    public IBrush? TickBrush
    {
        get => GetValue(TickBrushProperty);
        set => SetValue(TickBrushProperty, value);
    }

    public IBrush? NeedleBrush
    {
        get => GetValue(NeedleBrushProperty);
        set => SetValue(NeedleBrushProperty, value);
    }

    public IBrush? ValueBrush
    {
        get => GetValue(ValueBrushProperty);
        set => SetValue(ValueBrushProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var bounds = Bounds;
        var size = Math.Min(bounds.Width, bounds.Height);
        if (size <= 0)
        {
            return;
        }

        var radius = Math.Max(0, (size / 2) - Math.Max(ArcThickness, TickLength) - 4);
        if (radius <= 0)
        {
            return;
        }

        var center = bounds.Center;
        var range = Maximum - Minimum;
        if (range <= 0)
        {
            return;
        }

        var value = Math.Clamp(Value, Minimum, Maximum);
        var fraction = (value - Minimum) / range;
        var targetSweep = SweepAngle * fraction;

        var basePen = new Pen(BaseArcBrush ?? new SolidColorBrush(Color.FromRgb(0xD9, 0xD9, 0xD9)), ArcThickness)
        {
            LineCap = PenLineCap.Round,
            LineJoin = PenLineJoin.Round
        };

        var indicatorPen = new Pen(IndicatorBrush ?? new SolidColorBrush(Color.FromRgb(0x3B, 0x82, 0xF6)), ArcThickness)
        {
            LineCap = PenLineCap.Round,
            LineJoin = PenLineJoin.Round
        };

        DrawArc(context, center, radius, StartAngle, SweepAngle, basePen);
        DrawArc(context, center, radius, StartAngle, targetSweep, indicatorPen);

        if (ShowTicks)
        {
            DrawTicks(context, center, radius, value);
        }

        if (ShowNeedle)
        {
            DrawNeedle(context, center, radius, StartAngle + targetSweep);
        }

        if (ShowValue)
        {
            DrawValueText(context, center, value);
        }
    }

    private void DrawTicks(DrawingContext context, Point center, double radius, double clampedValue)
    {
        if (TickFrequency <= 0)
        {
            return;
        }

        var brush = TickBrush ?? new SolidColorBrush(Color.FromRgb(0x80, 0x80, 0x80));
        var pen = new Pen(brush, TickThickness)
        {
            LineCap = PenLineCap.Round
        };

        var range = Maximum - Minimum;
        var maxSteps = 512;
        var tickLength = Math.Max(0, TickLength);
        var stepsDrawn = 0;

        for (var current = Minimum; current <= Maximum + (TickFrequency / 2); current += TickFrequency)
        {
            if (stepsDrawn++ > maxSteps)
            {
                break;
            }

            var fraction = (current - Minimum) / range;
            var angle = StartAngle + (SweepAngle * fraction);
            var outer = GetPointOnCircle(center, radius + (ArcThickness / 2) + 2, angle);
            var inner = GetPointOnCircle(center, radius + (ArcThickness / 2) + 2 - tickLength, angle);
            context.DrawLine(pen, inner, outer);
        }

        var lastTickAngle = StartAngle + (SweepAngle * ((clampedValue - Minimum) / range));
        var highlightPen = new Pen(IndicatorBrush ?? brush, TickThickness + 0.5)
        {
            LineCap = PenLineCap.Round
        };
        var lastOuter = GetPointOnCircle(center, radius + (ArcThickness / 2) + 2, lastTickAngle);
        var lastInner = GetPointOnCircle(center, radius + (ArcThickness / 2) + 2 - tickLength, lastTickAngle);
        context.DrawLine(highlightPen, lastInner, lastOuter);
    }

    private void DrawNeedle(DrawingContext context, Point center, double radius, double angle)
    {
        var brush = NeedleBrush ?? new SolidColorBrush(Color.FromRgb(0x3B, 0x82, 0xF6));
        var pen = new Pen(brush, NeedleThickness)
        {
            LineCap = PenLineCap.Round
        };

        var needleLength = Math.Max(0, radius - (ArcThickness / 2));
        var tip = GetPointOnCircle(center, needleLength, angle);
        context.DrawLine(pen, center, tip);

        var hubRadius = Math.Max(NeedleThickness * 1.5, 4);
        var hubRect = new Rect(center.X - hubRadius, center.Y - hubRadius, hubRadius * 2, hubRadius * 2);
        context.DrawGeometry(brush, null, new EllipseGeometry(hubRect));
    }

    private void DrawValueText(DrawingContext context, Point center, double value)
    {
        var brush = ValueBrush ?? new SolidColorBrush(Color.FromRgb(0x17, 0x17, 0x17));
        var format = string.IsNullOrWhiteSpace(ValueFormat) ? "{0:0}" : ValueFormat;
        var text = string.Format(CultureInfo.InvariantCulture, format, value);

        var typeface = new Typeface(FontFamily.Default, FontStyle.Normal, FontWeight.SemiBold);
        var layout = new TextLayout(text, typeface, ValueFontSize, brush);
        var origin = new Point(center.X - (layout.Width / 2), center.Y - (layout.Height / 2));
        layout.Draw(context, origin);
    }

    private static void DrawArc(DrawingContext context, Point center, double radius, double sweepStartAngle, double sweepAngle, Pen pen)
    {
        if (Math.Abs(sweepAngle) < 0.001)
        {
            return;
        }

        var startPoint = GetPointOnCircle(center, radius, sweepStartAngle);
        var endPoint = GetPointOnCircle(center, radius, sweepStartAngle + sweepAngle);
        var isLargeArc = Math.Abs(sweepAngle) > 180;
        var sweepDirection = sweepAngle >= 0 ? SweepDirection.Clockwise : SweepDirection.CounterClockwise;

        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(startPoint, false);
            ctx.ArcTo(endPoint, new Size(radius, radius), 0, isLargeArc, sweepDirection);
            ctx.EndFigure(false);
        }

        context.DrawGeometry(null, pen, geometry);
    }

    private static Point GetPointOnCircle(Point center, double radius, double angleDegrees)
    {
        var radians = angleDegrees * Math.PI / 180d;
        var x = center.X + (radius * Math.Cos(radians));
        var y = center.Y + (radius * Math.Sin(radians));
        return new Point(x, y);
    }
}
