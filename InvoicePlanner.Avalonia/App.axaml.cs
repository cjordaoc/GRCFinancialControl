using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Runtime.ExceptionServices;
using App.Presentation.Localization;
using App.Presentation.Messages;
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
using ImportResources = GRCFinancialControl.Resources.Features.Import.Import;
using SharedResources = GRCFinancialControl.Resources.Shared.Resources;

namespace InvoicePlanner.Avalonia;

public partial class App : Application
{
    public static new App? Current => (App?)Application.Current;
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

    public override void OnFrameworkInitializationCompleted()
    {
        var initializationTask = InitializeAsync();
        initializationTask.ContinueWith(
            task => ObserveFailure(task, "Unhandled exception during application startup"),
            TaskScheduler.Current);
    }

    private async Task InitializeAsync()
    {
        ApplyLanguageFromSettings();
        LocalizationRegistry.Configure(new CompositeLocalizationProvider(
            new ResourceManagerLocalizationProvider(ImportResources.ResourceManager),
            new ResourceManagerLocalizationProvider(
                "InvoicePlanner.Avalonia.Resources.Strings",
                typeof(App).Assembly),
            new ResourceManagerLocalizationProvider(SharedResources.ResourceManager)));

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
                    var initialSettings = settingsService.GetAll();
                    string? initialConnection;
                    _hasConnectionSettings = TryBuildConnectionString(initialSettings, out initialConnection);
                }

                services.AddDbContextFactory<ApplicationDbContext>((provider, options) =>
                {
                    var settingsService = provider.GetRequiredService<ISettingsService>();
                    var settings = settingsService.GetAll();

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

                services.AddSingleton<InvoiceAccessScope>();
                services.AddSingleton<IInvoiceAccessScope>(provider => provider.GetRequiredService<InvoiceAccessScope>());
                services.AddTransient<IInvoicePlanRepository, InvoicePlanRepository>();
                services.AddSingleton<IInvoicePlanValidator, InvoicePlanValidator>();
                services.AddSingleton<InvoiceSummaryExporter>();
                services.AddSingleton<GlobalErrorHandler>();
                services.AddTransient<ErrorDialogViewModel>();
                services.AddSingleton<IMessenger>(WeakReferenceMessenger.Default);
                services.AddSingleton<DialogService>(provider => new DialogService(provider.GetRequiredService<IMessenger>()));
                services.AddSingleton(provider => new PlanEditorViewModel(
                    provider.GetRequiredService<IInvoicePlanRepository>(),
                    provider.GetRequiredService<IInvoicePlanValidator>(),
                    provider.GetRequiredService<ILogger<PlanEditorViewModel>>(),
                    provider.GetRequiredService<IInvoiceAccessScope>(),
                    provider.GetRequiredService<DialogService>(),
                    provider.GetRequiredService<IMessenger>()));
                services.AddSingleton(provider => new RequestConfirmationViewModel(
                    provider.GetRequiredService<IInvoicePlanRepository>(),
                    provider.GetRequiredService<ILogger<RequestConfirmationViewModel>>(),
                    provider.GetRequiredService<IInvoiceAccessScope>(),
                    provider.GetRequiredService<DialogService>(),
                    provider.GetRequiredService<IMessenger>()));
                services.AddSingleton(provider => new EmissionConfirmationViewModel(
                    provider.GetRequiredService<IInvoicePlanRepository>(),
                    provider.GetRequiredService<ILogger<EmissionConfirmationViewModel>>(),
                    provider.GetRequiredService<IInvoiceAccessScope>(),
                    provider.GetRequiredService<DialogService>(),
                    provider.GetRequiredService<IMessenger>()));
                services.AddSingleton<InvoiceSummaryViewModel>();
                services.AddSingleton<NotificationPreviewViewModel>();
                services.AddSingleton(provider => new HomeViewModel(
                    provider.GetRequiredService<PlanEditorViewModel>(),
                    provider.GetRequiredService<RequestConfirmationViewModel>(),
                    provider.GetRequiredService<EmissionConfirmationViewModel>(),
                    provider.GetRequiredService<InvoiceSummaryViewModel>(),
                    provider.GetRequiredService<NotificationPreviewViewModel>(),
                    provider.GetRequiredService<IMessenger>()));
                services.AddSingleton(provider => new ConnectionSettingsViewModel(
                    provider.GetRequiredService<FilePickerService>(),
                    provider.GetRequiredService<IConnectionPackageService>(),
                    provider.GetRequiredService<ISettingsService>(),
                    provider.GetRequiredService<IDatabaseSchemaInitializer>(),
                    provider.GetRequiredService<IMessenger>()));
                services.AddSingleton(provider => new MainWindowViewModel(
                    provider.GetRequiredService<PlanEditorViewModel>(),
                    provider.GetRequiredService<RequestConfirmationViewModel>(),
                    provider.GetRequiredService<EmissionConfirmationViewModel>(),
                    provider.GetRequiredService<ConnectionSettingsViewModel>(),
                    provider.GetRequiredService<ISettingsService>(),
                    provider.GetRequiredService<IMessenger>()));
                services.AddSingleton<MainWindow>();
                services.AddSingleton(provider =>
                {
                    var mainWindow = provider.GetRequiredService<MainWindow>();
                    return new FilePickerService(mainWindow);
                });
            })
            .Build();

        await _host.StartAsync();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.Exit += OnDesktopExit;

            var errorHandler = Services.GetRequiredService<GlobalErrorHandler>();
            errorHandler.Register(desktop);

            var mainWindow = Services.GetRequiredService<MainWindow>();
            mainWindow.DataContext = Services.GetRequiredService<MainWindowViewModel>();

            _messenger = Services.GetRequiredService<IMessenger>();
            _messenger.Register<ConnectionSettingsImportedMessage>(this, (_, _) => RequestRestart());
            _messenger.Register<ApplicationRestartRequestedMessage>(this, (_, _) => RequestRestart());

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

                if (_hasConnectionSettings)
                {
                    var accessScope = provider.GetRequiredService<InvoiceAccessScope>();
                    accessScope.EnsureInitialized();
                }
            }

            desktop.MainWindow = mainWindow;
            mainWindow.Show();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static void ApplyLanguageFromSettings()
    {
        try
        {
            var options = new DbContextOptionsBuilder<SettingsDbContext>()
                .UseSqlite(SettingsDatabaseOptions.BuildConnectionString())
                .Options;

            using var context = new SettingsDbContext(options);
            context.Database.EnsureCreated();

            var language = context.Settings
                .AsNoTracking()
                .FirstOrDefault(setting => setting.Key == SettingKeys.Language)
                ?.Value;

            LocalizationCultureManager.ApplyCulture(language);
        }
        catch
        {
            LocalizationCultureManager.ApplyCulture(null);
        }
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
            _messenger.Unregister<ApplicationRestartRequestedMessage>(this);
            _messenger = null;
        }

        if (provider.GetService(typeof(GlobalErrorHandler)) is IDisposable disposableHandler)
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

    private void OnDesktopExit(object? sender, ControlledApplicationLifetimeExitEventArgs e)
    {
        var disposalTask = DisposeHostAsync();
        disposalTask.ContinueWith(
            task => ObserveFailure(task, "Unhandled exception while disposing application host"),
            TaskScheduler.Current);
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
                logger.LogError(ex, "Failed to restart the application.");
                _restartRequested = false;
                return;
            }
        }

        desktop.Shutdown();
    }

    private static string QuoteArgument(string argument) =>
        argument.Contains(' ') ? $"\"{argument}\"" : argument;

    private static void ObserveFailure(Task task, string context)
    {
        if (task.Exception is not { } aggregate)
        {
            return;
        }

        var exception = aggregate.GetBaseException();
        Trace.TraceError("{0}: {1}", context, exception);
        aggregate.Handle(_ => true);
        Dispatcher.UIThread.Post(() => ExceptionDispatchInfo.Capture(exception).Throw());
    }
}

