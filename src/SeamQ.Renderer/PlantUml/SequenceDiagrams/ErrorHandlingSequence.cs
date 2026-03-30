using SeamQ.Core.Models;

namespace SeamQ.Renderer.PlantUml.SequenceDiagrams;

/// <summary>
/// Generates an error handling sequence diagram showing success and error paths
/// for method calls, including error propagation patterns.
/// </summary>
public static class ErrorHandlingSequence
{
    public static string Generate(Seam seam)
    {
        var encoder = new PlantUmlEncoder();
        encoder.StartSequenceDiagram($"Error Handling: {seam.Name}");

        var consumer = seam.Consumers.FirstOrDefault()?.Alias ?? "Consumer";
        var provider = seam.Provider.Alias;

        // Add participants
        encoder.AddParticipant(consumer);
        encoder.AddParticipant(provider, "Provider");
        encoder.AddParticipant("Error Handler", "ErrorHandler");

        encoder.AddBlankLine();

        var methods = seam.ContractSurface.Methods.Take(5).ToList();

        // Success path
        encoder.AddRawLine("== Success Path ==");

        if (methods.Count > 0)
        {
            foreach (var method in methods.Take(3))
            {
                var signature = method.TypeSignature is not null
                    ? $"{method.Name}(): {method.TypeSignature}"
                    : $"{method.Name}()";

                encoder.AddMessage(consumer, "Provider", signature);
                encoder.AddActivation("Provider");
                encoder.AddMessage("Provider", consumer, "result", isReturn: true);
                encoder.AddDeactivation("Provider");
            }
        }
        else
        {
            encoder.AddMessage(consumer, "Provider", "invoke()");
            encoder.AddActivation("Provider");
            encoder.AddMessage("Provider", consumer, "result", isReturn: true);
            encoder.AddDeactivation("Provider");
        }

        encoder.AddBlankLine();

        // Error path
        encoder.AddRawLine("== Error Path ==");

        if (methods.Count > 0)
        {
            // Show the first method in an error scenario
            var method = methods.First();
            var signature = method.TypeSignature is not null
                ? $"{method.Name}(): {method.TypeSignature}"
                : $"{method.Name}()";

            encoder.AddMessage(consumer, "Provider", signature);
            encoder.AddActivation("Provider");

            encoder.AddMessage("Provider", "ErrorHandler", "handleError(exception)");
            encoder.AddActivation("ErrorHandler");

            encoder.AddMessage("ErrorHandler", "ErrorHandler", "log(error)");
            encoder.AddMessage("ErrorHandler", "Provider", "errorResponse", isReturn: true);
            encoder.AddDeactivation("ErrorHandler");

            encoder.AddMessage("Provider", consumer, "error", isReturn: true);
            encoder.AddDeactivation("Provider");

            // Show remaining methods with catch pattern
            if (methods.Count > 1)
            {
                encoder.AddBlankLine();

                foreach (var remainingMethod in methods.Skip(1).Take(4))
                {
                    encoder.AddMessage(consumer, "Provider", $"{remainingMethod.Name}()");
                    encoder.AddActivation("Provider");

                    encoder.AddMessage("Provider", "ErrorHandler", $"catch({remainingMethod.Name}Error)");
                    encoder.AddMessage("ErrorHandler", "Provider", "fallback", isReturn: true);

                    encoder.AddMessage("Provider", consumer, "fallbackResult", isReturn: true);
                    encoder.AddDeactivation("Provider");
                }
            }
        }
        else
        {
            encoder.AddMessage(consumer, "Provider", "invoke()");
            encoder.AddActivation("Provider");

            encoder.AddMessage("Provider", "ErrorHandler", "handleError(exception)");
            encoder.AddActivation("ErrorHandler");

            encoder.AddMessage("ErrorHandler", "ErrorHandler", "log(error)");
            encoder.AddMessage("ErrorHandler", "Provider", "errorResponse", isReturn: true);
            encoder.AddDeactivation("ErrorHandler");

            encoder.AddMessage("Provider", consumer, "error", isReturn: true);
            encoder.AddDeactivation("Provider");

            encoder.AddNote("No specific methods found in contract surface");
        }

        encoder.EndDiagram();
        return encoder.Build();
    }
}
