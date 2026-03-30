using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using SeamQ.Cli.Rendering;
using SeamQ.Core.Abstractions;
using SeamQ.Core.Models;
using SeamQ.Detector;
using ExitCodes = SeamQ.Core.Models.ExitCodes;

namespace SeamQ.Cli.Commands;

public static class ValidateCommand
{
    public static Command Create(IServiceProvider serviceProvider)
    {
        var seamIdArgument = new Argument<string?>("seam-id", () => null, "Seam ID to validate");
        var allOption = new Option<bool>("--all", "Validate all seams");

        var command = new Command("validate", "Check consumer contract compliance")
        {
            seamIdArgument,
            allOption
        };

        command.SetHandler(async (seamId, all) =>
        {
            var renderer = serviceProvider.GetRequiredService<IConsoleRenderer>();
            var validator = serviceProvider.GetRequiredService<IContractValidator>();
            var registry = serviceProvider.GetRequiredService<SeamRegistry>();

            // Determine which seams to validate
            List<Seam> seamsToValidate;
            if (all)
            {
                seamsToValidate = registry.GetAll().ToList();
                if (seamsToValidate.Count == 0)
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
                seamsToValidate = new List<Seam> { seam };
            }
            else
            {
                renderer.WriteError("Provide a seam ID or use --all to validate all seams.");
                Environment.ExitCode = ExitCodes.FatalError;
                return;
            }

            var totalErrors = 0;
            var totalWarnings = 0;

            foreach (var seam in seamsToValidate)
            {
                renderer.WriteHeader(seam.Name);
                var report = await validator.ValidateAsync(seam);

                foreach (var result in report.Results)
                {
                    if (result.Errors == 0 && result.Warnings == 0)
                    {
                        renderer.WriteSuccess($"{result.ConsumerName} — compliant");
                    }
                    else
                    {
                        renderer.WriteWarning($"{result.ConsumerName} — {result.Errors} errors, {result.Warnings} warnings");
                        foreach (var finding in result.Findings)
                        {
                            var marker = finding.Severity == ValidationSeverity.Error ? "[!!]" : "[??]";
                            var color = finding.Severity == ValidationSeverity.Error
                                ? (Action<string>)renderer.WriteError
                                : renderer.WriteWarning;
                            color($"  {marker} {finding.ElementName}: {finding.Message}");
                        }
                    }
                }

                totalErrors += report.TotalErrors;
                totalWarnings += report.TotalWarnings;
                renderer.WriteLine();
            }

            // Summary
            if (totalErrors == 0 && totalWarnings == 0)
            {
                renderer.WriteSuccess($"all {seamsToValidate.Count} seam(s) valid.");
            }
            else
            {
                renderer.WriteInfo($"validated {seamsToValidate.Count} seam(s): {totalErrors} errors, {totalWarnings} warnings.");
                Environment.ExitCode = totalErrors > 0 ? ExitCodes.FatalError : ExitCodes.PartialFailure;
            }
        }, seamIdArgument, allOption);

        return command;
    }
}
