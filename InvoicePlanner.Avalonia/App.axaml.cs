using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using App.Presentation.Services;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
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
using InvoicePlanner.Avalonia.Messages;
using InvoicePlanner.Avalonia.ViewModels;
using InvoicePlanner.Avalonia.Views;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MySqlConnector;

namespace InvoicePlanner.Avalonia;

public partial class App : Application
{
    private IHost? _host;
    private bool _disposed;
    private bool _hasConnectionSettings;
    private IMessenger? _messenger;
    private bool _restartRequested;

    public IServiceProvider Services => _host?.Services ?? throw new InvalidOperationException("Host not initialised.");

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override async void OnFrameworkInitializationCompleted()
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

        await _host.StartAsync();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.Exit += async (_, _) => await DisposeHostAsync();

            var errorHandler = Services.GetRequiredService<IGlobalErrorHandler>();
            errorHandler.Register(desktop);

            var mainWindow = Services.GetRequiredService<MainWindow>();
            mainWindow.DataContext = Services.GetRequiredService<MainWindowViewModel>();

            _messenger = Services.GetRequiredService<IMessenger>();
            _messenger.Register<ConnectionSettingsImportedMessage>(this, (_, _) => RequestRestart());

            using (var scope = Services.CreateScope())
            {
                var provider = scope.ServiceProvider;
                if (_hasConnectionSettings)
                {
                    var schemaInitializer = provider.GetRequiredService<IDatabaseSchemaInitializer>();
                    try
                    {
                        await schemaInitializer.EnsureSchemaAsync();
                    }
                    catch (Exception ex)
                    {
                        _hasConnectionSettings = false;
                        var logger = provider.GetRequiredService<ILogger<App>>();
                        logger.LogError(
                            ex,
                            "Failed to initialize the remote database schema. The application will continue without a database connection.");
                    }
                }

                var settingsDbContext = provider.GetRequiredService<SettingsDbContext>();
                await settingsDbContext.Database.EnsureCreatedAsync();
                await settingsDbContext.Database.MigrateAsync();
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

        var builder = new MySqlConnectionStringBuilder
        {
            Server = server,
            Database = database,
            UserID = user,
            Password = password,
            SslMode = MySqlSslMode.Preferred,
            AllowUserVariables = true,
            ConnectionTimeout = 5
        };

        connectionString = builder.ConnectionString;
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
        if (_messenger is not null)
        {
            _messenger.Unregister<ConnectionSettingsImportedMessage>(this);
            _messenger = null;
        }

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

    private void RequestRestart()
    {
        if (_restartRequested)
        {
            return;
        }

        _restartRequested = true;
        Dispatcher.UIThread.Post(RestartApplication);
    }

    private void RestartApplication()
    {
        if (ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
        {
            return;
        }

        var executablePath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            executablePath = Process.GetCurrentProcess().MainModule?.FileName;
        }

        if (!string.IsNullOrWhiteSpace(executablePath))
        {
            try
            {
                var args = desktop.Args ?? Array.Empty<string>();
                var startInfo = new ProcessStartInfo
                {
                    FileName = executablePath,
                    UseShellExecute = true,
                };

                if (args.Length > 0)
                {
                    startInfo.Arguments = string.Join(' ', args.Select(QuoteArgument));
                }

                Process.Start(startInfo);
            }
            catch (Exception ex)
            {
                var logger = Services.GetRequiredService<ILogger<App>>();
                logger.LogError(ex, "Failed to restart the application after importing connection settings.");
                _restartRequested = false;
                return;
            }
        }

        desktop.Shutdown();
    }

    private static string QuoteArgument(string argument)
    {
        return argument.Contains(' ') ? $"\"{argument}\"" : argument;
    }
}
