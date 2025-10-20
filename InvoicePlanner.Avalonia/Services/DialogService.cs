using System;
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
using InvoicePlanner.Avalonia.Services.Interfaces;
using InvoicePlanner.Avalonia.ViewModels;

namespace InvoicePlanner.Avalonia.Services
{
    public class DialogService : IDialogService
    {
        private readonly ViewLocator _viewLocator = new();
        private Window? _currentDialog;

        public DialogService(IMessenger messenger)
        {
            messenger.Register<CloseDialogMessage>(this, (r, m) =>
            {
                _currentDialog?.Close(m.Value);
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
            var surfaceBrush = GetResource("ModalDialogBackgroundBrush", GetResource("BrushSurfaceVariant", new SolidColorBrush(Color.FromArgb(0xFF, 0x2E, 0x2E, 0x2E))));
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

            void UpdateDialogGeometry()
            {
                if (_currentDialog is null)
                {
                    return;
                }

                var bounds = owner.Bounds;
                _currentDialog.Width = bounds.Width;
                _currentDialog.Height = bounds.Height;
                _currentDialog.Position = owner.Position;
            }

            void UpdateSizing(Size size)
            {
                container.MaxWidth = size.Width > 0 ? size.Width * 0.55 : double.PositiveInfinity;
                container.MaxHeight = size.Height > 0 ? size.Height * 0.55 : double.PositiveInfinity;
                UpdateDialogGeometry();
            }

            UpdateSizing(owner.ClientSize);

            var overlay = new Grid
            {
                Background = overlayBrush,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
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
                owner.PositionChanged -= OwnerPositionChanged;
                if (_currentDialog is not null)
                {
                    _currentDialog.KeyDown -= HandleKeyDown;
                }
                Dispatcher.UIThread.Post(() => previousFocus?.Focus(), DispatcherPriority.Background);
                _currentDialog = null;
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
