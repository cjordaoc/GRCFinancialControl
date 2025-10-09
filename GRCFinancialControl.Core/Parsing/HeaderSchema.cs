using System;
using System.Collections.Generic;

namespace GRCFinancialControl.Parsing
{
    public sealed class HeaderSchema
    {
        public HeaderSchema(IDictionary<string, string[]> synonyms, params string[] requiredColumns)
        {
            Synonyms = synonyms ?? throw new ArgumentNullException(nameof(synonyms));
            RequiredColumns = requiredColumns ?? Array.Empty<string>();
        }

        public IDictionary<string, string[]> Synonyms { get; }
        public IReadOnlyList<string> RequiredColumns { get; }
    }
}
