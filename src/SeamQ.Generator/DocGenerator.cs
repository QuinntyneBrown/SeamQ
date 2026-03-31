using System.Diagnostics;
using System.Text;
using SeamQ.Core.Abstractions;
using SeamQ.Core.Models;
using SeamQ.Renderer.PlantUml;

namespace SeamQ.Generator;

/// <summary>
/// Generates comprehensive API reference documentation for Angular projects.
/// Creates per-project folders containing a README.md and PlantUML class diagrams.
/// </summary>
public class DocGenerator : IDocGenerator
{
    /// <summary>
    /// Barrel-export kinds that represent re-exports rather than actual symbols.
    /// </summary>
    private static readonly HashSet<string> BarrelExportKinds = new(StringComparer.OrdinalIgnoreCase)
    {
        "NamedExport", "WildcardExport", "DefaultExport"
    };

    /// <summary>
    /// Kinds that represent child members of a parent type.
    /// </summary>
    private static readonly HashSet<string> MemberKinds = new(StringComparer.OrdinalIgnoreCase)
    {
        "Method", "Property", "InputBinding", "Input Binding", "Input",
        "OutputBinding", "Output Binding", "Output",
        "SignalInput", "Signal Input", "ModelSignal", "Model Signal",
        "Observable"
    };

    /// <summary>
    /// Kinds that represent top-level types that own members.
    /// </summary>
    private static readonly HashSet<string> TypeKinds = new(StringComparer.OrdinalIgnoreCase)
    {
        "Class", "AbstractClass", "Abstract Class", "Component",
        "Interface", "Injectable",
        "Enum", "Enumeration",
        "TypeAlias", "Type Alias", "Type",
        "InjectionToken", "Injection Token"
    };

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

            var documentableSymbols = GetDocumentableSymbols(project.Exports);
            if (documentableSymbols.Count == 0)
            {
                continue;
            }

            var projectDir = Path.Combine(outputDirectory, SanitizeFileName(project.Name));
            Directory.CreateDirectory(projectDir);

            var diagramsDir = Path.Combine(projectDir, "diagrams");
            Directory.CreateDirectory(diagramsDir);

            // Group symbols: top-level types and their children
            var typeGroups = BuildTypeGroups(documentableSymbols);

            // Generate PlantUML diagrams for each top-level type
            var diagramFiles = await GenerateDiagramsAsync(typeGroups, diagramsDir, cancellationToken);
            generatedFiles.AddRange(diagramFiles);

            // Generate README.md
            var readmePath = Path.Combine(projectDir, "README.md");
            var markdown = GenerateReadme(project, typeGroups);
            await File.WriteAllTextAsync(readmePath, markdown, cancellationToken);
            generatedFiles.Add(readmePath);
        }

        return generatedFiles;
    }

    /// <summary>
    /// Filters exports to only documentable symbols (excluding barrel re-exports,
    /// private members, and protected members).
    /// </summary>
    internal static List<ExportedSymbol> GetDocumentableSymbols(IReadOnlyList<ExportedSymbol> exports)
    {
        var result = new List<ExportedSymbol>();

        foreach (var symbol in exports)
        {
            // Skip barrel re-exports
            if (BarrelExportKinds.Contains(symbol.Kind))
                continue;

            // Skip private/protected members (names starting with _ or #)
            if (symbol.Name.StartsWith('_') || symbol.Name.StartsWith('#'))
                continue;

            // Handle dotted names like "TileConfig.width" - check the member part
            var memberName = symbol.Name;
            var dotIndex = symbol.Name.LastIndexOf('.');
            if (dotIndex >= 0 && dotIndex < symbol.Name.Length - 1)
            {
                memberName = symbol.Name[(dotIndex + 1)..];
            }

            if (memberName.StartsWith('_') || memberName.StartsWith('#'))
                continue;

            // Skip if documentation context indicates private/protected
            // (but always include @Input/@Output/signal inputs)
            var isBinding = IsBindingKind(symbol.Kind);
            if (!isBinding && IsPrivateOrProtected(symbol))
                continue;

            result.Add(symbol);
        }

        return result;
    }

    private static bool IsBindingKind(string kind)
    {
        return kind.Equals("InputBinding", StringComparison.OrdinalIgnoreCase)
               || kind.Equals("Input Binding", StringComparison.OrdinalIgnoreCase)
               || kind.Equals("Input", StringComparison.OrdinalIgnoreCase)
               || kind.Equals("OutputBinding", StringComparison.OrdinalIgnoreCase)
               || kind.Equals("Output Binding", StringComparison.OrdinalIgnoreCase)
               || kind.Equals("Output", StringComparison.OrdinalIgnoreCase)
               || kind.Equals("SignalInput", StringComparison.OrdinalIgnoreCase)
               || kind.Equals("Signal Input", StringComparison.OrdinalIgnoreCase)
               || kind.Equals("ModelSignal", StringComparison.OrdinalIgnoreCase)
               || kind.Equals("Model Signal", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsPrivateOrProtected(ExportedSymbol symbol)
    {
        // Check if the documentation or type signature hints at private/protected
        if (symbol.Documentation != null)
        {
            var docLower = symbol.Documentation.ToLowerInvariant();
            if (docLower.Contains("@private") || docLower.Contains("@protected"))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Groups symbols into top-level types with their child members.
    /// </summary>
    internal static List<TypeGroup> BuildTypeGroups(List<ExportedSymbol> symbols)
    {
        var topLevelSymbols = new List<ExportedSymbol>();
        var childSymbols = new List<ExportedSymbol>();

        foreach (var symbol in symbols)
        {
            if (!string.IsNullOrEmpty(symbol.ParentName) && MemberKinds.Contains(symbol.Kind))
            {
                childSymbols.Add(symbol);
            }
            else if (TypeKinds.Contains(symbol.Kind))
            {
                topLevelSymbols.Add(symbol);
            }
            else if (string.IsNullOrEmpty(symbol.ParentName))
            {
                // Standalone items (actions, selectors, etc.) become their own group
                topLevelSymbols.Add(symbol);
            }
            else
            {
                childSymbols.Add(symbol);
            }
        }

        var groups = new List<TypeGroup>();

        foreach (var parent in topLevelSymbols.OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase))
        {
            var children = childSymbols
                .Where(c => string.Equals(c.ParentName, parent.Name, StringComparison.OrdinalIgnoreCase)
                            || (c.Name.Contains('.') && c.Name.StartsWith(parent.Name + ".", StringComparison.OrdinalIgnoreCase)))
                .OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            groups.Add(new TypeGroup(parent, children));
        }

        // Find orphan children whose parent wasn't found as a top-level symbol
        var assignedChildren = groups.SelectMany(g => g.Members).ToHashSet();
        var orphans = childSymbols.Where(c => !assignedChildren.Contains(c)).ToList();

        foreach (var orphan in orphans)
        {
            groups.Add(new TypeGroup(orphan, []));
        }

        return groups;
    }

    /// <summary>
    /// Generates the README.md content for a project.
    /// </summary>
    internal static string GenerateReadme(Project project, List<TypeGroup> typeGroups)
    {
        var sb = new StringBuilder();
        var projectKind = project.Type == ProjectType.Library ? "library" : "application";

        // Title
        sb.AppendLine($"# {project.Name} API Reference");
        sb.AppendLine();

        // Table of Contents
        sb.AppendLine("## Table of Contents");
        sb.AppendLine("- [Overview](#overview)");

        var categorized = GroupByCategory(typeGroups);
        foreach (var (category, _) in categorized)
        {
            var anchor = category.ToLowerInvariant().Replace(" ", "-");
            sb.AppendLine($"- [{category}](#{anchor})");
        }
        sb.AppendLine();

        // Overview
        sb.AppendLine("## Overview");
        sb.AppendLine();
        sb.AppendLine($"API documentation for **{project.Name}** {projectKind}.");
        sb.AppendLine();
        sb.AppendLine("| Category | Count |");
        sb.AppendLine("|----------|-------|");
        foreach (var (category, groups) in categorized)
        {
            sb.AppendLine($"| {category} | {groups.Count} |");
        }
        sb.AppendLine();

        // Detail sections per category
        foreach (var (category, groups) in categorized)
        {
            sb.AppendLine($"## {category}");
            sb.AppendLine();

            foreach (var group in groups)
            {
                WriteTypeSection(sb, group);
            }
        }

        return sb.ToString();
    }

    private static void WriteTypeSection(StringBuilder sb, TypeGroup group)
    {
        var symbol = group.Symbol;
        var simpleName = GetSimpleName(symbol.Name);

        sb.AppendLine($"### `{simpleName}`");
        sb.AppendLine();

        // Description
        var description = !string.IsNullOrWhiteSpace(symbol.Documentation)
            ? symbol.Documentation.Trim()
            : GenerateAutoDescription(symbol);

        sb.AppendLine(description);
        sb.AppendLine();

        // Type information table
        sb.AppendLine("| Property | Value |");
        sb.AppendLine("|----------|-------|");
        sb.AppendLine($"| **Kind** | {symbol.Kind} |");
        if (!string.IsNullOrWhiteSpace(symbol.FilePath))
        {
            var loc = symbol.LineNumber > 0 ? $"{symbol.FilePath}:{symbol.LineNumber}" : symbol.FilePath;
            sb.AppendLine($"| **Source** | `{loc}` |");
        }
        if (!string.IsNullOrWhiteSpace(symbol.TypeSignature))
        {
            if (symbol.TypeSignature.Contains("extends ", StringComparison.OrdinalIgnoreCase))
            {
                var ext = ExtractAfterKeyword(symbol.TypeSignature, "extends");
                if (ext != null) sb.AppendLine($"| **Extends** | `{ext}` |");
            }
            if (symbol.TypeSignature.Contains("implements ", StringComparison.OrdinalIgnoreCase))
            {
                var impl = ExtractAfterKeyword(symbol.TypeSignature, "implements");
                if (impl != null) sb.AppendLine($"| **Implements** | `{impl}` |");
            }
        }
        sb.AppendLine();

        // Diagram reference (only for types that get diagrams)
        if (TypeKinds.Contains(symbol.Kind))
        {
            var diagramBase = SanitizeFileName(simpleName);
            sb.AppendLine($"![{simpleName} class diagram](diagrams/{diagramBase}.png)");
            sb.AppendLine();
            sb.AppendLine($"[PlantUML source](diagrams/{diagramBase}.puml)");
            sb.AppendLine();
        }

        // Members table: properties, inputs, outputs, signals
        var properties = group.Members
            .Where(m => IsPropertyLike(m.Kind))
            .ToList();

        if (properties.Count > 0)
        {
            sb.AppendLine("#### Members");
            sb.AppendLine();
            sb.AppendLine("| Name | Type | Description |");
            sb.AppendLine("|------|------|-------------|");

            foreach (var member in properties)
            {
                var memberName = GetMemberName(member);
                var type = !string.IsNullOrWhiteSpace(member.TypeSignature)
                    ? $"`{member.TypeSignature}`"
                    : "-";
                var memberDoc = !string.IsNullOrWhiteSpace(member.Documentation)
                    ? member.Documentation.Trim()
                    : GenerateAutoDescription(member);

                sb.AppendLine($"| {memberName} | {type} | {memberDoc} |");
            }
            sb.AppendLine();
        }

        // Methods table
        var methods = group.Members
            .Where(m => m.Kind.Equals("Method", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (methods.Count > 0)
        {
            sb.AppendLine("#### Methods");
            sb.AppendLine();
            sb.AppendLine("| Signature | Returns | Description |");
            sb.AppendLine("|-----------|---------|-------------|");

            foreach (var method in methods)
            {
                var methodName = GetMemberName(method);
                var (signature, returnType) = ParseMethodSignature(method);
                var methodDoc = !string.IsNullOrWhiteSpace(method.Documentation)
                    ? method.Documentation.Trim()
                    : GenerateAutoDescription(method);

                sb.AppendLine($"| `{signature}` | `{returnType}` | {methodDoc} |");
            }
            sb.AppendLine();
        }

        // Source location
        if (!string.IsNullOrWhiteSpace(symbol.FilePath))
        {
            var location = symbol.LineNumber > 0
                ? $"`{symbol.FilePath}:{symbol.LineNumber}`"
                : $"`{symbol.FilePath}`";
            sb.AppendLine($"**Source:** {location}");
            sb.AppendLine();
        }

        sb.AppendLine("---");
        sb.AppendLine();
    }

    private static bool IsPropertyLike(string kind)
    {
        return kind.Equals("Property", StringComparison.OrdinalIgnoreCase)
               || kind.Equals("InputBinding", StringComparison.OrdinalIgnoreCase)
               || kind.Equals("Input Binding", StringComparison.OrdinalIgnoreCase)
               || kind.Equals("Input", StringComparison.OrdinalIgnoreCase)
               || kind.Equals("OutputBinding", StringComparison.OrdinalIgnoreCase)
               || kind.Equals("Output Binding", StringComparison.OrdinalIgnoreCase)
               || kind.Equals("Output", StringComparison.OrdinalIgnoreCase)
               || kind.Equals("SignalInput", StringComparison.OrdinalIgnoreCase)
               || kind.Equals("Signal Input", StringComparison.OrdinalIgnoreCase)
               || kind.Equals("ModelSignal", StringComparison.OrdinalIgnoreCase)
               || kind.Equals("Model Signal", StringComparison.OrdinalIgnoreCase)
               || kind.Equals("Observable", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Generates an auto-description when JSDoc is absent.
    /// </summary>
    internal static string GenerateAutoDescription(ExportedSymbol symbol)
    {
        var kind = symbol.Kind.ToLowerInvariant().Replace(" ", "");

        return kind switch
        {
            "component" => BuildComponentDescription(symbol),
            "injectable" => "Injectable service",
            "interface" => $"Interface defining the shape of {GetSimpleName(symbol.Name)}",
            "enum" or "enumeration" => $"Enumeration defining {GetSimpleName(symbol.Name)} values",
            "typealias" or "type" => !string.IsNullOrWhiteSpace(symbol.TypeSignature)
                ? $"Type alias for {symbol.TypeSignature}"
                : $"Type alias for {GetSimpleName(symbol.Name)}",
            "injectiontoken" => !string.IsNullOrWhiteSpace(symbol.TypeSignature)
                ? $"Injection token of type {symbol.TypeSignature}"
                : $"Injection token for {GetSimpleName(symbol.Name)}",
            "method" => BuildMethodDescription(symbol),
            "property" => !string.IsNullOrWhiteSpace(symbol.TypeSignature)
                ? $"Property of type {symbol.TypeSignature}"
                : $"Property of {GetSimpleName(symbol.Name)}",
            "inputbinding" or "input" => !string.IsNullOrWhiteSpace(symbol.TypeSignature)
                ? $"Input binding of type {symbol.TypeSignature}"
                : "Input binding",
            "outputbinding" or "output" => "Output event emitter",
            "signalinput" or "modelsignal" => !string.IsNullOrWhiteSpace(symbol.TypeSignature)
                ? $"Signal input of type {symbol.TypeSignature}"
                : "Signal input",
            "observable" => !string.IsNullOrWhiteSpace(symbol.TypeSignature)
                ? $"Observable of type {symbol.TypeSignature}"
                : "Observable stream",
            "action" => $"Action: {GetSimpleName(symbol.Name)}",
            "selector" => !string.IsNullOrWhiteSpace(symbol.TypeSignature)
                ? $"Selector returning {symbol.TypeSignature}"
                : $"Selector: {GetSimpleName(symbol.Name)}",
            "class" or "abstractclass" => $"Class {GetSimpleName(symbol.Name)}",
            _ => $"{symbol.Kind} {GetSimpleName(symbol.Name)}"
        };
    }

    private static string BuildComponentDescription(ExportedSymbol symbol)
    {
        var desc = "Angular component";
        // Check if selector info is in TypeSignature or Documentation
        if (!string.IsNullOrWhiteSpace(symbol.TypeSignature) &&
            symbol.TypeSignature.Contains("selector"))
        {
            desc += $" ({symbol.TypeSignature})";
        }
        return desc;
    }

    private static string BuildMethodDescription(ExportedSymbol symbol)
    {
        if (string.IsNullOrWhiteSpace(symbol.TypeSignature))
            return $"Method {GetMemberName(symbol)}";

        var typeSig = symbol.TypeSignature;

        // Try to extract return type from signatures like "Observable<any>" or "(p: Type) => ReturnType"
        var arrowIndex = typeSig.IndexOf("=>", StringComparison.Ordinal);
        if (arrowIndex >= 0)
        {
            var paramsPart = typeSig[..arrowIndex].Trim().Trim('(', ')').Trim();
            var returnPart = typeSig[(arrowIndex + 2)..].Trim();
            return $"Accepts {paramsPart} and returns {returnPart}";
        }

        // Simple return type
        return $"Accepts parameters and returns {typeSig}";
    }

    private static (string Signature, string ReturnType) ParseMethodSignature(ExportedSymbol method)
    {
        var name = GetMemberName(method);

        if (string.IsNullOrWhiteSpace(method.TypeSignature))
            return ($"{name}()", "void");

        var typeSig = method.TypeSignature;

        // Handle arrow function signatures: (params) => ReturnType
        var arrowIndex = typeSig.IndexOf("=>", StringComparison.Ordinal);
        if (arrowIndex >= 0)
        {
            var paramsPart = typeSig[..arrowIndex].Trim().Trim('(', ')').Trim();
            var returnPart = typeSig[(arrowIndex + 2)..].Trim();
            return ($"{name}({paramsPart})", returnPart);
        }

        // Handle parser-style method signatures: (params): ReturnType
        if (typeSig.StartsWith('('))
        {
            var closeParen = FindMatchingParen(typeSig, 0);
            if (closeParen > 0)
            {
                var paramsPart = typeSig[1..closeParen].Trim();
                var rest = typeSig[(closeParen + 1)..].Trim();
                var returnType = rest.StartsWith(':') ? rest[1..].Trim() : rest;
                if (string.IsNullOrWhiteSpace(returnType)) returnType = "void";
                return ($"{name}({paramsPart})", returnType);
            }
        }

        // TypeSignature is just the return type
        return ($"{name}()", typeSig);
    }

    private static int FindMatchingParen(string text, int openIndex)
    {
        var depth = 0;
        for (var i = openIndex; i < text.Length; i++)
        {
            if (text[i] == '(') depth++;
            else if (text[i] == ')') { depth--; if (depth == 0) return i; }
        }
        return -1;
    }

    /// <summary>
    /// Generates PlantUML diagram files for each top-level type.
    /// </summary>
    private static async Task<List<string>> GenerateDiagramsAsync(
        List<TypeGroup> typeGroups,
        string diagramsDir,
        CancellationToken cancellationToken)
    {
        var files = new List<string>();

        foreach (var group in typeGroups)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!TypeKinds.Contains(group.Symbol.Kind))
                continue;

            var simpleName = GetSimpleName(group.Symbol.Name);
            var filePath = Path.Combine(diagramsDir, SanitizeFileName(simpleName) + ".puml");

            var puml = GenerateDiagram(group);
            await File.WriteAllTextAsync(filePath, puml, cancellationToken);
            files.Add(filePath);
        }

        return files;
    }

    /// <summary>
    /// Generates a PlantUML class diagram for a single type and its members.
    /// </summary>
    internal static string GenerateDiagram(TypeGroup group)
    {
        var encoder = new PlantUmlEncoder();
        var symbol = group.Symbol;
        var simpleName = GetSimpleName(symbol.Name);
        var kindLower = symbol.Kind.ToLowerInvariant().Replace(" ", "");

        encoder.StartClassDiagram($"{simpleName} Class Diagram");

        var members = BuildDiagramMembers(group);

        switch (kindLower)
        {
            case "interface":
                encoder.AddInterface(simpleName, members);
                break;
            case "enum" or "enumeration":
                encoder.AddEnum(simpleName, members);
                break;
            default:
                encoder.AddClass(simpleName, members);
                break;
        }

        // Check for extends/implements in TypeSignature
        if (!string.IsNullOrWhiteSpace(symbol.TypeSignature))
        {
            var typeSig = symbol.TypeSignature;

            if (typeSig.Contains("extends ", StringComparison.OrdinalIgnoreCase))
            {
                var extendsMatch = ExtractAfterKeyword(typeSig, "extends");
                if (extendsMatch != null)
                {
                    encoder.AddRelationship(simpleName, extendsMatch, "--|>", "extends");
                }
            }

            if (typeSig.Contains("implements ", StringComparison.OrdinalIgnoreCase))
            {
                var implementsMatch = ExtractAfterKeyword(typeSig, "implements");
                if (implementsMatch != null)
                {
                    foreach (var iface in implementsMatch.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
                    {
                        encoder.AddRelationship(simpleName, iface.Trim(), "..|>", "implements");
                    }
                }
            }
        }

        // Show injected dependencies as dependency arrows
        foreach (var member in group.Members)
        {
            if (!string.IsNullOrWhiteSpace(member.TypeSignature) &&
                member.Kind.Equals("InjectedDependency", StringComparison.OrdinalIgnoreCase))
            {
                encoder.AddRelationship(simpleName, member.TypeSignature, "-->", "depends");
            }
            else if (!string.IsNullOrWhiteSpace(member.TypeSignature) &&
                member.Kind.Equals("Property", StringComparison.OrdinalIgnoreCase) &&
                member.TypeSignature.EndsWith("Service", StringComparison.OrdinalIgnoreCase))
            {
                encoder.AddRelationship(simpleName, member.TypeSignature, "-->", "depends");
            }
        }

        encoder.EndDiagram();
        return encoder.Build();
    }

    private static List<string> BuildDiagramMembers(TypeGroup group)
    {
        var members = new List<string>();
        var kindLower = group.Symbol.Kind.ToLowerInvariant().Replace(" ", "");

        if (kindLower is "enum" or "enumeration")
        {
            // For enums, list the values
            foreach (var member in group.Members)
            {
                members.Add(GetMemberName(member));
            }
            return members;
        }

        foreach (var member in group.Members)
        {
            var memberName = GetMemberName(member);
            var memberKind = member.Kind.ToLowerInvariant().Replace(" ", "");
            var type = member.TypeSignature ?? "any";

            var prefix = memberKind switch
            {
                "inputbinding" or "input" => "<<input>> +",
                "outputbinding" or "output" => "<<output>> +",
                "signalinput" or "modelsignal" => "<<signal>> +",
                _ => "+"
            };

            if (member.Kind.Equals("Method", StringComparison.OrdinalIgnoreCase))
            {
                var (sig, ret) = ParseMethodSignature(member);
                members.Add($"{prefix}{sig}: {ret}");
            }
            else
            {
                members.Add($"{prefix}{memberName}: {type}");
            }
        }

        return members;
    }

    private static string? ExtractAfterKeyword(string text, string keyword)
    {
        var idx = text.IndexOf(keyword, StringComparison.OrdinalIgnoreCase);
        if (idx < 0) return null;

        var rest = text[(idx + keyword.Length)..].Trim();

        // Take until next keyword or end
        var nextKeyword = -1;
        var keywords = new[] { "extends", "implements", "{" };
        foreach (var kw in keywords)
        {
            if (string.Equals(kw, keyword, StringComparison.OrdinalIgnoreCase))
                continue;
            var kwIdx = rest.IndexOf(kw, StringComparison.OrdinalIgnoreCase);
            if (kwIdx >= 0 && (nextKeyword < 0 || kwIdx < nextKeyword))
                nextKeyword = kwIdx;
        }

        return nextKeyword >= 0 ? rest[..nextKeyword].Trim() : rest.Trim();
    }

    /// <summary>
    /// Groups type groups by their display category.
    /// </summary>
    private static List<(string Category, List<TypeGroup> Groups)> GroupByCategory(List<TypeGroup> typeGroups)
    {
        var groups = new Dictionary<string, List<TypeGroup>>(StringComparer.OrdinalIgnoreCase);

        foreach (var tg in typeGroups)
        {
            var category = MapKindToCategory(tg.Symbol.Kind);
            if (!groups.TryGetValue(category, out var list))
            {
                list = [];
                groups[category] = list;
            }
            list.Add(tg);
        }

        var ordering = new[]
        {
            "Components", "Classes", "Abstract Classes", "Interfaces", "Enumerations",
            "Type Aliases", "Injection Tokens", "Services",
            "Input Bindings", "Output Bindings", "Signal Inputs", "Observables",
            "Actions", "Selectors", "Other"
        };

        var result = new List<(string, List<TypeGroup>)>();
        foreach (var category in ordering)
        {
            if (groups.TryGetValue(category, out var items))
            {
                result.Add((category, items));
                groups.Remove(category);
            }
        }

        foreach (var (category, items) in groups.OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase))
        {
            result.Add((category, items));
        }

        return result;
    }

    private static string MapKindToCategory(string kind)
    {
        return kind.ToLowerInvariant().Replace(" ", "") switch
        {
            "component" => "Components",
            "class" => "Classes",
            "abstractclass" => "Abstract Classes",
            "interface" => "Interfaces",
            "enum" or "enumeration" => "Enumerations",
            "typealias" or "type" => "Type Aliases",
            "injectiontoken" => "Injection Tokens",
            "injectable" => "Services",
            "method" => "Methods",
            "property" => "Properties",
            "inputbinding" or "input" => "Input Bindings",
            "outputbinding" or "output" => "Output Bindings",
            "signalinput" or "modelsignal" => "Signal Inputs",
            "observable" => "Observables",
            "action" => "Actions",
            "selector" => "Selectors",
            _ => "Other"
        };
    }

    private static string GetSimpleName(string name)
    {
        var dotIndex = name.LastIndexOf('.');
        return dotIndex >= 0 && dotIndex < name.Length - 1
            ? name[(dotIndex + 1)..]
            : name;
    }

    private static string GetMemberName(ExportedSymbol member)
    {
        var name = member.Name;
        // If name is "Parent.Member", return just "Member"
        if (!string.IsNullOrEmpty(member.ParentName) && name.StartsWith(member.ParentName + ".", StringComparison.OrdinalIgnoreCase))
        {
            name = name[(member.ParentName.Length + 1)..];
        }
        return name;
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

    /// <summary>
    /// Renders all .puml files under a directory to .png using the local PlantUML JAR.
    /// Returns the number of PNGs rendered, or -1 if PlantUML is not available.
    /// </summary>
    public static int RenderDiagramsToPng(string outputDirectory)
    {
        var jarPath = FindPlantUmlJar();
        if (jarPath is null)
            return -1;

        // Check java is available
        try
        {
            var javaCheck = Process.Start(new ProcessStartInfo("java", "-version")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });
            javaCheck?.WaitForExit(5000);
            if (javaCheck is null || javaCheck.ExitCode != 0)
                return -1;
        }
        catch
        {
            return -1;
        }

        var pumlFiles = Directory.GetFiles(outputDirectory, "*.puml", SearchOption.AllDirectories);
        var rendered = 0;

        foreach (var pumlFile in pumlFiles)
        {
            try
            {
                var dir = Path.GetDirectoryName(pumlFile)!;
                var process = Process.Start(new ProcessStartInfo("java", $"-jar \"{jarPath}\" -tpng \"{pumlFile}\"")
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = dir
                });

                process?.WaitForExit(30000);

                var pngPath = Path.ChangeExtension(pumlFile, ".png");
                if (File.Exists(pngPath))
                    rendered++;
            }
            catch
            {
                // Skip individual file failures
            }
        }

        return rendered;
    }

    /// <summary>
    /// Discovers the PlantUML JAR by checking env var, PATH, and common locations.
    /// </summary>
    internal static string? FindPlantUmlJar()
    {
        // 1. PLANTUML_JAR env var
        var envJar = Environment.GetEnvironmentVariable("PLANTUML_JAR");
        if (!string.IsNullOrEmpty(envJar) && File.Exists(envJar))
            return envJar;

        // 2. Common locations
        var candidates = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".dotnet", "tools", "plantuml.jar"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "plantuml.jar"),
            "/usr/local/bin/plantuml.jar",
            "/usr/share/plantuml/plantuml.jar",
        };

        foreach (var candidate in candidates)
        {
            if (File.Exists(candidate))
                return candidate;
        }

        // 3. Search PATH directories for plantuml.jar
        var pathDirs = Environment.GetEnvironmentVariable("PATH")?.Split(Path.PathSeparator) ?? [];
        foreach (var dir in pathDirs)
        {
            var jarInPath = Path.Combine(dir, "plantuml.jar");
            if (File.Exists(jarInPath))
                return jarInPath;
        }

        return null;
    }

    /// <summary>
    /// Represents a top-level type and its child members.
    /// </summary>
    internal record TypeGroup(ExportedSymbol Symbol, List<ExportedSymbol> Members);
}
