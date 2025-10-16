using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GRCFinancialControl.Core.Enums;
using GRCFinancialControl.Core.Models;
using GRCFinancialControl.Persistence.Services.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace GRCFinancialControl.Persistence.Services.Dataverse;

/// <summary>
/// Dataverse-backed implementation of <see cref="IManagerService"/>.
/// </summary>
public sealed class DataverseManagerService : DataverseServiceBase, IManagerService
{
    private readonly DataverseEntityMetadata _metadata;

    public DataverseManagerService(
        IDataverseRepository repository,
        DataverseEntityMetadataRegistry metadataRegistry,
        ILogger<DataverseManagerService> logger)
        : base(repository, metadataRegistry, logger)
    {
        _metadata = GetMetadata("Managers");
    }

    public Task<List<Manager>> GetAllAsync()
    {
        return ExecuteAsync(async client =>
        {
            var query = new QueryExpression(_metadata.LogicalName)
            {
                ColumnSet = new ColumnSet(
                    _metadata.GetAttribute("Id"),
                    _metadata.GetAttribute("Name"),
                    _metadata.GetAttribute("Email"),
                    _metadata.GetAttribute("Position"))
            };

            query.AddOrder(_metadata.GetAttribute("Name"), OrderType.Ascending);

            var result = await client.RetrieveMultipleAsync(query).ConfigureAwait(false);
            return result.Entities.Select(Map).ToList();
        });
    }

    public Task AddAsync(Manager manager)
    {
        ArgumentNullException.ThrowIfNull(manager);

        var entity = new Entity(_metadata.LogicalName)
        {
            [_metadata.GetAttribute("Id")] = manager.Id,
            [_metadata.GetAttribute("Name")] = manager.Name,
            [_metadata.GetAttribute("Email")] = manager.Email,
            [_metadata.GetAttribute("Position")] = new OptionSetValue((int)manager.Position),
        };

        return ExecuteAsync(client => client.CreateAsync(entity));
    }

    public async Task UpdateAsync(Manager manager)
    {
        ArgumentNullException.ThrowIfNull(manager);

        var recordId = await TryResolveRecordIdAsync(
            _metadata,
            _metadata.GetAttribute("Id"),
            manager.Id).ConfigureAwait(false);

        if (recordId is null)
        {
            throw new InvalidOperationException($"Manager with Id={manager.Id} was not found in Dataverse.");
        }

        var entity = new Entity(_metadata.LogicalName)
        {
            Id = recordId.Value,
            [_metadata.GetAttribute("Name")] = manager.Name,
            [_metadata.GetAttribute("Email")] = manager.Email,
            [_metadata.GetAttribute("Position")] = new OptionSetValue((int)manager.Position),
        };

        await ExecuteAsync(client => client.UpdateAsync(entity)).ConfigureAwait(false);
    }

    public async Task DeleteAsync(int id)
    {
        var recordId = await TryResolveRecordIdAsync(
            _metadata,
            _metadata.GetAttribute("Id"),
            id).ConfigureAwait(false);

        if (recordId is null)
        {
            return;
        }

        await ExecuteAsync(client => client.DeleteAsync(_metadata.LogicalName, recordId.Value)).ConfigureAwait(false);
    }

    private Manager Map(Entity entity)
    {
        var positionAttribute = _metadata.GetAttribute("Position");
        var positionValue = entity.GetInt(positionAttribute);

        var position = Enum.IsDefined(typeof(ManagerPosition), positionValue)
            ? (ManagerPosition)positionValue
            : ManagerPosition.Manager;

        return new Manager
        {
            Id = entity.GetInt(_metadata.GetAttribute("Id")),
            Name = entity.GetString(_metadata.GetAttribute("Name")),
            Email = entity.GetString(_metadata.GetAttribute("Email")),
            Position = position,
        };
    }
}
