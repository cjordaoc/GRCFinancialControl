using App.Presentation.Localization;
using App.Presentation.Messages;
using App.Presentation.Services;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using AvaloniaWebView;
using CommunityToolkit.Mvvm.Messaging;
using GRCFinancialControl.Avalonia.Services;
using GRCFinancialControl.Avalonia.Services.Interfaces;
using GRCFinancialControl.Avalonia.ViewModels;
using GRCFinancialControl.Avalonia.ViewModels.Dialogs;
using GRCFinancialControl.Avalonia.Views;
using GRCFinancialControl.Core.Configuration;
using GRCFinancialControl.Persistence;
using GRCFinancialControl.Persistence.Configuration;
using GRCFinancialControl.Persistence.Services;
using GRCFinancialControl.Persistence.Services.Importers;
using GRCFinancialControl.Persistence.Services.Interfaces;
using GRCFinancialControl.Persistence.Services.People;
using ImportResources = GRCFinancialControl.Resources.Features.Import.Import;
using SharedResources = GRCFinancialControl.Resources.Shared.Resources;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.Linq;

namespace GRCFinancialControl.Avalonia
{
    public partial class App : Application
    {
        public static new App? Current => (App?)Application.Current;
        public IServiceProvider Services { get; private set; } = null!;
        private IMessenger? _messenger;
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

        public override async void OnFrameworkInitializationCompleted()
        {
            LocalizationRegistry.Configure(new CompositeLocalizationProvider(
                new ResourceManagerLocalizationProvider(ImportResources.ResourceManager),
                new ResourceManagerLocalizationProvider(
                    "GRCFinancialControl.Avalonia.Resources.Strings",
                    typeof(App).Assembly),
                new ResourceManagerLocalizationProvider(SharedResources.ResourceManager)));

            LiveCharts.Configure(config =>
                config
                    .AddSkiaSharp()
                    .AddDefaultMappers()
                    .AddLightTheme());

            var services = new ServiceCollection();

            services.AddDbContext<SettingsDbContext>(options =>
                options.UseSqlite(SettingsDatabaseOptions.BuildConnectionString()));
            services.AddTransient<ISettingsService, SettingsService>();

            var hasConnectionSettings = false;

            using (var tempProvider = services.BuildServiceProvider())
            {
                using var scope = tempProvider.CreateScope();
                var scopedProvider = scope.ServiceProvider;
                var settingsDbContext = scopedProvider.GetRequiredService<SettingsDbContext>();
                await settingsDbContext.Database.EnsureCreatedAsync();

                var settingsService = scopedProvider.GetRequiredService<ISettingsService>();
                var settings = await settingsService.GetAllAsync();
                settings.TryGetValue(SettingKeys.Language, out var language);
                LocalizationCultureManager.ApplyCulture(language);

                settings.TryGetValue(SettingKeys.Server, out var server);
                settings.TryGetValue(SettingKeys.Database, out var database);
                settings.TryGetValue(SettingKeys.User, out var user);
                settings.TryGetValue(SettingKeys.Password, out var password);

                hasConnectionSettings =
                    !string.IsNullOrWhiteSpace(server) &&
                    !string.IsNullOrWhiteSpace(database) &&
                    !string.IsNullOrWhiteSpace(user);

                var connectionString = $"Server={server};Database={database};User ID={user};Password={password};";
                services.AddDbContextFactory<ApplicationDbContext>(options =>
                    options.UseMySql(
                        connectionString,
                        new MySqlServerVersion(new Version(8, 0, 29)),
                        mySqlOptions => mySqlOptions.EnableRetryOnFailure()));
            }

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
            services.AddTransient<IExceptionService, ExceptionService>();
            services.AddTransient<ICustomerService, CustomerService>();
            services.AddTransient<IRankMappingService, RankMappingService>();
            services.AddSingleton<IDatabaseSchemaInitializer, DatabaseSchemaInitializer>();

            services.AddSingleton<ILoggingService, LoggingService>();
            services.AddLogging(builder => builder.AddConsole());
            services.AddSingleton<IMessenger>(WeakReferenceMessenger.Default);
            services.AddSingleton<IDialogService, DialogService>();
            services.AddTransient<IExportService, ExportService>();
            services.AddSingleton<IConnectionPackageService, ConnectionPackageService>();
            services.AddSingleton<IPowerBiEmbeddingService, PowerBiEmbeddingService>();

            services.AddTransient<MainWindowViewModel>();
            services.AddTransient<EngagementsViewModel>();
            services.AddTransient<FiscalYearsViewModel>();
            services.AddTransient<ImportViewModel>();
            services.AddTransient<HoursAllocationsViewModel>();
            services.AddTransient<RevenueAllocationsViewModel>();
            services.AddTransient<AllocationsViewModel>();
            services.AddTransient(sp => new ReportsViewModel(
                sp.GetRequiredService<IPowerBiEmbeddingService>(),
                sp.GetRequiredService<ILoggingService>(),
                sp.GetRequiredService<IMessenger>()
            ));
            services.AddTransient<PapdContributionViewModel>();
            services.AddTransient<FinancialEvolutionViewModel>();
            services.AddTransient<PapdViewModel>();
            services.AddTransient<ManagersViewModel>();
            services.AddTransient<ManagerAssignmentsViewModel>();
            services.AddTransient<PapdAssignmentsViewModel>();
            services.AddTransient<GrcTeamViewModel>();
            services.AddTransient<SettingsViewModel>();
            services.AddTransient<ClosingPeriodsViewModel>();
            services.AddTransient<ClosingPeriodEditorViewModel>();
            services.AddTransient<CustomersViewModel>();
            services.AddTransient<CustomerEditorViewModel>();
            services.AddTransient<RankMappingsViewModel>();
            services.AddTransient<RankMappingEditorViewModel>();
            services.AddTransient<TasksViewModel>();

            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var mainWindow = new MainWindow();
                services.AddSingleton<IFilePickerService>(new FilePickerService(mainWindow));

                Services = services.BuildServiceProvider();

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
                        await schemaInitializer.EnsureSchemaAsync();
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

        private static string QuoteArgument(string argument)
        {
            return argument.Contains(' ') ? $"\"{argument}\"" : argument;
        }
    }
}
