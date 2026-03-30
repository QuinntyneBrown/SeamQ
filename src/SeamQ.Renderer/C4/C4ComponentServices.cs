using System.Text;
using SeamQ.Core.Models;

namespace SeamQ.Renderer.C4;

/// <summary>
/// Generates a C4 Component diagram focused on services.
/// Shows provider workspace components grouped by ParentName or name-classified types.
/// </summary>
public static class C4ComponentServices
{
    public static string Generate(Seam seam)
    {
        var sb = new StringBuilder();

        sb.AppendLine("@startuml");
        sb.AppendLine();
        C4Macros.AppendComponentMacros(sb);
        sb.AppendLine($"title Component Diagram (Services): {seam.Name}");
        sb.AppendLine();

        sb.AppendLine($"System_Boundary({SanitizeId(seam.Provider.Alias)}_boundary, \"{seam.Provider.Alias}\") {{");

        var surface = seam.ContractSurface;

        // Collect classified service components from Methods/Properties/Observables
        var serviceElements = surface.Methods
            .Concat(surface.Properties)
            .Concat(surface.Observables)
            .Where(e => !string.IsNullOrEmpty(e.ParentName))
            .GroupBy(e => e.ParentName!)
            .ToList();

        var shownComponents = new HashSet<string>();

        foreach (var group in serviceElements)
        {
            sb.AppendLine($"  Component({SanitizeId(group.Key)}, \"{group.Key}\", \"Service\", \"{group.Count()} member(s)\")");
            shownComponents.Add(group.Key);
        }

        // Fall back: name-classified services, components, and data types
        if (serviceElements.Count == 0)
        {
            var services = TypeClassifier.GetServices(surface);
            foreach (var svc in services)
            {
                if (shownComponents.Add(svc.Name))
                    sb.AppendLine($"  Component({SanitizeId(svc.Name)}, \"{svc.Name}\", \"Service\", \"API service\")");
            }

            var components = surface.Elements.Where(e => TypeClassifier.IsComponent(e)).ToList();
            foreach (var comp in components)
            {
                if (shownComponents.Add(comp.Name))
                    sb.AppendLine($"  Component({SanitizeId(comp.Name)}, \"{comp.Name}\", \"Component\", \"UI component\")");
            }

            // Show key data types as components
            var messages = surface.Elements.Where(TypeClassifier.IsRequestMessage).Take(5).ToList();
            foreach (var msg in messages)
            {
                if (shownComponents.Add(msg.Name))
                    sb.AppendLine($"  Component({SanitizeId(msg.Name)}, \"{msg.Name}\", \"Message\", \"Request type\")");
            }

            var responses = surface.Elements.Where(TypeClassifier.IsResponse).Take(5).ToList();
            foreach (var resp in responses)
            {
                if (shownComponents.Add(resp.Name))
                    sb.AppendLine($"  Component({SanitizeId(resp.Name)}, \"{resp.Name}\", \"Response\", \"Response type\")");
            }
        }

        // InjectionTokens
        foreach (var token in surface.InjectionTokens)
        {
            if (shownComponents.Add(token.Name))
                sb.AppendLine($"  Component({SanitizeId(token.Name)}, \"{token.Name}\", \"<<Token>>\", \"Injection token\")");
        }

        // Interfaces
        foreach (var iface in surface.Interfaces)
        {
            if (shownComponents.Add(iface.Name))
                sb.AppendLine($"  Component({SanitizeId(iface.Name)}, \"{iface.Name}\", \"<<Interface>>\", \"Contract interface\")");
        }

        sb.AppendLine("}");
        sb.AppendLine();

        // Add relationships: services → messages they handle
        var pairs = TypeClassifier.GetMessagePairs(surface);
        var svcs = TypeClassifier.GetServices(surface);
        foreach (var pair in pairs)
        {
            var svc = TypeClassifier.FindServiceForMessage(pair.Request, svcs);
            if (svc is not null && shownComponents.Contains(svc.Name) && shownComponents.Contains(pair.Request.Name))
            {
                sb.AppendLine($"Rel({SanitizeId(svc.Name)}, {SanitizeId(pair.Request.Name)}, \"handles\")");
                if (pair.Response is not null && shownComponents.Contains(pair.Response.Name))
                    sb.AppendLine($"Rel({SanitizeId(svc.Name)}, {SanitizeId(pair.Response.Name)}, \"returns\")");
            }
        }

        // Consumer workspaces as external systems
        foreach (var consumer in seam.Consumers)
        {
            sb.AppendLine($"System_Ext({SanitizeId(consumer.Alias)}, \"{consumer.Alias}\", \"Consumer workspace\")");
            foreach (var svc in svcs.Take(3))
            {
                if (shownComponents.Contains(svc.Name))
                    sb.AppendLine($"Rel({SanitizeId(consumer.Alias)}, {SanitizeId(svc.Name)}, \"uses\")");
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
