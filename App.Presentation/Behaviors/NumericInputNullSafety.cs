using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace App.Presentation.Behaviors;

public static class NumericInputNullSafety
{
    public static readonly AttachedProperty<bool> IsEnabledProperty =
        AvaloniaProperty.RegisterAttached<Interactive, bool>(
            "IsEnabled",
            typeof(NumericInputNullSafety));

    static NumericInputNullSafety() =>
        IsEnabledProperty.Changed.AddClassHandler<Interactive>((interactive, change) => OnIsEnabledChanged(interactive, change));

    public static void SetIsEnabled(AvaloniaObject element, bool value) => element.SetValue(IsEnabledProperty, value);

    public static bool GetIsEnabled(AvaloniaObject element) => element.GetValue(IsEnabledProperty);

    private static void OnIsEnabledChanged(Interactive interactive, AvaloniaPropertyChangedEventArgs change)
    {
        if (change.Property != IsEnabledProperty)
        {
            return;
        }

        if (change.GetNewValue<bool>())
        {
            interactive.AddHandler(InputElement.LostFocusEvent, OnLostFocus, RoutingStrategies.Tunnel | RoutingStrategies.Bubble);
            return;
        }

        interactive.RemoveHandler(InputElement.LostFocusEvent, OnLostFocus);
    }

    private static void OnLostFocus(object? sender, RoutedEventArgs e)
    {
        if (sender is TextBox textBox)
        {
            if (string.IsNullOrWhiteSpace(textBox.Text))
            {
                textBox.Text = "0";
            }

            return;
        }

        if (sender is NumericUpDown numericUpDown)
        {
            if (!numericUpDown.Value.HasValue)
            {
                numericUpDown.Value = 0;
            }
        }
    }
}
