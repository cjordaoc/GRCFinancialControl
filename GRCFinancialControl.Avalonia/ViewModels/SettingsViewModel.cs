using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GRCFinancialControl.Avalonia.Services.Interfaces;
using GRCFinancialControl.Core.Configuration;
using GRCFinancialControl.Persistence.Services.Interfaces;

namespace GRCFinancialControl.Avalonia.ViewModels
{
    public partial class SettingsViewModel : ViewModelBase
    {
        private readonly ISettingsService _settingsService;
        private readonly IDatabaseSchemaInitializer _schemaInitializer;
        private readonly IDialogService _dialogService;

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
        private string? _statusMessage;

        public SettingsViewModel(
            ISettingsService settingsService,
            IDatabaseSchemaInitializer schemaInitializer,
            IDialogService dialogService)
        {
            _settingsService = settingsService;
            _schemaInitializer = schemaInitializer;
            _dialogService = dialogService;

            LoadSettingsCommand = new AsyncRelayCommand(LoadSettingsAsync);
            SaveSettingsCommand = new AsyncRelayCommand(SaveSettingsAsync);
            TestConnectionCommand = new AsyncRelayCommand(TestConnectionAsync);
            ClearAllDataCommand = new AsyncRelayCommand(ClearAllDataAsync);
        }

        public IAsyncRelayCommand LoadSettingsCommand { get; }
        public IAsyncRelayCommand SaveSettingsCommand { get; }
        public IAsyncRelayCommand TestConnectionCommand { get; }
        public IAsyncRelayCommand ClearAllDataCommand { get; }

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

            Server = server ?? string.Empty;
            Database = database ?? string.Empty;
            User = user ?? string.Empty;
            Password = password ?? string.Empty;
            PowerBiEmbedUrl = embedUrl ?? string.Empty;
            StatusMessage = "Settings loaded.";
        }

        private async Task SaveSettingsAsync()
        {
            var settings = new Dictionary<string, string>
            {
                [SettingKeys.Server] = Server ?? string.Empty,
                [SettingKeys.Database] = Database ?? string.Empty,
                [SettingKeys.User] = User ?? string.Empty,
                [SettingKeys.Password] = Password ?? string.Empty,
                [SettingKeys.PowerBiEmbedUrl] = PowerBiEmbedUrl ?? string.Empty
            };

            await _settingsService.SaveAllAsync(settings);
            StatusMessage = "Settings saved.";
        }

        private async Task TestConnectionAsync()
        {
            StatusMessage = "Testing connection...";
            var result = await _settingsService.TestConnectionAsync(Server, Database, User, Password);
            StatusMessage = result.Message;
        }

        private async Task ClearAllDataAsync()
        {
            var confirmed = await _dialogService.ShowConfirmationAsync(
                "Clear All Data",
                "Are you sure you want to delete all data? This action cannot be undone.");

            if (!confirmed)
            {
                return;
            }

            StatusMessage = "Clearing all data...";
            await _schemaInitializer.ClearAllDataAsync();
            StatusMessage = "All data has been cleared.";
        }
    }
}
