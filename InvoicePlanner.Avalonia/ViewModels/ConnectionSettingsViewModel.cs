using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using App.Presentation.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GRCFinancialControl.Persistence.Services.Interfaces;
using InvoicePlanner.Avalonia.Resources;

namespace InvoicePlanner.Avalonia.ViewModels;

public partial class ConnectionSettingsViewModel : ViewModelBase
{
    private readonly IFilePickerService _filePickerService;
    private readonly IConnectionPackageService _connectionPackageService;
    private readonly ISettingsService _settingsService;
    private readonly IDatabaseSchemaInitializer _schemaInitializer;

    [ObservableProperty]
    private string? selectedPackagePath;

    [ObservableProperty]
    private string? passphrase;

    [ObservableProperty]
    private string? statusMessage;

    [ObservableProperty]
    private bool isImporting;

    public ConnectionSettingsViewModel(
        IFilePickerService filePickerService,
        IConnectionPackageService connectionPackageService,
        ISettingsService settingsService,
        IDatabaseSchemaInitializer schemaInitializer)
    {
        _filePickerService = filePickerService ?? throw new ArgumentNullException(nameof(filePickerService));
        _connectionPackageService = connectionPackageService ?? throw new ArgumentNullException(nameof(connectionPackageService));
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _schemaInitializer = schemaInitializer ?? throw new ArgumentNullException(nameof(schemaInitializer));

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

        try
        {
            IsImporting = true;
            StatusMessage = Strings.Get("ConnectionImportInProgress");

            var importedSettings = await _connectionPackageService.ImportAsync(SelectedPackagePath, Passphrase);
            await _settingsService.SaveAllAsync(new Dictionary<string, string>(importedSettings));
            await _schemaInitializer.EnsureSchemaAsync();

            StatusMessage = Strings.Get("ConnectionImportSuccess");
        }
        catch (InvalidOperationException ex)
        {
            StatusMessage = ex.Message;
        }
        catch (Exception ex)
        {
            StatusMessage = string.Format(Strings.Get("ConnectionImportFailure"), ex.Message);
        }
        finally
        {
            IsImporting = false;
            Passphrase = string.Empty;
            ImportCommand.NotifyCanExecuteChanged();
        }
    }

    partial void OnSelectedPackagePathChanged(string? value)
    {
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
}
