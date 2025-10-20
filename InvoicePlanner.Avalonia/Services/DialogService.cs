using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Avalonia.Media;
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

            view.DataContext = viewModel;

            if (desktop.MainWindow is null)
            {
                return false;
            }

            var owner = desktop.MainWindow;
            var overlayBrush = GetResource("BrushOverlayStrong", new SolidColorBrush(Color.FromArgb(0xCC, 0x00, 0x00, 0x00)));
            var surfaceBrush = GetResource("BrushSurfaceVariant", new SolidColorBrush(Color.FromArgb(0xFF, 0x3A, 0x3A, 0x3A)));
            var borderBrush = GetResource("BrushBorder", new SolidColorBrush(Color.FromArgb(0xFF, 0x4C, 0x4C, 0x4C)));
            var contentPadding = GetResource("DialogContentPadding", new Thickness(16));
            var cornerRadius = GetResource("CornerRadiusLG", new CornerRadius(12));

            view.HorizontalAlignment = HorizontalAlignment.Stretch;
            view.VerticalAlignment = VerticalAlignment.Stretch;

            var container = new Border
            {
                Background = surfaceBrush,
                BorderBrush = borderBrush,
                BorderThickness = new Thickness(1),
                CornerRadius = cornerRadius,
                Padding = contentPadding,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Child = view
            };

            void UpdateSizing(Size size)
            {
                container.MaxWidth = Math.Max(640, size.Width * 0.9);
                container.MaxHeight = Math.Max(480, size.Height * 0.9);
            }

            UpdateSizing(owner.ClientSize);

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
                Padding = new Thickness(0)
            };

            UpdateSizing(owner.ClientSize);
            var sizeSubscription = owner.GetObservable(Window.ClientSizeProperty).Subscribe(UpdateSizing);

            try
            {
                var result = await _currentDialog.ShowDialog<bool?>(owner);
                return result ?? false;
            }
            finally
            {
                sizeSubscription?.Dispose();
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
