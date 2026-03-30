using SeamQ.Core.Models;

namespace SeamQ.Renderer.PlantUml.SequenceDiagrams;

/// <summary>
/// Generates an app startup sequence diagram showing bootstrap, provider initialization,
/// consumer loading, and ready phases for a seam.
/// </summary>
public static class AppStartupSequence
{
    public static string Generate(Seam seam)
    {
        var encoder = new PlantUmlEncoder();
        encoder.StartSequenceDiagram($"App Startup: {seam.Name}");

        var provider = seam.Provider.Alias;
        var consumers = seam.Consumers.Select(c => c.Alias).ToList();

        // Add participants
        encoder.AddParticipant("Host Application", "Host");
        encoder.AddParticipant(provider, "Provider");
        foreach (var consumer in consumers.Take(6))
        {
            encoder.AddParticipant(consumer);
        }

        encoder.AddBlankLine();

        // Bootstrap phase — InjectionToken provisioning
        encoder.AddRawLine("== Bootstrap ==");
        encoder.AddMessage("Host", provider, "bootstrap()");
        encoder.AddActivation(provider);

        var tokens = seam.ContractSurface.InjectionTokens.Take(5).ToList();
        if (tokens.Count > 0)
        {
            foreach (var token in tokens)
            {
                encoder.AddMessage(provider, "Host", $"provide({token.Name})");
            }
        }
        else
        {
            encoder.AddMessage(provider, "Host", "provideDefaults()");
        }

        encoder.AddDeactivation(provider);
        encoder.AddBlankLine();

        // Provider Initialization phase — Interface registration
        encoder.AddRawLine("== Provider Initialization ==");
        encoder.AddMessage("Host", provider, "initialize()");
        encoder.AddActivation(provider);

        var interfaces = seam.ContractSurface.Interfaces.Take(5).ToList();
        if (interfaces.Count > 0)
        {
            foreach (var iface in interfaces)
            {
                encoder.AddMessage(provider, "Host", $"register({iface.Name})");
            }
        }
        else
        {
            encoder.AddMessage(provider, "Host", "registerModule()");
        }

        encoder.AddDeactivation(provider);
        encoder.AddBlankLine();

        // Consumer Loading phase — InputBinding binding
        encoder.AddRawLine("== Consumer Loading ==");
        var inputBindings = seam.ContractSurface.InputBindings.Take(5).ToList();

        foreach (var consumer in consumers.Take(5))
        {
            encoder.AddMessage("Host", consumer, "load()");
            encoder.AddActivation(consumer);

            if (inputBindings.Count > 0)
            {
                foreach (var input in inputBindings.Take(3))
                {
                    encoder.AddMessage(consumer, provider, $"bind({input.Name})");
                }
            }
            else
            {
                encoder.AddMessage(consumer, provider, "connect()");
            }

            encoder.AddMessage(consumer, "Host", "ready", isReturn: true);
            encoder.AddDeactivation(consumer);
        }

        if (consumers.Count == 0)
        {
            encoder.AddNote("No consumers detected for this seam");
        }

        encoder.AddBlankLine();

        // Ready phase
        encoder.AddRawLine("== Ready ==");
        encoder.AddMessage("Host", provider, "activate()");
        foreach (var consumer in consumers.Take(5))
        {
            encoder.AddMessage("Host", consumer, "activate()");
        }
        encoder.AddNote("All participants initialized and ready");

        encoder.EndDiagram();
        return encoder.Build();
    }
}
