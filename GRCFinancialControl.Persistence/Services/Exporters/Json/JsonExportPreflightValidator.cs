using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using GRCFinancialControl.Core.Models;
using GRCFinancialControl.Persistence.Services.Interfaces;
using GRCFinancialControl.Persistence;
using Invoices.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Logging;

namespace GRCFinancialControl.Persistence.Services.Exporters.Json;

public sealed class JsonExportPreflightValidator : IJsonExportPreflightValidator
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ILogger<JsonExportPreflightValidator> _logger;

    public JsonExportPreflightValidator(
        ApplicationDbContext dbContext,
        ILogger<JsonExportPreflightValidator> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public JsonExportPreflightReport Validate()
    {
        var entityTypes = _dbContext.Model.GetEntityTypes().ToArray();
        var modelChecks = new List<ModelCheckResult>();
        var fieldChecks = new List<FieldCheckResult>();

        modelChecks.Add(ValidateEntity<Engagement>(entityTypes, "Engagements", fieldChecks, nameof(Engagement.EngagementId), nameof(Engagement.CustomerId)));
        modelChecks.Add(ValidateEntity<Customer>(entityTypes, "Customers", fieldChecks));
        modelChecks.Add(ValidateInvoiceEntity(entityTypes, fieldChecks));
        modelChecks.Add(ValidateEntity<EngagementRankBudget>(entityTypes, "EngagementRankBudgets", fieldChecks, nameof(EngagementRankBudget.EngagementId)));
        modelChecks.Add(ValidateEntity<Employee>(entityTypes, "Employees", fieldChecks));

        ValidateField<EngagementManagerAssignment>(entityTypes, fieldChecks, "EngagementManagerAssignments", nameof(EngagementManagerAssignment.ManagerId));
        ValidateField<EngagementRankBudget>(entityTypes, fieldChecks, "EngagementRankBudgets", "RankId", alternativeFieldName: nameof(EngagementRankBudget.RankName));

        var report = new JsonExportPreflightReport(modelChecks, fieldChecks);

        if (report.HasErrors)
        {
            _logger.LogWarning("JSON export preflight detected missing dependencies. Review model checks before continuing.");
        }
        else
        {
            _logger.LogInformation("JSON export preflight validation succeeded. All required models and fields are present.");
        }

        return report;
    }

    private ModelCheckResult ValidateEntity<T>(
        IReadOnlyCollection<IEntityType> entityTypes,
        string modelName,
        ICollection<FieldCheckResult> fieldChecks,
        params string[] requiredPropertyNames)
    {
        var clrType = typeof(T);
        var entityType = entityTypes.FirstOrDefault(type => type.ClrType == clrType);
        var exists = entityType != null;

        if (!exists)
        {
            _logger.LogWarning("Required entity '{ModelName}' ({ClrType}) is not registered in the persistence model.", modelName, clrType.FullName);

            foreach (var propertyName in requiredPropertyNames)
            {
                fieldChecks.Add(new FieldCheckResult(modelName, propertyName, false));
            }

            return new ModelCheckResult(modelName, false);
        }

        foreach (var propertyName in requiredPropertyNames)
        {
            var propertyExists = clrType.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance) != null;
            fieldChecks.Add(new FieldCheckResult(modelName, propertyName, propertyExists));

            if (!propertyExists)
            {
                _logger.LogWarning("Property '{ModelName}.{PropertyName}' was not found on entity type {ClrType}.", modelName, propertyName, clrType.FullName);
            }
        }

        _logger.LogInformation("Validated presence of entity '{ModelName}' mapped to {ClrType}.", modelName, clrType.FullName);
        return new ModelCheckResult(modelName, true);
    }

    private ModelCheckResult ValidateInvoiceEntity(
        IReadOnlyCollection<IEntityType> entityTypes,
        ICollection<FieldCheckResult> fieldChecks)
    {
        static bool MatchesInvoiceName(IEntityType entityType)
        {
            var clrName = entityType.ClrType.Name;
            var tableName = entityType.GetTableName();
            return string.Equals(clrName, "EngagementInvoice", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(clrName, "EngagementInvoices", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(tableName, "EngagementInvoice", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(tableName, "EngagementInvoices", StringComparison.OrdinalIgnoreCase);
        }

        var invoiceEntity = entityTypes.FirstOrDefault(MatchesInvoiceName);
        if (invoiceEntity != null)
        {
            var propertyExists = invoiceEntity.ClrType.GetProperty("EngagementId", BindingFlags.Public | BindingFlags.Instance) != null;
            fieldChecks.Add(new FieldCheckResult("EngagementInvoices", "EngagementId", propertyExists));

            if (!propertyExists)
            {
                _logger.LogWarning("Entity '{EntityName}' is present but missing property 'EngagementId'.", invoiceEntity.ClrType.FullName);
            }
            else
            {
                _logger.LogInformation("Entity '{EntityName}' provides required property 'EngagementId'.", invoiceEntity.ClrType.FullName);
            }

            return new ModelCheckResult("EngagementInvoices", true);
        }

        var invoicePlanEntity = entityTypes.FirstOrDefault(entityType => entityType.ClrType == typeof(InvoicePlan));
        var invoiceItemEntity = entityTypes.FirstOrDefault(entityType => entityType.ClrType == typeof(InvoiceItem));
        var composedInvoiceExists = invoicePlanEntity != null && invoiceItemEntity != null;

        if (composedInvoiceExists)
        {
            _logger.LogInformation(
                "No dedicated 'EngagementInvoice' entity found. Invoice data will rely on '{PlanType}' linked to '{ItemType}'.",
                typeof(InvoicePlan).FullName,
                typeof(InvoiceItem).FullName);

            var engagementIdExists = typeof(InvoicePlan).GetProperty(nameof(InvoicePlan.EngagementId), BindingFlags.Public | BindingFlags.Instance) != null;
            fieldChecks.Add(new FieldCheckResult(nameof(InvoicePlan), nameof(InvoicePlan.EngagementId), engagementIdExists));

            if (!engagementIdExists)
            {
                _logger.LogWarning("InvoicePlan type {Type} is missing property '{Property}'.", typeof(InvoicePlan).FullName, nameof(InvoicePlan.EngagementId));
            }

            return new ModelCheckResult("EngagementInvoices", true);
        }

        if (invoicePlanEntity == null)
        {
            _logger.LogWarning("Invoice plan entity '{Type}' is missing from the persistence model.", typeof(InvoicePlan).FullName);
        }

        if (invoiceItemEntity == null)
        {
            _logger.LogWarning("Invoice item entity '{Type}' is missing from the persistence model.", typeof(InvoiceItem).FullName);
        }

        return new ModelCheckResult("EngagementInvoices", false);
    }

    private void ValidateField<T>(
        IReadOnlyCollection<IEntityType> entityTypes,
        ICollection<FieldCheckResult> fieldChecks,
        string modelName,
        string fieldName,
        string? alternativeFieldName = null)
    {
        var clrType = typeof(T);
        var entityType = entityTypes.FirstOrDefault(type => type.ClrType == clrType);
        if (entityType == null)
        {
            _logger.LogWarning(
                "Unable to validate field '{ModelName}.{FieldName}' because entity type {ClrType} is not registered.",
                modelName,
                fieldName,
                clrType.FullName);
            fieldChecks.Add(new FieldCheckResult(modelName, fieldName, false));
            return;
        }

        var propertyExists = clrType.GetProperty(fieldName, BindingFlags.Public | BindingFlags.Instance) != null;
        fieldChecks.Add(new FieldCheckResult(modelName, fieldName, propertyExists));

        if (propertyExists)
        {
            _logger.LogInformation("Confirmed field '{ModelName}.{FieldName}' on entity {ClrType}.", modelName, fieldName, clrType.FullName);
            return;
        }

        _logger.LogWarning("Field '{ModelName}.{FieldName}' is missing on entity {ClrType}.", modelName, fieldName, clrType.FullName);

        if (!string.IsNullOrWhiteSpace(alternativeFieldName))
        {
            var alternativeExists = clrType.GetProperty(alternativeFieldName, BindingFlags.Public | BindingFlags.Instance) != null;
            if (alternativeExists)
            {
                _logger.LogInformation(
                    "Found alternative field '{ModelName}.{AlternativeField}' on entity {ClrType} that may satisfy the requirement.",
                    modelName,
                    alternativeFieldName,
                    clrType.FullName);
            }
        }
    }
}
