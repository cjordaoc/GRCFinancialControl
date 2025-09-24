using System;

namespace GRCFinancialControl.Uploads
{
    public sealed class UploadDataException : InvalidOperationException
    {
        public UploadDataException(string message)
            : base(message)
        {
        }

        public UploadDataException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
