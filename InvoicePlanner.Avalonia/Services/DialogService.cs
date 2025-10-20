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

            view.HorizontalAlignment = HorizontalAlignment.Stretch;
            view.VerticalAlignment = VerticalAlignment.Stretch;
            view.DataContext = viewModel;

            if (desktop.MainWindow is null)
            {
                return false;
            }

            var owner = desktop.MainWindow;
            var overlayBrush = GetResource("ModalOverlayBrush", new SolidColorBrush(Color.FromArgb(0xAA, 0x00, 0x00, 0x00)));
            var surfaceBrush = GetResource("ModalDialogBackgroundBrush", new SolidColorBrush(Color.FromArgb(0xFF, 0x1E, 0x1E, 0x1E)));
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
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Child = view
            };

            void UpdateSizing(Size size)
            {
                container.MaxWidth = size.Width > 0 ? size.Width * 0.85 : double.PositiveInfinity;
                container.MaxHeight = size.Height > 0 ? size.Height * 0.85 : double.PositiveInfinity;
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
