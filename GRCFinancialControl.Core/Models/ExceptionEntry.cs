namespace GRCFinancialControl.Core.Models
{
    public class ExceptionEntry
    {
        public int Id { get; set; }
        public DateTime Timestamp { get; set; }
        public string SourceFile { get; set; } = string.Empty;
        public string RowData { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
    }
}