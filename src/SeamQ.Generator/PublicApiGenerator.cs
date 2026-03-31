using System.Text;
using SeamQ.Core.Abstractions;
using SeamQ.Core.Models;

namespace SeamQ.Generator;

/// <summary>
/// Generates public API documentation in markdown format for Angular projects.
/// Creates a separate file per project, documenting public types, methods,
/// enumerations, and classes with their code comments.
/// </summary>
public class PublicApiGenerator : IPublicApiGenerator
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> GenerateAsync(
        Workspace workspace,
        string outputDirectory,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);

        Directory.CreateDirectory(outputDirectory);

        var generatedFiles = new List<string>();

        foreach (var project in workspace.Projects)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (project.Exports.Count == 0)
            {
                continue;
            }

            var filePath = Path.Combine(outputDirectory, SanitizeFileName(project.Name) + "-public-api.md");
            var markdown = GenerateProjectMarkdown(project);

            await File.WriteAllTextAsync(filePath, markdown, cancellationToken);
            generatedFiles.Add(filePath);
        }

        return generatedFiles;
    }

    public static string GenerateProjectMarkdown(Project project)
    {
        var sb = new StringBuilder();
        var title = $"{project.Name} Public API";

        // Title
        sb.AppendLine($"# {title}");
        sb.AppendLine();

        // Overview section
        sb.AppendLine("## Overview");
        sb.AppendLine();
        var projectKind = project.Type == ProjectType.Library ? "library" : "application";
        sb.AppendLine($"Public API surface for the **{project.Name}** {projectKind}.");
        sb.AppendLine();

        // Group exports by kind
        var grouped = GroupExportsByKind(project.Exports);

        // Summary table
        sb.AppendLine("### Summary");
        sb.AppendLine();
        sb.AppendLine("| Category | Count |");
        sb.AppendLine("|----------|-------|");
        foreach (var (category, symbols) in grouped)
        {
            sb.AppendLine($"| {category} | {symbols.Count} |");
        }
        sb.AppendLine();

        // Detail sections for each category
        foreach (var (category, symbols) in grouped)
        {
            sb.AppendLine($"## {category}");
            sb.AppendLine();

            foreach (var symbol in symbols.OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase))
            {
                WriteSymbolSection(sb, symbol);
            }
        }

        return sb.ToString();
    }

    private static void WriteSymbolSection(StringBuilder sb, ExportedSymbol symbol)
    {
        sb.AppendLine($"### `{symbol.Name}`");
        sb.AppendLine();

        // Documentation from code comments
        if (!string.IsNullOrWhiteSpace(symbol.Documentation))
        {
            sb.AppendLine(symbol.Documentation.Trim());
            sb.AppendLine();
        }

        // Metadata table
        var hasType = !string.IsNullOrWhiteSpace(symbol.TypeSignature);
        var hasParent = !string.IsNullOrWhiteSpace(symbol.ParentName);
        var hasFile = !string.IsNullOrWhiteSpace(symbol.FilePath);

        if (hasType || hasParent || hasFile)
        {
            sb.AppendLine("| Property | Value |");
            sb.AppendLine("|----------|-------|");

            if (hasType)
                sb.AppendLine($"| Type | `{symbol.TypeSignature}` |");

            if (hasParent)
                sb.AppendLine($"| Parent | `{symbol.ParentName}` |");

            if (hasFile)
            {
                var location = symbol.LineNumber > 0
                    ? $"`{symbol.FilePath}:{symbol.LineNumber}`"
                    : $"`{symbol.FilePath}`";
                sb.AppendLine($"| Source | {location} |");
            }

            sb.AppendLine();
        }
    }

    private static List<(string Category, List<ExportedSymbol> Symbols)> GroupExportsByKind(
        IReadOnlyList<ExportedSymbol> exports)
    {
        var groups = new Dictionary<string, List<ExportedSymbol>>(StringComparer.OrdinalIgnoreCase);

        foreach (var export in exports)
        {
            var category = MapKindToCategory(export.Kind);
            if (!groups.TryGetValue(category, out var list))
            {
                list = [];
                groups[category] = list;
            }
            list.Add(export);
        }

        // Return in a deterministic display order
        var ordering = new[]
        {
            "Components", "Services", "Directives", "Pipes",
            "Classes", "Abstract Classes", "Interfaces", "Enumerations",
            "Type Aliases", "Injection Tokens", "Methods", "Properties",
            "Input Bindings", "Output Bindings", "Signal Inputs",
            "Injected Dependencies", "Observables",
            "Actions", "Selectors", "Other"
        };

        var result = new List<(string, List<ExportedSymbol>)>();
        foreach (var category in ordering)
        {
            if (groups.TryGetValue(category, out var symbols))
            {
                result.Add((category, symbols));
                groups.Remove(category);
            }
        }

        // Add any remaining categories not in our predefined order
        foreach (var (category, symbols) in groups.OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase))
        {
            result.Add((category, symbols));
        }

        return result;
    }

    private static string MapKindToCategory(string kind)
    {
        return kind?.ToLowerInvariant() switch
        {
            "class" => "Classes",
            "abstractclass" or "abstract class" => "Abstract Classes",
            "component" => "Components",
            "injectable" => "Services",
            "directive" => "Directives",
            "pipe" => "Pipes",
            "interface" => "Interfaces",
            "enum" or "enumeration" => "Enumerations",
            "typealias" or "type alias" or "type" => "Type Aliases",
            "injectiontoken" or "injection token" => "Injection Tokens",
            "method" => "Methods",
            "property" => "Properties",
            "inputbinding" or "input binding" or "input" => "Input Bindings",
            "outputbinding" or "output binding" or "output" => "Output Bindings",
            "signalinput" or "signal input" or "modelsignal" or "model signal" => "Signal Inputs",
            "injecteddependency" or "injected dependency" => "Injected Dependencies",
            "observable" => "Observables",
            "action" => "Actions",
            "selector" => "Selectors",
            _ => "Other"
        };
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = new StringBuilder(name.Length);
        foreach (var c in name)
        {
            sanitized.Append(invalid.Contains(c) ? '-' : c);
        }
        return sanitized.ToString();
    }
}
