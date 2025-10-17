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
/// Dataverse-backed implementation of <see cref="IClosingPeriodService"/>.
/// </summary>
public sealed class DataverseClosingPeriodService : DataverseServiceBase, IClosingPeriodService
{
    private readonly DataverseEntityMetadata _closingPeriodsMetadata;
    private readonly DataverseEntityMetadata _fiscalYearsMetadata;
    private const string FiscalYearAlias = "fiscalyear";

    public DataverseClosingPeriodService(
        IDataverseRepository repository,
        DataverseEntityMetadataRegistry metadataRegistry,
        ILogger<DataverseClosingPeriodService> logger)
        : base(repository, metadataRegistry, logger)
    {
        _closingPeriodsMetadata = GetMetadata("ClosingPeriods");
        _fiscalYearsMetadata = GetMetadata("FiscalYears");
    }

    public Task<List<ClosingPeriod>> GetAllAsync()
    {
        return ExecuteAsync(async client =>
        {
            var query = new QueryExpression(_closingPeriodsMetadata.LogicalName)
            {
                ColumnSet = new ColumnSet(
                    _closingPeriodsMetadata.GetAttribute("Id"),
                    _closingPeriodsMetadata.GetAttribute("Name"),
                    _closingPeriodsMetadata.GetAttribute("FiscalYearId"),
                    _closingPeriodsMetadata.GetAttribute("PeriodStart"),
                    _closingPeriodsMetadata.GetAttribute("PeriodEnd")),
            };

            query.AddOrder(_closingPeriodsMetadata.GetAttribute("PeriodStart"), OrderType.Ascending);

            var fiscalYearLink = query.AddLink(
                _fiscalYearsMetadata.LogicalName,
                _closingPeriodsMetadata.GetAttribute("FiscalYear"),
                _fiscalYearsMetadata.PrimaryIdAttribute,
                JoinOperator.LeftOuter);

            fiscalYearLink.EntityAlias = FiscalYearAlias;
            fiscalYearLink.Columns = new ColumnSet(
                _fiscalYearsMetadata.GetAttribute("Id"),
                _fiscalYearsMetadata.GetAttribute("Name"),
                _fiscalYearsMetadata.GetAttribute("StartDate"),
                _fiscalYearsMetadata.GetAttribute("EndDate"),
                _fiscalYearsMetadata.GetAttribute("IsLocked"),
                _fiscalYearsMetadata.GetAttribute("LockedAt"),
                _fiscalYearsMetadata.GetAttribute("LockedBy"));

            var result = await client.RetrieveMultipleAsync(query).ConfigureAwait(false);
            return result.Entities.Select(Map).ToList();
        });
    }

    public Task AddAsync(ClosingPeriod period)
    {
        return Task.FromException(new InvalidOperationException("Closing periods cannot be created from the Dataverse backend."));
    }

    public Task UpdateAsync(ClosingPeriod period)
    {
        return Task.FromException(new InvalidOperationException("Closing periods cannot be edited from the Dataverse backend."));
    }

    public Task DeleteAsync(int id)
    {
        return Task.FromException(new InvalidOperationException("Closing periods cannot be deleted from the Dataverse backend."));
    }

    public Task DeleteDataAsync(int closingPeriodId)
    {
        return Task.FromException(new InvalidOperationException("Closing period data cannot be deleted from the Dataverse backend."));
    }

    private ClosingPeriod Map(Entity entity)
    {
        var fiscalYearId = entity.GetAliasedInt(FiscalYearAlias, _fiscalYearsMetadata.GetAttribute("Id"));

        FiscalYear? fiscalYear = null;
        if (fiscalYearId.HasValue)
        {
            fiscalYear = new FiscalYear
            {
                Id = fiscalYearId.Value,
                Name = entity.GetAliasedString(FiscalYearAlias, _fiscalYearsMetadata.GetAttribute("Name")) ?? string.Empty,
                StartDate = entity.GetAliasedDateTime(FiscalYearAlias, _fiscalYearsMetadata.GetAttribute("StartDate"))?.Date ?? DateTime.MinValue,
                EndDate = entity.GetAliasedDateTime(FiscalYearAlias, _fiscalYearsMetadata.GetAttribute("EndDate"))?.Date ?? DateTime.MinValue,
                IsLocked = entity.GetAliasedInt(FiscalYearAlias, _fiscalYearsMetadata.GetAttribute("IsLocked")) == 1,
                LockedAt = entity.GetAliasedDateTime(FiscalYearAlias, _fiscalYearsMetadata.GetAttribute("LockedAt")),
                LockedBy = entity.GetAliasedString(FiscalYearAlias, _fiscalYearsMetadata.GetAttribute("LockedBy")),
            };
        }

        return new ClosingPeriod
        {
            Id = entity.GetInt(_closingPeriodsMetadata.GetAttribute("Id")),
            Name = entity.GetString(_closingPeriodsMetadata.GetAttribute("Name")),
            FiscalYearId = entity.GetInt(_closingPeriodsMetadata.GetAttribute("FiscalYearId")),
            FiscalYear = fiscalYear ?? new FiscalYear { Id = entity.GetInt(_closingPeriodsMetadata.GetAttribute("FiscalYearId")) },
            PeriodStart = entity.GetDateTime(_closingPeriodsMetadata.GetAttribute("PeriodStart"))?.Date ?? DateTime.MinValue,
            PeriodEnd = entity.GetDateTime(_closingPeriodsMetadata.GetAttribute("PeriodEnd"))?.Date ?? DateTime.MinValue,
        };
    }
}
