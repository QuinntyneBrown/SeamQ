using System.Text;
using SeamQ.Core.Models;

namespace SeamQ.Renderer.C4;

/// <summary>
/// Generates a C4 Component diagram showing the layered architecture
/// of a plugin API: Registration, Contract, Binding, and Runtime layers.
/// </summary>
public static class C4PluginApiLayers
{
    public static string Generate(Seam seam)
    {
        var sb = new StringBuilder();

        sb.AppendLine("@startuml");
        sb.AppendLine("!include https://raw.githubusercontent.com/plantuml-stdlib/C4-PlantUML/master/C4_Component.puml");
        sb.AppendLine();
        sb.AppendLine($"title Plugin API Layers: {seam.Name}");
        sb.AppendLine();

        // Registration Layer: InjectionTokens and AbstractClasses
        sb.AppendLine($"System_Boundary(registration_layer, \"Registration Layer\") {{");

        foreach (var token in seam.ContractSurface.InjectionTokens)
        {
            sb.AppendLine($"  Component({SanitizeId(token.Name)}, \"{token.Name}\", \"InjectionToken\", \"Provider registration token\")");
        }

        foreach (var ac in seam.ContractSurface.AbstractClasses)
        {
            sb.AppendLine($"  Component({SanitizeId(ac.Name)}, \"{ac.Name}\", \"AbstractClass\", \"Base contract class\")");
        }

        if (!seam.ContractSurface.InjectionTokens.Any() && !seam.ContractSurface.AbstractClasses.Any())
        {
            sb.AppendLine("  Component(reg_placeholder, \"(none)\", \"Registration\", \"No registration elements\")");
        }

        sb.AppendLine("}");
        sb.AppendLine();

        // Contract Layer: Interfaces
        sb.AppendLine($"System_Boundary(contract_layer, \"Contract Layer\") {{");

        foreach (var iface in seam.ContractSurface.Interfaces)
        {
            sb.AppendLine($"  Component({SanitizeId(iface.Name)}, \"{iface.Name}\", \"Interface\", \"Contract definition\")");
        }

        if (!seam.ContractSurface.Interfaces.Any())
        {
            sb.AppendLine("  Component(contract_placeholder, \"(none)\", \"Interface\", \"No contract interfaces\")");
        }

        sb.AppendLine("}");
        sb.AppendLine();

        // Binding Layer: InputBindings, OutputBindings, SignalInputs
        sb.AppendLine($"System_Boundary(binding_layer, \"Binding Layer\") {{");

        foreach (var input in seam.ContractSurface.InputBindings)
        {
            sb.AppendLine($"  Component({SanitizeId(input.Name)}_input, \"{input.Name}\", \"InputBinding\", \"Data input binding\")");
        }

        foreach (var output in seam.ContractSurface.OutputBindings)
        {
            sb.AppendLine($"  Component({SanitizeId(output.Name)}_output, \"{output.Name}\", \"OutputBinding\", \"Event output binding\")");
        }

        foreach (var signal in seam.ContractSurface.SignalInputs)
        {
            sb.AppendLine($"  Component({SanitizeId(signal.Name)}_signal, \"{signal.Name}\", \"SignalInput\", \"Signal-based input\")");
        }

        if (!seam.ContractSurface.InputBindings.Any() && !seam.ContractSurface.OutputBindings.Any() && !seam.ContractSurface.SignalInputs.Any())
        {
            sb.AppendLine("  Component(binding_placeholder, \"(none)\", \"Binding\", \"No binding elements\")");
        }

        sb.AppendLine("}");
        sb.AppendLine();

        // Runtime Layer: Methods, Observables
        sb.AppendLine($"System_Boundary(runtime_layer, \"Runtime Layer\") {{");

        foreach (var method in seam.ContractSurface.Methods)
        {
            sb.AppendLine($"  Component({SanitizeId(method.Name)}_method, \"{method.Name}\", \"Method\", \"Runtime method call\")");
        }

        foreach (var obs in seam.ContractSurface.Observables)
        {
            sb.AppendLine($"  Component({SanitizeId(obs.Name)}_obs, \"{obs.Name}\", \"Observable\", \"Reactive data stream\")");
        }

        if (!seam.ContractSurface.Methods.Any() && !seam.ContractSurface.Observables.Any())
        {
            sb.AppendLine("  Component(runtime_placeholder, \"(none)\", \"Runtime\", \"No runtime elements\")");
        }

        sb.AppendLine("}");
        sb.AppendLine();

        // Relationships flow top-down between layers
        // Registration -> Contract
        foreach (var iface in seam.ContractSurface.Interfaces)
        {
            var registrationSource = seam.ContractSurface.InjectionTokens.FirstOrDefault()
                ?? seam.ContractSurface.AbstractClasses.FirstOrDefault();

            if (registrationSource != null)
            {
                sb.AppendLine($"Rel_D({SanitizeId(registrationSource.Name)}, {SanitizeId(iface.Name)}, \"defines contract\")");
            }
        }

        // Contract -> Binding
        var firstInterface = seam.ContractSurface.Interfaces.FirstOrDefault();
        if (firstInterface != null)
        {
            foreach (var input in seam.ContractSurface.InputBindings)
            {
                sb.AppendLine($"Rel_D({SanitizeId(firstInterface.Name)}, {SanitizeId(input.Name)}_input, \"binds input\")");
            }

            foreach (var output in seam.ContractSurface.OutputBindings)
            {
                sb.AppendLine($"Rel_D({SanitizeId(firstInterface.Name)}, {SanitizeId(output.Name)}_output, \"binds output\")");
            }

            foreach (var signal in seam.ContractSurface.SignalInputs)
            {
                sb.AppendLine($"Rel_D({SanitizeId(firstInterface.Name)}, {SanitizeId(signal.Name)}_signal, \"binds signal\")");
            }
        }

        // Binding -> Runtime
        var firstBinding = seam.ContractSurface.InputBindings.FirstOrDefault()
            ?? seam.ContractSurface.OutputBindings.FirstOrDefault();
        var bindingId = firstBinding != null
            ? $"{SanitizeId(firstBinding.Name)}_{(firstBinding.Kind == ContractElementKind.InputBinding ? "input" : "output")}"
            : null;

        if (bindingId != null)
        {
            foreach (var method in seam.ContractSurface.Methods)
            {
                sb.AppendLine($"Rel_D({bindingId}, {SanitizeId(method.Name)}_method, \"invokes at runtime\")");
            }

            foreach (var obs in seam.ContractSurface.Observables)
            {
                sb.AppendLine($"Rel_D({bindingId}, {SanitizeId(obs.Name)}_obs, \"streams data\")");
            }
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
