using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using App.Presentation.Localization;
using App.Presentation.Messages;
using App.Presentation.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using GRCFinancialControl.Avalonia.Messages;
using GRCFinancialControl.Avalonia.Services;
using GRCFinancialControl.Core.Configuration;
using GRCFinancialControl.Persistence.Services.Interfaces;

namespace GRCFinancialControl.Avalonia.ViewModels
{
    public partial class SettingsViewModel : ViewModelBase
    {
        private readonly ISettingsService _settingsService;
        private readonly IDatabaseSchemaInitializer _schemaInitializer;
        private readonly DialogService _dialogService;
        private readonly IConnectionPackageService _connectionPackageService;
        private readonly FilePickerService _filePickerService;
        private bool _initializingLanguage;

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

        public bool HasStatusMessage => !string.IsNullOrWhiteSpace(StatusMessage);

        [ObservableProperty]
        private string _exportPassphrase = string.Empty;

        [ObservableProperty]
        private string _confirmExportPassphrase = string.Empty;

        [ObservableProperty]
        private string _importPassphrase = string.Empty;

        [ObservableProperty]
        private string? _selectedImportPackagePath;

        [ObservableProperty]
        private string _selectedImportPackageFileName = string.Empty;

        [ObservableProperty]
        private bool _isImporting;

        public bool HasSelectedImportPackage => !string.IsNullOrWhiteSpace(SelectedImportPackageFileName);

        [ObservableProperty]
        private bool _isDatabasePasswordVisible;

        [ObservableProperty]
        private bool _isExportPassphraseVisible;

        [ObservableProperty]
        private bool _isConfirmExportPassphraseVisible;

        [ObservableProperty]
        private bool _isImportPassphraseVisible;

        public IReadOnlyList<LanguageOption> Languages { get; }

        [ObservableProperty]
        private LanguageOption? _selectedLanguage;

        public SettingsViewModel(
            ISettingsService settingsService,
            IDatabaseSchemaInitializer schemaInitializer,
            DialogService dialogService,
            IConnectionPackageService connectionPackageService,
            FilePickerService filePickerService)
        {
            _settingsService = settingsService;
            _schemaInitializer = schemaInitializer;
            _dialogService = dialogService;
            _connectionPackageService = connectionPackageService;
            _filePickerService = filePickerService;

            LoadSettingsCommand = new AsyncRelayCommand(LoadSettingsAsync);
            SaveSettingsCommand = new AsyncRelayCommand(SaveSettingsAsync);
            TestConnectionCommand = new AsyncRelayCommand(TestConnectionAsync);
            ClearAllDataCommand = new AsyncRelayCommand(ClearAllDataAsync);
            ExportConnectionPackageCommand = new AsyncRelayCommand(ExportConnectionPackageAsync);
            BrowseImportPackageCommand = new AsyncRelayCommand(BrowseImportPackageAsync);
            ImportConnectionPackageCommand = new AsyncRelayCommand(
                ImportConnectionPackageAsync,
                () => !string.IsNullOrWhiteSpace(SelectedImportPackagePath)
                      && !string.IsNullOrWhiteSpace(ImportPassphrase)
                      && !IsImporting);

            Languages = LocalizationLanguageOptions.Create();
        }

        public IAsyncRelayCommand LoadSettingsCommand { get; }
        public IAsyncRelayCommand SaveSettingsCommand { get; }
        public IAsyncRelayCommand TestConnectionCommand { get; }
        public IAsyncRelayCommand ClearAllDataCommand { get; }
        public IAsyncRelayCommand ExportConnectionPackageCommand { get; }
        public IAsyncRelayCommand BrowseImportPackageCommand { get; }
        public IAsyncRelayCommand ImportConnectionPackageCommand { get; }

        public char DatabasePasswordChar => IsDatabasePasswordVisible ? '\0' : '•';
        public string DatabasePasswordToggleIcon => IsDatabasePasswordVisible ? "🙈" : "👁";

        public char ExportPassphraseChar => IsExportPassphraseVisible ? '\0' : '•';
        public string ExportPassphraseToggleIcon => IsExportPassphraseVisible ? "🙈" : "👁";

        public char ConfirmExportPassphraseChar => IsConfirmExportPassphraseVisible ? '\0' : '•';
        public string ConfirmExportPassphraseToggleIcon => IsConfirmExportPassphraseVisible ? "🙈" : "👁";

        public char ImportPassphraseChar => IsImportPassphraseVisible ? '\0' : '•';
        public string ImportPassphraseToggleIcon => IsImportPassphraseVisible ? "🙈" : "👁";

        public override async Task LoadDataAsync()
        {
            await LoadSettingsAsync();
        }

        private async Task LoadSettingsAsync()
        {
            _initializingLanguage = true;
            var settings = await _settingsService.GetAllAsync();
            settings.TryGetValue(SettingKeys.Server, out var server);
            settings.TryGetValue(SettingKeys.Database, out var database);
            settings.TryGetValue(SettingKeys.User, out var user);
            settings.TryGetValue(SettingKeys.Password, out var password);
            settings.TryGetValue(SettingKeys.PowerBiEmbedUrl, out var embedUrl);
            settings.TryGetValue(SettingKeys.Language, out var language);

            Server = server ?? string.Empty;
            Database = database ?? string.Empty;
            User = user ?? string.Empty;
            Password = password ?? string.Empty;
            PowerBiEmbedUrl = embedUrl ?? string.Empty;
            SelectedLanguage = Languages
                .FirstOrDefault(option => string.Equals(option.CultureName, language, StringComparison.OrdinalIgnoreCase))
                ?? Languages.FirstOrDefault();
            _initializingLanguage = false;
            StatusMessage = LocalizationRegistry.Get("Settings.Status.Loaded");
        }

        private Dictionary<string, string> BuildSettingsDictionary()
        {
            return new Dictionary<string, string>
            {
                [SettingKeys.Server] = Server ?? string.Empty,
                [SettingKeys.Database] = Database ?? string.Empty,
                [SettingKeys.User] = User ?? string.Empty,
                [SettingKeys.Password] = Password ?? string.Empty,
                [SettingKeys.PowerBiEmbedUrl] = PowerBiEmbedUrl ?? string.Empty,
                [SettingKeys.Language] = SelectedLanguage?.CultureName ?? string.Empty
            };
        }

        private async Task SaveSettingsAsync()
        {
            var settings = BuildSettingsDictionary();
            await _settingsService.SaveAllAsync(settings);
            StatusMessage = LocalizationRegistry.Get("Settings.Status.Saved");
        }

        private async Task TestConnectionAsync()
        {
            StatusMessage = LocalizationRegistry.Get("Settings.Status.Testing");
            var settings = BuildSettingsDictionary();
            var result = await _settingsService.TestConnectionAsync(Server, Database, User, Password);

            if (!result.Success)
            {
                StatusMessage = result.Message;
                return;
            }

            await _settingsService.SaveAllAsync(settings);
            StatusMessage = LocalizationRegistry.Get("Settings.Status.TestSuccess");
            Messenger.Send(new RefreshDataMessage());
        }

        private async Task ClearAllDataAsync()
        {
            var confirmed = await _dialogService.ShowConfirmationAsync(
                LocalizationRegistry.Get("Settings.Dialog.ClearAll.Title"),
                LocalizationRegistry.Get("Settings.Dialog.ClearAll.Message"));

            if (!confirmed)
            {
                return;
            }

            StatusMessage = LocalizationRegistry.Get("Settings.Status.Clearing");
            await _schemaInitializer.ClearAllDataAsync();
            StatusMessage = LocalizationRegistry.Get("Settings.Status.Cleared");
        }

        private async Task ExportConnectionPackageAsync()
        {
            StatusMessage = null;

            if (string.IsNullOrWhiteSpace(ExportPassphrase))
            {
                StatusMessage = LocalizationRegistry.Get("Settings.Validation.ExportPassphraseRequired");
                return;
            }

            if (!string.Equals(ExportPassphrase, ConfirmExportPassphrase, StringComparison.Ordinal))
            {
                StatusMessage = LocalizationRegistry.Get("Settings.Validation.ExportPassphraseMismatch");
                return;
            }

            var defaultFileName = $"GRCConnection_{DateTime.Now:yyyyMMdd_HHmm}.grcconfig";
            var filePath = await _filePickerService.SaveFileAsync(
                defaultFileName,
                title: LocalizationRegistry.Get("Settings.Dialog.Export.Title"),
                defaultExtension: ".grcconfig",
                allowedPatterns: new[] { "*.grcconfig" });

            if (string.IsNullOrWhiteSpace(filePath))
            {
                StatusMessage = LocalizationRegistry.Get("Settings.Status.ExportCancelled");
                return;
            }

            try
            {
                StatusMessage = LocalizationRegistry.Get("Settings.Status.ExportInProgress");
                await _connectionPackageService.ExportAsync(filePath, ExportPassphrase);
                StatusMessage = LocalizationRegistry.Format("Settings.Status.ExportSuccess", Path.GetFileName(filePath));
            }
            catch (InvalidOperationException ex)
            {
                StatusMessage = ex.Message;
            }
            catch (Exception ex)
            {
                StatusMessage = LocalizationRegistry.Format("Settings.Status.ExportFailure", ex.Message);
            }
            finally
            {
                ExportPassphrase = string.Empty;
                ConfirmExportPassphrase = string.Empty;
            }
        }

        private async Task BrowseImportPackageAsync()
        {
            StatusMessage = null;

            var filePath = await _filePickerService.OpenFileAsync(
                title: LocalizationRegistry.Get("Settings.Dialog.Import.Title"),
                defaultExtension: ".grcconfig",
                allowedPatterns: new[] { "*.grcconfig" });

            if (string.IsNullOrWhiteSpace(filePath))
            {
                return;
            }

            SelectedImportPackagePath = filePath;
        }

        private async Task ImportConnectionPackageAsync()
        {
            StatusMessage = null;

            if (string.IsNullOrWhiteSpace(ImportPassphrase))
            {
                StatusMessage = LocalizationRegistry.Get("Settings.Validation.ImportPassphraseRequired");
                return;
            }

            if (string.IsNullOrWhiteSpace(SelectedImportPackagePath))
            {
                StatusMessage = LocalizationRegistry.Get("Settings.Validation.ImportPackageRequired");
                return;
            }

            try
            {
                IsImporting = true;
                StatusMessage = LocalizationRegistry.Get("Settings.Status.ImportInProgress");
                var importedSettings = await _connectionPackageService.ImportAsync(SelectedImportPackagePath, ImportPassphrase);
                var existingSettings = await _settingsService.GetAllAsync();

                foreach (var kvp in importedSettings)
                {
                    existingSettings[kvp.Key] = kvp.Value;
                }

                await _settingsService.SaveAllAsync(existingSettings);
                await _schemaInitializer.EnsureSchemaAsync();

                Server = importedSettings.TryGetValue(SettingKeys.Server, out var server)
                    ? server
                    : Server;
                Database = importedSettings.TryGetValue(SettingKeys.Database, out var database)
                    ? database
                    : Database;
                User = importedSettings.TryGetValue(SettingKeys.User, out var user)
                    ? user
                    : User;
                Password = importedSettings.TryGetValue(SettingKeys.Password, out var password)
                    ? password
                    : Password;

                StatusMessage = LocalizationRegistry.Format(
                    "Settings.Status.ImportSuccess",
                    Path.GetFileName(SelectedImportPackagePath));
                Messenger.Send(new RefreshDataMessage());
                Messenger.Send(ApplicationRestartRequestedMessage.Instance);
            }
            catch (InvalidOperationException ex)
            {
                StatusMessage = ex.Message;
            }
            catch (Exception ex)
            {
                StatusMessage = LocalizationRegistry.Format("Settings.Status.ImportFailure", ex.Message);
            }
            finally
            {
                IsImporting = false;
                ImportPassphrase = string.Empty;
            }
        }

        partial void OnSelectedLanguageChanged(LanguageOption? value)
        {
            if (_initializingLanguage || value is null)
            {
                return;
            }

            LocalizationCultureManager.ApplyCulture(value.CultureName);
            _ = PersistLanguageAsync(value);
        }

        private async Task PersistLanguageAsync(LanguageOption language)
        {
            try
            {
                var settings = await _settingsService.GetAllAsync();
                settings[SettingKeys.Language] = language.CultureName;
                await _settingsService.SaveAllAsync(settings);
                StatusMessage = LocalizationRegistry.Get("Localization.Language.PreferenceSaved");
                Messenger.Send(ApplicationRestartRequestedMessage.Instance);
            }
            catch (Exception ex)
            {
                StatusMessage = LocalizationRegistry.Format("Localization.Language.PreferenceSaveFailure", ex.Message);
            }
        }

        partial void OnStatusMessageChanged(string? value)
        {
            OnPropertyChanged(nameof(HasStatusMessage));
        }

        partial void OnSelectedImportPackagePathChanged(string? value)
        {
            SelectedImportPackageFileName = string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : Path.GetFileName(value);
            ImportConnectionPackageCommand.NotifyCanExecuteChanged();
        }

        partial void OnImportPassphraseChanged(string value)
        {
            ImportConnectionPackageCommand.NotifyCanExecuteChanged();
        }

        partial void OnIsImportingChanged(bool value)
        {
            ImportConnectionPackageCommand.NotifyCanExecuteChanged();
        }

        partial void OnSelectedImportPackageFileNameChanged(string value)
        {
            OnPropertyChanged(nameof(HasSelectedImportPackage));
        }

        partial void OnIsDatabasePasswordVisibleChanged(bool value)
        {
            OnPropertyChanged(nameof(DatabasePasswordChar));
            OnPropertyChanged(nameof(DatabasePasswordToggleIcon));
        }

        partial void OnIsExportPassphraseVisibleChanged(bool value)
        {
            OnPropertyChanged(nameof(ExportPassphraseChar));
            OnPropertyChanged(nameof(ExportPassphraseToggleIcon));
        }

        partial void OnIsConfirmExportPassphraseVisibleChanged(bool value)
        {
            OnPropertyChanged(nameof(ConfirmExportPassphraseChar));
            OnPropertyChanged(nameof(ConfirmExportPassphraseToggleIcon));
        }

        partial void OnIsImportPassphraseVisibleChanged(bool value)
        {
            OnPropertyChanged(nameof(ImportPassphraseChar));
            OnPropertyChanged(nameof(ImportPassphraseToggleIcon));
        }
    }
}
