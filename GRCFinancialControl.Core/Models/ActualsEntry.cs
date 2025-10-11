using System;

namespace GRCFinancialControl.Core.Models
{
    /// <summary>
    /// Represents a single entry of actual recognized hours from an imported file.
    /// </summary>
    public class ActualsEntry
    {
        public int Id { get; set; }
        public int EngagementId { get; set; }
        public Engagement Engagement { get; set; } = null!;
        public DateTime Date { get; set; }
        public double Hours { get; set; }
        public string ImportBatchId { get; set; } = string.Empty; // To trace back to the source file/upload
        public int? PapdId { get; set; }
        public Papd? Papd { get; set; }
    }
}