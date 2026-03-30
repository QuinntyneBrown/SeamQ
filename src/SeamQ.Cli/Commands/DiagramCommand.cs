using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using SeamQ.Cli.Rendering;
using SeamQ.Core.Abstractions;
using SeamQ.Core.Configuration;
using SeamQ.Core.Models;
using SeamQ.Detector;

namespace SeamQ.Cli.Commands;

public static class DiagramCommand
{
    public static Command Create(IServiceProvider serviceProvider)
    {
        var seamIdArgument = new Argument<string?>("seam-id", () => null, "Seam ID to generate diagrams for");
        var allOption = new Option<bool>("--all", "Generate diagrams for all seams");
        var typeOption = new Option<string?>("--type", "Diagram type: context, class, sequence, state, c4-context, c4-container, c4-component, c4-code");

        var command = new Command("diagram", "Generate diagrams for a seam")
        {
            seamIdArgument,
            allOption,
            typeOption
        };

        command.SetHandler(async (seamId, all, type) =>
        {
            var renderer = serviceProvider.GetRequiredService<IConsoleRenderer>();
            var diagramRenderer = serviceProvider.GetRequiredService<IDiagramRenderer>();
            var registry = serviceProvider.GetRequiredService<SeamRegistry>();
            var config = serviceProvider.GetRequiredService<SeamQConfig>();

            var outputDir = Path.GetFullPath(config.Output.Directory);

            // Determine which seams to diagram
            List<Seam> seams;
            if (all)
            {
                seams = registry.GetAll().ToList();
                if (seams.Count == 0)
                {
                    renderer.WriteWarning("No seams in registry. Run 'seamq scan' first.");
                    return;
                }
            }
            else if (seamId is not null)
            {
                var seam = registry.GetById(seamId);
                if (seam is null)
                {
                    renderer.WriteError($"Seam '{seamId}' not found. Run 'seamq scan' first.");
                    return;
                }
                seams = new List<Seam> { seam };
            }
            else
            {
                renderer.WriteError("Provide a seam ID or use --all to generate diagrams for all seams.");
                return;
            }

            var totalFiles = 0;
            foreach (var seam in seams)
            {
                var seamOutputDir = Path.Combine(outputDir, seam.Id, "diagrams");
                var generatedFiles = await diagramRenderer.RenderAsync(seam, seamOutputDir);
                totalFiles += generatedFiles.Count;
                renderer.WriteSuccess($"generated {generatedFiles.Count} diagram(s) for {seam.Name}");
                foreach (var file in generatedFiles)
                {
                    renderer.WriteMuted($"  {Path.GetRelativePath(Directory.GetCurrentDirectory(), file)}");
                }
            }

            renderer.WriteLine();
            renderer.WriteInfo($"generated {totalFiles} diagram(s) for {seams.Count} seam(s).");
        }, seamIdArgument, allOption, typeOption);

        return command;
    }
}
