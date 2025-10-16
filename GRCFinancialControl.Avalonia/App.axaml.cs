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
using GRCFinancialControl.Persistence.Services;
using GRCFinancialControl.Persistence.Services.Importers;
using GRCFinancialControl.Persistence.Services.Interfaces;
using GRCFinancialControl.Persistence.Services.Dataverse;
using GRCFinancialControl.Persistence.Services.People;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
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

            var dataBackend = ResolveBackendPreference();
            services.AddSingleton(new DataBackendOptions(dataBackend));

            // Register SettingsDbContext
            services.AddDbContext<SettingsDbContext>(options =>
                options.UseSqlite("Data Source=settings.db"));

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

                    settings.TryGetValue("Server", out var server);
                    settings.TryGetValue("Database", out var database);
                    settings.TryGetValue("User", out var user);
                    settings.TryGetValue("Password", out var password);

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
            }
            else
            {
                services.AddSingleton<IDbContextFactory<ApplicationDbContext>, UnsupportedApplicationDbContextFactory>();
                var dataverseOptions = ResolveDataverseOptions(services);
                services.AddSingleton(dataverseOptions);
                services.AddSingleton(provider =>
                {
                    var prefix = Environment.GetEnvironmentVariable("DV_CUSTOMIZATION_PREFIX");
                    return DataverseEntityMetadataRegistry.CreateDefault(string.IsNullOrWhiteSpace(prefix) ? "grc" : prefix);
                });
                services.AddSingleton<IDataverseServiceClientFactory, DataverseServiceClientFactory>();
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

        private static DataBackend ResolveBackendPreference()
        {
            var environmentValue = Environment.GetEnvironmentVariable(DataBackendConfiguration.EnvironmentVariableName);
            if (!string.IsNullOrWhiteSpace(environmentValue) && Enum.TryParse(environmentValue, ignoreCase: true, out DataBackend backendFromEnvironment))
            {
                return backendFromEnvironment;
            }

            try
            {
                var optionsBuilder = new DbContextOptionsBuilder<SettingsDbContext>();
                optionsBuilder.UseSqlite("Data Source=settings.db");
                using var context = new SettingsDbContext(optionsBuilder.Options);
                context.Database.EnsureCreated();

                var storedValue = context.Settings.AsNoTracking()
                    .FirstOrDefault(s => s.Key == SettingKeys.DataBackendPreference)?.Value;

                if (!string.IsNullOrWhiteSpace(storedValue) && Enum.TryParse(storedValue, ignoreCase: true, out DataBackend storedBackend))
                {
                    return storedBackend;
                }
            }
            catch
            {
                // Ignore errors and fall back to the default backend.
            }

            return DataBackend.MySql;
        }

        private static DataverseConnectionOptions ResolveDataverseOptions(ServiceCollection services)
        {
            ServiceProvider? tempProvider = null;

            try
            {
                tempProvider = services.BuildServiceProvider();
                using var scope = tempProvider.CreateScope();
                var scopedProvider = scope.ServiceProvider;
                var settingsDbContext = scopedProvider.GetRequiredService<SettingsDbContext>();
                settingsDbContext.Database.EnsureCreated();

                var settingsService = scopedProvider.GetRequiredService<ISettingsService>();

                if (DataverseConnectionOptions.TryFromEnvironment(out var environmentOptions) && environmentOptions is not null)
                {
                    return environmentOptions;
                }

                var storedSettings = settingsService.GetDataverseSettingsAsync().GetAwaiter().GetResult();
                return DataverseConnectionOptions.FromSettings(storedSettings);
            }
            catch (InvalidOperationException ex)
            {
                throw new InvalidOperationException("Configure Dataverse credentials in Settings before selecting the Dataverse backend.", ex);
            }
            finally
            {
                if (tempProvider is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }
        }
    }
}