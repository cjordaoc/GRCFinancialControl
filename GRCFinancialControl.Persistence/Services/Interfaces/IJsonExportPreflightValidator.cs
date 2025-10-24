using GRCFinancialControl.Persistence.Services.Exporters.Json;

namespace GRCFinancialControl.Persistence.Services.Interfaces;

public interface IJsonExportPreflightValidator
{
    JsonExportPreflightReport Validate();
}
