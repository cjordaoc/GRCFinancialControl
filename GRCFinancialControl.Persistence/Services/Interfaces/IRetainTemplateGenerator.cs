using System.Threading.Tasks;

namespace GRCFinancialControl.Persistence.Services.Interfaces;

public interface IRetainTemplateGenerator
{
    Task<string> GenerateRetainTemplateAsync(string allocationFilePath, string destinationFilePath);
}
