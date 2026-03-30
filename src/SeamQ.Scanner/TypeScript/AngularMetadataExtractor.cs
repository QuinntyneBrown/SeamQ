using Microsoft.Extensions.Logging;
using SeamQ.Core.Models;

namespace SeamQ.Scanner.TypeScript;

/// <summary>
/// Extracts Angular-specific metadata from parsed TypeScript declarations,
/// converting them to ExportedSymbol instances with rich kind information.
/// Handles selectors, providers, inputs/outputs, and dependency injection.
/// </summary>
public class AngularMetadataExtractor
{
    private readonly ILogger<AngularMetadataExtractor> _logger;

    public AngularMetadataExtractor(ILogger<AngularMetadataExtractor> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Converts parsed declarations from a file into exported symbols with Angular-aware kind annotations.
    /// </summary>
    public IReadOnlyList<ExportedSymbol> ExtractSymbols(IReadOnlyList<ParsedDeclaration> declarations)
    {
        var symbols = new List<ExportedSymbol>();

        foreach (var decl in declarations)
        {
            try
            {
                symbols.AddRange(ExtractFromDeclaration(decl));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to extract symbols from declaration {Name}", decl.Name);
            }
        }

        return symbols;
    }

    private IEnumerable<ExportedSymbol> ExtractFromDeclaration(ParsedDeclaration decl)
    {
        // Emit the top-level declaration itself
        yield return new ExportedSymbol
        {
            Name = decl.Name,
            FilePath = decl.FilePath,
            LineNumber = decl.LineNumber,
            Kind = GetDeclarationKind(decl)
        };

        // For classes with Angular decorators, emit the selector as a separate symbol
        if (decl.Decorator is not null &&
            decl.DecoratorMetadata?.TryGetValue("selector", out var selector) == true)
        {
            yield return new ExportedSymbol
            {
                Name = selector,
                FilePath = decl.FilePath,
                LineNumber = decl.LineNumber,
                Kind = $"{decl.Decorator}Selector"
            };
        }

        // Emit input/output/signal bindings as individual symbols
        foreach (var member in decl.Members)
        {
            var memberKind = member.Kind switch
            {
                MemberKind.InputBinding => "InputBinding",
                MemberKind.OutputBinding => "OutputBinding",
                MemberKind.SignalInput => "SignalInput",
                MemberKind.ModelSignal => "ModelSignal",
                MemberKind.InjectedDependency => "InjectedDependency",
                _ => null
            };

            if (memberKind is not null)
            {
                yield return new ExportedSymbol
                {
                    Name = $"{decl.Name}.{member.Name}",
                    FilePath = decl.FilePath,
                    LineNumber = decl.LineNumber,
                    Kind = memberKind
                };
            }
        }
    }

    private static string GetDeclarationKind(ParsedDeclaration decl)
    {
        if (decl.Decorator is not null)
        {
            return decl.Decorator; // "Component", "Injectable", "Directive", "Pipe"
        }

        return decl.Kind switch
        {
            DeclarationKind.Interface => "Interface",
            DeclarationKind.AbstractClass => "AbstractClass",
            DeclarationKind.Class => "Class",
            DeclarationKind.Enum => "Enum",
            DeclarationKind.TypeAlias => "TypeAlias",
            _ => "Unknown"
        };
    }

    /// <summary>
    /// Extracts a flat list of provider tokens from inject() calls in parsed declarations.
    /// Useful for understanding the dependency graph of a component or service.
    /// </summary>
    public IReadOnlyList<string> ExtractProviderTokens(IReadOnlyList<ParsedDeclaration> declarations)
    {
        return declarations
            .SelectMany(d => d.Members)
            .Where(m => m.Kind == MemberKind.InjectedDependency && m.TypeSignature is not null)
            .Select(m => m.TypeSignature!)
            .Distinct()
            .ToList();
    }

    /// <summary>
    /// Extracts input binding names for a given component/directive declaration.
    /// </summary>
    public IReadOnlyList<string> ExtractInputNames(ParsedDeclaration declaration)
    {
        return declaration.Members
            .Where(m => m.Kind is MemberKind.InputBinding or MemberKind.SignalInput or MemberKind.ModelSignal)
            .Select(m => m.Name)
            .ToList();
    }

    /// <summary>
    /// Extracts output binding names for a given component/directive declaration.
    /// </summary>
    public IReadOnlyList<string> ExtractOutputNames(ParsedDeclaration declaration)
    {
        return declaration.Members
            .Where(m => m.Kind == MemberKind.OutputBinding)
            .Select(m => m.Name)
            .ToList();
    }
}
