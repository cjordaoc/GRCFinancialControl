using System.Threading.Tasks;

namespace GRCFinancialControl.Persistence.Services.Interfaces
{
    public interface IDatabaseSchemaInitializer
    {
        Task EnsureSchemaAsync();
    }
}
