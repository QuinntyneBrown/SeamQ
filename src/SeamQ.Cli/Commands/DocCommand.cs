using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using SeamQ.Cli.Rendering;
using SeamQ.Core.Abstractions;
using SeamQ.Core.Configuration;
using ExitCodes = SeamQ.Core.Models.ExitCodes;

namespace SeamQ.Cli.Commands;

/// <summary>
/// CLI command that generates comprehensive API reference documentation for Angular workspaces.
/// Creates per-project folders with README.md and PlantUML class diagrams.
/// </summary>
public static class DocCommand
{
    public static Command Create(IServiceProvider serviceProvider)
    {
        var pathsArgument = new Argument<string[]>("paths", "Workspace paths to document")
        {
            Arity = ArgumentArity.ZeroOrMore
        };

        var command = new Command("doc", "Generate API reference documentation for Angular projects")
        {
            pathsArgument
        };

        command.SetHandler(async (paths) =>
        {
            var renderer = serviceProvider.GetRequiredService<IConsoleRenderer>();
            var scanner = serviceProvider.GetRequiredService<IWorkspaceScanner>();
            var generator = serviceProvider.GetRequiredService<IDocGenerator>();
            var config = serviceProvider.GetRequiredService<SeamQConfig>();
            var globalContext = serviceProvider.GetRequiredService<GlobalContext>();

            // Determine workspace paths: use CLI args first, then config
            var workspacePaths = paths.Length > 0
                ? paths
                : config.Workspaces.Select(w => w.Path).ToArray();

            if (workspacePaths.Length == 0)
            {
                renderer.WriteError("No workspace paths provided. Pass paths as arguments or configure them in seamq.config.json.");
                Environment.ExitCode = ExitCodes.FatalError;
                return;
            }

            var outputDir = Path.GetFullPath(globalContext.OutputDir ?? config.Output.Directory);

            try
            {
                var totalFiles = 0;

                foreach (var wsPath in workspacePaths)
                {
                    var fullPath = Path.GetFullPath(wsPath);
                    var workspace = await scanner.ScanAsync(fullPath);

                    renderer.WriteSuccess($"scanned {workspace.Alias} ({workspace.Projects.Count} projects)");

                    var files = await generator.GenerateAsync(workspace, outputDir);
                    totalFiles += files.Count;

                    foreach (var file in files)
                    {
                        renderer.WriteMuted($"  {Path.GetRelativePath(Directory.GetCurrentDirectory(), file)}");
                    }
                }

                renderer.WriteLine();
                renderer.WriteInfo($"generated {totalFiles} doc file(s) in {outputDir}");
            }
            catch (DirectoryNotFoundException ex)
            {
                renderer.WriteError($"Directory not found: {ex.Message}");
                Environment.ExitCode = ExitCodes.FatalError;
            }
            catch (IOException ex)
            {
                renderer.WriteError($"I/O error: {ex.Message}");
                Environment.ExitCode = ExitCodes.FatalError;
            }
            catch (UnauthorizedAccessException ex)
            {
                renderer.WriteError($"Access denied: {ex.Message}");
                Environment.ExitCode = ExitCodes.FatalError;
            }
        }, pathsArgument);

        return command;
    }
}
