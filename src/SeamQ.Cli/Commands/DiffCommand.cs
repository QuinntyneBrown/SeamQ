using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using SeamQ.Cli.Rendering;
using SeamQ.Core.Abstractions;
using SeamQ.Detector;
using ExitCodes = SeamQ.Core.Models.ExitCodes;

namespace SeamQ.Cli.Commands;

public static class DiffCommand
{
    public static Command Create(IServiceProvider serviceProvider)
    {
        var baselineArgument = new Argument<string>("baseline-path", "Path to baseline JSON file");

        var command = new Command("diff", "Compare scan against a previous baseline")
        {
            baselineArgument
        };

        command.SetHandler(async (baselinePath) =>
        {
            var renderer = serviceProvider.GetRequiredService<IConsoleRenderer>();
            var differ = serviceProvider.GetRequiredService<ISeamDiffer>();
            var registry = serviceProvider.GetRequiredService<SeamRegistry>();

            var currentSeams = registry.GetAll();
            if (currentSeams.Count == 0)
            {
                renderer.WriteWarning("No seams in registry. Run 'seamq scan' first.");
                Environment.ExitCode = ExitCodes.PartialFailure;
                return;
            }

            var fullBaselinePath = Path.GetFullPath(baselinePath);
            if (!File.Exists(fullBaselinePath))
            {
                renderer.WriteError($"Baseline file not found: {fullBaselinePath}");
                Environment.ExitCode = ExitCodes.FatalError;
                return;
            }

            var report = await differ.DiffAsync(fullBaselinePath, currentSeams);

            if (!report.HasChanges)
            {
                renderer.WriteSuccess("no changes detected since baseline.");
                return;
            }

            renderer.WriteHeader("Diff Report");
            renderer.WriteLine();

            foreach (var seamDiff in report.SeamDiffs)
            {
                renderer.WriteInfo($"  {seamDiff.SeamName} ({seamDiff.SeamId})");

                foreach (var change in seamDiff.Changes)
                {
                    var marker = change.ChangeType switch
                    {
                        ChangeType.Added => "+",
                        ChangeType.Removed => "-",
                        ChangeType.Modified => "~",
                        _ => " "
                    };

                    var detail = "";
                    if (change.OldValue is not null && change.NewValue is not null)
                        detail = $" ({change.OldValue} -> {change.NewValue})";
                    else if (change.NewValue is not null)
                        detail = $" ({change.NewValue})";

                    var line = $"    {marker} {change.ElementName}{detail}";

                    switch (change.ChangeType)
                    {
                        case ChangeType.Added:
                            renderer.WriteSuccess(line);
                            break;
                        case ChangeType.Removed:
                            renderer.WriteError(line);
                            break;
                        default:
                            renderer.WriteWarning(line);
                            break;
                    }
                }
                renderer.WriteLine();
            }

            renderer.WriteInfo($"summary: +{report.TotalAdditions} added, ~{report.TotalModifications} modified, -{report.TotalRemovals} removed");
        }, baselineArgument);

        return command;
    }
}
