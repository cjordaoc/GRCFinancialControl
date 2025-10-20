using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace ResxAudit;

internal static class Program
{
    private static string? _cachedRepositoryRoot;
    private static readonly Regex PlaceholderRegex = new("\\{(\\d+)\\}", RegexOptions.Compiled);
    private static readonly Regex DoubleSpaceRegex = new("  ", RegexOptions.Compiled);
    private static readonly Regex GetStringRegex = new("ResourceManager\\.GetString\\s*\\(\\s*\"([^\"]+)\"", RegexOptions.Compiled);

    private static int Main()
    {
        var repositoryRoot = FindRepositoryRoot();
        if (repositoryRoot is null)
        {
            Console.Error.WriteLine("[resx-audit] Unable to locate repository root.");
            return 1;
        }

        Console.WriteLine($"[resx-audit] Repository root: {repositoryRoot}");

        var resxGroups = LoadResxGroups(repositoryRoot);
        ReportMissingKeys(resxGroups);
        ReportPlaceholderMismatches(resxGroups);
        ReportFormattingConcerns(resxGroups);
        ReportLiteralLookups(repositoryRoot);

        Console.WriteLine("[resx-audit] Completed.");
        return 0;
    }

    private static string? FindRepositoryRoot()
    {
        if (_cachedRepositoryRoot is not null)
        {
            return _cachedRepositoryRoot;
        }

        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (current.GetFiles("*.sln").Any())
            {
                _cachedRepositoryRoot = current.FullName;
                return _cachedRepositoryRoot;
            }

            current = current.Parent;
        }

        return _cachedRepositoryRoot;
    }

    private static Dictionary<string, ResxGroup> LoadResxGroups(string repositoryRoot)
    {
        var groups = new Dictionary<string, ResxGroup>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in EnumerateFiles(repositoryRoot, "*.resx"))
        {
            var resxFile = ResxFile.Load(file);
            var baseKey = resxFile.BasePath;

            if (!groups.TryGetValue(baseKey, out var group))
            {
                group = new ResxGroup(baseKey);
                groups.Add(baseKey, group);
            }

            if (resxFile.CultureName is null)
            {
                group.Neutral = resxFile;
            }
            else
            {
                group.Cultures[resxFile.CultureName] = resxFile;
            }
        }

        return groups;
    }

    private static void ReportMissingKeys(Dictionary<string, ResxGroup> groups)
    {
        Console.WriteLine("\n[resx-audit] Missing Keys (per culture)");
        var hasFindings = false;

        foreach (var group in groups.Values.OrderBy(g => g.BasePath, StringComparer.OrdinalIgnoreCase))
        {
            IEnumerable<string> neutralKeys = group.Neutral is not null
                ? group.Neutral.Entries.Keys
                : Enumerable.Empty<string>();
            foreach (var (culture, file) in group.Cultures.OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase))
            {
                var missing = neutralKeys.Except(file.Entries.Keys, StringComparer.Ordinal).ToList();
                if (missing.Count > 0)
                {
                    hasFindings = true;
                    Console.WriteLine($"  - {culture} missing {missing.Count} keys in {RelativePath(file.Path)}");
                    foreach (var key in missing.OrderBy(k => k, StringComparer.Ordinal))
                    {
                        Console.WriteLine($"      * {key}");
                    }
                }
            }

            if (group.Neutral is null)
            {
                foreach (var (culture, file) in group.Cultures.OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase))
                {
                    hasFindings = true;
                    Console.WriteLine($"  - Neutral resource missing for {RelativePath(file.Path)} ({culture})");
                }
            }
        }

        if (!hasFindings)
        {
            Console.WriteLine("  (none)");
        }

        Console.WriteLine("\n[resx-audit] Orphan Keys (localized keys without neutral counterpart)");
        hasFindings = false;

        foreach (var group in groups.Values.OrderBy(g => g.BasePath, StringComparer.OrdinalIgnoreCase))
        {
            var neutralKeys = group.Neutral is not null
                ? new HashSet<string>(group.Neutral.Entries.Keys, StringComparer.Ordinal)
                : new HashSet<string>(StringComparer.Ordinal);
            foreach (var (culture, file) in group.Cultures.OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase))
            {
                var orphan = file.Entries.Keys.Except(neutralKeys, StringComparer.Ordinal).ToList();
                if (orphan.Count > 0)
                {
                    hasFindings = true;
                    Console.WriteLine($"  - {culture} has {orphan.Count} orphan keys in {RelativePath(file.Path)}");
                    foreach (var key in orphan.OrderBy(k => k, StringComparer.Ordinal))
                    {
                        Console.WriteLine($"      * {key}");
                    }
                }
            }
        }

        if (!hasFindings)
        {
            Console.WriteLine("  (none)");
        }
    }

    private static void ReportPlaceholderMismatches(Dictionary<string, ResxGroup> groups)
    {
        Console.WriteLine("\n[resx-audit] Placeholder mismatches");
        var hasFindings = false;

        foreach (var group in groups.Values)
        {
            if (group.Neutral is null)
            {
                continue;
            }

            foreach (var (culture, file) in group.Cultures)
            {
                foreach (var (key, neutralEntry) in group.Neutral.Entries)
                {
                    if (!file.Entries.TryGetValue(key, out var localizedEntry))
                    {
                        continue;
                    }

                    var neutralPlaceholders = CountPlaceholders(neutralEntry.Value);
                    var localizedPlaceholders = CountPlaceholders(localizedEntry.Value);

                    if (!neutralPlaceholders.SequenceEqual(localizedPlaceholders))
                    {
                        hasFindings = true;
                        Console.WriteLine($"  - {culture} {RelativePath(file.Path)} key '{key}' -> neutral {{{string.Join(",", neutralPlaceholders)}}} vs localized {{{string.Join(",", localizedPlaceholders)}}}");
                    }
                }
            }
        }

        if (!hasFindings)
        {
            Console.WriteLine("  (none)");
        }
    }

    private static void ReportFormattingConcerns(Dictionary<string, ResxGroup> groups)
    {
        Console.WriteLine("\n[resx-audit] Formatting issues");
        var hasFindings = false;

        foreach (var file in groups.Values.SelectMany(g => g.AllFiles()))
        {
            foreach (var entry in file.Entries.Values)
            {
                if (entry.Value is null)
                {
                    continue;
                }

                if (!string.Equals(entry.Value, entry.Value.Trim(), StringComparison.Ordinal))
                {
                    hasFindings = true;
                    Console.WriteLine($"  - Leading/trailing whitespace: {RelativePath(file.Path)} :: {entry.Key}");
                }

                if (DoubleSpaceRegex.IsMatch(entry.Value))
                {
                    hasFindings = true;
                    Console.WriteLine($"  - Double spaces: {RelativePath(file.Path)} :: {entry.Key}");
                }

                if (HasStrayAmpersand(entry.Value))
                {
                    hasFindings = true;
                    Console.WriteLine($"  - Potential stray '&': {RelativePath(file.Path)} :: {entry.Key}");
                }

                if (!HasBalancedBraces(entry.Value))
                {
                    hasFindings = true;
                    Console.WriteLine($"  - Unbalanced braces: {RelativePath(file.Path)} :: {entry.Key}");
                }
            }
        }

        if (!hasFindings)
        {
            Console.WriteLine("  (none)");
        }
    }

    private static void ReportLiteralLookups(string repositoryRoot)
    {
        Console.WriteLine("\n[resx-audit] Literal ResourceManager.GetString usages");
        var hasFindings = false;

        foreach (var file in EnumerateFiles(repositoryRoot, "*.cs"))
        {
            var content = File.ReadAllText(file);
            foreach (Match match in GetStringRegex.Matches(content))
            {
                hasFindings = true;
                Console.WriteLine($"  - {RelativePath(file)} :: {match.Groups[1].Value}");
            }
        }

        if (!hasFindings)
        {
            Console.WriteLine("  (none)");
        }
    }

    private static IEnumerable<string> EnumerateFiles(string root, string searchPattern)
    {
        return Directory.EnumerateFiles(root, searchPattern, SearchOption.AllDirectories)
            .Where(path => !IsIgnored(path));
    }

    private static bool IsIgnored(string path)
    {
        var segments = path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        foreach (var segment in segments)
        {
            if (segment.Equals("bin", StringComparison.OrdinalIgnoreCase) ||
                segment.Equals("obj", StringComparison.OrdinalIgnoreCase) ||
                segment.Equals(".git", StringComparison.OrdinalIgnoreCase) ||
                segment.Equals("node_modules", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static string RelativePath(string fullPath)
    {
        var repositoryRoot = FindRepositoryRoot() ?? string.Empty;
        if (string.IsNullOrEmpty(repositoryRoot))
        {
            return fullPath;
        }

        if (!fullPath.StartsWith(repositoryRoot, StringComparison.OrdinalIgnoreCase))
        {
            return fullPath;
        }

        return fullPath.Substring(repositoryRoot.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    private static IReadOnlyList<int> CountPlaceholders(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return Array.Empty<int>();
        }

        return PlaceholderRegex.Matches(value)
            .Select(match => int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture))
            .OrderBy(v => v)
            .ToList();
    }

    private static bool HasStrayAmpersand(string value)
    {
        for (var index = 0; index < value.Length; index++)
        {
            if (value[index] != '&')
            {
                continue;
            }

            if (index + 1 < value.Length && value[index + 1] == '&')
            {
                index++;
                continue;
            }

            if (value[index..].StartsWith("&amp;", StringComparison.OrdinalIgnoreCase))
            {
                index += 3;
                continue;
            }

            return true;
        }

        return false;
    }

    private static bool HasBalancedBraces(string value)
    {
        var balance = 0;
        foreach (var ch in value)
        {
            if (ch == '{')
            {
                balance++;
            }
            else if (ch == '}')
            {
                balance--;
                if (balance < 0)
                {
                    return false;
                }
            }
        }

        return balance == 0;
    }

    private sealed record ResxEntry(string Key, string? Value, string? Comment);

    private sealed class ResxFile
    {
        private ResxFile(string path, string basePath, string? cultureName, Dictionary<string, ResxEntry> entries)
        {
            Path = path;
            BasePath = basePath;
            CultureName = cultureName;
            Entries = entries;
        }

        public string Path { get; }
        public string BasePath { get; }
        public string? CultureName { get; }
        public Dictionary<string, ResxEntry> Entries { get; }

        public static ResxFile Load(string path)
        {
            var directory = System.IO.Path.GetDirectoryName(path) ?? string.Empty;
            var fileName = System.IO.Path.GetFileNameWithoutExtension(path);
            string? culture = null;
            string baseName = fileName;

            var separatorIndex = fileName.LastIndexOf('.');
            if (separatorIndex > -1)
            {
                var candidate = fileName[(separatorIndex + 1)..];
                if (LooksLikeCulture(candidate))
                {
                    culture = candidate;
                    baseName = fileName[..separatorIndex];
                }
            }

            var basePath = System.IO.Path.GetFullPath(System.IO.Path.Combine(directory, baseName + ".resx"));
            var entries = ReadEntries(path);
            return new ResxFile(System.IO.Path.GetFullPath(path), basePath, culture, entries);
        }

        private static Dictionary<string, ResxEntry> ReadEntries(string path)
        {
            var entries = new Dictionary<string, ResxEntry>(StringComparer.Ordinal);
            var document = XDocument.Load(path);
            if (document.Root is null)
            {
                return entries;
            }

            foreach (var element in document.Root.Elements("data"))
            {
                var name = element.Attribute("name")?.Value;
                if (string.IsNullOrEmpty(name))
                {
                    continue;
                }

                var type = element.Attribute("type")?.Value;
                if (!string.IsNullOrEmpty(type) && !type.StartsWith(typeof(string).FullName!, StringComparison.Ordinal))
                {
                    continue;
                }

                var value = element.Element("value")?.Value;
                var comment = element.Element("comment")?.Value;
                entries[name] = new ResxEntry(name, value, comment);
            }

            return entries;
        }

        private static bool LooksLikeCulture(string candidate)
        {
            try
            {
                _ = CultureInfo.GetCultureInfo(candidate);
                return true;
            }
            catch (CultureNotFoundException)
            {
                return false;
            }
        }
    }

    private sealed class ResxGroup
    {
        public ResxGroup(string basePath)
        {
            BasePath = basePath;
            Cultures = new Dictionary<string, ResxFile>(StringComparer.OrdinalIgnoreCase);
        }

        public string BasePath { get; }
        public ResxFile? Neutral { get; set; }
        public Dictionary<string, ResxFile> Cultures { get; }

        public IEnumerable<ResxFile> AllFiles()
        {
            if (Neutral is not null)
            {
                yield return Neutral;
            }

            foreach (var file in Cultures.Values)
            {
                yield return file;
            }
        }
    }
}
