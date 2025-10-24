using System;
using System.Text;
using Avalonia;
using Avalonia.Controls;

namespace InvoicePlanner.Avalonia.Behaviors;

public class CnpjMaskBehavior
{
    public static readonly AttachedProperty<bool> IsEnabledProperty =
        AvaloniaProperty.RegisterAttached<CnpjMaskBehavior, TextBox, bool>("IsEnabled");

    private static readonly AttachedProperty<bool> IsUpdatingProperty =
        AvaloniaProperty.RegisterAttached<CnpjMaskBehavior, TextBox, bool>("IsUpdating");

    static CnpjMaskBehavior()
    {
        IsEnabledProperty.Changed.AddClassHandler<TextBox>((textBox, args) =>
        {
            if (args.NewValue is bool isEnabled)
            {
                OnIsEnabledChanged(textBox, isEnabled);
            }
        });
    }

    public static void SetIsEnabled(TextBox element, bool value) => element.SetValue(IsEnabledProperty, value);

    public static bool GetIsEnabled(TextBox element) => element.GetValue(IsEnabledProperty);

    private static void OnIsEnabledChanged(TextBox textBox, bool isEnabled)
    {
        if (isEnabled)
        {
            textBox.AddHandler(TextBox.TextChangedEvent, TextBoxOnTextChanged);
            ApplyMask(textBox);
        }
        else
        {
            textBox.RemoveHandler(TextBox.TextChangedEvent, TextBoxOnTextChanged);
        }
    }

    private static void TextBoxOnTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (sender is TextBox textBox)
        {
            ApplyMask(textBox);
        }
    }

    private static void ApplyMask(TextBox textBox)
    {
        if (GetIsUpdating(textBox))
        {
            return;
        }

        var formatted = Format(textBox.Text);

        if (string.Equals(textBox.Text, formatted, StringComparison.Ordinal))
        {
            return;
        }

        try
        {
            SetIsUpdating(textBox, true);
            textBox.Text = formatted;
            var caret = formatted.Length;
            textBox.CaretIndex = caret;
            textBox.SelectionStart = caret;
            textBox.SelectionEnd = caret;
        }
        finally
        {
            SetIsUpdating(textBox, false);
        }
    }

    private static string Format(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        Span<char> digits = stackalloc char[14];
        var length = 0;

        foreach (var character in value)
        {
            if (!char.IsDigit(character))
            {
                continue;
            }

            digits[length++] = character;

            if (length == digits.Length)
            {
                break;
            }
        }

        if (length == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder(length + 4);

        for (var i = 0; i < length; i++)
        {
            if (i == 2 || i == 5)
            {
                builder.Append('.');
            }
            else if (i == 8)
            {
                builder.Append('/');
            }
            else if (i == 12)
            {
                builder.Append('-');
            }

            builder.Append(digits[i]);
        }

        return builder.ToString();
    }

    private static void SetIsUpdating(TextBox element, bool value) => element.SetValue(IsUpdatingProperty, value);

    private static bool GetIsUpdating(TextBox element) => element.GetValue(IsUpdatingProperty);
}
