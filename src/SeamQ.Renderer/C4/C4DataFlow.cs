using System.Text;
using SeamQ.Core.Models;

namespace SeamQ.Renderer.C4;

/// <summary>
/// Generates a C4 Container-level diagram showing data flow through the system.
/// Observables represent data flow paths, Actions are trigger points,
/// and Selectors are consumption points for consumer workspaces.
/// </summary>
public static class C4DataFlow
{
    public static string Generate(Seam seam)
    {
        var sb = new StringBuilder();

        sb.AppendLine("@startuml");
        sb.AppendLine("!include https://raw.githubusercontent.com/plantuml-stdlib/C4-PlantUML/master/C4_Container.puml");
        sb.AppendLine();
        sb.AppendLine($"title Data Flow: {seam.Name}");
        sb.AppendLine();

        // Provider workspace as a system boundary with project containers
        sb.AppendLine($"System_Boundary({SanitizeId(seam.Provider.Alias)}_boundary, \"{seam.Provider.Alias}\") {{");

        if (seam.Provider.Projects.Count > 0)
        {
            foreach (var project in seam.Provider.Projects)
            {
                var tech = project.Type == ProjectType.Library ? "Library" : "Application";
                sb.AppendLine($"  Container({SanitizeId(project.Name)}, \"{project.Name}\", \"{tech}\", \"Provider project\")");
            }
        }
        else
        {
            sb.AppendLine($"  Container({SanitizeId(seam.Provider.Alias)}_main, \"{seam.Provider.Alias}\", \"Provider\", \"Provider workspace\")");
        }

        // Observables as data stores / streams
        foreach (var obs in seam.ContractSurface.Observables)
        {
            sb.AppendLine($"  ContainerDb({SanitizeId(obs.Name)}_stream, \"{obs.Name}\", \"Observable\", \"Reactive data stream\")");
        }

        // Actions as trigger containers
        foreach (var action in seam.ContractSurface.Actions)
        {
            sb.AppendLine($"  Container({SanitizeId(action.Name)}_action, \"{action.Name}\", \"Action\", \"State trigger\")");
        }

        sb.AppendLine("}");
        sb.AppendLine();

        // Consumer workspaces
        foreach (var consumer in seam.Consumers)
        {
            sb.AppendLine($"System_Boundary({SanitizeId(consumer.Alias)}_boundary, \"{consumer.Alias}\") {{");

            if (consumer.Projects.Count > 0)
            {
                foreach (var project in consumer.Projects)
                {
                    var tech = project.Type == ProjectType.Library ? "Library" : "Application";
                    sb.AppendLine($"  Container({SanitizeId(project.Name)}, \"{project.Name}\", \"{tech}\", \"Consumer project\")");
                }
            }
            else
            {
                sb.AppendLine($"  Container({SanitizeId(consumer.Alias)}_main, \"{consumer.Alias}\", \"Consumer\", \"Consumer workspace\")");
            }

            sb.AppendLine("}");
            sb.AppendLine();
        }

        // Relationships: Provider projects -> Observables (data production)
        var providerId = seam.Provider.Projects.Count > 0
            ? SanitizeId(seam.Provider.Projects[0].Name)
            : $"{SanitizeId(seam.Provider.Alias)}_main";

        foreach (var obs in seam.ContractSurface.Observables)
        {
            sb.AppendLine($"Rel({providerId}, {SanitizeId(obs.Name)}_stream, \"emits data\", \"Observable\")");
        }

        // Relationships: Provider projects -> Actions (trigger dispatch)
        foreach (var action in seam.ContractSurface.Actions)
        {
            sb.AppendLine($"Rel({providerId}, {SanitizeId(action.Name)}_action, \"dispatches\", \"Action\")");
        }

        // Relationships: Consumers consume data via Selectors
        var selectors = seam.ContractSurface.Selectors.ToList();

        foreach (var consumer in seam.Consumers)
        {
            var consumerId = consumer.Projects.Count > 0
                ? SanitizeId(consumer.Projects[0].Name)
                : $"{SanitizeId(consumer.Alias)}_main";

            if (selectors.Count > 0)
            {
                foreach (var selector in selectors)
                {
                    // Selectors read from observable streams
                    var targetStream = seam.ContractSurface.Observables.FirstOrDefault();
                    if (targetStream != null)
                    {
                        sb.AppendLine($"Rel({consumerId}, {SanitizeId(targetStream.Name)}_stream, \"selects via {selector.Name}\", \"Selector\")");
                    }
                }
            }
            else
            {
                // Fallback: consumers consume observables directly
                foreach (var obs in seam.ContractSurface.Observables)
                {
                    sb.AppendLine($"Rel({consumerId}, {SanitizeId(obs.Name)}_stream, \"subscribes\", \"Observable\")");
                }
            }

            // Consumers can trigger actions
            foreach (var action in seam.ContractSurface.Actions)
            {
                sb.AppendLine($"Rel({consumerId}, {SanitizeId(action.Name)}_action, \"triggers\", \"Action\")");
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
