using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using GRCFinancialControl.Common;

namespace GRCFinancialControl.Parsing;

internal sealed class FileFieldUploadMapProvider
{
    private const string MappingFileName = "FIle Field Upload Map.xlsx";
    private static readonly Lazy<FileFieldUploadMapProvider> LazyInstance = new(Create);
    private static readonly Regex QuotedTextPattern = new("['\"\u201C\u201D]([^'\"\u201C\u201D]+)['\"\u201C\u201D]", RegexOptions.Compiled);

    private readonly List<MapEntry> _entries;

    private FileFieldUploadMapProvider(List<MapEntry> entries)
    {
        _entries = entries;
    }

    public static FileFieldUploadMapProvider Instance => LazyInstance.Value;

    public HeaderSchema BuildSchema(IDictionary<string, string[]> baseSynonyms, params string[] requiredColumns)
    {
        if (baseSynonyms == null)
        {
            throw new ArgumentNullException(nameof(baseSynonyms));
        }

        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var key in baseSynonyms.Keys)
        {
            keys.Add(key);
        }

        if (requiredColumns != null)
        {
            foreach (var required in requiredColumns)
            {
                if (!string.IsNullOrWhiteSpace(required))
                {
                    keys.Add(required);
                }
            }
        }

        var synonyms = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        foreach (var key in keys)
        {
            var aggregate = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                key
            };

            if (baseSynonyms.TryGetValue(key, out var hints))
            {
                foreach (var hint in hints)
                {
                    if (!string.IsNullOrWhiteSpace(hint))
                    {
                        aggregate.Add(hint.Trim());
                    }
                }
            }

            foreach (var alias in ResolveSynonyms(key))
            {
                aggregate.Add(alias);
            }

            synonyms[key] = aggregate.ToArray();
        }

        return new HeaderSchema(synonyms, requiredColumns ?? Array.Empty<string>());
    }

    private IEnumerable<string> ResolveSynonyms(string canonicalKey)
    {
        if (string.IsNullOrWhiteSpace(canonicalKey))
        {
            yield break;
        }

        var normalizedKey = ExcelParsingUtilities.NormalizeHeader(canonicalKey);
        if (string.IsNullOrEmpty(normalizedKey))
        {
            yield break;
        }

        foreach (var entry in _entries)
        {
            if (entry.Matches(normalizedKey))
            {
                foreach (var alias in entry.Aliases)
                {
                    if (!string.IsNullOrWhiteSpace(alias))
                    {
                        yield return alias;
                    }
                }
            }
        }
    }

        private static FileFieldUploadMapProvider Create()
        {
            var entries = new List<MapEntry>();
            var mappingPath = ResolveMappingPath();
            if (mappingPath == null)
            {
                throw new FileNotFoundException(
                    $"Could not locate '{MappingFileName}'. Ensure the DataTemplate folder is deployed alongside the executable.");
            }

            using var workbook = new XLWorkbook(mappingPath);
            var sheet = workbook.Worksheets.FirstOrDefault();
            if (sheet != null)
            {
                var headers = ReadHeaderIndexes(sheet);
                foreach (var row in sheet.RowsUsed().Skip(1))
                {
                    var field = row.Cell(headers.FieldColumn).GetValue<string>()?.Trim();
                    if (string.IsNullOrWhiteSpace(field))
                    {
                        continue;
                    }

                    var headerLocation = headers.HeaderLocationColumn > 0
                        ? row.Cell(headers.HeaderLocationColumn).GetValue<string>()?.Trim()
                        : null;

                    entries.Add(new MapEntry(field, headerLocation));
                }
            }

            return new FileFieldUploadMapProvider(entries);
        }

        private static (int FieldColumn, int HeaderLocationColumn) ReadHeaderIndexes(IXLWorksheet sheet)
        {
            var headerRow = sheet.FirstRowUsed();
            if (headerRow == null)
            {
                throw new InvalidDataException("The File Field Upload Map worksheet does not contain any rows.");
            }
            var fieldColumn = 0;
            var headerLocationColumn = 0;

            foreach (var cell in headerRow.CellsUsed())
            {
                var header = ExcelParsingUtilities.NormalizeHeader(cell.GetValue<string>() ?? string.Empty);
                if (header == "FIELD")
                {
                    fieldColumn = cell.Address.ColumnNumber;
                }
                else if (header == "HEADERLOCATION")
                {
                    headerLocationColumn = cell.Address.ColumnNumber;
                }
            }

            if (fieldColumn == 0)
            {
                throw new InvalidDataException("The File Field Upload Map is missing the 'Field' column.");
            }

            return (fieldColumn, headerLocationColumn);
        }

    private static string? ResolveMappingPath()
    {
        var searchRoot = new DirectoryInfo(AppContext.BaseDirectory);
        while (searchRoot != null)
        {
            var candidate = Path.Combine(searchRoot.FullName, "DataTemplate", MappingFileName);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            searchRoot = searchRoot.Parent;
        }

        return null;
    }

    private sealed class MapEntry
    {
        private readonly HashSet<string> _normalizedAliases;

        public MapEntry(string field, string? headerLocation)
        {
            Field = field;
            Aliases = ExpandAliases(field, headerLocation).ToArray();
            _normalizedAliases = new HashSet<string>(Aliases.Select(ExcelParsingUtilities.NormalizeHeader).Where(v => !string.IsNullOrEmpty(v)));
        }

        public string Field { get; }

        public IReadOnlyCollection<string> Aliases { get; }

        public bool Matches(string normalizedKey)
        {
            if (string.IsNullOrEmpty(normalizedKey))
            {
                return false;
            }

            foreach (var alias in _normalizedAliases)
            {
                if (alias.Contains(normalizedKey, StringComparison.OrdinalIgnoreCase) ||
                    normalizedKey.Contains(alias, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static IEnumerable<string> ExpandAliases(string field, string? headerLocation)
        {
            if (!string.IsNullOrWhiteSpace(field))
            {
                var trimmed = field.Trim();
                yield return trimmed;

                foreach (var part in SplitComponents(trimmed))
                {
                    yield return part;
                }
            }

            if (!string.IsNullOrWhiteSpace(headerLocation))
            {
                foreach (Match match in QuotedTextPattern.Matches(headerLocation))
                {
                    var value = match.Groups[1].Value.Trim();
                    if (!string.IsNullOrEmpty(value))
                    {
                        yield return value;
                    }
                }
            }
        }

        private static IEnumerable<string> SplitComponents(string value)
        {
            var separators = new[] { '/', ':', '|', '-' };
            foreach (var separator in separators)
            {
                if (value.Contains(separator, StringComparison.Ordinal))
                {
                    foreach (var segment in value.Split(separator))
                    {
                        var cleaned = StringNormalizer.TrimToNull(segment);
                        if (!string.IsNullOrEmpty(cleaned) && !string.Equals(cleaned, value, StringComparison.Ordinal))
                        {
                            yield return cleaned;
                        }
                    }
                }
            }

            var parenthesisStart = value.IndexOf('(');
            var parenthesisEnd = value.IndexOf(')');
            if (parenthesisStart >= 0 && parenthesisEnd > parenthesisStart)
            {
                var inner = value.Substring(parenthesisStart + 1, parenthesisEnd - parenthesisStart - 1).Trim();
                if (!string.IsNullOrEmpty(inner))
                {
                    yield return inner;
                }

                var before = value.Substring(0, parenthesisStart).Trim();
                if (!string.IsNullOrEmpty(before))
                {
                    yield return before;
                }
            }
        }
    }
}
