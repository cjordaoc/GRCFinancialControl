using System.Threading.Tasks;

namespace GRCFinancialControl.Persistence.Services.Interfaces
{
    public interface IApplicationDataBackupService
    {
        Task ExportAsync(string filePath);

        Task ImportAsync(string filePath);
    }
}
