using GRCFinancialControl.Core.Enums;

namespace GRCFinancialControl.Core.Configuration
{
    public sealed class DataBackendOptions
    {
        public DataBackendOptions(DataBackend backend)
        {
            Backend = backend;
        }

        public DataBackend Backend { get; }
    }
}
