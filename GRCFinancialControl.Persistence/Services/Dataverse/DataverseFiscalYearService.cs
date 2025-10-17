using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GRCFinancialControl.Core.Models;
using GRCFinancialControl.Persistence.Services.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace GRCFinancialControl.Persistence.Services.Dataverse;

/// <summary>
/// Dataverse-backed implementation of <see cref="IFiscalYearService"/>.
/// </summary>
public sealed class DataverseFiscalYearService : DataverseServiceBase, IFiscalYearService
{
    private readonly DataverseEntityMetadata _fiscalYearsMetadata;

    public DataverseFiscalYearService(
        IDataverseRepository repository,
        DataverseEntityMetadataRegistry metadataRegistry,
        ILogger<DataverseFiscalYearService> logger)
        : base(repository, metadataRegistry, logger)
    {
        _fiscalYearsMetadata = GetMetadata("FiscalYears");
    }

    public Task<List<FiscalYear>> GetAllAsync()
    {
        return ExecuteAsync(async client =>
        {
            var query = new QueryExpression(_fiscalYearsMetadata.LogicalName)
            {
                ColumnSet = new ColumnSet(
                    _fiscalYearsMetadata.GetAttribute("Id"),
                    _fiscalYearsMetadata.GetAttribute("Name"),
                    _fiscalYearsMetadata.GetAttribute("StartDate"),
                    _fiscalYearsMetadata.GetAttribute("EndDate"),
                    _fiscalYearsMetadata.GetAttribute("AreaSalesTarget"),
                    _fiscalYearsMetadata.GetAttribute("AreaRevenueTarget"),
                    _fiscalYearsMetadata.GetAttribute("IsLocked"),
                    _fiscalYearsMetadata.GetAttribute("LockedAt"),
                    _fiscalYearsMetadata.GetAttribute("LockedBy")),
            };

            query.AddOrder(_fiscalYearsMetadata.GetAttribute("StartDate"), OrderType.Ascending);

            var result = await client.RetrieveMultipleAsync(query).ConfigureAwait(false);
            return result.Entities.Select(Map).ToList();
        });
    }

    public Task AddAsync(FiscalYear fiscalYear)
    {
        return Task.FromException(new InvalidOperationException("Fiscal years cannot be created from the Dataverse backend."));
    }

    public Task UpdateAsync(FiscalYear fiscalYear)
    {
        return Task.FromException(new InvalidOperationException("Fiscal years cannot be edited from the Dataverse backend."));
    }

    public Task DeleteAsync(int id)
    {
        return Task.FromException(new InvalidOperationException("Fiscal years cannot be deleted from the Dataverse backend."));
    }

    public Task DeleteDataAsync(int fiscalYearId)
    {
        return Task.FromException(new InvalidOperationException("Fiscal year data cannot be deleted from the Dataverse backend."));
    }

    public Task<DateTime?> LockAsync(int fiscalYearId, string lockedBy)
    {
        return Task.FromException<DateTime?>(new InvalidOperationException("Fiscal years cannot be locked from the Dataverse backend."));
    }

    public Task<DateTime?> UnlockAsync(int fiscalYearId, string unlockedBy)
    {
        return Task.FromException<DateTime?>(new InvalidOperationException("Fiscal years cannot be unlocked from the Dataverse backend."));
    }

    public Task<FiscalYearCloseResult> CloseAsync(int fiscalYearId, string closedBy)
    {
        return Task.FromException<FiscalYearCloseResult>(new InvalidOperationException("Fiscal years cannot be closed from the Dataverse backend."));
    }

    private FiscalYear Map(Entity entity)
    {
        return new FiscalYear
        {
            Id = entity.GetInt(_fiscalYearsMetadata.GetAttribute("Id")),
            Name = entity.GetString(_fiscalYearsMetadata.GetAttribute("Name")),
            StartDate = entity.GetDateTime(_fiscalYearsMetadata.GetAttribute("StartDate"))?.Date ?? DateTime.MinValue,
            EndDate = entity.GetDateTime(_fiscalYearsMetadata.GetAttribute("EndDate"))?.Date ?? DateTime.MinValue,
            AreaSalesTarget = entity.GetDecimal(_fiscalYearsMetadata.GetAttribute("AreaSalesTarget")),
            AreaRevenueTarget = entity.GetDecimal(_fiscalYearsMetadata.GetAttribute("AreaRevenueTarget")),
            IsLocked = entity.GetInt(_fiscalYearsMetadata.GetAttribute("IsLocked")) == 1,
            LockedAt = entity.GetDateTime(_fiscalYearsMetadata.GetAttribute("LockedAt")),
            LockedBy = entity.GetOptionalString(_fiscalYearsMetadata.GetAttribute("LockedBy")),
        };
    }
}
