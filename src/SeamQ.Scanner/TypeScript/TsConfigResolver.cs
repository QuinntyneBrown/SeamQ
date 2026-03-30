using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace SeamQ.Scanner.TypeScript;

/// <summary>
/// Parses tsconfig.json files to extract path aliases, project references, and extends chains.
/// </summary>
public class TsConfigResolver
{
    private readonly ILogger<TsConfigResolver> _logger;

    public TsConfigResolver(ILogger<TsConfigResolver> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Resolves the effective tsconfig by following "extends" chains and merging compilerOptions.paths.
    /// </summary>
    public async Task<TsConfigInfo> ResolveAsync(
        string workspacePath,
        CancellationToken cancellationToken = default)
    {
        // Try tsconfig.base.json first (common in Nx), then tsconfig.json
        var candidates = new[]
        {
            Path.Combine(workspacePath, "tsconfig.base.json"),
            Path.Combine(workspacePath, "tsconfig.json")
        };

        var configPath = candidates.FirstOrDefault(File.Exists);
        if (configPath is null)
        {
            _logger.LogDebug("No tsconfig.json found in {Path}", workspacePath);
            return TsConfigInfo.Empty;
        }

        return await ResolveConfigAsync(configPath, cancellationToken);
    }

    private async Task<TsConfigInfo> ResolveConfigAsync(
        string configPath,
        CancellationToken cancellationToken)
    {
        var paths = new Dictionary<string, string[]>();
        var references = new List<string>();
        var visitedConfigs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        await ResolveRecursiveAsync(configPath, paths, references, visitedConfigs, cancellationToken);

        return new TsConfigInfo
        {
            Paths = paths,
            References = references
        };
    }

    private async Task ResolveRecursiveAsync(
        string configPath,
        Dictionary<string, string[]> paths,
        List<string> references,
        HashSet<string> visitedConfigs,
        CancellationToken cancellationToken)
    {
        var fullPath = Path.GetFullPath(configPath);
        if (!visitedConfigs.Add(fullPath) || !File.Exists(fullPath))
        {
            return;
        }

        try
        {
            var json = await File.ReadAllTextAsync(fullPath, cancellationToken);
            using var document = JsonDocument.Parse(json, new JsonDocumentOptions
            {
                CommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            });

            var root = document.RootElement;
            var configDir = Path.GetDirectoryName(fullPath)!;

            // Process "extends" first so parent paths can be overridden
            if (root.TryGetProperty("extends", out var extendsProp) &&
                extendsProp.ValueKind == JsonValueKind.String)
            {
                var extendsValue = extendsProp.GetString()!;
                var extendsPath = ResolveExtendsPath(extendsValue, configDir);
                if (extendsPath is not null)
                {
                    await ResolveRecursiveAsync(extendsPath, paths, references, visitedConfigs, cancellationToken);
                }
            }

            // Extract compilerOptions.paths
            if (root.TryGetProperty("compilerOptions", out var compilerOptions) &&
                compilerOptions.TryGetProperty("paths", out var pathsElement))
            {
                foreach (var pathEntry in pathsElement.EnumerateObject())
                {
                    var alias = pathEntry.Name;
                    var targets = pathEntry.Value.EnumerateArray()
                        .Where(v => v.ValueKind == JsonValueKind.String)
                        .Select(v => v.GetString()!)
                        .ToArray();

                    // Override parent paths with child paths
                    paths[alias] = targets;
                }
            }

            // Extract project references
            if (root.TryGetProperty("references", out var refsElement) &&
                refsElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var refEntry in refsElement.EnumerateArray())
                {
                    if (refEntry.TryGetProperty("path", out var refPath) &&
                        refPath.ValueKind == JsonValueKind.String)
                    {
                        var resolvedRef = Path.GetFullPath(
                            Path.Combine(configDir, refPath.GetString()!.Replace('/', Path.DirectorySeparatorChar)));
                        if (!references.Contains(resolvedRef))
                        {
                            references.Add(resolvedRef);
                        }
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse tsconfig at {Path}", fullPath);
        }
    }

    private string? ResolveExtendsPath(string extendsValue, string configDir)
    {
        // Handle relative paths
        if (extendsValue.StartsWith("./") || extendsValue.StartsWith("../"))
        {
            var resolved = Path.GetFullPath(Path.Combine(configDir, extendsValue.Replace('/', Path.DirectorySeparatorChar)));

            // Try with .json extension if not already present
            if (!resolved.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                if (File.Exists(resolved + ".json"))
                    return resolved + ".json";
            }

            return File.Exists(resolved) ? resolved : null;
        }

        // Bare specifier — could be a node_modules package
        // e.g., "@angular/compiler-cli/ngcc/tsconfig.json"
        var nodeModulesPath = Path.Combine(configDir, "node_modules", extendsValue.Replace('/', Path.DirectorySeparatorChar));
        if (File.Exists(nodeModulesPath))
        {
            return nodeModulesPath;
        }

        return null;
    }
}

/// <summary>
/// Represents the resolved content of a tsconfig.json (or chain of extended configs).
/// </summary>
public record TsConfigInfo
{
    /// <summary>
    /// Path aliases from compilerOptions.paths (e.g., "@mylib/*" -> ["libs/mylib/src/*"]).
    /// </summary>
    public IReadOnlyDictionary<string, string[]> Paths { get; init; } = new Dictionary<string, string[]>();

    /// <summary>
    /// Resolved project reference paths.
    /// </summary>
    public IReadOnlyList<string> References { get; init; } = [];

    public static TsConfigInfo Empty { get; } = new();
}
