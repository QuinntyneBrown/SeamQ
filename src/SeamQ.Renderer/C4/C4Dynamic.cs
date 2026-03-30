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

        sb.AppendLine("@startuml");
        sb.AppendLine("!include https://raw.githubusercontent.com/plantuml-stdlib/C4-PlantUML/master/C4_Dynamic.puml");
        sb.AppendLine();
        sb.AppendLine($"title Dynamic Diagram: {seam.Name}");
        sb.AppendLine();

        // Participants
        sb.AppendLine($"System({SanitizeId(seam.Provider.Alias)}, \"{seam.Provider.Alias}\", \"Provider workspace\")");
        sb.AppendLine($"System({SanitizeId(seam.Id)}_contract, \"Contract Surface\", \"Shared contract definitions\")");

        foreach (var consumer in seam.Consumers)
        {
            sb.AppendLine($"System_Ext({SanitizeId(consumer.Alias)}, \"{consumer.Alias}\", \"Consumer workspace\")");
        }

        sb.AppendLine();

        var step = 1;

        // Step 1: Provider registers contracts
        var registrationElements = seam.ContractSurface.InjectionTokens
            .Concat(seam.ContractSurface.Interfaces)
            .Concat(seam.ContractSurface.AbstractClasses)
            .ToList();

        if (registrationElements.Count > 0)
        {
            var names = string.Join(", ", registrationElements.Select(e => e.Name).Take(3));
            var suffix = registrationElements.Count > 3 ? "..." : "";
            sb.AppendLine($"RelIndex({step}, {SanitizeId(seam.Provider.Alias)}, {SanitizeId(seam.Id)}_contract, \"Registers contracts: {names}{suffix}\")");
        }
        else
        {
            sb.AppendLine($"RelIndex({step}, {SanitizeId(seam.Provider.Alias)}, {SanitizeId(seam.Id)}_contract, \"Registers contract surface\")");
        }

        step++;

        // Step 2: Consumer discovers contract
        foreach (var consumer in seam.Consumers)
        {
            sb.AppendLine($"RelIndex({step}, {SanitizeId(consumer.Alias)}, {SanitizeId(seam.Id)}_contract, \"Discovers contract\")");
            step++;
        }

        // Step 3: Consumer binds inputs
        var bindings = seam.ContractSurface.InputBindings
            .Concat(seam.ContractSurface.OutputBindings)
            .Concat(seam.ContractSurface.SignalInputs)
            .ToList();

        if (bindings.Count > 0)
        {
            foreach (var consumer in seam.Consumers)
            {
                var bindingNames = string.Join(", ", bindings.Select(b => b.Name).Take(3));
                var suffix = bindings.Count > 3 ? "..." : "";
                sb.AppendLine($"RelIndex({step}, {SanitizeId(consumer.Alias)}, {SanitizeId(seam.Provider.Alias)}, \"Binds inputs: {bindingNames}{suffix}\")");
                step++;
            }
        }

        // Step 4: Runtime method calls
        var methods = seam.ContractSurface.Methods.ToList();
        if (methods.Count > 0)
        {
            foreach (var consumer in seam.Consumers)
            {
                var methodNames = string.Join(", ", methods.Select(m => m.Name).Take(3));
                var suffix = methods.Count > 3 ? "..." : "";
                sb.AppendLine($"RelIndex({step}, {SanitizeId(consumer.Alias)}, {SanitizeId(seam.Provider.Alias)}, \"Calls methods: {methodNames}{suffix}\")");
                step++;
            }
        }

        // Step 5: Observable data flow
        var observables = seam.ContractSurface.Observables.ToList();
        if (observables.Count > 0)
        {
            foreach (var consumer in seam.Consumers)
            {
                var obsNames = string.Join(", ", observables.Select(o => o.Name).Take(3));
                var suffix = observables.Count > 3 ? "..." : "";
                sb.AppendLine($"RelIndex({step}, {SanitizeId(seam.Provider.Alias)}, {SanitizeId(consumer.Alias)}, \"Streams data: {obsNames}{suffix}\")");
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
