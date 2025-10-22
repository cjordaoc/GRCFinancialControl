using System;

namespace GRCFinancialControl.Core.Models
{
    public class RankMapping
    {
        public int Id { get; set; }
        public string RawRank { get; set; } = string.Empty;
        public string NormalizedRank { get; set; } = string.Empty;
        public string SpreadsheetRank { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
        public DateTime? LastSeenAt { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
