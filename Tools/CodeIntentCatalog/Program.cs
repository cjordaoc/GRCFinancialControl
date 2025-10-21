using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using System.Linq;
using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.MSBuild;

namespace CodeIntentCatalog;

internal static class Program
{
    private static readonly SymbolDisplayFormat TypeDisplayFormat = new(
        globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Omitted,
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers);
    private static readonly Lazy<string> RepositoryRoot = new(LocateRepositoryRoot);

    public static async Task<int> Main(string[] args)
    {
        try
        {
            var options = ParseOptions(args);
            var solutionPath = ResolveSolutionPath(options.SolutionPath);
            if (solutionPath is null)
            {
                Console.Error.WriteLine("Unable to locate a solution file. Provide --solution <path>.");
                return 1;
            }

            EnsureMsBuildRegistered();

            using var workspace = MSBuildWorkspace.Create();
            workspace.WorkspaceFailed += (_, eventArgs) =>
            {
                if (eventArgs.Diagnostic.Kind == WorkspaceDiagnosticKind.Failure)
                {
                    Console.Error.WriteLine(eventArgs.Diagnostic.Message);
                }
            };

            var solution = await workspace.OpenSolutionAsync(solutionPath).ConfigureAwait(false);
            var entries = await BuildCatalogAsync(solution).ConfigureAwait(false);

            var outputPath = ResolveOutputPath(options.OutputPath, solutionPath);
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

            await WriteCatalogAsync(entries, outputPath).ConfigureAwait(false);

            var relative = Path.GetRelativePath(Environment.CurrentDirectory, outputPath);
            Console.WriteLine($"Catalog generated with {entries.Count} entries -> {relative}");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Catalog generation failed: {ex.Message}");
            Console.Error.WriteLine(ex);
            return 1;
        }
    }

    private static async Task<List<CatalogEntry>> BuildCatalogAsync(Solution solution)
    {
        var entries = new List<CatalogEntry>();
        var seenTypes = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
        var seenMethods = new HashSet<IMethodSymbol>(SymbolEqualityComparer.Default);

        foreach (var project in solution.Projects)
        {
            var compilation = await project.GetCompilationAsync().ConfigureAwait(false);
            if (compilation is null)
            {
                continue;
            }

            foreach (var document in project.Documents)
            {
                if (!document.SupportsSyntaxTree)
                {
                    continue;
                }

                var root = await document.GetSyntaxRootAsync().ConfigureAwait(false);
                if (root is null)
                {
                    continue;
                }

                var model = compilation.GetSemanticModel(root.SyntaxTree);
                foreach (var declaration in root.DescendantNodes().OfType<TypeDeclarationSyntax>())
                {
                    var symbol = model.GetDeclaredSymbol(declaration);
                    if (symbol is not INamedTypeSymbol namedType)
                    {
                        continue;
                    }

                    if (!IsSupportedType(namedType) || !seenTypes.Add(namedType))
                    {
                        continue;
                    }

                    entries.Add(CreateTypeEntry(namedType, document.FilePath));

                    foreach (var method in namedType.GetMembers().OfType<IMethodSymbol>())
                    {
                        if (!IsSupportedMethod(method) || !seenMethods.Add(method))
                        {
                            continue;
                        }

                        entries.Add(CreateMethodEntry(method));
                    }
                }
            }
        }

        entries.Sort((left, right) => string.Compare(left.Name, right.Name, StringComparison.Ordinal));
        return entries;
    }

    private static bool IsSupportedType(INamedTypeSymbol symbol)
        => symbol.TypeKind is TypeKind.Class or TypeKind.Struct or TypeKind.Interface;

    private static bool IsSupportedMethod(IMethodSymbol symbol)
    {
        if (symbol.MethodKind is MethodKind.PropertyGet or MethodKind.PropertySet or MethodKind.EventAdd or MethodKind.EventRemove or MethodKind.EventRaise)
        {
            return false;
        }

        if (symbol.Name.StartsWith("<", StringComparison.Ordinal))
        {
            return false;
        }

        return symbol.PartialDefinitionPart is null;
    }

    private static CatalogEntry CreateTypeEntry(INamedTypeSymbol symbol, string? filePath)
    {
        var inputs = CollectConstructorInputs(symbol);
        var outputs = CollectPublicProperties(symbol);
        var dependencies = CollectTypeDependencies(symbol);

        return new CatalogEntry
        {
            Kind = symbol.TypeKind.ToString(),
            Name = symbol.ToDisplayString(TypeDisplayFormat),
            Purpose = ResolvePurpose(symbol, $"Represents {ToPhrase(symbol.Name)}"),
            Inputs = inputs,
            Outputs = outputs,
            Dependencies = dependencies,
            SourceFile = filePath is null ? null : Path.GetRelativePath(FindRepositoryRoot(), filePath)
        };
    }

    private static CatalogEntry CreateMethodEntry(IMethodSymbol symbol)
    {
        var purposePrefix = symbol.MethodKind switch
        {
            MethodKind.Constructor => $"Initializes {ToPhrase(symbol.ContainingType.Name)}",
            MethodKind.StaticConstructor => $"Prepares static state for {ToPhrase(symbol.ContainingType.Name)}",
            _ => $"Executes {ToPhrase(symbol.Name)}"
        };

        var dependencies = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var parameter in symbol.Parameters)
        {
            AddTypeDependency(dependencies, parameter.Type);
        }

        if (!symbol.ReturnsVoid)
        {
            AddTypeDependency(dependencies, symbol.ReturnType);
        }

        return new CatalogEntry
        {
            Kind = "Method",
            Name = symbol.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat),
            Purpose = ResolvePurpose(symbol, purposePrefix),
            Inputs = symbol.Parameters.Length == 0
                ? "None"
                : string.Join(", ", symbol.Parameters.Select(p => $"{p.Type.ToDisplayString(TypeDisplayFormat)} {p.Name}")),
            Outputs = symbol.MethodKind == MethodKind.Constructor
                ? symbol.ContainingType.ToDisplayString(TypeDisplayFormat)
                : symbol.ReturnType.ToDisplayString(TypeDisplayFormat),
            Dependencies = dependencies.Count == 0 ? "None" : string.Join(", ", dependencies),
            SourceFile = ResolvePrimaryLocation(symbol)
        };
    }

    private static string CollectConstructorInputs(INamedTypeSymbol symbol)
    {
        var inputs = symbol.Constructors
            .Where(ctor => ctor.DeclaredAccessibility is Accessibility.Public or Accessibility.Internal)
            .SelectMany(ctor => ctor.Parameters)
            .Select(parameter => $"{parameter.Type.ToDisplayString(TypeDisplayFormat)} {parameter.Name}")
            .Distinct(StringComparer.Ordinal)
            .ToList();

        return inputs.Count == 0 ? "None" : string.Join(", ", inputs);
    }

    private static string CollectPublicProperties(INamedTypeSymbol symbol)
    {
        var properties = symbol.GetMembers()
            .OfType<IPropertySymbol>()
            .Where(property => property.DeclaredAccessibility is Accessibility.Public or Accessibility.Internal)
            .Select(property => $"{property.Name} ({property.Type.ToDisplayString(TypeDisplayFormat)})")
            .Distinct(StringComparer.Ordinal)
            .Take(10)
            .ToList();

        if (properties.Count == 0)
        {
            return "None";
        }

        var suffix = symbol.GetMembers().OfType<IPropertySymbol>().Count(property => property.DeclaredAccessibility is Accessibility.Public or Accessibility.Internal) > properties.Count
            ? "…"
            : string.Empty;

        return string.Join(", ", properties) + suffix;
    }

    private static string CollectTypeDependencies(INamedTypeSymbol symbol)
    {
        var dependencies = new SortedSet<string>(StringComparer.Ordinal);

        if (symbol.BaseType is { SpecialType: not SpecialType.System_Object })
        {
            AddTypeDependency(dependencies, symbol.BaseType);
        }

        foreach (var iface in symbol.Interfaces)
        {
            AddTypeDependency(dependencies, iface);
        }

        foreach (var member in symbol.GetMembers())
        {
            switch (member)
            {
                case IFieldSymbol field:
                    AddTypeDependency(dependencies, field.Type);
                    break;
                case IPropertySymbol property:
                    AddTypeDependency(dependencies, property.Type);
                    break;
                case IEventSymbol eventSymbol:
                    AddTypeDependency(dependencies, eventSymbol.Type);
                    break;
            }
        }

        return dependencies.Count == 0 ? "None" : string.Join(", ", dependencies);
    }

    private static void AddTypeDependency(ISet<string> dependencies, ITypeSymbol? type)
    {
        if (type is null)
        {
            return;
        }

        switch (type)
        {
            case IArrayTypeSymbol arrayType:
                AddTypeDependency(dependencies, arrayType.ElementType);
                return;
            case IPointerTypeSymbol pointerType:
                AddTypeDependency(dependencies, pointerType.PointedAtType);
                return;
            case INamedTypeSymbol namedType:
                foreach (var argument in namedType.TypeArguments)
                {
                    AddTypeDependency(dependencies, argument);
                }
                break;
        }

        if (type.ContainingNamespace is not null)
        {
            var ns = type.ContainingNamespace.ToDisplayString();
            if (ns.StartsWith("System", StringComparison.Ordinal))
            {
                return;
            }
        }

        var formatted = type.ToDisplayString(TypeDisplayFormat);
        if (!string.IsNullOrWhiteSpace(formatted))
        {
            dependencies.Add(formatted);
        }
    }

    private static string ResolvePurpose(ISymbol symbol, string fallback)
    {
        var documentation = symbol.GetDocumentationCommentXml(expandIncludes: true, cancellationToken: default);
        if (!string.IsNullOrWhiteSpace(documentation))
        {
            try
            {
                var xml = XDocument.Parse(documentation);
                var summary = xml.Descendants("summary").FirstOrDefault()?.Value;
                if (!string.IsNullOrWhiteSpace(summary))
                {
                    return NormalizeWhitespace(summary);
                }
            }
            catch (Exception)
            {
                // Ignore malformed XML and fall back to generated text.
            }
        }

        return fallback + ".";
    }

    private static string NormalizeWhitespace(string text)
    {
        var builder = new StringBuilder();
        var previousWasWhitespace = false;
        foreach (var rune in text.EnumerateRunes())
        {
            if (Rune.IsWhiteSpace(rune))
            {
                if (!previousWasWhitespace)
                {
                    builder.Append(' ');
                    previousWasWhitespace = true;
                }
            }
            else
            {
                builder.Append(rune);
                previousWasWhitespace = false;
            }
        }

        return builder.ToString().Trim();
    }

    private static string ToPhrase(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return name;
        }

        var cleaned = Regex.Replace(name, "`[0-9]+", string.Empty);
        cleaned = cleaned.Replace("_", " ");
        cleaned = Regex.Replace(cleaned, "(?<=.)(?=\\p{Lu}[\\p{Ll}])", " ");
        cleaned = Regex.Replace(cleaned, "(?<=[a-z0-9])(?=\\p{Lu})", " ");
        cleaned = Regex.Replace(cleaned, "\n+", " ");
        return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(cleaned.Trim());
    }

    private static Options ParseOptions(IReadOnlyList<string> args)
    {
        string? solution = null;
        string? output = null;

        for (var index = 0; index < args.Count; index++)
        {
            switch (args[index])
            {
                case "--solution" when index + 1 < args.Count:
                    solution = args[++index];
                    break;
                case "--output" when index + 1 < args.Count:
                    output = args[++index];
                    break;
            }
        }

        return new Options(solution, output);
    }

    private static string? ResolveSolutionPath(string? overridePath)
    {
        if (!string.IsNullOrWhiteSpace(overridePath))
        {
            return Path.GetFullPath(overridePath);
        }

        var current = new DirectoryInfo(Environment.CurrentDirectory);
        while (current is not null)
        {
            var solution = current.GetFiles("*.sln").FirstOrDefault();
            if (solution is not null)
            {
                return solution.FullName;
            }

            current = current.Parent;
        }

        return null;
    }

    private static string ResolveOutputPath(string? overridePath, string solutionPath)
    {
        if (!string.IsNullOrWhiteSpace(overridePath))
        {
            return Path.GetFullPath(overridePath);
        }

        var solutionDirectory = Path.GetDirectoryName(solutionPath)!;
        return Path.Combine(solutionDirectory, "CodeIntentCatalog.json");
    }

    private static string? ResolvePrimaryLocation(IMethodSymbol symbol)
    {
        var location = symbol.Locations.FirstOrDefault(loc => loc.IsInSource);
        if (location is null)
        {
            return null;
        }

        var path = location.SourceTree?.FilePath;
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        var repositoryRoot = FindRepositoryRoot();
        return Path.GetRelativePath(repositoryRoot, path);
    }

    private static string FindRepositoryRoot()
        => RepositoryRoot.Value;

    private static string LocateRepositoryRoot()
    {
        var current = new DirectoryInfo(Environment.CurrentDirectory);
        while (current is not null)
        {
            if (current.GetFiles(".git").Any() || current.GetDirectories(".git").Any())
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        return Environment.CurrentDirectory;
    }

    private static async Task WriteCatalogAsync(List<CatalogEntry> entries, string outputPath)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        await using var stream = File.Create(outputPath);
        await JsonSerializer.SerializeAsync(stream, entries, options).ConfigureAwait(false);
    }

    private static void EnsureMsBuildRegistered()
    {
        if (!MSBuildLocator.IsRegistered)
        {
            MSBuildLocator.RegisterDefaults();
        }
    }

    private sealed record Options(string? SolutionPath, string? OutputPath);

    private sealed record CatalogEntry
    {
        public string Kind { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public string Purpose { get; init; } = string.Empty;
        public string Inputs { get; init; } = string.Empty;
        public string Outputs { get; init; } = string.Empty;
        public string Dependencies { get; init; } = string.Empty;
        public string? SourceFile { get; init; }
    }
}
