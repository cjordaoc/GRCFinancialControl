using System;

namespace GRCFinancialControl.Persistence.Services.People;

public sealed class DataversePeopleOptions
{
    public DataversePeopleOptions(bool enablePeopleEnrichment)
    {
        EnablePeopleEnrichment = enablePeopleEnrichment;
    }

    public bool EnablePeopleEnrichment { get; }

    public static DataversePeopleOptions FromEnvironment()
    {
        var value = Environment.GetEnvironmentVariable("DV_ENRICH_PEOPLE");
        if (string.IsNullOrWhiteSpace(value))
        {
            return new DataversePeopleOptions(false);
        }

        var normalized = value.Trim();
        return new DataversePeopleOptions(
            normalized.Equals("1", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("true", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("yes", StringComparison.OrdinalIgnoreCase));
    }
}
