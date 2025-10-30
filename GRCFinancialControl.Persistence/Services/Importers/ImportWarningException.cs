using System;

namespace GRCFinancialControl.Persistence.Services.Importers
{
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
