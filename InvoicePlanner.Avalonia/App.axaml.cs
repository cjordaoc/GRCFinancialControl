using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using CommunityToolkit.Mvvm.Messaging;
using GRCFinancialControl.Persistence;
using Invoices.Core.Validation;
using Invoices.Data.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Pomelo.EntityFrameworkCore.MySql.Infrastructure;
using InvoicePlanner.Avalonia.Services;
using InvoicePlanner.Avalonia.ViewModels;
using InvoicePlanner.Avalonia.Views;

namespace InvoicePlanner.Avalonia;

public partial class App : Application
{
    private IHost? _host;

    public IServiceProvider Services => _host?.Services ?? throw new InvalidOperationException("Host not initialised.");

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        _host = Host.CreateDefaultBuilder()
            .ConfigureAppConfiguration((context, config) =>
            {
                config.SetBasePath(AppContext.BaseDirectory);
                config.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
                config.AddJsonFile($"appsettings.{context.HostingEnvironment.EnvironmentName}.json", optional: true);
                config.AddEnvironmentVariables(prefix: "INVOICEPLANNER_");
            })
            .ConfigureServices((context, services) =>
            {
                services.AddLogging(logging => logging.AddConsole());

                services.AddDbContextFactory<ApplicationDbContext>(options =>
                {
                    const string fallback = "Server=localhost;Database=InvoicePlanner;User ID=planner;Password=planner;";
                    var connectionString = context.Configuration.GetConnectionString("MySql");
                    var serverVersion = new MySqlServerVersion(new Version(8, 0, 36));
                    options.UseMySql(string.IsNullOrWhiteSpace(connectionString) ? fallback : connectionString, serverVersion);
                });

                services.AddSingleton<IInvoicePlanValidator, InvoicePlanValidator>();
                services.AddTransient<IInvoicePlanRepository, InvoicePlanRepository>();
                services.AddSingleton<InvoiceSummaryExporter>();
                services.AddSingleton<IErrorDialogService, ErrorDialogService>();
                services.AddSingleton<IGlobalErrorHandler, GlobalErrorHandler>();
                services.AddTransient<ErrorDialogViewModel>();
                services.AddTransient<ErrorDialog>();
                services.AddSingleton<IMessenger>(WeakReferenceMessenger.Default);
                services.AddSingleton<PlanEditorViewModel>();
                services.AddSingleton<RequestConfirmationViewModel>();
                services.AddSingleton<EmissionConfirmationViewModel>();
                services.AddSingleton<InvoiceSummaryViewModel>();
                services.AddSingleton<NotificationPreviewViewModel>();
                services.AddSingleton<HomeViewModel>();
                services.AddSingleton<MainWindowViewModel>();
                services.AddSingleton<MainWindow>();
            })
            .Build();

        _host.Start();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var mainWindow = Services.GetRequiredService<MainWindow>();
            mainWindow.DataContext = Services.GetRequiredService<MainWindowViewModel>();
            desktop.MainWindow = mainWindow;
            desktop.Exit += (_, _) => DisposeHostAsync().GetAwaiter().GetResult();

            var errorHandler = Services.GetRequiredService<IGlobalErrorHandler>();
            errorHandler.Register(desktop);
        }

        base.OnFrameworkInitializationCompleted();
    }

    private async Task DisposeHostAsync()
    {
        if (Services is IServiceProvider provider)
        {
            if (provider.GetService(typeof(IGlobalErrorHandler)) is IDisposable disposableHandler)
            {
                disposableHandler.Dispose();
            }
        }

        if (_host is IAsyncDisposable asyncDisposable)
        {
            await asyncDisposable.DisposeAsync().ConfigureAwait(false);
        }
        else
        {
            _host?.Dispose();
        }
    }
}
