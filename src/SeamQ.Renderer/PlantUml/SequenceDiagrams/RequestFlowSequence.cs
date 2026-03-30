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
        var surface = seam.ContractSurface;

        encoder.AddParticipant(consumer);
        encoder.AddParticipant(seam.Provider.Alias, "Provider");
        encoder.AddParticipant("Backend", "Backend");
        encoder.AddBlankLine();

        // Try classified methods first
        var requestMethods = surface.Methods
            .Where(m =>
                m.Name.Contains("request", StringComparison.OrdinalIgnoreCase) ||
                m.Name.Contains("send", StringComparison.OrdinalIgnoreCase) ||
                m.ParentName?.Contains("Request", StringComparison.Ordinal) == true ||
                m.ParentName?.Contains("Service", StringComparison.Ordinal) == true)
            .Take(8)
            .ToList();

        if (requestMethods.Count == 0)
            requestMethods = surface.Methods.Take(6).ToList();

        if (requestMethods.Count > 0)
        {
            var grouped = requestMethods.GroupBy(m => m.ParentName ?? "Service").ToList();
            foreach (var group in grouped.Take(5))
            {
                encoder.AddRawLine($"== {group.Key} ==");
                foreach (var method in group.Take(5))
                {
                    var sig = method.TypeSignature is not null ? $"{method.Name}(): {method.TypeSignature}" : $"{method.Name}()";
                    encoder.AddMessage(consumer, "Provider", sig);
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
            // Name-heuristic: use Service → Message → Response type triples
            var services = TypeClassifier.GetServices(surface);
            var pairs = TypeClassifier.GetMessagePairs(surface);

            if (services.Count > 0 && pairs.Count > 0)
            {
                encoder.AddRawLine("== Request/Response Flow ==");
                foreach (var pair in pairs.Take(5))
                {
                    var svc = TypeClassifier.FindServiceForMessage(pair.Request, services);
                    var svcName = svc?.Name ?? services.First().Name;
                    var respName = pair.Response?.Name ?? "response";

                    encoder.AddMessage(consumer, "Provider", $"{svcName}.send({pair.Request.Name})");
                    encoder.AddActivation("Provider");
                    encoder.AddMessage("Provider", "Backend", $"HTTP {pair.Request.Name}");
                    encoder.AddActivation("Backend");
                    encoder.AddMessage("Backend", "Provider", respName, isReturn: true);
                    encoder.AddDeactivation("Backend");
                    encoder.AddMessage("Provider", consumer, respName, isReturn: true);
                    encoder.AddDeactivation("Provider");
                    encoder.AddBlankLine();
                }
            }
            else
            {
                encoder.AddRawLine("== Request ==");
                encoder.AddMessage(consumer, "Provider", "request()");
                encoder.AddActivation("Provider");
                encoder.AddMessage("Provider", "Backend", "HTTP request");
                encoder.AddActivation("Backend");
                encoder.AddMessage("Backend", "Provider", "response", isReturn: true);
                encoder.AddDeactivation("Backend");
                encoder.AddMessage("Provider", consumer, "result", isReturn: true);
                encoder.AddDeactivation("Provider");
            }
        }

        encoder.EndDiagram();
        return encoder.Build();
    }
}
