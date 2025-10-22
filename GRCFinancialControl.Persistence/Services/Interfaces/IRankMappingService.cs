using System.Collections.Generic;
using System.Threading.Tasks;
using GRCFinancialControl.Core.Models;

namespace GRCFinancialControl.Persistence.Services.Interfaces
{
    public interface IRankMappingService
    {
        Task<IReadOnlyList<RankMapping>> GetAllAsync();

        Task AddAsync(RankMapping rankMapping);

        Task UpdateAsync(RankMapping rankMapping);

        Task DeleteAsync(int id);
    }
}
