using System.Threading.Tasks;
using GRCFinancialControl.Core.Models;

namespace GRCFinancialControl.Persistence.Services.Interfaces
{
    public interface IFiscalCalendarConsistencyService
    {
        Task<FiscalCalendarValidationSummary> EnsureConsistencyAsync();
    }
}
