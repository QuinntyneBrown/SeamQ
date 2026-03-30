using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using SeamQ.Core.Models;

namespace SeamQ.Scanner.TypeScript;

/// <summary>
/// Parses barrel export files (public-api.ts, index.ts) to discover exported symbols.
/// </summary>
public partial class BarrelExportParser
{
    private readonly ILogger<BarrelExportParser> _logger;

    // export * from './some/path';
    [GeneratedRegex(@"^export\s+\*\s+from\s+['""]([^'""]+)['""]", RegexOptions.Multiline)]
    private static partial Regex WildcardExportRegex();

    // export { Foo, Bar } from './some/path';
    [GeneratedRegex(@"^export\s*\{([^}]+)\}\s*from\s+['""]([^'""]+)['""]", RegexOptions.Multiline)]
    private static partial Regex NamedExportRegex();

    // export default ...
    [GeneratedRegex(@"^export\s+default\s+", RegexOptions.Multiline)]
    private static partial Regex DefaultExportRegex();

    // export { Foo, Bar };  (re-export from local)
    [GeneratedRegex(@"^export\s*\{([^}]+)\}\s*;", RegexOptions.Multiline)]
    private static partial Regex LocalNamedExportRegex();

    public BarrelExportParser(ILogger<BarrelExportParser> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Finds and parses barrel export files (public-api.ts or index.ts) within a source root.
    /// </summary>
    public async Task<IReadOnlyList<ExportedSymbol>> ParseBarrelExportsAsync(
        string sourceRoot,
        CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(sourceRoot))
        {
            return [];
        }

        // Look for common barrel file names
        var barrelFileNames = new[] { "public-api.ts", "public_api.ts", "index.ts" };
        var symbols = new List<ExportedSymbol>();

        foreach (var barrelFileName in barrelFileNames)
        {
            string[] barrelFiles;
            try
            {
                barrelFiles = Directory.GetFiles(sourceRoot, barrelFileName, SearchOption.AllDirectories);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to search for {FileName} in {Root}", barrelFileName, sourceRoot);
                continue;
            }

            foreach (var barrelFile in barrelFiles)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    var fileSymbols = await ParseFileAsync(barrelFile, cancellationToken);
                    symbols.AddRange(fileSymbols);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to parse barrel file {Path}", barrelFile);
                }
            }
        }

        return symbols;
    }

    /// <summary>
    /// Parses a single barrel file and returns exported symbols.
    /// </summary>
    public async Task<IReadOnlyList<ExportedSymbol>> ParseFileAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        var content = await File.ReadAllTextAsync(filePath, cancellationToken);
        var lines = content.Split('\n');
        var symbols = new List<ExportedSymbol>();

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].TrimEnd('\r');
            var lineNumber = i + 1;

            // export { Foo, Bar } from './path'
            var namedMatch = NamedExportRegex().Match(line);
            if (namedMatch.Success)
            {
                var names = namedMatch.Groups[1].Value
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                var fromPath = namedMatch.Groups[2].Value;

                foreach (var rawName in names)
                {
                    // Handle "Foo as Bar" syntax — use the exported name (Bar)
                    var parts = rawName.Split(" as ", StringSplitOptions.TrimEntries);
                    var exportedName = parts.Length > 1 ? parts[1] : parts[0];

                    symbols.Add(new ExportedSymbol
                    {
                        Name = exportedName,
                        FilePath = fromPath,
                        LineNumber = lineNumber,
                        Kind = "NamedExport"
                    });
                }
                continue;
            }

            // export * from './path'
            var wildcardMatch = WildcardExportRegex().Match(line);
            if (wildcardMatch.Success)
            {
                var fromPath = wildcardMatch.Groups[1].Value;
                symbols.Add(new ExportedSymbol
                {
                    Name = "*",
                    FilePath = fromPath,
                    LineNumber = lineNumber,
                    Kind = "WildcardExport"
                });
                continue;
            }

            // export { Foo, Bar };
            var localMatch = LocalNamedExportRegex().Match(line);
            if (localMatch.Success)
            {
                var names = localMatch.Groups[1].Value
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

                foreach (var rawName in names)
                {
                    var parts = rawName.Split(" as ", StringSplitOptions.TrimEntries);
                    var exportedName = parts.Length > 1 ? parts[1] : parts[0];

                    symbols.Add(new ExportedSymbol
                    {
                        Name = exportedName,
                        FilePath = filePath,
                        LineNumber = lineNumber,
                        Kind = "NamedExport"
                    });
                }
                continue;
            }

            // export default
            var defaultMatch = DefaultExportRegex().Match(line);
            if (defaultMatch.Success)
            {
                symbols.Add(new ExportedSymbol
                {
                    Name = "default",
                    FilePath = filePath,
                    LineNumber = lineNumber,
                    Kind = "DefaultExport"
                });
            }
        }

        return symbols;
    }
}
