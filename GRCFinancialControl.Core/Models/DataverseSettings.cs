namespace GRCFinancialControl.Core.Models;

public class DataverseSettings
{
    public string OrgUrl { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;

    public bool IsComplete()
    {
        return !string.IsNullOrWhiteSpace(OrgUrl)
            && !string.IsNullOrWhiteSpace(TenantId)
            && !string.IsNullOrWhiteSpace(ClientId)
            && !string.IsNullOrWhiteSpace(ClientSecret);
    }
}
