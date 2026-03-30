using SeamQ.Core.Models;

namespace SeamQ.Renderer.PlantUml.ClassDiagrams;

/// <summary>
/// Generates a class diagram showing message interfaces: request/response types,
/// actions, observables, and selectors.
/// </summary>
public static class MessageInterfacesClassDiagram
{
    public static string Generate(Seam seam)
    {
        var encoder = new PlantUmlEncoder();
        encoder.StartClassDiagram($"Message Interfaces: {seam.Name}");

        var surface = seam.ContractSurface;

        // Show properly classified actions
        foreach (var action in surface.Actions)
        {
            encoder.AddRawLine($"class {SanitizeName(action.Name)} <<Action>> {{");
            if (!string.IsNullOrEmpty(action.TypeSignature))
                encoder.AddRawLine($"  {action.TypeSignature}");
            encoder.AddRawLine("}");
            encoder.AddBlankLine();
        }

        // Show properly classified observables
        foreach (var observable in surface.Observables)
        {
            encoder.AddRawLine($"class {SanitizeName(observable.Name)} <<Observable>> {{");
            if (!string.IsNullOrEmpty(observable.TypeSignature))
                encoder.AddRawLine($"  {observable.TypeSignature}");
            encoder.AddRawLine("}");
            encoder.AddBlankLine();
        }

        // Show properly classified selectors
        foreach (var selector in surface.Selectors)
        {
            encoder.AddRawLine($"class {SanitizeName(selector.Name)} <<Selector>> {{");
            if (!string.IsNullOrEmpty(selector.TypeSignature))
                encoder.AddRawLine($"  {selector.TypeSignature}");
            encoder.AddRawLine("}");
            encoder.AddBlankLine();
        }

        // Fall back to name-based heuristic: show Message/Request types as <<Message>>,
        // Response types as <<Response>>
        var messages = surface.Elements.Where(TypeClassifier.IsRequestMessage).ToList();
        var responses = surface.Elements.Where(TypeClassifier.IsResponse).ToList();

        if (messages.Count > 0 || responses.Count > 0)
        {
            encoder.AddRawLine("' --- Message Types ---");
            encoder.AddBlankLine();

            foreach (var msg in messages)
            {
                var members = TypeClassifier.GroupWithMembers(surface.Elements)
                    .FirstOrDefault(g => g.Parent.Name == msg.Name);

                encoder.AddRawLine($"class {SanitizeName(msg.Name)} <<Message>> {{");
                if (members.Members is not null)
                {
                    foreach (var m in members.Members)
                    {
                        var name = m.Name.StartsWith(msg.Name + ".") ? m.Name[(msg.Name.Length + 1)..] : m.Name;
                        encoder.AddRawLine($"  +{name}: {m.TypeSignature ?? "any"}");
                    }
                }
                encoder.AddRawLine("}");
                encoder.AddBlankLine();
            }

            foreach (var resp in responses)
            {
                var members = TypeClassifier.GroupWithMembers(surface.Elements)
                    .FirstOrDefault(g => g.Parent.Name == resp.Name);

                encoder.AddRawLine($"class {SanitizeName(resp.Name)} <<Response>> {{");
                if (members.Members is not null)
                {
                    foreach (var m in members.Members)
                    {
                        var name = m.Name.StartsWith(resp.Name + ".") ? m.Name[(resp.Name.Length + 1)..] : m.Name;
                        encoder.AddRawLine($"  +{name}: {m.TypeSignature ?? "any"}");
                    }
                }
                encoder.AddRawLine("}");
                encoder.AddBlankLine();
            }

            // Add request → response relationships
            var pairs = TypeClassifier.GetMessagePairs(surface);
            foreach (var pair in pairs)
            {
                if (pair.Response is not null)
                {
                    encoder.AddRelationship(
                        SanitizeName(pair.Request.Name),
                        SanitizeName(pair.Response.Name),
                        "-->", "produces");
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
