using System.Collections.Generic;
using System.Threading.Tasks;

namespace GRCFinancialControl.Persistence.Services.Interfaces;

public interface IConnectionPackageService
{
    Task ExportAsync(string filePath, string passphrase);
    Task<IReadOnlyDictionary<string, string>> ImportAsync(string filePath, string passphrase);
}
