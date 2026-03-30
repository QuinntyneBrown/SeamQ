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

        // Add participants
        encoder.AddParticipant(consumer);
        encoder.AddParticipant("Query Service", "QuerySvc");
        encoder.AddParticipant("Data Store", "DataStore");

        encoder.AddBlankLine();

        // Find query-related methods
        var queryMethods = seam.ContractSurface.Methods
            .Where(m =>
                (m.Name?.Contains("query", StringComparison.OrdinalIgnoreCase) ?? false) ||
                (m.Name?.Contains("Query", StringComparison.Ordinal) ?? false) ||
                (m.ParentName?.Contains("Query", StringComparison.Ordinal) ?? false) ||
                (m.ParentName?.Contains("query", StringComparison.Ordinal) ?? false))
            .Take(8)
            .ToList();

        // Fall back to all Methods if no query-specific ones found
        if (queryMethods.Count == 0)
        {
            queryMethods = seam.ContractSurface.Methods.Take(6).ToList();
        }

        // Also gather selectors as supplementary query elements
        var selectors = seam.ContractSurface.Selectors.Take(5).ToList();

        // If still no methods, use name-heuristic on Types
        if (queryMethods.Count == 0)
        {
            var queryTypes = seam.ContractSurface.Elements
                .Where(e => e.ParentName is null &&
                            (e.Name.Contains("Query", StringComparison.OrdinalIgnoreCase) ||
                             e.Name.Contains("Service", StringComparison.OrdinalIgnoreCase)))
                .Take(6)
                .ToList();

            if (queryTypes.Count > 0)
            {
                encoder.AddRawLine("== Query Execution ==");
                foreach (var qt in queryTypes)
                {
                    encoder.AddMessage(consumer, "QuerySvc", $"{qt.Name}.query()");
                    encoder.AddActivation("QuerySvc");
                    encoder.AddMessage("QuerySvc", "DataStore", $"fetch({qt.Name})");
                    encoder.AddActivation("DataStore");
                    encoder.AddMessage("DataStore", "QuerySvc", "data", isReturn: true);
                    encoder.AddDeactivation("DataStore");
                    encoder.AddMessage("QuerySvc", consumer, "result", isReturn: true);
                    encoder.AddDeactivation("QuerySvc");
                    encoder.AddBlankLine();
                }
                encoder.EndDiagram();
                return encoder.Build();
            }
        }

        if (queryMethods.Count > 0)
        {
            encoder.AddRawLine("== Query Execution ==");

            foreach (var method in queryMethods.Take(6))
            {
                var signature = method.TypeSignature is not null
                    ? $"{method.Name}(): {method.TypeSignature}"
                    : $"{method.Name}()";

                encoder.AddMessage(consumer, "QuerySvc", signature);
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
            encoder.AddRawLine("== Query ==");
            encoder.AddMessage(consumer, "QuerySvc", "query()");
            encoder.AddActivation("QuerySvc");
            encoder.AddMessage("QuerySvc", "DataStore", "fetch()");
            encoder.AddActivation("DataStore");
            encoder.AddMessage("DataStore", "QuerySvc", "data", isReturn: true);
            encoder.AddDeactivation("DataStore");
            encoder.AddMessage("QuerySvc", consumer, "result", isReturn: true);
            encoder.AddDeactivation("QuerySvc");
            encoder.AddNote("No specific query methods found in contract surface");
        }

        // Show selectors if present
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
