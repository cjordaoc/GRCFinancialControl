using System.Collections.Generic;
using System.Threading.Tasks;
using GRC.Shared.Core.Models.Core;
using GRC.Shared.Core.Models.Financial;
using GRC.Shared.Core.Models.Allocations;
using GRC.Shared.Core.Models.Assignments;
using GRC.Shared.Core.Models.Lookups;
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
