using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using CommunityToolkit.Mvvm.Messaging;
using GRCFinancialControl.Core.Configuration;
using GRCFinancialControl.Core.Enums;
using GRCFinancialControl.Persistence;
using GRCFinancialControl.Persistence.Services;
using GRCFinancialControl.Persistence.Services.Dataverse;
using GRCFinancialControl.Persistence.Services.Interfaces;
using GRCFinancialControl.Persistence.Services.People;
using Invoices.Core.Validation;
using Invoices.Data.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Pomelo.EntityFrameworkCore.MySql.Infrastructure;
using InvoicePlanner.Avalonia.Services;
using InvoicePlanner.Avalonia.ViewModels;
using InvoicePlanner.Avalonia.Views;
using System.Linq;

namespace InvoicePlanner.Avalonia;

public partial class App : Application
{
    private IHost? _host;

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

                var dataBackend = ResolveBackendPreference();
                services.AddSingleton(new DataBackendOptions(dataBackend));

                if (dataBackend == DataBackend.MySql)
                {
                    services.AddDbContextFactory<ApplicationDbContext>(options =>
                    {
                        const string fallback = "Server=localhost;Database=InvoicePlanner;User ID=planner;Password=planner;";
                        var connectionString = context.Configuration.GetConnectionString("MySql");
                        var serverVersion = new MySqlServerVersion(new Version(8, 0, 36));
                        options.UseMySql(string.IsNullOrWhiteSpace(connectionString) ? fallback : connectionString, serverVersion);
                    });
                    services.AddSingleton<IPersonDirectory, NullPersonDirectory>();
                }
                else
                {
                    services.AddSingleton<IDbContextFactory<ApplicationDbContext>, UnsupportedApplicationDbContextFactory>();
                    var dataverseOptions = ResolveDataverseOptions();
                    services.AddSingleton(dataverseOptions);
                    services.AddSingleton(provider =>
                    {
                        var prefix = Environment.GetEnvironmentVariable("DV_CUSTOMIZATION_PREFIX");
                        return DataverseEntityMetadataRegistry.CreateDefault(string.IsNullOrWhiteSpace(prefix) ? "grc" : prefix);
                    });
                    services.AddSingleton<IDataverseServiceClientFactory, DataverseServiceClientFactory>();
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

                services.AddSingleton<IInvoicePlanValidator, InvoicePlanValidator>();
                services.AddTransient<IInvoicePlanRepository, InvoicePlanRepository>();
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
            })
            .Build();

        _host.Start();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var mainWindow = Services.GetRequiredService<MainWindow>();
            mainWindow.DataContext = Services.GetRequiredService<MainWindowViewModel>();
            desktop.MainWindow = mainWindow;
            desktop.Exit += (_, _) => DisposeHostAsync().GetAwaiter().GetResult();

            var errorHandler = Services.GetRequiredService<IGlobalErrorHandler>();
            errorHandler.Register(desktop);
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static DataBackend ResolveBackendPreference()
    {
        var environmentValue = Environment.GetEnvironmentVariable(DataBackendConfiguration.EnvironmentVariableName);
        if (!string.IsNullOrWhiteSpace(environmentValue) && Enum.TryParse(environmentValue, ignoreCase: true, out DataBackend backendFromEnvironment))
        {
            return backendFromEnvironment;
        }

        try
        {
            var optionsBuilder = new DbContextOptionsBuilder<SettingsDbContext>();
            optionsBuilder.UseSqlite("Data Source=settings.db");
            using var context = new SettingsDbContext(optionsBuilder.Options);
            context.Database.EnsureCreated();

            var storedValue = context.Settings.AsNoTracking()
                .FirstOrDefault(s => s.Key == SettingKeys.DataBackendPreference)?.Value;

            if (!string.IsNullOrWhiteSpace(storedValue) && Enum.TryParse(storedValue, ignoreCase: true, out DataBackend storedBackend))
            {
                return storedBackend;
            }
        }
        catch
        {
            // Ignore errors and fall back to MySQL.
        }

        return DataBackend.MySql;
    }

    private static DataverseConnectionOptions ResolveDataverseOptions()
    {
        if (DataverseConnectionOptions.TryFromEnvironment(out var environmentOptions) && environmentOptions is not null)
        {
            return environmentOptions;
        }

        try
        {
            var optionsBuilder = new DbContextOptionsBuilder<SettingsDbContext>();
            optionsBuilder.UseSqlite("Data Source=settings.db");
            using var context = new SettingsDbContext(optionsBuilder.Options);
            context.Database.EnsureCreated();

            var settingsService = new SettingsService(context);
            var storedSettings = settingsService.GetDataverseSettingsAsync().GetAwaiter().GetResult();
            return DataverseConnectionOptions.FromSettings(storedSettings);
        }
        catch (InvalidOperationException ex)
        {
            throw new InvalidOperationException("Configure Dataverse credentials in Settings before selecting the Dataverse backend.", ex);
        }
    }

    private async Task DisposeHostAsync()
    {
        if (Services is IServiceProvider provider)
        {
            if (provider.GetService(typeof(IGlobalErrorHandler)) is IDisposable disposableHandler)
            {
                disposableHandler.Dispose();
            }
        }

        if (_host is IAsyncDisposable asyncDisposable)
        {
            await asyncDisposable.DisposeAsync().ConfigureAwait(false);
        }
        else
        {
            _host?.Dispose();
        }
    }
}
