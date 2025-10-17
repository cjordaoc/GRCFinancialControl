using App.Presentation.Views;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using CommunityToolkit.Mvvm.Messaging;
using GRCFinancialControl.Avalonia.Services;
using GRCFinancialControl.Avalonia.Services.Interfaces;
using GRCFinancialControl.Avalonia.ViewModels;
using GRCFinancialControl.Avalonia.ViewModels.Dialogs;
using GRCFinancialControl.Avalonia.Views;
using GRCFinancialControl.Core.Authentication;
using GRCFinancialControl.Core.Configuration;
using GRCFinancialControl.Core.Enums;
using GRCFinancialControl.Persistence;
using GRCFinancialControl.Persistence.Authentication;
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
using System.Linq;
using System.Threading.Tasks;
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
                        options.UseMySql(
                            connectionString,
                            new MySqlServerVersion(new Version(8, 0, 29)),
                            mySqlOptions => mySqlOptions.EnableRetryOnFailure()));
                    services.AddSingleton<IPersonDirectory, NullPersonDirectory>();
                }

                if (tempServices is IDisposable disposableServices)
                {
                    disposableServices.Dispose();
                }

                services.AddSingleton<IDataverseProvisioningService, DisabledDataverseProvisioningService>();
                services.AddSingleton<IInteractiveAuthService, DisabledInteractiveAuthService>();
                services.AddSingleton<IDataverseClientFactory, DisabledDataverseClientFactory>();
            }
            else
            {
                services.AddSingleton<IDbContextFactory<ApplicationDbContext>, UnsupportedApplicationDbContextFactory>();
                var dataverseOptions = DataverseConnectionOptionsProvider.Resolve(CreateSettingsDbContext);
                services.AddSingleton(dataverseOptions);
                services.AddSingleton<IAuthConfig>(provider =>
                {
                    var options = provider.GetRequiredService<DataverseConnectionOptions>();
                    var authority = string.IsNullOrWhiteSpace(options.TenantId)
                        ? "https://login.microsoftonline.com/common"
                        : $"https://login.microsoftonline.com/{options.TenantId}";
                    var orgUri = new Uri(options.OrgUrl);
                    var scope = $"{orgUri.AbsoluteUri.TrimEnd('/')}/user_impersonation";
                    return new AuthConfig(options.ClientId, authority, orgUri, new[] { scope });
                });
                services.AddSingleton<IParentWindowProvider, AvaloniaParentWindowProvider>();
                services.AddSingleton<IInteractiveAuthService, InteractiveAuthService>();
                services.AddSingleton<IDataverseClientFactory, DataverseClientFactory>();
                services.AddSingleton(_ => DataverseEntityMetadataRegistry.CreateDefault());
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
            if (dataBackend == DataBackend.MySql)
            {
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
            }
            else
            {
                services.AddTransient<IEngagementService, DataverseEngagementService>();
                services.AddTransient<IFiscalYearService, DataverseFiscalYearService>();
                services.AddTransient<IFullManagementDataImporter, DataverseFullManagementDataImporter>();
                services.AddTransient<IImportService, DataverseImportService>();
                services.AddTransient<IClosingPeriodService, DataverseClosingPeriodService>();
                services.AddTransient<IPlannedAllocationService, DataversePlannedAllocationService>();
                services.AddTransient<IReportService, DataverseReportService>();
                services.AddTransient<IPapdService, DataversePapdService>();
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
            services.AddTransient<IStartupAuthenticationService, StartupAuthenticationService>();

            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var mainWindow = new MainWindow();
                services.AddSingleton<IFilePickerService>(new FilePickerService(mainWindow));

                Services = services.BuildServiceProvider();

                mainWindow.DataContext = Services.GetRequiredService<MainWindowViewModel>();

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

                if (dataBackend == DataBackend.Dataverse)
                {
                    var splashVm = new global::App.Presentation.ViewModels.AuthenticationSplashWindowViewModel();
                    var splash = new AuthenticationSplashWindow
                    {
                        DataContext = splashVm
                    };
                    desktop.MainWindow = splash;

                    splash.Opened += async (_, _) =>
                    {
                        var failureMessage = await TryAuthenticateOnStartupAsync(splashVm).ConfigureAwait(true);

                        if (!string.IsNullOrEmpty(failureMessage))
                        {
                            async void Handler(object? sender, EventArgs args)
                            {
                                mainWindow.Opened -= Handler;
                                var dialogService = Services.GetRequiredService<IDialogService>();
                                await dialogService.ShowConfirmationAsync("Dataverse sign-in", failureMessage);
                            }

                            mainWindow.Opened += Handler;
                        }

                        desktop.MainWindow = mainWindow;
                        mainWindow.Show();
                        splash.Close();
                    };
                }
                else
                {
                    desktop.MainWindow = mainWindow;
                    mainWindow.Show();
                }
            }

            base.OnFrameworkInitializationCompleted();
        }

        private async Task<string?> TryAuthenticateOnStartupAsync(global::App.Presentation.ViewModels.AuthenticationSplashWindowViewModel splashVm)
        {
            var options = Services.GetService<DataverseConnectionOptions>();
            if (options is null)
            {
                return null;
            }

            if (options.AuthMode != DataverseAuthMode.Interactive)
            {
                splashVm.StatusText = "Using application authentication.";
                await Task.Delay(400).ConfigureAwait(true);
                return null;
            }

            var authService = Services.GetRequiredService<IStartupAuthenticationService>();
            return await authService.AuthenticateAsync(
                status => splashVm.StatusText = status,
                isProgressVisible => splashVm.IsProgressVisible = isProgressVisible
            ).ConfigureAwait(true);
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