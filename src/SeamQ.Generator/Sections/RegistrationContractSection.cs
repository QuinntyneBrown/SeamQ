using SeamQ.Core.Models;

namespace SeamQ.Generator.Sections;

public class RegistrationContractSection : IIcdSection
{
    public string Title => "Registration Contract";
    public int Order => 40;

    public Task<string> GenerateAsync(Seam seam, CancellationToken cancellationToken = default)
    {
        var tokens = seam.ContractSurface.InjectionTokens.ToList();
        var abstractClasses = seam.ContractSurface.AbstractClasses.ToList();

        if (tokens.Count == 0 && abstractClasses.Count == 0)
        {
            return Task.FromResult("""
                ## 6. Registration Contract

                *No injection tokens or factory registrations detected for this seam.*
                """);
        }

        var tokenRows = tokens.Select(t =>
            $"| `{EscapePipe(t.Name)}` | InjectionToken | `{EscapePipe(t.TypeSignature ?? "unknown")}` | `{EscapePipe(t.SourceFile)}:{t.LineNumber}` |");

        var factoryRows = abstractClasses.Select(a =>
            $"| `{EscapePipe(a.Name)}` | AbstractClass | `{EscapePipe(a.TypeSignature ?? "abstract")}` | `{EscapePipe(a.SourceFile)}:{a.LineNumber}` |");

        var allRows = string.Join("\n", tokenRows.Concat(factoryRows));

        var md = $"""
            ## 6. Registration Contract

            Dependency injection tokens and abstract factory registrations that must be provided at module
            bootstrap time.

            | Name | Kind | Type / Signature | Source |
            |------|------|-----------------|--------|
            {allRows}
            """;

        return Task.FromResult(md);
    }

    private static string EscapePipe(string value) => value.Replace("|", "\\|");
}
