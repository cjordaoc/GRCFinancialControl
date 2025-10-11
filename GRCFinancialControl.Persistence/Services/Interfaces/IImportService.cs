using System.Threading.Tasks;

namespace GRCFinancialControl.Persistence.Services.Interfaces
{
    public interface IImportService
    {
        Task<string> ImportBudgetAsync(string filePath);
        Task<string> ImportActualsAsync(string filePath);
    }
}