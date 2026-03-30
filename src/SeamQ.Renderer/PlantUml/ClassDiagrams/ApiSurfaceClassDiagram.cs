using SeamQ.Core.Models;

namespace SeamQ.Renderer.PlantUml.ClassDiagrams;

/// <summary>
/// Generates the API surface class diagram from ContractSurface elements.
/// </summary>
public static class ApiSurfaceClassDiagram
{
    public static string Generate(Seam seam)
    {
        var encoder = new PlantUmlEncoder();
        encoder.StartClassDiagram($"API Surface: {seam.Name}");

        var surface = seam.ContractSurface;

        // Add interfaces
        foreach (var iface in surface.Interfaces)
        {
            var members = surface.Elements
                .Where(e => e.ParentName == iface.Name &&
                            e.Kind is ContractElementKind.Method or ContractElementKind.Property)
                .Select(e => FormatMember(e));

            encoder.AddInterface(iface.Name, members);
        }

        // Add abstract classes
        foreach (var abstractClass in surface.AbstractClasses)
        {
            var members = surface.Elements
                .Where(e => e.ParentName == abstractClass.Name &&
                            e.Kind is ContractElementKind.Method or ContractElementKind.Property)
                .Select(e => FormatMember(e));

            encoder.AddClass($"abstract {abstractClass.Name}", members);
        }

        // Add types and enums
        foreach (var type in surface.Types)
        {
            if (type.Kind == ContractElementKind.Enum)
            {
                encoder.AddEnum(type.Name);
            }
            else
            {
                encoder.AddClass(type.Name);
            }
        }

        // Add injection tokens as stereotyped classes
        foreach (var token in surface.InjectionTokens)
        {
            encoder.AddRawLine($"class {SanitizeName(token.Name)} <<InjectionToken>> {{");
            if (!string.IsNullOrEmpty(token.TypeSignature))
            {
                encoder.AddRawLine($"  {token.TypeSignature}");
            }
            encoder.AddRawLine("}");
            encoder.AddBlankLine();
        }

        // Add relationships: interface implementations detected from workspace context
        foreach (var iface in surface.Interfaces)
        {
            foreach (var abstractClass in surface.AbstractClasses)
            {
                // If an abstract class references the interface in its type signature
                if (abstractClass.TypeSignature?.Contains(iface.Name) == true)
                {
                    encoder.AddRelationship(abstractClass.Name, iface.Name, "..|>", "implements");
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

    private static string SanitizeName(string name)
    {
        return name.Replace('<', '_').Replace('>', '_').Replace(' ', '_').Replace('-', '_');
    }
}
