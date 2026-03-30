using SeamQ.Core.Models;

namespace SeamQ.Renderer.PlantUml.SequenceDiagrams;

/// <summary>
/// Generates a data consumption sequence diagram showing Observable subscription,
/// data push flow, and InputBinding delivery patterns.
/// </summary>
public static class DataConsumptionSequence
{
    public static string Generate(Seam seam)
    {
        var encoder = new PlantUmlEncoder();
        encoder.StartSequenceDiagram($"Data Consumption: {seam.Name}");

        var consumer = seam.Consumers.FirstOrDefault()?.Alias ?? "Consumer";
        var provider = seam.Provider.Alias;

        // Add participants
        encoder.AddParticipant(consumer);
        encoder.AddParticipant(provider, "Provider");
        encoder.AddParticipant("Data Source", "DataSource");

        encoder.AddBlankLine();

        var observables = seam.ContractSurface.Observables.Take(6).ToList();
        var inputBindings = seam.ContractSurface.InputBindings.Take(5).ToList();
        var properties = seam.ContractSurface.Properties.Take(5).ToList();

        // Subscription phase
        encoder.AddRawLine("== Subscription ==");

        if (observables.Count > 0)
        {
            foreach (var obs in observables)
            {
                var signature = obs.TypeSignature is not null
                    ? $"subscribe({obs.Name}: {obs.TypeSignature})"
                    : $"subscribe({obs.Name})";

                encoder.AddMessage(consumer, "Provider", signature);
                encoder.AddMessage("Provider", consumer, "subscription", isReturn: true);
            }
        }
        else
        {
            encoder.AddMessage(consumer, "Provider", "subscribe()");
            encoder.AddMessage("Provider", consumer, "subscription", isReturn: true);
            encoder.AddNote("No observables found in contract surface");
        }

        encoder.AddBlankLine();

        // Data push flow — source pushes through provider to consumer
        encoder.AddRawLine("== Data Push ==");
        encoder.AddActivation("DataSource");

        if (observables.Count > 0)
        {
            foreach (var obs in observables.Take(5))
            {
                encoder.AddMessage("DataSource", "Provider", $"next({obs.Name})");
                encoder.AddActivation("Provider");
                encoder.AddMessage("Provider", consumer, $"onData({obs.Name})", isReturn: false);
                encoder.AddDeactivation("Provider");
            }
        }
        else if (properties.Count > 0)
        {
            foreach (var prop in properties.Take(5))
            {
                encoder.AddMessage("DataSource", "Provider", $"update({prop.Name})");
                encoder.AddActivation("Provider");
                encoder.AddMessage("Provider", consumer, $"onUpdate({prop.Name})");
                encoder.AddDeactivation("Provider");
            }
        }
        else
        {
            encoder.AddMessage("DataSource", "Provider", "next(data)");
            encoder.AddActivation("Provider");
            encoder.AddMessage("Provider", consumer, "onData(data)");
            encoder.AddDeactivation("Provider");
        }

        encoder.AddDeactivation("DataSource");
        encoder.AddBlankLine();

        // Input binding delivery points
        if (inputBindings.Count > 0)
        {
            encoder.AddRawLine("== Input Binding Delivery ==");

            foreach (var input in inputBindings)
            {
                var signature = input.TypeSignature is not null
                    ? $"[{input.Name}]: {input.TypeSignature}"
                    : $"[{input.Name}]";

                encoder.AddMessage("Provider", consumer, signature);
            }

            encoder.AddBlankLine();
        }

        encoder.EndDiagram();
        return encoder.Build();
    }
}
