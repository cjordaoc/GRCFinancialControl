using System.Threading.Tasks;
using GRCFinancialControl.Avalonia.Services.Models;

namespace GRCFinancialControl.Avalonia.Services.Interfaces
{
    public interface IPowerBiEmbeddingService
    {
        Task<PowerBiEmbedConfiguration> GetConfigurationAsync();
    }
}
