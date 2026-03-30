using Microsoft.Extensions.Logging;
using SeamQ.Core.Abstractions;
using SeamQ.Core.Models;
using SeamQ.Scanner.Parsing;
using SeamQ.Scanner.TypeScript;

namespace SeamQ.Scanner;

/// <summary>
/// Orchestrates scanning of an Angular/Nx workspace to produce a complete <see cref="Workspace"/> model.
/// Detects workspace type, parses config files, resolves TS paths, discovers projects,
/// parses barrel exports, and extracts TypeScript declarations with Angular metadata.
/// </summary>
public class WorkspaceScanner : IWorkspaceScanner
{
    private readonly AngularWorkspaceParser _angularParser;
    private readonly NxWorkspaceParser _nxParser;
    private readonly TsConfigResolver _tsConfigResolver;
    private readonly BarrelExportParser _barrelExportParser;
    private readonly TypeScriptAstParser _typeScriptParser;
    private readonly AngularMetadataExtractor _metadataExtractor;
    private readonly ILogger<WorkspaceScanner> _logger;

    public WorkspaceScanner(
        AngularWorkspaceParser angularParser,
        NxWorkspaceParser nxParser,
        TsConfigResolver tsConfigResolver,
        BarrelExportParser barrelExportParser,
        TypeScriptAstParser typeScriptParser,
        AngularMetadataExtractor metadataExtractor,
        ILogger<WorkspaceScanner> logger)
    {
        _angularParser = angularParser;
        _nxParser = nxParser;
        _tsConfigResolver = tsConfigResolver;
        _barrelExportParser = barrelExportParser;
        _typeScriptParser = typeScriptParser;
        _metadataExtractor = metadataExtractor;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Workspace> ScanAsync(string workspacePath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspacePath);

        var fullPath = Path.GetFullPath(workspacePath);
        if (!Directory.Exists(fullPath))
        {
            throw new DirectoryNotFoundException($"Workspace directory not found: {fullPath}");
        }

        _logger.LogInformation("Scanning workspace at {Path}", fullPath);

        // Step 1: Detect workspace type
        var workspaceType = WorkspaceTypeDetector.Detect(fullPath);
        _logger.LogInformation("Detected workspace type: {Type}", workspaceType);

        // Step 2: Resolve TS config paths
        var tsConfig = await _tsConfigResolver.ResolveAsync(fullPath, cancellationToken);
        _logger.LogDebug("Resolved {Count} path aliases from tsconfig", tsConfig.Paths.Count);

        // Step 3: Discover projects based on workspace type
        var rawProjects = await DiscoverProjectsAsync(fullPath, workspaceType, cancellationToken);
        _logger.LogInformation("Discovered {Count} projects", rawProjects.Count);

        // Step 4: For each project, parse barrel exports and TypeScript source files
        var enrichedProjects = new List<Project>();
        var allWorkspaceExports = new List<ExportedSymbol>();

        foreach (var project in rawProjects)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var enriched = await EnrichProjectAsync(project, cancellationToken);
                enrichedProjects.Add(enriched);
                allWorkspaceExports.AddRange(enriched.Exports);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to enrich project {Name}, using raw project", project.Name);
                enrichedProjects.Add(project);
            }
        }

        // Step 5: Determine workspace alias and role
        var alias = DeriveAlias(fullPath);
        var role = DeriveRole(enrichedProjects, workspaceType);

        return new Workspace
        {
            Path = fullPath,
            Alias = alias,
            Type = workspaceType,
            Role = role,
            Projects = enrichedProjects,
            Exports = allWorkspaceExports
        };
    }

    private async Task<IReadOnlyList<Project>> DiscoverProjectsAsync(
        string workspacePath,
        WorkspaceType workspaceType,
        CancellationToken cancellationToken)
    {
        return workspaceType switch
        {
            WorkspaceType.NxMonorepo => await _nxParser.ParseAsync(workspacePath, cancellationToken),
            WorkspaceType.AngularCli => await _angularParser.ParseAsync(workspacePath, cancellationToken),
            WorkspaceType.Standalone => DiscoverStandaloneProject(workspacePath),
            _ => DiscoverStandaloneProject(workspacePath)
        };
    }

    private static IReadOnlyList<Project> DiscoverStandaloneProject(string workspacePath)
    {
        var srcDir = Path.Combine(workspacePath, "src");
        var sourceRoot = Directory.Exists(srcDir) ? srcDir : workspacePath;

        return
        [
            new Project
            {
                Name = Path.GetFileName(workspacePath),
                Type = ProjectType.Application,
                SourceRoot = sourceRoot
            }
        ];
    }

    private async Task<Project> EnrichProjectAsync(Project project, CancellationToken cancellationToken)
    {
        if (!Directory.Exists(project.SourceRoot))
        {
            _logger.LogDebug("Source root does not exist for project {Name}: {Root}",
                project.Name, project.SourceRoot);
            return project;
        }

        // Parse barrel exports
        var barrelExports = await _barrelExportParser.ParseBarrelExportsAsync(
            project.SourceRoot, cancellationToken);

        // Parse TypeScript source files
        var tsFiles = GetTypeScriptFiles(project.SourceRoot);
        var allDeclarations = new List<ParsedDeclaration>();

        foreach (var tsFile in tsFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var declarations = await _typeScriptParser.ParseFileAsync(tsFile, cancellationToken);
            allDeclarations.AddRange(declarations);
        }

        // Extract Angular metadata into ExportedSymbol format
        var angularSymbols = _metadataExtractor.ExtractSymbols(allDeclarations);

        // Merge barrel exports with parsed symbols (barrel exports first as they define public API)
        var allExports = new List<ExportedSymbol>(barrelExports);

        // Add Angular symbols that aren't already captured by barrel exports
        var barrelNames = barrelExports.Select(e => e.Name).ToHashSet();
        foreach (var symbol in angularSymbols)
        {
            if (!barrelNames.Contains(symbol.Name))
            {
                allExports.Add(symbol);
            }
        }

        return project with { Exports = allExports };
    }

    private static string[] GetTypeScriptFiles(string sourceRoot)
    {
        try
        {
            return Directory.GetFiles(sourceRoot, "*.ts", SearchOption.AllDirectories)
                .Where(f => !f.EndsWith(".spec.ts", StringComparison.OrdinalIgnoreCase) &&
                            !f.EndsWith(".test.ts", StringComparison.OrdinalIgnoreCase) &&
                            !f.EndsWith(".d.ts", StringComparison.OrdinalIgnoreCase))
                .ToArray();
        }
        catch
        {
            return [];
        }
    }

    private static string DeriveAlias(string workspacePath)
    {
        return Path.GetFileName(workspacePath);
    }

    private static WorkspaceRole DeriveRole(IReadOnlyList<Project> projects, WorkspaceType workspaceType)
    {
        var hasApps = projects.Any(p => p.Type == ProjectType.Application);
        var hasLibs = projects.Any(p => p.Type == ProjectType.Library);

        if (hasApps && hasLibs)
        {
            return WorkspaceRole.Framework;
        }

        if (hasApps)
        {
            return WorkspaceRole.Application;
        }

        if (hasLibs)
        {
            return WorkspaceRole.Library;
        }

        return WorkspaceRole.Plugin;
    }
}
