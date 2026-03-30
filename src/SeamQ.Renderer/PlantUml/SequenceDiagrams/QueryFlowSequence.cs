using SeamQ.Core.Models;

namespace SeamQ.Renderer.PlantUml.SequenceDiagrams;

/// <summary>
/// Generates a query flow sequence diagram showing query execution patterns
/// from consumer through query services to data stores.
/// </summary>
public static class QueryFlowSequence
{
    public static string Generate(Seam seam)
    {
        var encoder = new PlantUmlEncoder();
        encoder.StartSequenceDiagram($"Query Flow: {seam.Name}");

        var consumer = seam.Consumers.FirstOrDefault()?.Alias ?? "Consumer";
        var surface = seam.ContractSurface;

        encoder.AddParticipant(consumer);
        encoder.AddParticipant("Query Service", "QuerySvc");
        encoder.AddParticipant("Data Store", "DataStore");
        encoder.AddBlankLine();

        // Try classified methods first
        var queryMethods = surface.Methods
            .Where(m =>
                m.Name.Contains("query", StringComparison.OrdinalIgnoreCase) ||
                m.ParentName?.Contains("Query", StringComparison.Ordinal) == true)
            .Take(8)
            .ToList();

        if (queryMethods.Count == 0)
            queryMethods = surface.Methods.Take(6).ToList();

        var selectors = surface.Selectors.Take(5).ToList();

        if (queryMethods.Count > 0)
        {
            encoder.AddRawLine("== Query Execution ==");
            foreach (var method in queryMethods.Take(6))
            {
                var sig = method.TypeSignature is not null ? $"{method.Name}(): {method.TypeSignature}" : $"{method.Name}()";
                encoder.AddMessage(consumer, "QuerySvc", sig);
                encoder.AddActivation("QuerySvc");
                encoder.AddMessage("QuerySvc", "DataStore", $"fetch({method.Name})");
                encoder.AddActivation("DataStore");
                encoder.AddMessage("DataStore", "QuerySvc", "data", isReturn: true);
                encoder.AddDeactivation("DataStore");
                encoder.AddMessage("QuerySvc", consumer, "result", isReturn: true);
                encoder.AddDeactivation("QuerySvc");
                encoder.AddBlankLine();
            }
        }
        else
        {
            // Name-heuristic: find QueryService + QueryMessage/QueryResponse
            var queryService = surface.Elements.FirstOrDefault(e =>
                e.ParentName is null && e.Name.Equals("QueryService", StringComparison.Ordinal));
            var queryMessage = surface.Elements.FirstOrDefault(e =>
                e.ParentName is null && TypeClassifier.IsRequestMessage(e) &&
                e.Name.Contains("Query", StringComparison.Ordinal));
            var queryResponse = surface.Elements.FirstOrDefault(e =>
                e.ParentName is null && TypeClassifier.IsResponse(e) &&
                e.Name.Contains("Query", StringComparison.Ordinal));

            if (queryService is not null || queryMessage is not null)
            {
                var svcName = queryService?.Name ?? "QueryService";
                var msgName = queryMessage?.Name ?? "QueryMessage";
                var respName = queryResponse?.Name ?? "QueryResponse";

                encoder.AddRawLine("== Query Execution ==");
                encoder.AddMessage(consumer, "QuerySvc", $"{svcName}.query({msgName})");
                encoder.AddActivation("QuerySvc");
                encoder.AddMessage("QuerySvc", "DataStore", $"fetch({msgName})");
                encoder.AddActivation("DataStore");
                encoder.AddMessage("DataStore", "QuerySvc", respName, isReturn: true);
                encoder.AddDeactivation("DataStore");
                encoder.AddMessage("QuerySvc", consumer, respName, isReturn: true);
                encoder.AddDeactivation("QuerySvc");
            }
            else
            {
                encoder.AddRawLine("== Query ==");
                encoder.AddMessage(consumer, "QuerySvc", "query()");
                encoder.AddActivation("QuerySvc");
                encoder.AddMessage("QuerySvc", "DataStore", "fetch()");
                encoder.AddActivation("DataStore");
                encoder.AddMessage("DataStore", "QuerySvc", "data", isReturn: true);
                encoder.AddDeactivation("DataStore");
                encoder.AddMessage("QuerySvc", consumer, "result", isReturn: true);
                encoder.AddDeactivation("QuerySvc");
            }
        }

        if (selectors.Count > 0)
        {
            encoder.AddBlankLine();
            encoder.AddRawLine("== Selector Queries ==");
            foreach (var selector in selectors)
            {
                encoder.AddMessage(consumer, "QuerySvc", $"select({selector.Name})");
                encoder.AddMessage("QuerySvc", consumer, "selectedData", isReturn: true);
            }
        }

        encoder.EndDiagram();
        return encoder.Build();
    }
}
