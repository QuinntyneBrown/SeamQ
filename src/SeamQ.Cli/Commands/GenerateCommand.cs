using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using SeamQ.Cli.Rendering;
using SeamQ.Core.Abstractions;
using SeamQ.Core.Configuration;
using SeamQ.Core.Models;
using SeamQ.Detector;
using ExitCodes = SeamQ.Core.Models.ExitCodes;

namespace SeamQ.Cli.Commands;

public static class GenerateCommand
{
    public static Command Create(IServiceProvider serviceProvider)
    {
        var seamIdArgument = new Argument<string?>("seam-id", () => null, "Seam ID to generate ICD for");
        var allOption = new Option<bool>("--all", "Generate ICDs for all detected seams");
        var formatOption = new Option<string[]>("--format", () => new[] { "md" }, "Output format(s): md, html, pdf, docx")
        {
            Arity = ArgumentArity.OneOrMore
        };

        var command = new Command("generate", "Generate ICD for a seam")
        {
            seamIdArgument,
            allOption,
            formatOption
        };

        command.SetHandler(async (seamId, all, formats) =>
        {
            var renderer = serviceProvider.GetRequiredService<IConsoleRenderer>();
            var generator = serviceProvider.GetRequiredService<IIcdGenerator>();
            var registry = serviceProvider.GetRequiredService<SeamRegistry>();
            var config = serviceProvider.GetRequiredService<SeamQConfig>();
            var globalContext = serviceProvider.GetRequiredService<GlobalContext>();

            var outputDir = Path.GetFullPath(globalContext.OutputDir ?? config.Output.Directory);

            // Prompt mode: scan configured workspaces and generate code + prompt files
            if (globalContext.PromptMode)
            {
                var promptGen = serviceProvider.GetRequiredService<PromptFileGenerator>();
                var scanner = serviceProvider.GetRequiredService<IWorkspaceScanner>();
                var wsPaths = PromptFileGenerator.ResolveWorkspacePaths(config, registry);
                var formatStr = formats.Length > 0 ? string.Join(", ", formats) : "Markdown";
                foreach (var wsPath in wsPaths)
                {
                    var workspace = await scanner.ScanAsync(Path.GetFullPath(wsPath));
                    await promptGen.GenerateAsync(workspace, "generate", outputDir, formatStr);
                }
                return;
            }

            // Determine which seams to generate for
            List<Seam> seamsToGenerate;
            if (all)
            {
                seamsToGenerate = registry.GetAll().ToList();
                if (seamsToGenerate.Count == 0)
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
                seamsToGenerate = new List<Seam> { seam };
            }
            else
            {
                renderer.WriteError("Provide a seam ID or use --all to generate for all seams.");
                Environment.ExitCode = ExitCodes.FatalError;
                return;
            }

            // Use configured formats if none provided via CLI
            var outputFormats = formats.Length > 0 ? formats.ToList() : config.Output.Formats.ToList();

            foreach (var seam in seamsToGenerate)
            {
                var seamOutputDir = Path.Combine(outputDir, seam.Id);
                await generator.GenerateAsync(seam, seamOutputDir, outputFormats);
                renderer.WriteSuccess($"generated ICD for {seam.Name}");

                // List generated files
                if (Directory.Exists(seamOutputDir))
                {
                    foreach (var file in Directory.GetFiles(seamOutputDir))
                    {
                        renderer.WriteMuted($"  {Path.GetRelativePath(Directory.GetCurrentDirectory(), file)}");
                    }
                }
            }

            renderer.WriteLine();
            renderer.WriteInfo($"generated {seamsToGenerate.Count} ICD(s) in {outputDir}");
        }, seamIdArgument, allOption, formatOption);

        return command;
    }
}
