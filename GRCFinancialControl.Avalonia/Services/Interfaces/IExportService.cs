using System.Collections.Generic;
using System.Threading.Tasks;

namespace GRCFinancialControl.Avalonia.Services.Interfaces
{
    public interface IExportService
    {
        Task ExportToCsvAsync<T>(IEnumerable<T> data, string fileName);
    }
}