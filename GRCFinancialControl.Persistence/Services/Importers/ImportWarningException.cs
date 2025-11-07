using System;

namespace GRCFinancialControl.Persistence.Services.Importers
{
    /// <summary>
    /// Exception thrown for non-critical import warnings.
    /// </summary>
    public class ImportWarningException : Exception
    {
        public ImportWarningException()
        {
        }

        public ImportWarningException(string message)
            : base(message)
        {
        }

        public ImportWarningException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
