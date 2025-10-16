using System.Diagnostics.CodeAnalysis;

namespace GRCFinancialControl.Persistence.Services.Dataverse.Schema;

/// <summary>
/// Provides canonical Dataverse schema names shared by the Financial Control and Invoice Planner applications.
/// </summary>
[SuppressMessage("Design", "CA1034:Nested types should not be visible", Justification = "Schema constants are grouped by entity for clarity.")]
public static class DataverseSchemaConstants
{
    public const string PublisherPrefix = "ey";
    public const string PublisherUniqueName = "ey";
    public const string PublisherDisplayName = "EY Engineering Automation";
    public const string SolutionUniqueName = "eyFinancialControl";
    public const string SolutionDisplayName = "EY Financial Control";
    public const string SolutionDescription = "Financial Control and Invoice Planner unified Dataverse schema aligned with the MySQL reference database.";

    public static class Customers
    {
        public const string Key = "Customers";
        public const string LogicalName = "ey_customer";
        public const string PrimaryIdAttribute = "ey_customerid";
        public const string PrimaryNameAttribute = Columns.Name;

        public static class Columns
        {
            public const string SqlId = "ey_customer_sqlid";
            public const string Name = "ey_customer_name";
            public const string CustomerCode = "ey_customer_code";
        }

        public static class AlternateKeys
        {
            public const string SqlId = "ey_customer_sqlid_key";
            public const string CustomerCode = "ey_customer_code_key";
        }
    }

    public static class Managers
    {
        public const string Key = "Managers";
        public const string LogicalName = "ey_manager";
        public const string PrimaryIdAttribute = "ey_managerid";
        public const string PrimaryNameAttribute = Columns.Name;

        public static class Columns
        {
            public const string SqlId = "ey_manager_sqlid";
            public const string Name = "ey_manager_name";
            public const string Email = "ey_manager_email";
            public const string Position = "ey_manager_position";
        }

        public static class AlternateKeys
        {
            public const string SqlId = "ey_manager_sqlid_key";
            public const string Email = "ey_manager_email_key";
        }
    }

    public static class Papds
    {
        public const string Key = "Papds";
        public const string LogicalName = "ey_papd";
        public const string PrimaryIdAttribute = "ey_papdid";
        public const string PrimaryNameAttribute = Columns.Name;

        public static class Columns
        {
            public const string SqlId = "ey_papd_sqlid";
            public const string Name = "ey_papd_name";
            public const string Email = "ey_papd_email";
            public const string Level = "ey_papd_level";
        }

        public static class AlternateKeys
        {
            public const string SqlId = "ey_papd_sqlid_key";
            public const string Email = "ey_papd_email_key";
        }
    }

    public static class FiscalYears
    {
        public const string Key = "FiscalYears";
        public const string LogicalName = "ey_fiscalyear";
        public const string PrimaryIdAttribute = "ey_fiscalyearid";
        public const string PrimaryNameAttribute = Columns.Name;

        public static class Columns
        {
            public const string SqlId = "ey_fiscalyear_sqlid";
            public const string Name = "ey_fiscalyear_name";
            public const string StartDate = "ey_fiscalyear_startdate";
            public const string EndDate = "ey_fiscalyear_enddate";
            public const string AreaSalesTarget = "ey_fiscalyear_sales_target";
            public const string AreaRevenueTarget = "ey_fiscalyear_revenue_target";
            public const string IsLocked = "ey_fiscalyear_islocked";
            public const string LockedAt = "ey_fiscalyear_lockedat";
            public const string LockedBy = "ey_fiscalyear_lockedby";
        }

        public static class AlternateKeys
        {
            public const string SqlId = "ey_fiscalyear_sqlid_key";
            public const string Name = "ey_fiscalyear_name_key";
        }
    }

    public static class ClosingPeriods
    {
        public const string Key = "ClosingPeriods";
        public const string LogicalName = "ey_closingperiod";
        public const string PrimaryIdAttribute = "ey_closingperiodid";
        public const string PrimaryNameAttribute = Columns.Name;

        public static class Columns
        {
            public const string SqlId = "ey_closingperiod_sqlid";
            public const string Name = "ey_closingperiod_name";
            public const string FiscalYear = "ey_closingperiod_fiscalyear";
            public const string FiscalYearSqlId = "ey_closingperiod_fiscalyear_sqlid";
            public const string PeriodStart = "ey_closingperiod_start";
            public const string PeriodEnd = "ey_closingperiod_end";
        }

        public static class AlternateKeys
        {
            public const string SqlId = "ey_closingperiod_sqlid_key";
            public const string Name = "ey_closingperiod_name_key";
        }
    }

    public static class Engagements
    {
        public const string Key = "Engagements";
        public const string LogicalName = "ey_engagement";
        public const string PrimaryIdAttribute = "ey_engagementid";
        public const string PrimaryNameAttribute = Columns.Description;

        public static class Columns
        {
            public const string SqlId = "ey_engagement_sqlid";
            public const string EngagementNumber = "ey_engagement_number";
            public const string Description = "ey_engagement_description";
            public const string Currency = "ey_engagement_currency";
            public const string MarginPctBudget = "ey_engagement_margin_budget";
            public const string MarginPctEtc = "ey_engagement_margin_etcp";
            public const string LastEtcDate = "ey_engagement_lastetcdate";
            public const string ProposedNextEtcDate = "ey_engagement_nextetcdate";
            public const string StatusText = "ey_engagement_statustext";
            public const string Status = "ey_engagement_status";
            public const string Source = "ey_engagement_source";
            public const string Customer = "ey_engagement_customer";
            public const string CustomerSqlId = "ey_engagement_customer_sqlid";
            public const string OpeningValue = "ey_engagement_openingvalue";
            public const string OpeningExpenses = "ey_engagement_openingexpenses";
            public const string InitialHoursBudget = "ey_engagement_initialhoursbudget";
            public const string EstimatedHours = "ey_engagement_estimatedhours";
            public const string ValueEtc = "ey_engagement_valueetcp";
            public const string ExpensesEtc = "ey_engagement_expensesetcp";
            public const string LastClosingPeriod = "ey_engagement_lastclosingperiod";
            public const string LastClosingPeriodSqlId = "ey_engagement_lastclosingperiod_sqlid";
        }

        public static class AlternateKeys
        {
            public const string SqlId = "ey_engagement_sqlid_key";
            public const string EngagementNumber = "ey_engagement_number_key";
        }
    }

    public static class ActualsEntries
    {
        public const string Key = "ActualsEntries";
        public const string LogicalName = "ey_actualsentry";
        public const string PrimaryIdAttribute = "ey_actualsentryid";
        public const string PrimaryNameAttribute = Columns.ImportBatchId;

        public static class Columns
        {
            public const string SqlId = "ey_actualsentry_sqlid";
            public const string Engagement = "ey_actualsentry_engagement";
            public const string EngagementSqlId = "ey_actualsentry_engagement_sqlid";
            public const string Papd = "ey_actualsentry_papd";
            public const string PapdSqlId = "ey_actualsentry_papd_sqlid";
            public const string ClosingPeriod = "ey_actualsentry_closingperiod";
            public const string ClosingPeriodSqlId = "ey_actualsentry_closingperiod_sqlid";
            public const string EntryDate = "ey_actualsentry_date";
            public const string Hours = "ey_actualsentry_hours";
            public const string ImportBatchId = "ey_actualsentry_importbatch";
        }

        public static class AlternateKeys
        {
            public const string SqlId = "ey_actualsentry_sqlid_key";
        }
    }

    public static class PlannedAllocations
    {
        public const string Key = "PlannedAllocations";
        public const string LogicalName = "ey_plannedallocation";
        public const string PrimaryIdAttribute = "ey_plannedallocationid";
        public const string PrimaryNameAttribute = Columns.ClosingPeriod;

        public static class Columns
        {
            public const string SqlId = "ey_plannedallocation_sqlid";
            public const string Engagement = "ey_plannedallocation_engagement";
            public const string EngagementSqlId = "ey_plannedallocation_engagement_sqlid";
            public const string ClosingPeriod = "ey_plannedallocation_closingperiod";
            public const string ClosingPeriodSqlId = "ey_plannedallocation_closingperiod_sqlid";
            public const string AllocatedHours = "ey_plannedallocation_hours";
        }

        public static class AlternateKeys
        {
            public const string SqlId = "ey_plannedallocation_sqlid_key";
            public const string EngagementPeriod = "ey_plannedallocation_engagement_period_key";
        }
    }

    public static class EngagementPapds
    {
        public const string Key = "EngagementPapds";
        public const string LogicalName = "ey_engagementpapd";
        public const string PrimaryIdAttribute = "ey_engagementpapdid";
        public const string PrimaryNameAttribute = Columns.EffectiveDate;

        public static class Columns
        {
            public const string SqlId = "ey_engagementpapd_sqlid";
            public const string Engagement = "ey_engagementpapd_engagement";
            public const string EngagementSqlId = "ey_engagementpapd_engagement_sqlid";
            public const string Papd = "ey_engagementpapd_papd";
            public const string PapdSqlId = "ey_engagementpapd_papd_sqlid";
            public const string EffectiveDate = "ey_engagementpapd_effectivedate";
        }

        public static class AlternateKeys
        {
            public const string SqlId = "ey_engagementpapd_sqlid_key";
        }
    }

    public static class EngagementRankBudgets
    {
        public const string Key = "EngagementRankBudgets";
        public const string LogicalName = "ey_rankbudget";
        public const string PrimaryIdAttribute = "ey_rankbudgetid";
        public const string PrimaryNameAttribute = Columns.RankName;

        public static class Columns
        {
            public const string SqlId = "ey_rankbudget_sqlid";
            public const string Engagement = "ey_rankbudget_engagement";
            public const string EngagementSqlId = "ey_rankbudget_engagement_sqlid";
            public const string RankName = "ey_rankbudget_rankname";
            public const string PlannedHours = "ey_rankbudget_hours";
        }

        public static class AlternateKeys
        {
            public const string SqlId = "ey_rankbudget_sqlid_key";
        }
    }

    public static class FinancialEvolutions
    {
        public const string Key = "FinancialEvolutions";
        public const string LogicalName = "ey_financialevolution";
        public const string PrimaryIdAttribute = "ey_financialevolutionid";
        public const string PrimaryNameAttribute = Columns.PeriodName;

        public static class Columns
        {
            public const string SqlId = "ey_financialevolution_sqlid";
            public const string Engagement = "ey_financialevolution_engagement";
            public const string EngagementSqlId = "ey_financialevolution_engagement_sqlid";
            public const string PeriodName = "ey_financialevolution_period";
            public const string HoursData = "ey_financialevolution_hours";
            public const string ValueData = "ey_financialevolution_value";
            public const string MarginData = "ey_financialevolution_margin";
            public const string ExpenseData = "ey_financialevolution_expense";
        }

        public static class AlternateKeys
        {
            public const string SqlId = "ey_financialevolution_sqlid_key";
            public const string EngagementPeriod = "ey_financialevolution_engagement_period_key";
        }
    }

    public static class EngagementFiscalYearAllocations
    {
        public const string Key = "EngagementFiscalYearAllocations";
        public const string LogicalName = "ey_fiscalyearallocation";
        public const string PrimaryIdAttribute = "ey_fiscalyearallocationid";
        public const string PrimaryNameAttribute = Columns.FiscalYear;

        public static class Columns
        {
            public const string SqlId = "ey_fiscalyearallocation_sqlid";
            public const string Engagement = "ey_fiscalyearallocation_engagement";
            public const string EngagementSqlId = "ey_fiscalyearallocation_engagement_sqlid";
            public const string FiscalYear = "ey_fiscalyearallocation_fiscalyear";
            public const string FiscalYearSqlId = "ey_fiscalyearallocation_fiscalyear_sqlid";
            public const string PlannedHours = "ey_fiscalyearallocation_hours";
            public const string PlannedValue = "ey_fiscalyearallocation_value";
        }

        public static class AlternateKeys
        {
            public const string SqlId = "ey_fiscalyearallocation_sqlid_key";
            public const string EngagementFiscalYear = "ey_fiscalyearallocation_engagement_year_key";
        }
    }

    public static class EngagementFiscalYearRevenueAllocations
    {
        public const string Key = "EngagementFiscalYearRevenueAllocations";
        public const string LogicalName = "ey_fiscalyearrevenue";
        public const string PrimaryIdAttribute = "ey_fiscalyearrevenueid";
        public const string PrimaryNameAttribute = Columns.FiscalYear;

        public static class Columns
        {
            public const string SqlId = "ey_fiscalyearrevenue_sqlid";
            public const string Engagement = "ey_fiscalyearrevenue_engagement";
            public const string EngagementSqlId = "ey_fiscalyearrevenue_engagement_sqlid";
            public const string FiscalYear = "ey_fiscalyearrevenue_fiscalyear";
            public const string FiscalYearSqlId = "ey_fiscalyearrevenue_fiscalyear_sqlid";
            public const string ToDateValue = "ey_fiscalyearrevenue_todate";
            public const string ToGoValue = "ey_fiscalyearrevenue_togo";
        }

        public static class AlternateKeys
        {
            public const string SqlId = "ey_fiscalyearrevenue_sqlid_key";
            public const string EngagementFiscalYear = "ey_fiscalyearrevenue_engagement_year_key";
        }
    }

    public static class InvoicePlans
    {
        public const string Key = "InvoicePlans";
        public const string LogicalName = "ey_invoiceplan";
        public const string PrimaryIdAttribute = "ey_invoiceplanid";
        public const string PrimaryNameAttribute = Columns.EngagementId;

        public static class Columns
        {
            public const string SqlId = "ey_invoiceplan_sqlid";
            public const string EngagementId = "ey_invoiceplan_engagementid";
            public const string Engagement = "ey_invoiceplan_engagement";
            public const string Type = "ey_invoiceplan_type";
            public const string NumInvoices = "ey_invoiceplan_numinvoices";
            public const string PaymentTermDays = "ey_invoiceplan_paymentterm";
            public const string CustomerFocalPointName = "ey_invoiceplan_focalpoint_name";
            public const string CustomerFocalPointEmail = "ey_invoiceplan_focalpoint_email";
            public const string CustomInstructions = "ey_invoiceplan_instructions";
            public const string FirstEmissionDate = "ey_invoiceplan_firstemission";
            public const string CreatedAt = "ey_invoiceplan_createdon";
            public const string UpdatedAt = "ey_invoiceplan_updatedon";
        }

        public static class AlternateKeys
        {
            public const string SqlId = "ey_invoiceplan_sqlid_key";
            public const string EngagementType = "ey_invoiceplan_engagement_type_key";
        }
    }

    public static class InvoiceItems
    {
        public const string Key = "InvoiceItems";
        public const string LogicalName = "ey_invoiceitem";
        public const string PrimaryIdAttribute = "ey_invoiceitemid";
        public const string PrimaryNameAttribute = Columns.Description;

        public static class Columns
        {
            public const string SqlId = "ey_invoiceitem_sqlid";
            public const string InvoicePlan = "ey_invoiceitem_invoiceplan";
            public const string InvoicePlanSqlId = "ey_invoiceitem_invoiceplan_sqlid";
            public const string Sequence = "ey_invoiceitem_sequence";
            public const string Description = "ey_invoiceitem_description";
            public const string Amount = "ey_invoiceitem_amount";
            public const string ScheduledDate = "ey_invoiceitem_scheduleddate";
            public const string DueDate = "ey_invoiceitem_duedate";
            public const string Percentage = "ey_invoiceitem_percentage";
            public const string PayerCnpj = "ey_invoiceitem_payercnpj";
            public const string PoNumber = "ey_invoiceitem_ponumber";
            public const string FrsNumber = "ey_invoiceitem_frsnumber";
            public const string CustomerTicket = "ey_invoiceitem_customerticket";
            public const string AdditionalInfo = "ey_invoiceitem_additionalinfo";
            public const string Status = "ey_invoiceitem_status";
            public const string RitmNumber = "ey_invoiceitem_ritm";
            public const string CoeResponsible = "ey_invoiceitem_coe";
            public const string RequestDate = "ey_invoiceitem_requestdate";
            public const string BzCode = "ey_invoiceitem_bzcode";
            public const string EmittedAt = "ey_invoiceitem_emittedat";
            public const string CanceledAt = "ey_invoiceitem_canceledat";
            public const string CancelReason = "ey_invoiceitem_cancelreason";
            public const string ReplacementItem = "ey_invoiceitem_replacement";
            public const string ReplacementItemSqlId = "ey_invoiceitem_replacement_sqlid";
        }

        public static class AlternateKeys
        {
            public const string SqlId = "ey_invoiceitem_sqlid_key";
            public const string PlanSequence = "ey_invoiceitem_plan_sequence_key";
        }
    }

    public static class InvoicePlanEmails
    {
        public const string Key = "InvoicePlanEmails";
        public const string LogicalName = "ey_invoiceplanemail";
        public const string PrimaryIdAttribute = "ey_invoiceplanemailid";
        public const string PrimaryNameAttribute = Columns.Email;

        public static class Columns
        {
            public const string SqlId = "ey_invoiceplanemail_sqlid";
            public const string InvoicePlan = "ey_invoiceplanemail_invoiceplan";
            public const string InvoicePlanSqlId = "ey_invoiceplanemail_invoiceplan_sqlid";
            public const string Email = "ey_invoiceplanemail_email";
            public const string CreatedAt = "ey_invoiceplanemail_createdon";
            public const string UpdatedAt = "ey_invoiceplanemail_updatedon";
        }

        public static class AlternateKeys
        {
            public const string SqlId = "ey_invoiceplanemail_sqlid_key";
            public const string PlanEmail = "ey_invoiceplanemail_plan_email_key";
        }
    }
}
