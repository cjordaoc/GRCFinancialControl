using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.Messaging;
using InvoicePlanner.Avalonia.Messages;
using InvoicePlanner.Avalonia.ViewModels;

namespace InvoicePlanner.Avalonia.Services
{
    public class DialogService
    {
        private readonly ViewLocator _viewLocator = new();
        private readonly Stack<Window> _dialogStack = new();

        private Window? CurrentDialog => _dialogStack.Count > 0 ? _dialogStack.Peek() : null;

        public DialogService(IMessenger messenger)
        {
            messenger.Register<CloseDialogMessage>(this, (r, m) =>
            {
                CurrentDialog?.Close(m.Value);
            });
        }

        public async Task<bool> ShowDialogAsync(ViewModelBase viewModel, string? title = null)
        {
            if (App.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
            {
                return false;
            }

            if (_viewLocator.Build(viewModel) is not UserControl view)
            {
                throw new InvalidOperationException($"Could not locate a view for the view model '{viewModel.GetType().FullName}'.");
            }

            view.HorizontalAlignment = HorizontalAlignment.Stretch;
            view.VerticalAlignment = VerticalAlignment.Stretch;
            view.DataContext = viewModel;

            if (desktop.MainWindow is null)
            {
                return false;
            }

            var owner = desktop.MainWindow;
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
            var surfaceBrush = GetResource("ModalDialogBackgroundBrush", GetResource("BrushSurfaceVariant", new SolidColorBrush(Color.FromArgb(0xFF, 0x2E, 0x2E, 0x2E))));
            var borderBrush = GetResource("BrushBorder", new SolidColorBrush(Color.FromArgb(0xFF, 0x4C, 0x4C, 0x4C)));
            var contentPadding = GetResource("ModalDialogPadding", new Thickness(24));
            var cornerRadius = GetResource("ModalDialogCornerRadius", new CornerRadius(12));
            var boxShadow = GetResource("ModalDialogShadow", BoxShadows.Parse("0 8 16 0 #66000000"));
            var containerMargin = GetResource("SpaceThickness24", new Thickness(24));

            var container = new Border
            {
                Background = surfaceBrush,
                BorderBrush = borderBrush,
                BorderThickness = new Thickness(1),
                CornerRadius = cornerRadius,
                Padding = contentPadding,
                Margin = containerMargin,
                BoxShadow = boxShadow,
                MinWidth = 360,
                MinHeight = 320,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Child = view
            };

            KeyboardNavigation.SetTabNavigation(container, KeyboardNavigationMode.Cycle);

            Window? dialog = null;

            void UpdateDialogGeometry()
            {
                if (dialog is null)
                {
                    return;
                }

                var bounds = owner.Bounds;

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

                    dialog.Width = bounds.Width;
                    dialog.Height = bounds.Height;
                    dialog.Position = owner.Position;
                }
            }

            void UpdateSizing(Size size)
            {
                if (size.Width > 0)
                {
                    var targetWidth = size.Width * 0.85;
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
                    var targetHeight = size.Height * 0.85;
                    container.Height = targetHeight;
                    container.MaxHeight = targetHeight;
                }
                else
                {
                    container.Height = double.NaN;
                    container.MaxHeight = double.PositiveInfinity;
                }

                UpdateDialogGeometry();
            }

            UpdateSizing(owner.ClientSize);

            var overlay = new ExperimentalAcrylicBorder
            {
                Material = overlayMaterial,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                Child = container
            };

            dialog = new Window
            {
                Title = title,
                Content = overlay,
                WindowStartupLocation = WindowStartupLocation.Manual,
                ShowInTaskbar = false,
                CanResize = false,
                SystemDecorations = SystemDecorations.None,
                Background = Brushes.Transparent,
                TransparencyLevelHint = new[] { WindowTransparencyLevel.Transparent },
                Padding = new Thickness(0),
                SizeToContent = SizeToContent.Manual
            };

            UpdateDialogGeometry();

            UpdateSizing(owner.ClientSize);
            var sizeSubscription = owner.GetObservable(Window.ClientSizeProperty).Subscribe(UpdateSizing);
            void OwnerPositionChanged(object? _, PixelPointEventArgs __) => UpdateDialogGeometry();
            owner.PositionChanged += OwnerPositionChanged;

            bool IsEligibleForFocus(Control control) =>
                control.Focusable && control.IsEffectivelyEnabled && control.IsEffectivelyVisible && control is not ScrollViewer;

            List<Control> GetFocusableControls()
            {
                return container
                    .GetVisualDescendants()
                    .OfType<Control>()
                    .Prepend(container)
                    .Where(IsEligibleForFocus)
                    .Distinct()
                    .ToList();
            }

            void FocusFirstElement()
            {
                var focusable = GetFocusableControls().FirstOrDefault();

                if (focusable is null)
                {
                    focusable = container.GetVisualDescendants()
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

            dialog.Opened += (_, _) =>
            {
                Dispatcher.UIThread.Post(FocusFirstElement, DispatcherPriority.Background);
                dialog.KeyDown += HandleKeyDown;
            };

            var previousDialog = CurrentDialog;
            var focusScope = previousDialog ?? owner;
            var previousFocus = focusScope.FocusManager?.GetFocusedElement();

            if (previousDialog is null)
            {
                owner.IsEnabled = false;
            }
            else
            {
                previousDialog.IsEnabled = false;
            }

            _dialogStack.Push(dialog);

            try
            {
                var result = await dialog.ShowDialog<bool?>(owner);
                return result ?? false;
            }
            finally
            {
                if (_dialogStack.Count > 0 && ReferenceEquals(_dialogStack.Peek(), dialog))
                {
                    _dialogStack.Pop();
                }

                sizeSubscription?.Dispose();
                owner.PositionChanged -= OwnerPositionChanged;
                dialog.KeyDown -= HandleKeyDown;

                var restoredDialog = CurrentDialog;

                if (restoredDialog is not null)
                {
                    restoredDialog.IsEnabled = true;
                    Dispatcher.UIThread.Post(() =>
                    {
                        if (previousFocus is Control control)
                        {
                            control.Focus();
                        }
                        else
                        {
                            restoredDialog.Focus();
                        }
                    }, DispatcherPriority.Background);
                }
                else
                {
                    owner.IsEnabled = true;
                    Dispatcher.UIThread.Post(() => previousFocus?.Focus(), DispatcherPriority.Background);
                }
            }
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
}
