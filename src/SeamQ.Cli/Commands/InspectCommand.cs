using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using SeamQ.Cli.Rendering;
using SeamQ.Core.Abstractions;
using SeamQ.Core.Configuration;
using SeamQ.Core.Models;
using SeamQ.Detector;
using ExitCodes = SeamQ.Core.Models.ExitCodes;

namespace SeamQ.Cli.Commands;

public static class InspectCommand
{
    public static Command Create(IServiceProvider serviceProvider)
    {
        var seamIdArgument = new Argument<string>("seam-id", "Seam ID to inspect");

        var command = new Command("inspect", "Print detailed contract surface for a seam")
        {
            seamIdArgument
        };

        command.SetHandler(async (seamId) =>
        {
            var renderer = serviceProvider.GetRequiredService<IConsoleRenderer>();
            var registry = serviceProvider.GetRequiredService<SeamRegistry>();
            var globalContext = serviceProvider.GetRequiredService<GlobalContext>();

            // Prompt mode: scan configured workspaces and generate code + prompt files
            if (globalContext.PromptMode)
            {
                var promptGen = serviceProvider.GetRequiredService<PromptFileGenerator>();
                var scanner = serviceProvider.GetRequiredService<IWorkspaceScanner>();
                var config = serviceProvider.GetRequiredService<SeamQConfig>();
                var outputDir = Path.GetFullPath(globalContext.OutputDir ?? config.Output.Directory);
                var wsPaths = config.Workspaces.Select(w => w.Path).ToArray();
                foreach (var wsPath in wsPaths)
                {
                    var workspace = await scanner.ScanAsync(Path.GetFullPath(wsPath));
                    await promptGen.GenerateAsync(workspace, "inspect", outputDir, seamId);
                }
                return;
            }

            var seam = registry.GetById(seamId);
            if (seam is null)
            {
                // Try prefix match
                var all = registry.GetAll();
                var candidates = all.Where(s => s.Id.StartsWith(seamId, StringComparison.OrdinalIgnoreCase)).ToList();
                if (candidates.Count == 1)
                    seam = candidates[0];
                else if (candidates.Count > 1)
                {
                    renderer.WriteError($"Ambiguous seam ID '{seamId}'. Matches: {string.Join(", ", candidates.Select(s => s.Id))}");
                    Environment.ExitCode = ExitCodes.FatalError;
                    return;
                }
                else
                {
                    renderer.WriteError($"Seam '{seamId}' not found. Run 'seamq scan' first.");
                    Environment.ExitCode = ExitCodes.FatalError;
                    return;
                }
            }

            // Header
            renderer.WriteHeader(seam.Name);
            renderer.WriteKeyValue("id", seam.Id);
            renderer.WriteKeyValue("type", FormatSeamType(seam.Type));
            renderer.WriteKeyValue("provider", seam.Provider.Alias);
            renderer.WriteKeyValue("consumers", string.Join(", ", seam.Consumers.Select(c => c.Alias)));
            renderer.WriteKeyValue("confidence", seam.Confidence.ToString("P0"));
            renderer.WriteKeyValue("elements", seam.ContractSurface.Elements.Count.ToString());
            renderer.WriteLine();

            // Contract surface grouped by category
            PrintElementGroup(renderer, "Components", seam.ContractSurface.Components);
            PrintElementGroup(renderer, "Services", seam.ContractSurface.Services);
            PrintElementGroup(renderer, "Directives", seam.ContractSurface.Directives);
            PrintElementGroup(renderer, "Pipes", seam.ContractSurface.Pipes);
            PrintElementGroup(renderer, "Interfaces", seam.ContractSurface.Interfaces);
            PrintElementGroup(renderer, "Abstract Classes", seam.ContractSurface.AbstractClasses);
            PrintElementGroup(renderer, "Enumerations", seam.ContractSurface.Enumerations);
            PrintElementGroup(renderer, "Injection Tokens", seam.ContractSurface.InjectionTokens);
            PrintElementGroup(renderer, "Input Bindings", seam.ContractSurface.InputBindings);
            PrintElementGroup(renderer, "Output Bindings", seam.ContractSurface.OutputBindings);
            PrintElementGroup(renderer, "Signal Inputs", seam.ContractSurface.SignalInputs);
            PrintElementGroup(renderer, "Properties", seam.ContractSurface.Properties);
            PrintElementGroup(renderer, "Methods", seam.ContractSurface.Methods);
            PrintElementGroup(renderer, "Types", seam.ContractSurface.Types);

            await Task.CompletedTask;
        }, seamIdArgument);

        return command;
    }

    private static void PrintElementGroup(IConsoleRenderer renderer, string groupName, IEnumerable<ContractElement> elements)
    {
        var list = elements.ToList();
        if (list.Count == 0) return;

        renderer.WriteInfo($"  {groupName} ({list.Count})");
        foreach (var el in list.OrderBy(e => e.Name))
        {
            var sig = el.TypeSignature is not null ? $" : {el.TypeSignature}" : "";
            renderer.WriteLine($"    {el.Name}{sig}");
            renderer.WriteMuted($"      {el.SourceFile}:{el.LineNumber}");
        }
        renderer.WriteLine();
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
