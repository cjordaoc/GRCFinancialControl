using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using App.Presentation.Localization;
using App.Presentation.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using GRCFinancialControl.Core.Configuration;
using GRCFinancialControl.Persistence.Services.Interfaces;
using InvoicePlanner.Avalonia.Messages;

namespace InvoicePlanner.Avalonia.ViewModels;

public partial class ConnectionSettingsViewModel : ViewModelBase
{
    private readonly IFilePickerService _filePickerService;
    private readonly IConnectionPackageService _connectionPackageService;
    private readonly ISettingsService _settingsService;
    private readonly IDatabaseSchemaInitializer _schemaInitializer;

    [ObservableProperty]
    private string? selectedPackagePath;

    public bool HasSelectedFile => !string.IsNullOrWhiteSpace(SelectedPackagePath);

    [ObservableProperty]
    private string? passphrase;

    [ObservableProperty]
    private string? statusMessage;

    [ObservableProperty]
    private bool isImporting;

    public bool HasStatusMessage => !string.IsNullOrWhiteSpace(StatusMessage);

    public IReadOnlyList<LanguageOption> Languages { get; }

    [ObservableProperty]
    private LanguageOption? selectedLanguage;

    private bool _initializingLanguage = true;

    public ConnectionSettingsViewModel(
        IFilePickerService filePickerService,
        IConnectionPackageService connectionPackageService,
        ISettingsService settingsService,
        IDatabaseSchemaInitializer schemaInitializer,
        IMessenger messenger)
        : base(messenger)
    {
        _filePickerService = filePickerService ?? throw new ArgumentNullException(nameof(filePickerService));
        _connectionPackageService = connectionPackageService ?? throw new ArgumentNullException(nameof(connectionPackageService));
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _schemaInitializer = schemaInitializer ?? throw new ArgumentNullException(nameof(schemaInitializer));

        BrowseCommand = new AsyncRelayCommand(BrowseAsync);
        ImportCommand = new AsyncRelayCommand(ImportAsync, CanImport);

        Languages = LocalizationLanguageOptions.Create();

        var settings = _settingsService.GetAllAsync().GetAwaiter().GetResult();
        settings.TryGetValue(SettingKeys.Language, out var language);
        SelectedLanguage = Languages
            .FirstOrDefault(option => string.Equals(option.CultureName, language, StringComparison.OrdinalIgnoreCase))
            ?? Languages.FirstOrDefault();
        _initializingLanguage = false;
    }

    public IAsyncRelayCommand BrowseCommand { get; }
    public IAsyncRelayCommand ImportCommand { get; }

    public string? SelectedFileName => string.IsNullOrWhiteSpace(SelectedPackagePath)
        ? null
        : Path.GetFileName(SelectedPackagePath);

    private async Task BrowseAsync()
    {
        StatusMessage = null;
        var filePath = await _filePickerService.OpenFileAsync(
            title: LocalizationRegistry.Get("Connection.Dialog.BrowseTitle"),
            defaultExtension: ".grcconfig",
            allowedPatterns: new[] { "*.grcconfig" });

        if (!string.IsNullOrWhiteSpace(filePath))
        {
            SelectedPackagePath = filePath;
        }
    }

    private bool CanImport()
    {
        return !string.IsNullOrWhiteSpace(SelectedPackagePath) && !string.IsNullOrWhiteSpace(Passphrase) && !IsImporting;
    }

    private async Task ImportAsync()
    {
        if (string.IsNullOrWhiteSpace(SelectedPackagePath))
        {
            StatusMessage = LocalizationRegistry.Get("Connection.Validation.PackageRequired");
            return;
        }

        if (string.IsNullOrWhiteSpace(Passphrase))
        {
            StatusMessage = LocalizationRegistry.Get("Connection.Validation.PassphraseRequired");
            return;
        }

        var previousSettings = await _settingsService.GetAllAsync();
        var newSettingsPersisted = false;

        try
        {
            IsImporting = true;
            StatusMessage = LocalizationRegistry.Get("Connection.Status.ImportInProgress");

            var importedSettings = await _connectionPackageService.ImportAsync(SelectedPackagePath, Passphrase);
            await _settingsService.SaveAllAsync(new Dictionary<string, string>(importedSettings));
            newSettingsPersisted = true;

            await _schemaInitializer.EnsureSchemaAsync();

            StatusMessage = LocalizationRegistry.Get("Connection.Status.ImportSuccess");
            Messenger.Send(ConnectionSettingsImportedMessage.Instance);
        }
        catch (InvalidOperationException ex)
        {
            var restoreMessage = await TryRestorePreviousSettingsAsync(previousSettings, newSettingsPersisted);
            StatusMessage = string.IsNullOrWhiteSpace(restoreMessage)
                ? ex.Message
                : string.Concat(ex.Message, " ", restoreMessage);
        }
        catch (Exception ex)
        {
            var restoreMessage = await TryRestorePreviousSettingsAsync(previousSettings, newSettingsPersisted);
            var failureMessage = LocalizationRegistry.Format("Connection.Status.ImportFailure", ex.Message);
            StatusMessage = string.IsNullOrWhiteSpace(restoreMessage)
                ? failureMessage
                : string.Concat(failureMessage, " ", restoreMessage);
        }
        finally
        {
            IsImporting = false;
            Passphrase = string.Empty;
            ImportCommand.NotifyCanExecuteChanged();
        }
    }

    private async Task PersistLanguageAsync(LanguageOption language)
    {
        try
        {
            var settings = await _settingsService.GetAllAsync();
            settings[SettingKeys.Language] = language.CultureName;
            await _settingsService.SaveAllAsync(new Dictionary<string, string>(settings));
            StatusMessage = LocalizationRegistry.Get("Localization.Language.PreferenceSaved");
        }
        catch (Exception ex)
        {
            StatusMessage = LocalizationRegistry.Format("Localization.Language.PreferenceSaveFailure", ex.Message);
        }
    }

    private async Task<string?> TryRestorePreviousSettingsAsync(
        IReadOnlyDictionary<string, string> previousSettings,
        bool shouldRestore)
    {
        if (!shouldRestore)
        {
            return null;
        }

        try
        {
            await _settingsService.SaveAllAsync(new Dictionary<string, string>(previousSettings));
            return null;
        }
        catch (Exception ex)
        {
            return LocalizationRegistry.Format("Connection.Status.RestoreFailure", ex.Message);
        }
    }

    partial void OnSelectedPackagePathChanged(string? value)
    {
        OnPropertyChanged(nameof(HasSelectedFile));
        OnPropertyChanged(nameof(SelectedFileName));
        ImportCommand.NotifyCanExecuteChanged();
    }

    partial void OnPassphraseChanged(string? value)
    {
        ImportCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsImportingChanged(bool value)
    {
        ImportCommand.NotifyCanExecuteChanged();
    }

    partial void OnStatusMessageChanged(string? value)
    {
        OnPropertyChanged(nameof(HasStatusMessage));
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

}
