using System.Text.Json;
using System.Text.Json.Nodes;

namespace DvSchemaSync.Configuration;

internal sealed class ExecutionOptions
{
    public string SqlSchemaPath { get; }
    public string OutputPath { get; }
    public string? DataverseOrgUrl { get; }
    public string? DataverseClientId { get; }
    public string? DataverseClientSecret { get; }
    public string? DataverseTenantId { get; }
    public IReadOnlyDictionary<string, string> TableToEntityMap { get; }
    public bool DryRun { get; }
    public bool ApplyChanges { get; }
    public bool AllowDrop { get; }
    public bool EnvironmentAllowsDrop { get; }
    public string DeleteCandidatesPath { get; }
    public string? SolutionExportName { get; }
    public string SolutionExportDirectory { get; }

    private ExecutionOptions(
        string sqlSchemaPath,
        string outputPath,
        string? dataverseOrgUrl,
        string? dataverseClientId,
        string? dataverseClientSecret,
        string? dataverseTenantId,
        IReadOnlyDictionary<string, string> tableToEntityMap,
        bool dryRun,
        bool applyChanges,
        bool allowDrop,
        bool environmentAllowsDrop,
        string deleteCandidatesPath,
        string? solutionExportName,
        string solutionExportDirectory)
    {
        SqlSchemaPath = sqlSchemaPath;
        OutputPath = outputPath;
        DataverseOrgUrl = dataverseOrgUrl;
        DataverseClientId = dataverseClientId;
        DataverseClientSecret = dataverseClientSecret;
        DataverseTenantId = dataverseTenantId;
        TableToEntityMap = tableToEntityMap;
        DryRun = dryRun;
        ApplyChanges = applyChanges;
        AllowDrop = allowDrop;
        EnvironmentAllowsDrop = environmentAllowsDrop;
        DeleteCandidatesPath = deleteCandidatesPath;
        SolutionExportName = solutionExportName;
        SolutionExportDirectory = solutionExportDirectory;
    }

    public static ExecutionOptions Parse(string[] args)
    {
        var sqlSchemaPath = Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, "artifacts/mysql/rebuild_schema.sql"));
        var outputPath = Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, "docs/dv_alignment_report.md"));
        string? mapPath = null;
        var deleteCandidatesPath = Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, "docs/dv_delete_candidates.json"));
        var solutionExportDirectory = Environment.CurrentDirectory;
        string? solutionExportName = null;
        var dryRun = false;
        var applyChanges = false;
        var allowDrop = false;
        string? orgUrl = Environment.GetEnvironmentVariable("DV_ORG_URL");
        string? clientId = Environment.GetEnvironmentVariable("DV_CLIENT_ID");
        string? clientSecret = Environment.GetEnvironmentVariable("DV_CLIENT_SECRET");
        string? tenantId = Environment.GetEnvironmentVariable("DV_TENANT_ID");
        var dropEnv = Environment.GetEnvironmentVariable("DVSCHEMA_ALLOW_DROP");
        var environmentAllowsDrop = string.Equals(dropEnv, "1", StringComparison.OrdinalIgnoreCase) ||
                                     string.Equals(dropEnv, "true", StringComparison.OrdinalIgnoreCase);

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            switch (arg)
            {
                case "--sql":
                case "-s":
                    sqlSchemaPath = GetNextPath(args, ref i);
                    break;
                case "--output":
                case "-o":
                    outputPath = GetNextPath(args, ref i);
                    break;
                case "--map":
                case "-m":
                    mapPath = GetNextPath(args, ref i);
                    break;
                case "--delete-candidates":
                    deleteCandidatesPath = GetNextPath(args, ref i);
                    break;
                case "--solution-export":
                    solutionExportName = GetNextValue(args, ref i);
                    break;
                case "--solution-export-dir":
                    solutionExportDirectory = GetNextPath(args, ref i);
                    break;
                case "--dry-run":
                    dryRun = true;
                    break;
                case "--apply":
                    applyChanges = true;
                    break;
                case "--allow-drop":
                    allowDrop = true;
                    break;
                case "--org-url":
                    orgUrl = GetNextValue(args, ref i);
                    break;
                case "--client-id":
                    clientId = GetNextValue(args, ref i);
                    break;
                case "--client-secret":
                    clientSecret = GetNextValue(args, ref i);
                    break;
                case "--tenant-id":
                    tenantId = GetNextValue(args, ref i);
                    break;
                default:
                    throw new ArgumentException($"Unknown argument '{arg}'.");
            }
        }

        if (!File.Exists(sqlSchemaPath))
        {
            throw new FileNotFoundException($"The SQL schema file '{sqlSchemaPath}' could not be found.");
        }

        if (!Directory.Exists(solutionExportDirectory))
        {
            throw new DirectoryNotFoundException($"The solution export directory '{solutionExportDirectory}' does not exist.");
        }

        var map = LoadMapping(mapPath);

        return new ExecutionOptions(
            sqlSchemaPath,
            outputPath,
            orgUrl,
            clientId,
            clientSecret,
            tenantId,
            map,
            dryRun,
            applyChanges,
            allowDrop,
            environmentAllowsDrop,
            deleteCandidatesPath,
            solutionExportName,
            solutionExportDirectory);
    }

    public bool DropsPermitted => AllowDrop && EnvironmentAllowsDrop;

    public bool RequiresSolutionExport => !string.IsNullOrWhiteSpace(SolutionExportName);

    private static string GetNextPath(string[] args, ref int index)
    {
        var value = GetNextValue(args, ref index);
        return Path.GetFullPath(Path.IsPathRooted(value) ? value : Path.Combine(Environment.CurrentDirectory, value));
    }

    private static string GetNextValue(string[] args, ref int index)
    {
        if (index + 1 >= args.Length)
        {
            throw new ArgumentException("Missing value for argument '" + args[index] + "'.");
        }

        index += 1;
        return args[index];
    }

    private static IReadOnlyDictionary<string, string> LoadMapping(string? mapPath)
    {
        if (string.IsNullOrWhiteSpace(mapPath))
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        if (!File.Exists(mapPath))
        {
            throw new FileNotFoundException($"The mapping file '{mapPath}' could not be found.");
        }

        using var stream = File.OpenRead(mapPath);
        var root = JsonNode.Parse(stream, documentOptions: new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip })
            ?? throw new InvalidOperationException("The mapping file did not contain valid JSON.");

        if (root is not JsonObject obj)
        {
            throw new InvalidOperationException("The mapping file must be a JSON object with table-to-entity pairs.");
        }

        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var kvp in obj)
        {
            if (kvp.Value is not JsonValue valueNode)
            {
                throw new InvalidOperationException("Mapping values must be strings.");
            }

            if (!valueNode.TryGetValue(out string? value) || string.IsNullOrWhiteSpace(value))
            {
                throw new InvalidOperationException("Mapping values must be non-empty strings.");
            }

            result[kvp.Key] = value;
        }

        return result;
    }
}
