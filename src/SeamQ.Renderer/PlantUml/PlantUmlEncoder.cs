using System.Text;

namespace SeamQ.Renderer.PlantUml;

/// <summary>
/// Utility class that builds valid PlantUML syntax strings.
/// </summary>
public sealed class PlantUmlEncoder
{
    private readonly StringBuilder _sb = new();

    public PlantUmlEncoder StartClassDiagram(string title)
    {
        _sb.AppendLine("@startuml");
        _sb.AppendLine($"title {title}");
        _sb.AppendLine("skinparam classAttributeIconSize 0");
        _sb.AppendLine();
        return this;
    }

    public PlantUmlEncoder StartSequenceDiagram(string title)
    {
        _sb.AppendLine("@startuml");
        _sb.AppendLine($"title {title}");
        _sb.AppendLine();
        return this;
    }

    public PlantUmlEncoder AddClass(string name, IEnumerable<string>? members = null)
    {
        _sb.AppendLine($"class {SanitizeName(name)} {{");
        if (members is not null)
        {
            foreach (var member in members)
            {
                _sb.AppendLine($"  {member}");
            }
        }
        _sb.AppendLine("}");
        _sb.AppendLine();
        return this;
    }

    public PlantUmlEncoder AddInterface(string name, IEnumerable<string>? members = null)
    {
        _sb.AppendLine($"interface {SanitizeName(name)} {{");
        if (members is not null)
        {
            foreach (var member in members)
            {
                _sb.AppendLine($"  {member}");
            }
        }
        _sb.AppendLine("}");
        _sb.AppendLine();
        return this;
    }

    public PlantUmlEncoder AddEnum(string name, IEnumerable<string>? values = null)
    {
        _sb.AppendLine($"enum {SanitizeName(name)} {{");
        if (values is not null)
        {
            foreach (var value in values)
            {
                _sb.AppendLine($"  {value}");
            }
        }
        _sb.AppendLine("}");
        _sb.AppendLine();
        return this;
    }

    public PlantUmlEncoder AddRelationship(string from, string to, string relationType = "-->", string? label = null)
    {
        var line = $"{SanitizeName(from)} {relationType} {SanitizeName(to)}";
        if (!string.IsNullOrEmpty(label))
        {
            line += $" : {label}";
        }
        _sb.AppendLine(line);
        return this;
    }

    public PlantUmlEncoder AddParticipant(string name, string? alias = null)
    {
        if (alias is not null)
        {
            _sb.AppendLine($"participant \"{name}\" as {alias}");
        }
        else
        {
            _sb.AppendLine($"participant \"{name}\"");
        }
        return this;
    }

    public PlantUmlEncoder AddMessage(string from, string to, string message, bool isReturn = false)
    {
        var arrow = isReturn ? "-->" : "->";
        _sb.AppendLine($"\"{from}\" {arrow} \"{to}\" : {message}");
        return this;
    }

    public PlantUmlEncoder AddActivation(string participant)
    {
        _sb.AppendLine($"activate \"{participant}\"");
        return this;
    }

    public PlantUmlEncoder AddDeactivation(string participant)
    {
        _sb.AppendLine($"deactivate \"{participant}\"");
        return this;
    }

    public PlantUmlEncoder AddNote(string text, string position = "right")
    {
        _sb.AppendLine($"note {position}");
        _sb.AppendLine($"  {text}");
        _sb.AppendLine("end note");
        return this;
    }

    public PlantUmlEncoder AddRawLine(string line)
    {
        _sb.AppendLine(line);
        return this;
    }

    public PlantUmlEncoder AddBlankLine()
    {
        _sb.AppendLine();
        return this;
    }

    public PlantUmlEncoder EndDiagram()
    {
        _sb.AppendLine("@enduml");
        return this;
    }

    public string Build() => _sb.ToString();

    private static string SanitizeName(string name)
    {
        // Replace characters that are invalid in PlantUML identifiers
        return name.Replace('<', '_').Replace('>', '_').Replace(' ', '_').Replace('-', '_');
    }
}
