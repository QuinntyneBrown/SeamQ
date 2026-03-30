using System.Text;
using SeamQ.Core.Models;

namespace SeamQ.Renderer.C4;

/// <summary>
/// Generates a C4 Container-level diagram showing data flow through the system.
/// Shows message/response flows between services, with name-heuristic fallbacks.
/// </summary>
public static class C4DataFlow
{
    public static string Generate(Seam seam)
    {
        var sb = new StringBuilder();
        var surface = seam.ContractSurface;

        sb.AppendLine("@startuml");
        sb.AppendLine();
        C4Macros.AppendContainerMacros(sb);
        sb.AppendLine($"title Data Flow: {seam.Name}");
        sb.AppendLine();

        // Provider workspace boundary
        C4Macros.BeginBoundary(sb, $"{SanitizeId(seam.Provider.Alias)}_boundary", seam.Provider.Alias);

        // Services as containers
        var services = TypeClassifier.GetServices(surface);
        foreach (var svc in services)
            C4Macros.AddContainer(sb, SanitizeId(svc.Name), svc.Name, "Service", "API service");

        // Messages as data containers
        var messages = surface.Elements.Where(TypeClassifier.IsRequestMessage).ToList();
        foreach (var msg in messages)
            C4Macros.AddContainerDb(sb, $"{SanitizeId(msg.Name)}_data", msg.Name, "Message", "Request payload");

        // Responses as data containers
        var responses = surface.Elements.Where(TypeClassifier.IsResponse).ToList();
        foreach (var resp in responses)
            C4Macros.AddContainerDb(sb, $"{SanitizeId(resp.Name)}_data", resp.Name, "Response", "Response payload");

        // Observables as data streams
        foreach (var obs in surface.Observables)
            C4Macros.AddContainerDb(sb, $"{SanitizeId(obs.Name)}_stream", obs.Name, "Observable", "Data stream");

        // Actions as trigger containers
        foreach (var action in surface.Actions)
            C4Macros.AddContainer(sb, $"{SanitizeId(action.Name)}_action", action.Name, "Action", "State trigger");

        // If nothing specific, show projects
        if (services.Count == 0 && messages.Count == 0 && !surface.Observables.Any() && !surface.Actions.Any())
        {
            foreach (var project in seam.Provider.Projects)
            {
                var tech = project.Type == ProjectType.Library ? "Library" : "Application";
                C4Macros.AddContainer(sb, SanitizeId(project.Name), project.Name, tech, "Provider project");
            }
        }

        C4Macros.EndBoundary(sb);
        sb.AppendLine();

        // Consumer workspaces
        foreach (var consumer in seam.Consumers)
        {
            C4Macros.AddSystemExt(sb, SanitizeId(consumer.Alias), consumer.Alias, "Consumer workspace");
        }

        sb.AppendLine();

        // Relationships: services ↔ message/response pairs
        var pairs = TypeClassifier.GetMessagePairs(surface);
        foreach (var pair in pairs)
        {
            var svc = TypeClassifier.FindServiceForMessage(pair.Request, services);
            if (svc is not null)
            {
                sb.AppendLine($"Rel({SanitizeId(svc.Name)}, {SanitizeId(pair.Request.Name)}_data, \"accepts\", \"Input\")");
                if (pair.Response is not null)
                    sb.AppendLine($"Rel({SanitizeId(svc.Name)}, {SanitizeId(pair.Response.Name)}_data, \"returns\", \"Output\")");
            }
        }

        // Relationships: consumers → services
        foreach (var consumer in seam.Consumers)
        {
            foreach (var svc in services.Take(3))
                sb.AppendLine($"Rel({SanitizeId(consumer.Alias)}, {SanitizeId(svc.Name)}, \"uses\")");
        }

        // Observable data flow relationships
        foreach (var obs in surface.Observables)
        {
            var firstSvc = services.FirstOrDefault();
            if (firstSvc is not null)
                sb.AppendLine($"Rel({SanitizeId(firstSvc.Name)}, {SanitizeId(obs.Name)}_stream, \"emits\", \"Observable\")");

            foreach (var consumer in seam.Consumers)
                sb.AppendLine($"Rel({SanitizeId(consumer.Alias)}, {SanitizeId(obs.Name)}_stream, \"subscribes\")");
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
