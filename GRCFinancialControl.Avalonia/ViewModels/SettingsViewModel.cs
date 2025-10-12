using System.Collections.Generic;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GRCFinancialControl.Persistence.Services.Interfaces;

namespace GRCFinancialControl.Avalonia.ViewModels
{
    public partial class SettingsViewModel : ViewModelBase
    {
        private readonly ISettingsService _settingsService;

        [ObservableProperty]
        private string _server = string.Empty;

        [ObservableProperty]
        private string _database = string.Empty;

        [ObservableProperty]
        private string _user = string.Empty;

        [ObservableProperty]
        private string _password = string.Empty;

        [ObservableProperty]
        private string? _statusMessage;

        public SettingsViewModel(ISettingsService settingsService)
        {
            _settingsService = settingsService;
            LoadSettingsCommand = new AsyncRelayCommand(LoadSettingsAsync);
            SaveSettingsCommand = new AsyncRelayCommand(SaveSettingsAsync);
            TestConnectionCommand = new AsyncRelayCommand(TestConnectionAsync);
        }

        public IAsyncRelayCommand LoadSettingsCommand { get; }
        public IAsyncRelayCommand SaveSettingsCommand { get; }
        public IAsyncRelayCommand TestConnectionCommand { get; }

        private async Task LoadSettingsAsync()
        {
            var settings = await _settingsService.GetAllAsync();
            settings.TryGetValue("Server", out var server);
            settings.TryGetValue("Database", out var database);
            settings.TryGetValue("User", out var user);
            settings.TryGetValue("Password", out var password);

            Server = server ?? string.Empty;
            Database = database ?? string.Empty;
            User = user ?? string.Empty;
            Password = password ?? string.Empty;
        }

        private async Task SaveSettingsAsync()
        {
            var settings = new Dictionary<string, string>
            {
                { "Server", Server },
                { "Database", Database },
                { "User", User },
                { "Password", Password }
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
    }
}