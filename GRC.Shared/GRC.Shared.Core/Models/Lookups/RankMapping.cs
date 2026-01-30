using System;

using GRC.Shared.Core.Models.Core;
using GRC.Shared.Core.Models.Lookups;
using GRC.Shared.Core.Enums;

namespace GRC.Shared.Core.Models.Lookups
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
