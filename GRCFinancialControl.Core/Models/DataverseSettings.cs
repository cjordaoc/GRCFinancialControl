using GRCFinancialControl.Core.Enums;

namespace GRCFinancialControl.Core.Models;

public class DataverseSettings
{
    public string OrgUrl { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public DataverseAuthMode AuthMode { get; set; } = DataverseAuthMode.Interactive;

    public bool IsComplete()
    {
        return AuthMode switch
        {
            DataverseAuthMode.ClientSecret => !string.IsNullOrWhiteSpace(OrgUrl)
                                              && !string.IsNullOrWhiteSpace(TenantId)
                                              && !string.IsNullOrWhiteSpace(ClientId)
                                              && !string.IsNullOrWhiteSpace(ClientSecret),
            _ => !string.IsNullOrWhiteSpace(OrgUrl)
                 && !string.IsNullOrWhiteSpace(ClientId)
        };
    }
}
