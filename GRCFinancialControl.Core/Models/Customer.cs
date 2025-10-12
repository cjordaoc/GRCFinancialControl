using System.Collections.Generic;

namespace GRCFinancialControl.Core.Models
{
    public class Customer
    {
        public int Id { get; set; }

        /// <summary>
        /// Human friendly customer name as displayed in files and UI.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// External identifier from upstream systems (may be null for legacy records).
        /// </summary>
        public string? ClientIdText { get; set; }

        public ICollection<Engagement> Engagements { get; set; } = new List<Engagement>();
    }
}