using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using CommunityToolkit.Mvvm.Messaging;
using GRCFinancialControl.Core.Configuration;
using GRCFinancialControl.Core.Enums;
using GRCFinancialControl.Persistence;
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

                var dataverseOptions = DataverseConnectionOptionsProvider.Resolve(CreateSettingsDbContext);
                services.AddSingleton(dataverseOptions);
                services.AddSingleton(_ => DataverseEntityMetadataRegistry.CreateDefault());
                services.AddSingleton<IDataverseServiceClientFactory, DataverseServiceClientFactory>();
                services.AddSingleton<IDataverseRepository, DataverseRepository>();
                services.AddSingleton<SqlForeignKeyParser>();
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
}
