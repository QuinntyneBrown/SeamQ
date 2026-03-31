using SeamQ.Core.Models;

namespace SeamQ.Renderer.PlantUml.ClassDiagrams;

/// <summary>
/// Generates a class diagram showing domain data objects — types, interfaces,
/// and enums used as data shapes at the seam boundary.
/// </summary>
public static class DomainDataObjectsClassDiagram
{
    public static string Generate(Seam seam)
    {
        var encoder = new PlantUmlEncoder();
        encoder.StartClassDiagram($"Domain Data Objects: {seam.Name}");

        var surface = seam.ContractSurface;
        var grouped = TypeClassifier.GroupWithMembers(surface.Elements);

        // Only show data objects — filter out services and components
        var dataObjects = grouped
            .Where(g => TypeClassifier.IsDataObject(g.Parent) &&
                        !TypeClassifier.IsService(g.Parent) &&
                        !TypeClassifier.IsComponent(g.Parent))
            .ToList();

        // Also include data shapes from properly classified elements
        var classifiedTypes = surface.Types.ToList();
        var classifiedEnums = surface.Enumerations.ToList();
        var classifiedInterfaces = surface.Interfaces
            .Where(i => surface.Elements.Any(e =>
                e.ParentName == i.Name &&
                e.Kind is ContractElementKind.Property or ContractElementKind.Method))
            .ToList();

        var shownNames = new HashSet<string>();

        // Show data objects from heuristic classification
        foreach (var (parent, members) in dataObjects)
        {
            if (shownNames.Contains(parent.Name)) continue;

            if (TypeClassifier.IsEnumLike(parent))
            {
                encoder.AddEnum(parent.Name);
            }
            else if (members.Count > 0)
            {
                var memberStrings = members.Select(m =>
                {
                    var name = m.Name.StartsWith(parent.Name + ".") ? m.Name[(parent.Name.Length + 1)..] : m.Name;
                    return $"+{name}: {m.TypeSignature ?? "any"}";
                });
                encoder.AddClass(parent.Name, memberStrings);
            }
            else
            {
                encoder.AddClass(parent.Name);
            }
            shownNames.Add(parent.Name);
        }

        // Show properly classified types
        foreach (var type in classifiedTypes.Where(t => !shownNames.Contains(t.Name)))
        {
            var members = surface.Elements
                .Where(e => e.ParentName == type.Name &&
                            e.Kind is ContractElementKind.Property or ContractElementKind.Method)
                .Select(FormatMember);
            encoder.AddClass(type.Name, members);
            shownNames.Add(type.Name);
        }

        // Show properly classified enums
        foreach (var e in classifiedEnums.Where(e => !shownNames.Contains(e.Name)))
        {
            var values = surface.Elements
                .Where(el => el.ParentName == e.Name && el.Kind == ContractElementKind.Property)
                .Select(el => el.Name);
            encoder.AddEnum(e.Name, values);
            shownNames.Add(e.Name);
        }

        // Show data-shape interfaces
        foreach (var iface in classifiedInterfaces.Where(i => !shownNames.Contains(i.Name)))
        {
            var members = surface.Elements
                .Where(e => e.ParentName == iface.Name &&
                            e.Kind is ContractElementKind.Property or ContractElementKind.Method)
                .Select(FormatMember);
            encoder.AddInterface(iface.Name, members);
            shownNames.Add(iface.Name);
        }

        // Add composition/pairing relationships
        foreach (var (parent, members) in dataObjects)
        {
            foreach (var member in members)
            {
                if (member.TypeSignature is null) continue;
                foreach (var otherName in shownNames)
                {
                    if (otherName != parent.Name && member.TypeSignature.Contains(otherName, StringComparison.Ordinal))
                    {
                        encoder.AddRelationship(parent.Name, otherName, "*--", "contains");
                    }
                }
            }
        }

        // Add request→response pair relationships
        var pairs = TypeClassifier.GetMessagePairs(surface);
        foreach (var pair in pairs)
        {
            if (pair.Response is not null &&
                shownNames.Contains(pair.Request.Name) &&
                shownNames.Contains(pair.Response.Name))
            {
                encoder.AddRelationship(pair.Request.Name, pair.Response.Name, "-->", "produces");
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
