using System.Text;
using SeamQ.Core.Models;

namespace SeamQ.Renderer.C4;

/// <summary>
/// Generates a C4 System Context diagram using C4-PlantUML macros.
/// Shows the system boundary, external users/systems, and relationships.
/// </summary>
public static class C4SystemContext
{
    public static string Generate(Seam seam)
    {
        var sb = new StringBuilder();

        sb.AppendLine("@startuml");
        sb.AppendLine();
        C4Macros.AppendContextMacros(sb);
        sb.AppendLine($"title System Context: {seam.Name}");
        sb.AppendLine();

        // The provider is the central system
        C4Macros.AddSystem(sb, SanitizeId(seam.Provider.Alias), seam.Provider.Alias, "Provider workspace that defines the contract surface");
        sb.AppendLine();

        // Each consumer is an external system or person
        foreach (var consumer in seam.Consumers)
        {
            C4Macros.AddSystemExt(sb, SanitizeId(consumer.Alias), consumer.Alias, "Consumer workspace");
        }

        sb.AppendLine();

        // Relationships
        foreach (var consumer in seam.Consumers)
        {
            var interfaceCount = seam.ContractSurface.Interfaces.Count();
            var tokenCount = seam.ContractSurface.InjectionTokens.Count();
            var description = $"Consumes {interfaceCount} interface(s), {tokenCount} token(s)";
            C4Macros.AddRel(sb, SanitizeId(consumer.Alias), SanitizeId(seam.Provider.Alias), description);
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
