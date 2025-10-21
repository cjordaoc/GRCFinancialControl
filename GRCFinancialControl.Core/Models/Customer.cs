using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

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
        /// Stable identifier sourced from engagement workbooks (digits inside parentheses).
        /// </summary>
        public string CustomerCode { get; set; } = string.Empty;

        public ICollection<Engagement> Engagements { get; } = new List<Engagement>();

        [NotMapped]
        public string DisplayName
        {
            get => string.IsNullOrWhiteSpace(CustomerCode)
                ? Name
                : $"{CustomerCode} - {Name}";
        }
    }
}