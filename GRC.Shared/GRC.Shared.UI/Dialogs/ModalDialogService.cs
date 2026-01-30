using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace GRC.Shared.UI.Dialogs;

/// <summary>
/// Creates modal dialog windows with acrylic backgrounds and shared sizing rules.
/// Provides centered and owner-aligned layouts for both desktop applications.
/// </summary>
public class ModalDialogService : IModalDialogService
{
    private const string OverlayBrushKey = "BrushOverlay";
    private const string DialogBackgroundBrushKey = "BrushSurface";
    private const string DialogBorderBrushKey = "BrushBorder";
    private const double DefaultCornerRadius = 8;

    /// <summary>
    /// Creates a modal dialog window with the specified content and layout options.
    /// </summary>
    public ModalDialogSession Create(Window owner, Control view, string? title = null, ModalDialogOptions? options = null)
    {
        options ??= new ModalDialogOptions();
        var contentRatio = ResolveContentRatio(owner, options);
        var resolvedTitle = title ?? options.Title ?? string.Empty;

        var dialog = new Window
        {
            Title = resolvedTitle,
            Content = CreateDialogContent(owner, view, options),
            WindowStartupLocation = GetStartupLocation(options.Layout),
            CanResize = false,
            ShowInTaskbar = false,
            Topmost = true,
            Width = Math.Max(300, owner.Width * contentRatio),
            Height = Math.Max(200, owner.Height * contentRatio)
        };

        dialog.RequestedThemeVariant = owner.ActualThemeVariant;
        dialog.SystemDecorations = options.ShowWindowControls ? SystemDecorations.Full : SystemDecorations.None;

        // Set background with semi-transparency for acrylic effect
        dialog.Background = options.DimBackground
            ? GetRequiredBrush(owner, OverlayBrushKey)
            : Brushes.Transparent;

        var cleanupActions = new List<Action>();
        var focusFirstElement = CreateFocusHelper(view);
        var keyDownHandler = CreateKeyDownHandler(dialog);

        // Register owner closing event to close dialog
        var ownerClosingHandler = new EventHandler<WindowClosingEventArgs>((_, _) => dialog.Close());
        owner.Closing += ownerClosingHandler;
        cleanupActions.Add(() => owner.Closing -= ownerClosingHandler);

        return new ModalDialogSession(dialog, focusFirstElement, keyDownHandler, cleanupActions);
    }

    private static Control CreateDialogContent(Window owner, Control view, ModalDialogOptions options)
    {
        var margin = options.ContainerMargin ?? new Thickness(20);

        return new Border
        {
            Padding = margin,
            CornerRadius = new CornerRadius(DefaultCornerRadius),
            BorderBrush = GetRequiredBrush(owner, DialogBorderBrushKey),
            BorderThickness = new Thickness(1),
            Background = GetRequiredBrush(owner, DialogBackgroundBrushKey),
            Child = view
        };
    }

    private static WindowStartupLocation GetStartupLocation(ModalDialogLayout layout)
    {
        return layout switch
        {
            ModalDialogLayout.CenteredOverlay => WindowStartupLocation.CenterScreen,
            ModalDialogLayout.OwnerAligned => WindowStartupLocation.CenterOwner,
            _ => WindowStartupLocation.CenterScreen
        };
    }

    private static Action CreateFocusHelper(Control view)
    {
        return () =>
        {
            if (view is IInputElement inputElement)
            {
                inputElement.Focus();
            }
        };
    }

    private static EventHandler<KeyEventArgs> CreateKeyDownHandler(Window dialog)
    {
        return (_, args) =>
        {
            if (args.Key == Key.Escape)
            {
                dialog.Close();
                args.Handled = true;
            }
        };
    }

    private static IBrush GetRequiredBrush(Window owner, string resourceKey)
    {
        if (owner.TryFindResource(resourceKey, null, out var resource) && resource is IBrush ownerBrush)
        {
            return ownerBrush;
        }

        if (Application.Current?.Resources.TryGetResource(resourceKey, null, out resource) ?? false)
        {
            if (resource is IBrush appBrush)
            {
                return appBrush;
            }
        }

        throw new InvalidOperationException($"Required brush resource '{resourceKey}' was not found.");
    }

    private static double ResolveContentRatio(Window owner, ModalDialogOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.SizeRatioResourceKey))
        {
            return GetRequiredDouble(owner, options.SizeRatioResourceKey!);
        }

        return options.ContentSizeRatio;
    }

    private static double GetRequiredDouble(Window owner, string resourceKey)
    {
        if (owner.TryFindResource(resourceKey, null, out var resource) && resource is double ownerValue)
        {
            return ownerValue;
        }

        if (Application.Current?.Resources.TryGetResource(resourceKey, null, out resource) ?? false)
        {
            if (resource is double appValue)
            {
                return appValue;
            }
        }

        throw new InvalidOperationException($"Required double resource '{resourceKey}' was not found.");
    }
}
