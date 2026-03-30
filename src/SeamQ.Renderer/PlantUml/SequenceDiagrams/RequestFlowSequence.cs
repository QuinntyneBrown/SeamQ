using SeamQ.Core.Models;

namespace SeamQ.Renderer.PlantUml.SequenceDiagrams;

/// <summary>
/// Generates a request flow sequence diagram showing request/response patterns
/// through provider services, particularly for HttpApiContract seams.
/// </summary>
public static class RequestFlowSequence
{
    public static string Generate(Seam seam)
    {
        var encoder = new PlantUmlEncoder();
        encoder.StartSequenceDiagram($"Request Flow: {seam.Name}");

        var consumer = seam.Consumers.FirstOrDefault()?.Alias ?? "Consumer";

        // Add participants
        encoder.AddParticipant(consumer);
        encoder.AddParticipant(seam.Provider.Alias, "Provider");
        encoder.AddParticipant("Backend", "Backend");

        encoder.AddBlankLine();

        // Find request-related methods
        var requestMethods = seam.ContractSurface.Methods
            .Where(m =>
                (m.Name?.Contains("request", StringComparison.OrdinalIgnoreCase) ?? false) ||
                (m.Name?.Contains("Request", StringComparison.Ordinal) ?? false) ||
                (m.ParentName?.Contains("Request", StringComparison.Ordinal) ?? false) ||
                (m.ParentName?.Contains("Service", StringComparison.Ordinal) ?? false))
            .Take(8)
            .ToList();

        // Fall back to all methods if no request-specific ones found
        if (requestMethods.Count == 0)
        {
            requestMethods = seam.ContractSurface.Methods.Take(6).ToList();
        }

        // If still no methods, use name-heuristic on Types to build the sequence
        if (requestMethods.Count == 0)
        {
            var requestTypes = seam.ContractSurface.Elements
                .Where(e => e.ParentName is null &&
                            e.Name.Contains("Request", StringComparison.OrdinalIgnoreCase))
                .Take(4)
                .ToList();

            var responseTypes = seam.ContractSurface.Elements
                .Where(e => e.ParentName is null &&
                            (e.Name.Contains("Response", StringComparison.OrdinalIgnoreCase) ||
                             e.Name.Contains("Result", StringComparison.OrdinalIgnoreCase)))
                .Take(4)
                .ToList();

            var serviceTypes = seam.ContractSurface.Elements
                .Where(e => e.ParentName is null &&
                            e.Name.Contains("Service", StringComparison.OrdinalIgnoreCase))
                .Take(4)
                .ToList();

            if (requestTypes.Count > 0 || serviceTypes.Count > 0)
            {
                encoder.AddRawLine("== Request/Response Flow ==");
                foreach (var svc in serviceTypes.Take(3))
                {
                    var reqType = requestTypes.FirstOrDefault();
                    var respType = responseTypes.FirstOrDefault();
                    var reqName = reqType?.Name ?? "request";
                    var respName = respType?.Name ?? "response";

                    encoder.AddMessage(consumer, "Provider", $"{svc.Name}.send({reqName})");
                    encoder.AddActivation("Provider");
                    encoder.AddMessage("Provider", "Backend", $"HTTP {reqName}");
                    encoder.AddActivation("Backend");
                    encoder.AddMessage("Backend", "Provider", respName, isReturn: true);
                    encoder.AddDeactivation("Backend");
                    encoder.AddMessage("Provider", consumer, respName, isReturn: true);
                    encoder.AddDeactivation("Provider");
                    encoder.AddBlankLine();
                }

                encoder.EndDiagram();
                return encoder.Build();
            }
        }

        // Group by ParentName (service)
        var grouped = requestMethods
            .GroupBy(m => m.ParentName ?? "Service")
            .ToList();

        if (grouped.Count > 0)
        {
            foreach (var group in grouped.Take(5))
            {
                encoder.AddRawLine($"== {group.Key} ==");

                foreach (var method in group.Take(5))
                {
                    var signature = method.TypeSignature is not null
                        ? $"{method.Name}(): {method.TypeSignature}"
                        : $"{method.Name}()";

                    encoder.AddMessage(consumer, "Provider", signature);
                    encoder.AddActivation("Provider");

                    encoder.AddMessage("Provider", "Backend", $"HTTP {method.Name}");
                    encoder.AddActivation("Backend");

                    encoder.AddMessage("Backend", "Provider", "response", isReturn: true);
                    encoder.AddDeactivation("Backend");

                    encoder.AddMessage("Provider", consumer, "result", isReturn: true);
                    encoder.AddDeactivation("Provider");

                    encoder.AddBlankLine();
                }
            }
        }
        else
        {
            // No methods at all — show generic flow
            encoder.AddRawLine("== Request ==");
            encoder.AddMessage(consumer, "Provider", "request()");
            encoder.AddActivation("Provider");
            encoder.AddMessage("Provider", "Backend", "HTTP request");
            encoder.AddActivation("Backend");
            encoder.AddMessage("Backend", "Provider", "response", isReturn: true);
            encoder.AddDeactivation("Backend");
            encoder.AddMessage("Provider", consumer, "result", isReturn: true);
            encoder.AddDeactivation("Provider");
            encoder.AddNote("No specific request methods found in contract surface");
        }

        encoder.EndDiagram();
        return encoder.Build();
    }
}
