using SeamQ.Core.Models;

namespace SeamQ.Renderer.PlantUml.ClassDiagrams;

/// <summary>
/// Generates a class diagram showing realtime communication: observables grouped by service,
/// input/output bindings on components, and publish/subscribe relationships.
/// </summary>
public static class RealtimeCommunicationClassDiagram
{
    public static string Generate(Seam seam)
    {
        var encoder = new PlantUmlEncoder();
        encoder.StartClassDiagram($"Realtime Communication: {seam.Name}");

        var surface = seam.ContractSurface;

        // Group observables by ParentName (service)
        var groupedObservables = surface.Observables
            .GroupBy(o => o.ParentName ?? string.Empty)
            .ToList();

        var observableNames = surface.Observables.Select(o => o.Name).ToHashSet();

        foreach (var group in groupedObservables)
        {
            if (!string.IsNullOrEmpty(group.Key))
            {
                // Render the service as a class containing its observables as members
                var members = group.Select(o =>
                {
                    var sig = o.TypeSignature ?? "Observable";
                    return $"+{o.Name}: {sig}";
                });

                encoder.AddClass(group.Key, members);
            }
            else
            {
                // Standalone observables without a parent service
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
            }
        }

        // Group input and output bindings by ParentName (component)
        var bindingComponents = surface.InputBindings
            .Concat(surface.OutputBindings)
            .Where(b => !string.IsNullOrEmpty(b.ParentName))
            .GroupBy(b => b.ParentName!)
            .ToList();

        foreach (var component in bindingComponents)
        {
            var members = component.Select(b =>
            {
                var sig = b.TypeSignature ?? "any";
                var prefix = b.Kind == ContractElementKind.InputBinding ? "<<@Input>>" : "<<@Output>>";
                return $"+{b.Name}: {sig} {prefix}";
            });

            encoder.AddClass(component.Key, members);
        }

        // Add publish/subscribe relationships
        // Output bindings publish to observables when TypeSignature references an observable
        foreach (var binding in surface.OutputBindings.Where(b => !string.IsNullOrEmpty(b.ParentName)))
        {
            foreach (var observableName in observableNames)
            {
                if (binding.TypeSignature?.Contains(observableName) == true)
                {
                    encoder.AddRelationship(
                        binding.ParentName!, SanitizeName(observableName), "-->", "publishes");
                }
            }
        }

        // Input bindings subscribe from observables when TypeSignature references an observable
        foreach (var binding in surface.InputBindings.Where(b => !string.IsNullOrEmpty(b.ParentName)))
        {
            foreach (var observableName in observableNames)
            {
                if (binding.TypeSignature?.Contains(observableName) == true)
                {
                    encoder.AddRelationship(
                        SanitizeName(observableName), binding.ParentName!, "-->", "subscribes");
                }
            }
        }

        // Add relationships from consumer workspaces to observable services
        foreach (var consumer in seam.Consumers)
        {
            foreach (var group in groupedObservables.Where(g => !string.IsNullOrEmpty(g.Key)))
            {
                encoder.AddRelationship(
                    SanitizeName(consumer.Alias), group.Key, "..>", "subscribes");
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
