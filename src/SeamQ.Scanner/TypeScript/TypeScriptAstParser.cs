using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using SeamQ.Core.Models;

namespace SeamQ.Scanner.TypeScript;

/// <summary>
/// Simplified TypeScript parser using regex patterns to extract interfaces, classes,
/// decorators, bindings, enums, type aliases, and inject() calls from .ts source files.
/// This is NOT a full AST parser — it handles common patterns found in Angular codebases.
/// </summary>
public partial class TypeScriptAstParser
{
    private readonly ILogger<TypeScriptAstParser> _logger;

    #region Regex patterns

    // export interface Foo extends Bar, Baz { ... }
    [GeneratedRegex(@"^(\s*)export\s+interface\s+(\w+)(?:\s+extends\s+([\w\s,.<>]+))?\s*\{",
        RegexOptions.Multiline)]
    private static partial Regex InterfaceRegex();

    // export (abstract)? class Foo extends Bar implements Baz { ... }
    [GeneratedRegex(
        @"^(\s*)export\s+(?:abstract\s+)?class\s+(\w+)(?:\s+extends\s+([\w.<>]+))?(?:\s+implements\s+([\w\s,.<>]+))?\s*\{",
        RegexOptions.Multiline)]
    private static partial Regex ClassRegex();

    // export abstract class Foo
    [GeneratedRegex(@"^(\s*)export\s+abstract\s+class\s+(\w+)", RegexOptions.Multiline)]
    private static partial Regex AbstractClassRegex();

    // export enum Foo { ... }
    [GeneratedRegex(@"^(\s*)export\s+enum\s+(\w+)\s*\{", RegexOptions.Multiline)]
    private static partial Regex EnumRegex();

    // export type Foo = ...
    [GeneratedRegex(@"^(\s*)export\s+type\s+(\w+)(?:<[^>]*>)?\s*=\s*(.+)", RegexOptions.Multiline)]
    private static partial Regex TypeAliasRegex();

    // @Component({ ... })  @Injectable({ ... })  @Directive({ ... })
    [GeneratedRegex(@"@(Component|Injectable|Directive|Pipe|NgModule)\s*\(", RegexOptions.Multiline)]
    private static partial Regex DecoratorRegex();

    // @Input() propertyName  or  @Input('alias') propertyName
    [GeneratedRegex(@"@Input\([^)]*\)\s*(?:set\s+)?(\w+)\s*[!?]?\s*[:;=]?\s*(.*?)$", RegexOptions.Multiline)]
    private static partial Regex InputDecoratorRegex();

    // @Output() propertyName
    [GeneratedRegex(@"@Output\([^)]*\)\s*(\w+)\s*[!?]?\s*[:=]", RegexOptions.Multiline)]
    private static partial Regex OutputDecoratorRegex();

    // Signal inputs: input(), input<Type>(), input.required<Type>(), model(), model<Type>()
    [GeneratedRegex(@"(\w+)\s*=\s*(input|model)(?:\.required)?\s*(?:<([^>]*)>)?\s*\(", RegexOptions.Multiline)]
    private static partial Regex SignalInputRegex();

    // inject(ServiceName) or inject<ServiceName>(...)
    [GeneratedRegex(@"(\w+)\s*=\s*inject\s*(?:<([^>]*)>)?\s*\(\s*(\w+)?", RegexOptions.Multiline)]
    private static partial Regex InjectCallRegex();

    // Interface/class member: propertyName: Type;  or  propertyName?: Type;
    [GeneratedRegex(@"^\s+(\w+)\s*[?!]?\s*:\s*(.+?)\s*;", RegexOptions.Multiline)]
    private static partial Regex PropertyMemberRegex();

    // Interface/class method: methodName(params): ReturnType;  or  methodName(params): ReturnType { ... }
    [GeneratedRegex(@"^\s+(\w+)\s*(?:<[^>]*>)?\s*\(([^)]*)\)\s*:\s*([^;{]+)", RegexOptions.Multiline)]
    private static partial Regex MethodMemberRegex();

    // selector: 'my-component' inside decorator metadata
    [GeneratedRegex(@"selector\s*:\s*['""]([^'""]+)['""]")]
    private static partial Regex SelectorRegex();

    #endregion

    public TypeScriptAstParser(ILogger<TypeScriptAstParser> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Parses a TypeScript source file and returns all discovered exported symbols.
    /// </summary>
    public async Task<IReadOnlyList<ParsedDeclaration>> ParseFileAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
        {
            return [];
        }

        try
        {
            var content = await File.ReadAllTextAsync(filePath, cancellationToken);
            return ParseContent(content, filePath);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse TypeScript file {Path}", filePath);
            return [];
        }
    }

    /// <summary>
    /// Parses TypeScript content and returns declarations.
    /// </summary>
    public IReadOnlyList<ParsedDeclaration> ParseContent(string content, string filePath)
    {
        var declarations = new List<ParsedDeclaration>();
        var lines = content.Split('\n');

        // Parse interfaces
        foreach (Match match in InterfaceRegex().Matches(content))
        {
            var lineNumber = GetLineNumber(content, match.Index);
            var name = match.Groups[2].Value;
            var extends_ = match.Groups[3].Success
                ? match.Groups[3].Value.Split(',', StringSplitOptions.TrimEntries)
                : [];

            var bodyContent = ExtractBlock(content, match.Index + match.Length - 1);
            var members = ParseMembers(bodyContent);
            var doc = TsDocParser.ExtractDocComment(lines, lineNumber - 1);

            declarations.Add(new ParsedDeclaration
            {
                Name = name,
                Kind = DeclarationKind.Interface,
                FilePath = filePath,
                LineNumber = lineNumber,
                Extends = extends_,
                Members = members,
                Documentation = doc
            });
        }

        // Parse classes (including abstract)
        foreach (Match match in ClassRegex().Matches(content))
        {
            var lineNumber = GetLineNumber(content, match.Index);
            var name = match.Groups[2].Value;
            var extends_ = match.Groups[3].Success ? [match.Groups[3].Value.Trim()] : Array.Empty<string>();
            var implements_ = match.Groups[4].Success
                ? match.Groups[4].Value.Split(',', StringSplitOptions.TrimEntries)
                : [];

            var isAbstract = AbstractClassRegex().IsMatch(content.Substring(match.Index, match.Length));
            var bodyContent = ExtractBlock(content, match.Index + match.Length - 1);
            var members = ParseMembers(bodyContent);
            var doc = TsDocParser.ExtractDocComment(lines, lineNumber - 1);

            // Check for Angular decorator on lines preceding the class
            var decorator = FindDecoratorAbove(lines, lineNumber - 1);
            var decoratorMetadata = decorator is not null
                ? ExtractDecoratorMetadata(content, lineNumber)
                : null;

            // Parse signal inputs, inject calls, @Input/@Output within the class body
            var inputs = ParseInputBindings(bodyContent, filePath, lineNumber);
            var outputs = ParseOutputBindings(bodyContent, filePath, lineNumber);
            var signalInputs = ParseSignalInputs(bodyContent, filePath, lineNumber);
            var injectCalls = ParseInjectCalls(bodyContent, filePath, lineNumber);

            declarations.Add(new ParsedDeclaration
            {
                Name = name,
                Kind = isAbstract ? DeclarationKind.AbstractClass : DeclarationKind.Class,
                FilePath = filePath,
                LineNumber = lineNumber,
                Extends = extends_,
                Implements = implements_,
                Members = [..members, ..inputs, ..outputs, ..signalInputs, ..injectCalls],
                Decorator = decorator,
                DecoratorMetadata = decoratorMetadata,
                Documentation = doc
            });
        }

        // Parse enums
        foreach (Match match in EnumRegex().Matches(content))
        {
            var lineNumber = GetLineNumber(content, match.Index);
            var name = match.Groups[2].Value;
            var doc = TsDocParser.ExtractDocComment(lines, lineNumber - 1);

            declarations.Add(new ParsedDeclaration
            {
                Name = name,
                Kind = DeclarationKind.Enum,
                FilePath = filePath,
                LineNumber = lineNumber,
                Documentation = doc
            });
        }

        // Parse type aliases
        foreach (Match match in TypeAliasRegex().Matches(content))
        {
            var lineNumber = GetLineNumber(content, match.Index);
            var name = match.Groups[2].Value;
            var typeExpression = match.Groups[3].Value.TrimEnd(';').Trim();
            var doc = TsDocParser.ExtractDocComment(lines, lineNumber - 1);

            declarations.Add(new ParsedDeclaration
            {
                Name = name,
                Kind = DeclarationKind.TypeAlias,
                FilePath = filePath,
                LineNumber = lineNumber,
                TypeSignature = typeExpression,
                Documentation = doc
            });
        }

        return declarations;
    }

    private List<ParsedMember> ParseMembers(string blockContent)
    {
        var members = new List<ParsedMember>();

        // Methods first (more specific pattern)
        foreach (Match match in MethodMemberRegex().Matches(blockContent))
        {
            members.Add(new ParsedMember
            {
                Name = match.Groups[1].Value,
                Kind = MemberKind.Method,
                TypeSignature = $"({match.Groups[2].Value.Trim()}): {match.Groups[3].Value.Trim()}"
            });
        }

        // Properties
        var methodNames = members.Select(m => m.Name).ToHashSet();
        foreach (Match match in PropertyMemberRegex().Matches(blockContent))
        {
            var name = match.Groups[1].Value;
            if (methodNames.Contains(name))
                continue; // Already captured as a method

            members.Add(new ParsedMember
            {
                Name = name,
                Kind = MemberKind.Property,
                TypeSignature = match.Groups[2].Value.Trim()
            });
        }

        return members;
    }

    private List<ParsedMember> ParseInputBindings(string blockContent, string filePath, int classLine)
    {
        var members = new List<ParsedMember>();
        foreach (Match match in InputDecoratorRegex().Matches(blockContent))
        {
            members.Add(new ParsedMember
            {
                Name = match.Groups[1].Value,
                Kind = MemberKind.InputBinding,
                TypeSignature = match.Groups[2].Value.TrimEnd(';').Trim()
            });
        }
        return members;
    }

    private List<ParsedMember> ParseOutputBindings(string blockContent, string filePath, int classLine)
    {
        var members = new List<ParsedMember>();
        foreach (Match match in OutputDecoratorRegex().Matches(blockContent))
        {
            members.Add(new ParsedMember
            {
                Name = match.Groups[1].Value,
                Kind = MemberKind.OutputBinding
            });
        }
        return members;
    }

    private List<ParsedMember> ParseSignalInputs(string blockContent, string filePath, int classLine)
    {
        var members = new List<ParsedMember>();
        foreach (Match match in SignalInputRegex().Matches(blockContent))
        {
            var name = match.Groups[1].Value;
            var kind = match.Groups[2].Value; // "input" or "model"
            var typeParam = match.Groups[3].Success ? match.Groups[3].Value : null;

            members.Add(new ParsedMember
            {
                Name = name,
                Kind = kind == "model" ? MemberKind.ModelSignal : MemberKind.SignalInput,
                TypeSignature = typeParam
            });
        }
        return members;
    }

    private List<ParsedMember> ParseInjectCalls(string blockContent, string filePath, int classLine)
    {
        var members = new List<ParsedMember>();
        foreach (Match match in InjectCallRegex().Matches(blockContent))
        {
            var propertyName = match.Groups[1].Value;
            var typeParam = match.Groups[2].Success ? match.Groups[2].Value : null;
            var tokenArg = match.Groups[3].Success ? match.Groups[3].Value : null;

            members.Add(new ParsedMember
            {
                Name = propertyName,
                Kind = MemberKind.InjectedDependency,
                TypeSignature = typeParam ?? tokenArg
            });
        }
        return members;
    }

    private static string? FindDecoratorAbove(string[] lines, int lineIndex)
    {
        // Walk backwards from the line to find Angular decorator
        for (var i = lineIndex - 1; i >= Math.Max(0, lineIndex - 20); i--)
        {
            var trimmed = lines[i].Trim();

            var match = Regex.Match(trimmed, @"^@(Component|Injectable|Directive|Pipe|NgModule)\b");
            if (match.Success)
            {
                return match.Groups[1].Value;
            }

            // Stop if we hit a line that isn't decorator-related
            if (!string.IsNullOrEmpty(trimmed) &&
                !trimmed.StartsWith('@') &&
                !trimmed.StartsWith('*') &&
                !trimmed.StartsWith("//") &&
                !trimmed.StartsWith("/*") &&
                !trimmed.EndsWith("*/") &&
                !trimmed.EndsWith(",") &&
                !trimmed.EndsWith("{") &&
                !trimmed.EndsWith("}") &&
                !trimmed.EndsWith("(") &&
                !trimmed.EndsWith(")"))
            {
                break;
            }
        }

        return null;
    }

    private static Dictionary<string, string>? ExtractDecoratorMetadata(string content, int classLineNumber)
    {
        // Find the decorator before the class line and extract key-value pairs
        var lines = content.Split('\n');
        var startLineIndex = classLineNumber - 2; // 0-based, line before class

        // Walk back to find @Component/etc
        var decoratorStart = -1;
        for (var i = startLineIndex; i >= Math.Max(0, startLineIndex - 30); i--)
        {
            if (DecoratorRegex().IsMatch(lines[i]))
            {
                decoratorStart = i;
                break;
            }
        }

        if (decoratorStart < 0)
            return null;

        // Reconstruct decorator text from decoratorStart to classLine
        var decoratorText = string.Join('\n', lines[decoratorStart..classLineNumber]);

        var metadata = new Dictionary<string, string>();

        // Extract selector
        var selectorMatch = SelectorRegex().Match(decoratorText);
        if (selectorMatch.Success)
        {
            metadata["selector"] = selectorMatch.Groups[1].Value;
        }

        // Extract providedIn
        var providedInMatch = Regex.Match(decoratorText, @"providedIn\s*:\s*['""]([^'""]+)['""]");
        if (providedInMatch.Success)
        {
            metadata["providedIn"] = providedInMatch.Groups[1].Value;
        }

        // Extract standalone
        var standaloneMatch = Regex.Match(decoratorText, @"standalone\s*:\s*(true|false)");
        if (standaloneMatch.Success)
        {
            metadata["standalone"] = standaloneMatch.Groups[1].Value;
        }

        // Extract templateUrl
        var templateUrlMatch = Regex.Match(decoratorText, @"templateUrl\s*:\s*['""]([^'""]+)['""]");
        if (templateUrlMatch.Success)
        {
            metadata["templateUrl"] = templateUrlMatch.Groups[1].Value;
        }

        return metadata.Count > 0 ? metadata : null;
    }

    private static int GetLineNumber(string content, int charIndex)
    {
        var line = 1;
        for (var i = 0; i < charIndex && i < content.Length; i++)
        {
            if (content[i] == '\n')
                line++;
        }
        return line;
    }

    /// <summary>
    /// Extracts the content of a brace-delimited block starting at the given index (the opening brace).
    /// </summary>
    private static string ExtractBlock(string content, int openBraceIndex)
    {
        if (openBraceIndex >= content.Length || content[openBraceIndex] != '{')
        {
            return string.Empty;
        }

        var depth = 0;
        var start = openBraceIndex;

        for (var i = openBraceIndex; i < content.Length; i++)
        {
            switch (content[i])
            {
                case '{':
                    depth++;
                    break;
                case '}':
                    depth--;
                    if (depth == 0)
                    {
                        return content[(start + 1)..i];
                    }
                    break;
            }
        }

        // Unmatched brace — return what we have
        return content[(start + 1)..];
    }
}

#region Parsed models

/// <summary>
/// Represents a parsed top-level TypeScript declaration.
/// </summary>
public record ParsedDeclaration
{
    public required string Name { get; init; }
    public DeclarationKind Kind { get; init; }
    public required string FilePath { get; init; }
    public int LineNumber { get; init; }
    public string[] Extends { get; init; } = [];
    public string[] Implements { get; init; } = [];
    public IReadOnlyList<ParsedMember> Members { get; init; } = [];
    public string? Decorator { get; init; }
    public Dictionary<string, string>? DecoratorMetadata { get; init; }
    public string? TypeSignature { get; init; }
    public string? Documentation { get; init; }
}

public enum DeclarationKind
{
    Interface,
    Class,
    AbstractClass,
    Enum,
    TypeAlias
}

/// <summary>
/// Represents a member (property, method, binding) within a declaration.
/// </summary>
public record ParsedMember
{
    public required string Name { get; init; }
    public MemberKind Kind { get; init; }
    public string? TypeSignature { get; init; }
}

public enum MemberKind
{
    Property,
    Method,
    InputBinding,
    OutputBinding,
    SignalInput,
    ModelSignal,
    InjectedDependency
}

#endregion
