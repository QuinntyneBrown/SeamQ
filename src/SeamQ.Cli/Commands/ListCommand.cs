using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using SeamQ.Cli.Rendering;
using SeamQ.Core.Abstractions;
using SeamQ.Core.Configuration;
using SeamQ.Core.Models;
using SeamQ.Detector;
using ExitCodes = SeamQ.Core.Models.ExitCodes;

namespace SeamQ.Cli.Commands;

public static class ListCommand
{
    public static Command Create(IServiceProvider serviceProvider)
    {
        var typeOption = new Option<string?>("--type", "Filter by seam type");
        var providerOption = new Option<string?>("--provider", "Filter by provider workspace");
        var confidenceOption = new Option<double?>("--confidence", "Minimum confidence threshold");

        var command = new Command("list", "List all detected seams")
        {
            typeOption,
            providerOption,
            confidenceOption
        };

        command.SetHandler(async (type, provider, confidence) =>
        {
            var renderer = serviceProvider.GetRequiredService<IConsoleRenderer>();
            var registry = serviceProvider.GetRequiredService<SeamRegistry>();
            var config = serviceProvider.GetRequiredService<SeamQConfig>();
            var globalContext = serviceProvider.GetRequiredService<GlobalContext>();

            // Prompt mode: scan configured workspaces and generate code + prompt files
            if (globalContext.PromptMode)
            {
                var promptGen = serviceProvider.GetRequiredService<PromptFileGenerator>();
                var scanner = serviceProvider.GetRequiredService<IWorkspaceScanner>();
                var outputDir = Path.GetFullPath(globalContext.OutputDir ?? config.Output.Directory);
                var wsPaths = config.Workspaces.Select(w => w.Path).ToArray();
                foreach (var wsPath in wsPaths)
                {
                    var workspace = await scanner.ScanAsync(Path.GetFullPath(wsPath));
                    await promptGen.GenerateAsync(workspace, "list", outputDir);
                }
                return;
            }

            var seams = registry.GetAll();

            if (seams.Count == 0)
            {
                renderer.WriteWarning("No seams in registry. Run 'seamq scan' first.");
                Environment.ExitCode = ExitCodes.PartialFailure;
                return;
            }

            // Apply filters
            IEnumerable<Seam> filtered = seams;

            if (type is not null)
            {
                if (Enum.TryParse<SeamType>(type.Replace("-", ""), ignoreCase: true, out var seamType))
                    filtered = filtered.Where(s => s.Type == seamType);
                else
                    renderer.WriteWarning($"Unknown seam type '{type}', showing all.");
            }

            if (provider is not null)
                filtered = filtered.Where(s => s.Provider.Alias.Contains(provider, StringComparison.OrdinalIgnoreCase));

            if (confidence is not null)
                filtered = filtered.Where(s => s.Confidence >= confidence.Value);

            var results = filtered.OrderByDescending(s => s.Confidence).ToList();

            if (results.Count == 0)
            {
                renderer.WriteMuted("No seams match the specified filters.");
                return;
            }

            // Render table
            var headers = new[] { "ID", "seam_name", "type", "provider", "consumer(s)", "conf" };
            var rows = results.Select(s => new[]
            {
                s.Id.Length > 8 ? s.Id[..8] : s.Id,
                s.Name,
                FormatSeamType(s.Type),
                s.Provider.Alias,
                string.Join(", ", s.Consumers.Select(c => c.Alias)),
                s.Confidence.ToString("F2")
            });

            renderer.WriteTable(headers, rows);
            renderer.WriteLine();
            renderer.WriteMuted($"{results.Count} seams listed.");

            await Task.CompletedTask;
        }, typeOption, providerOption, confidenceOption);

        return command;
    }

    private static string FormatSeamType(SeamType type) => type switch
    {
        SeamType.PluginContract => "plugin-contract",
        SeamType.SharedLibrary => "shared-library",
        SeamType.MessageBus => "message-bus",
        SeamType.RouteContract => "route-contract",
        SeamType.StateContract => "state-contract",
        SeamType.HttpApiContract => "http-api-contract",
        _ => type.ToString()
    };
}
