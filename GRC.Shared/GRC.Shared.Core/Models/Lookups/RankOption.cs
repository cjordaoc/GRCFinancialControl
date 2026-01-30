using GRC.Shared.Core.Models.Core;
using GRC.Shared.Core.Models.Lookups;
using GRC.Shared.Core.Enums;

namespace GRC.Shared.Core.Models.Lookups
{
    public sealed record RankOption(string Code, string Name)
    {
        public string DisplayName => string.IsNullOrWhiteSpace(Name) ? Code : Name;
    }
}
