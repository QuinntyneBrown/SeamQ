using System.Text;
using SeamQ.Core.Models;

namespace SeamQ.Renderer.C4;

/// <summary>
/// Generates a C4 Dynamic diagram showing numbered interaction flow
/// between Provider, Consumer(s), and the Contract Surface.
/// </summary>
public static class C4Dynamic
{
    public static string Generate(Seam seam)
    {
        var sb = new StringBuilder();
        var surface = seam.ContractSurface;

        sb.AppendLine("@startuml");
        sb.AppendLine();
        C4Macros.AppendDynamicMacros(sb);
        sb.AppendLine($"title Dynamic Diagram: {seam.Name}");
        sb.AppendLine();

        // Participants
        C4Macros.AddSystem(sb, SanitizeId(seam.Provider.Alias), seam.Provider.Alias, "Provider workspace");
        C4Macros.AddSystem(sb, $"{SanitizeId(seam.Id)}_contract", "Contract Surface", "Shared contract definitions");

        foreach (var consumer in seam.Consumers)
            C4Macros.AddSystemExt(sb, SanitizeId(consumer.Alias), consumer.Alias, "Consumer workspace");

        sb.AppendLine();

        var step = 1;

        // Use both classified and name-heuristic data
        var services = TypeClassifier.GetServices(surface);
        var pairs = TypeClassifier.GetMessagePairs(surface);
        var registrationElements = surface.InjectionTokens.Concat(surface.Interfaces).Concat(surface.AbstractClasses).ToList();

        // Step 1: Provider registers contract surface
        if (registrationElements.Count > 0)
        {
            var names = string.Join(", ", registrationElements.Select(e => e.Name).Take(3));
            C4Macros.AddRelIndex(sb, step, SanitizeId(seam.Provider.Alias), $"{SanitizeId(seam.Id)}_contract", $"Registers: {names}{(registrationElements.Count > 3 ? "..." : "")}");
        }
        else if (services.Count > 0)
        {
            var names = string.Join(", ", services.Select(s => s.Name).Take(3));
            C4Macros.AddRelIndex(sb, step, SanitizeId(seam.Provider.Alias), $"{SanitizeId(seam.Id)}_contract", $"Registers services: {names}{(services.Count > 3 ? "..." : "")}");
        }
        else
        {
            C4Macros.AddRelIndex(sb, step, SanitizeId(seam.Provider.Alias), $"{SanitizeId(seam.Id)}_contract", "Registers contract surface");
        }
        step++;

        // Step 2: Consumer discovers contract
        foreach (var consumer in seam.Consumers)
        {
            C4Macros.AddRelIndex(sb, step, SanitizeId(consumer.Alias), $"{SanitizeId(seam.Id)}_contract", "Discovers contract");
            step++;
        }

        // Step 3: Service handles messages (from pairs)
        if (pairs.Count > 0)
        {
            foreach (var pair in pairs.Take(4))
            {
                var svc = TypeClassifier.FindServiceForMessage(pair.Request, services);
                var svcName = svc?.Name ?? "Service";
                var source = seam.Consumers.Count > 0 ? SanitizeId(seam.Consumers[0].Alias) : SanitizeId(seam.Provider.Alias);
                C4Macros.AddRelIndex(sb, step, source, SanitizeId(seam.Provider.Alias), $"{svcName}.send({pair.Request.Name})");
                step++;

                if (pair.Response is not null)
                {
                    C4Macros.AddRelIndex(sb, step, SanitizeId(seam.Provider.Alias), source, $"Returns {pair.Response.Name}");
                    step++;
                }
            }
        }
        else if (services.Count > 0)
        {
            // Show service invocation
            var source = seam.Consumers.Count > 0 ? SanitizeId(seam.Consumers[0].Alias) : SanitizeId(seam.Id) + "_contract";
            foreach (var svc in services.Take(3))
            {
                C4Macros.AddRelIndex(sb, step, source, SanitizeId(seam.Provider.Alias), $"Invokes {svc.Name}");
                step++;
            }
        }

        // Step N: Bindings/component interactions
        var bindings = surface.InputBindings.Concat(surface.OutputBindings).Concat(surface.SignalInputs).ToList();
        if (bindings.Count > 0)
        {
            foreach (var consumer in seam.Consumers)
            {
                var names = string.Join(", ", bindings.Select(b => b.Name).Take(3));
                C4Macros.AddRelIndex(sb, step, SanitizeId(consumer.Alias), SanitizeId(seam.Provider.Alias), $"Binds: {names}{(bindings.Count > 3 ? "..." : "")}");
                step++;
            }
        }

        // Step N: Observable data flow
        var observables = surface.Observables.ToList();
        if (observables.Count > 0)
        {
            foreach (var consumer in seam.Consumers)
            {
                var names = string.Join(", ", observables.Select(o => o.Name).Take(3));
                C4Macros.AddRelIndex(sb, step, SanitizeId(seam.Provider.Alias), SanitizeId(consumer.Alias), $"Streams: {names}{(observables.Count > 3 ? "..." : "")}");
                step++;
            }
        }

        sb.AppendLine();
        sb.AppendLine("@enduml");

        return sb.ToString();
    }

    private static string SanitizeId(string name)
    {
        return name.Replace(' ', '_').Replace('-', '_').Replace('.', '_');
    }
}
