using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using SeamQ.Cli.Rendering;
using SeamQ.Core.Abstractions;
using SeamQ.Core.Configuration;
using SeamQ.Core.Models;
using SeamQ.Detector;
using ExitCodes = SeamQ.Core.Models.ExitCodes;

namespace SeamQ.Cli.Commands;

public static class ScanCommand
{
    public static Command Create(IServiceProvider serviceProvider)
    {
        var pathsArgument = new Argument<string[]>("paths", "One or more workspace root paths")
        {
            Arity = ArgumentArity.ZeroOrMore
        };
        var saveBaselineOption = new Option<string?>("--save-baseline", "Save scan result as baseline JSON");
        var noCacheOption = new Option<bool>("--no-cache", "Disable AST caching");
        var excludeOption = new Option<string[]>("--exclude", "Glob patterns to exclude paths")
        {
            Arity = ArgumentArity.ZeroOrMore
        };

        var command = new Command("scan", "Scan workspaces and build the seam registry")
        {
            pathsArgument,
            saveBaselineOption,
            noCacheOption,
            excludeOption
        };

        command.SetHandler(async (paths, saveBaseline, noCache, exclude) =>
        {
            var renderer = serviceProvider.GetRequiredService<IConsoleRenderer>();
            var scanner = serviceProvider.GetRequiredService<IWorkspaceScanner>();
            var detector = serviceProvider.GetRequiredService<ISeamDetector>();
            var registry = serviceProvider.GetRequiredService<SeamRegistry>();
            var config = serviceProvider.GetRequiredService<SeamQConfig>();

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

            try
            {
                // Scan each workspace
                var workspaces = new List<Workspace>();
                foreach (var wsPath in workspacePaths)
                {
                    var fullPath = Path.GetFullPath(wsPath);
                    var workspace = await scanner.ScanAsync(fullPath);
                    workspaces.Add(workspace);
                    renderer.WriteSuccess($"scanned {workspace.Alias} ({workspace.Projects.Count} projects, {workspace.Exports.Count} exports)");
                }

                // Detect seams
                var seams = await detector.DetectAsync(workspaces);
                registry.RegisterAll(seams);

                // Persist registry to disk so other commands can load it
                var registryPath = SeamRegistry.DefaultRegistryPath;
                await registry.SaveToFileAsync(registryPath);

                renderer.WriteLine();
                renderer.WriteInfo($"found {seams.Count} seams across {workspaces.Count} workspaces.");
                renderer.WriteMuted("run 'seamq list' to view detected seams.");

                // Save baseline if requested
                if (saveBaseline is not null)
                {
                    var json = System.Text.Json.JsonSerializer.Serialize(seams, new System.Text.Json.JsonSerializerOptions
                    {
                        WriteIndented = true,
                        PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
                        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
                    });
                    var baselinePath = Path.GetFullPath(saveBaseline);
                    Directory.CreateDirectory(Path.GetDirectoryName(baselinePath)!);
                    await File.WriteAllTextAsync(baselinePath, json);
                    renderer.WriteMuted($"baseline saved to {baselinePath}");
                }
            }
            catch (DirectoryNotFoundException ex)
            {
                renderer.WriteError($"Directory not found: {ex.Message}");
                Environment.ExitCode = ExitCodes.FatalError;
            }
            catch (IOException ex)
            {
                renderer.WriteError($"I/O error during scan: {ex.Message}");
                Environment.ExitCode = ExitCodes.FatalError;
            }
            catch (UnauthorizedAccessException ex)
            {
                renderer.WriteError($"Access denied: {ex.Message}");
                Environment.ExitCode = ExitCodes.FatalError;
            }
        }, pathsArgument, saveBaselineOption, noCacheOption, excludeOption);

        return command;
    }
}
