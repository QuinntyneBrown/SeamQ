using System.Text;
using SeamQ.Core.Models;

namespace SeamQ.Renderer.C4;

/// <summary>
/// Generates a C4 Component diagram showing the layered architecture
/// of a plugin API: Registration, Contract, Binding, and Runtime layers.
/// Uses name-based heuristics when elements aren't richly classified.
/// </summary>
public static class C4PluginApiLayers
{
    public static string Generate(Seam seam)
    {
        var sb = new StringBuilder();
        var surface = seam.ContractSurface;

        sb.AppendLine("@startuml");
        sb.AppendLine();
        C4Macros.AppendComponentMacros(sb);
        C4Macros.AppendDirectionalRelMacros(sb);
        sb.AppendLine($"title Plugin API Layers: {seam.Name}");
        sb.AppendLine();

        // Classify elements by layer using both kinds and name heuristics
        var services = TypeClassifier.GetServices(surface);
        var messages = surface.Elements.Where(TypeClassifier.IsRequestMessage).ToList();
        var responses = surface.Elements.Where(TypeClassifier.IsResponse).ToList();
        var enumLikes = surface.Elements.Where(e => e.ParentName is null && TypeClassifier.IsEnumLike(e)).ToList();
        var components = surface.Elements.Where(TypeClassifier.IsComponent).ToList();

        // Registration Layer: InjectionTokens, AbstractClasses, or Services (as registrations)
        C4Macros.BeginBoundary(sb, "registration_layer", "Registration Layer");
        var regCount = 0;

        foreach (var token in surface.InjectionTokens)
        {
            C4Macros.AddComponent(sb, SanitizeId(token.Name), token.Name, "InjectionToken", "Provider token");
            regCount++;
        }
        foreach (var ac in surface.AbstractClasses)
        {
            C4Macros.AddComponent(sb, SanitizeId(ac.Name), ac.Name, "AbstractClass", "Base class");
            regCount++;
        }
        foreach (var svc in services)
        {
            C4Macros.AddComponent(sb, $"{SanitizeId(svc.Name)}_reg", svc.Name, "Service", "Injectable service");
            regCount++;
        }
        if (regCount == 0)
            C4Macros.AddComponent(sb, "reg_none", "(none)", "Registration", "No registrations");

        C4Macros.EndBoundary(sb);
        sb.AppendLine();

        // Contract Layer: Interfaces and Message types (the contract surface)
        C4Macros.BeginBoundary(sb, "contract_layer", "Contract Layer");
        var contractCount = 0;

        foreach (var iface in surface.Interfaces)
        {
            C4Macros.AddComponent(sb, SanitizeId(iface.Name), iface.Name, "Interface", "Contract definition");
            contractCount++;
        }
        foreach (var msg in messages)
        {
            C4Macros.AddComponent(sb, SanitizeId(msg.Name), msg.Name, "Message", "Request type");
            contractCount++;
        }
        foreach (var resp in responses)
        {
            C4Macros.AddComponent(sb, SanitizeId(resp.Name), resp.Name, "Response", "Response type");
            contractCount++;
        }
        if (contractCount == 0)
            C4Macros.AddComponent(sb, "contract_none", "(none)", "Interface", "No contracts");

        C4Macros.EndBoundary(sb);
        sb.AppendLine();

        // Binding Layer: InputBindings, OutputBindings, SignalInputs, Components
        C4Macros.BeginBoundary(sb, "binding_layer", "Binding Layer");
        var bindingCount = 0;

        foreach (var input in surface.InputBindings)
        {
            C4Macros.AddComponent(sb, $"{SanitizeId(input.Name)}_in", input.Name, "InputBinding", "Data input");
            bindingCount++;
        }
        foreach (var output in surface.OutputBindings)
        {
            C4Macros.AddComponent(sb, $"{SanitizeId(output.Name)}_out", output.Name, "OutputBinding", "Event output");
            bindingCount++;
        }
        foreach (var comp in components)
        {
            C4Macros.AddComponent(sb, $"{SanitizeId(comp.Name)}_comp", comp.Name, "Component", "UI component");
            bindingCount++;
        }
        foreach (var e in enumLikes)
        {
            C4Macros.AddComponent(sb, $"{SanitizeId(e.Name)}_enum", e.Name, "Enum", "State/status type");
            bindingCount++;
        }
        if (bindingCount == 0)
            C4Macros.AddComponent(sb, "binding_none", "(none)", "Binding", "No bindings");

        C4Macros.EndBoundary(sb);
        sb.AppendLine();

        // Runtime Layer: Methods, Observables, or remaining data types
        sb.AppendLine("System_Boundary(runtime_layer, \"Runtime Layer\") {");
        var runtimeCount = 0;

        foreach (var method in surface.Methods)
        {
            sb.AppendLine($"  Component({SanitizeId(method.Name)}_rt, \"{method.Name}\", \"Method\", \"Runtime method\")");
            runtimeCount++;
        }
        foreach (var obs in surface.Observables)
        {
            sb.AppendLine($"  Component({SanitizeId(obs.Name)}_rt, \"{obs.Name}\", \"Observable\", \"Data stream\")");
            runtimeCount++;
        }
        // If no classified runtime elements, show data objects as the runtime data
        if (runtimeCount == 0)
        {
            var dataTypes = surface.Elements
                .Where(e => e.ParentName is null && TypeClassifier.IsDataObject(e) &&
                            !TypeClassifier.IsService(e) && !TypeClassifier.IsComponent(e) &&
                            !TypeClassifier.IsEnumLike(e) && !TypeClassifier.IsRequestMessage(e) &&
                            !TypeClassifier.IsResponse(e))
                .Take(5)
                .ToList();

            foreach (var dt in dataTypes)
            {
                sb.AppendLine($"  Component({SanitizeId(dt.Name)}_rt, \"{dt.Name}\", \"DataType\", \"Runtime data object\")");
                runtimeCount++;
            }
        }
        if (runtimeCount == 0)
            sb.AppendLine("  Component(runtime_none, \"(none)\", \"Runtime\", \"No runtime elements\")");

        sb.AppendLine("}");
        sb.AppendLine();

        // Relationships flow top-down
        if (regCount > 0 && contractCount > 0)
            sb.AppendLine("Rel_D(registration_layer, contract_layer, \"defines\")");
        if (contractCount > 0 && bindingCount > 0)
            sb.AppendLine("Rel_D(contract_layer, binding_layer, \"binds\")");
        if (bindingCount > 0 && runtimeCount > 0)
            sb.AppendLine("Rel_D(binding_layer, runtime_layer, \"invokes\")");

        sb.AppendLine();
        sb.AppendLine("@enduml");

        return sb.ToString();
    }

    private static string SanitizeId(string name)
    {
        return name.Replace(' ', '_').Replace('-', '_').Replace('.', '_');
    }
}
