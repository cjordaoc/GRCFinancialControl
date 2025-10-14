using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using CommunityToolkit.Mvvm.Messaging;
using GRCFinancialControl.Avalonia.Services;
using GRCFinancialControl.Avalonia.Services.Interfaces;
using GRCFinancialControl.Avalonia.ViewModels;
using GRCFinancialControl.Avalonia.Views;
using GRCFinancialControl.Persistence;
using GRCFinancialControl.Persistence.Services;
using GRCFinancialControl.Persistence.Services.Interfaces;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;

namespace GRCFinancialControl.Avalonia
{
    public partial class App : Application
    {
        public IServiceProvider Services { get; private set; } = null!;

        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            LiveCharts.Configure(config =>
                config
                    .AddSkiaSharp()
                    .AddDefaultMappers()
                    .AddLightTheme());

            var services = new ServiceCollection();

            // Register SettingsDbContext
            services.AddDbContext<SettingsDbContext>(options =>
                options.UseSqlite("Data Source=settings.db"));

            // Register SettingsService
            services.AddTransient<ISettingsService, SettingsService>();

            // Build a temporary service provider to get the settings
            var tempServices = services.BuildServiceProvider();
            using (var scope = tempServices.CreateScope())
            {
                var scopedProvider = scope.ServiceProvider;

                // Ensure the local settings database exists before querying it
                var settingsDbContext = scopedProvider.GetRequiredService<SettingsDbContext>();
                settingsDbContext.Database.EnsureCreated();

                var settingsService = scopedProvider.GetRequiredService<ISettingsService>();
                var settings = settingsService.GetAllAsync().GetAwaiter().GetResult();

                settings.TryGetValue("Server", out var server);
                settings.TryGetValue("Database", out var database);
                settings.TryGetValue("User", out var user);
                settings.TryGetValue("Password", out var password);

                var connectionString = $"Server={server};Database={database};User ID={user};Password={password};";

                // Register ApplicationDbContext with MySQL
                services.AddDbContextFactory<ApplicationDbContext>(options =>
                    options.UseMySql(connectionString, new MySqlServerVersion(new Version(8, 0, 29))));
            }

            if (tempServices is IDisposable disposableServices)
            {
                disposableServices.Dispose();
            }

            // Register other services
            services.AddTransient<IEngagementService, EngagementService>();
            services.AddTransient<IFiscalYearService, FiscalYearService>();
            services.AddTransient<IImportService, ImportService>();
            services.AddTransient<IClosingPeriodService, ClosingPeriodService>();
            services.AddTransient<IPlannedAllocationService, PlannedAllocationService>();
            services.AddTransient<IReportService, ReportService>();
            services.AddTransient<IPapdService, PapdService>();
            services.AddTransient<IExceptionService, ExceptionService>();
            services.AddTransient<ICustomerService, CustomerService>();
            services.AddSingleton<IDatabaseSchemaInitializer, DatabaseSchemaInitializer>();

            // Register LoggingService
            services.AddSingleton<ILoggingService, LoggingService>();

            // Register .NET logging abstractions for service constructors
            services.AddLogging(builder => builder.AddConsole());

            // Register Messenger
            services.AddSingleton<IMessenger>(WeakReferenceMessenger.Default);

            // Register dialog service
            services.AddSingleton<IDialogService, DialogService>();
            services.AddTransient<IExportService, ExportService>();

            // Register ViewModels
            services.AddTransient<MainWindowViewModel>();
            services.AddTransient<EngagementsViewModel>();
            services.AddTransient<FiscalYearsViewModel>();
            services.AddTransient<ImportViewModel>();
            services.AddTransient<AllocationViewModel>();
            services.AddTransient(sp => new ReportsViewModel(
                sp.GetRequiredService<ReportFilterViewModel>(),
                sp.GetRequiredService<PlannedVsActualViewModel>(),
                sp.GetRequiredService<BacklogViewModel>(),
                sp.GetRequiredService<FiscalPerformanceViewModel>(),
                sp.GetRequiredService<EngagementPerformanceViewModel>(),
                sp.GetRequiredService<PapdContributionViewModel>(),
                sp.GetRequiredService<TimeAllocationViewModel>(),
                sp.GetRequiredService<StrategicKpiViewModel>(),
                sp.GetRequiredService<FinancialEvolutionViewModel>(),
                sp.GetRequiredService<IMessenger>()
            ));
            services.AddTransient<FiscalPerformanceViewModel>();
            services.AddTransient<EngagementPerformanceViewModel>();
            services.AddTransient<PapdContributionViewModel>();
            services.AddTransient<TimeAllocationViewModel>();
            services.AddTransient<StrategicKpiViewModel>();
            services.AddTransient<FinancialEvolutionViewModel>();
            services.AddTransient<ReportFilterViewModel>();
            services.AddTransient<PlannedVsActualViewModel>();
            services.AddTransient<BacklogViewModel>();
            services.AddTransient<PapdViewModel>();
            services.AddTransient<ExceptionsViewModel>();
            services.AddTransient<SettingsViewModel>();
            services.AddTransient<ClosingPeriodsViewModel>();
            services.AddTransient<ClosingPeriodEditorViewModel>();
            services.AddTransient<EngagementPapdAssignmentViewModel>();
            services.AddTransient<CustomersViewModel>();
            services.AddTransient<CustomerEditorViewModel>();
            services.AddTransient<Func<EngagementPapdAssignmentViewModel>>(sp => () => sp.GetRequiredService<EngagementPapdAssignmentViewModel>());

            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var mainWindow = new MainWindow();

                // Register FilePickerService with the created main window
                services.AddSingleton<IFilePickerService>(new FilePickerService(mainWindow));

                Services = services.BuildServiceProvider();

                desktop.MainWindow = mainWindow;
                desktop.MainWindow.DataContext = Services.GetRequiredService<MainWindowViewModel>();

                // Apply migrations at startup
                using (var scope = Services.CreateScope())
                {
                    var schemaInitializer = scope.ServiceProvider.GetRequiredService<IDatabaseSchemaInitializer>();
                    schemaInitializer.EnsureSchemaAsync().GetAwaiter().GetResult();

                    var settingsDbContext = scope.ServiceProvider.GetRequiredService<SettingsDbContext>();
                    settingsDbContext.Database.Migrate();
                }
            }

            base.OnFrameworkInitializationCompleted();
        }
    }
}