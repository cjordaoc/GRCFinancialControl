using System;
using GRCFinancialControl.Core.Enums;

namespace GRCFinancialControl.Core.Configuration
{
    public static class DataBackendConfiguration
    {
        public const string EnvironmentVariableName = "DATA_BACKEND";

        public static DataBackend GetBackend()
        {
            var value = Environment.GetEnvironmentVariable(EnvironmentVariableName);
            if (string.IsNullOrWhiteSpace(value))
            {
                return DataBackend.MySql;
            }

            return Enum.TryParse<DataBackend>(value, ignoreCase: true, out var backend)
                ? backend
                : DataBackend.MySql;
        }
    }
}
