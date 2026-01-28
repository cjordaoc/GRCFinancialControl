using System;
using System.IO;
using CommunityToolkit.Mvvm.Messaging;
using GRCFinancialControl.Avalonia;
using GRCFinancialControl.Avalonia.Services;
using GRCFinancialControl.Avalonia.Services.Interfaces;
using GRCFinancialControl.Avalonia.Services.Logging;
using GRCFinancialControl.Avalonia.ViewModels;
using GRCFinancialControl.Avalonia.ViewModels.Dialogs;
using GRCFinancialControl.Avalonia.Views;
using GRCFinancialControl.Persistence;
using GRCFinancialControl.Persistence.Configuration;
using GRCFinancialControl.Persistence.Services;
using GRCFinancialControl.Persistence.Services.Exporters;
using GRCFinancialControl.Persistence.Services.Importers;
using GRCFinancialControl.Persistence.Services.Importers.Budget;
using GRCFinancialControl.Persistence.Services.Interfaces;
using GRCFinancialControl.Persistence.Services.People;
using GRC.Shared.Core.Services;
using GRC.Shared.UI.Dialogs;
using GRC.Shared.UI.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Pomelo.EntityFrameworkCore.MySql.Infrastructure;

namespace GRCFinancialControl.Avalonia.Services.DependencyInjection;

/// <summary>
/// Configures application services for GRC Financial Control Avalonia app.
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAvaloniaAppServices(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddLogging(builder =>
        {
            builder.AddConsole();

            var logDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "GRCFinancialControl",
                "Logs");

            builder.AddProvider(new FileLoggerProvider(logDirectory, null, LogLevel.Debug));
        });

        services.AddDbContext<SettingsDbContext>(options =>
            options.UseSqlite(SettingsDatabaseOptions.BuildConnectionString()));
        services.AddTransient<ISettingsService, SettingsService>();

        services.AddSingleton<IDatabaseConnectionAvailability>(_ => new DatabaseConnectionAvailability(false));

        services.AddDbContextFactory<ApplicationDbContext>((provider, options) =>
        {
            var availability = provider.GetRequiredService<IDatabaseConnectionAvailability>();
            var settingsService = provider.GetRequiredService<ISettingsService>();
            var settings = settingsService.GetAll();

            if (!App.TryBuildConnectionString(settings, out var connectionString))
            {
                availability.Update(false);
                throw new InvalidOperationException("Database connection is not configured. Please provide valid MySQL settings.");
            }

            options.UseMySql(
                connectionString,
                new MySqlServerVersion(new Version(8, 0, 29)),
                mySqlOptions => mySqlOptions.EnableRetryOnFailure());

            availability.Update(true);
        });

        services.AddSingleton<IPersonDirectory, NullPersonDirectory>();

        services.AddTransient<IEngagementService, EngagementService>();
        services.AddTransient<IFiscalYearService, FiscalYearService>();
        services.AddTransient<IFullManagementDataImporter, FullManagementDataImporter>();
        services.AddTransient<BudgetImporter>();
        services.AddTransient<AllocationPlanningImporter>();
        services.AddTransient<IFiscalCalendarConsistencyService, FiscalCalendarConsistencyService>();
        services.AddTransient<IHoursAllocationService, HoursAllocationService>();
        services.AddTransient<IAllocationSnapshotService, AllocationSnapshotService>();
        services.AddTransient<IImportService, ImportService>();
        services.AddTransient<IClosingPeriodService, ClosingPeriodService>();
        services.AddTransient<IPlannedAllocationService, PlannedAllocationService>();
        services.AddTransient<IReportService, ReportService>();
        services.AddTransient<IPapdService, PapdService>();
        services.AddTransient<IManagerService, ManagerService>();
        services.AddTransient<IManagerAssignmentService, ManagerAssignmentService>();
        services.AddTransient<IPapdAssignmentService, PapdAssignmentService>();
        services.AddTransient<IEngagementManagementFacade, EngagementManagementFacade>();
        services.AddTransient<IExceptionService, ExceptionService>();
        services.AddTransient<ICustomerService, CustomerService>();
        services.AddTransient<IRankMappingService, RankMappingService>();

        services.AddSingleton<IDatabaseSchemaInitializer, DatabaseSchemaInitializer>();

        services.AddSingleton<LoggingService>();
        services.AddSingleton<IMessenger>(_ => WeakReferenceMessenger.Default);
        services.AddSingleton<IModalDialogService, ModalDialogService>();
        services.AddSingleton<DialogService>();
        services.AddSingleton<IDialogService>(provider => provider.GetRequiredService<DialogService>());
        services.AddSingleton<IPresenterService>(provider => new PresenterService(provider.GetRequiredService<LoggingService>()));
        services.AddTransient<IRetainTemplateGenerator, RetainTemplateGenerator>();
        services.AddSingleton<IConnectionPackageService, ConnectionPackageService>();
        services.AddSingleton<IApplicationDataBackupService, ApplicationDataBackupService>();
        services.AddSingleton<PowerBiEmbeddingService>();
        services.AddSingleton<ITabDelimitedExportService, TabDelimitedExportService>();

        services.AddSingleton<MainWindow>();
        services.AddSingleton(provider => new FilePickerService(provider.GetRequiredService<MainWindow>()));

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
        services.AddTransient<GrcTeamViewModel>();
        services.AddTransient(provider => new ReportsViewModel(
            provider.GetRequiredService<PowerBiEmbeddingService>(),
            provider.GetRequiredService<LoggingService>(),
            provider.GetRequiredService<IMessenger>()));
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
        services.AddTransient<PapdSelectionViewModel>();
        services.AddTransient<PapdEngagementAssignmentViewModel>();
        services.AddTransient<ManagerSelectionViewModel>();

        return services;
    }
}
