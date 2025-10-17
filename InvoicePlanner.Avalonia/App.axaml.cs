using System;
using System.Linq;
using System.Threading.Tasks;
using App.Presentation.Views;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using CommunityToolkit.Mvvm.Messaging;
using GRCFinancialControl.Core.Authentication;
using GRCFinancialControl.Core.Configuration;
using GRCFinancialControl.Core.Enums;
using GRCFinancialControl.Core.Models;
using GRCFinancialControl.Persistence;
using GRCFinancialControl.Persistence.Authentication;
using GRCFinancialControl.Persistence.Configuration;
using GRCFinancialControl.Persistence.Services;
using GRCFinancialControl.Persistence.Services.Dataverse;
using GRCFinancialControl.Persistence.Services.Dataverse.Provisioning;
using GRCFinancialControl.Persistence.Services.Interfaces;
using GRCFinancialControl.Persistence.Services.People;
using Invoices.Core.Validation;
using Invoices.Data.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.IO;
using InvoicePlanner.Avalonia.Configuration;
using InvoicePlanner.Avalonia.Services;
using InvoicePlanner.Avalonia.ViewModels;
using InvoicePlanner.Avalonia.Views;

namespace InvoicePlanner.Avalonia;

public partial class App : Application
{
    private IHost? _host;
    private bool _disposed;

    public IServiceProvider Services => _host?.Services ?? throw new InvalidOperationException("Host not initialised.");

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        _host = Host.CreateDefaultBuilder()
            .ConfigureAppConfiguration((context, config) =>
            {
                config.SetBasePath(AppContext.BaseDirectory);
                config.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
                config.AddJsonFile($"appsettings.{context.HostingEnvironment.EnvironmentName}.json", optional: true);
                config.AddEnvironmentVariables(prefix: "INVOICEPLANNER_");
            })
            .ConfigureServices((context, services) =>
            {
                services.AddLogging(logging => logging.AddConsole());

                services.AddSingleton(new DataBackendOptions(DataBackend.Dataverse));
                services.AddSingleton<IDbContextFactory<ApplicationDbContext>, UnsupportedApplicationDbContextFactory>();

                ApplyDataverseDefaults(context.Configuration);
                var hasDataverseOptions = DataverseConnectionOptionsProvider.TryResolve(
                    CreateSettingsDbContext,
                    out var dataverseOptions,
                    out var dataverseFailureReason);

                var startupStatus = hasDataverseOptions && dataverseOptions is not null
                    ? new DataverseStartupStatus(true, null)
                    : new DataverseStartupStatus(false, string.IsNullOrWhiteSpace(dataverseFailureReason)
                        ? "Configure Dataverse credentials in Settings before launching Invoice Planner."
                        : dataverseFailureReason);

                services.AddSingleton(startupStatus);

                services.AddSingleton(_ => DataverseEntityMetadataRegistry.CreateDefault());
                services.AddSingleton<IDataverseRepository, DataverseRepository>();
                services.AddSingleton<SqlForeignKeyParser>();

                if (startupStatus.IsConfigured && dataverseOptions is not null)
                {
                    services.AddSingleton(dataverseOptions);
                    services.AddSingleton<IAuthConfig>(provider =>
                    {
                        var options = provider.GetRequiredService<DataverseConnectionOptions>();
                        var authority = string.IsNullOrWhiteSpace(options.TenantId)
                            ? "https://login.microsoftonline.com/common"
                            : $"https://login.microsoftonline.com/{options.TenantId}";
                        var orgUri = new Uri(options.OrgUrl);
                        var scope = $"{orgUri.AbsoluteUri.TrimEnd('/')}/user_impersonation";
                        return new AuthConfig(options.ClientId, authority, orgUri, new[] { scope });
                    });
                    services.AddSingleton<IInteractiveAuthService, InteractiveAuthService>();
                    services.AddSingleton<IDataverseClientFactory, DataverseClientFactory>();
                    services.Configure<DataverseProvisioningOptions>(options =>
                    {
                        options.MetadataPath = Path.Combine(AppContext.BaseDirectory, "artifacts", "dataverse", "metadata_changes.json");
                        options.SqlSchemaPath = Path.Combine(AppContext.BaseDirectory, "artifacts", "mysql", "rebuild_schema.sql");
                    });
                    services.AddSingleton<IDataverseProvisioningService, DataverseProvisioningService>();
                    services.AddSingleton(_ => DataversePeopleOptions.FromEnvironment());
                    services.AddSingleton<NullPersonDirectory>();
                    services.AddSingleton<DataversePersonDirectory>();
                    services.AddSingleton<IPersonDirectory>(provider =>
                    {
                        var options = provider.GetRequiredService<DataversePeopleOptions>();
                        return options.EnablePeopleEnrichment
                            ? provider.GetRequiredService<DataversePersonDirectory>()
                            : provider.GetRequiredService<NullPersonDirectory>();
                    });
                }
                else
                {
                    services.AddSingleton<IDataverseClientFactory, DisabledDataverseClientFactory>();
                    services.AddSingleton<IDataverseProvisioningService, DisabledDataverseProvisioningService>();
                    services.AddSingleton<IInteractiveAuthService, DisabledInteractiveAuthService>();
                    services.AddSingleton<NullPersonDirectory>();
                    services.AddSingleton<IPersonDirectory>(provider => provider.GetRequiredService<NullPersonDirectory>());
                }

                services.AddTransient<IInvoicePlanRepository, DataverseInvoicePlanRepository>();

                services.AddSingleton<IInvoicePlanValidator, InvoicePlanValidator>();
                services.AddSingleton<InvoiceSummaryExporter>();
                services.AddSingleton<IErrorDialogService, ErrorDialogService>();
                services.AddSingleton<IGlobalErrorHandler, GlobalErrorHandler>();
                services.AddTransient<ErrorDialogViewModel>();
                services.AddTransient<ErrorDialog>();
                services.AddSingleton<IMessenger>(WeakReferenceMessenger.Default);
                services.AddSingleton<PlanEditorViewModel>();
                services.AddSingleton<RequestConfirmationViewModel>();
                services.AddSingleton<EmissionConfirmationViewModel>();
                services.AddSingleton<InvoiceSummaryViewModel>();
                services.AddSingleton<NotificationPreviewViewModel>();
                services.AddSingleton<HomeViewModel>();
                services.AddSingleton<MainWindowViewModel>();
                services.AddSingleton<MainWindow>();
                services.AddTransient<IStartupAuthenticationService, StartupAuthenticationService>();
            })
            .Build();

        _host.Start();

        var startupStatus = Services.GetRequiredService<DataverseStartupStatus>();
        if (startupStatus.IsConfigured)
        {
            EnsureDataverseDefaultsPersisted(Services.GetRequiredService<IConfiguration>());
        }

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.Exit += (_, _) => DisposeHostAsync().GetAwaiter().GetResult();

            var errorHandler = Services.GetRequiredService<IGlobalErrorHandler>();
            errorHandler.Register(desktop);

            if (!startupStatus.IsConfigured)
            {
                var dialogService = Services.GetRequiredService<IErrorDialogService>();
                var message = startupStatus.ErrorMessage ?? "Configure Dataverse credentials in Settings before launching Invoice Planner.";
                var showTask = dialogService.ShowErrorAsync(null, message, "Dataverse configuration");
                showTask.ContinueWith(_ => desktop.Shutdown(), TaskScheduler.FromCurrentSynchronizationContext());
                base.OnFrameworkInitializationCompleted();
                return;
            }

            var mainWindow = Services.GetRequiredService<MainWindow>();
            mainWindow.DataContext = Services.GetRequiredService<MainWindowViewModel>();

            var backendOptions = Services.GetRequiredService<DataBackendOptions>();
            if (backendOptions.Backend == DataBackend.Dataverse)
            {
                var splashVm = new global::App.Presentation.ViewModels.AuthenticationSplashWindowViewModel();
                var splash = new AuthenticationSplashWindow
                {
                    DataContext = splashVm
                };
                desktop.MainWindow = splash;

                splash.Opened += async (_, _) =>
                {
                    var failureMessage = await TryAuthenticateOnStartupAsync(splashVm).ConfigureAwait(true);

                    if (!string.IsNullOrEmpty(failureMessage))
                    {
                        async void Handler(object? sender, EventArgs args)
                        {
                            mainWindow.Opened -= Handler;
                            var dialogService = Services.GetRequiredService<IErrorDialogService>();
                            await dialogService.ShowErrorAsync(mainWindow, failureMessage, "Dataverse sign-in");
                        }

                        mainWindow.Opened += Handler;
                    }

                    desktop.MainWindow = mainWindow;
                    mainWindow.Show();
                    splash.Close();
                };
            }
            else
            {
                desktop.MainWindow = mainWindow;
                mainWindow.Show();
            }
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static SettingsDbContext CreateSettingsDbContext()
    {
        var optionsBuilder = new DbContextOptionsBuilder<SettingsDbContext>();
        optionsBuilder.UseSqlite(SettingsDatabaseOptions.BuildConnectionString());
        return new SettingsDbContext(optionsBuilder.Options);
    }

    private async Task DisposeHostAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        var host = _host;
        if (host is null)
        {
            return;
        }

        var provider = host.Services;
        if (provider.GetService(typeof(IGlobalErrorHandler)) is IDisposable disposableHandler)
        {
            disposableHandler.Dispose();
        }

        if (host is IAsyncDisposable asyncDisposable)
        {
            await asyncDisposable.DisposeAsync().ConfigureAwait(false);
        }
        else
        {
            host.Dispose();
        }

        _host = null;
    }

    private static void ApplyDataverseDefaults(IConfiguration configuration)
    {
        var section = configuration.GetSection("Dataverse");
        if (!section.Exists())
        {
            return;
        }

        SetEnvironmentIfMissing("DV_ORG_URL", section["OrgUrl"]);
        if (IsMeaningfulClientId(section["ClientId"]))
        {
            SetEnvironmentIfMissing("DV_CLIENT_ID", section["ClientId"]);
        }
        SetEnvironmentIfMissing("DV_TENANT_ID", section["TenantId"]);
        SetEnvironmentIfMissing("DV_AUTH_MODE", section["AuthMode"] ?? "Interactive");
    }

    private static void SetEnvironmentIfMissing(string variable, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(variable)))
        {
            Environment.SetEnvironmentVariable(variable, value);
        }
    }

    private static void EnsureDataverseDefaultsPersisted(IConfiguration configuration)
    {
        var section = configuration.GetSection("Dataverse");
        var orgUrl = section["OrgUrl"];
        var clientId = section["ClientId"];

        if (string.IsNullOrWhiteSpace(orgUrl) || !IsMeaningfulClientId(clientId))
        {
            return;
        }

        using var context = CreateSettingsDbContext();
        context.Database.EnsureCreated();

        var settingsService = new SettingsService(context);
        var existingSettings = settingsService.GetDataverseSettingsAsync().GetAwaiter().GetResult();

        if (!existingSettings.IsComplete())
        {
            var authModeValue = section["AuthMode"];
            var authMode = Enum.TryParse(authModeValue, ignoreCase: true, out DataverseAuthMode parsedMode)
                ? parsedMode
                : DataverseAuthMode.Interactive;

            var tenantId = string.IsNullOrWhiteSpace(section["TenantId"])
                ? "common"
                : section["TenantId"]!;

            var dataverseSettings = new DataverseSettings
            {
                OrgUrl = orgUrl!,
                ClientId = clientId!,
                TenantId = tenantId,
                ClientSecret = string.Empty,
                AuthMode = authMode
            };

            settingsService.SaveDataverseSettingsAsync(dataverseSettings).GetAwaiter().GetResult();
        }

        settingsService.SetBackendPreferenceAsync(DataBackend.Dataverse).GetAwaiter().GetResult();
    }

    private static bool IsMeaningfulClientId(string? value)
    {
        return Guid.TryParse(value, out var clientId) && clientId != Guid.Empty;
    }

    private async Task<string?> TryAuthenticateOnStartupAsync(global::App.Presentation.ViewModels.AuthenticationSplashWindowViewModel splashVm)
    {
        var options = Services.GetService<DataverseConnectionOptions>();
        if (options is null)
        {
            return null;
        }

        if (options.AuthMode != DataverseAuthMode.Interactive)
        {
            splashVm.StatusText = "Using application authentication.";
            await Task.Delay(400).ConfigureAwait(true);
            return null;
        }

        var authService = Services.GetRequiredService<IStartupAuthenticationService>();
        return await authService.AuthenticateAsync(
            status => splashVm.StatusText = status,
            isProgressVisible => splashVm.IsProgressVisible = isProgressVisible
        ).ConfigureAwait(true);
    }
}
