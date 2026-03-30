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

        // Add participants
        encoder.AddParticipant(consumer);
        encoder.AddParticipant("Command Service", "CmdSvc");
        encoder.AddParticipant("Event Bus", "EventBus");

        encoder.AddBlankLine();

        // Find command-related methods
        var commandMethods = seam.ContractSurface.Methods
            .Where(m =>
                (m.Name?.Contains("command", StringComparison.OrdinalIgnoreCase) ?? false) ||
                (m.Name?.Contains("Command", StringComparison.Ordinal) ?? false) ||
                (m.ParentName?.Contains("Command", StringComparison.Ordinal) ?? false) ||
                (m.ParentName?.Contains("command", StringComparison.Ordinal) ?? false))
            .Take(8)
            .ToList();

        // Fall back to all methods if no command-specific ones found
        if (commandMethods.Count == 0)
        {
            commandMethods = seam.ContractSurface.Methods.Take(6).ToList();
        }

        var outputBindings = seam.ContractSurface.OutputBindings.Take(5).ToList();

        // If still no methods, use name-heuristic on Types
        if (commandMethods.Count == 0)
        {
            var commandTypes = seam.ContractSurface.Elements
                .Where(e => e.ParentName is null &&
                            e.Name.Contains("Command", StringComparison.OrdinalIgnoreCase))
                .Take(6)
                .ToList();

            if (commandTypes.Count > 0)
            {
                encoder.AddRawLine("== Command Dispatch ==");
                foreach (var ct in commandTypes.Take(4))
                {
                    encoder.AddMessage(consumer, "CmdSvc", $"dispatch({ct.Name})");
                    encoder.AddActivation("CmdSvc");
                    encoder.AddMessage("CmdSvc", "CmdSvc", "validate()");
                    encoder.AddMessage("CmdSvc", "CmdSvc", "execute()");
                    encoder.AddMessage("CmdSvc", "EventBus", "emit(completed)");
                    encoder.AddMessage("CmdSvc", consumer, "result", isReturn: true);
                    encoder.AddDeactivation("CmdSvc");
                    encoder.AddBlankLine();
                }
                encoder.EndDiagram();
                return encoder.Build();
            }
        }

        if (commandMethods.Count > 0)
        {
            encoder.AddRawLine("== Command Dispatch ==");

            foreach (var method in commandMethods.Take(6))
            {
                // Command → Validate → Execute → Response
                encoder.AddMessage(consumer, "CmdSvc", $"{method.Name}()");
                encoder.AddActivation("CmdSvc");

                encoder.AddMessage("CmdSvc", "CmdSvc", "validate()");
                encoder.AddMessage("CmdSvc", "CmdSvc", "execute()");

                // Emit events via output bindings after execution
                if (outputBindings.Count > 0)
                {
                    foreach (var output in outputBindings.Take(2))
                    {
                        encoder.AddMessage("CmdSvc", "EventBus", $"emit({output.Name})");
                    }
                }

                encoder.AddMessage("CmdSvc", consumer, "result", isReturn: true);
                encoder.AddDeactivation("CmdSvc");

                encoder.AddBlankLine();
            }
        }
        else
        {
            // No methods — show generic command flow
            encoder.AddRawLine("== Command ==");
            encoder.AddMessage(consumer, "CmdSvc", "dispatch(command)");
            encoder.AddActivation("CmdSvc");
            encoder.AddMessage("CmdSvc", "CmdSvc", "validate()");
            encoder.AddMessage("CmdSvc", "CmdSvc", "execute()");

            if (outputBindings.Count > 0)
            {
                foreach (var output in outputBindings.Take(3))
                {
                    encoder.AddMessage("CmdSvc", "EventBus", $"emit({output.Name})");
                }
            }
            else
            {
                encoder.AddMessage("CmdSvc", "EventBus", "emit(commandCompleted)");
            }

            encoder.AddMessage("CmdSvc", consumer, "result", isReturn: true);
            encoder.AddDeactivation("CmdSvc");
            encoder.AddNote("No specific command methods found in contract surface");
        }

        // Show remaining output bindings as event summary if many
        if (outputBindings.Count > 2 && commandMethods.Count > 0)
        {
            encoder.AddBlankLine();
            encoder.AddRawLine("== Event Emissions ==");
            foreach (var output in outputBindings.Skip(2).Take(5))
            {
                encoder.AddMessage("CmdSvc", "EventBus", $"emit({output.Name})");
            }
        }

        encoder.EndDiagram();
        return encoder.Build();
    }
}
