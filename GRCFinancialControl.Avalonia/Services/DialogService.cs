using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Avalonia.Input;
using CommunityToolkit.Mvvm.Messaging;
using GRCFinancialControl.Avalonia.Messages;
using GRCFinancialControl.Avalonia.Services.Interfaces;
using GRCFinancialControl.Avalonia.ViewModels;
using GRCFinancialControl.Avalonia.ViewModels.Dialogs;

namespace GRCFinancialControl.Avalonia.Services
{
    public class DialogService : IDialogService
    {
        private readonly IMessenger _messenger;
        private readonly ViewLocator _viewLocator = new();
        private Window? _currentDialog;

        public DialogService(IMessenger messenger)
        {
            _messenger = messenger;
            _messenger.Register<CloseDialogMessage>(this, (recipient, message) =>
            {
                _currentDialog?.Close(message.Value);
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
            var surfaceBrush = GetResource(
                "ModalDialogBackgroundBrush",
                GetResource("BrushSurfaceVariant", new SolidColorBrush(Color.FromArgb(0xFF, 0x2E, 0x2E, 0x2E))));
            var borderBrush = GetResource("BrushBorder", new SolidColorBrush(Color.FromArgb(0xFF, 0x4C, 0x4C, 0x4C)));
            var contentPadding = GetResource("ModalDialogPadding", new Thickness(24));
            var cornerRadius = GetResource("ModalDialogCornerRadius", new CornerRadius(12));
            var boxShadowResource = GetResource<object>("ModalDialogShadow", "0 4 24 0 #66000000");
            var boxShadow = boxShadowResource switch
            {
                string shadowString => BoxShadows.Parse(shadowString),
                BoxShadows shadows => shadows,
                _ => BoxShadows.Parse("0 4 24 0 #66000000")
            };
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

            var overlay = new Grid
            {
                Background = overlayBrush,
                Children = { container }
            };

            _currentDialog = new Window
            {
                Title = title,
                Content = overlay,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                ShowInTaskbar = false,
                CanResize = false,
                SystemDecorations = SystemDecorations.None,
                Background = Brushes.Transparent,
                TransparencyLevelHint = new[] { WindowTransparencyLevel.Transparent },
                SizeToContent = SizeToContent.Manual,
                Padding = new Thickness(0)
            };

            void UpdateSizing(Size size)
            {
                var ownerWidth = size.Width > 0 ? size.Width : owner.Bounds.Width;
                var ownerHeight = size.Height > 0 ? size.Height : owner.Bounds.Height;

                if (ownerWidth > 0)
                {
                    var targetWidth = ownerWidth * 0.85;
                    container.MaxWidth = targetWidth;
                    container.Width = targetWidth;
                }
                else
                {
                    container.MaxWidth = double.PositiveInfinity;
                    container.Width = double.NaN;
                }

                if (ownerHeight > 0)
                {
                    var targetHeight = ownerHeight * 0.85;
                    container.MaxHeight = targetHeight;
                    container.Height = targetHeight;
                }
                else
                {
                    container.MaxHeight = double.PositiveInfinity;
                    container.Height = double.NaN;
                }

                if (_currentDialog is { } window)
                {
                    if (ownerWidth > 0)
                    {
                        window.Width = ownerWidth;
                    }

                    if (ownerHeight > 0)
                    {
                        window.Height = ownerHeight;
                    }
                }
            }

            UpdateSizing(owner.ClientSize);
            var sizeSubscription = owner.GetObservable(Window.ClientSizeProperty).Subscribe(UpdateSizing);

            bool IsEligibleForFocus(Control control) =>
                control.Focusable && control.IsEffectivelyEnabled && control.IsEffectivelyVisible && control is not ScrollViewer;

            System.Collections.Generic.List<Control> GetFocusableControls()
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

                var current = TopLevel.GetTopLevel(_currentDialog)?.FocusManager?.GetFocusedElement() as Control;
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

            _currentDialog.Opened += (_, _) =>
            {
                Dispatcher.UIThread.Post(FocusFirstElement, DispatcherPriority.Background);
                _currentDialog.KeyDown += HandleKeyDown;
            };

            var previousFocus = owner.FocusManager?.GetFocusedElement();

            try
            {
                owner.IsEnabled = false;
                var result = await _currentDialog.ShowDialog<bool?>(owner);
                return result ?? false;
            }
            finally
            {
                owner.IsEnabled = true;
                sizeSubscription?.Dispose();

                if (_currentDialog is not null)
                {
                    _currentDialog.KeyDown -= HandleKeyDown;
                }

                Dispatcher.UIThread.Post(() => previousFocus?.Focus(), DispatcherPriority.Background);
                _currentDialog = null;
            }
        }

        public Task<bool> ShowConfirmationAsync(string title, string message)
        {
            var confirmationViewModel = new ConfirmationDialogViewModel(title, message, _messenger);
            return ShowDialogAsync(confirmationViewModel, title);
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
