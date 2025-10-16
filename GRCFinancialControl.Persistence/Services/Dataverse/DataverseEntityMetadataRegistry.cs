using System;
using System.Collections.Generic;
using GRCFinancialControl.Persistence.Services.Dataverse.Schema;

namespace GRCFinancialControl.Persistence.Services.Dataverse;

/// <summary>
/// Stores the Dataverse entity metadata used by Dataverse-backed services.
/// </summary>
public sealed class DataverseEntityMetadataRegistry
{
    private readonly Dictionary<string, DataverseEntityMetadata> _entities;

    public DataverseEntityMetadataRegistry(IEnumerable<DataverseEntityMetadata> entities)
    {
        _entities = new Dictionary<string, DataverseEntityMetadata>(StringComparer.OrdinalIgnoreCase);

        foreach (var entity in entities)
        {
            _entities[entity.Key] = entity;
        }
    }

    public DataverseEntityMetadata Get(string key)
    {
        if (!_entities.TryGetValue(key, out var metadata))
        {
            throw new KeyNotFoundException($"Entity metadata '{key}' has not been registered for the Dataverse backend.");
        }

        return metadata;
    }

    public static DataverseEntityMetadataRegistry CreateDefault()
    {
        var customers = new DataverseEntityMetadata(
            key: DataverseSchemaConstants.Customers.Key,
            logicalName: DataverseSchemaConstants.Customers.LogicalName,
            primaryIdAttribute: DataverseSchemaConstants.Customers.PrimaryIdAttribute,
            attributeMap: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Id"] = DataverseSchemaConstants.Customers.Columns.SqlId,
                ["Name"] = DataverseSchemaConstants.Customers.Columns.Name,
                ["CustomerCode"] = DataverseSchemaConstants.Customers.Columns.CustomerCode,
            });

        var managers = new DataverseEntityMetadata(
            key: DataverseSchemaConstants.Managers.Key,
            logicalName: DataverseSchemaConstants.Managers.LogicalName,
            primaryIdAttribute: DataverseSchemaConstants.Managers.PrimaryIdAttribute,
            attributeMap: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Id"] = DataverseSchemaConstants.Managers.Columns.SqlId,
                ["Name"] = DataverseSchemaConstants.Managers.Columns.Name,
                ["Email"] = DataverseSchemaConstants.Managers.Columns.Email,
                ["Position"] = DataverseSchemaConstants.Managers.Columns.Position,
            });

        var papds = new DataverseEntityMetadata(
            key: DataverseSchemaConstants.Papds.Key,
            logicalName: DataverseSchemaConstants.Papds.LogicalName,
            primaryIdAttribute: DataverseSchemaConstants.Papds.PrimaryIdAttribute,
            attributeMap: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Id"] = DataverseSchemaConstants.Papds.Columns.SqlId,
                ["Name"] = DataverseSchemaConstants.Papds.Columns.Name,
                ["Email"] = DataverseSchemaConstants.Papds.Columns.Email,
                ["Level"] = DataverseSchemaConstants.Papds.Columns.Level,
            });

        var fiscalYears = new DataverseEntityMetadata(
            key: DataverseSchemaConstants.FiscalYears.Key,
            logicalName: DataverseSchemaConstants.FiscalYears.LogicalName,
            primaryIdAttribute: DataverseSchemaConstants.FiscalYears.PrimaryIdAttribute,
            attributeMap: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Id"] = DataverseSchemaConstants.FiscalYears.Columns.SqlId,
                ["Name"] = DataverseSchemaConstants.FiscalYears.Columns.Name,
                ["StartDate"] = DataverseSchemaConstants.FiscalYears.Columns.StartDate,
                ["EndDate"] = DataverseSchemaConstants.FiscalYears.Columns.EndDate,
                ["AreaSalesTarget"] = DataverseSchemaConstants.FiscalYears.Columns.AreaSalesTarget,
                ["AreaRevenueTarget"] = DataverseSchemaConstants.FiscalYears.Columns.AreaRevenueTarget,
                ["IsLocked"] = DataverseSchemaConstants.FiscalYears.Columns.IsLocked,
                ["LockedAt"] = DataverseSchemaConstants.FiscalYears.Columns.LockedAt,
                ["LockedBy"] = DataverseSchemaConstants.FiscalYears.Columns.LockedBy,
            });

        var closingPeriods = new DataverseEntityMetadata(
            key: DataverseSchemaConstants.ClosingPeriods.Key,
            logicalName: DataverseSchemaConstants.ClosingPeriods.LogicalName,
            primaryIdAttribute: DataverseSchemaConstants.ClosingPeriods.PrimaryIdAttribute,
            attributeMap: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Id"] = DataverseSchemaConstants.ClosingPeriods.Columns.SqlId,
                ["Name"] = DataverseSchemaConstants.ClosingPeriods.Columns.Name,
                ["FiscalYearId"] = DataverseSchemaConstants.ClosingPeriods.Columns.FiscalYearSqlId,
                ["FiscalYear"] = DataverseSchemaConstants.ClosingPeriods.Columns.FiscalYear,
                ["PeriodStart"] = DataverseSchemaConstants.ClosingPeriods.Columns.PeriodStart,
                ["PeriodEnd"] = DataverseSchemaConstants.ClosingPeriods.Columns.PeriodEnd,
            });

        var engagements = new DataverseEntityMetadata(
            key: DataverseSchemaConstants.Engagements.Key,
            logicalName: DataverseSchemaConstants.Engagements.LogicalName,
            primaryIdAttribute: DataverseSchemaConstants.Engagements.PrimaryIdAttribute,
            attributeMap: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Id"] = DataverseSchemaConstants.Engagements.Columns.SqlId,
                ["EngagementId"] = DataverseSchemaConstants.Engagements.Columns.EngagementNumber,
                ["Description"] = DataverseSchemaConstants.Engagements.Columns.Description,
                ["Currency"] = DataverseSchemaConstants.Engagements.Columns.Currency,
                ["MarginPctBudget"] = DataverseSchemaConstants.Engagements.Columns.MarginPctBudget,
                ["MarginPctEtcp"] = DataverseSchemaConstants.Engagements.Columns.MarginPctEtc,
                ["LastEtcDate"] = DataverseSchemaConstants.Engagements.Columns.LastEtcDate,
                ["ProposedNextEtcDate"] = DataverseSchemaConstants.Engagements.Columns.ProposedNextEtcDate,
                ["StatusText"] = DataverseSchemaConstants.Engagements.Columns.StatusText,
                ["Status"] = DataverseSchemaConstants.Engagements.Columns.Status,
                ["Source"] = DataverseSchemaConstants.Engagements.Columns.Source,
                ["CustomerId"] = DataverseSchemaConstants.Engagements.Columns.CustomerSqlId,
                ["Customer"] = DataverseSchemaConstants.Engagements.Columns.Customer,
                ["OpeningValue"] = DataverseSchemaConstants.Engagements.Columns.OpeningValue,
                ["OpeningExpenses"] = DataverseSchemaConstants.Engagements.Columns.OpeningExpenses,
                ["InitialHoursBudget"] = DataverseSchemaConstants.Engagements.Columns.InitialHoursBudget,
                ["EstimatedToCompleteHours"] = DataverseSchemaConstants.Engagements.Columns.EstimatedHours,
                ["ValueEtcp"] = DataverseSchemaConstants.Engagements.Columns.ValueEtc,
                ["ExpensesEtcp"] = DataverseSchemaConstants.Engagements.Columns.ExpensesEtc,
                ["LastClosingPeriodId"] = DataverseSchemaConstants.Engagements.Columns.LastClosingPeriodSqlId,
                ["LastClosingPeriod"] = DataverseSchemaConstants.Engagements.Columns.LastClosingPeriod,
            });

        var actualsEntries = new DataverseEntityMetadata(
            key: DataverseSchemaConstants.ActualsEntries.Key,
            logicalName: DataverseSchemaConstants.ActualsEntries.LogicalName,
            primaryIdAttribute: DataverseSchemaConstants.ActualsEntries.PrimaryIdAttribute,
            attributeMap: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Id"] = DataverseSchemaConstants.ActualsEntries.Columns.SqlId,
                ["EngagementId"] = DataverseSchemaConstants.ActualsEntries.Columns.EngagementSqlId,
                ["Engagement"] = DataverseSchemaConstants.ActualsEntries.Columns.Engagement,
                ["PapdId"] = DataverseSchemaConstants.ActualsEntries.Columns.PapdSqlId,
                ["Papd"] = DataverseSchemaConstants.ActualsEntries.Columns.Papd,
                ["ClosingPeriodId"] = DataverseSchemaConstants.ActualsEntries.Columns.ClosingPeriodSqlId,
                ["ClosingPeriod"] = DataverseSchemaConstants.ActualsEntries.Columns.ClosingPeriod,
                ["Date"] = DataverseSchemaConstants.ActualsEntries.Columns.EntryDate,
                ["Hours"] = DataverseSchemaConstants.ActualsEntries.Columns.Hours,
                ["ImportBatchId"] = DataverseSchemaConstants.ActualsEntries.Columns.ImportBatchId,
            });

        var plannedAllocations = new DataverseEntityMetadata(
            key: DataverseSchemaConstants.PlannedAllocations.Key,
            logicalName: DataverseSchemaConstants.PlannedAllocations.LogicalName,
            primaryIdAttribute: DataverseSchemaConstants.PlannedAllocations.PrimaryIdAttribute,
            attributeMap: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Id"] = DataverseSchemaConstants.PlannedAllocations.Columns.SqlId,
                ["EngagementId"] = DataverseSchemaConstants.PlannedAllocations.Columns.EngagementSqlId,
                ["Engagement"] = DataverseSchemaConstants.PlannedAllocations.Columns.Engagement,
                ["ClosingPeriodId"] = DataverseSchemaConstants.PlannedAllocations.Columns.ClosingPeriodSqlId,
                ["ClosingPeriod"] = DataverseSchemaConstants.PlannedAllocations.Columns.ClosingPeriod,
                ["AllocatedHours"] = DataverseSchemaConstants.PlannedAllocations.Columns.AllocatedHours,
            });

        var engagementPapds = new DataverseEntityMetadata(
            key: DataverseSchemaConstants.EngagementPapds.Key,
            logicalName: DataverseSchemaConstants.EngagementPapds.LogicalName,
            primaryIdAttribute: DataverseSchemaConstants.EngagementPapds.PrimaryIdAttribute,
            attributeMap: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Id"] = DataverseSchemaConstants.EngagementPapds.Columns.SqlId,
                ["EngagementId"] = DataverseSchemaConstants.EngagementPapds.Columns.EngagementSqlId,
                ["Engagement"] = DataverseSchemaConstants.EngagementPapds.Columns.Engagement,
                ["PapdId"] = DataverseSchemaConstants.EngagementPapds.Columns.PapdSqlId,
                ["Papd"] = DataverseSchemaConstants.EngagementPapds.Columns.Papd,
                ["EffectiveDate"] = DataverseSchemaConstants.EngagementPapds.Columns.EffectiveDate,
            });

        var engagementRankBudgets = new DataverseEntityMetadata(
            key: DataverseSchemaConstants.EngagementRankBudgets.Key,
            logicalName: DataverseSchemaConstants.EngagementRankBudgets.LogicalName,
            primaryIdAttribute: DataverseSchemaConstants.EngagementRankBudgets.PrimaryIdAttribute,
            attributeMap: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Id"] = DataverseSchemaConstants.EngagementRankBudgets.Columns.SqlId,
                ["EngagementId"] = DataverseSchemaConstants.EngagementRankBudgets.Columns.EngagementSqlId,
                ["Engagement"] = DataverseSchemaConstants.EngagementRankBudgets.Columns.Engagement,
                ["RankName"] = DataverseSchemaConstants.EngagementRankBudgets.Columns.RankName,
                ["PlannedHours"] = DataverseSchemaConstants.EngagementRankBudgets.Columns.PlannedHours,
            });

        var financialEvolutions = new DataverseEntityMetadata(
            key: DataverseSchemaConstants.FinancialEvolutions.Key,
            logicalName: DataverseSchemaConstants.FinancialEvolutions.LogicalName,
            primaryIdAttribute: DataverseSchemaConstants.FinancialEvolutions.PrimaryIdAttribute,
            attributeMap: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Id"] = DataverseSchemaConstants.FinancialEvolutions.Columns.SqlId,
                ["EngagementId"] = DataverseSchemaConstants.FinancialEvolutions.Columns.EngagementSqlId,
                ["Engagement"] = DataverseSchemaConstants.FinancialEvolutions.Columns.Engagement,
                ["PeriodName"] = DataverseSchemaConstants.FinancialEvolutions.Columns.PeriodName,
                ["HoursData"] = DataverseSchemaConstants.FinancialEvolutions.Columns.HoursData,
                ["ValueData"] = DataverseSchemaConstants.FinancialEvolutions.Columns.ValueData,
                ["MarginData"] = DataverseSchemaConstants.FinancialEvolutions.Columns.MarginData,
                ["ExpenseData"] = DataverseSchemaConstants.FinancialEvolutions.Columns.ExpenseData,
            });

        var engagementFiscalYearAllocations = new DataverseEntityMetadata(
            key: DataverseSchemaConstants.EngagementFiscalYearAllocations.Key,
            logicalName: DataverseSchemaConstants.EngagementFiscalYearAllocations.LogicalName,
            primaryIdAttribute: DataverseSchemaConstants.EngagementFiscalYearAllocations.PrimaryIdAttribute,
            attributeMap: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Id"] = DataverseSchemaConstants.EngagementFiscalYearAllocations.Columns.SqlId,
                ["EngagementId"] = DataverseSchemaConstants.EngagementFiscalYearAllocations.Columns.EngagementSqlId,
                ["Engagement"] = DataverseSchemaConstants.EngagementFiscalYearAllocations.Columns.Engagement,
                ["FiscalYearId"] = DataverseSchemaConstants.EngagementFiscalYearAllocations.Columns.FiscalYearSqlId,
                ["FiscalYear"] = DataverseSchemaConstants.EngagementFiscalYearAllocations.Columns.FiscalYear,
                ["PlannedHours"] = DataverseSchemaConstants.EngagementFiscalYearAllocations.Columns.PlannedHours,
                ["PlannedValue"] = DataverseSchemaConstants.EngagementFiscalYearAllocations.Columns.PlannedValue,
            });

        var engagementFiscalYearRevenueAllocations = new DataverseEntityMetadata(
            key: DataverseSchemaConstants.EngagementFiscalYearRevenueAllocations.Key,
            logicalName: DataverseSchemaConstants.EngagementFiscalYearRevenueAllocations.LogicalName,
            primaryIdAttribute: DataverseSchemaConstants.EngagementFiscalYearRevenueAllocations.PrimaryIdAttribute,
            attributeMap: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Id"] = DataverseSchemaConstants.EngagementFiscalYearRevenueAllocations.Columns.SqlId,
                ["EngagementId"] = DataverseSchemaConstants.EngagementFiscalYearRevenueAllocations.Columns.EngagementSqlId,
                ["Engagement"] = DataverseSchemaConstants.EngagementFiscalYearRevenueAllocations.Columns.Engagement,
                ["FiscalYearId"] = DataverseSchemaConstants.EngagementFiscalYearRevenueAllocations.Columns.FiscalYearSqlId,
                ["FiscalYear"] = DataverseSchemaConstants.EngagementFiscalYearRevenueAllocations.Columns.FiscalYear,
                ["ToDateValue"] = DataverseSchemaConstants.EngagementFiscalYearRevenueAllocations.Columns.ToDateValue,
                ["ToGoValue"] = DataverseSchemaConstants.EngagementFiscalYearRevenueAllocations.Columns.ToGoValue,
            });

        var invoicePlans = new DataverseEntityMetadata(
            key: DataverseSchemaConstants.InvoicePlans.Key,
            logicalName: DataverseSchemaConstants.InvoicePlans.LogicalName,
            primaryIdAttribute: DataverseSchemaConstants.InvoicePlans.PrimaryIdAttribute,
            attributeMap: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Id"] = DataverseSchemaConstants.InvoicePlans.Columns.SqlId,
                ["EngagementId"] = DataverseSchemaConstants.InvoicePlans.Columns.EngagementId,
                ["Engagement"] = DataverseSchemaConstants.InvoicePlans.Columns.Engagement,
                ["Type"] = DataverseSchemaConstants.InvoicePlans.Columns.Type,
                ["NumInvoices"] = DataverseSchemaConstants.InvoicePlans.Columns.NumInvoices,
                ["PaymentTermDays"] = DataverseSchemaConstants.InvoicePlans.Columns.PaymentTermDays,
                ["CustomerFocalPointName"] = DataverseSchemaConstants.InvoicePlans.Columns.CustomerFocalPointName,
                ["CustomerFocalPointEmail"] = DataverseSchemaConstants.InvoicePlans.Columns.CustomerFocalPointEmail,
                ["CustomInstructions"] = DataverseSchemaConstants.InvoicePlans.Columns.CustomInstructions,
                ["FirstEmissionDate"] = DataverseSchemaConstants.InvoicePlans.Columns.FirstEmissionDate,
                ["CreatedAt"] = DataverseSchemaConstants.InvoicePlans.Columns.CreatedAt,
                ["UpdatedAt"] = DataverseSchemaConstants.InvoicePlans.Columns.UpdatedAt,
            });

        var invoiceItems = new DataverseEntityMetadata(
            key: DataverseSchemaConstants.InvoiceItems.Key,
            logicalName: DataverseSchemaConstants.InvoiceItems.LogicalName,
            primaryIdAttribute: DataverseSchemaConstants.InvoiceItems.PrimaryIdAttribute,
            attributeMap: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Id"] = DataverseSchemaConstants.InvoiceItems.Columns.SqlId,
                ["InvoicePlanId"] = DataverseSchemaConstants.InvoiceItems.Columns.InvoicePlanSqlId,
                ["InvoicePlanSqlId"] = DataverseSchemaConstants.InvoiceItems.Columns.InvoicePlanSqlId,
                ["InvoicePlan"] = DataverseSchemaConstants.InvoiceItems.Columns.InvoicePlan,
                ["Sequence"] = DataverseSchemaConstants.InvoiceItems.Columns.Sequence,
                ["Description"] = DataverseSchemaConstants.InvoiceItems.Columns.Description,
                ["Amount"] = DataverseSchemaConstants.InvoiceItems.Columns.Amount,
                ["ScheduledDate"] = DataverseSchemaConstants.InvoiceItems.Columns.ScheduledDate,
                ["DueDate"] = DataverseSchemaConstants.InvoiceItems.Columns.DueDate,
                ["Percentage"] = DataverseSchemaConstants.InvoiceItems.Columns.Percentage,
                ["PayerCnpj"] = DataverseSchemaConstants.InvoiceItems.Columns.PayerCnpj,
                ["PoNumber"] = DataverseSchemaConstants.InvoiceItems.Columns.PoNumber,
                ["FrsNumber"] = DataverseSchemaConstants.InvoiceItems.Columns.FrsNumber,
                ["CustomerTicket"] = DataverseSchemaConstants.InvoiceItems.Columns.CustomerTicket,
                ["AdditionalInfo"] = DataverseSchemaConstants.InvoiceItems.Columns.AdditionalInfo,
                ["Status"] = DataverseSchemaConstants.InvoiceItems.Columns.Status,
                ["RitmNumber"] = DataverseSchemaConstants.InvoiceItems.Columns.RitmNumber,
                ["CoeResponsible"] = DataverseSchemaConstants.InvoiceItems.Columns.CoeResponsible,
                ["RequestDate"] = DataverseSchemaConstants.InvoiceItems.Columns.RequestDate,
                ["BzCode"] = DataverseSchemaConstants.InvoiceItems.Columns.BzCode,
                ["EmittedAt"] = DataverseSchemaConstants.InvoiceItems.Columns.EmittedAt,
                ["CanceledAt"] = DataverseSchemaConstants.InvoiceItems.Columns.CanceledAt,
                ["CancelReason"] = DataverseSchemaConstants.InvoiceItems.Columns.CancelReason,
                ["ReplacementItem"] = DataverseSchemaConstants.InvoiceItems.Columns.ReplacementItem,
                ["ReplacementItemId"] = DataverseSchemaConstants.InvoiceItems.Columns.ReplacementItemSqlId,
                ["ReplacementItemSqlId"] = DataverseSchemaConstants.InvoiceItems.Columns.ReplacementItemSqlId,
            });

        var invoicePlanEmails = new DataverseEntityMetadata(
            key: DataverseSchemaConstants.InvoicePlanEmails.Key,
            logicalName: DataverseSchemaConstants.InvoicePlanEmails.LogicalName,
            primaryIdAttribute: DataverseSchemaConstants.InvoicePlanEmails.PrimaryIdAttribute,
            attributeMap: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Id"] = DataverseSchemaConstants.InvoicePlanEmails.Columns.SqlId,
                ["InvoicePlan"] = DataverseSchemaConstants.InvoicePlanEmails.Columns.InvoicePlan,
                ["InvoicePlanId"] = DataverseSchemaConstants.InvoicePlanEmails.Columns.InvoicePlanSqlId,
                ["InvoicePlanSqlId"] = DataverseSchemaConstants.InvoicePlanEmails.Columns.InvoicePlanSqlId,
                ["Email"] = DataverseSchemaConstants.InvoicePlanEmails.Columns.Email,
                ["CreatedAt"] = DataverseSchemaConstants.InvoicePlanEmails.Columns.CreatedAt,
                ["UpdatedAt"] = DataverseSchemaConstants.InvoicePlanEmails.Columns.UpdatedAt,
            });

        var systemUsers = new DataverseEntityMetadata(
            key: "SystemUsers",
            logicalName: "systemuser",
            primaryIdAttribute: "systemuserid",
            attributeMap: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["FullName"] = "fullname",
                ["InternalEmailAddress"] = "internalemailaddress",
                ["DomainName"] = "domainname",
            });

        return new DataverseEntityMetadataRegistry(
            new[]
            {
                customers,
                managers,
                papds,
                fiscalYears,
                closingPeriods,
                engagements,
                actualsEntries,
                plannedAllocations,
                engagementPapds,
                engagementRankBudgets,
                financialEvolutions,
                engagementFiscalYearAllocations,
                engagementFiscalYearRevenueAllocations,
                invoicePlans,
                invoiceItems,
                invoicePlanEmails,
                systemUsers,
            });
    }
}
