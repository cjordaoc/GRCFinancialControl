using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GRCFinancialControl.Avalonia.Services.Interfaces;
using GRCFinancialControl.Core.Authentication;
using GRCFinancialControl.Core.Configuration;
using GRCFinancialControl.Core.Enums;
using GRCFinancialControl.Core.Models;
using GRCFinancialControl.Persistence.Authentication;
using GRCFinancialControl.Persistence.Services.Dataverse;
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
        private readonly IInteractiveAuthService _interactiveAuthService;
        private readonly IDataverseClientFactory _dataverseClientFactory;

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
        private DataverseAuthMode _selectedAuthMode = DataverseAuthMode.Interactive;

        [ObservableProperty]
        private bool _isEnvironmentOverride;

        [ObservableProperty]
        private string _dataverseCurrentUser = "Not signed in.";

        [ObservableProperty]
        private bool _isAuthOperationInProgress;

        [ObservableProperty]
        private string? _statusMessage;

        [ObservableProperty]
        private bool _isProvisioningDataverse;

        public SettingsViewModel(
            ISettingsService settingsService,
            IDatabaseSchemaInitializer schemaInitializer,
            IDialogService dialogService,
            IDataverseProvisioningService dataverseProvisioningService,
            IInteractiveAuthService interactiveAuthService,
            IDataverseClientFactory dataverseClientFactory)
        {
            _settingsService = settingsService;
            _schemaInitializer = schemaInitializer;
            _dialogService = dialogService;
            _dataverseProvisioningService = dataverseProvisioningService;
            _interactiveAuthService = interactiveAuthService;
            _dataverseClientFactory = dataverseClientFactory;
            LoadSettingsCommand = new AsyncRelayCommand(LoadSettingsAsync);
            SaveSettingsCommand = new AsyncRelayCommand(SaveSettingsAsync);
            TestConnectionCommand = new AsyncRelayCommand(TestConnectionAsync);
            ClearAllDataCommand = new AsyncRelayCommand(ClearAllDataAsync);
            SignInCommand = new AsyncRelayCommand(SignInAsync, CanExecuteSignIn);
            SignOutCommand = new AsyncRelayCommand(SignOutAsync, CanExecuteSignOut);
            TestDataverseConnectionCommand = new AsyncRelayCommand(TestDataverseConnectionAsync, CanExecuteTestDataverse);
            ProvisionDataverseCommand = new AsyncRelayCommand(ProvisionDataverseAsync, () => IsDataverseSelected && !IsProvisioningDataverse && SelectedAuthMode == DataverseAuthMode.ClientSecret);
        }

        public IAsyncRelayCommand LoadSettingsCommand { get; }
        public IAsyncRelayCommand SaveSettingsCommand { get; }
        public IAsyncRelayCommand TestConnectionCommand { get; }
        public IAsyncRelayCommand ClearAllDataCommand { get; }
        public IAsyncRelayCommand ProvisionDataverseCommand { get; }
        public IAsyncRelayCommand SignInCommand { get; }
        public IAsyncRelayCommand SignOutCommand { get; }
        public IAsyncRelayCommand TestDataverseConnectionCommand { get; }

        public bool IsDataverseSelected
        {
            get => SelectedBackend == DataBackend.Dataverse;
            set => SelectedBackend = value ? DataBackend.Dataverse : DataBackend.MySql;
        }

        public bool IsInteractiveAuthSelected
        {
            get => SelectedAuthMode == DataverseAuthMode.Interactive;
            set
            {
                if (value)
                {
                    SelectedAuthMode = DataverseAuthMode.Interactive;
                }
            }
        }

        public bool IsClientSecretAuthSelected
        {
            get => SelectedAuthMode == DataverseAuthMode.ClientSecret;
            set
            {
                if (value)
                {
                    SelectedAuthMode = DataverseAuthMode.ClientSecret;
                }
            }
        }

        public bool CanEditDataverseSettings => !IsEnvironmentOverride && !IsAuthOperationInProgress;

        partial void OnSelectedBackendChanged(DataBackend value)
        {
            OnPropertyChanged(nameof(IsDataverseSelected));
            ProvisionDataverseCommand.NotifyCanExecuteChanged();
            UpdateAuthCommandStates();
        }

        partial void OnIsProvisioningDataverseChanged(bool value)
        {
            ProvisionDataverseCommand.NotifyCanExecuteChanged();
        }

        partial void OnSelectedAuthModeChanged(DataverseAuthMode value)
        {
            OnPropertyChanged(nameof(IsInteractiveAuthSelected));
            OnPropertyChanged(nameof(IsClientSecretAuthSelected));
            ProvisionDataverseCommand.NotifyCanExecuteChanged();
            UpdateAuthCommandStates();
        }

        partial void OnIsEnvironmentOverrideChanged(bool value)
        {
            OnPropertyChanged(nameof(CanEditDataverseSettings));
            UpdateAuthCommandStates();
        }

        partial void OnIsAuthOperationInProgressChanged(bool value)
        {
            OnPropertyChanged(nameof(CanEditDataverseSettings));
            UpdateAuthCommandStates();
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
            DataverseTenantId = string.IsNullOrWhiteSpace(dataverseSettings.TenantId) ? "common" : dataverseSettings.TenantId;
            DataverseClientId = dataverseSettings.ClientId;
            DataverseClientSecret = dataverseSettings.ClientSecret;
            SelectedAuthMode = dataverseSettings.AuthMode;
            IsEnvironmentOverride = DataverseConnectionOptions.TryFromEnvironment(out _);
            OnPropertyChanged(nameof(IsDataverseSelected));
            await UpdateCurrentUserAsync();
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
                OrgUrl = DataverseOrgUrl?.Trim() ?? string.Empty,
                TenantId = (DataverseTenantId ?? string.Empty).Trim(),
                ClientId = DataverseClientId?.Trim() ?? string.Empty,
                ClientSecret = DataverseClientSecret ?? string.Empty,
                AuthMode = SelectedAuthMode
            };

            if (SelectedBackend == DataBackend.Dataverse && !dataverseSettings.IsComplete())
            {
                StatusMessage = SelectedAuthMode == DataverseAuthMode.Interactive
                    ? "Provide the Dataverse organization URL and client ID before enabling Dataverse."
                    : "Provide the Dataverse organization URL, tenant ID, client ID, and client secret before enabling Dataverse.";
                SelectedBackend = previousBackend;
                return;
            }

            await _settingsService.SaveDataverseSettingsAsync(dataverseSettings);
            await _settingsService.SetBackendPreferenceAsync(SelectedBackend);

            StatusMessage = SelectedBackend == DataBackend.Dataverse
                ? "Settings saved. Restart the application to use Dataverse."
                : "Settings saved.";
        }

        private bool CanExecuteSignIn()
        {
            return IsDataverseSelected && SelectedAuthMode == DataverseAuthMode.Interactive && !IsEnvironmentOverride && !IsAuthOperationInProgress;
        }

        private bool CanExecuteSignOut()
        {
            return IsDataverseSelected && !IsEnvironmentOverride && !IsAuthOperationInProgress;
        }

        private bool CanExecuteTestDataverse()
        {
            return IsDataverseSelected && !IsEnvironmentOverride && !IsAuthOperationInProgress;
        }

        private async Task TestConnectionAsync()
        {
            StatusMessage = "Testing connection...";
            var result = await _settingsService.TestConnectionAsync(Server, Database, User, Password);
            StatusMessage = result.Message;
        }

        private async Task SignInAsync()
        {
            await RunAuthOperationAsync("Signing in to Dataverse...", async () =>
            {
                if (SelectedAuthMode != DataverseAuthMode.Interactive)
                {
                    StatusMessage = "Interactive sign-in is only available in Interactive mode.";
                    return;
                }

                await _interactiveAuthService.AcquireTokenAsync(Array.Empty<string>());
                await UpdateCurrentUserAsync();
                StatusMessage = "Signed in to Dataverse.";
            });
        }

        private async Task SignOutAsync()
        {
            await RunAuthOperationAsync("Signing out of Dataverse...", async () =>
            {
                await _interactiveAuthService.SignOutAsync();
                await UpdateCurrentUserAsync();
                StatusMessage = "Signed out.";
            });
        }

        private async Task TestDataverseConnectionAsync()
        {
            await RunAuthOperationAsync("Testing Dataverse connection...", async () =>
            {
                using var client = await _dataverseClientFactory.CreateAsync();
                StatusMessage = client.IsReady
                    ? "Dataverse connection succeeded."
                    : "Dataverse connection reported an issue.";
            });
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

            if (SelectedAuthMode != DataverseAuthMode.ClientSecret)
            {
                StatusMessage = "Dataverse provisioning is only supported when using app (client secret) authentication.";
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

        private async Task RunAuthOperationAsync(string inProgressMessage, Func<Task> operation)
        {
            if (IsEnvironmentOverride)
            {
                StatusMessage = "Dataverse connection is controlled by environment variables.";
                return;
            }

            if (IsAuthOperationInProgress)
            {
                return;
            }

            try
            {
                IsAuthOperationInProgress = true;
                StatusMessage = inProgressMessage;
                await operation();
            }
            catch (Exception ex)
            {
                StatusMessage = AuthenticationMessageFormatter.GetFriendlyMessage(ex);
            }
            finally
            {
                IsAuthOperationInProgress = false;
            }
        }

        private async Task UpdateCurrentUserAsync()
        {
            try
            {
                var user = await _interactiveAuthService.GetCurrentUserAsync();
                if (user is null)
                {
                    DataverseCurrentUser = "Not signed in.";
                    return;
                }

                var displayName = user.DisplayName;
                var upn = user.UserPrincipalName;

                if (!string.IsNullOrWhiteSpace(displayName) && !string.IsNullOrWhiteSpace(upn))
                {
                    DataverseCurrentUser = $"{displayName} ({upn})";
                }
                else if (!string.IsNullOrWhiteSpace(upn))
                {
                    DataverseCurrentUser = upn!;
                }
                else if (!string.IsNullOrWhiteSpace(displayName))
                {
                    DataverseCurrentUser = displayName!;
                }
                else
                {
                    DataverseCurrentUser = "Signed-in user unavailable.";
                }
            }
            catch
            {
                DataverseCurrentUser = "Signed-in user unavailable.";
            }
        }

        private void UpdateAuthCommandStates()
        {
            SignInCommand.NotifyCanExecuteChanged();
            SignOutCommand.NotifyCanExecuteChanged();
            TestDataverseConnectionCommand.NotifyCanExecuteChanged();
        }

    }
}
