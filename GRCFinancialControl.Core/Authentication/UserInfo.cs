namespace GRCFinancialControl.Core.Authentication;

/// <summary>
/// Represents basic information about the signed-in Dataverse user.
/// </summary>
public sealed record UserInfo(string? DisplayName, string? UserPrincipalName);
