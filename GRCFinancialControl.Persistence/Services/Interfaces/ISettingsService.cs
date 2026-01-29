using System.Collections.Generic;
using System.Threading.Tasks;
using GRC.Shared.Core.Models.Core;
using GRC.Shared.Core.Models.Financial;
using GRC.Shared.Core.Models.Allocations;

using GRCFinancialControl.Persistence.Models.Assignments;
using GRC.Shared.Core.Models.Lookups;
using GRCFinancialControl.Core.Models;

namespace GRCFinancialControl.Persistence.Services.Interfaces
{
    public interface ISettingsService
    {
        Dictionary<string, string> GetAll();
        Task<Dictionary<string, string>> GetAllAsync();
        Task SaveAllAsync(Dictionary<string, string> settings);
        Task<ConnectionTestResult> TestConnectionAsync(string server, string database, string user, string password);
        Task<int?> GetDefaultFiscalYearIdAsync();
        Task SetDefaultFiscalYearIdAsync(int? fiscalYearId);
        Task<int?> GetDefaultClosingPeriodIdAsync();
        Task SetDefaultClosingPeriodIdAsync(int? closingPeriodId);
    }
}
