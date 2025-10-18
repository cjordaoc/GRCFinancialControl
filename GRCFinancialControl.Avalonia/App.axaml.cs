using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
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
            LiveCharts.Configure(config =>
                config
                    .AddSkiaSharp()
                    .AddDefaultMappers()
                    .AddLightTheme());

            var services = new ServiceCollection();

            services.AddDbContext<SettingsDbContext>(options =>
                options.UseSqlite(SettingsDatabaseOptions.BuildConnectionString()));
            services.AddTransient<ISettingsService, SettingsService>();

            using (var tempProvider = services.BuildServiceProvider())
            {
                using var scope = tempProvider.CreateScope();
                var scopedProvider = scope.ServiceProvider;
                var settingsDbContext = scopedProvider.GetRequiredService<SettingsDbContext>();
                await settingsDbContext.Database.EnsureCreatedAsync();

                var settingsService = scopedProvider.GetRequiredService<ISettingsService>();
                var settings = await settingsService.GetAllAsync();
                settings.TryGetValue(SettingKeys.Server, out var server);
                settings.TryGetValue(SettingKeys.Database, out var database);
                settings.TryGetValue(SettingKeys.User, out var user);
                settings.TryGetValue(SettingKeys.Password, out var password);

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
            services.AddTransient<IImportService, ImportService>();
            services.AddTransient<IClosingPeriodService, ClosingPeriodService>();
            services.AddTransient<IPlannedAllocationService, PlannedAllocationService>();
            services.AddTransient<IReportService, ReportService>();
            services.AddTransient<IPapdService, PapdService>();
            services.AddTransient<IManagerService, ManagerService>();
            services.AddTransient<IManagerAssignmentService, ManagerAssignmentService>();
            services.AddTransient<IExceptionService, ExceptionService>();
            services.AddTransient<ICustomerService, CustomerService>();
            services.AddSingleton<IDatabaseSchemaInitializer, DatabaseSchemaInitializer>();

            services.AddSingleton<ILoggingService, LoggingService>();
            services.AddLogging(builder => builder.AddConsole());
            services.AddSingleton<IMessenger>(WeakReferenceMessenger.Default);
            services.AddSingleton<IDialogService, DialogService>();
            services.AddTransient<IExportService, ExportService>();
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
            services.AddTransient<TasksViewModel>();

            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var mainWindow = new MainWindow();
                services.AddSingleton<IFilePickerService>(new FilePickerService(mainWindow));

                Services = services.BuildServiceProvider();

                mainWindow.DataContext = Services.GetRequiredService<MainWindowViewModel>();

                using (var scope = Services.CreateScope())
                {
                    var provider = scope.ServiceProvider;
                    var schemaInitializer = provider.GetRequiredService<IDatabaseSchemaInitializer>();
                    await schemaInitializer.EnsureSchemaAsync();

                    var settingsDbContext = provider.GetRequiredService<SettingsDbContext>();
                    await settingsDbContext.Database.EnsureCreatedAsync();
                    await settingsDbContext.Database.MigrateAsync();
                }

                desktop.MainWindow = mainWindow;
                mainWindow.Show();
            }

            base.OnFrameworkInitializationCompleted();
        }
    }
}
