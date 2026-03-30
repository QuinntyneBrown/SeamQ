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

        // Collect methods and properties that belong to a service (have a ParentName)
        var serviceMembers = surface.Methods
            .Concat(surface.Properties)
            .Where(e => !string.IsNullOrEmpty(e.ParentName))
            .GroupBy(e => e.ParentName!)
            .ToList();

        // If no classified methods/properties, fall back to name-based heuristic on Types
        if (serviceMembers.Count == 0)
        {
            // Group child elements by parent, or find service-like types
            var serviceTypes = surface.Elements
                .Where(e => e.Name.Contains("Service", StringComparison.OrdinalIgnoreCase) &&
                            e.ParentName is null)
                .ToList();

            var childElements = surface.Elements
                .Where(e => e.ParentName is not null)
                .GroupBy(e => e.ParentName!)
                .ToList();

            if (childElements.Count > 0)
            {
                serviceMembers = childElements;
            }
            else
            {
                // Render service types as standalone classes
                foreach (var svc in serviceTypes)
                {
                    encoder.AddClass(svc.Name);
                }
            }
        }

        var serviceNames = serviceMembers.Select(g => g.Key).ToHashSet();

        // Render each service as a class with its members
        foreach (var group in serviceMembers)
        {
            var members = group.Select(e => FormatMember(e));
            encoder.AddClass(group.Key, members);
        }

        // Add relationships when a method's TypeSignature references another service
        foreach (var group in serviceMembers)
        {
            foreach (var element in group)
            {
                if (string.IsNullOrEmpty(element.TypeSignature))
                    continue;

                foreach (var otherService in serviceNames)
                {
                    if (otherService != group.Key &&
                        element.TypeSignature.Contains(otherService))
                    {
                        encoder.AddRelationship(
                            group.Key, otherService, "-->", "uses");
                    }
                }
            }
        }

        encoder.EndDiagram();
        return encoder.Build();
    }

    private static string FormatMember(ContractElement element)
    {
        var signature = element.TypeSignature ?? "void";
        return element.Kind == ContractElementKind.Method
            ? $"+{element.Name}(): {signature}"
            : $"+{element.Name}: {signature}";
    }
}
