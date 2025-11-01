using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace GRC.Shared.UI.Dialogs;

public sealed class ModalDialogService : IModalDialogService
{
    private static readonly Thickness DefaultMargin = new(24);

    public ModalDialogSession Create(Window owner, Control view, string? title = null, ModalDialogOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(view);

        var layout = options?.Layout ?? ModalDialogLayout.CenteredOverlay;
        var ratio = options?.ContentSizeRatio ?? 0.85d;
        var margin = options?.ContainerMargin ?? DefaultMargin;

        view.HorizontalAlignment = HorizontalAlignment.Stretch;
        view.VerticalAlignment = VerticalAlignment.Stretch;

        var overlayBrush = GetResource("ModalOverlayBrush", new SolidColorBrush(Color.FromArgb(0x8C, 0x00, 0x00, 0x00)));
        var overlayMaterial = GetResource(
            "ModalOverlayMaterial",
            new ExperimentalAcrylicMaterial
            {
                BackgroundSource = AcrylicBackgroundSource.Digger,
                TintColor = Color.FromArgb(0xAA, 0x00, 0x00, 0x00),
                TintOpacity = 0.4,
                MaterialOpacity = 1,
                FallbackColor = overlayBrush.Color
            });
        overlayMaterial.FallbackColor = overlayBrush.Color;

        var container = new Border
        {
            Margin = margin,
            MinWidth = 360,
            MinHeight = 320,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Child = view
        };
        container.Classes.Add("ModalDialog");

        KeyboardNavigation.SetTabNavigation(container, KeyboardNavigationMode.Cycle);

        var overlay = new ExperimentalAcrylicBorder
        {
            Material = overlayMaterial,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Child = container
        };

        var dialog = new Window
        {
            Title = title,
            Content = overlay,
            ShowInTaskbar = false,
            CanResize = false,
            SystemDecorations = SystemDecorations.None,
            Background = Brushes.Transparent,
            TransparencyLevelHint = new[] { WindowTransparencyLevel.Transparent },
            Padding = new Thickness(0),
            SizeToContent = SizeToContent.Manual,
            WindowStartupLocation = layout == ModalDialogLayout.CenteredOverlay
                ? WindowStartupLocation.CenterOwner
                : WindowStartupLocation.Manual
        };

        void ApplySizing(Size size)
        {
            if (size.Width > 0)
            {
                var targetWidth = size.Width * ratio;
                container.Width = targetWidth;
                container.MaxWidth = targetWidth;
            }
            else
            {
                container.Width = double.NaN;
                container.MaxWidth = double.PositiveInfinity;
            }

            if (size.Height > 0)
            {
                var targetHeight = size.Height * ratio;
                container.Height = targetHeight;
                container.MaxHeight = targetHeight;
            }
            else
            {
                container.Height = double.NaN;
                container.MaxHeight = double.PositiveInfinity;
            }
        }

        void SyncDialogWithOwner()
        {
            switch (layout)
            {
                case ModalDialogLayout.CenteredOverlay:
                    if (owner.Bounds.Width > 0)
                    {
                        dialog.Width = owner.Bounds.Width;
                    }

                    if (owner.Bounds.Height > 0)
                    {
                        dialog.Height = owner.Bounds.Height;
                    }

                    break;
                case ModalDialogLayout.OwnerAligned:
                    if (owner.WindowState == WindowState.Maximized)
                    {
                        if (dialog.WindowState != WindowState.Maximized)
                        {
                            dialog.WindowState = WindowState.Maximized;
                        }

                        dialog.Position = owner.Position;
                    }
                    else
                    {
                        if (dialog.WindowState != WindowState.Normal)
                        {
                            dialog.WindowState = WindowState.Normal;
                        }

                        dialog.Width = owner.Bounds.Width;
                        dialog.Height = owner.Bounds.Height;
                        dialog.Position = owner.Position;
                    }

                    break;
            }
        }

        ApplySizing(owner.ClientSize);
        SyncDialogWithOwner();

        var cleanupActions = new List<Action>();

        void OwnerPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property == Window.ClientSizeProperty && e.NewValue is Size newSize)
            {
                ApplySizing(newSize);
                SyncDialogWithOwner();
            }
        }

        owner.PropertyChanged += OwnerPropertyChanged;
        cleanupActions.Add(() => owner.PropertyChanged -= OwnerPropertyChanged);

        if (layout == ModalDialogLayout.OwnerAligned)
        {
            void OwnerPositionChanged(object? sender, PixelPointEventArgs e) => SyncDialogWithOwner();
            owner.PositionChanged += OwnerPositionChanged;
            cleanupActions.Add(() => owner.PositionChanged -= OwnerPositionChanged);
        }

        List<Control> GetFocusableControls()
        {
            return container
                .GetVisualDescendants()
                .OfType<Control>()
                .Prepend(container)
                .Where(static control => control.Focusable && control.IsEffectivelyEnabled && control.IsEffectivelyVisible && control is not ScrollViewer)
                .Distinct()
                .ToList();
        }

        void FocusFirstElement()
        {
            var focusable = GetFocusableControls().FirstOrDefault();

            if (focusable is null)
            {
                focusable = container
                    .GetVisualDescendants()
                    .OfType<Button>()
                    .FirstOrDefault(button => button.IsCancel);
            }

            focusable?.Focus();
        }

        void HandleKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key != Key.Tab)
            {
                return;
            }

            var focusables = GetFocusableControls();

            if (focusables.Count == 0)
            {
                return;
            }

            var current = TopLevel.GetTopLevel(dialog)?.FocusManager?.GetFocusedElement() as Control;
            var currentIndex = current is not null ? focusables.IndexOf(current) : -1;

            if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
            {
                if (currentIndex <= 0)
                {
                    focusables[^1].Focus();
                    e.Handled = true;
                }

                return;
            }

            if (currentIndex == -1 || currentIndex >= focusables.Count - 1)
            {
                focusables[0].Focus();
                e.Handled = true;
            }
        }

        return new ModalDialogSession(dialog, FocusFirstElement, HandleKeyDown, cleanupActions);
    }

    private static T GetResource<T>(string key, T fallback)
    {
        if (Application.Current is { } app && app.TryFindResource(key, out var resource) && resource is T typed)
        {
            return typed;
        }

        return fallback;
    }
}
