using System;
using System.Threading.Tasks;
using App.Presentation.Localization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using GRCFinancialControl.Avalonia.Services.Interfaces;
using GRCFinancialControl.Avalonia.Services.Models;

namespace GRCFinancialControl.Avalonia.ViewModels
{
    public partial class ReportsViewModel : ViewModelBase
    {
        private readonly IPowerBiEmbeddingService _embeddingService;
        private readonly ILoggingService _loggingService;

        [ObservableProperty]
        private Uri? _dashboardUri;

        [ObservableProperty]
        private string? _statusMessage;

        public ReportsViewModel(IPowerBiEmbeddingService embeddingService, ILoggingService loggingService, IMessenger messenger)
            : base(messenger)
        {
            _embeddingService = embeddingService;
            _loggingService = loggingService;

            RefreshCommand = new AsyncRelayCommand(LoadDataAsync);
            OpenInBrowserCommand = new RelayCommand(OpenInBrowser, CanOpenInBrowser);
        }

        public IAsyncRelayCommand RefreshCommand { get; }

        public IRelayCommand OpenInBrowserCommand { get; }

        public bool HasDashboard => DashboardUri is not null;

        public string? DashboardUrl => DashboardUri?.ToString();

        public override async Task LoadDataAsync()
        {
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

        private bool CanOpenInBrowser() => DashboardUri is not null;

        partial void OnDashboardUriChanged(Uri? value)
        {
            OnPropertyChanged(nameof(HasDashboard));
            OnPropertyChanged(nameof(DashboardUrl));
            OpenInBrowserCommand.NotifyCanExecuteChanged();
        }
    }
}
