namespace GRCFinancialControl.Core.Models
{
    /// <summary>
    /// Represents the outcome of testing a MySQL connection.
    /// </summary>
    public record ConnectionTestResult(bool Success, string Message);
}
