using System.Text;
using SeamQ.Core.Models;

namespace SeamQ.Renderer.C4;

/// <summary>
/// Generates a C4 Component diagram focused on services.
/// Shows provider workspace components grouped by ParentName, with
/// InjectionTokens and Interfaces as distinct component types.
/// </summary>
public static class C4ComponentServices
{
    public static string Generate(Seam seam)
    {
        var sb = new StringBuilder();

        sb.AppendLine("@startuml");
        sb.AppendLine("!include https://raw.githubusercontent.com/plantuml-stdlib/C4-PlantUML/master/C4_Component.puml");
        sb.AppendLine();
        sb.AppendLine($"title Component Diagram (Services): {seam.Name}");
        sb.AppendLine();

        // Provider workspace as a system boundary
        sb.AppendLine($"System_Boundary({SanitizeId(seam.Provider.Alias)}_boundary, \"{seam.Provider.Alias}\") {{");

        // Collect distinct service components from Methods, Properties, and Observables by ParentName
        var serviceElements = seam.ContractSurface.Methods
            .Concat(seam.ContractSurface.Properties)
            .Concat(seam.ContractSurface.Observables)
            .Where(e => !string.IsNullOrEmpty(e.ParentName))
            .GroupBy(e => e.ParentName!)
            .ToList();

        foreach (var group in serviceElements)
        {
            var memberCount = group.Count();
            sb.AppendLine($"  Component({SanitizeId(group.Key)}, \"{group.Key}\", \"Service\", \"{memberCount} member(s)\")");
        }

        // InjectionTokens as components with <<Token>> tech label
        foreach (var token in seam.ContractSurface.InjectionTokens)
        {
            sb.AppendLine($"  Component({SanitizeId(token.Name)}, \"{token.Name}\", \"<<Token>>\", \"Injection token\")");
        }

        // Interfaces as components with <<Interface>> tech label
        foreach (var iface in seam.ContractSurface.Interfaces)
        {
            sb.AppendLine($"  Component({SanitizeId(iface.Name)}, \"{iface.Name}\", \"<<Interface>>\", \"Contract interface\")");
        }

        sb.AppendLine("}");
        sb.AppendLine();

        // Internal relationships: components that reference each other via TypeSignature
        var allComponents = serviceElements.Select(g => g.Key).ToHashSet();
        foreach (var iface in seam.ContractSurface.Interfaces)
            allComponents.Add(iface.Name);
        foreach (var token in seam.ContractSurface.InjectionTokens)
            allComponents.Add(token.Name);

        foreach (var group in serviceElements)
        {
            foreach (var element in group)
            {
                if (string.IsNullOrEmpty(element.TypeSignature)) continue;

                foreach (var componentName in allComponents)
                {
                    if (componentName == group.Key) continue;
                    if (element.TypeSignature.Contains(componentName))
                    {
                        sb.AppendLine($"Rel({SanitizeId(group.Key)}, {SanitizeId(componentName)}, \"uses\")");
                    }
                }
            }
        }

        // Consumer workspaces as external systems
        foreach (var consumer in seam.Consumers)
        {
            sb.AppendLine($"System_Ext({SanitizeId(consumer.Alias)}, \"{consumer.Alias}\", \"Consumer workspace\")");
        }

        sb.AppendLine();

        // Relationships from consumers to components they use
        foreach (var consumer in seam.Consumers)
        {
            // Consumers interact with interfaces
            foreach (var iface in seam.ContractSurface.Interfaces)
            {
                sb.AppendLine($"Rel({SanitizeId(consumer.Alias)}, {SanitizeId(iface.Name)}, \"implements\")");
            }

            // Consumers interact with injection tokens
            foreach (var token in seam.ContractSurface.InjectionTokens)
            {
                sb.AppendLine($"Rel({SanitizeId(consumer.Alias)}, {SanitizeId(token.Name)}, \"injects\")");
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
