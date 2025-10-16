using System.Threading.Tasks;
using GRCFinancialControl.Persistence.Services.Importers;

namespace GRCFinancialControl.Persistence.Services.Interfaces
{
    public interface IFullManagementDataImporter
    {
        Task<FullManagementDataImportResult> ImportAsync(string filePath);
    }
}
