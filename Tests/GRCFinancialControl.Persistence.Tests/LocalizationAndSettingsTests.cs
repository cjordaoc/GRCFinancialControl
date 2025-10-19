using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using App.Presentation.Controls;
using App.Presentation.Localization;
using App.Presentation.Services;
using CommunityToolkit.Mvvm.Messaging;
using GRCFinancialControl.Avalonia.Services.Interfaces;
using GRCFinancialControl.Core.Configuration;
using GRCFinancialControl.Core.Models;
using GRCFinancialControl.Persistence.Services.Interfaces;
using PlannerConnectionSettingsViewModel = InvoicePlanner.Avalonia.ViewModels.ConnectionSettingsViewModel;
using GrcSettingsViewModel = GRCFinancialControl.Avalonia.ViewModels.SettingsViewModel;
using GrcViewModelBase = GRCFinancialControl.Avalonia.ViewModels.ViewModelBase;
using Xunit;

namespace GRCFinancialControl.Persistence.Tests;

[Collection("LocalizationTests")]
public sealed class LocalizationAndSettingsTests
{
    public LocalizationAndSettingsTests()
    {
        LocalizationCultureManager.ApplyCulture("en-US");
    }

    [Theory]
    [InlineData("en-US", "Language preference saved. Restart the application to apply changes.")]
    [InlineData("pt-BR", "Preferência de idioma salva. Reinicie o aplicativo para aplicar as alterações.")]
    [InlineData("es-PE", "Preferencia de idioma guardada. Reinicie la aplicación para aplicar los cambios.")]
    public void ResourceManagerProvider_ResolvesLocalizedStrings(string cultureName, string expected)
    {
        LocalizationCultureManager.ApplyCulture(cultureName);

        var actual = LocalizationRegistry.Get("Localization.Language.PreferenceSaved");

        Assert.Equal(expected, actual);
    }

    [Fact]
    public async Task ConnectionSettingsViewModel_PersistsLanguageSelection()
    {
        var settingsStore = new Dictionary<string, string>
        {
            [SettingKeys.Language] = "en-US"
        };

        var filePicker = new FakeFilePickerService();
        var packageService = new FakeConnectionPackageService();
        var settingsService = new FakeSettingsService(settingsStore);
        var schemaInitializer = new FakeSchemaInitializer();
        var messenger = new WeakReferenceMessenger();

        var viewModel = new PlannerConnectionSettingsViewModel(
            filePicker,
            packageService,
            settingsService,
            schemaInitializer,
            messenger);

        var targetLanguage = viewModel.Languages.First(option => option.CultureName == "es-PE");

        viewModel.SelectedLanguage = targetLanguage;

        var saved = await settingsService.WaitForSaveAsync();

        Assert.Equal("es-PE", saved[SettingKeys.Language]);
        Assert.Equal("es-PE", CultureInfo.CurrentUICulture.Name);
        Assert.Equal(
            LocalizationRegistry.Get("Localization.Language.PreferenceSaved"),
            viewModel.StatusMessage);
    }

    [Fact]
    public async Task SettingsViewModel_PersistsLanguageSelection()
    {
        var settingsStore = new Dictionary<string, string>
        {
            [SettingKeys.Language] = "en-US"
        };

        var settingsService = new FakeSettingsService(settingsStore);
        var schemaInitializer = new FakeSchemaInitializer();
        var dialogService = new FakeDialogService();
        var packageService = new FakeConnectionPackageService();
        var filePicker = new FakeFilePickerService();

        var viewModel = new GrcSettingsViewModel(
            settingsService,
            schemaInitializer,
            dialogService,
            packageService,
            filePicker);

        await viewModel.LoadDataAsync();

        var targetLanguage = viewModel.Languages.First(option => option.CultureName == "pt-BR");

        viewModel.SelectedLanguage = targetLanguage;

        var saved = await settingsService.WaitForSaveAsync();

        Assert.Equal("pt-BR", saved[SettingKeys.Language]);
        Assert.Equal("pt-BR", CultureInfo.CurrentUICulture.Name);
        Assert.Equal(
            LocalizationRegistry.Get("Localization.Language.PreferenceSaved"),
            viewModel.StatusMessage);
    }

    private sealed class FakeFilePickerService : IFilePickerService
    {
        public Task<string?> OpenFileAsync(string title = "Open File", string? defaultExtension = ".xlsx", string[]? allowedPatterns = null)
        {
            return Task.FromResult<string?>(null);
        }

        public Task<string?> SaveFileAsync(string defaultFileName, string title = "Save File", string defaultExtension = ".xlsx", string[]? allowedPatterns = null)
        {
            return Task.FromResult<string?>(null);
        }
    }

    private sealed class FakeConnectionPackageService : IConnectionPackageService
    {
        public Task ExportAsync(string filePath, string passphrase)
        {
            return Task.CompletedTask;
        }

        public Task<IReadOnlyDictionary<string, string>> ImportAsync(string filePath, string passphrase)
        {
            IReadOnlyDictionary<string, string> result = new Dictionary<string, string>();
            return Task.FromResult(result);
        }
    }

    private sealed class FakeSchemaInitializer : IDatabaseSchemaInitializer
    {
        public Task EnsureSchemaAsync()
        {
            return Task.CompletedTask;
        }

        public Task ClearAllDataAsync()
        {
            return Task.CompletedTask;
        }
    }

    private sealed class FakeDialogService : IDialogService
    {
        public Task<bool> ShowDialogAsync(GrcViewModelBase viewModel, string? title = null, bool canClose = true)
        {
            return Task.FromResult(false);
        }

        public Task<bool> ShowConfirmationAsync(string title, string message)
        {
            return Task.FromResult(false);
        }

        public void AttachHost(IModalOverlayHost host)
        {
        }
    }

    private sealed class FakeSettingsService : ISettingsService
    {
        private readonly Dictionary<string, string> _store;
        private readonly TaskCompletionSource<Dictionary<string, string>> _saveCompletionSource = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public FakeSettingsService(Dictionary<string, string> initialValues)
        {
            _store = new Dictionary<string, string>(initialValues, StringComparer.OrdinalIgnoreCase);
        }

        public Task<Dictionary<string, string>> GetAllAsync()
        {
            return Task.FromResult(new Dictionary<string, string>(_store));
        }

        public Task SaveAllAsync(Dictionary<string, string> settings)
        {
            _store.Clear();
            foreach (var pair in settings)
            {
                _store[pair.Key] = pair.Value;
            }

            if (!_saveCompletionSource.Task.IsCompleted)
            {
                _saveCompletionSource.SetResult(new Dictionary<string, string>(_store));
            }

            return Task.CompletedTask;
        }

        public Task<ConnectionTestResult> TestConnectionAsync(string server, string database, string user, string password)
        {
            return Task.FromResult(new ConnectionTestResult(false, string.Empty));
        }

        public Task<int?> GetDefaultFiscalYearIdAsync()
        {
            return Task.FromResult<int?>(null);
        }

        public Task SetDefaultFiscalYearIdAsync(int? fiscalYearId)
        {
            return Task.CompletedTask;
        }

        public Task<Dictionary<string, string>> WaitForSaveAsync()
        {
            return _saveCompletionSource.Task;
        }
    }
}

public sealed class InvoicePlannerResourceTests
{
    [Fact]
    public void AppStylesIncludeSharedResource()
    {
        var appXamlPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "..",
            "InvoicePlanner.Avalonia",
            "App.axaml"));

        var contents = File.ReadAllText(appXamlPath);

        Assert.Contains("StyleInclude Source=\"avares://App.Presentation/Styles/Styles.xaml\"", contents);
    }

    [Fact]
    public void MainAppStylesIncludeSharedResource()
    {
        var appXamlPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "..",
            "GRCFinancialControl.Avalonia",
            "App.axaml"));

        var contents = File.ReadAllText(appXamlPath);

        Assert.Contains("StyleInclude Source=\"avares://App.Presentation/Styles/Styles.xaml\"", contents);
    }
}

[CollectionDefinition("LocalizationTests", DisableParallelization = true)]
public sealed class LocalizationTestCollection : ICollectionFixture<LocalizationTestFixture>
{
}

public sealed class LocalizationTestFixture : IDisposable
{
    public LocalizationTestFixture()
    {
        LocalizationRegistry.Configure(new ResourceManagerLocalizationProvider(
            "InvoicePlanner.Avalonia.Resources.Strings",
            typeof(InvoicePlanner.Avalonia.App).Assembly));
        LocalizationCultureManager.ApplyCulture("en-US");
    }

    public void Dispose()
    {
        LocalizationCultureManager.ApplyCulture("en-US");
    }
}
