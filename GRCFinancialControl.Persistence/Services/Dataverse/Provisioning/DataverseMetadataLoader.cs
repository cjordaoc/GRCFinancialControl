using System;
using System.IO;
using System.Text.Json;

namespace GRCFinancialControl.Persistence.Services.Dataverse.Provisioning;

public sealed class DataverseMetadataLoader
{
    private readonly string _metadataPath;

    public DataverseMetadataLoader(string metadataPath)
    {
        _metadataPath = metadataPath ?? throw new ArgumentNullException(nameof(metadataPath));
    }

    public DataverseMetadataChangeSet Load()
    {
        if (!File.Exists(_metadataPath))
        {
            throw new FileNotFoundException("The Dataverse metadata change set file was not found.", _metadataPath);
        }

        var json = File.ReadAllText(_metadataPath);
        var changeSet = JsonSerializer.Deserialize<DataverseMetadataChangeSet>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        });

        if (changeSet is null)
        {
            throw new InvalidOperationException($"Unable to deserialize Dataverse metadata change set from '{_metadataPath}'.");
        }

        return changeSet;
    }
}
