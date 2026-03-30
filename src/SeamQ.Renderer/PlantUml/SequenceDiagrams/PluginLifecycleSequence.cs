using SeamQ.Core.Models;

namespace SeamQ.Renderer.PlantUml.SequenceDiagrams;

/// <summary>
/// Generates a plugin lifecycle sequence diagram showing the initialization,
/// registration, and teardown flow for a plugin seam.
/// </summary>
public static class PluginLifecycleSequence
{
    public static string Generate(Seam seam)
    {
        var encoder = new PlantUmlEncoder();
        encoder.StartSequenceDiagram($"Plugin Lifecycle: {seam.Name}");

        var provider = seam.Provider.Alias;
        var consumers = seam.Consumers.Select(c => c.Alias).ToList();

        // Add participants
        encoder.AddParticipant("Host Application", "Host");
        encoder.AddParticipant(provider, "Provider");
        foreach (var consumer in consumers)
        {
            encoder.AddParticipant(consumer);
        }

        encoder.AddBlankLine();

        // Initialization phase
        encoder.AddRawLine("== Initialization ==");
        encoder.AddMessage("Host", provider, "initialize()");
        encoder.AddActivation(provider);

        // Register contract surface interfaces
        foreach (var iface in seam.ContractSurface.Interfaces.Take(5))
        {
            encoder.AddMessage(provider, "Host", $"register({iface.Name})");
        }

        // Register injection tokens
        foreach (var token in seam.ContractSurface.InjectionTokens.Take(5))
        {
            encoder.AddMessage(provider, "Host", $"provide({token.Name})");
        }

        encoder.AddDeactivation(provider);
        encoder.AddBlankLine();

        // Consumer registration phase
        encoder.AddRawLine("== Consumer Registration ==");
        foreach (var consumer in consumers)
        {
            encoder.AddMessage("Host", consumer, "loadPlugin()");
            encoder.AddActivation(consumer);
            encoder.AddMessage(consumer, "Host", "registerConsumer()");

            // Show input/output bindings
            foreach (var input in seam.ContractSurface.InputBindings.Take(3))
            {
                encoder.AddMessage(consumer, provider, $"bind({input.Name})");
            }

            encoder.AddDeactivation(consumer);
        }

        encoder.AddBlankLine();

        // Runtime phase
        encoder.AddRawLine("== Runtime ==");
        foreach (var consumer in consumers.Take(2))
        {
            foreach (var method in seam.ContractSurface.Methods.Take(2))
            {
                encoder.AddMessage(consumer, provider, $"{method.Name}()");
                encoder.AddMessage(provider, consumer, "result", isReturn: true);
            }
        }

        encoder.AddBlankLine();

        // Teardown phase
        encoder.AddRawLine("== Teardown ==");
        foreach (var consumer in consumers)
        {
            encoder.AddMessage("Host", consumer, "destroy()");
        }
        encoder.AddMessage("Host", provider, "shutdown()");

        encoder.EndDiagram();
        return encoder.Build();
    }
}
