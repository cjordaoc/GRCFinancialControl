using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GRCFinancialControl.Avalonia.Services.Interfaces;
using GRCFinancialControl.Core.Configuration;
using GRCFinancialControl.Core.Enums;
using GRCFinancialControl.Core.Models;
using GRCFinancialControl.Persistence.Services.Dataverse.Provisioning;
using GRCFinancialControl.Persistence.Services.Interfaces;

namespace GRCFinancialControl.Avalonia.ViewModels
{
    public partial class SettingsViewModel : ViewModelBase
    {
        private readonly ISettingsService _settingsService;
        private readonly IDatabaseSchemaInitializer _schemaInitializer;
        private readonly IDialogService _dialogService;
        private readonly IDataverseProvisioningService _dataverseProvisioningService;

        [ObservableProperty]
        private string _server = string.Empty;

        [ObservableProperty]
        private string _database = string.Empty;

        [ObservableProperty]
        private string _user = string.Empty;

        [ObservableProperty]
        private string _password = string.Empty;

        [ObservableProperty]
        private string _powerBiEmbedUrl = string.Empty;

        [ObservableProperty]
        private string _powerBiWorkspaceId = string.Empty;

        [ObservableProperty]
        private string _powerBiReportId = string.Empty;

        [ObservableProperty]
        private string _powerBiEmbedToken = string.Empty;

        [ObservableProperty]
        private DataBackend _selectedBackend = DataBackend.MySql;

        [ObservableProperty]
        private string _dataverseOrgUrl = string.Empty;

        [ObservableProperty]
        private string _dataverseTenantId = string.Empty;

        [ObservableProperty]
        private string _dataverseClientId = string.Empty;

        [ObservableProperty]
        private string _dataverseClientSecret = string.Empty;

        [ObservableProperty]
        private string? _statusMessage;

        [ObservableProperty]
        private bool _isProvisioningDataverse;

        public SettingsViewModel(
            ISettingsService settingsService,
            IDatabaseSchemaInitializer schemaInitializer,
            IDialogService dialogService,
            IDataverseProvisioningService dataverseProvisioningService)
        {
            _settingsService = settingsService;
            _schemaInitializer = schemaInitializer;
            _dialogService = dialogService;
            _dataverseProvisioningService = dataverseProvisioningService;
            LoadSettingsCommand = new AsyncRelayCommand(LoadSettingsAsync);
            SaveSettingsCommand = new AsyncRelayCommand(SaveSettingsAsync);
            TestConnectionCommand = new AsyncRelayCommand(TestConnectionAsync);
            ClearAllDataCommand = new AsyncRelayCommand(ClearAllDataAsync);
            ProvisionDataverseCommand = new AsyncRelayCommand(ProvisionDataverseAsync, () => IsDataverseSelected && !IsProvisioningDataverse);
        }

        public IAsyncRelayCommand LoadSettingsCommand { get; }
        public IAsyncRelayCommand SaveSettingsCommand { get; }
        public IAsyncRelayCommand TestConnectionCommand { get; }
        public IAsyncRelayCommand ClearAllDataCommand { get; }
        public IAsyncRelayCommand ProvisionDataverseCommand { get; }

        public bool IsDataverseSelected
        {
            get => SelectedBackend == DataBackend.Dataverse;
            set => SelectedBackend = value ? DataBackend.Dataverse : DataBackend.MySql;
        }

        partial void OnSelectedBackendChanged(DataBackend value)
        {
            OnPropertyChanged(nameof(IsDataverseSelected));
            ProvisionDataverseCommand.NotifyCanExecuteChanged();
        }

        partial void OnIsProvisioningDataverseChanged(bool value)
        {
            ProvisionDataverseCommand.NotifyCanExecuteChanged();
        }

        private async Task ClearAllDataAsync()
        {
            var result = await _dialogService.ShowConfirmationAsync("Clear All Data", "Are you sure you want to delete all data? This action cannot be undone.");
            if (result)
            {
                StatusMessage = "Clearing all data...";
                await _schemaInitializer.ClearAllDataAsync();
                StatusMessage = "All data has been cleared.";
            }
        }

        public override async Task LoadDataAsync()
        {
            await LoadSettingsAsync();
        }

        private async Task LoadSettingsAsync()
        {
            var settings = await _settingsService.GetAllAsync();
            settings.TryGetValue(SettingKeys.Server, out var server);
            settings.TryGetValue(SettingKeys.Database, out var database);
            settings.TryGetValue(SettingKeys.User, out var user);
            settings.TryGetValue(SettingKeys.Password, out var password);
            settings.TryGetValue(SettingKeys.PowerBiEmbedUrl, out var embedUrl);
            settings.TryGetValue(SettingKeys.PowerBiWorkspaceId, out var workspaceId);
            settings.TryGetValue(SettingKeys.PowerBiReportId, out var reportId);
            settings.TryGetValue(SettingKeys.PowerBiEmbedToken, out var embedToken);

            Server = server ?? string.Empty;
            Database = database ?? string.Empty;
            User = user ?? string.Empty;
            Password = password ?? string.Empty;
            PowerBiEmbedUrl = embedUrl ?? string.Empty;
            PowerBiWorkspaceId = workspaceId ?? string.Empty;
            PowerBiReportId = reportId ?? string.Empty;
            PowerBiEmbedToken = embedToken ?? string.Empty;

            SelectedBackend = await _settingsService.GetBackendPreferenceAsync();
            var dataverseSettings = await _settingsService.GetDataverseSettingsAsync();
            DataverseOrgUrl = dataverseSettings.OrgUrl;
            DataverseTenantId = dataverseSettings.TenantId;
            DataverseClientId = dataverseSettings.ClientId;
            DataverseClientSecret = dataverseSettings.ClientSecret;
            OnPropertyChanged(nameof(IsDataverseSelected));
        }

        private async Task SaveSettingsAsync()
        {
            var previousBackend = await _settingsService.GetBackendPreferenceAsync();

            var settings = new Dictionary<string, string>
            {
                { SettingKeys.Server, Server },
                { SettingKeys.Database, Database },
                { SettingKeys.User, User },
                { SettingKeys.Password, Password },
                { SettingKeys.PowerBiEmbedUrl, PowerBiEmbedUrl },
                { SettingKeys.PowerBiWorkspaceId, PowerBiWorkspaceId },
                { SettingKeys.PowerBiReportId, PowerBiReportId },
                { SettingKeys.PowerBiEmbedToken, PowerBiEmbedToken }
            };
            await _settingsService.SaveAllAsync(settings);

            var dataverseSettings = new DataverseSettings
            {
                OrgUrl = DataverseOrgUrl,
                TenantId = DataverseTenantId,
                ClientId = DataverseClientId,
                ClientSecret = DataverseClientSecret
            };

            if (SelectedBackend == DataBackend.Dataverse && !dataverseSettings.IsComplete())
            {
                StatusMessage = "Provide the Dataverse organization URL, tenant ID, client ID, and client secret before enabling Dataverse.";
                SelectedBackend = previousBackend;
                return;
            }

            await _settingsService.SaveDataverseSettingsAsync(dataverseSettings);
            await _settingsService.SetBackendPreferenceAsync(SelectedBackend);

            StatusMessage = SelectedBackend == DataBackend.Dataverse
                ? "Settings saved. Restart the application to use Dataverse."
                : "Settings saved.";
        }

        private async Task TestConnectionAsync()
        {
            StatusMessage = "Testing connection...";
            var result = await _settingsService.TestConnectionAsync(Server, Database, User, Password);
            StatusMessage = result.Message;
        }

        private async Task ProvisionDataverseAsync()
        {
            if (SelectedBackend != DataBackend.Dataverse)
            {
                StatusMessage = "Switch to the Dataverse backend before running provisioning.";
                return;
            }

            if (IsProvisioningDataverse)
            {
                return;
            }

            IsProvisioningDataverse = true;
            StatusMessage = "Provisioning Dataverse schema...";

            try
            {
                var result = await _dataverseProvisioningService.ProvisionAsync(default);
                var summary = string.Join(Environment.NewLine, result.Actions);
                if (string.IsNullOrWhiteSpace(summary))
                {
                    summary = result.Succeeded ? "No changes were required." : "No provisioning actions were reported.";
                }
                StatusMessage = result.Succeeded
                    ? $"Dataverse schema successfully provisioned.{Environment.NewLine}{summary}"
                    : $"Dataverse provisioning reported issues.{Environment.NewLine}{summary}";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Dataverse provisioning failed: {ex.Message}";
            }
            finally
            {
                IsProvisioningDataverse = false;
            }
        }
    }
}
