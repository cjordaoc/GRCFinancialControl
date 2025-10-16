using System.Threading.Tasks;
using GRCFinancialControl.Persistence.Services.Interfaces;

namespace GRCFinancialControl.Persistence.Services
{
    public sealed class DataverseSchemaInitializer : IDatabaseSchemaInitializer
    {
        public Task EnsureSchemaAsync()
        {
            return Task.CompletedTask;
        }

        public Task ClearAllDataAsync()
        {
            return Task.CompletedTask;
        }
    }
}
