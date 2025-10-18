using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using App.Presentation.Services;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using CommunityToolkit.Mvvm.Messaging;
using GRCFinancialControl.Persistence;
using GRCFinancialControl.Persistence.Configuration;
using GRCFinancialControl.Persistence.Services;
using GRCFinancialControl.Persistence.Services.Interfaces;
using GRCFinancialControl.Persistence.Services.People;
using GRCFinancialControl.Core.Configuration;
using Invoices.Core.Validation;
using Invoices.Data.Repositories;
using InvoicePlanner.Avalonia.Services;
using InvoicePlanner.Avalonia.ViewModels;
using InvoicePlanner.Avalonia.Views;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace InvoicePlanner.Avalonia;

public partial class App : Application
{
    private IHost? _host;
    private bool _disposed;
    private bool _hasConnectionSettings;

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
            .ConfigureServices((_, services) =>
            {
                services.AddLogging(logging => logging.AddConsole());

                services.AddDbContext<SettingsDbContext>(options =>
                    options.UseSqlite(SettingsDatabaseOptions.BuildConnectionString()));
                services.AddTransient<ISettingsService, SettingsService>();
                services.AddSingleton<IConnectionPackageService, ConnectionPackageService>();

                using (var tempProvider = services.BuildServiceProvider())
                {
                    using var scope = tempProvider.CreateScope();
                    var scopedProvider = scope.ServiceProvider;
                    var settingsDbContext = scopedProvider.GetRequiredService<SettingsDbContext>();
                    settingsDbContext.Database.EnsureCreated();

                    var settingsService = scopedProvider.GetRequiredService<ISettingsService>();
                    var initialSettings = settingsService.GetAllAsync().GetAwaiter().GetResult();
                    string? initialConnection;
                    _hasConnectionSettings = TryBuildConnectionString(initialSettings, out initialConnection);
                }

                services.AddDbContextFactory<ApplicationDbContext>((provider, options) =>
                {
                    var settingsService = provider.GetRequiredService<ISettingsService>();
                    var settings = settingsService.GetAllAsync().GetAwaiter().GetResult();

                    if (!TryBuildConnectionString(settings, out var connectionString))
                    {
                        return;
                    }

                    options.UseMySql(
                        connectionString,
                        new MySqlServerVersion(new Version(8, 0, 29)),
                        mySqlOptions => mySqlOptions.EnableRetryOnFailure());
                });

                services.AddSingleton<IPersonDirectory, NullPersonDirectory>();
                services.AddSingleton<IDatabaseSchemaInitializer, DatabaseSchemaInitializer>();

                services.AddTransient<IInvoicePlanRepository, InvoicePlanRepository>();
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
                services.AddSingleton<ConnectionSettingsViewModel>();
                services.AddSingleton<MainWindowViewModel>();
                services.AddSingleton<MainWindow>();
                services.AddSingleton<IFilePickerService>(provider =>
                {
                    var mainWindow = provider.GetRequiredService<MainWindow>();
                    return new FilePickerService(mainWindow);
                });
            })
            .Build();

        _host.Start();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.Exit += (_, _) => DisposeHostAsync().GetAwaiter().GetResult();

            var errorHandler = Services.GetRequiredService<IGlobalErrorHandler>();
            errorHandler.Register(desktop);

            var mainWindow = Services.GetRequiredService<MainWindow>();
            mainWindow.DataContext = Services.GetRequiredService<MainWindowViewModel>();

            using (var scope = Services.CreateScope())
            {
                var provider = scope.ServiceProvider;
                if (_hasConnectionSettings)
                {
                    var schemaInitializer = provider.GetRequiredService<IDatabaseSchemaInitializer>();
                    schemaInitializer.EnsureSchemaAsync().GetAwaiter().GetResult();
                }

                var settingsDbContext = provider.GetRequiredService<SettingsDbContext>();
                settingsDbContext.Database.EnsureCreated();
                settingsDbContext.Database.Migrate();
            }

            desktop.MainWindow = mainWindow;
            mainWindow.Show();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static bool TryBuildConnectionString(IReadOnlyDictionary<string, string> settings, out string connectionString)
    {
        connectionString = string.Empty;

        if (settings is null)
        {
            return false;
        }

        if (!settings.TryGetValue(SettingKeys.Server, out var server) || string.IsNullOrWhiteSpace(server))
        {
            return false;
        }

        if (!settings.TryGetValue(SettingKeys.Database, out var database) || string.IsNullOrWhiteSpace(database))
        {
            return false;
        }

        if (!settings.TryGetValue(SettingKeys.User, out var user) || string.IsNullOrWhiteSpace(user))
        {
            return false;
        }

        if (!settings.TryGetValue(SettingKeys.Password, out var password) || string.IsNullOrWhiteSpace(password))
        {
            return false;
        }

        connectionString = $"Server={server};Database={database};User ID={user};Password={password};";
        return true;
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
