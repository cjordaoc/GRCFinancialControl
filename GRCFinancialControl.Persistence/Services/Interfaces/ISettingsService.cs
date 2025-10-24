using System.Collections.Generic;
using System.Threading.Tasks;
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
