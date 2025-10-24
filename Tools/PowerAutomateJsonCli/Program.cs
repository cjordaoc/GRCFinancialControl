using System.Globalization;
using GRCFinancialControl.Persistence;
using GRCFinancialControl.Persistence.Services.Exporters.Json;
using GRCFinancialControl.Persistence.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = Host.CreateApplicationBuilder(args);

builder.Configuration
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
    .AddEnvironmentVariables("GRC_")
    .AddCommandLine(args, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["--from"] = "PowerAutomateExport:InvoiceStartDate",
        ["--to"] = "PowerAutomateExport:InvoiceEndDate",
        ["--fiscalYear"] = "PowerAutomateExport:FiscalYearName",
        ["--manager"] = "PowerAutomateExport:Managers",
        ["--scheduledAt"] = "PowerAutomateExport:ScheduledAt",
        ["--timezone"] = "PowerAutomateExport:Timezone",
        ["--locale"] = "PowerAutomateExport:Locale",
        ["--connection"] = "ApplicationDb:ConnectionString"
    });

builder.Services.AddLogging(logging => logging.AddConsole());

var connectionString = ResolveConnectionString(builder.Configuration);

builder.Services.AddDbContextFactory<ApplicationDbContext>(options =>
    options.UseMySql(
        connectionString,
        ServerVersion.AutoDetect(connectionString),
        mysql => mysql.EnableRetryOnFailure()));

builder.Services.AddTransient<IJsonExportService, JsonExportService>();
builder.Services.AddSingleton<IPowerAutomateJsonPayloadBuilder, PowerAutomateJsonPayloadBuilder>();
builder.Services.AddSingleton<IPowerAutomateExportTelemetry, LoggingPowerAutomateExportTelemetry>();
builder.Services.AddSingleton<IPowerAutomateJsonFileExporter, PowerAutomateJsonFileExporter>();

using var host = builder.Build();

using var scope = host.Services.CreateScope();
var serviceProvider = scope.ServiceProvider;
var exporter = serviceProvider.GetRequiredService<IPowerAutomateJsonFileExporter>();
var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
var logger = loggerFactory.CreateLogger("PowerAutomateJsonCli");

using var cancellation = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    cancellation.Cancel();
};

try
{
    var request = BuildRequest(builder.Configuration);
    logger.LogInformation("Starting Power Automate JSON export using connection {Connection}.", connectionString);
    var outputPath = await exporter.ExportAsync(request, cancellation.Token);
    logger.LogInformation("Export completed successfully. Output path: {OutputPath}.", outputPath);
    return 0;
}
catch (OperationCanceledException)
{
    logger.LogWarning("Export cancelled by user.");
    return 2;
}
catch (Exception ex)
{
    logger.LogError(ex, "Failed to generate Power Automate JSON export.");
    return 1;
}

static string ResolveConnectionString(IConfiguration configuration)
{
    var connection = configuration["ApplicationDb:ConnectionString"]
        ?? configuration.GetConnectionString("ApplicationDb");

    if (string.IsNullOrWhiteSpace(connection))
    {
        throw new InvalidOperationException(
            "ApplicationDb connection string is required. Provide via appsettings.json, environment variables, or the --connection argument.");
    }

    return connection;
}

static PowerAutomateJsonExportRequest BuildRequest(IConfiguration configuration)
{
    var scheduledAt = ParseDateTimeOffset(configuration["PowerAutomateExport:ScheduledAt"]);
    var timezone = configuration["PowerAutomateExport:Timezone"];
    var locale = configuration["PowerAutomateExport:Locale"];
    var invoiceStart = ParseDate(configuration["PowerAutomateExport:InvoiceStartDate"]);
    var invoiceEnd = ParseDate(configuration["PowerAutomateExport:InvoiceEndDate"]);
    var fiscalYear = configuration["PowerAutomateExport:FiscalYearName"];
    var managerSection = configuration.GetSection("PowerAutomateExport:Managers");
    var managerValues = managerSection.Get<string[]>() ?? Array.Empty<string>();
    var managerFilter = managerValues
        .Select(value => value?.Trim())
        .Where(value => !string.IsNullOrWhiteSpace(value))
        .Select(value => value!)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();

    return new PowerAutomateJsonExportRequest
    {
        ScheduledAt = scheduledAt,
        Timezone = string.IsNullOrWhiteSpace(timezone) ? null : timezone,
        Locale = string.IsNullOrWhiteSpace(locale) ? null : locale,
        Filters = new PowerAutomateJsonExportFilters
        {
            InvoiceStartDate = invoiceStart,
            InvoiceEndDate = invoiceEnd,
            FiscalYearName = string.IsNullOrWhiteSpace(fiscalYear) ? null : fiscalYear,
            ManagerEmails = managerFilter.Length > 0 ? managerFilter : null
        }
    };
}

static DateTimeOffset ParseDateTimeOffset(string? value)
{
    if (string.IsNullOrWhiteSpace(value))
    {
        return DateTimeOffset.Now;
    }

    return DateTimeOffset.TryParse(
        value,
        CultureInfo.InvariantCulture,
        DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
        out var result)
        ? result
        : DateTimeOffset.Parse(value, CultureInfo.CurrentCulture, DateTimeStyles.AssumeLocal);
}

static DateTime? ParseDate(string? value)
{
    if (string.IsNullOrWhiteSpace(value))
    {
        return null;
    }

    return DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var result)
        ? result.Date
        : DateTime.Parse(value, CultureInfo.CurrentCulture, DateTimeStyles.AssumeLocal).Date;
}
