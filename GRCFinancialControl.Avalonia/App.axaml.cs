using App.Presentation.Localization;
using App.Presentation.Messages;
using App.Presentation.Services;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using AvaloniaWebView;
using CommunityToolkit.Mvvm.Messaging;
using GRC.Shared.UI.Dialogs;
using GRCFinancialControl.Avalonia.Services;
using GRCFinancialControl.Avalonia.ViewModels;
using GRCFinancialControl.Avalonia.ViewModels.Dialogs;
using GRCFinancialControl.Avalonia.Views;
using GRCFinancialControl.Core.Configuration;
using GRCFinancialControl.Persistence;
using GRCFinancialControl.Persistence.Configuration;
using GRCFinancialControl.Persistence.Services;
using GRCFinancialControl.Persistence.Services.Exporters;
using GRCFinancialControl.Persistence.Services.Importers;
using GRCFinancialControl.Persistence.Services.Interfaces;
using GRCFinancialControl.Persistence.Services.People;
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
        public IServiceProvider Services { get; private set; } = null!;
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

            var services = new ServiceCollection();
            string? restoredLanguage = null;
            string? restoredDefaultCurrency = null;

            services.AddDbContext<SettingsDbContext>(options =>
                options.UseSqlite(SettingsDatabaseOptions.BuildConnectionString()));
            services.AddTransient<ISettingsService, SettingsService>();

            var hasConnectionSettings = false;

            var connectionAvailability = new DatabaseConnectionAvailability(false);
            services.AddSingleton<IDatabaseConnectionAvailability>(connectionAvailability);

            using (var tempProvider = services.BuildServiceProvider())
            {
                using var scope = tempProvider.CreateScope();
                var scopedProvider = scope.ServiceProvider;
                var settingsDbContext = scopedProvider.GetRequiredService<SettingsDbContext>();
                await settingsDbContext.Database.EnsureCreatedAsync();

                var settingsService = scopedProvider.GetRequiredService<ISettingsService>();
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
                settings.TryGetValue(SettingKeys.Password, out var password);

                hasConnectionSettings =
                    !string.IsNullOrWhiteSpace(server) &&
                    !string.IsNullOrWhiteSpace(database) &&
                    !string.IsNullOrWhiteSpace(user);

                connectionAvailability.Update(hasConnectionSettings);
            }

            services.AddDbContextFactory<ApplicationDbContext>((provider, options) =>
            {
                var availability = provider.GetRequiredService<IDatabaseConnectionAvailability>();
                if (!availability.IsConfigured)
                {
                    return;
                }

                var settingsService = provider.GetRequiredService<ISettingsService>();
                var settings = settingsService.GetAll();

                if (!TryBuildConnectionString(settings, out var connectionString))
                {
                    availability.Update(false);
                    return;
                }

                options.UseMySql(
                    connectionString,
                    new MySqlServerVersion(new Version(8, 0, 29)),
                    mySqlOptions => mySqlOptions.EnableRetryOnFailure());
            });

            services.AddSingleton<IPersonDirectory, NullPersonDirectory>();

            services.AddTransient<IEngagementService, EngagementService>();
            services.AddTransient<IFiscalYearService, FiscalYearService>();
            services.AddTransient<IFullManagementDataImporter, FullManagementDataImporter>();
            services.AddTransient<IFiscalCalendarConsistencyService, FiscalCalendarConsistencyService>();
            services.AddTransient<IHoursAllocationService, HoursAllocationService>();
            services.AddTransient<IImportService, ImportService>();
            services.AddTransient<IClosingPeriodService, ClosingPeriodService>();
            services.AddTransient<IPlannedAllocationService, PlannedAllocationService>();
            services.AddTransient<IReportService, ReportService>();
            services.AddTransient<IPapdService, PapdService>();
            services.AddTransient<IManagerService, ManagerService>();
            services.AddTransient<IManagerAssignmentService, ManagerAssignmentService>();
            services.AddTransient<IPapdAssignmentService, PapdAssignmentService>();
            services.AddTransient<IExceptionService, ExceptionService>();
            services.AddTransient<ICustomerService, CustomerService>();
            services.AddTransient<IRankMappingService, RankMappingService>();
            services.AddSingleton<IDatabaseSchemaInitializer, DatabaseSchemaInitializer>();

            services.AddSingleton<LoggingService>();
            services.AddLogging(builder => builder.AddConsole());
            services.AddSingleton<IMessenger>(WeakReferenceMessenger.Default);
            services.AddSingleton<IModalDialogService, ModalDialogService>();
            services.AddSingleton<DialogService>();
            services.AddTransient<IRetainTemplateGenerator, RetainTemplateGenerator>();
            services.AddSingleton<IConnectionPackageService, ConnectionPackageService>();
            services.AddSingleton<IApplicationDataBackupService, ApplicationDataBackupService>();
            services.AddSingleton<PowerBiEmbeddingService>();

            services.AddTransient<HomeViewModel>();
            services.AddTransient<MainWindowViewModel>();
            services.AddTransient<EngagementsViewModel>();
            services.AddTransient<FiscalYearsViewModel>();
            services.AddTransient<ImportViewModel>();
            services.AddTransient<HoursAllocationDetailViewModel>();
            services.AddTransient<Func<HoursAllocationDetailViewModel>>(provider => () => provider.GetRequiredService<HoursAllocationDetailViewModel>());
            services.AddTransient<HoursAllocationsViewModel>();
            services.AddTransient<RevenueAllocationsViewModel>();
            services.AddTransient<AllocationsViewModel>();
            services.AddTransient(sp => new ReportsViewModel(
                sp.GetRequiredService<PowerBiEmbeddingService>(),
                sp.GetRequiredService<LoggingService>(),
                sp.GetRequiredService<IMessenger>()
            ));
            services.AddTransient<PapdViewModel>();
            services.AddTransient<ManagersViewModel>();
            services.AddTransient<SettingsViewModel>();
            services.AddTransient<ClosingPeriodsViewModel>();
            services.AddTransient<ClosingPeriodEditorViewModel>();
            services.AddTransient<CustomersViewModel>();
            services.AddTransient<CustomerEditorViewModel>();
            services.AddTransient<AppMasterDataViewModel>();
            services.AddTransient<ControlMasterDataViewModel>();
            services.AddTransient<RankMappingsViewModel>();
            services.AddTransient<RankMappingEditorViewModel>();
            services.AddTransient<TasksViewModel>();
            services.AddTransient<EngagementAssignmentViewModel>();
            services.AddTransient<Func<EngagementAssignmentViewModel>>(provider => () => provider.GetRequiredService<EngagementAssignmentViewModel>());

            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var mainWindow = new MainWindow();
                services.AddSingleton(new FilePickerService(mainWindow));

                Services = services.BuildServiceProvider();
                _logger = Services.GetRequiredService<ILogger<App>>();
                _logger.LogInformation(
                    "GRC Financial Control initialised with language '{Language}' and default currency '{Currency}'.",
                    string.IsNullOrWhiteSpace(restoredLanguage) ? "system" : restoredLanguage,
                    string.IsNullOrWhiteSpace(restoredDefaultCurrency) ? "system" : restoredDefaultCurrency);
                _logger.LogInformation(
                    "Connection settings detected during startup: {HasConnectionSettings}.",
                    hasConnectionSettings);

                _messenger = Services.GetRequiredService<IMessenger>();
                _messenger.Register<ApplicationRestartRequestedMessage>(this, (_, _) => RequestRestart());

                mainWindow.DataContext = Services.GetRequiredService<MainWindowViewModel>();
                using (var scope = Services.CreateScope())
                {
                    var provider = scope.ServiceProvider;
                    var settingsDbContext = provider.GetRequiredService<SettingsDbContext>();
                    await settingsDbContext.Database.EnsureCreatedAsync();
                    await settingsDbContext.Database.MigrateAsync();

                    if (hasConnectionSettings)
                    {
                        var schemaInitializer = provider.GetRequiredService<IDatabaseSchemaInitializer>();
                        try
                        {
                            await schemaInitializer.EnsureSchemaAsync();
                        }
                        catch (Exception ex)
                        {
                            hasConnectionSettings = false;
                            provider
                                .GetRequiredService<IDatabaseConnectionAvailability>()
                                .Update(false, ex.Message);
                            var logger = provider.GetRequiredService<ILogger<App>>();
                            logger.LogError(
                                ex,
                                "Failed to initialize the remote database schema. The application will continue without a database connection.");
                        }
                    }
                }

                desktop.MainWindow = mainWindow;
                desktop.Exit += (_, _) =>
                {
                    _messenger?.Unregister<ApplicationRestartRequestedMessage>(this);
                    _messenger = null;
                };
                mainWindow.Show();
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
                    logger.LogError(ex, "Failed to restart the application.");
                    _restartRequested = false;
                    return;
                }
            }

            desktop.Shutdown();
        }

        private static bool TryBuildConnectionString(
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
