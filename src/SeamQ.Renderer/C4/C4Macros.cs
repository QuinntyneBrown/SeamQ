using System.Text;

namespace SeamQ.Renderer.C4;

/// <summary>
/// Helper methods that emit plain PlantUML syntax for C4-style diagrams.
/// Produces standalone output with no macros, no includes — just standard
/// PlantUML rectangles, databases, and arrows that render everywhere.
/// </summary>
internal static class C4Macros
{
    public static void AppendSkinParams(StringBuilder sb)
    {
        sb.AppendLine("skinparam rectangle {");
        sb.AppendLine("  StereotypeFontSize 12");
        sb.AppendLine("  shadowing false");
        sb.AppendLine("}");
        sb.AppendLine();
    }

    // --- Shape helpers ---

    public static void AddSystem(StringBuilder sb, string id, string label, string description)
    {
        sb.AppendLine($"rectangle \"=={label}\\n\\n{description}\" <<system>> as {id} #438DD5");
    }

    public static void AddSystemExt(StringBuilder sb, string id, string label, string description)
    {
        sb.AppendLine($"rectangle \"=={label}\\n\\n{description}\" <<external>> as {id} #999999");
    }

    public static void AddPerson(StringBuilder sb, string id, string label, string description)
    {
        sb.AppendLine($"rectangle \"=={label}\\n\\n{description}\" <<person>> as {id} #08427B");
    }

    public static void BeginBoundary(StringBuilder sb, string id, string label)
    {
        sb.AppendLine($"rectangle \"{label}\" <<boundary>> as {id} {{");
    }

    public static void EndBoundary(StringBuilder sb)
    {
        sb.AppendLine("}");
    }

    public static void AddContainer(StringBuilder sb, string id, string label, string tech, string description)
    {
        sb.AppendLine($"  rectangle \"=={label}\\n//<size:10>[{tech}]</size>//\\n\\n{description}\" <<container>> as {id} #438DD5");
    }

    public static void AddContainerDb(StringBuilder sb, string id, string label, string tech, string description)
    {
        sb.AppendLine($"  database \"=={label}\\n//<size:10>[{tech}]</size>//\\n\\n{description}\" <<database>> as {id} #438DD5");
    }

    public static void AddComponent(StringBuilder sb, string id, string label, string tech, string description)
    {
        sb.AppendLine($"  rectangle \"=={label}\\n//<size:10>[{tech}]</size>//\\n\\n{description}\" <<component>> as {id} #85BBF0");
    }

    // --- Relationship helpers ---

    public static void AddRel(StringBuilder sb, string from, string to, string label)
    {
        sb.AppendLine($"{from} --> {to} : {label}");
    }

    public static void AddRel(StringBuilder sb, string from, string to, string label, string tech)
    {
        sb.AppendLine($"{from} --> {to} : {label}\\n//<size:10>[{tech}]</size>//");
    }

    public static void AddRelDown(StringBuilder sb, string from, string to, string label)
    {
        sb.AppendLine($"{from} -down-> {to} : {label}");
    }

    public static void AddRelIndex(StringBuilder sb, int index, string from, string to, string label)
    {
        sb.AppendLine($"{from} --> {to} : **{index}.** {label}");
    }

    // --- Convenience: emit standard preamble for C4-style diagrams ---

    public static void AppendContextMacros(StringBuilder sb) => AppendSkinParams(sb);
    public static void AppendContainerMacros(StringBuilder sb) => AppendSkinParams(sb);
    public static void AppendComponentMacros(StringBuilder sb) => AppendSkinParams(sb);
    public static void AppendDynamicMacros(StringBuilder sb) => AppendSkinParams(sb);
    public static void AppendDirectionalRelMacros(StringBuilder sb) { /* no-op, helpers used directly */ }
}
