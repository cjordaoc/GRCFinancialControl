using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
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
        private readonly IApplicationDataBackupService _applicationDataBackupService;
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

        [ObservableProperty]
        private bool _isApplicationDataOperationRunning;

        public IReadOnlyList<LanguageOption> Languages { get; }

        [ObservableProperty]
        private LanguageOption? _selectedLanguage;

        public SettingsViewModel(
            ISettingsService settingsService,
            IDatabaseSchemaInitializer schemaInitializer,
            DialogService dialogService,
            IConnectionPackageService connectionPackageService,
            IApplicationDataBackupService applicationDataBackupService,
            FilePickerService filePickerService)
        {
            _settingsService = settingsService;
            _schemaInitializer = schemaInitializer;
            _dialogService = dialogService;
            _connectionPackageService = connectionPackageService;
            _applicationDataBackupService = applicationDataBackupService;
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
            ExportApplicationDataCommand = new AsyncRelayCommand(
                ExportApplicationDataAsync,
                () => !IsApplicationDataOperationRunning);
            ImportApplicationDataCommand = new AsyncRelayCommand(
                ImportApplicationDataAsync,
                () => !IsApplicationDataOperationRunning);

            Languages = LocalizationLanguageOptions.Create();
        }

        public IAsyncRelayCommand LoadSettingsCommand { get; }
        public IAsyncRelayCommand SaveSettingsCommand { get; }
        public IAsyncRelayCommand TestConnectionCommand { get; }
        public IAsyncRelayCommand ClearAllDataCommand { get; }
        public IAsyncRelayCommand ExportConnectionPackageCommand { get; }
        public IAsyncRelayCommand BrowseImportPackageCommand { get; }
        public IAsyncRelayCommand ImportConnectionPackageCommand { get; }
        public IAsyncRelayCommand ExportApplicationDataCommand { get; }
        public IAsyncRelayCommand ImportApplicationDataCommand { get; }

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

            RunOnUiThread(() =>
            {
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
            });
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
            RunOnUiThread(() =>
                StatusMessage = LocalizationRegistry.Get("Settings.Status.Saved"));
        }

        private async Task TestConnectionAsync()
        {
            RunOnUiThread(() => StatusMessage = LocalizationRegistry.Get("Settings.Status.Testing"));
            var settings = BuildSettingsDictionary();
            var result = await _settingsService.TestConnectionAsync(Server, Database, User, Password);

            if (!result.Success)
            {
                RunOnUiThread(() => StatusMessage = result.Message);
                return;
            }

            await _settingsService.SaveAllAsync(settings);
            RunOnUiThread(() =>
            {
                StatusMessage = LocalizationRegistry.Get("Settings.Status.TestSuccess");
                Messenger.Send(new RefreshDataMessage());
            });
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

            RunOnUiThread(() => StatusMessage = LocalizationRegistry.Get("Settings.Status.Clearing"));
            await _schemaInitializer.ClearAllDataAsync();
            RunOnUiThread(() => StatusMessage = LocalizationRegistry.Get("Settings.Status.Cleared"));
        }

        private async Task ExportConnectionPackageAsync()
        {
            RunOnUiThread(() => StatusMessage = null);

            if (string.IsNullOrWhiteSpace(ExportPassphrase))
            {
                RunOnUiThread(() =>
                    StatusMessage = LocalizationRegistry.Get("Settings.Validation.ExportPassphraseRequired"));
                return;
            }

            if (!string.Equals(ExportPassphrase, ConfirmExportPassphrase, StringComparison.Ordinal))
            {
                RunOnUiThread(() =>
                    StatusMessage = LocalizationRegistry.Get("Settings.Validation.ExportPassphraseMismatch"));
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
                RunOnUiThread(() =>
                    StatusMessage = LocalizationRegistry.Get("Settings.Status.ExportCancelled"));
                return;
            }

            try
            {
                RunOnUiThread(() =>
                    StatusMessage = LocalizationRegistry.Get("Settings.Status.ExportInProgress"));
                await _connectionPackageService.ExportAsync(filePath, ExportPassphrase);
                RunOnUiThread(() =>
                    StatusMessage = LocalizationRegistry.Format(
                        "Settings.Status.ExportSuccess",
                        Path.GetFileName(filePath)));
            }
            catch (InvalidOperationException ex)
            {
                RunOnUiThread(() => StatusMessage = ex.Message);
            }
            catch (Exception ex)
            {
                RunOnUiThread(() =>
                    StatusMessage = LocalizationRegistry.Format("Settings.Status.ExportFailure", ex.Message));
            }
            finally
            {
                RunOnUiThread(() =>
                {
                    ExportPassphrase = string.Empty;
                    ConfirmExportPassphrase = string.Empty;
                });
            }
        }

        private async Task BrowseImportPackageAsync()
        {
            RunOnUiThread(() => StatusMessage = null);

            var filePath = await _filePickerService.OpenFileAsync(
                title: LocalizationRegistry.Get("Settings.Dialog.Import.Title"),
                defaultExtension: ".grcconfig",
                allowedPatterns: new[] { "*.grcconfig" });

            if (string.IsNullOrWhiteSpace(filePath))
            {
                return;
            }

            RunOnUiThread(() => SelectedImportPackagePath = filePath);
        }

        private async Task ImportConnectionPackageAsync()
        {
            RunOnUiThread(() => StatusMessage = null);

            if (string.IsNullOrWhiteSpace(ImportPassphrase))
            {
                RunOnUiThread(() =>
                    StatusMessage = LocalizationRegistry.Get("Settings.Validation.ImportPassphraseRequired"));
                return;
            }

            if (string.IsNullOrWhiteSpace(SelectedImportPackagePath))
            {
                RunOnUiThread(() =>
                    StatusMessage = LocalizationRegistry.Get("Settings.Validation.ImportPackageRequired"));
                return;
            }

            try
            {
                SetIsImporting(true);
                RunOnUiThread(() =>
                    StatusMessage = LocalizationRegistry.Get("Settings.Status.ImportInProgress"));
                var importedSettings = await _connectionPackageService.ImportAsync(SelectedImportPackagePath, ImportPassphrase);
                var existingSettings = await _settingsService.GetAllAsync();

                foreach (var kvp in importedSettings)
                {
                    existingSettings[kvp.Key] = kvp.Value;
                }

                await _settingsService.SaveAllAsync(existingSettings);
                await _schemaInitializer.EnsureSchemaAsync();

                RunOnUiThread(() =>
                {
                    Server = importedSettings.TryGetValue(SettingKeys.Server, out var importedServer)
                        ? importedServer
                        : Server;
                    Database = importedSettings.TryGetValue(SettingKeys.Database, out var importedDatabase)
                        ? importedDatabase
                        : Database;
                    User = importedSettings.TryGetValue(SettingKeys.User, out var importedUser)
                        ? importedUser
                        : User;
                    Password = importedSettings.TryGetValue(SettingKeys.Password, out var importedPassword)
                        ? importedPassword
                        : Password;

                    StatusMessage = LocalizationRegistry.Format(
                        "Settings.Status.ImportSuccess",
                        Path.GetFileName(SelectedImportPackagePath));
                    Messenger.Send(new RefreshDataMessage());
                    Messenger.Send(ApplicationRestartRequestedMessage.Instance);
                });
            }
            catch (InvalidOperationException ex)
            {
                RunOnUiThread(() => StatusMessage = ex.Message);
            }
            catch (Exception ex)
            {
                RunOnUiThread(() =>
                    StatusMessage = LocalizationRegistry.Format("Settings.Status.ImportFailure", ex.Message));
            }
            finally
            {
                SetIsImporting(false);
                RunOnUiThread(() => ImportPassphrase = string.Empty);
            }
        }

        private async Task ExportApplicationDataAsync()
        {
            RunOnUiThread(() => StatusMessage = null);

            var defaultFileName = $"GRCData_{DateTime.Now:yyyyMMdd_HHmm}.xml";
            var filePath = await _filePickerService.SaveFileAsync(
                defaultFileName,
                title: LocalizationRegistry.Get("Settings.Dialog.DataExport.Title"),
                defaultExtension: ".xml",
                allowedPatterns: new[] { "*.xml" });

            if (string.IsNullOrWhiteSpace(filePath))
            {
                RunOnUiThread(() =>
                    StatusMessage = LocalizationRegistry.Get("Settings.Status.DataExportCancelled"));
                return;
            }

            try
            {
                SetIsApplicationDataOperationRunning(true);
                RunOnUiThread(() =>
                    StatusMessage = LocalizationRegistry.Get("Settings.Status.DataExportInProgress"));
                await _applicationDataBackupService.ExportAsync(filePath).ConfigureAwait(false);
                RunOnUiThread(() =>
                    StatusMessage = LocalizationRegistry.Format(
                        "Settings.Status.DataExportSuccess",
                        Path.GetFileName(filePath)));
            }
            catch (InvalidOperationException ex)
            {
                RunOnUiThread(() => StatusMessage = ex.Message);
            }
            catch (Exception ex)
            {
                RunOnUiThread(() =>
                    StatusMessage = LocalizationRegistry.Format(
                        "Settings.Status.DataExportFailure",
                        ex.Message));
            }
            finally
            {
                SetIsApplicationDataOperationRunning(false);
            }
        }

        private async Task ImportApplicationDataAsync()
        {
            RunOnUiThread(() => StatusMessage = null);

            var filePath = await _filePickerService.OpenFileAsync(
                title: LocalizationRegistry.Get("Settings.Dialog.DataImport.Title"),
                defaultExtension: ".xml",
                allowedPatterns: new[] { "*.xml" });

            if (string.IsNullOrWhiteSpace(filePath))
            {
                return;
            }

            var confirmed = await _dialogService.ShowConfirmationAsync(
                LocalizationRegistry.Get("Settings.Dialog.DataImport.Title"),
                LocalizationRegistry.Get("Settings.Dialog.DataImport.Message"));

            if (!confirmed)
            {
                return;
            }

            try
            {
                SetIsApplicationDataOperationRunning(true);
                RunOnUiThread(() =>
                    StatusMessage = LocalizationRegistry.Get("Settings.Status.DataImportInProgress"));
                await _applicationDataBackupService.ImportAsync(filePath).ConfigureAwait(false);
                RunOnUiThread(() =>
                {
                    StatusMessage = LocalizationRegistry.Format(
                        "Settings.Status.DataImportSuccess",
                        Path.GetFileName(filePath));
                    Messenger.Send(new RefreshDataMessage());
                });
            }
            catch (InvalidOperationException ex)
            {
                RunOnUiThread(() => StatusMessage = ex.Message);
            }
            catch (Exception ex)
            {
                RunOnUiThread(() =>
                    StatusMessage = LocalizationRegistry.Format(
                        "Settings.Status.DataImportFailure",
                        ex.Message));
            }
            finally
            {
                SetIsApplicationDataOperationRunning(false);
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
                RunOnUiThread(() =>
                {
                    StatusMessage = LocalizationRegistry.Get("Localization.Language.PreferenceSaved");
                    Messenger.Send(ApplicationRestartRequestedMessage.Instance);
                });
            }
            catch (Exception ex)
            {
                RunOnUiThread(() =>
                    StatusMessage = LocalizationRegistry.Format(
                        "Localization.Language.PreferenceSaveFailure",
                        ex.Message));
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
            NotifyCommandCanExecute(ImportConnectionPackageCommand);
        }

        partial void OnImportPassphraseChanged(string value)
        {
            NotifyCommandCanExecute(ImportConnectionPackageCommand);
        }

        partial void OnIsImportingChanged(bool value)
        {
            NotifyCommandCanExecute(ImportConnectionPackageCommand);
        }

        private static void RunOnUiThread(Action action)
        {
            if (Dispatcher.UIThread.CheckAccess())
            {
                action();
                return;
            }

            Dispatcher.UIThread.Post(action);
        }

        private void SetIsImporting(bool value)
        {
            if (Dispatcher.UIThread.CheckAccess())
            {
                IsImporting = value;
                return;
            }

            Dispatcher.UIThread.Post(() => IsImporting = value);
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

        partial void OnIsApplicationDataOperationRunningChanged(bool value)
        {
            NotifyCommandCanExecute(ExportApplicationDataCommand);
            NotifyCommandCanExecute(ImportApplicationDataCommand);
        }

        private void SetIsApplicationDataOperationRunning(bool value)
        {
            if (Dispatcher.UIThread.CheckAccess())
            {
                IsApplicationDataOperationRunning = value;
                return;
            }

            Dispatcher.UIThread.Post(() => IsApplicationDataOperationRunning = value);
        }
    }
}
