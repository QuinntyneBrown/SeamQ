using SeamQ.Core.Models;

namespace SeamQ.Renderer.PlantUml.SequenceDiagrams;

/// <summary>
/// Generates a command flow sequence diagram showing command dispatch,
/// validation, execution, and event emission patterns.
/// </summary>
public static class CommandFlowSequence
{
    public static string Generate(Seam seam)
    {
        var encoder = new PlantUmlEncoder();
        encoder.StartSequenceDiagram($"Command Flow: {seam.Name}");

        var consumer = seam.Consumers.FirstOrDefault()?.Alias ?? "Consumer";
        var surface = seam.ContractSurface;

        encoder.AddParticipant(consumer);
        encoder.AddParticipant("Command Service", "CmdSvc");
        encoder.AddParticipant("Event Bus", "EventBus");
        encoder.AddBlankLine();

        // Try classified methods first
        var commandMethods = surface.Methods
            .Where(m =>
                m.Name.Contains("command", StringComparison.OrdinalIgnoreCase) ||
                m.Name.Contains("execute", StringComparison.OrdinalIgnoreCase) ||
                m.ParentName?.Contains("Command", StringComparison.Ordinal) == true)
            .Take(8)
            .ToList();

        if (commandMethods.Count == 0)
            commandMethods = surface.Methods.Take(6).ToList();

        var outputBindings = surface.OutputBindings.Take(5).ToList();

        if (commandMethods.Count > 0)
        {
            encoder.AddRawLine("== Command Dispatch ==");
            foreach (var method in commandMethods.Take(6))
            {
                encoder.AddMessage(consumer, "CmdSvc", $"{method.Name}()");
                encoder.AddActivation("CmdSvc");
                encoder.AddMessage("CmdSvc", "CmdSvc", "validate()");
                encoder.AddMessage("CmdSvc", "CmdSvc", "execute()");
                if (outputBindings.Count > 0)
                    foreach (var output in outputBindings.Take(2))
                        encoder.AddMessage("CmdSvc", "EventBus", $"emit({output.Name})");
                encoder.AddMessage("CmdSvc", consumer, "result", isReturn: true);
                encoder.AddDeactivation("CmdSvc");
                encoder.AddBlankLine();
            }
        }
        else
        {
            // Name-heuristic: find CommandService + CommandMessage → CommandResponse
            var cmdService = surface.Elements.FirstOrDefault(e =>
                e.ParentName is null && e.Name.Equals("CommandService", StringComparison.Ordinal));
            var cmdMessages = surface.Elements
                .Where(e => TypeClassifier.IsRequestMessage(e) &&
                            e.Name.Contains("Command", StringComparison.Ordinal))
                .Take(4)
                .ToList();
            var cmdResponse = surface.Elements.FirstOrDefault(e =>
                TypeClassifier.IsResponse(e) && e.Name.Contains("Command", StringComparison.Ordinal));

            if (cmdService is not null || cmdMessages.Count > 0)
            {
                var svcName = cmdService?.Name ?? "CommandService";
                var respName = cmdResponse?.Name ?? "CommandResponse";

                encoder.AddRawLine("== Command Dispatch ==");
                foreach (var msg in cmdMessages.DefaultIfEmpty(null!))
                {
                    var msgName = msg?.Name ?? "CommandMessage";
                    encoder.AddMessage(consumer, "CmdSvc", $"{svcName}.execute({msgName})");
                    encoder.AddActivation("CmdSvc");
                    encoder.AddMessage("CmdSvc", "CmdSvc", "validate()");
                    encoder.AddMessage("CmdSvc", "CmdSvc", "execute()");
                    encoder.AddMessage("CmdSvc", "EventBus", "emit(commandCompleted)");
                    encoder.AddMessage("CmdSvc", consumer, respName, isReturn: true);
                    encoder.AddDeactivation("CmdSvc");
                    encoder.AddBlankLine();
                }
            }
            else
            {
                encoder.AddRawLine("== Command ==");
                encoder.AddMessage(consumer, "CmdSvc", "dispatch(command)");
                encoder.AddActivation("CmdSvc");
                encoder.AddMessage("CmdSvc", "CmdSvc", "validate()");
                encoder.AddMessage("CmdSvc", "CmdSvc", "execute()");
                encoder.AddMessage("CmdSvc", "EventBus", "emit(commandCompleted)");
                encoder.AddMessage("CmdSvc", consumer, "result", isReturn: true);
                encoder.AddDeactivation("CmdSvc");
            }
        }

        encoder.EndDiagram();
        return encoder.Build();
    }
}
