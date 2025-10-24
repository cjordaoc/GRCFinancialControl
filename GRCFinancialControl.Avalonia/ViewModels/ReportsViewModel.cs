using System;
using System.Threading.Tasks;
using App.Presentation.Localization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using GRCFinancialControl.Avalonia.Services;
using GRCFinancialControl.Avalonia.Services.Models;

namespace GRCFinancialControl.Avalonia.ViewModels
{
    public partial class ReportsViewModel : ViewModelBase
    {
        private readonly PowerBiEmbeddingService _embeddingService;
        private readonly LoggingService _loggingService;

        [ObservableProperty]
        private Uri? _dashboardUri;

        [ObservableProperty]
        private string? _statusMessage;

        [ObservableProperty]
        private bool _isRefreshing;

        public ReportsViewModel(PowerBiEmbeddingService embeddingService, LoggingService loggingService, IMessenger messenger)
            : base(messenger)
        {
            _embeddingService = embeddingService;
            _loggingService = loggingService;

            RefreshCommand = new AsyncRelayCommand(LoadDataAsync, CanRefresh);
            OpenInBrowserCommand = new RelayCommand(OpenInBrowser, CanOpenInBrowser);
        }

        public IAsyncRelayCommand RefreshCommand { get; }

        public IRelayCommand OpenInBrowserCommand { get; }

        public bool HasDashboard => DashboardUri is not null;

        public string? DashboardUrl => DashboardUri?.ToString();

        public override async Task LoadDataAsync()
        {
            if (IsRefreshing)
            {
                return;
            }

            IsRefreshing = true;

            try
            {
                var configuration = await _embeddingService.GetConfigurationAsync();

                DashboardUri = configuration.DashboardUri;
                StatusMessage = configuration.StatusMessage;
            }
            catch (Exception ex)
            {
                _loggingService.LogError($"Failed to load Power BI configuration: {ex.Message}");
                StatusMessage = LocalizationRegistry.Get("Reports.Status.LoadFailure");
                DashboardUri = null;
            }
            finally
            {
                IsRefreshing = false;
                RefreshCommand.NotifyCanExecuteChanged();
                OpenInBrowserCommand.NotifyCanExecuteChanged();
            }
        }

        private void OpenInBrowser()
        {
            if (DashboardUri is null)
            {
                return;
            }

            try
            {
                var _ = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = DashboardUri.ToString(),
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                _loggingService.LogError($"Failed to launch browser: {ex.Message}");
                StatusMessage = LocalizationRegistry.Format("Reports.Status.OpenExternalFailure", ex.Message);
            }
        }

        private bool CanOpenInBrowser() => !IsRefreshing && DashboardUri is not null;

        private bool CanRefresh() => !IsRefreshing;

        partial void OnDashboardUriChanged(Uri? value)
        {
            OnPropertyChanged(nameof(HasDashboard));
            OnPropertyChanged(nameof(DashboardUrl));
            OpenInBrowserCommand.NotifyCanExecuteChanged();
        }

        partial void OnIsRefreshingChanged(bool value)
        {
            RefreshCommand.NotifyCanExecuteChanged();
            OpenInBrowserCommand.NotifyCanExecuteChanged();
        }
    }
}
