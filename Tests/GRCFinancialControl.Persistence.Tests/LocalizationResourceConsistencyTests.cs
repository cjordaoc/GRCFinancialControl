using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Xunit;

namespace GRCFinancialControl.Persistence.Tests;

public sealed class LocalizationResourceConsistencyTests
{
    public static IEnumerable<object[]> ResourceDirectories()
    {
        var root = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            ".."));

        yield return new object[] { Path.Combine(root, "GRCFinancialControl.Avalonia", "Resources") };
        yield return new object[] { Path.Combine(root, "InvoicePlanner.Avalonia", "Resources") };
    }

    [Theory]
    [MemberData(nameof(ResourceDirectories))]
    public void ResourceFiles_DoNotContainDuplicateKeys(string resourcesPath)
    {
        foreach (var file in Directory.EnumerateFiles(resourcesPath, "Strings*.resx"))
        {
            var keys = ReadResourceKeys(file);
            var duplicates = keys
                .GroupBy(key => key, StringComparer.Ordinal)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key)
                .ToArray();

            Assert.True(duplicates.Length == 0, $"{Path.GetFileName(file)} contains duplicate keys: {string.Join(", ", duplicates)}");
        }
    }

    [Theory]
    [MemberData(nameof(ResourceDirectories))]
    public void LocalizedFiles_UseNeutralKeySet(string resourcesPath)
    {
        var neutralPath = Path.Combine(resourcesPath, "Strings.resx");
        var neutralKeys = ReadResourceKeys(neutralPath)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var file in Directory.EnumerateFiles(resourcesPath, "Strings.*.resx"))
        {
            if (string.Equals(file, neutralPath, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var localizedKeys = ReadResourceKeys(file).ToHashSet(StringComparer.Ordinal);
            Assert.True(neutralKeys.SetEquals(localizedKeys),
                $"{Path.GetFileName(file)} key set differs from Strings.resx");
        }
    }

    private static IReadOnlyList<string> ReadResourceKeys(string path)
    {
        var document = XDocument.Load(path);
        return document.Root?
            .Elements("data")
            .Select(element => element.Attribute("name")?.Value ?? string.Empty)
            .ToArray() ?? Array.Empty<string>();
    }
}
