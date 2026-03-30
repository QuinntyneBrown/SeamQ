using SeamQ.Core.Models;

namespace SeamQ.Renderer.PlantUml.ClassDiagrams;

/// <summary>
/// Generates a class diagram showing the registration system with injection tokens,
/// abstract classes, and provider/consumer workspace packages.
/// </summary>
public static class RegistrationSystemClassDiagram
{
    public static string Generate(Seam seam)
    {
        var encoder = new PlantUmlEncoder();
        encoder.StartClassDiagram($"Registration System: {seam.Name}");

        var surface = seam.ContractSurface;

        // Show provider workspace as a package containing tokens and abstract classes
        encoder.AddRawLine($"package \"{seam.Provider.Alias}\" as provider {{");

        foreach (var token in surface.InjectionTokens)
        {
            encoder.AddRawLine($"  class {SanitizeName(token.Name)} <<InjectionToken>> {{");
            if (!string.IsNullOrEmpty(token.TypeSignature))
            {
                encoder.AddRawLine($"    {token.TypeSignature}");
            }
            encoder.AddRawLine("  }");
        }

        foreach (var abstractClass in surface.AbstractClasses)
        {
            var members = surface.Elements
                .Where(e => e.ParentName == abstractClass.Name &&
                            e.Kind is ContractElementKind.Method or ContractElementKind.Property)
                .ToList();

            encoder.AddRawLine($"  abstract class {SanitizeName(abstractClass.Name)} {{");
            foreach (var member in members)
            {
                encoder.AddRawLine($"    {FormatMember(member)}");
            }
            encoder.AddRawLine("  }");
        }

        encoder.AddRawLine("}");
        encoder.AddBlankLine();

        // Show each consumer workspace as a package
        foreach (var consumer in seam.Consumers)
        {
            encoder.AddRawLine($"package \"{consumer.Alias}\" as {SanitizeName(consumer.Alias)} {{");
            encoder.AddRawLine("}");
            encoder.AddBlankLine();

            // Add dependency relationships from consumer to each injection token
            foreach (var token in surface.InjectionTokens)
            {
                encoder.AddRelationship(
                    SanitizeName(consumer.Alias),
                    SanitizeName(token.Name),
                    "..>",
                    "depends on");
            }

            // Add dependency relationships from consumer to each abstract class
            foreach (var abstractClass in surface.AbstractClasses)
            {
                encoder.AddRelationship(
                    SanitizeName(consumer.Alias),
                    SanitizeName(abstractClass.Name),
                    "..>",
                    "extends");
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
