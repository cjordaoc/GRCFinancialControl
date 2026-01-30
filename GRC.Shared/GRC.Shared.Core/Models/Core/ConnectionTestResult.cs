using GRC.Shared.Core.Models.Core;
using GRC.Shared.Core.Models.Lookups;
using GRC.Shared.Core.Enums;

namespace GRC.Shared.Core.Models.Core
{
    /// <summary>
    /// Represents the outcome of testing a MySQL connection.
    /// </summary>
    public record ConnectionTestResult(bool Success, string Message);
}
