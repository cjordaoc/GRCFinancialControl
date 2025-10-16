using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using CommunityToolkit.Mvvm.Messaging;
using GRCFinancialControl.Avalonia.Services;
using GRCFinancialControl.Avalonia.Services.Interfaces;
using GRCFinancialControl.Avalonia.ViewModels;
using GRCFinancialControl.Avalonia.ViewModels.Dialogs;
using GRCFinancialControl.Avalonia.Views;
using GRCFinancialControl.Core.Configuration;
using GRCFinancialControl.Core.Enums;
using GRCFinancialControl.Persistence;
using GRCFinancialControl.Persistence.Configuration;
using GRCFinancialControl.Persistence.Services;
using GRCFinancialControl.Persistence.Services.Importers;
using GRCFinancialControl.Persistence.Services.Interfaces;
using GRCFinancialControl.Persistence.Services.Dataverse;
using GRCFinancialControl.Persistence.Services.Dataverse.Provisioning;
using GRCFinancialControl.Persistence.Services.People;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.IO;
using AvaloniaWebView;

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

        public override void OnFrameworkInitializationCompleted()
        {
            LiveCharts.Configure(config =>
                config
                    .AddSkiaSharp()
                    .AddDefaultMappers()
                    .AddLightTheme());

            var services = new ServiceCollection();

            var dataBackend = BackendPreferenceResolver.Resolve(CreateSettingsDbContext);
            services.AddSingleton(new DataBackendOptions(dataBackend));

            // Register SettingsDbContext
            services.AddDbContext<SettingsDbContext>(options =>
                options.UseSqlite(SettingsDatabaseOptions.BuildConnectionString()));

            // Register SettingsService
            services.AddTransient<ISettingsService, SettingsService>();

            ServiceProvider? tempServices = null;

            if (dataBackend == DataBackend.MySql)
            {
                // Build a temporary service provider to get the settings
                tempServices = services.BuildServiceProvider();
                using (var scope = tempServices.CreateScope())
                {
                    var scopedProvider = scope.ServiceProvider;

                    // Ensure the local settings database exists before querying it
                    var settingsDbContext = scopedProvider.GetRequiredService<SettingsDbContext>();
                    settingsDbContext.Database.EnsureCreated();

                    var settingsService = scopedProvider.GetRequiredService<ISettingsService>();
                    var settings = settingsService.GetAllAsync().GetAwaiter().GetResult();

                    settings.TryGetValue(SettingKeys.Server, out var server);
                    settings.TryGetValue(SettingKeys.Database, out var database);
                    settings.TryGetValue(SettingKeys.User, out var user);
                    settings.TryGetValue(SettingKeys.Password, out var password);

                    var connectionString = $"Server={server};Database={database};User ID={user};Password={password};";

                    // Register ApplicationDbContext with MySQL
                    services.AddDbContextFactory<ApplicationDbContext>(options =>
                        options.UseMySql(connectionString, new MySqlServerVersion(new Version(8, 0, 29))));
                    services.AddSingleton<IPersonDirectory, NullPersonDirectory>();
                }

                if (tempServices is IDisposable disposableServices)
                {
                    disposableServices.Dispose();
                }

                services.AddSingleton<IDataverseProvisioningService, DisabledDataverseProvisioningService>();
            }
            else
            {
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
            }

            // Register other services
            services.AddTransient<IEngagementService, EngagementService>();
            services.AddTransient<IFiscalYearService, FiscalYearService>();
            services.AddTransient<IFullManagementDataImporter, FullManagementDataImporter>();
            services.AddTransient<IImportService, ImportService>();
            services.AddTransient<IClosingPeriodService, ClosingPeriodService>();
            services.AddTransient<IPlannedAllocationService, PlannedAllocationService>();
            services.AddTransient<IReportService, ReportService>();
            services.AddTransient<IPapdService, PapdService>();
            if (dataBackend == DataBackend.MySql)
            {
                services.AddTransient<IManagerService, ManagerService>();
                services.AddTransient<IManagerAssignmentService, ManagerAssignmentService>();
                services.AddTransient<IExceptionService, ExceptionService>();
                services.AddTransient<ICustomerService, CustomerService>();
            }
            else
            {
                services.AddTransient<IManagerService, DataverseManagerService>();
                services.AddTransient<IManagerAssignmentService, ManagerAssignmentService>();
                services.AddTransient<IExceptionService, ExceptionService>();
                services.AddTransient<ICustomerService, DataverseCustomerService>();
            }
            if (dataBackend == DataBackend.MySql)
            {
                services.AddSingleton<IDatabaseSchemaInitializer, DatabaseSchemaInitializer>();
            }
            else
            {
                services.AddSingleton<IDatabaseSchemaInitializer, DataverseSchemaInitializer>();
            }

            // Register LoggingService
            services.AddSingleton<ILoggingService, LoggingService>();

            // Register .NET logging abstractions for service constructors
            services.AddLogging(builder => builder.AddConsole());

            // Register Messenger
            services.AddSingleton<IMessenger>(WeakReferenceMessenger.Default);

            // Register dialog service
            services.AddSingleton<IDialogService, DialogService>();
            services.AddTransient<IExportService, ExportService>();
            services.AddSingleton<IPowerBiEmbeddingService, PowerBiEmbeddingService>();

            // Register ViewModels
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
                    settingsDbContext.Database.EnsureCreated();

                    if (dataBackend == DataBackend.MySql)
                    {
                        settingsDbContext.Database.Migrate();
                    }
                }
            }

            base.OnFrameworkInitializationCompleted();
        }

        private static SettingsDbContext CreateSettingsDbContext()
        {
            var optionsBuilder = new DbContextOptionsBuilder<SettingsDbContext>();
            optionsBuilder.UseSqlite(SettingsDatabaseOptions.BuildConnectionString());
            return new SettingsDbContext(optionsBuilder.Options);
        }

        [Obsolete("Use BackendPreferenceResolver.Resolve.")]
        private static DataBackend ResolveBackendPreference()
        {
            return BackendPreferenceResolver.Resolve(CreateSettingsDbContext);
        }

        [Obsolete("Use DataverseConnectionOptionsProvider.Resolve.")]
        private static DataverseConnectionOptions ResolveDataverseOptions(ServiceCollection _)
        {
            return DataverseConnectionOptionsProvider.Resolve(CreateSettingsDbContext);
        }
    }
}