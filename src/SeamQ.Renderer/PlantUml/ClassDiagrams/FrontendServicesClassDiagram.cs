using SeamQ.Core.Models;

namespace SeamQ.Renderer.PlantUml.ClassDiagrams;

/// <summary>
/// Generates a class diagram showing frontend services with their methods and properties.
/// </summary>
public static class FrontendServicesClassDiagram
{
    public static string Generate(Seam seam)
    {
        var encoder = new PlantUmlEncoder();
        encoder.StartClassDiagram($"Frontend Services: {seam.Name}");

        var surface = seam.ContractSurface;

        // Use TypeClassifier to get service types and group with members
        var services = TypeClassifier.GetServices(surface);
        var grouped = TypeClassifier.GroupWithMembers(surface.Elements);

        // Show services with their members
        foreach (var svc in services)
        {
            var entry = grouped.FirstOrDefault(g => g.Parent.Name == svc.Name);
            var members = entry.Members.Count > 0
                ? entry.Members.Select(FormatMember)
                : null;
            encoder.AddClass(svc.Name, members);
        }

        // Show components that have members (they consume services)
        var components = grouped
            .Where(g => TypeClassifier.IsComponent(g.Parent) && g.Members.Count > 0)
            .ToList();

        foreach (var comp in components)
        {
            encoder.AddClass(comp.Parent.Name, comp.Members.Select(FormatMember));
        }

        // Add relationships: components → services (via member TypeSignature)
        var serviceNames = services.Select(s => s.Name).ToHashSet();
        foreach (var comp in components)
        {
            foreach (var member in comp.Members)
            {
                if (member.TypeSignature is null) continue;
                foreach (var svcName in serviceNames)
                {
                    if (member.TypeSignature.Contains(svcName, StringComparison.Ordinal))
                    {
                        encoder.AddRelationship(comp.Parent.Name, svcName, "-->", "uses");
                    }
                }
            }
        }

        // Show message pairs: Service → handles → Message/Response
        var pairs = TypeClassifier.GetMessagePairs(surface);
        foreach (var pair in pairs)
        {
            var svc = TypeClassifier.FindServiceForMessage(pair.Request, services);
            if (svc is not null)
            {
                encoder.AddRelationship(svc.Name, pair.Request.Name, "-->", "handles");
                if (pair.Response is not null)
                {
                    encoder.AddRelationship(svc.Name, pair.Response.Name, "-->", "returns");
                }
            }
        }

        encoder.EndDiagram();
        return encoder.Build();
    }

    private static string FormatMember(ContractElement element)
    {
        var signature = element.TypeSignature ?? "void";
        // Strip parent prefix from member name for readability
        var name = element.ParentName is not null && element.Name.StartsWith(element.ParentName + ".")
            ? element.Name[(element.ParentName.Length + 1)..]
            : element.Name;
        return element.Kind == ContractElementKind.Method
            ? $"+{name}(): {signature}"
            : $"+{name}: {signature}";
    }
}
