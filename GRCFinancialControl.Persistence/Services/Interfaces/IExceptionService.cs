using System.Collections.Generic;
using System.Threading.Tasks;
using GRCFinancialControl.Core.Models;

namespace GRCFinancialControl.Persistence.Services.Interfaces
{
    public interface IExceptionService
    {
        Task<List<ExceptionEntry>> GetAllAsync();
    }
}