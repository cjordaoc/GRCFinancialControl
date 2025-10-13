using System.Collections.Generic;
using System.Threading.Tasks;

namespace GRCFinancialControl.Avalonia.Services.Interfaces
{
    public interface IExportService
    {
        Task ExportToExcelAsync<T>(IEnumerable<T> data, string entityName);
    }
}