using System.Text;

namespace SeamQ.Renderer.C4;

/// <summary>
/// Provides inline C4-PlantUML macro definitions so diagrams are fully
/// standalone and render without network access or external includes.
/// </summary>
internal static class C4Macros
{
    /// <summary>
    /// Appends C4 Context-level macros (System, System_Ext, Person, Rel, etc.).
    /// </summary>
    public static void AppendContextMacros(StringBuilder sb)
    {
        sb.AppendLine("' C4 Context macros (standalone)");
        sb.AppendLine("skinparam rectangle {");
        sb.AppendLine("  StereotypeFontSize 12");
        sb.AppendLine("  shadowing false");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("!define LAYOUT_WITH_LEGEND");
        sb.AppendLine("!define C4_STDLIB");
        sb.AppendLine();

        // Colors
        sb.AppendLine("!define C4_BG_COLOR #438DD5");
        sb.AppendLine("!define C4_EXT_BG_COLOR #999999");
        sb.AppendLine("!define C4_PERSON_BG #08427B");
        sb.AppendLine("!define C4_BORDER #3C7FC0");
        sb.AppendLine();

        // Core shapes
        sb.AppendLine("!procedure System($alias, $label, $descr)");
        sb.AppendLine("  rectangle \"==$label\\n\\n$descr\" <<system>> as $alias #C4_BG_COLOR");
        sb.AppendLine("!endprocedure");
        sb.AppendLine();

        sb.AppendLine("!procedure System_Ext($alias, $label, $descr)");
        sb.AppendLine("  rectangle \"==$label\\n\\n$descr\" <<external>> as $alias #C4_EXT_BG_COLOR");
        sb.AppendLine("!endprocedure");
        sb.AppendLine();

        sb.AppendLine("!procedure Person($alias, $label, $descr)");
        sb.AppendLine("  rectangle \"==$label\\n\\n$descr\" <<person>> as $alias #C4_PERSON_BG");
        sb.AppendLine("!endprocedure");
        sb.AppendLine();

        // Boundaries
        sb.AppendLine("!procedure System_Boundary($alias, $label)");
        sb.AppendLine("  rectangle $label <<boundary>> as $alias {");
        sb.AppendLine("!endprocedure");
        sb.AppendLine();

        // Relationships
        sb.AppendLine("!procedure Rel($from, $to, $label)");
        sb.AppendLine("  $from --> $to : $label");
        sb.AppendLine("!endprocedure");
        sb.AppendLine();

        sb.AppendLine("!procedure Rel($from, $to, $label, $techn)");
        sb.AppendLine("  $from --> $to : $label\\n//<size:10>[$techn]</size>//");
        sb.AppendLine("!endprocedure");
        sb.AppendLine();
    }

    /// <summary>
    /// Appends C4 Container-level macros (Container, ContainerDb, plus Context macros).
    /// </summary>
    public static void AppendContainerMacros(StringBuilder sb)
    {
        AppendContextMacros(sb);

        sb.AppendLine("' C4 Container macros");
        sb.AppendLine("!procedure Container($alias, $label, $techn, $descr)");
        sb.AppendLine("  rectangle \"==$label\\n//<size:10>[$techn]</size>//\\n\\n$descr\" <<container>> as $alias #438DD5");
        sb.AppendLine("!endprocedure");
        sb.AppendLine();

        sb.AppendLine("!procedure ContainerDb($alias, $label, $techn, $descr)");
        sb.AppendLine("  database \"==$label\\n//<size:10>[$techn]</size>//\\n\\n$descr\" <<database>> as $alias #438DD5");
        sb.AppendLine("!endprocedure");
        sb.AppendLine();
    }

    /// <summary>
    /// Appends C4 Component-level macros (Component, plus Container and Context macros).
    /// </summary>
    public static void AppendComponentMacros(StringBuilder sb)
    {
        AppendContainerMacros(sb);

        sb.AppendLine("' C4 Component macros");
        sb.AppendLine("!procedure Component($alias, $label, $techn, $descr)");
        sb.AppendLine("  rectangle \"==$label\\n//<size:10>[$techn]</size>//\\n\\n$descr\" <<component>> as $alias #85BBF0");
        sb.AppendLine("!endprocedure");
        sb.AppendLine();
    }

    /// <summary>
    /// Appends C4 Dynamic diagram macros (RelIndex, plus Context macros).
    /// </summary>
    public static void AppendDynamicMacros(StringBuilder sb)
    {
        AppendContextMacros(sb);

        sb.AppendLine("' C4 Dynamic macros");
        sb.AppendLine("!procedure RelIndex($index, $from, $to, $label)");
        sb.AppendLine("  $from --> $to : **$index.** $label");
        sb.AppendLine("!endprocedure");
        sb.AppendLine();
    }

    /// <summary>
    /// Appends directional relationship macros (Rel_D, Rel_R, Rel_L, Rel_U).
    /// </summary>
    public static void AppendDirectionalRelMacros(StringBuilder sb)
    {
        sb.AppendLine("' Directional relationship macros");
        sb.AppendLine("!procedure Rel_D($from, $to, $label)");
        sb.AppendLine("  $from -down-> $to : $label");
        sb.AppendLine("!endprocedure");
        sb.AppendLine();

        sb.AppendLine("!procedure Rel_R($from, $to, $label)");
        sb.AppendLine("  $from -right-> $to : $label");
        sb.AppendLine("!endprocedure");
        sb.AppendLine();

        sb.AppendLine("!procedure Rel_L($from, $to, $label)");
        sb.AppendLine("  $from -left-> $to : $label");
        sb.AppendLine("!endprocedure");
        sb.AppendLine();

        sb.AppendLine("!procedure Rel_U($from, $to, $label)");
        sb.AppendLine("  $from -up-> $to : $label");
        sb.AppendLine("!endprocedure");
        sb.AppendLine();
    }
}
