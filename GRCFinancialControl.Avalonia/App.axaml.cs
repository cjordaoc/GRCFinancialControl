using App.Presentation.Localization;
using GRC.Shared.UI.Messages;
using App.Presentation.Services;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using AvaloniaWebView;
using CommunityToolkit.Mvvm.Messaging;
using GRCFinancialControl.Avalonia.Services.DependencyInjection;
using GRCFinancialControl.Avalonia.ViewModels;
using GRCFinancialControl.Avalonia.Views;
using GRCFinancialControl.Core.Configuration;
using GRCFinancialControl.Persistence;
using GRCFinancialControl.Persistence.Configuration;
using GRCFinancialControl.Persistence.Logging;
using GRCFinancialControl.Persistence.Services;
using GRCFinancialControl.Persistence.Services.Interfaces;
using GRC.Shared.Resources.Localization;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;

namespace GRCFinancialControl.Avalonia
{
    public partial class App : Application
    {
        public static new App? Current => (App?)Application.Current;

        public App()
        {
            var services = new ServiceCollection();
            services.AddAvaloniaAppServices();
            Services = services.BuildServiceProvider();
        }

        public IServiceProvider Services { get; }
        private IMessenger? _messenger;
        private ILogger<App>? _logger;
        private bool _restartRequested;

        public override void RegisterServices()
        {
            base.RegisterServices();
            AvaloniaWebViewBuilder.Initialize(default);
        }

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
            LocalizationRegistry.Configure(new ResourceManagerLocalizationProvider(Strings.ResourceManager));

            LiveCharts.Configure(config =>
                config
                    .AddSkiaSharp()
                    .AddDefaultMappers()
                    .AddLightTheme());

            string? restoredLanguage = null;
            string? restoredDefaultCurrency = null;
            var hasConnectionSettings = false;

            var connectionAvailability = Services.GetRequiredService<IDatabaseConnectionAvailability>();

            using (var scope = Services.CreateScope())
            {
                var provider = scope.ServiceProvider;
                var settingsDbContext = provider.GetRequiredService<SettingsDbContext>();
                await settingsDbContext.Database.EnsureCreatedAsync();

                var settingsService = provider.GetRequiredService<ISettingsService>();
                
                // Seed default MySQL connection settings on first run
                await SeedDefaultSettingsAsync(settingsService);
                
                var settings = await settingsService.GetAllAsync();
                settings.TryGetValue(SettingKeys.Language, out var language);
                settings.TryGetValue(SettingKeys.DefaultCurrency, out var defaultCurrency);
                restoredLanguage = language;
                restoredDefaultCurrency = string.IsNullOrWhiteSpace(defaultCurrency)
                    ? null
                    : defaultCurrency.Trim().ToUpperInvariant();
                CurrencyDisplayHelper.SetDefaultCurrency(defaultCurrency);
                LocalizationCultureManager.ApplyCulture(language);

                settings.TryGetValue(SettingKeys.Server, out var server);
                settings.TryGetValue(SettingKeys.Database, out var database);
                settings.TryGetValue(SettingKeys.User, out var user);

                hasConnectionSettings =
                    !string.IsNullOrWhiteSpace(server) &&
                    !string.IsNullOrWhiteSpace(database) &&
                    !string.IsNullOrWhiteSpace(user);

                connectionAvailability.Update(hasConnectionSettings);
            }

            _logger = Services.GetRequiredService<ILogger<App>>();
            _logger.LogInformation(
                "GRC Financial Control initialised with language '{Language}' and default currency '{Currency}'.",
                string.IsNullOrWhiteSpace(restoredLanguage) ? "system" : restoredLanguage,
                string.IsNullOrWhiteSpace(restoredDefaultCurrency) ? "system" : restoredDefaultCurrency);

            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var connectionAvailable = hasConnectionSettings;

                var mainWindow = Services.GetRequiredService<MainWindow>();
                mainWindow.DataContext = Services.GetRequiredService<MainWindowViewModel>();

                using (var scope = Services.CreateScope())
                {
                    var provider = scope.ServiceProvider;
                    var settingsDbContext = provider.GetRequiredService<SettingsDbContext>();
                    // Settings DB: use ensure-created only; migrations are not required for this local store.
                    await settingsDbContext.Database.EnsureCreatedAsync();

                    if (connectionAvailable)
                    {
                        var schemaInitializer = provider.GetRequiredService<IDatabaseSchemaInitializer>();
                        try
                        {
                            await schemaInitializer.EnsureSchemaAsync();
                        }
                        catch (Exception ex)
                        {
                            connectionAvailable = false;
                            provider
                                .GetRequiredService<IDatabaseConnectionAvailability>()
                                .Update(false, ex.Message);
                            var logger = provider.GetRequiredService<ILogger<App>>();
                            logger.LogErrorWithContext(
                                ex,
                                "Failed to initialize the remote database schema. The application will continue without a database connection.");
                        }
                    }
                }

                _logger.LogInformation(
                    "Connection settings detected during startup: {HasConnectionSettings}.",
                    connectionAvailable);

                _messenger = Services.GetRequiredService<IMessenger>();
                _messenger.Register<ApplicationRestartRequestedMessage>(this, (_, _) => RequestRestart());

                desktop.MainWindow = mainWindow;
                desktop.Exit += (_, _) =>
                {
                    _messenger?.Unregister<ApplicationRestartRequestedMessage>(this);
                    _messenger = null;
                };
                mainWindow.Show();
            }
            else
            {
                _logger.LogInformation(
                    "Connection settings detected during startup: {HasConnectionSettings}.",
                    hasConnectionSettings);
            }

            base.OnFrameworkInitializationCompleted();
        }

        private void RequestRestart()
        {
            if (_restartRequested)
            {
                return;
            }

            _restartRequested = true;
            _logger?.LogInformation("Restart requested; scheduling application relaunch.");
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

                    _logger?.LogInformation(
                        "Restarting application from '{Executable}' with arguments '{Arguments}'.",
                        executablePath,
                        startInfo.Arguments ?? string.Empty);

                    Process.Start(startInfo);
                }
                catch (Exception ex)
                {
                    var logger = Services.GetRequiredService<ILogger<App>>();
                    logger.LogErrorWithContext(ex, "Failed to restart the application.");
                    _restartRequested = false;
                    return;
                }
            }

            desktop.Shutdown();
        }

        internal static bool TryBuildConnectionString(
            IReadOnlyDictionary<string, string> settings,
            out string connectionString)
        {
            connectionString = string.Empty;

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

            if (!settings.TryGetValue(SettingKeys.Password, out var password))
            {
                password = string.Empty;
            }

            connectionString = $"Server={server};Database={database};User ID={user};Password={password};";
            return true;
        }

        private static string QuoteArgument(string argument) =>
            argument.Contains(' ') ? $"\"{argument}\"" : argument;

        private static async Task SeedDefaultSettingsAsync(ISettingsService settingsService)
        {
            var settings = await settingsService.GetAllAsync();
            
            // Only seed if MySQL connection settings are missing
            if (settings.ContainsKey(SettingKeys.Server) && 
                !string.IsNullOrWhiteSpace(settings[SettingKeys.Server]))
            {
                return;
            }

            var defaultSettings = new Dictionary<string, string>
            {
                [SettingKeys.Server] = "162.241.203.96",
                [SettingKeys.Database] = "blac3289_GRCFinancialControl",
                [SettingKeys.User] = "blac3289_GRCFinControl",
                [SettingKeys.Password] = "EYbr2@@25#"
            };

            await settingsService.SaveAllAsync(defaultSettings);
        }

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
}
