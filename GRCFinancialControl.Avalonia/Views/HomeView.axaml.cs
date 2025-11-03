using System;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using GRCFinancialControl.Avalonia.ViewModels;

namespace GRCFinancialControl.Avalonia.Views
{
    public partial class HomeView : UserControl
    {
        private HomeViewModel? _viewModel;

        public HomeView()
        {
            InitializeComponent();
            DataContextChanged += OnDataContextChanged;
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnDetachedFromVisualTree(e);
            UnsubscribeFromViewModel();
            UpdateMarkdown(null);
        }

        private void OnDataContextChanged(object? sender, EventArgs e)
        {
            UnsubscribeFromViewModel();

            _viewModel = DataContext as HomeViewModel;
            if (_viewModel is null)
            {
                UpdateMarkdown(null);
                return;
            }

            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
            UpdateMarkdown(_viewModel.ReadmeContent);
        }

        private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(HomeViewModel.ReadmeContent))
            {
                UpdateMarkdown(_viewModel?.ReadmeContent);
            }
        }

        private void UpdateMarkdown(string? markdown)
        {
            var content = markdown ?? string.Empty;

            if (Dispatcher.UIThread.CheckAccess())
            {
                ReadmeViewer.Markdown = content;
            }
            else
            {
                Dispatcher.UIThread.Post(() => ReadmeViewer.Markdown = content);
            }
        }

        private void UnsubscribeFromViewModel()
        {
            if (_viewModel is null)
            {
                return;
            }

            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            _viewModel = null;
        }
    }
}
