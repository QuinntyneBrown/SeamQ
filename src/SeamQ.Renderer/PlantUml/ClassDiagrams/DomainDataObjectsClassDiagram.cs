using SeamQ.Core.Models;

namespace SeamQ.Renderer.PlantUml.ClassDiagrams;

/// <summary>
/// Generates a class diagram showing domain data objects, enums, and data-shape interfaces.
/// </summary>
public static class DomainDataObjectsClassDiagram
{
    public static string Generate(Seam seam)
    {
        var encoder = new PlantUmlEncoder();
        encoder.StartClassDiagram($"Domain Data Objects: {seam.Name}");

        var surface = seam.ContractSurface;

        // Collect all type and enum names for relationship detection
        var allTypeNames = surface.Types
            .Select(t => t.Name)
            .Concat(surface.Interfaces
                .Where(i => HasDataMembers(surface, i))
                .Select(i => i.Name))
            .ToHashSet();

        // Show non-enum types as classes with their child properties
        foreach (var type in surface.Types.Where(t => t.Kind != ContractElementKind.Enum))
        {
            var members = surface.Elements
                .Where(e => e.ParentName == type.Name &&
                            e.Kind is ContractElementKind.Property or ContractElementKind.Method)
                .Select(e => FormatMember(e));

            encoder.AddClass(type.Name, members);
        }

        // Show enums with their child values
        foreach (var enumType in surface.Types.Where(t => t.Kind == ContractElementKind.Enum))
        {
            var values = surface.Elements
                .Where(e => e.ParentName == enumType.Name && e.Kind == ContractElementKind.Property)
                .Select(e => e.Name);

            encoder.AddEnum(enumType.Name, values);
        }

        // Show interfaces that have property/method children (data shapes)
        foreach (var iface in surface.Interfaces.Where(i => HasDataMembers(surface, i)))
        {
            var members = surface.Elements
                .Where(e => e.ParentName == iface.Name &&
                            e.Kind is ContractElementKind.Property or ContractElementKind.Method)
                .Select(e => FormatMember(e));

            encoder.AddInterface(iface.Name, members);
        }

        // Add composition relationships when a property's TypeSignature references another type
        foreach (var element in surface.Elements.Where(e =>
            e.Kind == ContractElementKind.Property &&
            !string.IsNullOrEmpty(e.ParentName) &&
            !string.IsNullOrEmpty(e.TypeSignature)))
        {
            foreach (var typeName in allTypeNames)
            {
                if (typeName != element.ParentName &&
                    element.TypeSignature!.Contains(typeName))
                {
                    encoder.AddRelationship(
                        element.ParentName!, typeName, "*--", "contains");
                }
            }
        }

        encoder.EndDiagram();
        return encoder.Build();
    }

    private static bool HasDataMembers(ContractSurface surface, ContractElement iface)
    {
        return surface.Elements.Any(e =>
            e.ParentName == iface.Name &&
            e.Kind is ContractElementKind.Property or ContractElementKind.Method);
    }

    private static string FormatMember(ContractElement element)
    {
        var signature = element.TypeSignature ?? "void";
        return element.Kind == ContractElementKind.Method
            ? $"+{element.Name}(): {signature}"
            : $"+{element.Name}: {signature}";
    }
}
