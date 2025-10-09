using System.Collections.Generic;

namespace GRCFinancialControl.Uploads
{
    public sealed class UploadBatchSummary
    {
        private readonly List<UploadFileSummary> _files = new();

        public IReadOnlyList<UploadFileSummary> Files => _files;

        public void Add(UploadFileSummary summary)
        {
            if (summary != null)
            {
                _files.Add(summary);
            }
        }

        public void Clear() => _files.Clear();
    }
}
