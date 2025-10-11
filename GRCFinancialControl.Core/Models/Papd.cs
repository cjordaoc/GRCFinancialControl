namespace GRCFinancialControl.Core.Models
{
    /// <summary>
    /// Represents a Partner, Associate Partner, or Director (PAPD).
    /// </summary>
    public class Papd
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Level { get; set; } = string.Empty; // e.g., "Partner", "Director"
    }
}