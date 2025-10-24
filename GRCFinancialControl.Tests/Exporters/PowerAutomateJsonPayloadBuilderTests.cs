using System;
using System.Collections.Generic;
using System.Text.Json;
using GRCFinancialControl.Persistence.Services.Exporters.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GRCFinancialControl.Tests.Exporters;

public sealed class PowerAutomateJsonPayloadBuilderTests
{
    private static PowerAutomateJsonPayloadBuilder CreateBuilder()
    {
        return new PowerAutomateJsonPayloadBuilder(new NullLogger<PowerAutomateJsonPayloadBuilder>());
    }

    [Fact]
    public void BuildPayload_SortsManagersAndDocuments_AndPopulatesDefaults()
    {
        var builder = CreateBuilder();
        var scheduledAt = new DateTimeOffset(2025, 10, 20, 10, 0, 0, TimeSpan.FromHours(-3));

        var managerData = new List<ManagerEmailData>
        {
            new()
            {
                ManagerName = "beta manager",
                ManagerEmail = "beta.manager@ey.com",
                Invoices = new List<InvoiceEmailData>
                {
                    new()
                    {
                        EngagementCode = "ENG-002",
                        EngagementName = "Engagement Beta",
                        CustomerName = "Customer B",
                        ParcelNumber = 1,
                        TotalParcels = 2,
                        IssueDate = new DateTime(2025, 1, 5),
                        DueDate = new DateTime(2025, 1, 20),
                        Amount = 1500m,
                        Currency = string.Empty,
                        PoNumber = "PO-2"
                    },
                    new()
                    {
                        EngagementCode = "ENG-002",
                        EngagementName = "Engagement Beta",
                        CustomerName = "Customer B",
                        ParcelNumber = 2,
                        TotalParcels = 2,
                        IssueDate = new DateTime(2025, 1, 10),
                        DueDate = new DateTime(2025, 1, 25),
                        Amount = 1600m,
                        Currency = "USD",
                        PoNumber = "PO-2"
                    }
                },
                Etcs = new List<EtcEmailData>
                {
                    new()
                    {
                        EngagementCode = "ENG-002",
                        EngagementName = "Engagement Beta",
                        CustomerName = "Customer B",
                        RankName = "Senior",
                        BudgetHours = 120m,
                        ConsumedHours = 45m,
                        AdditionalHours = 0m,
                        RemainingHours = 75m,
                        Status = "On Track",
                        FiscalYearName = "FY25",
                        LastEtcDate = new DateTime(2025, 1, 12),
                        ProposedCompletionDate = new DateTime(2025, 2, 1)
                    },
                    new()
                    {
                        EngagementCode = "ENG-002",
                        EngagementName = "Engagement Beta",
                        CustomerName = "Customer B",
                        RankName = "Manager",
                        BudgetHours = 80m,
                        ConsumedHours = 20m,
                        AdditionalHours = 0m,
                        RemainingHours = 60m,
                        Status = "Planned",
                        FiscalYearName = "FY25",
                        LastEtcDate = new DateTime(2025, 1, 10),
                        ProposedCompletionDate = new DateTime(2025, 1, 15)
                    }
                }
            },
            new()
            {
                ManagerName = "Alpha Manager",
                ManagerEmail = "alpha.manager@ey.com",
                Invoices = new List<InvoiceEmailData>
                {
                    new()
                    {
                        EngagementCode = "ENG-001",
                        EngagementName = "Engagement Alpha",
                        CustomerName = "Customer A",
                        ParcelNumber = 1,
                        TotalParcels = 1,
                        IssueDate = new DateTime(2025, 1, 1),
                        DueDate = new DateTime(2025, 1, 15),
                        Amount = 1000m,
                        Currency = "EUR",
                        PoNumber = "PO-1"
                    }
                }
            }
        };

        var json = builder.BuildPayload(managerData, scheduledAt, string.Empty, null!);
        using var document = JsonDocument.Parse(json);

        var root = document.RootElement;
        var meta = root.GetProperty("meta");
        Assert.Equal("2025-10-20T10:00:00-03:00", meta.GetProperty("scheduledAt").GetString());
        Assert.Equal("America/Sao_Paulo", meta.GetProperty("timezone").GetString());
        Assert.Equal("pt-BR", meta.GetProperty("locale").GetString());

        var messages = root.GetProperty("messages");
        Assert.Equal(2, messages.GetArrayLength());
        Assert.Equal("Alpha Manager", messages[0].GetProperty("to")[0].GetProperty("name").GetString());
        Assert.Equal("1", messages[0].GetProperty("id").GetString());
        Assert.Equal("2", messages[1].GetProperty("id").GetString());

        var betaInvoices = messages[1].GetProperty("invoices");
        Assert.Equal(2, betaInvoices.GetArrayLength());
        Assert.Equal("2025-01-10", betaInvoices[0].GetProperty("issueDate").GetString());
        Assert.Equal(2, betaInvoices[0].GetProperty("parcelNumber").GetInt32());
        Assert.Equal("USD", betaInvoices[0].GetProperty("currency").GetString());
        Assert.Equal("2025-01-05", betaInvoices[1].GetProperty("issueDate").GetString());
        Assert.Equal("BRL", betaInvoices[1].GetProperty("currency").GetString());

        var betaEtcs = messages[1].GetProperty("etcs");
        Assert.Equal(2, betaEtcs.GetArrayLength());
        Assert.Equal("2025-01-15", betaEtcs[0].GetProperty("proposedCompletionDate").GetString());
        Assert.Equal("Manager", betaEtcs[0].GetProperty("rankName").GetString());
        Assert.Equal("2025-02-01", betaEtcs[1].GetProperty("proposedCompletionDate").GetString());
    }

    [Fact]
    public void BuildPayload_WarningManager_UsesWarningBodyAndEmitsEmptyCollections()
    {
        var builder = CreateBuilder();
        var scheduledAt = DateTimeOffset.UtcNow;

        var managerData = new List<ManagerEmailData>
        {
            new()
            {
                ManagerName = "Gamma Manager",
                ManagerEmail = "gamma.manager@ey.com",
                WarningBodyHtml = "<p>Aviso</p>",
                Invoices = Array.Empty<InvoiceEmailData>(),
                Etcs = Array.Empty<EtcEmailData>()
            }
        };

        var json = builder.BuildPayload(managerData, scheduledAt, PowerAutomateJsonPayloadBuilder.DefaultTimezone, PowerAutomateJsonPayloadBuilder.DefaultLocale);
        using var document = JsonDocument.Parse(json);

        var message = document.RootElement.GetProperty("messages")[0];
        Assert.Equal("<p>Aviso</p>", message.GetProperty("bodyTemplate").GetProperty("value").GetString());
        Assert.Equal(0, message.GetProperty("invoices").GetArrayLength());
        Assert.Equal(0, message.GetProperty("etcs").GetArrayLength());
    }

    [Fact]
    public void BuildPayload_InvalidEmail_OmitsManagerFromMessages()
    {
        var builder = CreateBuilder();
        var scheduledAt = DateTimeOffset.UtcNow;

        var managerData = new List<ManagerEmailData>
        {
            new()
            {
                ManagerName = "Invalid Manager",
                ManagerEmail = "not-an-email"
            }
        };

        var json = builder.BuildPayload(managerData, scheduledAt, PowerAutomateJsonPayloadBuilder.DefaultTimezone, PowerAutomateJsonPayloadBuilder.DefaultLocale);
        using var document = JsonDocument.Parse(json);

        Assert.Equal(0, document.RootElement.GetProperty("messages").GetArrayLength());
    }
}
