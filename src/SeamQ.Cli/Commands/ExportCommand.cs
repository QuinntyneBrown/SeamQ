using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using SeamQ.Cli.Rendering;
using SeamQ.Core.Abstractions;
using SeamQ.Core.Configuration;
using SeamQ.Core.Models;
using SeamQ.Detector;
using ExitCodes = SeamQ.Core.Models.ExitCodes;

namespace SeamQ.Cli.Commands;

public static class ExportCommand
{
    public static Command Create(IServiceProvider serviceProvider)
    {
        var seamIdArgument = new Argument<string?>("seam-id", () => null, "Seam ID to export");
        var allOption = new Option<bool>("--all", "Export all seams");
        var formatOption = new Option<string>("--format", () => "json", "Export format");

        var command = new Command("export", "Export raw seam data as JSON")
        {
            seamIdArgument,
            allOption,
            formatOption
        };

        command.SetHandler(async (seamId, all, format) =>
        {
            var renderer = serviceProvider.GetRequiredService<IConsoleRenderer>();
            var exporter = serviceProvider.GetRequiredService<IDataExporter>();
            var registry = serviceProvider.GetRequiredService<SeamRegistry>();
            var config = serviceProvider.GetRequiredService<SeamQConfig>();
            var globalContext = serviceProvider.GetRequiredService<GlobalContext>();

            var outputDir = Path.GetFullPath(globalContext.OutputDir ?? config.Output.Directory);

            // Determine which seams to export
            List<Seam> seamsToExport;
            if (all)
            {
                seamsToExport = registry.GetAll().ToList();
                if (seamsToExport.Count == 0)
                {
                    renderer.WriteWarning("No seams in registry. Run 'seamq scan' first.");
                    Environment.ExitCode = ExitCodes.PartialFailure;
                    return;
                }
            }
            else if (seamId is not null)
            {
                var seam = registry.GetById(seamId);
                if (seam is null)
                {
                    renderer.WriteError($"Seam '{seamId}' not found. Run 'seamq scan' first.");
                    Environment.ExitCode = ExitCodes.FatalError;
                    return;
                }
                seamsToExport = new List<Seam> { seam };
            }
            else
            {
                renderer.WriteError("Provide a seam ID or use --all to export all seams.");
                Environment.ExitCode = ExitCodes.FatalError;
                return;
            }

            var totalFiles = 0;
            foreach (var seam in seamsToExport)
            {
                var seamOutputDir = Path.Combine(outputDir, seam.Id, "export");
                var exportedFiles = await exporter.ExportAsync(seam, seamOutputDir, format);
                totalFiles += exportedFiles.Count;
                renderer.WriteSuccess($"exported {seam.Name}");
                foreach (var file in exportedFiles)
                {
                    renderer.WriteMuted($"  {Path.GetRelativePath(Directory.GetCurrentDirectory(), file)}");
                }
            }

            renderer.WriteLine();
            renderer.WriteInfo($"exported {totalFiles} file(s) for {seamsToExport.Count} seam(s).");
        }, seamIdArgument, allOption, formatOption);

        return command;
    }
}
