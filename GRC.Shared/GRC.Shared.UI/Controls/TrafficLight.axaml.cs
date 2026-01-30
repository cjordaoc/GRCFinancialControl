using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using GRC.Shared.Core.Enums;

namespace GRC.Shared.UI.Controls;

/// <summary>
/// Defines the operating mode for the TrafficLight control.
/// Numeric: Control expects a numeric Value and automatically calculates status based on boundaries.
/// String: Control expects a direct status string value (Green, Yellow, or Red).
/// </summary>
public enum TrafficLightMode
{
    /// <summary>Numeric mode: Calculate status from Value and boundaries (GreenUpperBound, YellowUpperBound).</summary>
    Numeric = 0,
    
    /// <summary>String mode: Use direct status value from Status property.</summary>
    String = 1
}

/// <summary>
/// Traffic light indicator control that displays status based on numeric values with customizable color boundaries.
/// Automatically calculates color (Green, Yellow, Red) based on value and threshold settings.
/// Includes auto-generated accessibility tooltip showing the color name.
/// Suitable for use in DataGrids, lists, and any layout requiring visual status indication.
/// Follows Avalonia control patterns with full StyledProperty support for styling and templating.
/// </summary>
public partial class TrafficLight : UserControl
{
    /// <summary>
    /// Gets or sets the operating mode for the control.
    /// Numeric (default): Expects a numeric Value and calculates status from boundaries.
    /// String: Expects a direct status value (Green, Yellow, Red).
    /// </summary>
    public static readonly StyledProperty<TrafficLightMode> ModeProperty =
        AvaloniaProperty.Register<TrafficLight, TrafficLightMode>(nameof(Mode), TrafficLightMode.Numeric);

    /// <summary>
    /// Gets or sets the direct status value when Mode is String.
    /// When Mode is Numeric, this property is ignored; use Value instead.
    /// Expected values: "Green", "Yellow", "Red" (case-insensitive parsing supported).
    /// </summary>
    public static readonly StyledProperty<string> StatusProperty =
        AvaloniaProperty.Register<TrafficLight, string>(nameof(Status), "Green");

    /// <summary>
    /// Gets or sets the numeric value to evaluate for color determination.
    /// Only used when Mode is Numeric.
    /// The control automatically calculates status based on this value and boundary properties.
    /// </summary>
    public static readonly StyledProperty<double> ValueProperty =
        AvaloniaProperty.Register<TrafficLight, double>(nameof(Value), 0.0);

    /// <summary>
    /// Gets or sets the upper boundary for Green status (0 to GreenUpperBound = Green).
    /// Default: 50 (meaning 0-50 is Green, 50-85 is Yellow, 85+ is Red).
    /// </summary>
    public static readonly StyledProperty<double> GreenUpperBoundProperty =
        AvaloniaProperty.Register<TrafficLight, double>(nameof(GreenUpperBound), 50.0);

    /// <summary>
    /// Gets or sets the upper boundary for Yellow status (GreenUpperBound to YellowUpperBound = Yellow).
    /// Default: 85 (meaning values 50-85 are Yellow, 85+ are Red).
    /// </summary>
    public static readonly StyledProperty<double> YellowUpperBoundProperty =
        AvaloniaProperty.Register<TrafficLight, double>(nameof(YellowUpperBound), 85.0);

    /// <summary>
    /// Gets or sets the visual symbol/indicator displayed in the center.
    /// </summary>
    public static readonly StyledProperty<string> SymbolProperty =
        AvaloniaProperty.Register<TrafficLight, string>(nameof(Symbol), "●");

    /// <summary>
    /// Gets or sets the color brush used when status is Green (value ≤ GreenUpperBound).
    /// </summary>
    public static readonly StyledProperty<IBrush> GreenBrushProperty =
        AvaloniaProperty.Register<TrafficLight, IBrush>(nameof(GreenBrush), new SolidColorBrush(Color.Parse("#28a745")));

    /// <summary>
    /// Gets or sets the color brush used when status is Yellow/Warning (GreenUpperBound &lt; value ≤ YellowUpperBound).
    /// </summary>
    public static readonly StyledProperty<IBrush> YellowBrushProperty =
        AvaloniaProperty.Register<TrafficLight, IBrush>(nameof(YellowBrush), new SolidColorBrush(Color.Parse("#ffc107")));

    /// <summary>
    /// Gets or sets the color brush used when status is Red/Error (value &gt; YellowUpperBound).
    /// </summary>
    public static readonly StyledProperty<IBrush> RedBrushProperty =
        AvaloniaProperty.Register<TrafficLight, IBrush>(nameof(RedBrush), new SolidColorBrush(Color.Parse("#dc3545")));

    /// <summary>
    /// Gets or sets the size (diameter) of the indicator circle.
    /// </summary>
    public static readonly StyledProperty<double> SizeProperty =
        AvaloniaProperty.Register<TrafficLight, double>(nameof(Size), 24.0);

    /// <summary>
    /// Gets or sets the font size of the symbol text.
    /// </summary>
    public static readonly StyledProperty<double> SymbolFontSizeProperty =
        AvaloniaProperty.Register<TrafficLight, double>(nameof(SymbolFontSize), 14.0);

    /// <summary>
    /// Gets the calculated traffic light status based on the current value and boundaries.
    /// Read-only, automatically updated when Value, GreenUpperBound, or YellowUpperBound changes.
    /// </summary>
    public static readonly StyledProperty<TrafficLightStatus> CalculatedStatusProperty =
        AvaloniaProperty.Register<TrafficLight, TrafficLightStatus>(nameof(CalculatedStatus), TrafficLightStatus.Green);

    /// <summary>
    /// Gets the current color name (Green, Yellow, or Red) for accessibility purposes.
    /// Read-only, automatically updated based on CalculatedStatus.
    /// </summary>
    public static readonly StyledProperty<string> ColorNameProperty =
        AvaloniaProperty.Register<TrafficLight, string>(nameof(ColorName), "Green");

    public TrafficLight()
    {
        InitializeComponent();
        
        // Wire up property change handlers for automatic status calculation
        PropertyChanged += (s, e) =>
        {
            if (e.Property == ModeProperty || e.Property == StatusProperty || 
                e.Property == ValueProperty || e.Property == GreenUpperBoundProperty || 
                e.Property == YellowUpperBoundProperty)
            {
                RecalculateStatus();
            }
        };
    }

    public double Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public TrafficLightMode Mode
    {
        get => GetValue(ModeProperty);
        set => SetValue(ModeProperty, value);
    }

    public string Status
    {
        get => GetValue(StatusProperty);
        set => SetValue(StatusProperty, value);
    }

    public double GreenUpperBound
    {
        get => GetValue(GreenUpperBoundProperty);
        set => SetValue(GreenUpperBoundProperty, value);
    }

    public double YellowUpperBound
    {
        get => GetValue(YellowUpperBoundProperty);
        set => SetValue(YellowUpperBoundProperty, value);
    }

    public string Symbol
    {
        get => GetValue(SymbolProperty);
        set => SetValue(SymbolProperty, value);
    }

    public IBrush GreenBrush
    {
        get => GetValue(GreenBrushProperty);
        set => SetValue(GreenBrushProperty, value);
    }

    public IBrush YellowBrush
    {
        get => GetValue(YellowBrushProperty);
        set => SetValue(YellowBrushProperty, value);
    }

    public IBrush RedBrush
    {
        get => GetValue(RedBrushProperty);
        set => SetValue(RedBrushProperty, value);
    }

    public double Size
    {
        get => GetValue(SizeProperty);
        set => SetValue(SizeProperty, value);
    }

    public double SymbolFontSize
    {
        get => GetValue(SymbolFontSizeProperty);
        set => SetValue(SymbolFontSizeProperty, value);
    }

    public TrafficLightStatus CalculatedStatus
    {
        get => GetValue(CalculatedStatusProperty);
        set => SetValue(CalculatedStatusProperty, value);
    }

    public string ColorName
    {
        get => GetValue(ColorNameProperty);
        set => SetValue(ColorNameProperty, value);
    }

    /// <summary>
    /// Recalculates the traffic light status based on current mode and property values.
    /// In Numeric mode: Automatically calculates status from Value and boundaries.
    /// In String mode: Parses and uses the Status property directly.
    /// Automatically updates CalculatedStatus and ColorName properties.
    /// </summary>
    private void RecalculateStatus()
    {
        TrafficLightStatus newStatus;

        if (Mode == TrafficLightMode.String)
        {
            // String mode: parse Status property directly
            if (Enum.TryParse<TrafficLightStatus>(Status, ignoreCase: true, out var parsedStatus))
            {
                newStatus = parsedStatus;
            }
            else
            {
                // Fallback to Green if parsing fails
                newStatus = TrafficLightStatus.Green;
            }
        }
        else
        {
            // Numeric mode: calculate from Value and boundaries
            newStatus = CalculateStatus(Value, GreenUpperBound, YellowUpperBound);
        }

        SetValue(CalculatedStatusProperty, newStatus);
        SetValue(ColorNameProperty, GetColorName(newStatus));
    }

    /// <summary>
    /// Determines the traffic light status based on the provided value and boundaries.
    /// </summary>
    private static TrafficLightStatus CalculateStatus(double value, double greenUpperBound, double yellowUpperBound)
    {
        if (value <= greenUpperBound)
        {
            return TrafficLightStatus.Green;
        }
        
        if (value <= yellowUpperBound)
        {
            return TrafficLightStatus.Yellow;
        }
        
        return TrafficLightStatus.Red;
    }

    /// <summary>
    /// Returns the human-readable color name for accessibility purposes.
    /// </summary>
    private static string GetColorName(TrafficLightStatus status) => status switch
    {
        TrafficLightStatus.Green => "Green",
        TrafficLightStatus.Yellow => "Yellow",
        TrafficLightStatus.Red => "Red",
        _ => "Unknown"
    };
}
