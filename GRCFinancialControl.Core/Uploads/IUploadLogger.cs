using System;

namespace GRCFinancialControl.Uploads
{
    public interface IUploadLogger
    {
        void LogStart(UploadFileSummary summary);
        void LogSuccess(UploadFileSummary summary);
        void LogSkipped(UploadFileSummary summary);
        void LogFailure(UploadFileSummary summary, Exception exception);
    }

    public sealed class NullUploadLogger : IUploadLogger
    {
        public static readonly NullUploadLogger Instance = new();

        private NullUploadLogger()
        {
        }

        public void LogStart(UploadFileSummary summary)
        {
        }

        public void LogSuccess(UploadFileSummary summary)
        {
        }

        public void LogSkipped(UploadFileSummary summary)
        {
        }

        public void LogFailure(UploadFileSummary summary, Exception exception)
        {
        }
    }
}
