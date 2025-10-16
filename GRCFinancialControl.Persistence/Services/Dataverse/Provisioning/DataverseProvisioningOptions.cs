using System;

namespace GRCFinancialControl.Persistence.Services.Dataverse.Provisioning;

public sealed class DataverseProvisioningOptions
{
    public string MetadataPath { get; set; } = string.Empty;

    public string SqlSchemaPath { get; set; } = string.Empty;

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(MetadataPath))
        {
            throw new InvalidOperationException("The Dataverse metadata path has not been configured.");
        }

        if (string.IsNullOrWhiteSpace(SqlSchemaPath))
        {
            throw new InvalidOperationException("The SQL schema path has not been configured.");
        }
    }
}
