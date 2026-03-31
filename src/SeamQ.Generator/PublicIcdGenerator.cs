using System.Text;
using SeamQ.Core.Abstractions;
using SeamQ.Core.Models;
using SeamQ.Renderer.PlantUml;

namespace SeamQ.Generator;

/// <summary>
/// Generates detailed public Interface Control Documents for each project in a workspace.
/// Produces Markdown with overview, type descriptions, sequence/class/C4 diagrams.
/// </summary>
public class PublicIcdGenerator : IPublicIcdGenerator
{
    private static readonly HashSet<string> BarrelExportKinds = new(StringComparer.OrdinalIgnoreCase)
    {
        "NamedExport", "WildcardExport", "DefaultExport"
    };

    private static readonly HashSet<string> MemberKinds = new(StringComparer.OrdinalIgnoreCase)
    {
        "Method", "Property", "InputBinding", "Input Binding", "Input",
        "OutputBinding", "Output Binding", "Output",
        "SignalInput", "Signal Input", "ModelSignal", "Model Signal",
        "Observable"
    };

    private static readonly HashSet<string> TypeKinds = new(StringComparer.OrdinalIgnoreCase)
    {
        "Class", "AbstractClass", "Abstract Class", "Component",
        "Interface", "Injectable",
        "Enum", "Enumeration",
        "TypeAlias", "Type Alias", "Type",
        "InjectionToken", "Injection Token"
    };

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

            var symbols = GetDocumentableSymbols(project.Exports);
            if (symbols.Count == 0)
                continue;

            var projectDir = Path.Combine(outputDirectory, SanitizeFileName(project.Name));
            Directory.CreateDirectory(projectDir);

            var diagramsDir = Path.Combine(projectDir, "diagrams");
            Directory.CreateDirectory(diagramsDir);

            var typeGroups = BuildTypeGroups(symbols);
            var categorized = GroupByCategory(typeGroups);

            // Generate all diagrams
            var diagramFiles = await GenerateAllDiagramsAsync(
                project, workspace, typeGroups, categorized, diagramsDir, cancellationToken);
            generatedFiles.AddRange(diagramFiles);

            // Generate ICD.md
            var icdPath = Path.Combine(projectDir, "ICD.md");
            var markdown = GenerateIcd(project, workspace, typeGroups, categorized);
            await File.WriteAllTextAsync(icdPath, markdown, cancellationToken);
            generatedFiles.Add(icdPath);
        }

        return generatedFiles;
    }

    #region ICD Document Generation

    private static string GenerateIcd(
        Project project,
        Workspace workspace,
        List<TypeGroup> typeGroups,
        List<(string Category, List<TypeGroup> Groups)> categorized)
    {
        var sb = new StringBuilder();
        var projectKind = project.Type == ProjectType.Library ? "Library" : "Application";

        // Title
        sb.AppendLine($"# {project.Name} — Public Interface Control Document");
        sb.AppendLine();

        // Table of Contents
        sb.AppendLine("## Table of Contents");
        sb.AppendLine();
        sb.AppendLine("- [1. Overview](#1-overview)");
        sb.AppendLine("- [2. Type Descriptions](#2-type-descriptions)");
        foreach (var (category, _) in categorized)
        {
            var anchor = category.ToLowerInvariant().Replace(" ", "-");
            sb.AppendLine($"  - [{category}](#{anchor})");
        }
        sb.AppendLine("- [3. Sequence Diagrams](#3-sequence-diagrams)");
        sb.AppendLine("- [4. Class Diagrams](#4-class-diagrams)");
        sb.AppendLine("- [5. C4 Architecture Diagrams](#5-c4-architecture-diagrams)");
        sb.AppendLine();

        // 1. Overview
        WriteOverviewSection(sb, project, workspace, categorized);

        // 2. Type Descriptions
        WriteTypeDescriptions(sb, categorized);

        // 3. Sequence Diagrams
        WriteSequenceDiagramSection(sb, typeGroups, categorized);

        // 4. Class Diagrams
        WriteClassDiagramSection(sb, categorized);

        // 5. C4 Diagrams
        WriteC4DiagramSection(sb, project, workspace, categorized);

        return sb.ToString();
    }

    private static void WriteOverviewSection(
        StringBuilder sb,
        Project project,
        Workspace workspace,
        List<(string Category, List<TypeGroup> Groups)> categorized)
    {
        var projectKind = project.Type == ProjectType.Library ? "Library" : "Application";

        sb.AppendLine("## 1. Overview");
        sb.AppendLine();
        sb.AppendLine($"**Project:** {project.Name}");
        sb.AppendLine();
        sb.AppendLine($"**Type:** {projectKind}");
        sb.AppendLine();
        sb.AppendLine($"**Workspace:** {workspace.Alias} ({workspace.Type})");
        sb.AppendLine();
        sb.AppendLine($"**Source Root:** `{project.SourceRoot}`");
        sb.AppendLine();

        // Auto-generated description
        sb.AppendLine(GenerateProjectDescription(project, categorized));
        sb.AppendLine();

        // Summary table
        sb.AppendLine("### Public Surface Summary");
        sb.AppendLine();
        sb.AppendLine("| Category | Count |");
        sb.AppendLine("|----------|------:|");
        var total = 0;
        foreach (var (category, groups) in categorized)
        {
            sb.AppendLine($"| {category} | {groups.Count} |");
            total += groups.Count;
        }
        sb.AppendLine($"| **Total** | **{total}** |");
        sb.AppendLine();
    }

    private static string GenerateProjectDescription(
        Project project,
        List<(string Category, List<TypeGroup> Groups)> categorized)
    {
        var kinds = categorized.Select(c => c.Category).ToList();
        var sb = new StringBuilder();

        sb.Append($"**{project.Name}** is an Angular {(project.Type == ProjectType.Library ? "library" : "application")}");

        if (kinds.Contains("Components") && kinds.Contains("Services"))
            sb.Append(" providing UI components and injectable services");
        else if (kinds.Contains("Components"))
            sb.Append(" providing reusable UI components");
        else if (kinds.Contains("Services"))
            sb.Append(" providing injectable services");
        else if (kinds.Contains("Interfaces") || kinds.Contains("Type Aliases"))
            sb.Append(" defining shared type contracts");
        else
            sb.Append(" exporting public types");

        sb.Append(" for consumption by dependent projects.");
        return sb.ToString();
    }

    private static void WriteTypeDescriptions(
        StringBuilder sb,
        List<(string Category, List<TypeGroup> Groups)> categorized)
    {
        sb.AppendLine("## 2. Type Descriptions");
        sb.AppendLine();

        foreach (var (category, groups) in categorized)
        {
            sb.AppendLine($"### {category}");
            sb.AppendLine();

            foreach (var group in groups)
            {
                WriteTypeEntry(sb, group);
            }
        }
    }

    private static void WriteTypeEntry(StringBuilder sb, TypeGroup group)
    {
        var symbol = group.Symbol;
        var simpleName = GetSimpleName(symbol.Name);

        sb.AppendLine($"#### `{simpleName}`");
        sb.AppendLine();

        // Description
        var description = !string.IsNullOrWhiteSpace(symbol.Documentation)
            ? symbol.Documentation.Trim()
            : GenerateAutoDescription(symbol);
        sb.AppendLine(description);
        sb.AppendLine();

        // Metadata table
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

        // Members
        var properties = group.Members.Where(m => IsPropertyLike(m.Kind)).ToList();
        if (properties.Count > 0)
        {
            sb.AppendLine("**Members:**");
            sb.AppendLine();
            sb.AppendLine("| Name | Kind | Type | Description |");
            sb.AppendLine("|------|------|------|-------------|");
            foreach (var member in properties)
            {
                var memberName = GetMemberName(member);
                var kind = FormatMemberKind(member.Kind);
                var type = EscapePipe(!string.IsNullOrWhiteSpace(member.TypeSignature) ? $"`{member.TypeSignature}`" : "-");
                var doc = EscapePipe(!string.IsNullOrWhiteSpace(member.Documentation) ? member.Documentation.Trim() : GenerateAutoDescription(member));
                sb.AppendLine($"| {memberName} | {kind} | {type} | {doc} |");
            }
            sb.AppendLine();
        }

        // Methods
        var methods = group.Members.Where(m => m.Kind.Equals("Method", StringComparison.OrdinalIgnoreCase)).ToList();
        if (methods.Count > 0)
        {
            sb.AppendLine("**Methods:**");
            sb.AppendLine();
            sb.AppendLine("| Signature | Returns | Description |");
            sb.AppendLine("|-----------|---------|-------------|");
            foreach (var method in methods)
            {
                var (signature, returnType) = ParseMethodSignature(method);
                var doc = EscapePipe(!string.IsNullOrWhiteSpace(method.Documentation) ? method.Documentation.Trim() : GenerateAutoDescription(method));
                sb.AppendLine($"| `{EscapePipe(signature)}` | `{EscapePipe(returnType)}` | {doc} |");
            }
            sb.AppendLine();
        }

        sb.AppendLine("---");
        sb.AppendLine();
    }

    #endregion

    #region Sequence Diagrams

    private static void WriteSequenceDiagramSection(
        StringBuilder sb,
        List<TypeGroup> typeGroups,
        List<(string Category, List<TypeGroup> Groups)> categorized)
    {
        sb.AppendLine("## 3. Sequence Diagrams");
        sb.AppendLine();

        var hasServices = categorized.Any(c => c.Category is "Services");
        var hasComponents = categorized.Any(c => c.Category is "Components");
        var hasObservables = typeGroups.Any(g => g.Members.Any(m =>
            m.Kind.Equals("Observable", StringComparison.OrdinalIgnoreCase)));
        var hasHttpPatterns = typeGroups.Any(g => g.Members.Any(m =>
            m.TypeSignature != null && (m.TypeSignature.Contains("HttpClient") || m.TypeSignature.Contains("Observable<Http"))));

        if (hasServices)
        {
            sb.AppendLine("### Service Interaction");
            sb.AppendLine();
            sb.AppendLine("![Service Interaction](diagrams/seq-service-interaction.png)");
            sb.AppendLine();
            sb.AppendLine("[PlantUML source](diagrams/seq-service-interaction.puml)");
            sb.AppendLine();
        }

        if (hasComponents)
        {
            sb.AppendLine("### Component Lifecycle");
            sb.AppendLine();
            sb.AppendLine("![Component Lifecycle](diagrams/seq-component-lifecycle.png)");
            sb.AppendLine();
            sb.AppendLine("[PlantUML source](diagrams/seq-component-lifecycle.puml)");
            sb.AppendLine();
        }

        if (hasObservables)
        {
            sb.AppendLine("### Data Flow");
            sb.AppendLine();
            sb.AppendLine("![Data Flow](diagrams/seq-data-flow.png)");
            sb.AppendLine();
            sb.AppendLine("[PlantUML source](diagrams/seq-data-flow.puml)");
            sb.AppendLine();
        }

        if (hasHttpPatterns)
        {
            sb.AppendLine("### HTTP/API Interaction");
            sb.AppendLine();
            sb.AppendLine("![HTTP Interaction](diagrams/seq-http-interaction.png)");
            sb.AppendLine();
            sb.AppendLine("[PlantUML source](diagrams/seq-http-interaction.puml)");
            sb.AppendLine();
        }

        if (!hasServices && !hasComponents && !hasObservables && !hasHttpPatterns)
        {
            sb.AppendLine("_No behavioral patterns detected for sequence diagram generation._");
            sb.AppendLine();
        }
    }

    private static string GenerateServiceInteractionDiagram(List<(string Category, List<TypeGroup> Groups)> categorized)
    {
        var encoder = new PlantUmlEncoder();
        encoder.StartSequenceDiagram("Service Interaction");

        var services = categorized
            .Where(c => c.Category is "Services")
            .SelectMany(c => c.Groups)
            .ToList();

        if (services.Count == 0)
            return string.Empty;

        // Add participants
        encoder.AddParticipant("Consumer", "consumer");
        foreach (var svc in services.Take(8))
        {
            var name = GetSimpleName(svc.Symbol.Name);
            encoder.AddParticipant(name);
        }

        // Show method calls for each service
        foreach (var svc in services.Take(8))
        {
            var name = GetSimpleName(svc.Symbol.Name);
            var methods = svc.Members
                .Where(m => m.Kind.Equals("Method", StringComparison.OrdinalIgnoreCase))
                .Take(4)
                .ToList();

            if (methods.Count == 0) continue;

            encoder.AddBlankLine();
            encoder.AddRawLine($"== {name} ==");

            foreach (var method in methods)
            {
                var methodName = GetMemberName(method);
                var (_, returnType) = ParseMethodSignature(method);
                encoder.AddMessage("consumer", name, $"{methodName}()");
                encoder.AddActivation(name);
                encoder.AddMessage(name, "consumer", returnType, isReturn: true);
                encoder.AddDeactivation(name);
            }
        }

        encoder.EndDiagram();
        return encoder.Build();
    }

    private static string GenerateComponentLifecycleDiagram(List<(string Category, List<TypeGroup> Groups)> categorized)
    {
        var encoder = new PlantUmlEncoder();
        encoder.StartSequenceDiagram("Component Lifecycle");

        var components = categorized
            .Where(c => c.Category is "Components")
            .SelectMany(c => c.Groups)
            .Take(6)
            .ToList();

        if (components.Count == 0)
            return string.Empty;

        encoder.AddParticipant("Angular Framework", "framework");
        encoder.AddParticipant("Parent Component", "parent");

        foreach (var comp in components)
        {
            var name = GetSimpleName(comp.Symbol.Name);
            encoder.AddParticipant(name);
        }

        foreach (var comp in components)
        {
            var name = GetSimpleName(comp.Symbol.Name);
            var inputs = comp.Members.Where(m => IsInputKind(m.Kind)).Take(3).ToList();
            var outputs = comp.Members.Where(m => IsOutputKind(m.Kind)).Take(3).ToList();

            encoder.AddBlankLine();
            encoder.AddRawLine($"== {name} ==");

            // Instantiation
            encoder.AddMessage("framework", name, "constructor()");
            encoder.AddActivation(name);

            // Input bindings
            foreach (var input in inputs)
            {
                var memberName = GetMemberName(input);
                encoder.AddMessage("parent", name, $"[{memberName}] = value");
            }

            // Lifecycle
            encoder.AddMessage("framework", name, "ngOnInit()");

            // Output emissions
            foreach (var output in outputs)
            {
                var memberName = GetMemberName(output);
                encoder.AddMessage(name, "parent", $"({memberName}).emit()", isReturn: true);
            }

            encoder.AddDeactivation(name);
        }

        encoder.EndDiagram();
        return encoder.Build();
    }

    private static string GenerateDataFlowDiagram(List<TypeGroup> typeGroups)
    {
        var encoder = new PlantUmlEncoder();
        encoder.StartSequenceDiagram("Data Flow");

        encoder.AddParticipant("Consumer", "consumer");

        // Find types with observables
        var observableOwners = typeGroups
            .Where(g => g.Members.Any(m => m.Kind.Equals("Observable", StringComparison.OrdinalIgnoreCase)))
            .Take(6)
            .ToList();

        foreach (var owner in observableOwners)
        {
            encoder.AddParticipant(GetSimpleName(owner.Symbol.Name));
        }

        foreach (var owner in observableOwners)
        {
            var name = GetSimpleName(owner.Symbol.Name);
            var observables = owner.Members
                .Where(m => m.Kind.Equals("Observable", StringComparison.OrdinalIgnoreCase))
                .Take(3)
                .ToList();

            encoder.AddBlankLine();
            encoder.AddRawLine($"== {name} streams ==");

            foreach (var obs in observables)
            {
                var memberName = GetMemberName(obs);
                var type = obs.TypeSignature ?? "unknown";
                encoder.AddMessage("consumer", name, $"subscribe({memberName})");
                encoder.AddActivation(name);
                encoder.AddMessage(name, "consumer", $"emit({type})", isReturn: true);
                encoder.AddDeactivation(name);
            }
        }

        encoder.EndDiagram();
        return encoder.Build();
    }

    private static string GenerateHttpInteractionDiagram(List<TypeGroup> typeGroups)
    {
        var encoder = new PlantUmlEncoder();
        encoder.StartSequenceDiagram("HTTP/API Interaction");

        encoder.AddParticipant("Consumer", "consumer");
        encoder.AddParticipant("HTTP Service", "http_service");
        encoder.AddParticipant("Backend API", "api");

        var httpServices = typeGroups
            .Where(g => g.Members.Any(m =>
                m.TypeSignature != null && (
                    m.TypeSignature.Contains("HttpClient") ||
                    m.TypeSignature.Contains("Observable<Http"))))
            .Take(4)
            .ToList();

        foreach (var svc in httpServices)
        {
            var name = GetSimpleName(svc.Symbol.Name);
            var httpMethods = svc.Members
                .Where(m => m.Kind.Equals("Method", StringComparison.OrdinalIgnoreCase))
                .Take(4)
                .ToList();

            encoder.AddBlankLine();
            encoder.AddRawLine($"== {name} ==");

            foreach (var method in httpMethods)
            {
                var methodName = GetMemberName(method);
                var (_, returnType) = ParseMethodSignature(method);

                encoder.AddMessage("consumer", "http_service", $"{name}.{methodName}()");
                encoder.AddActivation("http_service");
                encoder.AddMessage("http_service", "api", "HTTP request");
                encoder.AddActivation("api");
                encoder.AddMessage("api", "http_service", "HTTP response", isReturn: true);
                encoder.AddDeactivation("api");
                encoder.AddMessage("http_service", "consumer", returnType, isReturn: true);
                encoder.AddDeactivation("http_service");
            }
        }

        encoder.EndDiagram();
        return encoder.Build();
    }

    #endregion

    #region Class Diagrams

    private static void WriteClassDiagramSection(
        StringBuilder sb,
        List<(string Category, List<TypeGroup> Groups)> categorized)
    {
        sb.AppendLine("## 4. Class Diagrams");
        sb.AppendLine();

        // Master class diagram
        sb.AppendLine("### Master Class Diagram");
        sb.AppendLine();
        sb.AppendLine("![Master Class Diagram](diagrams/class-master.png)");
        sb.AppendLine();
        sb.AppendLine("[PlantUML source](diagrams/class-master.puml)");
        sb.AppendLine();

        // Per-category diagrams
        foreach (var (category, groups) in categorized)
        {
            if (groups.Count < 2) continue;
            var slug = SanitizeFileName(category.ToLowerInvariant().Replace(" ", "-"));
            sb.AppendLine($"### {category} Class Diagram");
            sb.AppendLine();
            sb.AppendLine($"![{category} Diagram](diagrams/class-{slug}.png)");
            sb.AppendLine();
            sb.AppendLine($"[PlantUML source](diagrams/class-{slug}.puml)");
            sb.AppendLine();
        }
    }

    private static string GenerateMasterClassDiagram(
        List<(string Category, List<TypeGroup> Groups)> categorized)
    {
        var encoder = new PlantUmlEncoder();
        encoder.StartClassDiagram("Master Class Diagram");

        foreach (var (category, groups) in categorized)
        {
            encoder.AddRawLine($"package \"{category}\" {{");

            foreach (var group in groups)
            {
                AddTypeToClassDiagram(encoder, group);
            }

            encoder.AddRawLine("}");
            encoder.AddBlankLine();
        }

        // Relationships
        foreach (var (_, groups) in categorized)
        {
            foreach (var group in groups)
            {
                AddRelationships(encoder, group);
            }
        }

        encoder.EndDiagram();
        return encoder.Build();
    }

    private static string GenerateCategoryClassDiagram(string category, List<TypeGroup> groups)
    {
        var encoder = new PlantUmlEncoder();
        encoder.StartClassDiagram($"{category} Class Diagram");

        foreach (var group in groups)
        {
            AddTypeToClassDiagram(encoder, group);
        }

        foreach (var group in groups)
        {
            AddRelationships(encoder, group);
        }

        encoder.EndDiagram();
        return encoder.Build();
    }

    private static void AddTypeToClassDiagram(PlantUmlEncoder encoder, TypeGroup group)
    {
        var simpleName = GetSimpleName(group.Symbol.Name);
        var kindLower = group.Symbol.Kind.ToLowerInvariant().Replace(" ", "");
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
    }

    private static void AddRelationships(PlantUmlEncoder encoder, TypeGroup group)
    {
        var simpleName = GetSimpleName(group.Symbol.Name);

        if (!string.IsNullOrWhiteSpace(group.Symbol.TypeSignature))
        {
            var typeSig = group.Symbol.TypeSignature;

            if (typeSig.Contains("extends ", StringComparison.OrdinalIgnoreCase))
            {
                var ext = ExtractAfterKeyword(typeSig, "extends");
                if (ext != null)
                    encoder.AddRelationship(simpleName, ext, "--|>", "extends");
            }

            if (typeSig.Contains("implements ", StringComparison.OrdinalIgnoreCase))
            {
                var impl = ExtractAfterKeyword(typeSig, "implements");
                if (impl != null)
                {
                    foreach (var iface in impl.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
                    {
                        encoder.AddRelationship(simpleName, iface.Trim(), "..|>", "implements");
                    }
                }
            }
        }

        // Dependency arrows for injected services
        foreach (var member in group.Members)
        {
            if (!string.IsNullOrWhiteSpace(member.TypeSignature) &&
                member.Kind.Equals("Property", StringComparison.OrdinalIgnoreCase) &&
                member.TypeSignature.EndsWith("Service", StringComparison.OrdinalIgnoreCase))
            {
                encoder.AddRelationship(simpleName, member.TypeSignature, "-->", "depends");
            }
        }
    }

    #endregion

    #region C4 Diagrams

    private static void WriteC4DiagramSection(
        StringBuilder sb,
        Project project,
        Workspace workspace,
        List<(string Category, List<TypeGroup> Groups)> categorized)
    {
        sb.AppendLine("## 5. C4 Architecture Diagrams");
        sb.AppendLine();

        sb.AppendLine("### System Context");
        sb.AppendLine();
        sb.AppendLine("![System Context](diagrams/c4-system-context.png)");
        sb.AppendLine();
        sb.AppendLine("[PlantUML source](diagrams/c4-system-context.puml)");
        sb.AppendLine();

        sb.AppendLine("### Container");
        sb.AppendLine();
        sb.AppendLine("![Container](diagrams/c4-container.png)");
        sb.AppendLine();
        sb.AppendLine("[PlantUML source](diagrams/c4-container.puml)");
        sb.AppendLine();

        sb.AppendLine("### Component");
        sb.AppendLine();
        sb.AppendLine("![Component](diagrams/c4-component.png)");
        sb.AppendLine();
        sb.AppendLine("[PlantUML source](diagrams/c4-component.puml)");
        sb.AppendLine();

        sb.AppendLine("### Code");
        sb.AppendLine();
        sb.AppendLine("![Code](diagrams/c4-code.png)");
        sb.AppendLine();
        sb.AppendLine("[PlantUML source](diagrams/c4-code.puml)");
        sb.AppendLine();
    }

    private static string GenerateC4SystemContextDiagram(Project project, Workspace workspace)
    {
        var encoder = new PlantUmlEncoder();
        encoder.StartClassDiagram($"C4 System Context — {project.Name}");

        encoder.AddRawLine("skinparam rectangle {");
        encoder.AddRawLine("  BackgroundColor<<system>> #438DD5");
        encoder.AddRawLine("  FontColor<<system>> white");
        encoder.AddRawLine("  BackgroundColor<<external>> #999999");
        encoder.AddRawLine("  FontColor<<external>> white");
        encoder.AddRawLine("}");
        encoder.AddBlankLine();

        // The project as the main system
        encoder.AddRawLine($"rectangle \"{project.Name}\\n[{(project.Type == ProjectType.Library ? "Library" : "Application")}]\" as system <<system>>");
        encoder.AddBlankLine();

        // Other projects in workspace as external
        foreach (var other in workspace.Projects.Where(p => p.Name != project.Name))
        {
            var sanitized = SanitizePlantUmlName(other.Name);
            var kind = other.Type == ProjectType.Library ? "Library" : "App";
            encoder.AddRawLine($"rectangle \"{other.Name}\\n[{kind}]\" as {sanitized} <<external>>");
        }

        encoder.AddBlankLine();

        // Relationships
        foreach (var other in workspace.Projects.Where(p => p.Name != project.Name))
        {
            var sanitized = SanitizePlantUmlName(other.Name);
            encoder.AddRawLine($"{sanitized} --> system : uses");
        }

        encoder.EndDiagram();
        return encoder.Build();
    }

    private static string GenerateC4ContainerDiagram(Project project, Workspace workspace)
    {
        var encoder = new PlantUmlEncoder();
        encoder.StartClassDiagram($"C4 Container — {workspace.Alias}");

        encoder.AddRawLine("skinparam rectangle {");
        encoder.AddRawLine("  BackgroundColor<<container>> #438DD5");
        encoder.AddRawLine("  FontColor<<container>> white");
        encoder.AddRawLine("  BackgroundColor<<boundary>> #FFFFFF");
        encoder.AddRawLine("  BorderColor<<boundary>> #444444");
        encoder.AddRawLine("}");
        encoder.AddBlankLine();

        encoder.AddRawLine($"rectangle \"{workspace.Alias} Workspace\" as boundary <<boundary>> {{");

        foreach (var proj in workspace.Projects)
        {
            var sanitized = SanitizePlantUmlName(proj.Name);
            var kind = proj.Type == ProjectType.Library ? "Library" : "Application";
            var exports = proj.Exports.Count;
            encoder.AddRawLine($"  rectangle \"{proj.Name}\\n[{kind}, {exports} exports]\" as {sanitized} <<container>>");
        }

        encoder.AddRawLine("}");
        encoder.AddBlankLine();

        encoder.EndDiagram();
        return encoder.Build();
    }

    private static string GenerateC4ComponentDiagram(
        Project project,
        List<(string Category, List<TypeGroup> Groups)> categorized)
    {
        var encoder = new PlantUmlEncoder();
        encoder.StartClassDiagram($"C4 Component — {project.Name}");

        encoder.AddRawLine("skinparam rectangle {");
        encoder.AddRawLine("  BackgroundColor<<component>> #85BBF0");
        encoder.AddRawLine("  FontColor<<component>> black");
        encoder.AddRawLine("  BackgroundColor<<boundary>> #FFFFFF");
        encoder.AddRawLine("  BorderColor<<boundary>> #444444");
        encoder.AddRawLine("}");
        encoder.AddBlankLine();

        encoder.AddRawLine($"rectangle \"{project.Name}\" as boundary <<boundary>> {{");

        foreach (var (category, groups) in categorized)
        {
            var sanitized = SanitizePlantUmlName(category);
            encoder.AddRawLine($"  rectangle \"{category}\\n[{groups.Count} types]\" as {sanitized} <<component>>");
        }

        encoder.AddRawLine("}");
        encoder.AddBlankLine();

        // Relationships between component categories
        var categoryNames = categorized.Select(c => c.Category).ToList();
        if (categoryNames.Contains("Components") && categoryNames.Contains("Services"))
        {
            encoder.AddRawLine($"{SanitizePlantUmlName("Components")} --> {SanitizePlantUmlName("Services")} : injects");
        }
        if (categoryNames.Contains("Services") && categoryNames.Contains("Interfaces"))
        {
            encoder.AddRawLine($"{SanitizePlantUmlName("Services")} ..|> {SanitizePlantUmlName("Interfaces")} : implements");
        }

        encoder.EndDiagram();
        return encoder.Build();
    }

    private static string GenerateC4CodeDiagram(List<(string Category, List<TypeGroup> Groups)> categorized)
    {
        // Code level = detailed class diagram (same as master)
        return GenerateMasterClassDiagram(categorized);
    }

    #endregion

    #region Diagram File Generation

    private static async Task<List<string>> GenerateAllDiagramsAsync(
        Project project,
        Workspace workspace,
        List<TypeGroup> typeGroups,
        List<(string Category, List<TypeGroup> Groups)> categorized,
        string diagramsDir,
        CancellationToken cancellationToken)
    {
        var files = new List<string>();

        // Sequence diagrams
        var hasServices = categorized.Any(c => c.Category is "Services");
        var hasComponents = categorized.Any(c => c.Category is "Components");
        var hasObservables = typeGroups.Any(g => g.Members.Any(m =>
            m.Kind.Equals("Observable", StringComparison.OrdinalIgnoreCase)));
        var hasHttpPatterns = typeGroups.Any(g => g.Members.Any(m =>
            m.TypeSignature != null && (m.TypeSignature.Contains("HttpClient") || m.TypeSignature.Contains("Observable<Http"))));

        if (hasServices)
        {
            var content = GenerateServiceInteractionDiagram(categorized);
            if (!string.IsNullOrEmpty(content))
            {
                var path = Path.Combine(diagramsDir, "seq-service-interaction.puml");
                await File.WriteAllTextAsync(path, content, cancellationToken);
                files.Add(path);
            }
        }

        if (hasComponents)
        {
            var content = GenerateComponentLifecycleDiagram(categorized);
            if (!string.IsNullOrEmpty(content))
            {
                var path = Path.Combine(diagramsDir, "seq-component-lifecycle.puml");
                await File.WriteAllTextAsync(path, content, cancellationToken);
                files.Add(path);
            }
        }

        if (hasObservables)
        {
            var content = GenerateDataFlowDiagram(typeGroups);
            if (!string.IsNullOrEmpty(content))
            {
                var path = Path.Combine(diagramsDir, "seq-data-flow.puml");
                await File.WriteAllTextAsync(path, content, cancellationToken);
                files.Add(path);
            }
        }

        if (hasHttpPatterns)
        {
            var content = GenerateHttpInteractionDiagram(typeGroups);
            if (!string.IsNullOrEmpty(content))
            {
                var path = Path.Combine(diagramsDir, "seq-http-interaction.puml");
                await File.WriteAllTextAsync(path, content, cancellationToken);
                files.Add(path);
            }
        }

        // Class diagrams
        var masterContent = GenerateMasterClassDiagram(categorized);
        var masterPath = Path.Combine(diagramsDir, "class-master.puml");
        await File.WriteAllTextAsync(masterPath, masterContent, cancellationToken);
        files.Add(masterPath);

        foreach (var (category, groups) in categorized)
        {
            if (groups.Count < 2) continue;
            var slug = SanitizeFileName(category.ToLowerInvariant().Replace(" ", "-"));
            var content = GenerateCategoryClassDiagram(category, groups);
            var path = Path.Combine(diagramsDir, $"class-{slug}.puml");
            await File.WriteAllTextAsync(path, content, cancellationToken);
            files.Add(path);
        }

        // C4 diagrams
        var c4SystemContext = GenerateC4SystemContextDiagram(project, workspace);
        var c4SystemContextPath = Path.Combine(diagramsDir, "c4-system-context.puml");
        await File.WriteAllTextAsync(c4SystemContextPath, c4SystemContext, cancellationToken);
        files.Add(c4SystemContextPath);

        var c4Container = GenerateC4ContainerDiagram(project, workspace);
        var c4ContainerPath = Path.Combine(diagramsDir, "c4-container.puml");
        await File.WriteAllTextAsync(c4ContainerPath, c4Container, cancellationToken);
        files.Add(c4ContainerPath);

        var c4Component = GenerateC4ComponentDiagram(project, categorized);
        var c4ComponentPath = Path.Combine(diagramsDir, "c4-component.puml");
        await File.WriteAllTextAsync(c4ComponentPath, c4Component, cancellationToken);
        files.Add(c4ComponentPath);

        var c4Code = GenerateC4CodeDiagram(categorized);
        var c4CodePath = Path.Combine(diagramsDir, "c4-code.puml");
        await File.WriteAllTextAsync(c4CodePath, c4Code, cancellationToken);
        files.Add(c4CodePath);

        return files;
    }

    #endregion

    #region Utilities

    private static List<ExportedSymbol> GetDocumentableSymbols(IReadOnlyList<ExportedSymbol> exports)
    {
        var result = new List<ExportedSymbol>();
        foreach (var symbol in exports)
        {
            if (BarrelExportKinds.Contains(symbol.Kind)) continue;
            if (symbol.Name.StartsWith('_') || symbol.Name.StartsWith('#')) continue;

            var memberName = symbol.Name;
            var dotIndex = symbol.Name.LastIndexOf('.');
            if (dotIndex >= 0 && dotIndex < symbol.Name.Length - 1)
                memberName = symbol.Name[(dotIndex + 1)..];
            if (memberName.StartsWith('_') || memberName.StartsWith('#')) continue;

            var isBinding = IsInputKind(symbol.Kind) || IsOutputKind(symbol.Kind);
            if (!isBinding && IsPrivateOrProtected(symbol)) continue;

            result.Add(symbol);
        }
        return result;
    }

    private static List<TypeGroup> BuildTypeGroups(List<ExportedSymbol> symbols)
    {
        var topLevel = new List<ExportedSymbol>();
        var children = new List<ExportedSymbol>();

        foreach (var symbol in symbols)
        {
            if (!string.IsNullOrEmpty(symbol.ParentName) && MemberKinds.Contains(symbol.Kind))
                children.Add(symbol);
            else if (TypeKinds.Contains(symbol.Kind))
                topLevel.Add(symbol);
            else if (string.IsNullOrEmpty(symbol.ParentName))
                topLevel.Add(symbol);
            else
                children.Add(symbol);
        }

        var groups = new List<TypeGroup>();
        foreach (var parent in topLevel.OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase))
        {
            var members = children
                .Where(c => string.Equals(c.ParentName, parent.Name, StringComparison.OrdinalIgnoreCase)
                             || (c.Name.Contains('.') && c.Name.StartsWith(parent.Name + ".", StringComparison.OrdinalIgnoreCase)))
                .OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
            groups.Add(new TypeGroup(parent, members));
        }

        var assigned = groups.SelectMany(g => g.Members).ToHashSet();
        foreach (var orphan in children.Where(c => !assigned.Contains(c)))
        {
            groups.Add(new TypeGroup(orphan, []));
        }

        return groups;
    }

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
            "Components", "Services", "Classes", "Abstract Classes", "Interfaces",
            "Enumerations", "Type Aliases", "Injection Tokens",
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
        foreach (var (category, items) in groups.OrderBy(g => g.Key))
        {
            result.Add((category, items));
        }
        return result;
    }

    private static List<string> BuildDiagramMembers(TypeGroup group)
    {
        var members = new List<string>();
        var kindLower = group.Symbol.Kind.ToLowerInvariant().Replace(" ", "");

        if (kindLower is "enum" or "enumeration")
        {
            foreach (var member in group.Members)
                members.Add(GetMemberName(member));
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

    private static string GenerateAutoDescription(ExportedSymbol symbol)
    {
        var kind = symbol.Kind.ToLowerInvariant().Replace(" ", "");
        return kind switch
        {
            "component" => "Angular component",
            "injectable" => "Injectable service",
            "interface" => $"Interface defining the shape of {GetSimpleName(symbol.Name)}",
            "enum" or "enumeration" => $"Enumeration defining {GetSimpleName(symbol.Name)} values",
            "typealias" or "type" => !string.IsNullOrWhiteSpace(symbol.TypeSignature) && !symbol.TypeSignature.StartsWith('{')
                ? $"Type alias for {symbol.TypeSignature}"
                : $"Data type defining the shape of {GetSimpleName(symbol.Name)}",
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
            "class" or "abstractclass" => $"Class {GetSimpleName(symbol.Name)}",
            _ => $"{symbol.Kind} {GetSimpleName(symbol.Name)}"
        };
    }

    private static string BuildMethodDescription(ExportedSymbol symbol)
    {
        if (string.IsNullOrWhiteSpace(symbol.TypeSignature)) return "-";
        var arrowIndex = symbol.TypeSignature.IndexOf("=>", StringComparison.Ordinal);
        if (arrowIndex >= 0)
        {
            var returnPart = symbol.TypeSignature[(arrowIndex + 2)..].Trim();
            return string.IsNullOrWhiteSpace(returnPart) ? "-" : $"Returns {returnPart}";
        }
        return "-";
    }

    private static (string Signature, string ReturnType) ParseMethodSignature(ExportedSymbol method)
    {
        var name = GetMemberName(method);
        if (string.IsNullOrWhiteSpace(method.TypeSignature))
            return ($"{name}()", "void");

        var typeSig = method.TypeSignature;

        var arrowIndex = typeSig.IndexOf("=>", StringComparison.Ordinal);
        if (arrowIndex >= 0)
        {
            var paramsPart = typeSig[..arrowIndex].Trim().Trim('(', ')').Trim();
            var returnPart = typeSig[(arrowIndex + 2)..].Trim();
            return ($"{name}({paramsPart})", returnPart);
        }

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

    private static string? ExtractAfterKeyword(string text, string keyword)
    {
        var idx = text.IndexOf(keyword, StringComparison.OrdinalIgnoreCase);
        if (idx < 0) return null;
        var rest = text[(idx + keyword.Length)..].Trim();
        var nextKeyword = -1;
        var keywords = new[] { "extends", "implements", "{" };
        foreach (var kw in keywords)
        {
            if (string.Equals(kw, keyword, StringComparison.OrdinalIgnoreCase)) continue;
            var kwIdx = rest.IndexOf(kw, StringComparison.OrdinalIgnoreCase);
            if (kwIdx >= 0 && (nextKeyword < 0 || kwIdx < nextKeyword))
                nextKeyword = kwIdx;
        }
        return nextKeyword >= 0 ? rest[..nextKeyword].Trim() : rest.Trim();
    }

    private static string FormatMemberKind(string kind) => kind.ToLowerInvariant().Replace(" ", "") switch
    {
        "inputbinding" or "input" => "Input",
        "outputbinding" or "output" => "Output",
        "signalinput" or "modelsignal" => "Signal",
        "observable" => "Observable",
        "property" => "Property",
        _ => kind
    };

    private static bool IsPropertyLike(string kind) =>
        kind.Equals("Property", StringComparison.OrdinalIgnoreCase) ||
        IsInputKind(kind) || IsOutputKind(kind) ||
        kind.Equals("Observable", StringComparison.OrdinalIgnoreCase);

    private static bool IsInputKind(string kind) =>
        kind.Equals("InputBinding", StringComparison.OrdinalIgnoreCase) ||
        kind.Equals("Input Binding", StringComparison.OrdinalIgnoreCase) ||
        kind.Equals("Input", StringComparison.OrdinalIgnoreCase) ||
        kind.Equals("SignalInput", StringComparison.OrdinalIgnoreCase) ||
        kind.Equals("Signal Input", StringComparison.OrdinalIgnoreCase) ||
        kind.Equals("ModelSignal", StringComparison.OrdinalIgnoreCase) ||
        kind.Equals("Model Signal", StringComparison.OrdinalIgnoreCase);

    private static bool IsOutputKind(string kind) =>
        kind.Equals("OutputBinding", StringComparison.OrdinalIgnoreCase) ||
        kind.Equals("Output Binding", StringComparison.OrdinalIgnoreCase) ||
        kind.Equals("Output", StringComparison.OrdinalIgnoreCase);

    private static bool IsPrivateOrProtected(ExportedSymbol symbol)
    {
        if (symbol.Documentation != null)
        {
            var docLower = symbol.Documentation.ToLowerInvariant();
            if (docLower.Contains("@private") || docLower.Contains("@protected"))
                return true;
        }
        return false;
    }

    private static string GetSimpleName(string name)
    {
        var dotIndex = name.LastIndexOf('.');
        return dotIndex >= 0 && dotIndex < name.Length - 1 ? name[(dotIndex + 1)..] : name;
    }

    private static string GetMemberName(ExportedSymbol member)
    {
        var name = member.Name;
        if (!string.IsNullOrEmpty(member.ParentName) && name.StartsWith(member.ParentName + ".", StringComparison.OrdinalIgnoreCase))
            name = name[(member.ParentName.Length + 1)..];
        return name;
    }

    private static string EscapePipe(string text) => text.Replace("|", "\\|");

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sb = new StringBuilder(name.Length);
        foreach (var c in name)
            sb.Append(invalid.Contains(c) ? '-' : c);
        return sb.ToString();
    }

    private static string SanitizePlantUmlName(string name) =>
        name.Replace('<', '_').Replace('>', '_').Replace(' ', '_').Replace('-', '_').Replace('/', '_').Replace('@', '_');

    internal record TypeGroup(ExportedSymbol Symbol, List<ExportedSymbol> Members);

    #endregion
}
