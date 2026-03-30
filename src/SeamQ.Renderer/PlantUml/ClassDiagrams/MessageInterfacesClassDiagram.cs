using SeamQ.Core.Models;

namespace SeamQ.Renderer.PlantUml.ClassDiagrams;

/// <summary>
/// Generates a class diagram showing message interfaces: actions, observables, and selectors.
/// </summary>
public static class MessageInterfacesClassDiagram
{
    public static string Generate(Seam seam)
    {
        var encoder = new PlantUmlEncoder();
        encoder.StartClassDiagram($"Message Interfaces: {seam.Name}");

        var surface = seam.ContractSurface;

        // Group actions by ParentName if available, otherwise render standalone
        var groupedActions = surface.Actions
            .GroupBy(a => a.ParentName ?? string.Empty)
            .ToList();

        foreach (var group in groupedActions)
        {
            if (!string.IsNullOrEmpty(group.Key))
            {
                encoder.AddRawLine($"package \"{group.Key}\" {{");
            }

            foreach (var action in group)
            {
                encoder.AddRawLine($"class {SanitizeName(action.Name)} <<Action>> {{");
                if (!string.IsNullOrEmpty(action.TypeSignature))
                {
                    encoder.AddRawLine($"  {action.TypeSignature}");
                }
                encoder.AddRawLine("}");
                encoder.AddBlankLine();
            }

            if (!string.IsNullOrEmpty(group.Key))
            {
                encoder.AddRawLine("}");
                encoder.AddBlankLine();
            }
        }

        // Group observables by ParentName if available, otherwise render standalone
        var groupedObservables = surface.Observables
            .GroupBy(o => o.ParentName ?? string.Empty)
            .ToList();

        foreach (var group in groupedObservables)
        {
            if (!string.IsNullOrEmpty(group.Key))
            {
                encoder.AddRawLine($"package \"{group.Key}\" {{");
            }

            foreach (var observable in group)
            {
                encoder.AddRawLine($"class {SanitizeName(observable.Name)} <<Observable>> {{");
                if (!string.IsNullOrEmpty(observable.TypeSignature))
                {
                    encoder.AddRawLine($"  {observable.TypeSignature}");
                }
                encoder.AddRawLine("}");
                encoder.AddBlankLine();
            }

            if (!string.IsNullOrEmpty(group.Key))
            {
                encoder.AddRawLine("}");
                encoder.AddBlankLine();
            }
        }

        // Show selectors as stereotyped classes
        foreach (var selector in surface.Selectors)
        {
            encoder.AddRawLine($"class {SanitizeName(selector.Name)} <<Selector>> {{");
            if (!string.IsNullOrEmpty(selector.TypeSignature))
            {
                encoder.AddRawLine($"  {selector.TypeSignature}");
            }
            encoder.AddRawLine("}");
            encoder.AddBlankLine();
        }

        // Add relationships: selectors reading from observables/actions when TypeSignature matches
        foreach (var selector in surface.Selectors)
        {
            if (string.IsNullOrEmpty(selector.TypeSignature))
                continue;

            foreach (var action in surface.Actions)
            {
                if (selector.TypeSignature.Contains(action.Name))
                {
                    encoder.AddRelationship(
                        SanitizeName(selector.Name),
                        SanitizeName(action.Name),
                        "..>",
                        "reads");
                }
            }

            foreach (var observable in surface.Observables)
            {
                if (selector.TypeSignature.Contains(observable.Name))
                {
                    encoder.AddRelationship(
                        SanitizeName(selector.Name),
                        SanitizeName(observable.Name),
                        "..>",
                        "reads");
                }
            }
        }

        encoder.EndDiagram();
        return encoder.Build();
    }

    private static string SanitizeName(string name)
    {
        return name.Replace('<', '_').Replace('>', '_').Replace(' ', '_').Replace('-', '_');
    }
}
