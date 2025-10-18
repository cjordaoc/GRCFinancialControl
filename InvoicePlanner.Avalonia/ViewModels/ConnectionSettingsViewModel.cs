using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using App.Presentation.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using GRCFinancialControl.Persistence.Services.Interfaces;
using InvoicePlanner.Avalonia.Messages;
using InvoicePlanner.Avalonia.Resources;

namespace InvoicePlanner.Avalonia.ViewModels;

public partial class ConnectionSettingsViewModel : ViewModelBase
{
    private readonly IFilePickerService _filePickerService;
    private readonly IConnectionPackageService _connectionPackageService;
    private readonly ISettingsService _settingsService;
    private readonly IDatabaseSchemaInitializer _schemaInitializer;
    private readonly IMessenger _messenger;

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

    public ConnectionSettingsViewModel(
        IFilePickerService filePickerService,
        IConnectionPackageService connectionPackageService,
        ISettingsService settingsService,
        IDatabaseSchemaInitializer schemaInitializer,
        IMessenger messenger)
    {
        _filePickerService = filePickerService ?? throw new ArgumentNullException(nameof(filePickerService));
        _connectionPackageService = connectionPackageService ?? throw new ArgumentNullException(nameof(connectionPackageService));
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _schemaInitializer = schemaInitializer ?? throw new ArgumentNullException(nameof(schemaInitializer));
        _messenger = messenger ?? throw new ArgumentNullException(nameof(messenger));

        BrowseCommand = new AsyncRelayCommand(BrowseAsync);
        ImportCommand = new AsyncRelayCommand(ImportAsync, CanImport);
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
            title: Strings.Get("ConnectionBrowseTitle"),
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
            StatusMessage = Strings.Get("ConnectionImportSelectPackage");
            return;
        }

        if (string.IsNullOrWhiteSpace(Passphrase))
        {
            StatusMessage = Strings.Get("ConnectionImportEnterPassphrase");
            return;
        }

        var previousSettings = await _settingsService.GetAllAsync();
        var newSettingsPersisted = false;

        try
        {
            IsImporting = true;
            StatusMessage = Strings.Get("ConnectionImportInProgress");

            var importedSettings = await _connectionPackageService.ImportAsync(SelectedPackagePath, Passphrase);
            await _settingsService.SaveAllAsync(new Dictionary<string, string>(importedSettings));
            newSettingsPersisted = true;

            await _schemaInitializer.EnsureSchemaAsync();

            StatusMessage = Strings.Get("ConnectionImportSuccess");
            _messenger.Send(ConnectionSettingsImportedMessage.Instance);
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
            var failureMessage = string.Format(Strings.Get("ConnectionImportFailure"), ex.Message);
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
            return string.Format(Strings.Get("ConnectionImportRestoreFailed"), ex.Message);
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

}
