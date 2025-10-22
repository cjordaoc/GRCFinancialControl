namespace GRCFinancialControl.Core.Models
{
    public sealed record RankOption(string Code, string Name)
    {
        public string DisplayName => string.IsNullOrWhiteSpace(Name) ? Code : Name;
    }
}
