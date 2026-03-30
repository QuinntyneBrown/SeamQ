using System.Text;
using SeamQ.Core.Models;

namespace SeamQ.Renderer.C4;

/// <summary>
/// Generates a C4 Container diagram using C4-PlantUML macros.
/// Shows the containers (provider workspace projects, consumer workspaces)
/// and how they interact through the contract surface.
/// </summary>
public static class C4Container
{
    public static string Generate(Seam seam)
    {
        var sb = new StringBuilder();

        sb.AppendLine("@startuml");
        sb.AppendLine();
        C4Macros.AppendContainerMacros(sb);
        sb.AppendLine($"title Container Diagram: {seam.Name}");
        sb.AppendLine();

        // Provider boundary
        C4Macros.BeginBoundary(sb, $"{SanitizeId(seam.Provider.Alias)}_boundary", seam.Provider.Alias);

        // Each project in the provider is a container
        if (seam.Provider.Projects.Count > 0)
        {
            foreach (var project in seam.Provider.Projects)
            {
                var tech = project.Type == ProjectType.Library ? "Library" : "Application";
                C4Macros.AddContainer(sb, SanitizeId(project.Name), project.Name, tech, "Exports contract elements");
            }
        }
        else
        {
            // Fallback: represent the provider as a single container
            C4Macros.AddContainer(sb, $"{SanitizeId(seam.Provider.Alias)}_main", seam.Provider.Alias, "Provider", "Defines the contract surface");
        }

        // Contract surface container
        C4Macros.AddContainerDb(sb, $"{SanitizeId(seam.Id)}_contract", "Contract Surface", "Interfaces/Tokens", $"{seam.ContractSurface.Elements.Count} element(s)");
        C4Macros.EndBoundary(sb);
        sb.AppendLine();

        // Consumer containers
        foreach (var consumer in seam.Consumers)
        {
            C4Macros.BeginBoundary(sb, $"{SanitizeId(consumer.Alias)}_boundary", consumer.Alias);
            if (consumer.Projects.Count > 0)
            {
                foreach (var project in consumer.Projects)
                {
                    var tech = project.Type == ProjectType.Library ? "Library" : "Application";
                    C4Macros.AddContainer(sb, SanitizeId(project.Name), project.Name, tech, "Consumes contract");
                }
            }
            else
            {
                C4Macros.AddContainer(sb, $"{SanitizeId(consumer.Alias)}_main", consumer.Alias, "Consumer", "Implements contract surface");
            }
            C4Macros.EndBoundary(sb);
            sb.AppendLine();
        }

        // Relationships between consumers and the contract surface
        foreach (var consumer in seam.Consumers)
        {
            var consumerId = consumer.Projects.Count > 0
                ? SanitizeId(consumer.Projects[0].Name)
                : $"{SanitizeId(consumer.Alias)}_main";

            C4Macros.AddRel(sb, consumerId, $"{SanitizeId(seam.Id)}_contract", "implements/uses");
        }

        // Relationship from provider projects to contract surface
        if (seam.Provider.Projects.Count > 0)
        {
            C4Macros.AddRel(sb, SanitizeId(seam.Provider.Projects[0].Name), $"{SanitizeId(seam.Id)}_contract", "defines");
        }
        else
        {
            C4Macros.AddRel(sb, $"{SanitizeId(seam.Provider.Alias)}_main", $"{SanitizeId(seam.Id)}_contract", "defines");
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
